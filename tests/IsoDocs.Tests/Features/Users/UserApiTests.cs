using System.Net;
using System.Net.Http.Json;
using IsoDocument.Api.Common;
using IsoDocument.Api.Data.Entities;
using IsoDocument.Api.Features.Auth;
using IsoDocument.Api.Features.Auth.Dtos;
using IsoDocument.Api.Features.AuditLogs;
using IsoDocument.Api.Features.Users;
using IsoDocument.Api.Features.Users.Dtos;
using IsoDocument.Api.Security;
using IsoDocs.Tests.Features.Auth;
using IsoDocs.Tests.Features.AuditLogs;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Xunit;

namespace IsoDocs.Tests.Features.Users;

public sealed class UserApiTests
{
    [Fact]
    public async Task Create_WithValidRequest_ReturnsCreatedUserAndAcceptsShortPassword()
    {
        await using var factory = new UserWebApplicationFactory(UserRole.COMPANY_ADMIN);
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);
        var token = await GetAntiforgeryTokenAsync(client);
        using var request = CreateWriteRequest(
            HttpMethod.Post,
            "/api/users",
            token,
            new CreateUserRequest(
                " EMP100 ", " New User ", "user@example.com", factory.CompanyA,
                factory.DeptA, "USER", "1", "1"));

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<UserResponse>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("EMP100", body.Empno);
        Assert.Equal("New User", body.Name);
        Assert.True(body.IsActive);
        var stored = Assert.Single(factory.UserStore.Users, user => user.Id == body.Id);
        Assert.NotNull(stored.PasswordDigest);
        Assert.NotEqual(
            PasswordVerificationResult.Failed,
            new PasswordHasher<User>().VerifyHashedPassword(stored, stored.PasswordDigest, "1"));
        AssertAudit(factory.Audit, AuditActions.CreateUser, body.Id);
    }

    [Fact]
    public async Task Create_WithDuplicateEmpno_ReturnsValidationError()
    {
        await using var factory = new UserWebApplicationFactory(UserRole.COMPANY_ADMIN);
        factory.UserStore.AddSeed(factory.CompanyA, factory.DeptA, "EMP100", "Existing User");
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);
        var token = await GetAntiforgeryTokenAsync(client);
        using var request = CreateWriteRequest(
            HttpMethod.Post,
            "/api/users",
            token,
            new CreateUserRequest(
                "EMP100", "Duplicate", null, factory.CompanyA,
                factory.DeptA, "USER", null, null));

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(body);
        Assert.Contains("empno", body.Errors.Keys);
        Assert.Contains("已被使用", body.Errors["empno"].Single(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateSystemAdmin_AsCompanyAdmin_ReturnsForbidden()
    {
        await using var factory = new UserWebApplicationFactory(UserRole.COMPANY_ADMIN);
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);
        var token = await GetAntiforgeryTokenAsync(client);
        using var request = CreateWriteRequest(
            HttpMethod.Post,
            "/api/users",
            token,
            new CreateUserRequest(
                "SYS100", "System Admin", null, factory.CompanyA,
                factory.DeptA, "SYSTEM_ADMIN", null, null));

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.DoesNotContain(factory.UserStore.Users, user => user.Empno == "SYS100");
    }

    [Fact]
    public async Task Delete_SoftDeletesUserAndListOnlyIncludesItWhenRequested()
    {
        await using var factory = new UserWebApplicationFactory(UserRole.COMPANY_ADMIN);
        var user = factory.UserStore.AddSeed(factory.CompanyA, factory.DeptA, "EMP200", "Delete Me");
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);
        var token = await GetAntiforgeryTokenAsync(client);
        using var request = CreateWriteRequest<object?>(
            HttpMethod.Delete, $"/api/users/{user.Id}", token, body: null);

        var deleteResponse = await client.SendAsync(request);
        var getResponse = await client.GetAsync($"/api/users/{user.Id}");
        var fetched = await getResponse.Content.ReadFromJsonAsync<UserResponse>();
        var defaultList = await client.GetFromJsonAsync<PagedResult<UserResponse>>("/api/users");
        var fullList = await client.GetFromJsonAsync<PagedResult<UserResponse>>(
            "/api/users?includeInactive=true");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.NotNull(fetched);
        Assert.False(fetched.IsActive);
        Assert.Contains(factory.UserStore.Users, candidate => candidate.Id == user.Id);
        Assert.DoesNotContain(defaultList!.Items, candidate => candidate.Id == user.Id);
        Assert.Contains(fullList!.Items, candidate => candidate.Id == user.Id && !candidate.IsActive);
        AssertAudit(factory.Audit, AuditActions.DeleteUser, user.Id);
    }

    [Fact]
    public async Task ResetPassword_SetsMustChangePasswordAndReturnsUsableTemporaryPassword()
    {
        await using var factory = new UserWebApplicationFactory(UserRole.COMPANY_ADMIN);
        var user = factory.UserStore.AddSeed(factory.CompanyA, factory.DeptA, "EMP300", "Reset Me");
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);
        var token = await GetAntiforgeryTokenAsync(client);
        using var request = CreateWriteRequest<object?>(
            HttpMethod.Post, $"/api/users/{user.Id}/reset-password", token, body: null);

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<ResetPasswordResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body.TemporaryPassword));
        Assert.True(user.MustChangePassword);
        Assert.NotNull(user.PasswordDigest);
        Assert.NotEqual(
            PasswordVerificationResult.Failed,
            new PasswordHasher<User>().VerifyHashedPassword(
                user, user.PasswordDigest, body.TemporaryPassword));
        AssertAudit(factory.Audit, AuditActions.ResetUserPassword, user.Id);
    }

    [Fact]
    public async Task Update_WithValidRequest_UpdatesUser()
    {
        await using var factory = new UserWebApplicationFactory(UserRole.COMPANY_ADMIN);
        var user = factory.UserStore.AddSeed(factory.CompanyA, factory.DeptA, "EMP400", "Old Name");
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);
        var token = await GetAntiforgeryTokenAsync(client);
        using var request = CreateWriteRequest(
            HttpMethod.Put,
            $"/api/users/{user.Id}",
            token,
            new UpdateUserRequest(
                "Updated Name", "updated@example.com", factory.DeptA,
                "COMPANY_ADMIN", true, false));

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<UserResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("Updated Name", body.Name);
        Assert.Equal("COMPANY_ADMIN", body.Role);
        Assert.False(body.NotifyEmailEnabled);
        AssertAudit(factory.Audit, AuditActions.UpdateUser, user.Id);
    }

    [Fact]
    public async Task UpdateSystemAdmin_AsCompanyAdmin_ReturnsForbidden()
    {
        await using var factory = new UserWebApplicationFactory(UserRole.COMPANY_ADMIN);
        var user = factory.UserStore.AddSeed(
            factory.CompanyA, factory.DeptA, "SYS400", "System Admin", UserRole.SYSTEM_ADMIN);
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);
        var token = await GetAntiforgeryTokenAsync(client);
        using var request = CreateWriteRequest(
            HttpMethod.Put,
            $"/api/users/{user.Id}",
            token,
            new UpdateUserRequest(
                "Changed", null, factory.DeptA, "SYSTEM_ADMIN", true, true));

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("System Admin", user.Name);
    }

    [Fact]
    public async Task Get_WithUnknownId_ReturnsNotFound()
    {
        await using var factory = new UserWebApplicationFactory(UserRole.SYSTEM_ADMIN);
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);

        var response = await client.GetAsync($"/api/users/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_WithUnknownId_ReturnsNotFound()
    {
        await using var factory = new UserWebApplicationFactory(UserRole.SYSTEM_ADMIN);
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);
        var token = await GetAntiforgeryTokenAsync(client);
        using var request = CreateWriteRequest<object?>(
            HttpMethod.Delete, $"/api/users/{Guid.NewGuid()}", token, body: null);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_WithUnknownId_ReturnsNotFound()
    {
        await using var factory = new UserWebApplicationFactory(UserRole.SYSTEM_ADMIN);
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);
        var token = await GetAntiforgeryTokenAsync(client);
        using var request = CreateWriteRequest<object?>(
            HttpMethod.Post, $"/api/users/{Guid.NewGuid()}/reset-password", token, body: null);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static void AssertAudit(
        RecordingOperationAuditLogService audit,
        string action,
        Guid resourceId)
    {
        var entry = Assert.Single(audit.Entries);
        Assert.Equal(action, entry.Action);
        Assert.Equal(AuditResourceTypes.User, entry.ResourceType);
        Assert.Equal(resourceId, entry.ResourceId);
        Assert.NotNull(entry.Detail);
    }

    private static async Task LoginAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("EMP001", AuthWebApplicationFactory.InitialPassword));
        response.EnsureSuccessStatusCode();
    }

    private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/antiforgery/token");
        response.EnsureSuccessStatusCode();
        var cookie = response.Headers.GetValues("Set-Cookie")
            .Single(value => value.StartsWith("isodocs.xsrf=", StringComparison.Ordinal));
        return Uri.UnescapeDataString(
            cookie["isodocs.xsrf=".Length..cookie.IndexOf(';')]);
    }

    private static HttpRequestMessage CreateWriteRequest<T>(
        HttpMethod method,
        string uri,
        string token,
        T body)
    {
        var request = new HttpRequestMessage(method, uri);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        request.Headers.Add("X-XSRF-TOKEN", token);
        return request;
    }
}

internal sealed class UserWebApplicationFactory : WebApplicationFactory<Program>
{
    public UserWebApplicationFactory(UserRole actorRole)
    {
        AuthStore = new FakeAuthUserStore();
        AuthStore.User.Role = actorRole.ToString();
        CompanyA = AuthStore.User.CompanyId;
        CompanyB = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        DeptA = AuthStore.User.DeptId;
        DeptB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        UserStore = new FakeUserStore(
            [CompanyA, CompanyB],
            new Dictionary<Guid, Guid> { [DeptA] = CompanyA, [DeptB] = CompanyB });
        Audit = new RecordingOperationAuditLogService();
    }

    public Guid CompanyA { get; }
    public Guid CompanyB { get; }
    public Guid DeptA { get; }
    public Guid DeptB { get; }
    public FakeAuthUserStore AuthStore { get; }
    public FakeUserStore UserStore { get; }
    public RecordingOperationAuditLogService Audit { get; }

    public HttpClient CreateSecureClient() => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        HandleCookies = true,
        AllowAutoRedirect = false
    });

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureTestServices(services =>
        {
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
            services.RemoveAll<IAuthUserStore>();
            services.AddSingleton<IAuthUserStore>(AuthStore);
            services.RemoveAll<IUserStore>();
            services.AddSingleton<IUserStore>(UserStore);
            services.RemoveAll<IOperationAuditLogService>();
            services.AddSingleton<IOperationAuditLogService>(Audit);
        });
    }
}

internal sealed class FakeUserStore(
    IEnumerable<Guid> companyIds,
    IReadOnlyDictionary<Guid, Guid> deptCompanies) : IUserStore
{
    private readonly HashSet<Guid> _companyIds = [.. companyIds];

    public List<User> Users { get; } = [];

    public User AddSeed(
        Guid companyId,
        Guid deptId,
        string empno,
        string name,
        UserRole role = UserRole.USER)
    {
        var now = DateTimeOffset.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            DeptId = deptId,
            Empno = empno,
            Name = name,
            Role = role.ToString(),
            IsActive = true,
            NotifyEmailEnabled = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        Users.Add(user);
        return user;
    }

    public Task<bool> CompanyExistsAsync(Guid companyId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_companyIds.Contains(companyId));
    }

    public Task<bool> DeptBelongsToCompanyAsync(
        Guid deptId,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(
            deptCompanies.TryGetValue(deptId, out var ownerCompanyId)
            && ownerCompanyId == companyId);
    }

    public Task<bool> EmpnoExistsAsync(string empno, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Users.Any(user => user.Empno == empno));
    }

    public Task<int> CountAsync(
        Guid? companyId,
        Guid? deptId,
        string? keyword,
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Filter(companyId, deptId, keyword, includeInactive).Count());
    }

    public Task<IReadOnlyList<User>> ListAsync(
        Guid? companyId,
        Guid? deptId,
        string? keyword,
        bool includeInactive,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<User> users = Filter(companyId, deptId, keyword, includeInactive)
            .OrderBy(user => user.Empno)
            .ThenBy(user => user.Id)
            .Skip(skip)
            .Take(take)
            .ToArray();
        return Task.FromResult(users);
    }

    public Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Users.SingleOrDefault(user => user.Id == id));
    }

    public void Add(User user) => Users.Add(user);

    public void Detach(User user) => Users.Remove(user);

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    private IEnumerable<User> Filter(
        Guid? companyId,
        Guid? deptId,
        string? keyword,
        bool includeInactive) => Users.Where(user =>
            (!companyId.HasValue || user.CompanyId == companyId.Value)
            && (!deptId.HasValue || user.DeptId == deptId.Value)
            && (includeInactive || user.IsActive)
            && (string.IsNullOrWhiteSpace(keyword)
                || user.Empno.Contains(keyword.Trim(), StringComparison.OrdinalIgnoreCase)
                || user.Name.Contains(keyword.Trim(), StringComparison.OrdinalIgnoreCase)
                || (user.Email?.Contains(keyword.Trim(), StringComparison.OrdinalIgnoreCase) ?? false)));
}

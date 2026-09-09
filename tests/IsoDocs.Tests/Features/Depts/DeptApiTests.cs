using System.Net;
using System.Net.Http.Json;
using IsoDocument.Api.Common;
using IsoDocument.Api.Data.Entities;
using IsoDocument.Api.Features.Auth;
using IsoDocument.Api.Features.Auth.Dtos;
using IsoDocument.Api.Features.Depts;
using IsoDocument.Api.Features.Depts.Dtos;
using IsoDocument.Api.Security;
using IsoDocs.Tests.Features.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Xunit;

namespace IsoDocs.Tests.Features.Depts;

public sealed class DeptApiTests
{
    [Fact]
    public async Task Create_WithValidRequest_ReturnsCreatedDepartment()
    {
        await using var factory = new DeptWebApplicationFactory(UserRole.COMPANY_ADMIN);
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);
        var token = await GetAntiforgeryTokenAsync(client);
        using var request = CreateWriteRequest(
            HttpMethod.Post,
            "/api/depts",
            token,
            new CreateDeptRequest(factory.CompanyA, " Quality ", 10));

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<DeptResponse>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(factory.CompanyA, body.CompanyId);
        Assert.Equal("Quality", body.Name);
        Assert.Equal(10, body.Seq);
        Assert.Contains(factory.DeptStore.Depts, dept => dept.Id == body.Id);
    }

    [Fact]
    public async Task Create_WithDuplicateNameInSameCompany_ReturnsConflict()
    {
        await using var factory = new DeptWebApplicationFactory(UserRole.COMPANY_ADMIN);
        factory.DeptStore.AddSeed(factory.CompanyA, "Quality", 10);
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);
        var token = await GetAntiforgeryTokenAsync(client);
        using var request = CreateWriteRequest(
            HttpMethod.Post,
            "/api/depts",
            token,
            new CreateDeptRequest(factory.CompanyA, "Quality", null));

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.NotNull(body);
        Assert.Contains("same name", body.Detail, StringComparison.OrdinalIgnoreCase);
        Assert.Single(factory.DeptStore.Depts, dept => dept.Name == "Quality");
    }

    [Fact]
    public async Task Delete_WhenDepartmentHasActiveUsers_ReturnsConflictAndKeepsDepartment()
    {
        await using var factory = new DeptWebApplicationFactory(UserRole.COMPANY_ADMIN);
        var dept = factory.DeptStore.AddSeed(factory.CompanyA, "Operations", 20);
        factory.DeptStore.ActiveUserDeptIds.Add(dept.Id);
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);
        var token = await GetAntiforgeryTokenAsync(client);
        using var request = CreateWriteRequest<object?>(
            HttpMethod.Delete,
            $"/api/depts/{dept.Id}",
            token,
            body: null);

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.NotNull(body);
        Assert.Contains("active users", body.Detail, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(factory.DeptStore.Depts, candidate => candidate.Id == dept.Id);
        Assert.Contains(dept.Id, factory.DeptStore.ActiveUserDeptIds);
    }

    [Fact]
    public async Task Delete_WhenDepartmentHasNoUsers_RemovesDepartment()
    {
        await using var factory = new DeptWebApplicationFactory(UserRole.COMPANY_ADMIN);
        var dept = factory.DeptStore.AddSeed(factory.CompanyA, "Operations", 20);
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);
        var token = await GetAntiforgeryTokenAsync(client);
        using var request = CreateWriteRequest<object?>(
            HttpMethod.Delete,
            $"/api/depts/{dept.Id}",
            token,
            body: null);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.DoesNotContain(factory.DeptStore.Depts, candidate => candidate.Id == dept.Id);
    }

    [Fact]
    public async Task List_AsSystemAdmin_ReturnsDepartmentsAcrossCompanies()
    {
        await using var factory = new DeptWebApplicationFactory(UserRole.SYSTEM_ADMIN);
        factory.DeptStore.AddSeed(factory.CompanyA, "Company A Dept", 1);
        factory.DeptStore.AddSeed(factory.CompanyB, "Company B Dept", 1);
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);

        var response = await client.GetAsync("/api/depts");
        var body = await response.Content.ReadFromJsonAsync<PagedResult<DeptResponse>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(2, body.TotalCount);
        Assert.Contains(body.Items, dept => dept.CompanyId == factory.CompanyA);
        Assert.Contains(body.Items, dept => dept.CompanyId == factory.CompanyB);
    }

    [Fact]
    public async Task List_AsCompanyAdmin_ReturnsOnlyOwnCompanyDepartments()
    {
        await using var factory = new DeptWebApplicationFactory(UserRole.COMPANY_ADMIN);
        factory.DeptStore.AddSeed(factory.CompanyA, "Company A Dept", 1);
        factory.DeptStore.AddSeed(factory.CompanyB, "Company B Dept", 1);
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);

        var response = await client.GetAsync("/api/depts");
        var body = await response.Content.ReadFromJsonAsync<PagedResult<DeptResponse>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Single(body.Items);
        Assert.All(body.Items, dept => Assert.Equal(factory.CompanyA, dept.CompanyId));
    }

    [Fact]
    public async Task List_AsCompanyAdminRequestingAnotherCompany_ReturnsForbidden()
    {
        await using var factory = new DeptWebApplicationFactory(UserRole.COMPANY_ADMIN);
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);

        var response = await client.GetAsync($"/api/depts?companyId={factory.CompanyB}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Get_WithExistingDepartment_ReturnsDepartment()
    {
        await using var factory = new DeptWebApplicationFactory(UserRole.COMPANY_ADMIN);
        var dept = factory.DeptStore.AddSeed(factory.CompanyA, "Operations", 20);
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);

        var response = await client.GetAsync($"/api/depts/{dept.Id}");
        var body = await response.Content.ReadFromJsonAsync<DeptResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(dept.Id, body.Id);
    }

    [Fact]
    public async Task Get_WithUnknownDepartment_ReturnsNotFound()
    {
        await using var factory = new DeptWebApplicationFactory(UserRole.COMPANY_ADMIN);
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);

        var response = await client.GetAsync($"/api/depts/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_WithValidRequest_UpdatesDepartment()
    {
        await using var factory = new DeptWebApplicationFactory(UserRole.COMPANY_ADMIN);
        var dept = factory.DeptStore.AddSeed(factory.CompanyA, "Operations", 20);
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);
        var token = await GetAntiforgeryTokenAsync(client);
        using var request = CreateWriteRequest(
            HttpMethod.Put,
            $"/api/depts/{dept.Id}",
            token,
            new UpdateDeptRequest(" Production ", 30));

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<DeptResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("Production", body.Name);
        Assert.Equal(30, body.Seq);
    }

    [Fact]
    public async Task Update_WithDuplicateNameInSameCompany_ReturnsConflict()
    {
        await using var factory = new DeptWebApplicationFactory(UserRole.COMPANY_ADMIN);
        var dept = factory.DeptStore.AddSeed(factory.CompanyA, "Operations", 20);
        factory.DeptStore.AddSeed(factory.CompanyA, "Quality", 10);
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);
        var token = await GetAntiforgeryTokenAsync(client);
        using var request = CreateWriteRequest(
            HttpMethod.Put,
            $"/api/depts/{dept.Id}",
            token,
            new UpdateDeptRequest("Quality", 30));

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("Operations", dept.Name);
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
        var cookie = response.Headers
            .GetValues("Set-Cookie")
            .Single(value => value.StartsWith("isodocs.xsrf=", StringComparison.Ordinal));
        var encodedToken = cookie["isodocs.xsrf=".Length..cookie.IndexOf(';')];
        return Uri.UnescapeDataString(encodedToken);
    }

    private static HttpRequestMessage CreateWriteRequest<T>(
        HttpMethod method,
        string uri,
        string antiforgeryToken,
        T body)
    {
        var request = new HttpRequestMessage(method, uri);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        request.Headers.Add("X-XSRF-TOKEN", antiforgeryToken);
        return request;
    }
}

internal sealed class DeptWebApplicationFactory : WebApplicationFactory<Program>
{
    public DeptWebApplicationFactory(UserRole role)
    {
        AuthStore = new FakeAuthUserStore();
        AuthStore.User.Role = role.ToString();
        CompanyA = AuthStore.User.CompanyId;
        CompanyB = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        DeptStore = new FakeDeptStore([CompanyA, CompanyB]);
    }

    public Guid CompanyA { get; }

    public Guid CompanyB { get; }

    public FakeAuthUserStore AuthStore { get; }

    public FakeDeptStore DeptStore { get; }

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
            services.RemoveAll<IDeptStore>();
            services.AddSingleton<IDeptStore>(DeptStore);
        });
    }
}

internal sealed class FakeDeptStore(IEnumerable<Guid> companyIds) : IDeptStore
{
    private readonly HashSet<Guid> _companyIds = [.. companyIds];

    public List<Dept> Depts { get; } = [];

    public HashSet<Guid> ActiveUserDeptIds { get; } = [];

    public Dept AddSeed(Guid companyId, string name, int? seq)
    {
        var now = DateTimeOffset.UtcNow;
        var dept = new Dept
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Name = name,
            Seq = seq,
            CreatedAt = now,
            UpdatedAt = now
        };
        Depts.Add(dept);
        return dept;
    }

    public Task<bool> CompanyExistsAsync(Guid companyId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_companyIds.Contains(companyId));
    }

    public Task<bool> NameExistsAsync(
        Guid companyId,
        string name,
        Guid? excludingDeptId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Depts.Any(dept =>
            dept.CompanyId == companyId
            && dept.Name == name
            && (!excludingDeptId.HasValue || dept.Id != excludingDeptId.Value)));
    }

    public Task<int> CountAsync(Guid? companyId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Filter(companyId).Count());
    }

    public Task<IReadOnlyList<Dept>> ListAsync(
        Guid? companyId,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<Dept> result = Filter(companyId)
            .OrderBy(dept => dept.Seq is null)
            .ThenBy(dept => dept.Seq)
            .ThenBy(dept => dept.Name)
            .ThenBy(dept => dept.Id)
            .Skip(skip)
            .Take(take)
            .ToArray();
        return Task.FromResult(result);
    }

    public Task<Dept?> FindByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Depts.SingleOrDefault(dept => dept.Id == id));
    }

    public Task<bool> HasActiveUsersAsync(Guid deptId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(ActiveUserDeptIds.Contains(deptId));
    }

    public void Add(Dept dept) => Depts.Add(dept);

    public void Remove(Dept dept) => Depts.Remove(dept);

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    private IEnumerable<Dept> Filter(Guid? companyId) => companyId.HasValue
        ? Depts.Where(dept => dept.CompanyId == companyId.Value)
        : Depts;
}

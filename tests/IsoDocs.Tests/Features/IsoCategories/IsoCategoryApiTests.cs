using System.Net;
using System.Net.Http.Json;
using IsoDocument.Api.Common;
using IsoDocument.Api.Data.Entities;
using IsoDocument.Api.Features.Auth;
using IsoDocument.Api.Features.Auth.Dtos;
using IsoDocument.Api.Features.AuditLogs;
using IsoDocument.Api.Features.IsoCategories;
using IsoDocument.Api.Features.IsoCategories.Dtos;
using IsoDocument.Api.Security;
using IsoDocs.Tests.Features.Auth;
using IsoDocs.Tests.Features.AuditLogs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Xunit;

namespace IsoDocs.Tests.Features.IsoCategories;

public sealed class IsoCategoryApiTests
{
    [Fact]
    public async Task Create_WithValidRequest_ReturnsCreatedCategory()
    {
        await using var factory = new IsoCategoryWebApplicationFactory(UserRole.COMPANY_ADMIN);
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);
        var token = await GetAntiforgeryTokenAsync(client);
        using var request = CreateWriteRequest(
            HttpMethod.Post,
            "/api/iso-categories",
            token,
            new CreateIsoCategoryRequest(factory.CompanyA, " ISO 9001 "));

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<IsoCategoryResponse>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(factory.CompanyA, body.CompanyId);
        Assert.Equal("ISO 9001", body.Name);
        Assert.True(body.IsActive);
        Assert.Contains(factory.IsoCategoryStore.Categories, category => category.Id == body.Id);
        AssertAudit(factory.Audit, AuditActions.CreateIsoCategory, AuditResourceTypes.IsoCategory, body.Id);
    }

    [Fact]
    public async Task Create_WithDuplicateNameInSameCompany_ReturnsConflict()
    {
        await using var factory = new IsoCategoryWebApplicationFactory(UserRole.COMPANY_ADMIN);
        factory.IsoCategoryStore.AddSeed(factory.CompanyA, "ISO 9001");
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);
        var token = await GetAntiforgeryTokenAsync(client);
        using var request = CreateWriteRequest(
            HttpMethod.Post,
            "/api/iso-categories",
            token,
            new CreateIsoCategoryRequest(factory.CompanyA, "ISO 9001"));

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.NotNull(body);
        Assert.Contains("相同名稱的品質系統", body.Detail, StringComparison.Ordinal);
        Assert.Single(factory.IsoCategoryStore.Categories, category => category.Name == "ISO 9001");
    }

    [Fact]
    public async Task Create_AsCompanyAdminForAnotherCompany_ReturnsForbidden()
    {
        await using var factory = new IsoCategoryWebApplicationFactory(UserRole.COMPANY_ADMIN);
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);
        var token = await GetAntiforgeryTokenAsync(client);
        using var request = CreateWriteRequest(
            HttpMethod.Post,
            "/api/iso-categories",
            token,
            new CreateIsoCategoryRequest(factory.CompanyB, "ISO 14001"));

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(factory.IsoCategoryStore.Categories);
    }

    [Fact]
    public async Task List_AsCompanyAdmin_ReturnsOnlyOwnCompanyActiveCategories()
    {
        await using var factory = new IsoCategoryWebApplicationFactory(UserRole.COMPANY_ADMIN);
        factory.IsoCategoryStore.AddSeed(factory.CompanyA, "Active A", isActive: true);
        factory.IsoCategoryStore.AddSeed(factory.CompanyA, "Inactive A", isActive: false);
        factory.IsoCategoryStore.AddSeed(factory.CompanyB, "Active B", isActive: true);
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);

        var response = await client.GetAsync("/api/iso-categories");
        var body = await response.Content.ReadFromJsonAsync<PagedResult<IsoCategoryResponse>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        var item = Assert.Single(body.Items);
        Assert.Equal("Active A", item.Name);
    }

    [Fact]
    public async Task List_WithIncludeInactive_ReturnsInactiveCategoriesToo()
    {
        await using var factory = new IsoCategoryWebApplicationFactory(UserRole.COMPANY_ADMIN);
        factory.IsoCategoryStore.AddSeed(factory.CompanyA, "Active A", isActive: true);
        factory.IsoCategoryStore.AddSeed(factory.CompanyA, "Inactive A", isActive: false);
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);

        var response = await client.GetAsync("/api/iso-categories?includeInactive=true");
        var body = await response.Content.ReadFromJsonAsync<PagedResult<IsoCategoryResponse>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(2, body.TotalCount);
    }

    [Fact]
    public async Task Update_WithValidRequest_UpdatesCategory()
    {
        await using var factory = new IsoCategoryWebApplicationFactory(UserRole.COMPANY_ADMIN);
        var category = factory.IsoCategoryStore.AddSeed(factory.CompanyA, "ISO 9001");
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);
        var token = await GetAntiforgeryTokenAsync(client);
        using var request = CreateWriteRequest(
            HttpMethod.Put,
            $"/api/iso-categories/{category.Id}",
            token,
            new UpdateIsoCategoryRequest(" ISO 14001 ", true));

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<IsoCategoryResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("ISO 14001", body.Name);
        AssertAudit(factory.Audit, AuditActions.UpdateIsoCategory, AuditResourceTypes.IsoCategory, category.Id);
    }

    [Fact]
    public async Task Update_CanReactivateASoftDeletedCategory()
    {
        await using var factory = new IsoCategoryWebApplicationFactory(UserRole.COMPANY_ADMIN);
        var category = factory.IsoCategoryStore.AddSeed(factory.CompanyA, "ISO 9001", isActive: false);
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);
        var token = await GetAntiforgeryTokenAsync(client);
        using var request = CreateWriteRequest(
            HttpMethod.Put,
            $"/api/iso-categories/{category.Id}",
            token,
            new UpdateIsoCategoryRequest("ISO 9001", true));

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<IsoCategoryResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.True(body.IsActive);
    }

    [Fact]
    public async Task Delete_SoftDeletesCategoryAndKeepsItQueryable()
    {
        await using var factory = new IsoCategoryWebApplicationFactory(UserRole.COMPANY_ADMIN);
        var category = factory.IsoCategoryStore.AddSeed(factory.CompanyA, "ISO 9001");
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);
        var token = await GetAntiforgeryTokenAsync(client);
        using var request = CreateWriteRequest<object?>(
            HttpMethod.Delete, $"/api/iso-categories/{category.Id}", token, body: null);

        var deleteResponse = await client.SendAsync(request);
        var getResponse = await client.GetAsync($"/api/iso-categories/{category.Id}");
        var body = await getResponse.Content.ReadFromJsonAsync<IsoCategoryResponse>();

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.NotNull(body);
        Assert.False(body.IsActive);
        Assert.Contains(factory.IsoCategoryStore.Categories, candidate => candidate.Id == category.Id);
        AssertAudit(factory.Audit, AuditActions.DeleteIsoCategory, AuditResourceTypes.IsoCategory, category.Id);
    }

    [Fact]
    public async Task Delete_WithUnknownCategory_ReturnsNotFound()
    {
        await using var factory = new IsoCategoryWebApplicationFactory(UserRole.SYSTEM_ADMIN);
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);
        var token = await GetAntiforgeryTokenAsync(client);
        using var request = CreateWriteRequest<object?>(
            HttpMethod.Delete, $"/api/iso-categories/{Guid.NewGuid()}", token, body: null);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static void AssertAudit(
        RecordingOperationAuditLogService audit,
        string action,
        string resourceType,
        Guid resourceId)
    {
        var entry = Assert.Single(audit.Entries);
        Assert.Equal(action, entry.Action);
        Assert.Equal(resourceType, entry.ResourceType);
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

internal sealed class IsoCategoryWebApplicationFactory : WebApplicationFactory<Program>
{
    public IsoCategoryWebApplicationFactory(UserRole role)
    {
        AuthStore = new FakeAuthUserStore();
        AuthStore.User.Role = role.ToString();
        CompanyA = AuthStore.User.CompanyId;
        CompanyB = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        IsoCategoryStore = new FakeIsoCategoryStore([CompanyA, CompanyB]);
        Audit = new RecordingOperationAuditLogService();
    }

    public Guid CompanyA { get; }

    public Guid CompanyB { get; }

    public FakeAuthUserStore AuthStore { get; }

    public FakeIsoCategoryStore IsoCategoryStore { get; }

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
            services.RemoveAll<IIsoCategoryStore>();
            services.AddSingleton<IIsoCategoryStore>(IsoCategoryStore);
            services.RemoveAll<IOperationAuditLogService>();
            services.AddSingleton<IOperationAuditLogService>(Audit);
        });
    }
}

internal sealed class FakeIsoCategoryStore(IEnumerable<Guid> companyIds) : IIsoCategoryStore
{
    private readonly HashSet<Guid> _companyIds = [.. companyIds];

    public List<IsoCategory> Categories { get; } = [];

    public IsoCategory AddSeed(Guid companyId, string name, bool isActive = true)
    {
        var now = DateTimeOffset.UtcNow;
        var category = new IsoCategory
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Name = name,
            IsActive = isActive,
            CreatedAt = now,
            UpdatedAt = now
        };
        Categories.Add(category);
        return category;
    }

    public Task<bool> CompanyExistsAsync(Guid companyId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_companyIds.Contains(companyId));
    }

    public Task<bool> NameExistsAsync(
        Guid companyId,
        string name,
        Guid? excludingIsoCategoryId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Categories.Any(category =>
            category.CompanyId == companyId
            && category.Name == name
            && (!excludingIsoCategoryId.HasValue || category.Id != excludingIsoCategoryId.Value)));
    }

    public Task<int> CountAsync(Guid? companyId, bool includeInactive, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Filter(companyId, includeInactive).Count());
    }

    public Task<IReadOnlyList<IsoCategory>> ListAsync(
        Guid? companyId,
        bool includeInactive,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<IsoCategory> result = Filter(companyId, includeInactive)
            .OrderBy(category => category.Name)
            .ThenBy(category => category.Id)
            .Skip(skip)
            .Take(take)
            .ToArray();
        return Task.FromResult(result);
    }

    public Task<IsoCategory?> FindByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Categories.SingleOrDefault(category => category.Id == id));
    }

    public void Add(IsoCategory isoCategory) => Categories.Add(isoCategory);

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    private IEnumerable<IsoCategory> Filter(Guid? companyId, bool includeInactive) => Categories
        .Where(category => !companyId.HasValue || category.CompanyId == companyId.Value)
        .Where(category => includeInactive || category.IsActive);
}

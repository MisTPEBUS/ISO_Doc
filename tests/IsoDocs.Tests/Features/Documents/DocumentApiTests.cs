using System.Net;
using System.Net.Http.Json;
using IsoDocument.Api.Common;
using IsoDocument.Api.Data.Entities;
using IsoDocument.Api.Features.Auth;
using IsoDocument.Api.Features.Auth.Dtos;
using IsoDocument.Api.Features.Documents;
using IsoDocument.Api.Features.Documents.Dtos;
using IsoDocument.Api.Security;
using IsoDocs.Tests.Features.Auth;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Xunit;

namespace IsoDocs.Tests.Features.Documents;

public sealed class DocumentApiTests
{
    [Fact]
    public async Task Create_WithValidRequest_ReturnsCreatedDocument()
    {
        await using var factory = new DocumentWebApplicationFactory(UserRole.COMPANY_ADMIN);
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);
        var token = await GetAntiforgeryTokenAsync(client);
        using var request = CreateWriteRequest(
            HttpMethod.Post,
            "/api/documents",
            token,
            new CreateDocumentRequest(factory.CompanyA, " ISO-001 ", " Quality Manual "));

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<DocumentResponse>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("ISO-001", body.DocumentNo);
        Assert.Equal("Quality Manual", body.Name);
        Assert.True(body.IsActive);
        Assert.Equal(factory.AuthStore.User.Id, body.CreatedBy);
        Assert.Contains(factory.DocumentStore.Documents, document => document.Id == body.Id);
    }

    [Fact]
    public async Task Create_WithDuplicateNumberInSameCompany_ReturnsValidationError()
    {
        await using var factory = new DocumentWebApplicationFactory(UserRole.COMPANY_ADMIN);
        factory.DocumentStore.AddSeed(factory.CompanyA, "ISO-001", "Existing Manual");
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);
        var token = await GetAntiforgeryTokenAsync(client);
        using var request = CreateWriteRequest(
            HttpMethod.Post,
            "/api/documents",
            token,
            new CreateDocumentRequest(factory.CompanyA, "ISO-001", "Duplicate Manual"));

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(body);
        Assert.Contains("documentNo", body.Errors.Keys);
        Assert.Contains("already in use", body.Errors["documentNo"].Single(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task List_AsCompanyAdmin_ReturnsOnlyOwnCompanyDocuments()
    {
        await using var factory = new DocumentWebApplicationFactory(UserRole.COMPANY_ADMIN);
        factory.DocumentStore.AddSeed(factory.CompanyA, "ISO-001", "Company A Manual");
        factory.DocumentStore.AddSeed(factory.CompanyB, "ISO-002", "Company B Manual");
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);

        var response = await client.GetAsync("/api/documents");
        var body = await response.Content.ReadFromJsonAsync<PagedResult<DocumentResponse>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Single(body.Items);
        Assert.All(body.Items, document => Assert.Equal(factory.CompanyA, document.CompanyId));
    }

    [Fact]
    public async Task List_AsCompanyAdminRequestingAnotherCompany_ReturnsForbidden()
    {
        await using var factory = new DocumentWebApplicationFactory(UserRole.COMPANY_ADMIN);
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);

        var response = await client.GetAsync($"/api/documents?companyId={factory.CompanyB}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Get_WithExistingDocument_ReturnsEmptyVersionsArray()
    {
        await using var factory = new DocumentWebApplicationFactory(UserRole.COMPANY_ADMIN);
        var document = factory.DocumentStore.AddSeed(
            factory.CompanyA, "ISO-001", "Quality Manual");
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);

        var response = await client.GetAsync($"/api/documents/{document.Id}");
        var body = await response.Content.ReadFromJsonAsync<DocumentDetailResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(document.Id, body.Id);
        Assert.Empty(body.Versions);
    }

    [Fact]
    public async Task Get_WithUnknownDocument_ReturnsNotFound()
    {
        await using var factory = new DocumentWebApplicationFactory(UserRole.SYSTEM_ADMIN);
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);

        var response = await client.GetAsync($"/api/documents/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_WithValidRequest_UpdatesDocumentName()
    {
        await using var factory = new DocumentWebApplicationFactory(UserRole.COMPANY_ADMIN);
        var document = factory.DocumentStore.AddSeed(
            factory.CompanyA, "ISO-001", "Old Name");
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);
        var token = await GetAntiforgeryTokenAsync(client);
        using var request = CreateWriteRequest(
            HttpMethod.Put,
            $"/api/documents/{document.Id}",
            token,
            new UpdateDocumentRequest(" Updated Manual "));

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<DocumentResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("Updated Manual", body.Name);
        Assert.Equal("ISO-001", body.DocumentNo);
    }

    [Fact]
    public async Task Update_WithBlankName_ReturnsValidationError()
    {
        await using var factory = new DocumentWebApplicationFactory(UserRole.COMPANY_ADMIN);
        var document = factory.DocumentStore.AddSeed(
            factory.CompanyA, "ISO-001", "Original Name");
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);
        var token = await GetAntiforgeryTokenAsync(client);
        using var request = CreateWriteRequest(
            HttpMethod.Put,
            $"/api/documents/{document.Id}",
            token,
            new UpdateDocumentRequest("   "));

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Original Name", document.Name);
    }

    [Fact]
    public async Task Delete_SoftDeletesDocumentAndKeepsItQueryable()
    {
        await using var factory = new DocumentWebApplicationFactory(UserRole.COMPANY_ADMIN);
        var document = factory.DocumentStore.AddSeed(
            factory.CompanyA, "ISO-001", "Quality Manual");
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);
        var token = await GetAntiforgeryTokenAsync(client);
        using var request = CreateWriteRequest<object?>(
            HttpMethod.Delete, $"/api/documents/{document.Id}", token, body: null);

        var deleteResponse = await client.SendAsync(request);
        var getResponse = await client.GetAsync($"/api/documents/{document.Id}");
        var body = await getResponse.Content.ReadFromJsonAsync<DocumentDetailResponse>();

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.NotNull(body);
        Assert.False(body.IsActive);
        Assert.Contains(factory.DocumentStore.Documents, candidate => candidate.Id == document.Id);
    }

    [Fact]
    public async Task Delete_WithUnknownDocument_ReturnsNotFound()
    {
        await using var factory = new DocumentWebApplicationFactory(UserRole.SYSTEM_ADMIN);
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);
        var token = await GetAntiforgeryTokenAsync(client);
        using var request = CreateWriteRequest<object?>(
            HttpMethod.Delete, $"/api/documents/{Guid.NewGuid()}", token, body: null);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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

internal sealed class DocumentWebApplicationFactory : WebApplicationFactory<Program>
{
    public DocumentWebApplicationFactory(UserRole actorRole)
    {
        AuthStore = new FakeAuthUserStore();
        AuthStore.User.Role = actorRole.ToString();
        CompanyA = AuthStore.User.CompanyId;
        CompanyB = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        DocumentStore = new FakeDocumentStore([CompanyA, CompanyB]);
    }

    public Guid CompanyA { get; }
    public Guid CompanyB { get; }
    public FakeAuthUserStore AuthStore { get; }
    public FakeDocumentStore DocumentStore { get; }

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
            services.RemoveAll<IDocumentStore>();
            services.AddSingleton<IDocumentStore>(DocumentStore);
        });
    }
}

internal sealed class FakeDocumentStore(IEnumerable<Guid> companyIds) : IDocumentStore
{
    private readonly HashSet<Guid> _companyIds = [.. companyIds];
    private readonly Dictionary<Guid, List<DocumentVersion>> _versions = [];

    public List<Document> Documents { get; } = [];

    public Document AddSeed(Guid companyId, string documentNo, string name)
    {
        var now = DateTimeOffset.UtcNow;
        var document = new Document
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            DocumentNo = documentNo,
            Name = name,
            IsActive = true,
            CreatedBy = Guid.NewGuid(),
            CreatedAt = now,
            UpdatedAt = now
        };
        Documents.Add(document);
        return document;
    }

    public Task<bool> CompanyExistsAsync(Guid companyId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_companyIds.Contains(companyId));
    }

    public Task<bool> DocumentNoExistsAsync(
        Guid companyId,
        string documentNo,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Documents.Any(document =>
            document.CompanyId == companyId && document.DocumentNo == documentNo));
    }

    public Task<int> CountAsync(
        Guid? companyId,
        string? keyword,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Filter(companyId, keyword).Count());
    }

    public Task<IReadOnlyList<Document>> ListAsync(
        Guid? companyId,
        string? keyword,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<Document> documents = Filter(companyId, keyword)
            .OrderBy(document => document.DocumentNo)
            .ThenBy(document => document.Id)
            .Skip(skip)
            .Take(take)
            .ToArray();
        return Task.FromResult(documents);
    }

    public Task<Document?> FindByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Documents.SingleOrDefault(document => document.Id == id));
    }

    public Task<IReadOnlyList<DocumentVersion>> ListVersionsAsync(
        Guid documentId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<DocumentVersion> versions = _versions.TryGetValue(documentId, out var stored)
            ? stored
            : [];
        return Task.FromResult(versions);
    }

    public void Add(Document document) => Documents.Add(document);

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    private IEnumerable<Document> Filter(Guid? companyId, string? keyword) =>
        Documents.Where(document =>
            (!companyId.HasValue || document.CompanyId == companyId.Value)
            && (string.IsNullOrWhiteSpace(keyword)
                || document.DocumentNo.Contains(keyword.Trim(), StringComparison.OrdinalIgnoreCase)
                || document.Name.Contains(keyword.Trim(), StringComparison.OrdinalIgnoreCase)));
}

using System.Net;
using System.Net.Http.Json;
using IsoDocument.Api.Common;
using IsoDocument.Api.Features.AuditLogs;
using IsoDocument.Api.Features.Auth;
using IsoDocument.Api.Features.Auth.Dtos;
using IsoDocument.Api.Features.Home;
using IsoDocument.Api.Features.Home.Dtos;
using IsoDocument.Api.Security;
using IsoDocument.Api.Security.Authorization;
using IsoDocument.Api.Storage;
using IsoDocs.Tests.Features.Auth;
using IsoDocs.Tests.Features.Documents;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Xunit;

namespace IsoDocs.Tests.Features.Home;

public sealed class DocumentsBrowseApiTests
{
    [Fact]
    public async Task Available_WhenDepartmentHasPermission_ReturnsPublishedDocument()
    {
        await using var factory = new BrowseWebApplicationFactory(UserRole.USER);
        factory.BrowseStore.AddAvailable(
            factory.DocumentId, factory.VersionId, factory.DeptId, "PUBLISHED", isActive: true);
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);

        var response = await client.GetAsync("/api/documents/available?page=1&pageSize=20");
        var body = await response.Content.ReadFromJsonAsync<PagedResult<AvailableDocumentResponse>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = Assert.Single(body!.Items);
        Assert.Equal(factory.DocumentId, document.DocumentId);
        Assert.Equal(factory.VersionId, document.CurrentVersion.VersionId);
    }

    [Fact]
    public async Task Available_WhenDepartmentHasNoPermission_DoesNotReturnDocument()
    {
        await using var factory = new BrowseWebApplicationFactory(UserRole.USER);
        factory.BrowseStore.AddAvailable(
            factory.DocumentId, factory.VersionId, Guid.NewGuid(), "PUBLISHED", isActive: true);
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);

        var body = await client.GetFromJsonAsync<PagedResult<AvailableDocumentResponse>>(
            "/api/documents/available");

        Assert.NotNull(body);
        Assert.Empty(body.Items);
    }

    [Fact]
    public async Task Available_ExcludesObsoleteAndInactiveDocuments()
    {
        await using var factory = new BrowseWebApplicationFactory(UserRole.USER);
        factory.BrowseStore.AddAvailable(
            factory.DocumentId, factory.VersionId, factory.DeptId, "OBSOLETE", isActive: true);
        factory.BrowseStore.AddAvailable(
            Guid.NewGuid(), Guid.NewGuid(), factory.DeptId, "PUBLISHED", isActive: false);
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);

        var body = await client.GetFromJsonAsync<PagedResult<AvailableDocumentResponse>>(
            "/api/documents/available");

        Assert.NotNull(body);
        Assert.Empty(body.Items);
    }

    [Fact]
    public async Task DownloadDocument_WhenDepartmentHasNoPermission_ReturnsForbiddenWithoutProbeDetails()
    {
        await using var factory = new BrowseWebApplicationFactory(UserRole.USER);
        factory.AccessStore.AddDocument(factory.DocumentId, factory.CompanyId);
        factory.BrowseStore.AddDocumentDownload(
            factory.DocumentId, factory.VersionId, factory.CompanyId, "PUBLISHED", "main.pdf");
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);

        var response = await client.GetAsync(
            $"/api/documents/{factory.DocumentId}/versions/{factory.VersionId}/download");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(factory.Audit.Entries);
    }

    [Fact]
    public async Task DownloadDocument_WhenAuthorized_ReturnsFileAndWritesAudit()
    {
        await using var factory = new BrowseWebApplicationFactory(UserRole.USER);
        factory.AllowUserDocumentAccess();
        const string fileKey = "store/COMPANYA/ISO-001/v1.0/main/file.pdf";
        factory.BrowseStore.AddDocumentDownload(
            factory.DocumentId, factory.VersionId, factory.CompanyId, "PUBLISHED", fileKey);
        factory.Storage.WrittenKeys.Add(fileKey);
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);

        var response = await client.GetAsync(
            $"/api/documents/{factory.DocumentId}/versions/{factory.VersionId}/download");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        var audit = Assert.Single(factory.Audit.Entries);
        Assert.Equal("DOWNLOAD_DOCUMENT", audit.Action);
        Assert.Equal(factory.VersionId, audit.ResourceId);
    }

    [Fact]
    public async Task DownloadObsoleteDocument_AsUser_ReturnsForbidden()
    {
        await using var factory = new BrowseWebApplicationFactory(UserRole.USER);
        factory.AllowUserDocumentAccess();
        factory.BrowseStore.AddDocumentDownload(
            factory.DocumentId, factory.VersionId, factory.CompanyId, "OBSOLETE", "main.pdf");
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);

        var response = await client.GetAsync(
            $"/api/documents/{factory.DocumentId}/versions/{factory.VersionId}/download");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(factory.Audit.Entries);
    }

    [Fact]
    public async Task DownloadObsoleteDocument_AsCompanyAdmin_ReturnsFile()
    {
        await using var factory = new BrowseWebApplicationFactory(UserRole.COMPANY_ADMIN);
        factory.AccessStore.AddDocument(factory.DocumentId, factory.CompanyId);
        const string fileKey = "store/COMPANYA/ISO-001/v1.0/main/file.pdf";
        factory.BrowseStore.AddDocumentDownload(
            factory.DocumentId, factory.VersionId, factory.CompanyId, "OBSOLETE", fileKey);
        factory.Storage.WrittenKeys.Add(fileKey);
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);

        var response = await client.GetAsync(
            $"/api/documents/{factory.DocumentId}/versions/{factory.VersionId}/download");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task DownloadAttachment_WhenAuthorized_ReturnsFileAndWritesAudit()
    {
        await using var factory = new BrowseWebApplicationFactory(UserRole.USER);
        factory.AllowUserDocumentAccess();
        const string fileKey = "store/COMPANYA/ISO-001/v1.0/att/01_file.xlsx";
        factory.BrowseStore.AddAttachmentDownload(
            factory.DocumentId,
            factory.VersionId,
            factory.AttachmentId,
            factory.CompanyId,
            "PUBLISHED",
            fileKey);
        factory.Storage.WrittenKeys.Add(fileKey);
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);

        var response = await client.GetAsync(
            $"/api/documents/{factory.DocumentId}/versions/{factory.VersionId}/attachments/{factory.AttachmentId}/download");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var audit = Assert.Single(factory.Audit.Entries);
        Assert.Equal("DOWNLOAD_ATTACHMENT", audit.Action);
        Assert.Equal(factory.AttachmentId, audit.ResourceId);
    }

    [Fact]
    public async Task DownloadAttachment_WhenMetadataHasNoFile_ReturnsNotFoundAfterAuthorization()
    {
        await using var factory = new BrowseWebApplicationFactory(UserRole.USER);
        factory.AllowUserDocumentAccess();
        factory.BrowseStore.AddAttachmentDownload(
            factory.DocumentId,
            factory.VersionId,
            factory.AttachmentId,
            factory.CompanyId,
            "PUBLISHED",
            fileKey: null);
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);

        var response = await client.GetAsync(
            $"/api/documents/{factory.DocumentId}/versions/{factory.VersionId}/attachments/{factory.AttachmentId}/download");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(factory.Audit.Entries);
    }

    private static async Task LoginAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("EMP001", AuthWebApplicationFactory.InitialPassword));
        response.EnsureSuccessStatusCode();
    }
}

internal sealed class BrowseWebApplicationFactory : WebApplicationFactory<Program>
{
    public BrowseWebApplicationFactory(UserRole role)
    {
        AuthStore = new FakeAuthUserStore();
        AuthStore.User.Role = role.ToString();
        CompanyId = AuthStore.User.CompanyId;
        DeptId = AuthStore.User.DeptId;
        DocumentId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        VersionId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
        AttachmentId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        AccessStore = new FakeDocumentAccessStore();
        BrowseStore = new FakeDocumentsBrowseStore();
        Storage = new FakeDocumentStorage();
        Audit = new FakeDownloadAuditLogService();
    }

    public FakeAuthUserStore AuthStore { get; }
    public Guid CompanyId { get; }
    public Guid DeptId { get; }
    public Guid DocumentId { get; }
    public Guid VersionId { get; }
    public Guid AttachmentId { get; }
    public FakeDocumentAccessStore AccessStore { get; }
    public FakeDocumentsBrowseStore BrowseStore { get; }
    public FakeDocumentStorage Storage { get; }
    public FakeDownloadAuditLogService Audit { get; }

    public void AllowUserDocumentAccess()
    {
        AccessStore.AddDocument(DocumentId, CompanyId);
        AccessStore.Allow(DocumentId, DeptId);
    }

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
            services.RemoveAll<IDocumentAccessStore>();
            services.AddSingleton<IDocumentAccessStore>(AccessStore);
            services.RemoveAll<IDocumentsBrowseStore>();
            services.AddSingleton<IDocumentsBrowseStore>(BrowseStore);
            services.RemoveAll<IDocumentStorage>();
            services.AddSingleton<IDocumentStorage>(Storage);
            services.RemoveAll<IDownloadAuditLogService>();
            services.AddSingleton<IDownloadAuditLogService>(Audit);
        });
    }
}

internal sealed class FakeDocumentAccessStore : IDocumentAccessStore
{
    private readonly Dictionary<Guid, Guid> _documentCompanies = [];
    private readonly HashSet<(Guid DocumentId, Guid DeptId)> _permissions = [];

    public void AddDocument(Guid documentId, Guid companyId) =>
        _documentCompanies[documentId] = companyId;

    public void Allow(Guid documentId, Guid deptId) =>
        _permissions.Add((documentId, deptId));

    public Task<Guid?> FindDocumentCompanyIdAsync(
        Guid documentId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_documentCompanies.TryGetValue(documentId, out var companyId)
            ? (Guid?)companyId
            : null);
    }

    public Task<bool> DeptHasAccessAsync(
        Guid documentId,
        Guid deptId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_permissions.Contains((documentId, deptId)));
    }
}

internal sealed class FakeDocumentsBrowseStore : IDocumentsBrowseStore
{
    private readonly List<AvailableEntry> _available = [];
    private readonly Dictionary<(Guid, Guid), DocumentDownloadRecord> _documents = [];
    private readonly Dictionary<(Guid, Guid, Guid), AttachmentDownloadRecord> _attachments = [];

    public void AddAvailable(
        Guid documentId,
        Guid versionId,
        Guid deptId,
        string status,
        bool isActive) =>
        _available.Add(new(
            deptId,
            status,
            isActive,
            new AvailableDocumentResponse(
                documentId,
                "ISO-001",
                "Quality Manual",
                "Company A",
                new AvailableDocumentVersionResponse(
                    versionId, "1.0", DateOnly.FromDateTime(DateTime.UtcNow), 10))));

    public void AddDocumentDownload(
        Guid documentId,
        Guid versionId,
        Guid companyId,
        string status,
        string? fileKey) =>
        _documents[(documentId, versionId)] = new(
            companyId,
            versionId,
            status,
            fileKey,
            "manual.pdf",
            "application/pdf");

    public void AddAttachmentDownload(
        Guid documentId,
        Guid versionId,
        Guid attachmentId,
        Guid companyId,
        string status,
        string? fileKey) =>
        _attachments[(documentId, versionId, attachmentId)] = new(
            companyId,
            attachmentId,
            status,
            fileKey,
            "form.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");

    public Task<int> CountAvailableAsync(
        Guid deptId,
        string? keyword,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Query(deptId, keyword).Count());
    }

    public Task<IReadOnlyList<AvailableDocumentResponse>> ListAvailableAsync(
        Guid deptId,
        string? keyword,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<AvailableDocumentResponse> result = Query(deptId, keyword)
            .Skip(skip)
            .Take(take)
            .Select(entry => entry.Response)
            .ToArray();
        return Task.FromResult(result);
    }

    public Task<DocumentDownloadRecord?> FindDocumentDownloadAsync(
        Guid documentId,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_documents.GetValueOrDefault((documentId, versionId)));
    }

    public Task<AttachmentDownloadRecord?> FindAttachmentDownloadAsync(
        Guid documentId,
        Guid versionId,
        Guid attachmentId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(
            _attachments.GetValueOrDefault((documentId, versionId, attachmentId)));
    }

    private IEnumerable<AvailableEntry> Query(Guid deptId, string? keyword) =>
        _available.Where(entry =>
            entry.DeptId == deptId
            && entry.IsActive
            && entry.Status == "PUBLISHED"
            && (string.IsNullOrWhiteSpace(keyword)
                || entry.Response.DocumentNo.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || entry.Response.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)));

    private sealed record AvailableEntry(
        Guid DeptId,
        string Status,
        bool IsActive,
        AvailableDocumentResponse Response);
}

internal sealed class FakeDownloadAuditLogService : IDownloadAuditLogService
{
    public List<DownloadAuditEntry> Entries { get; } = [];

    public Task WriteDocumentDownloadedAsync(
        Guid companyId,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Entries.Add(new("DOWNLOAD_DOCUMENT", versionId));
        return Task.CompletedTask;
    }

    public Task WriteAttachmentDownloadedAsync(
        Guid companyId,
        Guid attachmentId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Entries.Add(new("DOWNLOAD_ATTACHMENT", attachmentId));
        return Task.CompletedTask;
    }

    public sealed record DownloadAuditEntry(string Action, Guid ResourceId);
}

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
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
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
        factory.BrowseStore.AddAvailableAttachment(
            factory.DocumentId,
            factory.AttachmentId,
            Guid.NewGuid(),
            "FM-HR-001",
            "請假申請表",
            hasFile: true);
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);

        var response = await client.GetAsync("/api/documents/available?page=1&pageSize=20");
        var body = await response.Content.ReadFromJsonAsync<PagedResult<AvailableDocumentResponse>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = Assert.Single(body!.Items);
        Assert.Equal(factory.DocumentId, document.DocumentId);
        Assert.Equal(factory.VersionId, document.CurrentVersion.VersionId);
        Assert.True(document.CurrentVersion.HasFile);
        var attachment = Assert.Single(document.Attachments);
        Assert.Equal(factory.AttachmentId, attachment.AttachmentId);
        Assert.Equal("FM-HR-001", attachment.AttachmentNo);
        Assert.Equal("請假申請表", attachment.Name);
        Assert.True(attachment.CurrentVersion?.HasFile);
    }

    [Fact]
    public async Task Available_WhenAttachmentHasNoCurrentVersion_ReturnsAttachmentWithNullCurrentVersion()
    {
        await using var factory = new BrowseWebApplicationFactory(UserRole.USER);
        factory.BrowseStore.AddAvailable(
            factory.DocumentId, factory.VersionId, factory.DeptId, "PUBLISHED", isActive: true);
        factory.BrowseStore.AddAvailableAttachment(
            factory.DocumentId,
            factory.AttachmentId,
            versionId: null,
            "FM-HR-002",
            "加班申請表",
            hasFile: false);
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);

        var body = await client.GetFromJsonAsync<PagedResult<AvailableDocumentResponse>>(
            "/api/documents/available");

        var document = Assert.Single(body!.Items);
        var attachment = Assert.Single(document.Attachments);
        Assert.Equal("FM-HR-002", attachment.AttachmentNo);
        Assert.Equal("加班申請表", attachment.Name);
        Assert.Null(attachment.CurrentVersion);
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
        var sourceBytes = CreateMinimalPdfBytes(pageCount: 2);
        factory.Storage.SeedContent(fileKey, sourceBytes);
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);

        var response = await client.GetAsync(
            $"/api/documents/{factory.DocumentId}/versions/{factory.VersionId}/download");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        var audit = Assert.Single(factory.Audit.Entries);
        Assert.Equal("DOWNLOAD_DOCUMENT", audit.Action);
        Assert.Equal(factory.VersionId, audit.ResourceId);

        // 主文下載浮水印：回傳的內容是被改過、但仍然是合法、頁數不變的 PDF；
        // 來源檔案本身（sourceBytes）不受影響，浮水印只發生在回應內容。
        var responseBytes = await response.Content.ReadAsByteArrayAsync();
        Assert.NotEqual(sourceBytes, responseBytes);
        Assert.True(responseBytes.Length > sourceBytes.Length);
        using var watermarked = PdfReader.Open(
            new MemoryStream(responseBytes), PdfDocumentOpenMode.Import);
        Assert.Equal(2, watermarked.PageCount);
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
        factory.Storage.SeedContent(fileKey, CreateMinimalPdfBytes());
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
        const string fileKey = "store/COMPANYA/ISO-001/att/ATT-A/v1.0/file.xlsx";
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
            $"/api/attachments/{factory.AttachmentId}/versions/{factory.VersionId}/download");

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
            $"/api/attachments/{factory.AttachmentId}/versions/{factory.VersionId}/download");

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

    private static byte[] CreateMinimalPdfBytes(int pageCount = 1)
    {
        using var document = new PdfDocument();
        for (var i = 0; i < pageCount; i++)
        {
            document.AddPage();
        }

        using var stream = new MemoryStream();
        document.Save(stream, closeStream: false);
        return stream.ToArray();
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
        AccessStore.AddAttachment(AttachmentId, DocumentId);
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
    private readonly Dictionary<Guid, Guid> _attachmentDocuments = [];
    private readonly HashSet<(Guid DocumentId, Guid DeptId)> _permissions = [];

    public void AddDocument(Guid documentId, Guid companyId) =>
        _documentCompanies[documentId] = companyId;

    public void AddAttachment(Guid attachmentId, Guid documentId) =>
        _attachmentDocuments[attachmentId] = documentId;

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

    public Task<Guid?> FindAttachmentDocumentIdAsync(
        Guid attachmentId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_attachmentDocuments.TryGetValue(attachmentId, out var documentId)
            ? (Guid?)documentId
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
    private readonly Dictionary<Guid, List<AvailableAttachmentResponse>> _availableAttachments = [];
    private readonly Dictionary<(Guid, Guid), DocumentDownloadRecord> _documents = [];
    private readonly Dictionary<(Guid, Guid), AttachmentDownloadRecord> _attachments = [];

    public void AddAvailable(
        Guid documentId,
        Guid versionId,
        Guid deptId,
        string status,
        bool isActive,
        Guid? isoCategoryId = null,
        string? isoCategoryName = null) =>
        _available.Add(new(
            deptId,
            status,
            isActive,
            new AvailableDocumentResponse(
                documentId,
                "ISO-001",
                "Quality Manual",
                "Company A",
                isoCategoryId,
                isoCategoryName,
                new AvailableDocumentVersionResponse(
                    versionId, "1.0", DateOnly.FromDateTime(DateTime.UtcNow), 10, true),
                [])));

    public void AddAvailableAttachment(
        Guid documentId,
        Guid attachmentId,
        Guid? versionId,
        string attachmentNo,
        string name,
        bool hasFile)
    {
        if (!_availableAttachments.TryGetValue(documentId, out var attachments))
        {
            attachments = [];
            _availableAttachments[documentId] = attachments;
        }

        attachments.Add(new(
            attachmentId,
            attachmentNo,
            name,
            versionId.HasValue
                ? new AvailableAttachmentVersionResponse(versionId.Value, "1.0", hasFile)
                : null));
    }

    public void AddDocumentDownload(
        Guid documentId,
        Guid versionId,
        Guid companyId,
        string status,
        string? fileKey,
        string companyCode = "COA") =>
        _documents[(documentId, versionId)] = new(
            companyId,
            versionId,
            status,
            fileKey,
            "manual.pdf",
            "application/pdf",
            companyCode);

    public void AddAttachmentDownload(
        Guid documentId,
        Guid versionId,
        Guid attachmentId,
        Guid companyId,
        string status,
        string? fileKey) =>
        _attachments[(attachmentId, versionId)] = new(
            companyId,
            attachmentId,
            status,
            fileKey,
            "form.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");

    public Task<int> CountAvailableAsync(
        Guid deptId,
        string? keyword,
        Guid? isoCategoryId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Query(deptId, keyword, isoCategoryId).Count());
    }

    public Task<IReadOnlyList<AvailableDocumentResponse>> ListAvailableAsync(
        Guid deptId,
        string? keyword,
        Guid? isoCategoryId,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<AvailableDocumentResponse> result = Query(deptId, keyword, isoCategoryId)
            .Skip(skip)
            .Take(take)
            .Select(entry => entry.Response with
            {
                Attachments = _availableAttachments.GetValueOrDefault(entry.Response.DocumentId) ?? []
            })
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
        Guid attachmentId,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(
            _attachments.GetValueOrDefault((attachmentId, versionId)));
    }

    private IEnumerable<AvailableEntry> Query(Guid deptId, string? keyword, Guid? isoCategoryId) =>
        _available.Where(entry =>
            entry.DeptId == deptId
            && entry.IsActive
            && entry.Status == "PUBLISHED"
            && (string.IsNullOrWhiteSpace(keyword)
                || entry.Response.DocumentNo.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || entry.Response.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            && (!isoCategoryId.HasValue || entry.Response.IsoCategoryId == isoCategoryId));

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

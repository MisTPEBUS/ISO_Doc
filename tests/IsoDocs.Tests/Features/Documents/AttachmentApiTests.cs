using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using IsoDocument.Api.Data.Entities;
using IsoDocument.Api.Features.Auth;
using IsoDocument.Api.Features.Auth.Dtos;
using IsoDocument.Api.Features.AuditLogs;
using IsoDocument.Api.Features.Documents;
using IsoDocument.Api.Features.Documents.Dtos;
using IsoDocument.Api.Security;
using IsoDocument.Api.Storage;
using IsoDocs.Tests.Features.Auth;
using IsoDocs.Tests.Features.AuditLogs;
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

public sealed class AttachmentApiTests
{
    [Fact]
    public async Task IdentityCrud_CreatesListsDetailsAndSoftDeletesAttachment()
    {
        await using var factory = new AttachmentWebApplicationFactory();
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);
        var token = await GetAntiforgeryTokenAsync(client);
        using var create = WithXsrf(HttpMethod.Post,
            $"/api/documents/{factory.Document.Id}/attachments", token,
            JsonContent.Create(new CreateAttachmentRequest("ATT-A", "表單及附件 A")));

        var createResponse = await client.SendAsync(create);
        var created = await createResponse.Content.ReadFromJsonAsync<AttachmentResponse>();

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.NotNull(created);
        Assert.True(created.IsActive);
        Assert.Single((await client.GetFromJsonAsync<IReadOnlyList<AttachmentResponse>>(
            $"/api/documents/{factory.Document.Id}/attachments"))!);
        var detail = await client.GetFromJsonAsync<AttachmentDetailResponse>(
            $"/api/documents/{factory.Document.Id}/attachments/{created.AttachmentId}");
        Assert.Empty(detail!.Versions);

        using var delete = WithXsrf(HttpMethod.Delete,
            $"/api/documents/{factory.Document.Id}/attachments/{created.AttachmentId}", token);
        var deleteResponse = await client.SendAsync(delete);

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.False(factory.Store.Attachments.Single().IsActive);
        Assert.Empty((await client.GetFromJsonAsync<IReadOnlyList<AttachmentResponse>>(
            $"/api/documents/{factory.Document.Id}/attachments"))!);
    }

    [Fact]
    public async Task CreateIdentity_WithDuplicateNumber_ReturnsValidationProblem()
    {
        await using var factory = new AttachmentWebApplicationFactory();
        factory.Store.AddAttachment(factory.Document.Id, "ATT-A", "Existing");
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);
        var token = await GetAntiforgeryTokenAsync(client);
        using var request = WithXsrf(HttpMethod.Post,
            $"/api/documents/{factory.Document.Id}/attachments", token,
            JsonContent.Create(new CreateAttachmentRequest("ATT-A", "Duplicate")));

        var response = await client.SendAsync(request);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("attachmentNo", problem!.Errors.Keys);
    }

    [Fact]
    public async Task CreateVersion_CalculatesMinorVersion_PreservesHistoryAndDoesNotChangeMainVersion()
    {
        await using var factory = new AttachmentWebApplicationFactory();
        var attachment = factory.Store.AddAttachment(factory.Document.Id, "ATT-A", "表單及附件 A");
        var oldEffectiveDate = new DateOnly(2026, 1, 5);
        var oldVersion = factory.VersionStore.AddVersion(
            attachment.Id, 1, 0, "PUBLISHED", oldEffectiveDate);
        var mainStatus = factory.MainVersion.Status;
        var mainExpiredDate = factory.MainVersion.ExpiredDate;
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);
        var token = await GetAntiforgeryTokenAsync(client);
        var effectiveDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        using var request = CreateVersionRequest(
            attachment.Id, token, "MINOR", effectiveDate, "form.pdf", "%PDF-new"u8.ToArray());

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<AttachmentVersionResponse>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("1.1", body!.Version);
        Assert.Equal(
            DateOnly.FromDateTime(DateTime.UtcNow),
            factory.VersionStore.Versions.Single(x => x.Id == body.VersionId).PublishDate);
        Assert.Equal("OBSOLETE", oldVersion.Status);
        Assert.Equal(effectiveDate, oldVersion.ExpiredDate);
        Assert.Equal(oldEffectiveDate, oldVersion.EffectiveDate);
        Assert.Equal(mainStatus, factory.MainVersion.Status);
        Assert.Equal(mainExpiredDate, factory.MainVersion.ExpiredDate);
        Assert.Contains("/att/ATT-A/v1.1/", Assert.Single(factory.Storage.WrittenKeys));
    }

    [Fact]
    public async Task CreateVersion_WhenContentDoesNotMatchExtension_RejectsWithoutWriteOrRow()
    {
        await using var factory = new AttachmentWebApplicationFactory();
        var attachment = factory.Store.AddAttachment(factory.Document.Id, "ATT-A", "表單及附件 A");
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);
        var token = await GetAntiferyTokenAsync(client);
        using var request = CreateVersionRequest(attachment.Id, token, "MINOR",
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)), "form.pdf", "not-pdf"u8.ToArray());

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(factory.Storage.WrittenKeys);
        Assert.Empty(factory.VersionStore.Versions);
    }

    [Theory]
    [InlineData("")]
    [InlineData("PATCH")]
    [InlineData("major")]
    public async Task CreateVersion_WithInvalidChangeType_ReturnsBadRequestWithoutWritingFile(
        string changeType)
    {
        await using var factory = new AttachmentWebApplicationFactory();
        var attachment = factory.Store.AddAttachment(factory.Document.Id, "ATT-A", "表單及附件 A");
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);
        var token = await GetAntiforgeryTokenAsync(client);
        using var request = CreateVersionRequest(
            attachment.Id, token, changeType,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)), "form.pdf", "%PDF-new"u8.ToArray());

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(factory.Storage.WrittenKeys);
        Assert.Empty(factory.VersionStore.Versions);
    }

    [Fact]
    public async Task CreateFirstVersion_UsesOnePointZeroAndDefaultsDatesToUtcToday()
    {
        await using var factory = new AttachmentWebApplicationFactory();
        var attachment = factory.Store.AddAttachment(factory.Document.Id, "ATT-A", "表單及附件 A");
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);
        var token = await GetAntiforgeryTokenAsync(client);
        using var request = CreateVersionRequest(
            attachment.Id, token, "MAJOR", null,
            "form.pdf", "%PDF-new"u8.ToArray());

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<AttachmentVersionResponse>();
        var created = Assert.Single(factory.VersionStore.Versions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("1.0", body!.Version);
        Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow), created.PublishDate);
        Assert.Equal(created.PublishDate, created.EffectiveDate);
    }

    [Fact]
    public async Task BulkImportDocuments_ContinuesAfterDuplicateAndReportsCounts()
    {
        await using var factory = new AttachmentWebApplicationFactory();
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);
        var token = await GetAntiforgeryTokenAsync(client);
        var items = Enumerable.Range(1, 10).Select(index => new BulkImportDocumentItem
        {
            DocumentNo = index == 4 ? factory.Document.DocumentNo : $"ISO-{index:000}",
            Name = $"Document {index}"
        }).ToArray();
        using var request = WithXsrf(HttpMethod.Post, "/api/documents/bulk-import", token,
            JsonContent.Create(new BulkImportDocumentsRequest
            {
                CompanyId = factory.Document.CompanyId, Items = items
            }));

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<BulkImportDocumentsResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(10, body!.Total);
        Assert.Equal(9, body.SuccessCount);
        Assert.Equal(1, body.FailureCount);
        Assert.Equal(4, Assert.Single(body.Failed).Index);
    }

    [Fact]
    public async Task BulkImportAttachments_MissingDocumentFailsOnlyThatItem()
    {
        await using var factory = new AttachmentWebApplicationFactory();
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);
        var token = await GetAntiforgeryTokenAsync(client);
        using var request = WithXsrf(HttpMethod.Post, "/api/attachments/bulk-import", token,
            JsonContent.Create(new BulkImportAttachmentsRequest
            {
                CompanyId = factory.Document.CompanyId,
                Items =
                [
                    new() { DocumentNo = "MISSING", AttachmentNo = "ATT-X", Name = "Missing" },
                    new() { DocumentNo = factory.Document.DocumentNo, AttachmentNo = "ATT-A", Name = "Valid" }
                ]
            }));

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<BulkImportAttachmentsResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, body!.Total);
        Assert.Equal(1, body.SuccessCount);
        Assert.Equal(1, body.FailureCount);
        Assert.Equal(1, Assert.Single(body.Failed).Index);
        Assert.Equal("ATT-A", Assert.Single(body.Succeeded).Attachment.AttachmentNo);
    }

    [Fact]
    public async Task BulkImport_WhenItemsEmpty_ReturnsBadRequest()
    {
        await using var factory = new AttachmentWebApplicationFactory();
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);
        var token = await GetAntiforgeryTokenAsync(client);
        using var request = WithXsrf(HttpMethod.Post, "/api/attachments/bulk-import", token,
            JsonContent.Create(new BulkImportAttachmentsRequest
            {
                CompanyId = factory.Document.CompanyId, Items = []
            }));

        Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(request)).StatusCode);
    }

    private static HttpRequestMessage CreateVersionRequest(
        Guid attachmentId, string token, string changeType, DateOnly? effectiveDate,
        string fileName, byte[] content)
    {
        var multipart = new MultipartFormDataContent();
        multipart.Add(new StringContent(changeType), "changeType");
        if (effectiveDate.HasValue)
        {
            multipart.Add(new StringContent(effectiveDate.Value.ToString("yyyy-MM-dd")), "effectiveDate");
        }
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        multipart.Add(file, "file", fileName);
        return WithXsrf(HttpMethod.Post, $"/api/attachments/{attachmentId}/versions", token, multipart);
    }

    private static HttpRequestMessage WithXsrf(
        HttpMethod method, string uri, string token, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, uri) { Content = content };
        request.Headers.Add("X-XSRF-TOKEN", token);
        return request;
    }

    private static async Task LoginAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/login", new LoginRequest("EMP001", AuthWebApplicationFactory.InitialPassword));
        response.EnsureSuccessStatusCode();
    }

    private static Task<string> GetAntiferyTokenAsync(HttpClient client) =>
        GetAntiforgeryTokenAsync(client);

    private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/antiforgery/token");
        response.EnsureSuccessStatusCode();
        var cookie = response.Headers.GetValues("Set-Cookie")
            .Single(value => value.StartsWith("isodocs.xsrf=", StringComparison.Ordinal));
        return Uri.UnescapeDataString(cookie["isodocs.xsrf=".Length..cookie.IndexOf(';')]);
    }
}

internal sealed class AttachmentWebApplicationFactory : WebApplicationFactory<Program>
{
    public AttachmentWebApplicationFactory()
    {
        AuthStore = new FakeAuthUserStore();
        AuthStore.User.Role = UserRole.COMPANY_ADMIN.ToString();
        Document = new Document
        {
            Id = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
            CompanyId = AuthStore.User.CompanyId, DocumentNo = "ISO-BASE", Name = "Quality Manual",
            IsActive = true, CreatedBy = AuthStore.User.Id,
            CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
        };
        MainVersion = new DocumentVersion
        {
            Id = Guid.NewGuid(), DocumentId = Document.Id, Version = "1.0",
            VersionMajor = 1, VersionMinor = 0, Status = "PUBLISHED",
            PublishDate = new DateOnly(2026, 1, 1), EffectiveDate = new DateOnly(2026, 1, 2),
            CreatedBy = AuthStore.User.Id, CreatedAt = DateTimeOffset.UtcNow
        };
        Store = new FakeAttachmentDocumentStore(Document, AuthStore.User.Id);
        VersionStore = new FakeAttachmentVersionStore(Document, Store, AuthStore.User.Id);
        Storage = new FakeDocumentStorage();
        Audit = new RecordingOperationAuditLogService();
    }

    public FakeAuthUserStore AuthStore { get; }
    public Document Document { get; }
    public DocumentVersion MainVersion { get; }
    public FakeAttachmentDocumentStore Store { get; }
    public FakeAttachmentVersionStore VersionStore { get; }
    public FakeDocumentStorage Storage { get; }
    public RecordingOperationAuditLogService Audit { get; }

    public HttpClient CreateSecureClient() => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"), HandleCookies = true, AllowAutoRedirect = false
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
            services.AddSingleton<IDocumentStore>(Store);
            services.RemoveAll<IAttachmentStore>();
            services.AddSingleton<IAttachmentStore>(Store);
            services.RemoveAll<IAttachmentVersionStore>();
            services.AddSingleton<IAttachmentVersionStore>(VersionStore);
            services.RemoveAll<IDocumentStorage>();
            services.AddSingleton<IDocumentStorage>(Storage);
            services.RemoveAll<IOperationAuditLogService>();
            services.AddSingleton<IOperationAuditLogService>(Audit);
        });
    }
}

internal sealed class FakeAttachmentDocumentStore(Document seed, Guid userId)
    : IAttachmentStore, IDocumentStore
{
    public List<Document> Documents { get; } = [seed];
    public List<Attachment> Attachments { get; } = [];

    public Attachment AddAttachment(Guid documentId, string attachmentNo, string name)
    {
        var now = DateTimeOffset.UtcNow;
        var attachment = new Attachment
        {
            Id = Guid.NewGuid(), DocumentId = documentId, AttachmentNo = attachmentNo,
            Name = name, IsActive = true, CreatedBy = userId, CreatedAt = now, UpdatedAt = now
        };
        Attachments.Add(attachment);
        return attachment;
    }

    public Task<bool> CompanyExistsAsync(Guid companyId, CancellationToken ct) =>
        Task.FromResult(Documents.Any(x => x.CompanyId == companyId));
    public Task<bool> DocumentNoExistsAsync(Guid companyId, string documentNo, CancellationToken ct) =>
        Task.FromResult(Documents.Any(x => x.CompanyId == companyId && x.DocumentNo == documentNo));
    public Task<int> CountAsync(Guid? companyId, string? keyword, CancellationToken ct) =>
        Task.FromResult(Documents.Count(x => !companyId.HasValue || x.CompanyId == companyId));
    public Task<IReadOnlyList<Document>> ListAsync(
        Guid? companyId, string? keyword, int skip, int take, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Document>>(Documents
            .Where(x => !companyId.HasValue || x.CompanyId == companyId).Skip(skip).Take(take).ToArray());
    public Task<Document?> FindByIdAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(Documents.SingleOrDefault(x => x.Id == id));
    public Task<IReadOnlyList<DocumentVersion>> ListVersionsAsync(Guid documentId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<DocumentVersion>>([]);
    public Task<IReadOnlyList<DocumentAttachmentRecord>> ListAttachmentsAsync(
        Guid documentId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<DocumentAttachmentRecord>>([]);
    public Task<IReadOnlyList<Guid>> ListCompanyDeptIdsAsync(Guid companyId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Guid>>([]);
    public void Add(Document document) => Documents.Add(document);
    public void Add(DocumentVersion version) { }
    public void AddRange(IEnumerable<DocumentDeptPermission> permissions) { }
    public void Detach(Document document) => Documents.Remove(document);
    public void Detach(DocumentVersion version) { }
    public void DetachRange(IEnumerable<DocumentDeptPermission> permissions) { }
    public Task<IDocumentTransaction> BeginTransactionAsync(CancellationToken ct) =>
        Task.FromResult<IDocumentTransaction>(new DocumentTransaction());

    public Task<Document?> FindDocumentAsync(Guid documentId, CancellationToken ct) =>
        Task.FromResult(Documents.SingleOrDefault(x => x.Id == documentId));
    public Task<Document?> FindDocumentByNoAsync(Guid companyId, string documentNo, CancellationToken ct) =>
        Task.FromResult(Documents.SingleOrDefault(x => x.CompanyId == companyId && x.DocumentNo == documentNo));
    public Task<bool> AttachmentNoExistsAsync(Guid documentId, string attachmentNo, CancellationToken ct) =>
        Task.FromResult(Attachments.Any(x => x.DocumentId == documentId && x.AttachmentNo == attachmentNo));
    public Task<IReadOnlyList<Attachment>> ListAsync(Guid documentId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Attachment>>(Attachments
            .Where(x => x.DocumentId == documentId && x.IsActive).ToArray());
    Task<Attachment?> IAttachmentStore.FindByIdAsync(Guid attachmentId, CancellationToken ct) =>
        Task.FromResult(Attachments.SingleOrDefault(x => x.Id == attachmentId));
    Task<IReadOnlyList<AttachmentVersion>> IAttachmentStore.ListVersionsAsync(Guid attachmentId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<AttachmentVersion>>([]);
    public void Add(Attachment attachment) => Attachments.Add(attachment);
    public void Detach(Attachment attachment) => Attachments.Remove(attachment);
    public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;

    private sealed class DocumentTransaction : IDocumentTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

internal sealed class FakeAttachmentVersionStore(
    Document document, FakeAttachmentDocumentStore attachmentStore, Guid userId)
    : IAttachmentVersionStore
{
    public List<AttachmentVersion> Versions { get; } = [];

    public AttachmentVersion AddVersion(
        Guid attachmentId, int major, int minor, string status,
        DateOnly effectiveDate)
    {
        var version = new AttachmentVersion
        {
            Id = Guid.NewGuid(), AttachmentId = attachmentId, Version = $"{major}.{minor}",
            VersionMajor = major, VersionMinor = minor, Status = status,
            PublishDate = effectiveDate,
            EffectiveDate = effectiveDate,
            FileKey = "store/existing", OriginalFileName = "old.pdf",
            ContentType = "application/pdf", FileSize = 10, Checksum = "checksum",
            CreatedBy = userId, CreatedAt = DateTimeOffset.UtcNow
        };
        Versions.Add(version);
        return version;
    }

    public Task<AttachmentVersionCreateContext?> FindCreateContextAsync(Guid attachmentId, CancellationToken ct)
    {
        var attachment = attachmentStore.Attachments.SingleOrDefault(x => x.Id == attachmentId);
        return Task.FromResult(attachment is null ? null : new AttachmentVersionCreateContext(
            attachment.Id, attachment.AttachmentNo, attachment.IsActive, document.Id,
            document.CompanyId, document.DocumentNo, "COMPANYA"));
    }

    public Task<AttachmentVersion?> FindLatestVersionAsync(Guid attachmentId, CancellationToken ct) =>
        Task.FromResult(Versions
            .Where(x => x.AttachmentId == attachmentId)
            .OrderByDescending(x => x.VersionMajor)
            .ThenByDescending(x => x.VersionMinor)
            .FirstOrDefault());
    public Task<IReadOnlyList<AttachmentVersion>> ListPublishedVersionsAsync(Guid attachmentId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<AttachmentVersion>>(Versions
            .Where(x => x.AttachmentId == attachmentId && x.Status == "PUBLISHED").ToArray());
    public Task<AttachmentVersion?> FindVersionAsync(Guid versionId, CancellationToken ct) =>
        Task.FromResult(Versions.SingleOrDefault(x => x.Id == versionId));
    public void Add(AttachmentVersion version) => Versions.Add(version);
    public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
    public Task<IAttachmentVersionTransaction> BeginTransactionAsync(CancellationToken ct) =>
        Task.FromResult<IAttachmentVersionTransaction>(new Transaction());

    private sealed class Transaction : IAttachmentVersionTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

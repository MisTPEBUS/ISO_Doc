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
    public async Task UploadBatch_CreatesFileAndMetadataOnlyAttachments()
    {
        await using var factory = new AttachmentWebApplicationFactory();
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);
        var token = await GetAntiforgeryTokenAsync(client);
        using var request = CreateUploadRequest(
            factory,
            token,
            [
                new UploadItem("ATT-01", "Form", "form.PDF", "%PDF-attachment"u8.ToArray()),
                new UploadItem("ATT-02", "Instructions", null, null)
            ]);

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<CreateAttachmentsResponse>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(2, body.Created.Count);
        Assert.True(body.Created.Single(item => item.AttachmentNo == "ATT-01").HasFile);
        Assert.False(body.Created.Single(item => item.AttachmentNo == "ATT-02").HasFile);
        Assert.Equal(2, factory.AttachmentStore.Attachments.Count);
        var objectKey = Assert.Single(factory.Storage.WrittenKeys);
        Assert.Contains("/v1.0/att/01_", objectKey, StringComparison.Ordinal);
        Assert.EndsWith("_form.pdf", objectKey, StringComparison.Ordinal);
        Assert.Collection(
            factory.Audit.Entries,
            entry =>
            {
                Assert.Equal(AuditActions.UploadAttachment, entry.Action);
                Assert.Equal(AuditResourceTypes.Attachment, entry.ResourceType);
                Assert.NotNull(entry.Detail);
            },
            entry =>
            {
                Assert.Equal(AuditActions.CreateAttachmentMetadata, entry.Action);
                Assert.Equal(AuditResourceTypes.Attachment, entry.ResourceType);
                Assert.NotNull(entry.Detail);
            });
    }

    [Fact]
    public async Task UploadBatch_WhenAnyExtensionIsNotAllowed_RejectsEntireBatchWithoutWrites()
    {
        await using var factory = new AttachmentWebApplicationFactory();
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);
        var token = await GetAntiforgeryTokenAsync(client);
        using var request = CreateUploadRequest(
            factory,
            token,
            [
                new UploadItem("ATT-01", "Valid", "valid.pdf", "%PDF-valid"u8.ToArray()),
                new UploadItem("ATT-02", "Invalid", "malware.exe", "invalid"u8.ToArray())
            ]);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(factory.Storage.WrittenKeys);
        Assert.Empty(factory.AttachmentStore.Attachments);
        Assert.False(factory.AttachmentStore.TransactionStarted);
    }

    [Fact]
    public async Task UploadBatch_WithDuplicateNumberInVersion_ReturnsValidationError()
    {
        await using var factory = new AttachmentWebApplicationFactory();
        factory.AttachmentStore.AddSeed("ATT-01", "Existing", fileKey: null);
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);
        var token = await GetAntiforgeryTokenAsync(client);
        using var request = CreateUploadRequest(
            factory,
            token,
            [new UploadItem("ATT-01", "Duplicate", "duplicate.pdf", "%PDF-valid"u8.ToArray())]);

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(body);
        Assert.Contains("attachmentNo", body.Errors.Keys);
        Assert.Empty(factory.Storage.WrittenKeys);
        Assert.Single(factory.AttachmentStore.Attachments);
    }

    [Fact]
    public async Task Delete_WithStoredFile_MovesFileToTrashAndRemovesAttachment()
    {
        await using var factory = new AttachmentWebApplicationFactory();
        var attachment = factory.AttachmentStore.AddSeed(
            "ATT-01",
            "Form",
            "store/COMPANYA/ISO-001/v1.0/att/01_file_form.pdf");
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);
        var token = await GetAntiforgeryTokenAsync(client);
        using var request = new HttpRequestMessage(
            HttpMethod.Delete,
            $"/api/attachments/{attachment.Id}");
        request.Headers.Add("X-XSRF-TOKEN", token);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.DoesNotContain(factory.AttachmentStore.Attachments, item => item.Id == attachment.Id);
        Assert.Contains(attachment.FileKey!, factory.Storage.TrashedKeys);
        Assert.True(factory.AttachmentStore.TransactionCommitted);
        var audit = Assert.Single(factory.Audit.Entries);
        Assert.Equal(AuditActions.DeleteAttachment, audit.Action);
        Assert.Equal(AuditResourceTypes.Attachment, audit.ResourceType);
        Assert.Equal(attachment.Id, audit.ResourceId);
        Assert.NotNull(audit.Detail);
    }

    [Fact]
    public async Task List_ReturnsAttachmentsForSpecifiedVersion()
    {
        await using var factory = new AttachmentWebApplicationFactory();
        factory.AttachmentStore.AddSeed("ATT-01", "Form", fileKey: null);
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);

        var response = await client.GetAsync(
            $"/api/documents/{factory.Document.Id}/versions/{factory.Version.Id}/attachments");
        var body = await response.Content.ReadFromJsonAsync<IReadOnlyList<AttachmentResponse>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var attachment = Assert.Single(body!);
        Assert.Equal("ATT-01", attachment.AttachmentNo);
        Assert.False(attachment.HasFile);
    }

    [Fact]
    public async Task List_WithVersionFromAnotherDocument_ReturnsNotFound()
    {
        await using var factory = new AttachmentWebApplicationFactory();
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);

        var response = await client.GetAsync(
            $"/api/documents/{Guid.NewGuid()}/versions/{factory.Version.Id}/attachments");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_WithUnknownAttachment_ReturnsNotFound()
    {
        await using var factory = new AttachmentWebApplicationFactory();
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);
        var token = await GetAntiforgeryTokenAsync(client);
        using var request = new HttpRequestMessage(
            HttpMethod.Delete,
            $"/api/attachments/{Guid.NewGuid()}");
        request.Headers.Add("X-XSRF-TOKEN", token);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UploadFile_ForMetadataOnlyAttachment_StoresFileAndReturnsHasFile()
    {
        await using var factory = new AttachmentWebApplicationFactory();
        var attachment = factory.AttachmentStore.AddSeed("ATT-01", "Form", fileKey: null);
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);
        var token = await GetAntiforgeryTokenAsync(client);
        using var request = CreateUploadFileRequest(
            attachment.Id, token, "form.PDF", "%PDF-attachment"u8.ToArray());

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<UploadAttachmentFileResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(attachment.Id, body.AttachmentId);
        Assert.True(body.HasFile);

        var objectKey = Assert.Single(factory.Storage.WrittenKeys);
        Assert.Contains("/v1.0/att/01_", objectKey, StringComparison.Ordinal);
        Assert.EndsWith("_form.pdf", objectKey, StringComparison.Ordinal);
        Assert.True(await factory.Storage.ExistsAsync(objectKey));

        var stored = Assert.Single(factory.AttachmentStore.Attachments);
        Assert.Equal(objectKey, stored.FileKey);
        Assert.Equal("form.PDF", stored.OriginalFileName);
        Assert.Equal("application/pdf", stored.ContentType);
        Assert.NotNull(stored.FileSize);
        Assert.NotNull(stored.Checksum);
        Assert.True(factory.AttachmentStore.TransactionCommitted);

        var audit = Assert.Single(factory.Audit.Entries);
        Assert.Equal(AuditActions.UploadAttachment, audit.Action);
        Assert.Equal(AuditResourceTypes.Attachment, audit.ResourceType);
        Assert.Equal(attachment.Id, audit.ResourceId);
    }

    [Fact]
    public async Task UploadFile_WhenAttachmentAlreadyHasFile_ReturnsConflictWithoutWriting()
    {
        await using var factory = new AttachmentWebApplicationFactory();
        var attachment = factory.AttachmentStore.AddSeed(
            "ATT-01", "Form", "store/COMPANYA/ISO-001/v1.0/att/01_file_form.pdf");
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);
        var token = await GetAntiforgeryTokenAsync(client);
        using var request = CreateUploadFileRequest(
            attachment.Id, token, "replacement.pdf", "%PDF-new"u8.ToArray());

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Empty(factory.Storage.WrittenKeys);
        Assert.Empty(factory.Audit.Entries);
    }

    [Fact]
    public async Task UploadFile_WithDisallowedExtension_ReturnsBadRequestWithoutWriting()
    {
        await using var factory = new AttachmentWebApplicationFactory();
        var attachment = factory.AttachmentStore.AddSeed("ATT-01", "Form", fileKey: null);
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);
        var token = await GetAntiforgeryTokenAsync(client);
        using var request = CreateUploadFileRequest(
            attachment.Id, token, "malware.exe", "invalid"u8.ToArray());

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(body);
        Assert.Contains("file", body.Errors.Keys);
        Assert.Empty(factory.Storage.WrittenKeys);

        var stored = Assert.Single(factory.AttachmentStore.Attachments);
        Assert.Null(stored.FileKey);
        Assert.Null(stored.OriginalFileName);
        Assert.Null(stored.ContentType);
        Assert.Null(stored.FileSize);
        Assert.Null(stored.Checksum);
    }

    [Fact]
    public async Task UploadFile_WhenDatabaseFails_MovesFileToTrashAndLeavesAttachmentUnchanged()
    {
        await using var factory = new AttachmentWebApplicationFactory();
        var attachment = factory.AttachmentStore.AddSeed("ATT-01", "Form", fileKey: null);
        factory.AttachmentStore.SaveChangesShouldThrow = true;
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);
        var token = await GetAntiforgeryTokenAsync(client);
        using var request = CreateUploadFileRequest(
            attachment.Id, token, "form.pdf", "%PDF-attachment"u8.ToArray());

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var written = Assert.Single(factory.Storage.WrittenKeys);
        Assert.Contains(written, factory.Storage.TrashedKeys);

        var stored = Assert.Single(factory.AttachmentStore.Attachments);
        Assert.Null(stored.FileKey);
        Assert.Null(stored.OriginalFileName);
        Assert.Null(stored.ContentType);
        Assert.Null(stored.FileSize);
        Assert.Null(stored.Checksum);
        Assert.False(factory.AttachmentStore.TransactionCommitted);
        Assert.Empty(factory.Audit.Entries);
    }

    private static HttpRequestMessage CreateUploadFileRequest(
        Guid attachmentId,
        string token,
        string fileName,
        byte[] content)
    {
        var multipart = new MultipartFormDataContent();
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        multipart.Add(file, "file", fileName);

        var request = new HttpRequestMessage(
            HttpMethod.Put,
            $"/api/attachments/{attachmentId}/file")
        {
            Content = multipart
        };
        request.Headers.Add("X-XSRF-TOKEN", token);
        return request;
    }

    private static HttpRequestMessage CreateUploadRequest(
        AttachmentWebApplicationFactory factory,
        string token,
        IReadOnlyList<UploadItem> items)
    {
        var multipart = new MultipartFormDataContent();
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            multipart.Add(new StringContent(item.AttachmentNo), $"items[{index}].attachmentNo");
            multipart.Add(new StringContent(item.Name), $"items[{index}].name");
            if (item.FileName is not null && item.Content is not null)
            {
                var file = new ByteArrayContent(item.Content);
                file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                multipart.Add(file, $"items[{index}].file", item.FileName);
            }
        }

        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/documents/{factory.Document.Id}/versions/{factory.Version.Id}/attachments")
        {
            Content = multipart
        };
        request.Headers.Add("X-XSRF-TOKEN", token);
        return request;
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

    private sealed record UploadItem(
        string AttachmentNo,
        string Name,
        string? FileName,
        byte[]? Content);
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
            CompanyId = AuthStore.User.CompanyId,
            DocumentNo = "ISO-001",
            Name = "Quality Manual",
            IsActive = true,
            CreatedBy = AuthStore.User.Id,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        Version = new DocumentVersion
        {
            Id = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),
            DocumentId = Document.Id,
            Version = "1.0",
            VersionMajor = 1,
            VersionMinor = 0,
            Status = "PUBLISHED",
            CreatedBy = AuthStore.User.Id,
            CreatedAt = DateTimeOffset.UtcNow
        };
        AttachmentStore = new FakeAttachmentStore(Document, Version, "COMPANYA", AuthStore.User.Id);
        Storage = new FakeDocumentStorage();
        Audit = new RecordingOperationAuditLogService();
    }

    public FakeAuthUserStore AuthStore { get; }
    public Document Document { get; }
    public DocumentVersion Version { get; }
    public FakeAttachmentStore AttachmentStore { get; }
    public FakeDocumentStorage Storage { get; }
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
            services.RemoveAll<IAttachmentStore>();
            services.AddSingleton<IAttachmentStore>(AttachmentStore);
            services.RemoveAll<IDocumentStorage>();
            services.AddSingleton<IDocumentStorage>(Storage);
            services.RemoveAll<IOperationAuditLogService>();
            services.AddSingleton<IOperationAuditLogService>(Audit);
        });
    }
}

internal sealed class FakeAttachmentStore(
    Document document,
    DocumentVersion version,
    string companyCode,
    Guid userId) : IAttachmentStore
{
    private List<Attachment>? _snapshot;

    public List<Attachment> Attachments { get; } = [];
    public bool TransactionStarted { get; private set; }
    public bool TransactionCommitted { get; private set; }
    public bool SaveChangesShouldThrow { get; set; }

    public Attachment AddSeed(string attachmentNo, string name, string? fileKey)
    {
        var attachment = new Attachment
        {
            Id = Guid.NewGuid(),
            DocumentVersionId = version.Id,
            AttachmentNo = attachmentNo,
            Name = name,
            FileKey = fileKey,
            OriginalFileName = fileKey is null ? null : "form.pdf",
            ContentType = fileKey is null ? null : "application/pdf",
            FileSize = fileKey is null ? null : 10,
            Checksum = fileKey is null ? null : "checksum",
            CreatedBy = userId,
            CreatedAt = DateTimeOffset.UtcNow
        };
        Attachments.Add(attachment);
        return attachment;
    }

    public Task<AttachmentVersionContext?> FindVersionContextAsync(
        Guid documentId,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var context = document.Id == documentId && version.Id == versionId
            ? new AttachmentVersionContext(document, version, companyCode)
            : null;
        return Task.FromResult(context);
    }

    public Task<AttachmentContext?> FindAttachmentContextAsync(
        Guid attachmentId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var attachment = Attachments.SingleOrDefault(item => item.Id == attachmentId);
        return Task.FromResult(attachment is null
            ? null
            : new AttachmentContext(attachment, document.CompanyId));
    }

    public Task<AttachmentUploadContext?> FindAttachmentUploadContextAsync(
        Guid attachmentId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var attachment = Attachments.SingleOrDefault(item => item.Id == attachmentId);
        if (attachment is null)
        {
            return Task.FromResult<AttachmentUploadContext?>(null);
        }

        var sequence = Attachments
            .Where(item => item.DocumentVersionId == attachment.DocumentVersionId)
            .OrderBy(item => item.AttachmentNo, StringComparer.Ordinal)
            .ToList()
            .FindIndex(item => item.Id == attachmentId) + 1;

        return Task.FromResult<AttachmentUploadContext?>(new AttachmentUploadContext(
            attachment,
            document.CompanyId,
            companyCode,
            document.DocumentNo,
            version.Version,
            sequence));
    }

    public Task<IReadOnlyList<Attachment>> ListAsync(
        Guid versionId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<Attachment> result = Attachments
            .Where(attachment => attachment.DocumentVersionId == versionId)
            .OrderBy(attachment => attachment.AttachmentNo)
            .ToArray();
        return Task.FromResult(result);
    }

    public Task<IReadOnlySet<string>> FindExistingAttachmentNumbersAsync(
        Guid versionId,
        IReadOnlyCollection<string> attachmentNumbers,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlySet<string> result = Attachments
            .Where(attachment => attachment.DocumentVersionId == versionId
                && attachmentNumbers.Contains(attachment.AttachmentNo))
            .Select(attachment => attachment.AttachmentNo)
            .ToHashSet(StringComparer.Ordinal);
        return Task.FromResult(result);
    }

    public void AddRange(IEnumerable<Attachment> attachments) =>
        Attachments.AddRange(attachments);

    public void Remove(Attachment attachment) => Attachments.Remove(attachment);

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (SaveChangesShouldThrow)
        {
            throw new InvalidOperationException("Simulated database failure.");
        }

        return Task.CompletedTask;
    }

    public Task<IAttachmentTransaction> BeginTransactionAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        TransactionStarted = true;
        TransactionCommitted = false;
        _snapshot = Attachments.Select(CloneAttachment).ToList();
        return Task.FromResult<IAttachmentTransaction>(new Transaction(this));
    }

    private static Attachment CloneAttachment(Attachment source) => new()
    {
        Id = source.Id,
        DocumentVersionId = source.DocumentVersionId,
        AttachmentNo = source.AttachmentNo,
        Name = source.Name,
        FileKey = source.FileKey,
        OriginalFileName = source.OriginalFileName,
        ContentType = source.ContentType,
        FileSize = source.FileSize,
        Checksum = source.Checksum,
        CreatedBy = source.CreatedBy,
        CreatedAt = source.CreatedAt
    };

    private sealed class Transaction(FakeAttachmentStore store) : IAttachmentTransaction
    {
        private bool _committed;

        public Task CommitAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _committed = true;
            store.TransactionCommitted = true;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            if (!_committed && store._snapshot is not null)
            {
                store.Attachments.Clear();
                store.Attachments.AddRange(store._snapshot);
            }

            return ValueTask.CompletedTask;
        }
    }
}

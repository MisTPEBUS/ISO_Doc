using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using IsoDocument.Api.Data.Entities;
using IsoDocument.Api.Features.Auth;
using IsoDocument.Api.Features.Auth.Dtos;
using IsoDocument.Api.Features.Documents;
using IsoDocument.Api.Features.Documents.Dtos;
using IsoDocument.Api.Security;
using IsoDocument.Api.Storage;
using IsoDocs.Tests.Features.Auth;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Xunit;

namespace IsoDocs.Tests.Features.Documents;

public sealed class DocumentVersionApiTests
{
    [Fact]
    public async Task UploadMinorVersion_IncrementsMinorVersionNumber()
    {
        await using var factory = new DocumentVersionWebApplicationFactory();
        factory.VersionStore.AddVersionSeed(1, 0, "PUBLISHED");
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);
        var token = await GetAntiforgeryTokenAsync(client);
        var effectiveDate = UtcToday().AddDays(1);
        using var request = CreateUploadRequest(
            factory.Document.Id, token, "MINOR", effectiveDate, "manual.pdf", ValidPdf());

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<DocumentVersionResponse>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("1.1", body.Version);
        Assert.Equal("PUBLISHED", body.Status);
        var created = Assert.Single(
            factory.VersionStore.Versions,
            version => version.Id == body.VersionId);
        Assert.Equal(1, created.VersionMajor);
        Assert.Equal(1, created.VersionMinor);
        Assert.Equal(UtcToday(), created.PublishDate);
        Assert.Equal(effectiveDate, created.EffectiveDate);
        Assert.Single(factory.Storage.WrittenKeys);
    }

    [Fact]
    public async Task UploadVersion_ObsoletesPreviousPublishedVersionInTransaction()
    {
        await using var factory = new DocumentVersionWebApplicationFactory();
        var previous = factory.VersionStore.AddVersionSeed(1, 0, "PUBLISHED");
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);
        var token = await GetAntiforgeryTokenAsync(client);
        var effectiveDate = UtcToday().AddDays(2);
        using var request = CreateUploadRequest(
            factory.Document.Id, token, "MINOR", effectiveDate, "manual.pdf", ValidPdf());

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("OBSOLETE", previous.Status);
        Assert.Equal(effectiveDate, previous.ExpiredDate);
        Assert.Single(
            factory.VersionStore.Versions,
            version => version.Status == "PUBLISHED");
        Assert.True(factory.VersionStore.TransactionCommitted);
    }

    [Theory]
    [InlineData("manual.txt", "%PDF-valid", "extension")]
    [InlineData("manual.pdf", "not-a-pdf", "content")]
    public async Task UploadNonPdf_ReturnsBadRequestWithoutWritingFile(
        string fileName,
        string contents,
        string _)
    {
        await using var factory = new DocumentVersionWebApplicationFactory();
        factory.VersionStore.AddVersionSeed(1, 0, "PUBLISHED");
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);
        var token = await GetAntiforgeryTokenAsync(client);
        using var request = CreateUploadRequest(
            factory.Document.Id,
            token,
            "MINOR",
            UtcToday().AddDays(1),
            fileName,
            System.Text.Encoding.UTF8.GetBytes(contents));

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(factory.Storage.WrittenKeys);
        Assert.Single(factory.VersionStore.Versions);
        Assert.False(factory.VersionStore.TransactionStarted);
    }

    [Fact]
    public async Task ConcurrentPublishedConflict_ReturnsConflictAndLeavesOnlyOnePublishedVersion()
    {
        await using var factory = new DocumentVersionWebApplicationFactory();
        var previous = factory.VersionStore.AddVersionSeed(1, 0, "PUBLISHED");
        factory.VersionStore.ThrowPublishedConflict = true;
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);
        var token = await GetAntiforgeryTokenAsync(client);
        using var request = CreateUploadRequest(
            factory.Document.Id,
            token,
            "MINOR",
            UtcToday().AddDays(1),
            "manual.pdf",
            ValidPdf());

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Single(factory.VersionStore.Versions);
        Assert.Equal("PUBLISHED", previous.Status);
        Assert.Single(
            factory.VersionStore.Versions,
            version => version.Status == "PUBLISHED");
        Assert.Single(factory.Storage.TrashedKeys);
    }

    private static byte[] ValidPdf() => "%PDF-1.7\nmock"u8.ToArray();

    private static DateOnly UtcToday() => DateOnly.FromDateTime(DateTime.UtcNow);

    private static HttpRequestMessage CreateUploadRequest(
        Guid documentId,
        string token,
        string changeType,
        DateOnly effectiveDate,
        string fileName,
        byte[] contents)
    {
        var multipart = new MultipartFormDataContent
        {
            { new StringContent(changeType), "changeType" },
            { new StringContent(effectiveDate.ToString("yyyy-MM-dd")), "effectiveDate" },
            { new StringContent("12"), "pageCount" },
            { new StringContent("Version memo"), "memo" }
        };
        var file = new ByteArrayContent(contents);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        multipart.Add(file, "file", fileName);

        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/documents/{documentId}/versions")
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
}

internal sealed class DocumentVersionWebApplicationFactory : WebApplicationFactory<Program>
{
    public DocumentVersionWebApplicationFactory()
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
        VersionStore = new FakeDocumentVersionStore(Document, "COMPANYA");
        Storage = new FakeDocumentStorage();
    }

    public FakeAuthUserStore AuthStore { get; }
    public Document Document { get; }
    public FakeDocumentVersionStore VersionStore { get; }
    public FakeDocumentStorage Storage { get; }

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
            services.RemoveAll<IDocumentVersionStore>();
            services.AddSingleton<IDocumentVersionStore>(VersionStore);
            services.RemoveAll<IDocumentStorage>();
            services.AddSingleton<IDocumentStorage>(Storage);
        });
    }
}

internal sealed class FakeDocumentVersionStore(
    Document document,
    string companyCode) : IDocumentVersionStore
{
    private int _transactionStartCount;
    private List<(DocumentVersion Version, string Status, DateOnly? ExpiredDate)>? _snapshot;

    public List<DocumentVersion> Versions { get; } = [];
    public bool ThrowPublishedConflict { get; set; }
    public bool TransactionStarted { get; private set; }
    public bool TransactionCommitted { get; private set; }

    public DocumentVersion AddVersionSeed(int major, int minor, string status)
    {
        var version = new DocumentVersion
        {
            Id = Guid.NewGuid(),
            DocumentId = document.Id,
            Version = $"{major}.{minor}",
            VersionMajor = major,
            VersionMinor = minor,
            Status = status,
            PublishDate = UtcToday(),
            EffectiveDate = UtcToday(),
            CreatedBy = document.CreatedBy,
            CreatedAt = DateTimeOffset.UtcNow
        };
        Versions.Add(version);
        return version;
    }

    public Task<Document?> FindDocumentAsync(Guid documentId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<Document?>(document.Id == documentId ? document : null);
    }

    public Task<string?> FindCompanyCodeAsync(Guid companyId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<string?>(document.CompanyId == companyId ? companyCode : null);
    }

    public Task<DocumentVersion?> FindLatestVersionAsync(
        Guid documentId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Versions
            .Where(version => version.DocumentId == documentId)
            .OrderByDescending(version => version.VersionMajor)
            .ThenByDescending(version => version.VersionMinor)
            .FirstOrDefault());
    }

    public Task<IReadOnlyList<DocumentVersion>> ListPublishedVersionsAsync(
        Guid documentId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<DocumentVersion> published = Versions
            .Where(version => version.DocumentId == documentId && version.Status == "PUBLISHED")
            .ToArray();
        return Task.FromResult(published);
    }

    public void Add(DocumentVersion version) => Versions.Add(version);

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (ThrowPublishedConflict && Versions.Count > _transactionStartCount)
        {
            throw new PublishedVersionConflictException();
        }

        return Task.CompletedTask;
    }

    public Task<IDocumentVersionTransaction> BeginTransactionAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        TransactionStarted = true;
        TransactionCommitted = false;
        _transactionStartCount = Versions.Count;
        _snapshot = Versions
            .Select(version => (version, version.Status, version.ExpiredDate))
            .ToList();
        return Task.FromResult<IDocumentVersionTransaction>(new Transaction(this));
    }

    private static DateOnly UtcToday() => DateOnly.FromDateTime(DateTime.UtcNow);

    private sealed class Transaction(FakeDocumentVersionStore store)
        : IDocumentVersionTransaction
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
                while (store.Versions.Count > store._transactionStartCount)
                {
                    store.Versions.RemoveAt(store.Versions.Count - 1);
                }

                foreach (var (version, status, expiredDate) in store._snapshot)
                {
                    version.Status = status;
                    version.ExpiredDate = expiredDate;
                }
            }

            return ValueTask.CompletedTask;
        }
    }
}

internal sealed class FakeDocumentStorage : IDocumentStorage
{
    public List<string> WrittenKeys { get; } = [];
    public List<string> TrashedKeys { get; } = [];

    public async Task<StorageWriteResult> WriteAsync(
        string objectKey,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        WrittenKeys.Add(objectKey);
        long length = 0;
        var buffer = new byte[64];
        int read;
        while ((read = await content.ReadAsync(buffer, cancellationToken)) > 0)
        {
            length += read;
        }

        return new StorageWriteResult(length, "test-checksum");
    }

    public Task<Stream> OpenReadAsync(
        string objectKey,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<Stream>(new MemoryStream());

    public Task<bool> ExistsAsync(
        string objectKey,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(WrittenKeys.Contains(objectKey) && !TrashedKeys.Contains(objectKey));

    public Task MoveToTrashAsync(
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        TrashedKeys.Add(objectKey);
        return Task.CompletedTask;
    }
}

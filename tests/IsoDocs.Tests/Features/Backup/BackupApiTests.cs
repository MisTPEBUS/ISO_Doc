using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using IsoDocument.Api.Features.AuditLogs;
using IsoDocument.Api.Features.Auth;
using IsoDocument.Api.Features.Auth.Dtos;
using IsoDocument.Api.Features.Backup;
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

namespace IsoDocs.Tests.Features.Backup;

public sealed class BackupApiTests
{
    [Fact]
    public async Task Download_AsCompanyAdminForAnotherCompany_ReturnsForbidden()
    {
        await using var factory = new BackupWebApplicationFactory(UserRole.COMPANY_ADMIN);
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);

        var response = await client.GetAsync(
            $"/api/companies/{Guid.NewGuid()}/backup");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(factory.Audit.Entries);
    }

    [Fact]
    public async Task Download_AsSystemAdminForAnotherCompany_ReturnsZip()
    {
        await using var factory = new BackupWebApplicationFactory(UserRole.SYSTEM_ADMIN);
        var otherCompanyId = Guid.NewGuid();
        factory.Store.Company = new(otherCompanyId, "首都客運");
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);

        var response = await client.GetAsync(
            $"/api/companies/{otherCompanyId}/backup");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/zip", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(
            "首都客運_backup_20260909.zip",
            response.Content.Headers.ContentDisposition?.FileNameStar);
    }

    [Fact]
    public async Task Download_IncludesOnlyPublishedMainAndUploadedAttachments()
    {
        await using var factory = new BackupWebApplicationFactory(UserRole.COMPANY_ADMIN);
        factory.Store.Files.AddRange(
        [
            new("ISO-001", "1.1", "PUBLISHED", "published-main", "品質手冊.pdf", null, null),
            new("ISO-001", "1.1", "PUBLISHED", "published-att", "表單.xlsx", Guid.Empty, "ATT-01"),
            new("ISO-001", "1.0", "OBSOLETE", "obsolete-main", "舊版.pdf", null, null),
            new("ISO-002", "1.0", "DRAFT", "draft-main", "草稿.pdf", null, null),
            new("ISO-001", "1.1", "PUBLISHED", null, null, Guid.Empty, "ATT-02")
        ]);
        factory.Storage.Add("published-main", [1, 2, 3]);
        factory.Storage.Add("published-att", [4, 5, 6]);
        factory.Storage.Add("obsolete-main", [7]);
        factory.Storage.Add("draft-main", [8]);
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);

        using var response = await client.GetAsync(
            $"/api/companies/{factory.CompanyId}/backup");
        response.EnsureSuccessStatusCode();
        await using var responseStream = await response.Content.ReadAsStreamAsync();
        using var archive = new ZipArchive(responseStream, ZipArchiveMode.Read);

        Assert.Equal(
        [
            "ISO-001/v1.1/main/品質手冊.pdf",
            "ISO-001/v1.1/attachments/ATT-01_表單.xlsx"
        ], archive.Entries.Select(entry => entry.FullName).ToArray());
        Assert.DoesNotContain(
            factory.Storage.OpenedKeys,
            key => key is "obsolete-main" or "draft-main");
        var audit = Assert.Single(factory.Audit.Entries);
        Assert.Equal("BACKUP_COMPANY_DOCUMENTS", audit.Action);
        Assert.Equal("Company", audit.ResourceType);
        Assert.Equal(factory.CompanyId, audit.ResourceId);
    }

    [Fact]
    public async Task Download_LargeFile_ReadsSourceIncrementally()
    {
        await using var factory = new BackupWebApplicationFactory(UserRole.COMPANY_ADMIN);
        const long fileSize = 8L * 1024 * 1024;
        var trackingStream = new IncrementalContentStream(fileSize);
        factory.Store.Files.Add(new(
            "ISO-003", "2.0", "PUBLISHED", "large-main", "large.pdf", null, null));
        factory.Storage.Add("large-main", () => trackingStream);
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);

        using var response = await client.GetAsync(
            $"/api/companies/{factory.CompanyId}/backup",
            HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        await using var responseStream = await response.Content.ReadAsStreamAsync();
        await responseStream.CopyToAsync(Stream.Null);

        Assert.Equal(fileSize, trackingStream.TotalBytesRead);
        Assert.InRange(trackingStream.MaximumRequestedReadSize, 1, 128 * 1024);
        Assert.True(trackingStream.AsyncReadCount > 1);
    }

    private static async Task LoginAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("EMP001", AuthWebApplicationFactory.InitialPassword));
        response.EnsureSuccessStatusCode();
    }
}

internal sealed class BackupWebApplicationFactory : WebApplicationFactory<Program>
{
    public BackupWebApplicationFactory(UserRole role)
    {
        AuthStore = new FakeAuthUserStore();
        AuthStore.User.Role = role.ToString();
        CompanyId = AuthStore.User.CompanyId;
        Store = new FakeBackupStore
        {
            Company = new BackupCompany(CompanyId, "首都客運")
        };
        Storage = new FakeBackupStorage();
        Audit = new FakeBackupAuditLogService();
    }

    public FakeAuthUserStore AuthStore { get; }
    public Guid CompanyId { get; }
    public FakeBackupStore Store { get; }
    public FakeBackupStorage Storage { get; }
    public FakeBackupAuditLogService Audit { get; }

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
            services.RemoveAll<IBackupStore>();
            services.AddSingleton<IBackupStore>(Store);
            services.RemoveAll<IDocumentStorage>();
            services.AddSingleton<IDocumentStorage>(Storage);
            services.RemoveAll<IBackupAuditLogService>();
            services.AddSingleton<IBackupAuditLogService>(Audit);
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(new BackupFixedTimeProvider(
                new DateTimeOffset(2026, 9, 9, 0, 0, 0, TimeSpan.Zero)));
        });
    }
}

internal sealed class FakeBackupStore : IBackupStore
{
    public BackupCompany? Company { get; set; }
    public List<BackupSourceFile> Files { get; } = [];

    public Task<BackupCompany?> FindCompanyAsync(
        Guid companyId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Company?.Id == companyId ? Company : null);
    }

    public Task<IReadOnlyList<BackupSourceFile>> ListSourceFilesAsync(
        Guid companyId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<BackupSourceFile> result = Files.ToArray();
        return Task.FromResult(result);
    }
}

internal sealed class FakeBackupStorage : IDocumentStorage
{
    private readonly Dictionary<string, Func<Stream>> _streams = [];

    public List<string> OpenedKeys { get; } = [];

    public void Add(string objectKey, byte[] content) =>
        Add(objectKey, () => new MemoryStream(content, writable: false));

    public void Add(string objectKey, Func<Stream> streamFactory) =>
        _streams[objectKey] = streamFactory;

    public Task<Stream> OpenReadAsync(
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        OpenedKeys.Add(objectKey);
        return Task.FromResult(_streams[objectKey]());
    }

    public Task<StorageWriteResult> WriteAsync(
        string objectKey,
        Stream content,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<bool> ExistsAsync(
        string objectKey,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task MoveToTrashAsync(
        string objectKey,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}

internal sealed class FakeBackupAuditLogService : IBackupAuditLogService
{
    public List<BackupAuditEntry> Entries { get; } = [];

    public Task WriteCompanyBackupCreatedAsync(
        Guid companyId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Entries.Add(new(
            "BACKUP_COMPANY_DOCUMENTS", "Company", companyId));
        return Task.CompletedTask;
    }

    public sealed record BackupAuditEntry(
        string Action,
        string ResourceType,
        Guid ResourceId);
}

internal sealed class IncrementalContentStream(long length) : Stream
{
    private long _position;

    public long TotalBytesRead { get; private set; }
    public int MaximumRequestedReadSize { get; private set; }
    public int AsyncReadCount { get; private set; }
    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => _position;
        set => throw new NotSupportedException();
    }

    public override ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        AsyncReadCount++;
        MaximumRequestedReadSize = Math.Max(MaximumRequestedReadSize, buffer.Length);
        var count = (int)Math.Min(buffer.Length, length - _position);
        if (count <= 0)
        {
            return ValueTask.FromResult(0);
        }

        buffer[..count].Span.Fill(0x41);
        _position += count;
        TotalBytesRead += count;
        return ValueTask.FromResult(count);
    }

    public override int Read(byte[] buffer, int offset, int count) =>
        throw new InvalidOperationException("Backup source files must be read asynchronously.");

    public override void Flush() => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();
}

internal sealed class BackupFixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;
}

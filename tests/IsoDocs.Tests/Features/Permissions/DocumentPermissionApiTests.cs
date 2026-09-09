using System.Net;
using System.Net.Http.Json;
using IsoDocument.Api.Data.Entities;
using IsoDocument.Api.Features.AuditLogs;
using IsoDocument.Api.Features.Auth;
using IsoDocument.Api.Features.Auth.Dtos;
using IsoDocument.Api.Features.Permissions;
using IsoDocument.Api.Features.Permissions.Dtos;
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

namespace IsoDocs.Tests.Features.Permissions;

public sealed class DocumentPermissionApiTests
{
    [Fact]
    public async Task Update_FromABToBC_RemovesAAddsCAndKeepsB()
    {
        await using var factory = new PermissionWebApplicationFactory();
        var permissionA = factory.PermissionStore.AddSeed(factory.DeptA);
        var permissionB = factory.PermissionStore.AddSeed(factory.DeptB);
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);
        var token = await GetAntiforgeryTokenAsync(client);
        using var request = CreateUpdateRequest(
            factory.Document.Id,
            token,
            new UpdateDocumentDeptPermissionsRequest(
                [factory.DeptB, factory.DeptC, factory.DeptC]));

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<DocumentDeptPermissionsResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal([factory.DeptB, factory.DeptC], body.DeptIds.Order().ToArray());
        Assert.DoesNotContain(factory.PermissionStore.Permissions, item => item.Id == permissionA.Id);
        Assert.Contains(factory.PermissionStore.Permissions, item => item.Id == permissionB.Id);
        Assert.Contains(factory.PermissionStore.Permissions, item => item.DeptId == factory.DeptC);
        var audit = Assert.Single(factory.AuditLogService.Entries);
        Assert.Equal([factory.DeptA, factory.DeptB], audit.OldDeptIds.Order().ToArray());
        Assert.Equal([factory.DeptB, factory.DeptC], audit.NewDeptIds.Order().ToArray());
        Assert.True(factory.PermissionStore.TransactionCommitted);
    }

    [Fact]
    public async Task Update_WithUnknownDepartment_ReturnsBadRequestWithoutChangesOrAudit()
    {
        await using var factory = new PermissionWebApplicationFactory();
        factory.PermissionStore.AddSeed(factory.DeptA);
        var original = factory.PermissionStore.Permissions.Select(item => item.Id).ToArray();
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);
        var token = await GetAntiforgeryTokenAsync(client);
        using var request = CreateUpdateRequest(
            factory.Document.Id,
            token,
            new UpdateDocumentDeptPermissionsRequest([factory.DeptB, Guid.NewGuid()]));

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(body);
        Assert.Contains("deptIds", body.Errors.Keys);
        Assert.Equal(original, factory.PermissionStore.Permissions.Select(item => item.Id));
        Assert.Empty(factory.AuditLogService.Entries);
        Assert.False(factory.PermissionStore.TransactionStarted);
    }

    [Fact]
    public async Task Update_WithDepartmentFromAnotherCompany_ReturnsBadRequest()
    {
        await using var factory = new PermissionWebApplicationFactory();
        factory.PermissionStore.AddSeed(factory.DeptA);
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);
        var token = await GetAntiforgeryTokenAsync(client);
        using var request = CreateUpdateRequest(
            factory.Document.Id,
            token,
            new UpdateDocumentDeptPermissionsRequest([factory.OtherCompanyDept]));

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Single(factory.PermissionStore.Permissions);
        Assert.Empty(factory.AuditLogService.Entries);
    }

    [Fact]
    public async Task Update_WithEmptyArray_ReturnsBadRequest()
    {
        await using var factory = new PermissionWebApplicationFactory();
        factory.PermissionStore.AddSeed(factory.DeptA);
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);
        var token = await GetAntiforgeryTokenAsync(client);
        using var request = CreateUpdateRequest(
            factory.Document.Id,
            token,
            new UpdateDocumentDeptPermissionsRequest([]));

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Single(factory.PermissionStore.Permissions);
        Assert.Empty(factory.AuditLogService.Entries);
    }

    [Fact]
    public async Task Update_WithNoDiff_DoesNotWriteAuditLog()
    {
        await using var factory = new PermissionWebApplicationFactory();
        factory.PermissionStore.AddSeed(factory.DeptA);
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);
        var token = await GetAntiforgeryTokenAsync(client);
        using var request = CreateUpdateRequest(
            factory.Document.Id,
            token,
            new UpdateDocumentDeptPermissionsRequest([factory.DeptA, factory.DeptA]));

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(factory.AuditLogService.Entries);
        Assert.False(factory.PermissionStore.TransactionStarted);
    }

    [Fact]
    public async Task Get_ReturnsCurrentDepartmentIds()
    {
        await using var factory = new PermissionWebApplicationFactory();
        factory.PermissionStore.AddSeed(factory.DeptB);
        factory.PermissionStore.AddSeed(factory.DeptA);
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);

        var response = await client.GetAsync(
            $"/api/documents/{factory.Document.Id}/dept-permissions");
        var body = await response.Content.ReadFromJsonAsync<DocumentDeptPermissionsResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal([factory.DeptA, factory.DeptB], body.DeptIds.Order().ToArray());
    }

    private static HttpRequestMessage CreateUpdateRequest(
        Guid documentId,
        string token,
        UpdateDocumentDeptPermissionsRequest body)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Put,
            $"/api/documents/{documentId}/dept-permissions")
        {
            Content = JsonContent.Create(body)
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

internal sealed class PermissionWebApplicationFactory : WebApplicationFactory<Program>
{
    public PermissionWebApplicationFactory()
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
        DeptA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1");
        DeptB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb2");
        DeptC = Guid.Parse("cccccccc-cccc-cccc-cccc-ccccccccccc3");
        OtherCompanyDept = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
        PermissionStore = new FakeDocumentPermissionStore(
            Document,
            new Dictionary<Guid, Guid>
            {
                [DeptA] = Document.CompanyId,
                [DeptB] = Document.CompanyId,
                [DeptC] = Document.CompanyId,
                [OtherCompanyDept] = Guid.NewGuid()
            },
            AuthStore.User.Id);
        AuditLogService = new FakeAuditLogService();
    }

    public FakeAuthUserStore AuthStore { get; }
    public Document Document { get; }
    public Guid DeptA { get; }
    public Guid DeptB { get; }
    public Guid DeptC { get; }
    public Guid OtherCompanyDept { get; }
    public FakeDocumentPermissionStore PermissionStore { get; }
    public FakeAuditLogService AuditLogService { get; }

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
            services.RemoveAll<IDocumentPermissionStore>();
            services.AddSingleton<IDocumentPermissionStore>(PermissionStore);
            services.RemoveAll<IAuditLogService>();
            services.AddSingleton<IAuditLogService>(AuditLogService);
        });
    }
}

internal sealed class FakeDocumentPermissionStore(
    Document document,
    IReadOnlyDictionary<Guid, Guid> deptCompanies,
    Guid grantedBy) : IDocumentPermissionStore
{
    private List<DocumentDeptPermission>? _snapshot;

    public List<DocumentDeptPermission> Permissions { get; } = [];
    public bool TransactionStarted { get; private set; }
    public bool TransactionCommitted { get; private set; }

    public DocumentDeptPermission AddSeed(Guid deptId)
    {
        var permission = new DocumentDeptPermission
        {
            Id = Guid.NewGuid(),
            DocumentId = document.Id,
            DeptId = deptId,
            GrantedBy = grantedBy,
            CreatedAt = DateTimeOffset.UtcNow
        };
        Permissions.Add(permission);
        return permission;
    }

    public Task<Document?> FindDocumentAsync(Guid documentId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<Document?>(document.Id == documentId ? document : null);
    }

    public Task<IReadOnlyList<DocumentDeptPermission>> ListPermissionsAsync(
        Guid documentId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<DocumentDeptPermission> result = Permissions
            .Where(permission => permission.DocumentId == documentId)
            .OrderBy(permission => permission.DeptId)
            .ToArray();
        return Task.FromResult(result);
    }

    public Task<IReadOnlySet<Guid>> FindCompanyDeptIdsAsync(
        Guid companyId,
        IReadOnlyCollection<Guid> deptIds,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlySet<Guid> result = deptIds
            .Where(deptId => deptCompanies.TryGetValue(deptId, out var owner)
                && owner == companyId)
            .ToHashSet();
        return Task.FromResult(result);
    }

    public void AddRange(IEnumerable<DocumentDeptPermission> permissions) =>
        Permissions.AddRange(permissions);

    public void RemoveRange(IEnumerable<DocumentDeptPermission> permissions)
    {
        foreach (var permission in permissions)
        {
            Permissions.Remove(permission);
        }
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public Task<IDocumentPermissionTransaction> BeginTransactionAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        TransactionStarted = true;
        TransactionCommitted = false;
        _snapshot = [.. Permissions];
        return Task.FromResult<IDocumentPermissionTransaction>(new Transaction(this));
    }

    private sealed class Transaction(FakeDocumentPermissionStore store)
        : IDocumentPermissionTransaction
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
                store.Permissions.Clear();
                store.Permissions.AddRange(store._snapshot);
            }

            return ValueTask.CompletedTask;
        }
    }
}

internal sealed class FakeAuditLogService : IAuditLogService
{
    public List<AuditEntry> Entries { get; } = [];

    public Task WriteDocumentDeptPermissionsChangedAsync(
        Guid companyId,
        Guid documentId,
        IReadOnlyCollection<Guid> oldDeptIds,
        IReadOnlyCollection<Guid> newDeptIds,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Entries.Add(new AuditEntry(
            companyId, documentId, [.. oldDeptIds], [.. newDeptIds]));
        return Task.CompletedTask;
    }

    public sealed record AuditEntry(
        Guid CompanyId,
        Guid DocumentId,
        IReadOnlyCollection<Guid> OldDeptIds,
        IReadOnlyCollection<Guid> NewDeptIds);
}

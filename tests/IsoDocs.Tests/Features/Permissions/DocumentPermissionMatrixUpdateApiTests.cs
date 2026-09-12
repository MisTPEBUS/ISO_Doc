using System.Net;
using System.Net.Http.Json;
using IsoDocument.Api.Data.Entities;
using IsoDocument.Api.Features.Auth;
using IsoDocument.Api.Features.Auth.Dtos;
using IsoDocument.Api.Features.AuditLogs;
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

public sealed class DocumentPermissionMatrixUpdateApiTests
{
    [Fact]
    public async Task UpdateBatch_WithTwoDocuments_DiffsEachIndependentlyAndAuditsOnlyChangedOnes()
    {
        await using var factory = new MatrixUpdateWebApplicationFactory();
        var documentA = factory.AddDocument("CP-A-01");
        var documentB = factory.AddDocument("CP-A-02");
        factory.Store.AddSeed(documentA.Id, factory.DeptA);
        factory.Store.AddSeed(documentA.Id, factory.DeptB);
        factory.Store.AddSeed(documentB.Id, factory.DeptA); // 不會被異動，B 文件維持現況
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);
        var token = await GetAntiforgeryTokenAsync(client);
        using var request = CreateUpdateRequest(token, new
        {
            items = new object[]
            {
                new { documentId = documentA.Id, departmentIds = new[] { factory.DeptB, factory.DeptC } },
                new { documentId = documentB.Id, departmentIds = new[] { factory.DeptA } }
            }
        });

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<UpdateDocumentPermissionMatrixResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(2, body.Items.Count);

        var resultA = body.Items.Single(item => item.DocumentId == documentA.Id);
        Assert.Equal([factory.DeptB, factory.DeptC], resultA.DepartmentIds.Order().ToArray());
        Assert.Equal([factory.DeptC], resultA.AddedDepartmentIds);
        Assert.Equal([factory.DeptA], resultA.RemovedDepartmentIds);

        var resultB = body.Items.Single(item => item.DocumentId == documentB.Id);
        Assert.Equal([factory.DeptA], resultB.DepartmentIds);
        Assert.Empty(resultB.AddedDepartmentIds);
        Assert.Empty(resultB.RemovedDepartmentIds);

        Assert.Equal(factory.AuthStore.User.Id, body.UpdatedBy.Id);
        Assert.Equal("Test User", body.UpdatedBy.Name);

        // 只有 A 有實際變動，只該寫一筆 audit。
        var audit = Assert.Single(factory.AuditLogService.Entries);
        Assert.Equal(documentA.Id, audit.DocumentId);
        Assert.True(factory.Store.TransactionCommitted);
    }

    [Fact]
    public async Task UpdateBatch_WithEmptyDepartmentIds_ClearsAllPermissionsForThatDocument()
    {
        await using var factory = new MatrixUpdateWebApplicationFactory();
        var document = factory.AddDocument("CP-A-01");
        factory.Store.AddSeed(document.Id, factory.DeptA);
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);
        var token = await GetAntiforgeryTokenAsync(client);
        using var request = CreateUpdateRequest(token, new
        {
            items = new object[]
            {
                new { documentId = document.Id, departmentIds = Array.Empty<Guid>() }
            }
        });

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<UpdateDocumentPermissionMatrixResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        var result = Assert.Single(body.Items);
        Assert.Empty(result.DepartmentIds);
        Assert.Empty(factory.Store.Permissions);
    }

    [Fact]
    public async Task UpdateBatch_WithEmptyItemsArray_ReturnsBadRequest()
    {
        await using var factory = new MatrixUpdateWebApplicationFactory();
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);
        var token = await GetAntiforgeryTokenAsync(client);
        using var request = CreateUpdateRequest(token, new { items = Array.Empty<object>() });

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(body);
        Assert.Contains("items", body.Errors.Keys);
    }

    [Fact]
    public async Task UpdateBatch_WithDuplicateDocumentId_ReturnsBadRequestWithoutWriting()
    {
        await using var factory = new MatrixUpdateWebApplicationFactory();
        var document = factory.AddDocument("CP-A-01");
        factory.Store.AddSeed(document.Id, factory.DeptA);
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);
        var token = await GetAntiforgeryTokenAsync(client);
        using var request = CreateUpdateRequest(token, new
        {
            items = new object[]
            {
                new { documentId = document.Id, departmentIds = new[] { factory.DeptA } },
                new { documentId = document.Id, departmentIds = new[] { factory.DeptB } }
            }
        });

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(body);
        Assert.Contains("items", body.Errors.Keys);
        Assert.Single(factory.Store.Permissions);
    }

    [Fact]
    public async Task UpdateBatch_WithDuplicateDepartmentIdInOneItem_ReturnsBadRequest()
    {
        await using var factory = new MatrixUpdateWebApplicationFactory();
        var document = factory.AddDocument("CP-A-01");
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);
        var token = await GetAntiforgeryTokenAsync(client);
        using var request = CreateUpdateRequest(token, new
        {
            items = new object[]
            {
                new { documentId = document.Id, departmentIds = new[] { factory.DeptA, factory.DeptA } }
            }
        });

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(body);
        Assert.Contains("items[0].departmentIds", body.Errors.Keys);
    }

    [Fact]
    public async Task UpdateBatch_WithUnknownDocumentId_ReturnsNotFoundWithoutWriting()
    {
        await using var factory = new MatrixUpdateWebApplicationFactory();
        var document = factory.AddDocument("CP-A-01");
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);
        var token = await GetAntiforgeryTokenAsync(client);
        using var request = CreateUpdateRequest(token, new
        {
            items = new object[]
            {
                new { documentId = document.Id, departmentIds = new[] { factory.DeptA } },
                new { documentId = Guid.NewGuid(), departmentIds = Array.Empty<Guid>() }
            }
        });

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(factory.Store.Permissions);
        Assert.False(factory.Store.TransactionStarted);
    }

    [Fact]
    public async Task UpdateBatch_WithDocumentFromAnotherCompany_ReturnsForbiddenWithoutWriting()
    {
        await using var factory = new MatrixUpdateWebApplicationFactory();
        var ownDocument = factory.AddDocument("CP-A-01");
        var otherDocument = factory.AddDocument("OT-A-01", companyId: Guid.NewGuid());
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);
        var token = await GetAntiforgeryTokenAsync(client);
        using var request = CreateUpdateRequest(token, new
        {
            items = new object[]
            {
                new { documentId = ownDocument.Id, departmentIds = new[] { factory.DeptA } },
                new { documentId = otherDocument.Id, departmentIds = Array.Empty<Guid>() }
            }
        });

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(factory.Store.Permissions);
        Assert.False(factory.Store.TransactionStarted);
    }

    [Fact]
    public async Task UpdateBatch_WithDepartmentFromAnotherCompany_ReturnsBadRequestWithoutWriting()
    {
        await using var factory = new MatrixUpdateWebApplicationFactory();
        var document = factory.AddDocument("CP-A-01");
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);
        var token = await GetAntiforgeryTokenAsync(client);
        using var request = CreateUpdateRequest(token, new
        {
            items = new object[]
            {
                new { documentId = document.Id, departmentIds = new[] { factory.OtherCompanyDept } }
            }
        });

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(body);
        Assert.Contains("items[0].departmentIds", body.Errors.Keys);
        Assert.Empty(factory.Store.Permissions);
    }

    private static HttpRequestMessage CreateUpdateRequest(string token, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, "/api/documents/permission-matrix")
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

internal sealed class MatrixUpdateWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly Dictionary<Guid, Guid> _deptCompanies;

    public MatrixUpdateWebApplicationFactory()
    {
        AuthStore = new FakeAuthUserStore();
        AuthStore.User.Role = UserRole.COMPANY_ADMIN.ToString();
        DeptA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1");
        DeptB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb2");
        DeptC = Guid.Parse("cccccccc-cccc-cccc-cccc-ccccccccccc3");
        OtherCompanyDept = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
        _deptCompanies = new Dictionary<Guid, Guid>
        {
            [DeptA] = AuthStore.User.CompanyId,
            [DeptB] = AuthStore.User.CompanyId,
            [DeptC] = AuthStore.User.CompanyId,
            [OtherCompanyDept] = Guid.NewGuid()
        };
        Store = new MultiDocumentPermissionStore(_deptCompanies);
        AuditLogService = new FakeAuditLogService();
    }

    public FakeAuthUserStore AuthStore { get; }
    public Guid DeptA { get; }
    public Guid DeptB { get; }
    public Guid DeptC { get; }
    public Guid OtherCompanyDept { get; }
    public MultiDocumentPermissionStore Store { get; }
    public FakeAuditLogService AuditLogService { get; }

    public Document AddDocument(string documentNo, Guid? companyId = null)
    {
        var document = new Document
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId ?? AuthStore.User.CompanyId,
            DocumentNo = documentNo,
            Name = documentNo,
            IsActive = true,
            CreatedBy = AuthStore.User.Id,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        Store.Documents.Add(document.Id, document);
        return document;
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
            services.RemoveAll<IDocumentPermissionStore>();
            services.AddSingleton<IDocumentPermissionStore>(Store);
            services.RemoveAll<IAuditLogService>();
            services.AddSingleton<IAuditLogService>(AuditLogService);
        });
    }
}

internal sealed class MultiDocumentPermissionStore(IReadOnlyDictionary<Guid, Guid> deptCompanies)
    : IDocumentPermissionStore
{
    private List<DocumentDeptPermission>? _snapshot;

    public Dictionary<Guid, Document> Documents { get; } = [];
    public List<DocumentDeptPermission> Permissions { get; } = [];
    public bool TransactionStarted { get; private set; }
    public bool TransactionCommitted { get; private set; }

    public DocumentDeptPermission AddSeed(Guid documentId, Guid deptId)
    {
        var permission = new DocumentDeptPermission
        {
            Id = Guid.NewGuid(),
            DocumentId = documentId,
            DeptId = deptId,
            GrantedBy = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow
        };
        Permissions.Add(permission);
        return permission;
    }

    public Task<Document?> FindDocumentAsync(Guid documentId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Documents.GetValueOrDefault(documentId));
    }

    public Task<IReadOnlyDictionary<Guid, Document>> FindDocumentsAsync(
        IReadOnlyCollection<Guid> documentIds,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyDictionary<Guid, Document> result = documentIds
            .Where(Documents.ContainsKey)
            .ToDictionary(id => id, id => Documents[id]);
        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<DocumentDeptPermission>> ListPermissionsAsync(
        Guid documentId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<DocumentDeptPermission> result = Permissions
            .Where(permission => permission.DocumentId == documentId)
            .ToArray();
        return Task.FromResult(result);
    }

    public Task<IReadOnlyDictionary<Guid, IReadOnlyList<DocumentDeptPermission>>> ListPermissionsAsync(
        IReadOnlyCollection<Guid> documentIds,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyDictionary<Guid, IReadOnlyList<DocumentDeptPermission>> result = Permissions
            .Where(permission => documentIds.Contains(permission.DocumentId))
            .GroupBy(permission => permission.DocumentId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<DocumentDeptPermission>)group.ToArray());
        return Task.FromResult(result);
    }

    public Task<IReadOnlySet<Guid>> FindCompanyDeptIdsAsync(
        Guid companyId,
        IReadOnlyCollection<Guid> deptIds,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlySet<Guid> result = deptIds
            .Where(deptId => deptCompanies.TryGetValue(deptId, out var owner) && owner == companyId)
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

    private sealed class Transaction(MultiDocumentPermissionStore store)
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

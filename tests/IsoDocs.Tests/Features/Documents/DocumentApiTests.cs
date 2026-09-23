using System.Net;
using System.Net.Http.Json;
using IsoDocument.Api.Common;
using IsoDocument.Api.Data.Entities;
using IsoDocument.Api.Features.Auth;
using IsoDocument.Api.Features.Auth.Dtos;
using IsoDocument.Api.Features.AuditLogs;
using IsoDocument.Api.Features.Documents;
using IsoDocument.Api.Features.Documents.Dtos;
using IsoDocument.Api.Security;
using IsoDocs.Tests.Features.Auth;
using IsoDocs.Tests.Features.AuditLogs;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
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
        // 公司底下沒有任何部門時，不建立任何權限，也不多寫一筆 audit。
        Assert.Empty(factory.DocumentStore.DocumentDeptPermissions);
        AssertAudit(factory.Audit, AuditActions.CreateDocument, body.Id);
    }

    [Fact]
    public async Task Create_WithCompanyDepartments_GrantsAllDepartmentsPermissionInSameTransaction()
    {
        await using var factory = new DocumentWebApplicationFactory(UserRole.COMPANY_ADMIN);
        var deptA = factory.DocumentStore.AddDeptSeed(factory.CompanyA);
        var deptB = factory.DocumentStore.AddDeptSeed(factory.CompanyA);
        factory.DocumentStore.AddDeptSeed(factory.CompanyB); // 不同公司，不應被授權
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);
        var token = await GetAntiforgeryTokenAsync(client);
        using var request = CreateWriteRequest(
            HttpMethod.Post,
            "/api/documents",
            token,
            new CreateDocumentRequest(factory.CompanyA, "ISO-001", "Quality Manual"));

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<DocumentResponse>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(
            new[] { deptA, deptB }.OrderBy(id => id),
            factory.DocumentStore.DocumentDeptPermissions.Select(p => p.DeptId).OrderBy(id => id));
        Assert.All(factory.DocumentStore.DocumentDeptPermissions, permission =>
        {
            Assert.Equal(body.Id, permission.DocumentId);
            Assert.Equal(factory.AuthStore.User.Id, permission.GrantedBy);
        });
        // 文件與權限同一個 transaction：只開、只 commit 一次。
        Assert.Equal(1, factory.DocumentStore.BegunTransactionCount);
        Assert.Equal(1, factory.DocumentStore.CommittedTransactionCount);
        Assert.Equal(2, factory.Audit.Entries.Count);
        Assert.Equal(AuditActions.CreateDocument, factory.Audit.Entries[0].Action);
        Assert.Equal(AuditActions.UpdateDocumentDeptPermissions, factory.Audit.Entries[1].Action);
        Assert.Equal(body.Id, factory.Audit.Entries[1].ResourceId);
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
        Assert.Contains("已使用相同的文件編號", body.Errors["documentNo"].Single(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Create_WithDeptId_ReturnsDeptIdAndDeptName()
    {
        await using var factory = new DocumentWebApplicationFactory(UserRole.COMPANY_ADMIN);
        var deptId = factory.DocumentStore.AddDeptSeed(factory.CompanyA, "品保部");
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);
        var token = await GetAntiforgeryTokenAsync(client);
        using var request = CreateWriteRequest(
            HttpMethod.Post,
            "/api/documents",
            token,
            new CreateDocumentRequest(
                factory.CompanyA, "ISO-001", "Quality Manual", DeptId: deptId));

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<DocumentResponse>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(deptId, body.DeptId);
        Assert.Equal("品保部", body.DeptName);
    }

    [Fact]
    public async Task Create_WithDeptFromAnotherCompany_ReturnsValidationErrorAndDoesNotCreate()
    {
        await using var factory = new DocumentWebApplicationFactory(UserRole.COMPANY_ADMIN);
        var deptOfCompanyB = factory.DocumentStore.AddDeptSeed(factory.CompanyB, "另一間公司的部門");
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);
        var token = await GetAntiforgeryTokenAsync(client);
        using var request = CreateWriteRequest(
            HttpMethod.Post,
            "/api/documents",
            token,
            new CreateDocumentRequest(
                factory.CompanyA, "ISO-001", "Quality Manual", DeptId: deptOfCompanyB));

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(body);
        Assert.Contains("deptId", body.Errors.Keys);
        Assert.Empty(factory.DocumentStore.Documents);
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
    public async Task Get_WithPublishedAttachment_ReturnsDownloadableCurrentVersion()
    {
        await using var factory = new DocumentWebApplicationFactory(UserRole.COMPANY_ADMIN);
        var document = factory.DocumentStore.AddSeed(
            factory.CompanyA, "ISO-001", "Quality Manual");
        var attachment = factory.DocumentStore.AddAttachmentSeed(
            document.Id, "ISO-001-A", "Checklist");
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);

        var response = await client.GetAsync($"/api/documents/{document.Id}");
        var body = await response.Content.ReadFromJsonAsync<DocumentDetailResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        var attachmentBody = Assert.Single(body.Attachments);
        Assert.Equal(attachment.Id, attachmentBody.AttachmentId);
        Assert.Equal("Checklist", attachmentBody.Name);
        Assert.NotNull(attachmentBody.CurrentVersion);
        Assert.Equal("1.0", attachmentBody.CurrentVersion.Version);
        Assert.True(attachmentBody.CurrentVersion.HasFile);
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
        AssertAudit(factory.Audit, AuditActions.UpdateDocument, document.Id);
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
    public async Task Update_WithDeptId_ChangesDeptAndCanClearToNull()
    {
        await using var factory = new DocumentWebApplicationFactory(UserRole.COMPANY_ADMIN);
        var oldDept = factory.DocumentStore.AddDeptSeed(factory.CompanyA, "品保部");
        var newDept = factory.DocumentStore.AddDeptSeed(factory.CompanyA, "研發部");
        var document = factory.DocumentStore.AddSeed(factory.CompanyA, "ISO-001", "Manual");
        document.DeptId = oldDept;
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);
        var token = await GetAntiforgeryTokenAsync(client);

        using var changeRequest = CreateWriteRequest(
            HttpMethod.Put,
            $"/api/documents/{document.Id}",
            token,
            new UpdateDocumentRequest("Manual", DeptId: newDept));
        var changeResponse = await client.SendAsync(changeRequest);
        var changed = await changeResponse.Content.ReadFromJsonAsync<DocumentResponse>();

        Assert.Equal(HttpStatusCode.OK, changeResponse.StatusCode);
        Assert.NotNull(changed);
        Assert.Equal(newDept, changed.DeptId);
        Assert.Equal("研發部", changed.DeptName);

        using var clearRequest = CreateWriteRequest(
            HttpMethod.Put,
            $"/api/documents/{document.Id}",
            token,
            new UpdateDocumentRequest("Manual"));
        var clearResponse = await client.SendAsync(clearRequest);
        var cleared = await clearResponse.Content.ReadFromJsonAsync<DocumentResponse>();

        Assert.Equal(HttpStatusCode.OK, clearResponse.StatusCode);
        Assert.NotNull(cleared);
        Assert.Null(cleared.DeptId);
        Assert.Null(cleared.DeptName);
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
        AssertAudit(factory.Audit, AuditActions.DeleteDocument, document.Id);
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

    [Fact]
    public async Task BulkImport_WithValidItems_CreatesDraftVersionsInPerItemTransactions()
    {
        await using var factory = new DocumentWebApplicationFactory(UserRole.COMPANY_ADMIN);
        var deptA = factory.DocumentStore.AddDeptSeed(factory.CompanyA);
        var deptB = factory.DocumentStore.AddDeptSeed(factory.CompanyA);
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);
        var token = await GetAntiforgeryTokenAsync(client);
        var effectiveDate = new DateOnly(2026, 9, 1);
        using var request = CreateWriteRequest(
            HttpMethod.Post,
            "/api/documents/bulk-import",
            token,
            new BulkImportDocumentsRequest
            {
                CompanyId = factory.CompanyA,
                Items =
                [
                    new BulkImportDocumentItem
                    {
                        DocumentNo = "HR-I-01",
                        Name = "人力資源管理程序",
                        PageCount = 12,
                        EffectiveDate = effectiveDate,
                        Version = "1"
                    },
                    new BulkImportDocumentItem
                    {
                        DocumentNo = "HR-I-02",
                        Name = "教育訓練管理程序",
                        PageCount = 0,
                        EffectiveDate = effectiveDate,
                        Version = "2.1"
                    }
                ]
            });

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<BulkImportDocumentsResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(2, body.Total);
        Assert.Equal(2, body.SuccessCount);
        Assert.Equal(0, body.FailureCount);
        Assert.Equal(2, factory.DocumentStore.Documents.Count);
        Assert.Equal(2, factory.DocumentStore.DocumentVersions.Count);
        Assert.Equal(2, factory.DocumentStore.BegunTransactionCount);
        Assert.Equal(2, factory.DocumentStore.CommittedTransactionCount);
        Assert.Equal("1.0", body.Succeeded[0].Document.Version);
        Assert.Equal("DRAFT", body.Succeeded[0].Document.Status);
        Assert.Equal(effectiveDate, body.Succeeded[0].Document.EffectiveDate);
        Assert.Equal(0, body.Succeeded[1].Document.PageCount);
        // 每份成功建立的文件都套用同一批部門的預設全開權限（2 份文件 x 2 個部門）。
        Assert.Equal(4, factory.DocumentStore.DocumentDeptPermissions.Count);
        Assert.All(factory.DocumentStore.Documents, document =>
        {
            var deptIds = factory.DocumentStore.DocumentDeptPermissions
                .Where(permission => permission.DocumentId == document.Id)
                .Select(permission => permission.DeptId)
                .OrderBy(id => id)
                .ToArray();
            Assert.Equal(new[] { deptA, deptB }.OrderBy(id => id), deptIds);
        });
        Assert.All(factory.DocumentStore.DocumentVersions, version =>
        {
            Assert.Equal("DRAFT", version.Status);
            Assert.Null(version.PublishDate);
            Assert.Null(version.FileKey);
            Assert.Contains(factory.DocumentStore.Documents,
                document => document.Id == version.DocumentId);
        });
    }

    [Fact]
    public async Task BulkImport_WithErrors_ContinuesAndReturnsFailedRows()
    {
        await using var factory = new DocumentWebApplicationFactory(UserRole.COMPANY_ADMIN);
        factory.DocumentStore.AddSeed(factory.CompanyA, "HR-I-09", "既有文件");
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);
        var token = await GetAntiforgeryTokenAsync(client);
        var items = Enumerable.Range(1, 10)
            .Select(index => new BulkImportDocumentItem
            {
                DocumentNo = $"HR-I-{index:00}",
                Name = $"文件 {index}",
                PageCount = index == 2 ? -1 : index,
                EffectiveDate = new DateOnly(2026, 9, 1),
                Version = index == 2 ? "1.x" : "1.0"
            })
            .ToArray();
        using var request = CreateWriteRequest(
            HttpMethod.Post,
            "/api/documents/bulk-import",
            token,
            new BulkImportDocumentsRequest
            {
                CompanyId = factory.CompanyA,
                Items = items
            });

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<BulkImportDocumentsResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(10, body.Total);
        Assert.Equal(8, body.SuccessCount);
        Assert.Equal(2, body.FailureCount);
        Assert.Equal([2, 9], body.Failed.Select(failure => failure.Index).ToArray());
        Assert.Equal("HR-I-02", body.Failed[0].OriginalData.DocumentNo);
        Assert.Contains("pageCount", body.Failed[0].Errors.Keys);
        Assert.Contains("version", body.Failed[0].Errors.Keys);
        Assert.Equal("HR-I-09", body.Failed[1].OriginalData.DocumentNo);
        Assert.Contains("documentNo", body.Failed[1].Errors.Keys);
        Assert.Equal(8, factory.DocumentStore.DocumentVersions.Count);
        Assert.Equal(8, factory.DocumentStore.CommittedTransactionCount);
    }

    [Fact]
    public async Task BulkImport_WhenOneWriteFails_RollsBackThatItemAndContinues()
    {
        await using var factory = new DocumentWebApplicationFactory(UserRole.COMPANY_ADMIN);
        var deptA = factory.DocumentStore.AddDeptSeed(factory.CompanyA);
        factory.DocumentStore.DocumentNoToFailOnSave = "HR-I-01";
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);
        var token = await GetAntiforgeryTokenAsync(client);
        using var request = CreateWriteRequest(
            HttpMethod.Post,
            "/api/documents/bulk-import",
            token,
            new BulkImportDocumentsRequest
            {
                CompanyId = factory.CompanyA,
                Items =
                [
                    ValidBulkItem("HR-I-01"),
                    ValidBulkItem("HR-I-02")
                ]
            });

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<BulkImportDocumentsResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(1, body.SuccessCount);
        Assert.Equal(1, body.FailureCount);
        Assert.Equal(1, body.Failed[0].Index);
        Assert.DoesNotContain(factory.DocumentStore.Documents,
            document => document.DocumentNo == "HR-I-01");
        var succeededDocument = Assert.Single(factory.DocumentStore.Documents);
        var succeededVersion = Assert.Single(factory.DocumentStore.DocumentVersions);
        Assert.Equal("HR-I-02", succeededDocument.DocumentNo);
        Assert.Equal(succeededDocument.Id, succeededVersion.DocumentId);
        Assert.Equal(2, factory.DocumentStore.BegunTransactionCount);
        Assert.Equal(1, factory.DocumentStore.CommittedTransactionCount);
        // 失敗那筆連同它的「預設全開」權限一起 rollback，不會留下沒有對應文件的權限列。
        var permission = Assert.Single(factory.DocumentStore.DocumentDeptPermissions);
        Assert.Equal(succeededDocument.Id, permission.DocumentId);
        Assert.Equal(deptA, permission.DeptId);
    }

    private static BulkImportDocumentItem ValidBulkItem(string documentNo) => new()
    {
        DocumentNo = documentNo,
        Name = $"{documentNo} 名稱",
        PageCount = 1,
        EffectiveDate = new DateOnly(2026, 9, 1),
        Version = "1.0"
    };

    private static void AssertAudit(
        RecordingOperationAuditLogService audit,
        string action,
        Guid resourceId)
    {
        var entry = Assert.Single(audit.Entries);
        Assert.Equal(action, entry.Action);
        Assert.Equal(AuditResourceTypes.Document, entry.ResourceType);
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
        Audit = new RecordingOperationAuditLogService();
    }

    public Guid CompanyA { get; }
    public Guid CompanyB { get; }
    public FakeAuthUserStore AuthStore { get; }
    public FakeDocumentStore DocumentStore { get; }
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
            services.RemoveAll<IDocumentStore>();
            services.AddSingleton<IDocumentStore>(DocumentStore);
            services.RemoveAll<IOperationAuditLogService>();
            services.AddSingleton<IOperationAuditLogService>(Audit);
        });
    }
}

internal sealed class FakeDocumentStore(IEnumerable<Guid> companyIds) : IDocumentStore
{
    private readonly HashSet<Guid> _companyIds = [.. companyIds];
    private readonly Dictionary<Guid, List<DocumentVersion>> _versions = [];
    private readonly Dictionary<Guid, List<Guid>> _deptIdsByCompany = [];
    private readonly Dictionary<Guid, Dept> _deptsById = [];
    private readonly HashSet<(Guid CompanyId, Guid IsoCategoryId)> _isoCategoriesByCompany = [];

    public List<Document> Documents { get; } = [];
    public List<DocumentVersion> DocumentVersions { get; } = [];
    public List<DocumentAttachmentRecord> Attachments { get; } = [];
    public List<DocumentDeptPermission> DocumentDeptPermissions { get; } = [];
    public int BegunTransactionCount { get; private set; }
    public int CommittedTransactionCount { get; private set; }
    public string? DocumentNoToFailOnSave { get; set; }

    public Guid AddDeptSeed(Guid companyId, string name = "部門")
    {
        var deptId = Guid.NewGuid();
        if (!_deptIdsByCompany.TryGetValue(companyId, out var deptIds))
        {
            deptIds = [];
            _deptIdsByCompany[companyId] = deptIds;
        }

        deptIds.Add(deptId);
        var now = DateTimeOffset.UtcNow;
        _deptsById[deptId] = new Dept
        {
            Id = deptId,
            CompanyId = companyId,
            Name = name,
            CreatedAt = now,
            UpdatedAt = now
        };
        return deptId;
    }

    public Guid AddIsoCategorySeed(Guid companyId)
    {
        var isoCategoryId = Guid.NewGuid();
        _isoCategoriesByCompany.Add((companyId, isoCategoryId));
        return isoCategoryId;
    }

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

    public Attachment AddAttachmentSeed(
        Guid documentId,
        string attachmentNo,
        string name)
    {
        var now = DateTimeOffset.UtcNow;
        var attachment = new Attachment
        {
            Id = Guid.NewGuid(),
            DocumentId = documentId,
            AttachmentNo = attachmentNo,
            Name = name,
            IsActive = true,
            CreatedBy = Guid.NewGuid(),
            CreatedAt = now,
            UpdatedAt = now
        };
        var version = new AttachmentVersion
        {
            Id = Guid.NewGuid(),
            AttachmentId = attachment.Id,
            Version = "1.0",
            VersionMajor = 1,
            VersionMinor = 0,
            Status = "PUBLISHED",
            PublishDate = DateOnly.FromDateTime(now.UtcDateTime),
            EffectiveDate = DateOnly.FromDateTime(now.UtcDateTime),
            FileKey = "store/checklist.pdf",
            OriginalFileName = "checklist.pdf",
            ContentType = "application/pdf",
            FileSize = 10,
            Checksum = "checksum",
            CreatedBy = Guid.NewGuid(),
            CreatedAt = now
        };
        Attachments.Add(new(attachment, version));
        return attachment;
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
            .Select(WithDept)
            .ToArray();
        return Task.FromResult(documents);
    }

    public Task<Document?> FindByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var document = Documents.SingleOrDefault(document => document.Id == id);
        return Task.FromResult(document is null ? null : WithDept(document));
    }

    /// <summary>
    /// 模擬真正 EfDocumentStore 對 Dept 的 .Include()：依 DeptId 補上 Dept 導覽屬性，
    /// 讓測試不必每次自己手動組裝，行為與正式環境的 join 結果一致。
    /// </summary>
    private Document WithDept(Document document)
    {
        document.Dept = document.DeptId is { } deptId ? _deptsById.GetValueOrDefault(deptId) : null;
        return document;
    }

    public Task<IReadOnlyList<DocumentVersion>> ListVersionsAsync(
        Guid documentId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var seeded = _versions.TryGetValue(documentId, out var stored)
            ? stored
            : [];
        IReadOnlyList<DocumentVersion> versions = seeded
            .Concat(DocumentVersions.Where(version => version.DocumentId == documentId))
            .ToArray();
        return Task.FromResult(versions);
    }

    public Task<IReadOnlyList<DocumentAttachmentRecord>> ListAttachmentsAsync(
        Guid documentId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<DocumentAttachmentRecord>>(
            Attachments.Where(item => item.Attachment.DocumentId == documentId).ToArray());
    }

    public Task<IReadOnlyList<Guid>> ListCompanyDeptIdsAsync(
        Guid companyId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<Guid> deptIds = _deptIdsByCompany.TryGetValue(companyId, out var stored)
            ? [.. stored]
            : [];
        return Task.FromResult(deptIds);
    }

    public Task<bool> IsoCategoryBelongsToCompanyAsync(
        Guid companyId,
        Guid isoCategoryId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_isoCategoriesByCompany.Contains((companyId, isoCategoryId)));
    }

    public Task<Dept?> FindCompanyDeptAsync(
        Guid companyId,
        Guid deptId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var dept = _deptsById.GetValueOrDefault(deptId);
        return Task.FromResult(dept is not null && dept.CompanyId == companyId ? dept : null);
    }

    public void Add(Document document) => Documents.Add(document);

    public void Add(DocumentVersion version) => DocumentVersions.Add(version);

    public void AddRange(IEnumerable<DocumentDeptPermission> permissions) =>
        DocumentDeptPermissions.AddRange(permissions);

    public void Detach(Document document) => Documents.Remove(document);

    public void Detach(DocumentVersion version) => DocumentVersions.Remove(version);

    public void DetachRange(IEnumerable<DocumentDeptPermission> permissions)
    {
        foreach (var permission in permissions)
        {
            DocumentDeptPermissions.Remove(permission);
        }
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (DocumentNoToFailOnSave is { } documentNo
            && Documents.LastOrDefault()?.DocumentNo == documentNo)
        {
            DocumentNoToFailOnSave = null;
            throw new DbUpdateException("Simulated bulk-import write failure.");
        }

        return Task.CompletedTask;
    }

    public Task<IDocumentTransaction> BeginTransactionAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        BegunTransactionCount++;
        return Task.FromResult<IDocumentTransaction>(new FakeDocumentTransaction(this));
    }

    private IEnumerable<Document> Filter(Guid? companyId, string? keyword) =>
        Documents.Where(document =>
            (!companyId.HasValue || document.CompanyId == companyId.Value)
            && (string.IsNullOrWhiteSpace(keyword)
                || document.DocumentNo.Contains(keyword.Trim(), StringComparison.OrdinalIgnoreCase)
                || document.Name.Contains(keyword.Trim(), StringComparison.OrdinalIgnoreCase)));

    private sealed class FakeDocumentTransaction(FakeDocumentStore store) : IDocumentTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            store.CommittedTransactionCount++;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

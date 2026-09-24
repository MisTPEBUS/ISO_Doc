using System.Net;
using System.Net.Http.Json;
using IsoDocument.Api.Data.Entities;
using IsoDocument.Api.Features.AiImport;
using IsoDocument.Api.Features.AiImport.Dtos;
using IsoDocument.Api.Features.AuditLogs;
using IsoDocument.Api.Features.Auth;
using IsoDocument.Api.Features.Auth.Dtos;
using IsoDocument.Api.Features.Documents;
using IsoDocument.Api.Security;
using IsoDocs.Tests.Features.Auth;
using IsoDocs.Tests.Features.AuditLogs;
using IsoDocs.Tests.Features.Documents;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Xunit;

namespace IsoDocs.Tests.Features.AiImport;

public sealed class AiImportApiTests
{
    [Fact]
    public async Task Commit_NewDocument_SavesSelectedCategoryAndDept()
    {
        await using var factory = new AiImportWebApplicationFactory();
        var categoryId = factory.DocumentStore.AddIsoCategorySeed(factory.CompanyA);
        var deptId = factory.DocumentStore.AddDeptSeed(factory.CompanyA);
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);
        var token = await GetAntiforgeryTokenAsync(client);
        using var request = CreateCommitRequest(factory.CompanyA, token,
            [new("GA-P-01", categoryId, deptId)]);

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<CommitImportResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(1, body.Documents.SuccessCount);
        var document = Assert.Single(factory.DocumentStore.Documents);
        Assert.Equal(categoryId, document.IsoCategoryId);
        Assert.Equal(deptId, document.DeptId);
    }

    [Fact]
    public async Task Commit_InvalidCompanyMetadata_FailsOnlyThatDocument()
    {
        await using var factory = new AiImportWebApplicationFactory();
        var foreignCategoryId = factory.DocumentStore.AddIsoCategorySeed(factory.CompanyB);
        var foreignDeptId = factory.DocumentStore.AddDeptSeed(factory.CompanyB);
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);
        var token = await GetAntiforgeryTokenAsync(client);
        using var request = CreateCommitRequest(factory.CompanyA, token,
            [new("GA-P-01", foreignCategoryId, foreignDeptId, "GA-P-01-01"),
                new("GA-P-02", null, null)]);

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<CommitImportResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(1, body.Documents.SuccessCount);
        Assert.Equal(1, body.Documents.FailureCount);
        Assert.Contains("isoCategoryId", body.Documents.Failed[0].Errors.Keys);
        Assert.Contains("deptId", body.Documents.Failed[0].Errors.Keys);
        Assert.Equal("PARENT_DOCUMENT_FAILED", Assert.Single(body.Attachments.Skipped).Reason);
        var document = Assert.Single(factory.DocumentStore.Documents);
        Assert.Equal("GA-P-02", document.DocumentNo);
        Assert.Null(document.IsoCategoryId);
        Assert.Null(document.DeptId);
    }

    [Fact]
    public async Task Commit_ExistingDocument_EmptyFieldsPreserveAndSelectedFieldsUpdate()
    {
        await using var factory = new AiImportWebApplicationFactory();
        var originalCategoryId = factory.DocumentStore.AddIsoCategorySeed(factory.CompanyA);
        var originalDeptId = factory.DocumentStore.AddDeptSeed(factory.CompanyA);
        var newCategoryId = factory.DocumentStore.AddIsoCategorySeed(factory.CompanyA);
        var newDeptId = factory.DocumentStore.AddDeptSeed(factory.CompanyA);
        var document = factory.DocumentStore.AddSeed(factory.CompanyA, "GA-P-01", "既有主文");
        document.IsoCategoryId = originalCategoryId;
        document.DeptId = originalDeptId;
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);
        var token = await GetAntiforgeryTokenAsync(client);

        using (var preserve = CreateCommitRequest(factory.CompanyA, token,
            [new("GA-P-01", null, null)]))
        {
            var response = await client.SendAsync(preserve);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        Assert.Equal(originalCategoryId, document.IsoCategoryId);
        Assert.Equal(originalDeptId, document.DeptId);

        using (var update = CreateCommitRequest(factory.CompanyA, token,
            [new("GA-P-01", newCategoryId, null)]))
        {
            var response = await client.SendAsync(update);
            var body = await response.Content.ReadFromJsonAsync<CommitImportResponse>();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(1, body?.Documents.SuccessCount);
        }
        Assert.Equal(newCategoryId, document.IsoCategoryId);
        Assert.Equal(originalDeptId, document.DeptId);

        using (var update = CreateCommitRequest(factory.CompanyA, token,
            [new("GA-P-01", null, newDeptId)]))
        {
            var response = await client.SendAsync(update);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        Assert.Equal(newCategoryId, document.IsoCategoryId);
        Assert.Equal(newDeptId, document.DeptId);
        Assert.Contains(factory.Audit.Entries, entry =>
            entry.Action == AuditActions.UpdateDocument && entry.ResourceId == document.Id);
    }

    private sealed record ImportItem(
        string DocumentNo, Guid? IsoCategoryId, Guid? DeptId, string? AttachmentNo = null);

    private static HttpRequestMessage CreateCommitRequest(
        Guid companyId, string token, IReadOnlyList<ImportItem> items)
    {
        var content = new MultipartFormDataContent();
        content.Add(new StringContent(companyId.ToString()), "companyId");
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            var prefix = $"documents[{index}]";
            content.Add(new StringContent(item.DocumentNo), $"{prefix}.documentNo");
            content.Add(new StringContent("ISO 主文"), $"{prefix}.name");
            content.Add(new StringContent("1.0"), $"{prefix}.version");
            if (item.IsoCategoryId is { } categoryId)
            {
                content.Add(new StringContent(categoryId.ToString()), $"{prefix}.isoCategoryId");
            }
            if (item.DeptId is { } deptId)
            {
                content.Add(new StringContent(deptId.ToString()), $"{prefix}.deptId");
            }
            if (item.AttachmentNo is { } attachmentNo)
            {
                content.Add(new StringContent(attachmentNo), $"{prefix}.attachments[0].attachmentNo");
                content.Add(new StringContent("申請表"), $"{prefix}.attachments[0].name");
                content.Add(new StringContent("1.0"), $"{prefix}.attachments[0].version");
            }
        }

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/documents/ai-import/commit")
        {
            Content = content
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
        return Uri.UnescapeDataString(cookie["isodocs.xsrf=".Length..cookie.IndexOf(';')]);
    }
}

internal sealed class AiImportWebApplicationFactory : WebApplicationFactory<Program>
{
    public AiImportWebApplicationFactory()
    {
        AuthStore = new FakeAuthUserStore();
        AuthStore.User.Role = UserRole.COMPANY_ADMIN.ToString();
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
            services.RemoveAll<IAiImportStore>();
            services.AddSingleton<IAiImportStore>(new FakeAiImportStore(DocumentStore));
            services.RemoveAll<IOperationAuditLogService>();
            services.AddSingleton<IOperationAuditLogService>(Audit);
        });
    }
}

internal sealed class FakeAiImportStore(FakeDocumentStore documentStore) : IAiImportStore
{
    public Task<DocumentImportState?> FindDocumentStateAsync(
        Guid companyId, string documentNo, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var document = documentStore.Documents.SingleOrDefault(candidate =>
            candidate.CompanyId == companyId && candidate.DocumentNo == documentNo);
        DocumentImportState? state = document is null ? null : new(document, null);
        return Task.FromResult(state);
    }

    public Task<AttachmentImportState?> FindAttachmentStateAsync(
        Guid documentId, string attachmentNo, CancellationToken cancellationToken) =>
        Task.FromResult<AttachmentImportState?>(null);
}

using System.Net;
using System.Net.Http.Json;
using IsoDocument.Api.Data;
using IsoDocument.Api.Data.Entities;
using IsoDocument.Api.Features.Auth;
using IsoDocument.Api.Features.Auth.Dtos;
using IsoDocument.Api.Features.Permissions.Dtos;
using IsoDocument.Api.Security;
using IsoDocument.Api.Storage;
using IsoDocs.Tests.Features.Auth;
using IsoDocs.Tests.Features.Documents;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Xunit;

namespace IsoDocs.Tests.Features.Permissions;

public sealed class DocumentPermissionMatrixApiTests
{
    [Fact]
    public async Task Matrix_DocumentWithoutPermissions_ReturnsEmptyDepartmentIds()
    {
        await using var factory = new MatrixWebApplicationFactory();
        await factory.SeedAsync(seed =>
        {
            var document = seed.AddDocument("CP-A-01", "無授權文件");
            seed.AddPublishedVersion(document.Id, "1.0", "store/A/CP-A-01/v1.0/main.pdf");
        });
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);

        var body = await GetMatrixAsync(client);

        var row = Assert.Single(body.Items);
        Assert.Equal("CP-A-01", row.DocumentCode);
        Assert.Empty(row.DepartmentIds);
        Assert.NotNull(row.DepartmentIds);
    }

    [Fact]
    public async Task Matrix_CompanyAdminWithForeignCompanyIdInQuery_IgnoresItAndScopesToOwnCompany()
    {
        await using var factory = new MatrixWebApplicationFactory();
        Guid otherCompanyId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        await factory.SeedAsync(seed =>
        {
            seed.AddCompany(otherCompanyId, "OTHER");
            seed.AddDept(otherCompanyId, "他公司部門", 1);
            var otherDocument = seed.AddDocument("OT-A-01", "他公司文件", otherCompanyId);
            seed.AddPublishedVersion(otherDocument.Id, "1.0", "store/OT/main.pdf");

            var ownDocument = seed.AddDocument("CP-A-01", "本公司文件");
            seed.AddPublishedVersion(ownDocument.Id, "1.0", "store/A/main.pdf");
        });
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);

        var response = await client.GetAsync(
            $"/api/documents/permission-matrix?companyId={otherCompanyId}");
        var body = await response.Content.ReadFromJsonAsync<DocumentPermissionMatrixResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        var row = Assert.Single(body.Items);
        Assert.Equal("CP-A-01", row.DocumentCode);
        Assert.Equal(factory.OwnCompanyId, row.CompanyId);
        Assert.All(body.Departments, department =>
            Assert.DoesNotContain("他公司", department.Name));
        Assert.Equal(3, body.Departments.Count);
    }

    [Fact]
    public async Task Matrix_SystemAdminWithCompanyCode_ScopesToThatCompany()
    {
        await using var factory = new MatrixWebApplicationFactory(UserRole.SYSTEM_ADMIN);
        var otherCompanyId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        await factory.SeedAsync(seed =>
        {
            seed.AddCompany(otherCompanyId, "TARGET");
            seed.AddDept(otherCompanyId, "目標公司部門", 1);
            var target = seed.AddDocument("TG-A-01", "目標公司文件", otherCompanyId);
            seed.AddPublishedVersion(target.Id, "1.0", "store/TG/main.pdf");

            var own = seed.AddDocument("CP-A-01", "本公司文件");
            seed.AddPublishedVersion(own.Id, "1.0", "store/A/main.pdf");
        });
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);

        var response = await client.GetAsync(
            "/api/documents/permission-matrix?companyCode=target");
        var body = await response.Content.ReadFromJsonAsync<DocumentPermissionMatrixResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        var row = Assert.Single(body.Items);
        Assert.Equal("TG-A-01", row.DocumentCode);
        Assert.Equal(otherCompanyId, row.CompanyId);
        var department = Assert.Single(body.Departments);
        Assert.Equal("目標公司部門", department.Name);
    }

    [Fact]
    public async Task Matrix_InactiveDocument_ReportsCancelledEffectiveStatus()
    {
        await using var factory = new MatrixWebApplicationFactory();
        await factory.SeedAsync(seed =>
        {
            var document = seed.AddDocument("CP-A-01", "已停用文件", isActive: false);
            seed.AddPublishedVersion(document.Id, "1.0", "store/A/main.pdf");
        });
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);

        var body = await GetMatrixAsync(client);

        var row = Assert.Single(body.Items);
        Assert.Equal("CANCELLED", row.Status.Effective.Code);
    }

    [Fact]
    public async Task Matrix_WhenStoredMainFileIsMissing_ReportsMainDocumentError()
    {
        await using var factory = new MatrixWebApplicationFactory();
        await factory.SeedAsync(seed =>
        {
            var present = seed.AddDocument("CP-A-01", "檔案齊全");
            seed.AddPublishedVersion(present.Id, "1.0", "store/A/present.pdf");
            factory.Storage.WrittenKeys.Add("store/A/present.pdf");

            var missing = seed.AddDocument("CP-A-02", "檔案遺失");
            seed.AddPublishedVersion(missing.Id, "1.0", "store/A/gone.pdf");
            // 不加入 WrittenKeys → ExistsAsync 回 false
        });
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);

        var body = await GetMatrixAsync(client);

        var present = body.Items.Single(item => item.DocumentCode == "CP-A-01");
        Assert.Equal("NORMAL", present.Status.MainDocument.Code);
        Assert.False(present.Status.MainDocument.HasError);

        var missing = body.Items.Single(item => item.DocumentCode == "CP-A-02");
        Assert.Equal("ERROR", missing.Status.MainDocument.Code);
        Assert.True(missing.Status.MainDocument.HasError);
    }

    [Fact]
    public async Task Matrix_PaginationAndKeywordFilter_ApplyCorrectly()
    {
        await using var factory = new MatrixWebApplicationFactory();
        await factory.SeedAsync(seed =>
        {
            foreach (var index in Enumerable.Range(1, 5))
            {
                var document = seed.AddDocument($"AAA-{index:D2}", $"品質文件 {index}");
                seed.AddPublishedVersion(document.Id, "1.0", $"store/A/aaa-{index}.pdf");
            }

            var unrelated = seed.AddDocument("ZZZ-99", "其他文件");
            seed.AddPublishedVersion(unrelated.Id, "1.0", "store/A/zzz.pdf");
        });
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);

        var firstPage = await client.GetFromJsonAsync<DocumentPermissionMatrixResponse>(
            "/api/documents/permission-matrix?keyword=AAA&page=1&pageSize=2");
        Assert.NotNull(firstPage);
        Assert.Equal(5, firstPage.Pagination.TotalCount);
        Assert.Equal(3, firstPage.Pagination.TotalPages);
        Assert.Equal(2, firstPage.Items.Count);
        Assert.Equal("AAA-01", firstPage.Items[0].DocumentCode);
        Assert.Equal("AAA-02", firstPage.Items[1].DocumentCode);

        var lastPage = await client.GetFromJsonAsync<DocumentPermissionMatrixResponse>(
            "/api/documents/permission-matrix?keyword=AAA&page=3&pageSize=2");
        Assert.NotNull(lastPage);
        Assert.Single(lastPage.Items);
        Assert.Equal("AAA-05", lastPage.Items[0].DocumentCode);

        var unrelated = await client.GetFromJsonAsync<DocumentPermissionMatrixResponse>(
            "/api/documents/permission-matrix?keyword=zzz");
        Assert.NotNull(unrelated);
        var onlyRow = Assert.Single(unrelated.Items);
        Assert.Equal("ZZZ-99", onlyRow.DocumentCode);
    }

    private static async Task LoginAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("EMP001", AuthWebApplicationFactory.InitialPassword));
        response.EnsureSuccessStatusCode();
    }

    private static async Task<DocumentPermissionMatrixResponse> GetMatrixAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/documents/permission-matrix");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<DocumentPermissionMatrixResponse>();
        Assert.NotNull(body);
        return body;
    }
}

internal sealed class MatrixWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"matrix-{Guid.NewGuid():N}";

    public MatrixWebApplicationFactory(UserRole role = UserRole.COMPANY_ADMIN)
    {
        AuthStore = new FakeAuthUserStore();
        AuthStore.User.Role = role.ToString();
        OwnCompanyId = AuthStore.User.CompanyId;
        Storage = new FakeDocumentStorage();
    }

    public FakeAuthUserStore AuthStore { get; }
    public FakeDocumentStorage Storage { get; }
    public Guid OwnCompanyId { get; }

    public HttpClient CreateSecureClient() => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        HandleCookies = true,
        AllowAutoRedirect = false
    });

    public async Task SeedAsync(Action<MatrixSeed> configure)
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IsoDbContext>();
        var seed = new MatrixSeed(dbContext, OwnCompanyId, AuthStore.User.Id);
        seed.EnsureOwnCompanyAndDepartments();
        configure(seed);
        await dbContext.SaveChangesAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureTestServices(services =>
        {
            services.AddDataProtection().UseEphemeralDataProtectionProvider();

            services.RemoveAll<IAuthUserStore>();
            services.AddSingleton<IAuthUserStore>(AuthStore);

            services.RemoveAll<IDocumentStorage>();
            services.AddSingleton<IDocumentStorage>(Storage);

            services.RemoveAll<DbContextOptions<IsoDbContext>>();
            services.RemoveAll<DbContextOptions>();
            services.RemoveAll<Microsoft.EntityFrameworkCore.Storage.IDatabaseProvider>();
            services.RemoveAll<IsoDbContext>();
            var inMemoryServices = new ServiceCollection()
                .AddEntityFrameworkInMemoryDatabase()
                .BuildServiceProvider();
            services.AddDbContext<IsoDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName)
                    .UseInternalServiceProvider(inMemoryServices));
        });
    }
}

internal sealed class MatrixSeed(IsoDbContext dbContext, Guid ownCompanyId, Guid userId)
{
    public void EnsureOwnCompanyAndDepartments()
    {
        AddCompany(ownCompanyId, "OWN");
        AddDept(ownCompanyId, "總經理室", 1);
        AddDept(ownCompanyId, "人力資源部", 2);
        AddDept(ownCompanyId, "資訊中心", 3);
    }

    public void AddCompany(Guid companyId, string code)
    {
        dbContext.Companies.Add(new Company
        {
            Id = companyId,
            Code = code,
            Name = $"{code} 公司",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
    }

    public Dept AddDept(Guid companyId, string name, int seq)
    {
        var dept = new Dept
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Name = name,
            Seq = seq,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        dbContext.Depts.Add(dept);
        return dept;
    }

    public Document AddDocument(
        string documentNo,
        string name,
        Guid? companyId = null,
        bool isActive = true)
    {
        var document = new Document
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId ?? ownCompanyId,
            DocumentNo = documentNo,
            Name = name,
            IsActive = isActive,
            CreatedBy = userId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        dbContext.Documents.Add(document);
        return document;
    }

    public DocumentVersion AddPublishedVersion(Guid documentId, string version, string fileKey)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var entity = new DocumentVersion
        {
            Id = Guid.NewGuid(),
            DocumentId = documentId,
            Version = version,
            VersionMajor = 1,
            VersionMinor = 0,
            Status = "PUBLISHED",
            PublishDate = today,
            EffectiveDate = today,
            FileKey = fileKey,
            OriginalFileName = "main.pdf",
            ContentType = "application/pdf",
            FileSize = 1024,
            Checksum = "checksum",
            CreatedBy = userId,
            CreatedAt = DateTimeOffset.UtcNow
        };
        dbContext.DocumentVersions.Add(entity);
        return entity;
    }

    public void GrantPermission(Guid documentId, Guid deptId)
    {
        dbContext.DocumentDeptPermissions.Add(new DocumentDeptPermission
        {
            Id = Guid.NewGuid(),
            DocumentId = documentId,
            DeptId = deptId,
            GrantedBy = userId,
            CreatedAt = DateTimeOffset.UtcNow
        });
    }
}

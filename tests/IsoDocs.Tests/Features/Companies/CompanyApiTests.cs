using System.Net;
using System.Net.Http.Json;
using IsoDocument.Api.Common;
using IsoDocument.Api.Data.Entities;
using IsoDocument.Api.Features.Auth;
using IsoDocument.Api.Features.Auth.Dtos;
using IsoDocument.Api.Features.Companies;
using IsoDocument.Api.Features.Companies.Dtos;
using IsoDocument.Api.Security;
using IsoDocs.Tests.Features.Auth;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Xunit;

namespace IsoDocs.Tests.Features.Companies;

public sealed class CompanyApiTests
{
    [Theory]
    [InlineData("acm")]
    [InlineData("艾克米")]
    public async Task List_AsSystemAdminFiltersByCodeOrName_ReturnsPagedCompanies(
        string keyword)
    {
        await using var factory = new CompanyWebApplicationFactory(UserRole.SYSTEM_ADMIN);
        factory.CompanyStore.AddSeed("ACME", "艾克米股份有限公司");
        factory.CompanyStore.AddSeed("BETA", "貝塔有限公司");
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);

        var response = await client.GetAsync(
            $"/api/companies?keyword={Uri.EscapeDataString(keyword)}&page=1&pageSize=20");
        var body = await response.Content.ReadFromJsonAsync<PagedResult<CompanyResponse>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        var company = Assert.Single(body.Items);
        Assert.Equal("ACME", company.Code);
        Assert.Equal("艾克米股份有限公司", company.Name);
        Assert.Equal(1, body.TotalCount);
    }

    [Fact]
    public async Task List_AsCompanyAdmin_ReturnsForbidden()
    {
        await using var factory = new CompanyWebApplicationFactory(UserRole.COMPANY_ADMIN);
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);

        var response = await client.GetAsync("/api/companies");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static async Task LoginAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("EMP001", AuthWebApplicationFactory.InitialPassword));
        response.EnsureSuccessStatusCode();
    }
}

internal sealed class CompanyWebApplicationFactory : WebApplicationFactory<Program>
{
    public CompanyWebApplicationFactory(UserRole role)
    {
        AuthStore = new FakeAuthUserStore();
        AuthStore.User.Role = role.ToString();
        CompanyStore = new FakeCompanyStore();
    }

    public FakeAuthUserStore AuthStore { get; }

    public FakeCompanyStore CompanyStore { get; }

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
            services.RemoveAll<ICompanyStore>();
            services.AddSingleton<ICompanyStore>(CompanyStore);
        });
    }
}

internal sealed class FakeCompanyStore : ICompanyStore
{
    public List<Company> Companies { get; } = [];

    public Company AddSeed(string code, string name)
    {
        var now = DateTimeOffset.UtcNow;
        var company = new Company
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = name,
            CreatedAt = now,
            UpdatedAt = now
        };
        Companies.Add(company);
        return company;
    }

    public Task<int> CountAsync(string? keyword, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Filter(keyword).Count());
    }

    public Task<IReadOnlyList<Company>> ListAsync(
        string? keyword,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<Company> result = Filter(keyword)
            .OrderBy(company => company.Code)
            .ThenBy(company => company.Name)
            .ThenBy(company => company.Id)
            .Skip(skip)
            .Take(take)
            .ToArray();
        return Task.FromResult(result);
    }

    private IEnumerable<Company> Filter(string? keyword) =>
        string.IsNullOrEmpty(keyword)
            ? Companies
            : Companies.Where(company =>
                company.Code.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || company.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase));
}

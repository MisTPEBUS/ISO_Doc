using System.Net;
using System.Net.Http.Json;
using IsoDocument.Api.Data.Entities;
using IsoDocument.Api.Features.Auth;
using IsoDocument.Api.Features.Auth.Dtos;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Xunit;

namespace IsoDocs.Tests.Features.Auth;

public sealed class AuthApiTests
{
    [Fact]
    public async Task Login_WithCorrectCredentials_ReturnsUserAndCreatesSession()
    {
        await using var factory = new AuthWebApplicationFactory();
        using var client = factory.CreateSecureClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("  EMP001  ", AuthWebApplicationFactory.InitialPassword));
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(factory.Store.User.Id, body.UserId);
        Assert.Equal(factory.Store.User.Name, body.Name);
        Assert.Equal(factory.Store.User.Role, body.Role);
        Assert.Equal(factory.Store.User.CompanyId, body.CompanyId);
        Assert.NotNull(factory.Store.User.LastLoginAt);
        var cookies = response.Headers.GetValues("Set-Cookie").ToArray();
        Assert.Contains(
            cookies,
            value => value.StartsWith("isodocs.auth=", StringComparison.Ordinal)
                && value.Contains("secure", StringComparison.OrdinalIgnoreCase)
                && value.Contains("httponly", StringComparison.OrdinalIgnoreCase)
                && value.Contains("samesite=lax", StringComparison.OrdinalIgnoreCase)
                && !value.Contains("max-age", StringComparison.OrdinalIgnoreCase)
                && !value.Contains("expires", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            cookies,
            value => value.StartsWith("isodocs.xsrf=", StringComparison.Ordinal)
                && !value.Contains("httponly", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Login_WithIncorrectPassword_ReturnsUnauthorizedWithoutAccountDetails()
    {
        await using var factory = new AuthWebApplicationFactory();
        using var client = factory.CreateSecureClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("EMP001", "incorrect-password"));
        var body = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("The employee number or password is incorrect.", body.Detail);
        Assert.DoesNotContain("active", body.Detail, StringComparison.OrdinalIgnoreCase);
        Assert.Null(factory.Store.User.LastLoginAt);
    }

    [Fact]
    public async Task Login_WhenPasswordDigestIsNull_ReturnsSameUnauthorizedResponse()
    {
        await using var factory = new AuthWebApplicationFactory();
        factory.Store.User.PasswordDigest = null;
        using var client = factory.CreateSecureClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest("EMP001", "any-password"));
        var body = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("The employee number or password is incorrect.", body.Detail);
    }

    [Fact]
    public async Task AntiforgeryToken_WhenAnonymous_IssuesReadableRequestTokenCookie()
    {
        await using var factory = new AuthWebApplicationFactory();
        using var client = factory.CreateSecureClient();

        var response = await client.GetAsync("/api/antiforgery/token");
        var cookie = response.Headers
            .GetValues("Set-Cookie")
            .Single(value => value.StartsWith("isodocs.xsrf=", StringComparison.Ordinal));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("httponly", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ChangePassword_WithValidRequest_ChangesPasswordAndClearsRequiredFlag()
    {
        await using var factory = new AuthWebApplicationFactory();
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);
        var antiforgeryToken = await GetAntiforgeryTokenAsync(client);
        using var request = CreateChangePasswordRequest(
            antiforgeryToken,
            new ChangePasswordRequest(
                AuthWebApplicationFactory.InitialPassword,
                "new-password-123",
                "new-password-123"));

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var hasher = new PasswordHasher<User>();
        var updatedDigest = Assert.IsType<string>(factory.Store.User.PasswordDigest);
        Assert.NotEqual(
            PasswordVerificationResult.Failed,
            hasher.VerifyHashedPassword(
                factory.Store.User,
                updatedDigest,
                "new-password-123"));
        Assert.False(factory.Store.User.MustChangePassword);
        Assert.Contains(
            response.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith("isodocs.auth=", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ChangePassword_WhenConfirmationDoesNotMatch_ReturnsFieldValidationError()
    {
        await using var factory = new AuthWebApplicationFactory();
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);
        var originalDigest = factory.Store.User.PasswordDigest;
        var antiforgeryToken = await GetAntiforgeryTokenAsync(client);
        using var request = CreateChangePasswordRequest(
            antiforgeryToken,
            new ChangePasswordRequest(
                AuthWebApplicationFactory.InitialPassword,
                "new-password-123",
                "does-not-match"));

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(body);
        Assert.Contains("newPasswordConfirmation", body.Errors.Keys);
        Assert.Equal(originalDigest, factory.Store.User.PasswordDigest);
    }

    [Fact]
    public async Task Me_WhenAuthenticated_ReturnsCurrentUserContract()
    {
        await using var factory = new AuthWebApplicationFactory();
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);

        var response = await client.GetAsync("/api/auth/me");
        var body = await response.Content.ReadFromJsonAsync<MeResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(factory.Store.User.Id, body.UserId);
        Assert.Equal(factory.Store.User.Empno, body.Empno);
        Assert.Equal(factory.Store.User.DeptId, body.DeptId);
        Assert.True(body.MustChangePassword);
    }

    [Fact]
    public async Task Logout_WithAntiforgeryToken_ClearsAuthenticationSession()
    {
        await using var factory = new AuthWebApplicationFactory();
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);
        var antiforgeryToken = await GetAntiforgeryTokenAsync(client);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        request.Headers.Add("X-XSRF-TOKEN", antiforgeryToken);

        var response = await client.SendAsync(request);
        var meResponse = await client.GetAsync("/api/auth/me");
        var problem = await meResponse.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, meResponse.StatusCode);
        Assert.NotNull(problem);
        Assert.Equal(401, problem.Status);
    }

    [Fact]
    public async Task ChangePassword_WithoutAntiforgeryToken_ReturnsProblemDetails()
    {
        await using var factory = new AuthWebApplicationFactory();
        using var client = factory.CreateSecureClient();
        await LoginAsync(client);

        var response = await client.PostAsJsonAsync(
            "/api/auth/change-password",
            new ChangePasswordRequest(
                AuthWebApplicationFactory.InitialPassword,
                "new-password-123",
                "new-password-123"));
        var body = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.NotNull(body);
        Assert.Equal(400, body.Status);
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
        var cookie = response.Headers
            .GetValues("Set-Cookie")
            .Single(value => value.StartsWith("isodocs.xsrf=", StringComparison.Ordinal));
        var encodedToken = cookie["isodocs.xsrf=".Length..cookie.IndexOf(';')];
        return Uri.UnescapeDataString(encodedToken);
    }

    private static HttpRequestMessage CreateChangePasswordRequest(
        string antiforgeryToken,
        ChangePasswordRequest body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/change-password")
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Add("X-XSRF-TOKEN", antiforgeryToken);
        return request;
    }
}

internal sealed class AuthWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string InitialPassword = "initial-password";

    public AuthWebApplicationFactory()
    {
        Store = new FakeAuthUserStore();
    }

    public FakeAuthUserStore Store { get; }

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
            services.AddSingleton<IAuthUserStore>(Store);
        });
    }
}

internal sealed class FakeAuthUserStore : IAuthUserStore
{
    public FakeAuthUserStore()
    {
        User = new User
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            CompanyId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            DeptId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            Empno = "EMP001",
            Name = "Test User",
            Role = "USER",
            IsActive = true,
            MustChangePassword = true,
            NotifyEmailEnabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        User.PasswordDigest = new PasswordHasher<User>()
            .HashPassword(User, AuthWebApplicationFactory.InitialPassword);
    }

    public User User { get; }

    public Task<User?> FindByEmpnoAsync(string empno, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<User?>(User.Empno == empno ? User : null);
    }

    public Task<User?> FindByIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<User?>(User.Id == userId ? User : null);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}

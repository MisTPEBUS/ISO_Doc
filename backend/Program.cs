using FluentValidation;
using IsoDocument.Api.Common;
using IsoDocument.Api.Data;
using IsoDocument.Api.Data.Entities;
using IsoDocument.Api.Features.Auth;
using IsoDocument.Api.Features.Auth.Dtos;
using IsoDocument.Api.Features.Auth.Validators;
using IsoDocument.Api.Features.AuditLogs;
using IsoDocument.Api.Features.Backup;
using IsoDocument.Api.Features.Depts;
using IsoDocument.Api.Features.Depts.Dtos;
using IsoDocument.Api.Features.Depts.Validators;
using IsoDocument.Api.Features.Documents;
using IsoDocument.Api.Features.Documents.Dtos;
using IsoDocument.Api.Features.Documents.Validators;
using IsoDocument.Api.Features.Health;
using IsoDocument.Api.Features.Home;
using IsoDocument.Api.Features.Permissions;
using IsoDocument.Api.Features.Permissions.Dtos;
using IsoDocument.Api.Features.Permissions.Validators;
using IsoDocument.Api.Features.Users;
using IsoDocument.Api.Features.Users.Dtos;
using IsoDocument.Api.Features.Users.Validators;
using IsoDocument.Api.Security;
using IsoDocument.Api.Security.Authorization;
using IsoDocument.Api.Storage;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serilog;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, loggerConfiguration) =>
    loggerConfiguration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console(new RenderedCompactJsonFormatter()));

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddControllersWithViews(options =>
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
builder.Services.AddAntiforgery(options =>
{
        options.HeaderName = "X-XSRF-TOKEN";
    options.Cookie.Name = "isodocs.antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
});
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "isodocs.auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.EventsType = typeof(ApiCookieAuthenticationEvents);
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Policies.CompanyAdminScope, policy =>
        policy.RequireAuthenticatedUser()
            .RequireRole(nameof(UserRole.COMPANY_ADMIN), nameof(UserRole.SYSTEM_ADMIN))
            .AddRequirements(new CompanyScopeRequirement()));
    options.AddPolicy(Policies.DocumentAccess, policy =>
        policy.RequireAuthenticatedUser().AddRequirements(new DocumentAccessRequirement()));
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ApiCookieAuthenticationEvents>();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<IAuthSession, CookieAuthSession>();
builder.Services.AddScoped<IAuthorizationHandler, CompanyScopeHandler>();
builder.Services.AddScoped<IAuthorizationHandler, DocumentAccessHandler>();
builder.Services.AddScoped<IDocumentAccessStore, EfDocumentAccessStore>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<IValidator<LoginRequest>, LoginRequestValidator>();
builder.Services.AddScoped<IValidator<ChangePasswordRequest>, ChangePasswordRequestValidator>();
builder.Services.AddScoped<IValidator<CreateDeptRequest>, CreateDeptRequestValidator>();
builder.Services.AddScoped<IValidator<UpdateDeptRequest>, UpdateDeptRequestValidator>();
builder.Services.AddScoped<IValidator<CreateDocumentRequest>, CreateDocumentRequestValidator>();
builder.Services.AddScoped<IValidator<UpdateDocumentRequest>, UpdateDocumentRequestValidator>();
builder.Services.AddScoped<IValidator<CreateDocumentVersionRequest>, CreateDocumentVersionRequestValidator>();
builder.Services.AddScoped<IValidator<CreateAttachmentsRequest>, CreateAttachmentsRequestValidator>();
builder.Services.AddScoped<IValidator<UpdateDocumentDeptPermissionsRequest>, UpdateDocumentDeptPermissionsRequestValidator>();
builder.Services.AddScoped<IValidator<CreateUserRequest>, CreateUserRequestValidator>();
builder.Services.AddScoped<IValidator<UpdateUserRequest>, UpdateUserRequestValidator>();
builder.Services.AddOptions<StorageOptions>()
    .BindConfiguration(StorageOptions.SectionName)
    .Validate(
        options => !string.IsNullOrWhiteSpace(options.RootPath),
        "Storage:RootPath must be configured.")
    .ValidateOnStart();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<StoragePathGuard>();
builder.Services.AddSingleton<StorageKeyBuilder>();
builder.Services.AddSingleton<IDocumentStorage, LocalFileStorage>();
builder.Services.AddHealthChecks()
    .AddCheck<StorageHealthCheck>("storage", tags: ["startup"]);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found.");

builder.Services.AddDbContext<IsoDbContext>(options =>
    options.UseNpgsql(connectionString));
builder.Services.AddScoped<IAuthUserStore, EfAuthUserStore>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IDeptStore, EfDeptStore>();
builder.Services.AddScoped<IDeptService, DeptService>();
builder.Services.AddScoped<IDocumentStore, EfDocumentStore>();
builder.Services.AddScoped<IDocumentService, DocumentService>();
builder.Services.AddScoped<IDocumentVersionStore, EfDocumentVersionStore>();
builder.Services.AddScoped<IDocumentVersionService, DocumentVersionService>();
builder.Services.AddScoped<IAttachmentStore, EfAttachmentStore>();
builder.Services.AddScoped<IAttachmentService, AttachmentService>();
builder.Services.AddScoped<IDocumentPermissionStore, EfDocumentPermissionStore>();
builder.Services.AddScoped<IDocumentPermissionService, DocumentPermissionService>();
builder.Services.AddScoped<IAuditLogStore, EfAuditLogStore>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddScoped<IUserStore, EfUserStore>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IHealthService, HealthService>();
builder.Services.AddScoped<IBackupStore, EfBackupStore>();
builder.Services.AddScoped<IBackupService, BackupService>();
builder.Services.AddScoped<IDocumentsBrowseStore, EfDocumentsBrowseStore>();
builder.Services.AddScoped<IDocumentsBrowseService, DocumentsBrowseService>();
builder.Services.AddScoped<IDownloadAuditLogService>(serviceProvider =>
    serviceProvider.GetRequiredService<IAuditLogService>() as IDownloadAuditLogService
    ?? throw new InvalidOperationException("The audit log service does not support download auditing."));
builder.Services.AddScoped<IBackupAuditLogService>(serviceProvider =>
    serviceProvider.GetRequiredService<IAuditLogService>() as IBackupAuditLogService
    ?? throw new InvalidOperationException("The audit log service does not support backup auditing."));
builder.Services.AddScoped<IOperationAuditLogService>(serviceProvider =>
    serviceProvider.GetRequiredService<IAuditLogService>() as IOperationAuditLogService
    ?? throw new InvalidOperationException("The audit log service does not support operation auditing."));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseStatusCodePages();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

var startupHealth = app.Services
    .GetRequiredService<HealthCheckService>()
    .CheckHealthAsync(registration => registration.Tags.Contains("startup"))
    .GetAwaiter()
    .GetResult();
if (startupHealth.Status != HealthStatus.Healthy)
{
    throw new InvalidOperationException("Storage health check failed during startup.");
}

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

public partial class Program
{
}

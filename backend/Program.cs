using FluentValidation;
using IsoDocument.Api.Common;
using IsoDocument.Api.Data;
using IsoDocument.Api.Data.Entities;
using IsoDocument.Api.Features.AiImport;
using IsoDocument.Api.Features.AiImport.Dtos;
using IsoDocument.Api.Features.AiImport.Llm;
using IsoDocument.Api.Features.AiImport.Validators;
using IsoDocument.Api.Features.Auth;
using IsoDocument.Api.Features.Auth.Dtos;
using IsoDocument.Api.Features.Auth.Validators;
using IsoDocument.Api.Features.AuditLogs;
using IsoDocument.Api.Features.Backup;
using IsoDocument.Api.Features.Companies;
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
using Microsoft.AspNetCore.HttpOverrides;
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
builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = context =>
    {
        // 將框架自動產生的英文標題（找不到路由的 404、模型繫結失敗的 400 等）改為中文。
        var localizedTitle = (context.ProblemDetails.Status ?? context.HttpContext.Response.StatusCode) switch
        {
            StatusCodes.Status400BadRequest => "輸入資料有誤",
            StatusCodes.Status401Unauthorized => "尚未登入",
            StatusCodes.Status403Forbidden => "沒有權限",
            StatusCodes.Status404NotFound => "找不到資源",
            StatusCodes.Status405MethodNotAllowed => "不支援的操作",
            StatusCodes.Status409Conflict => "資料衝突",
            StatusCodes.Status415UnsupportedMediaType => "不支援的內容格式",
            StatusCodes.Status429TooManyRequests => "請求過於頻繁",
            >= 500 => "伺服器發生錯誤",
            _ => context.ProblemDetails.Title
        };

        // 只覆寫框架預設標題，服務層自訂的中文標題維持不變。
        if (localizedTitle is not null && IsFrameworkDefaultTitle(context.ProblemDetails.Title))
        {
            context.ProblemDetails.Title = localizedTitle;
        }
    });

// 框架預設標題一律是英文（ASCII 開頭）；服務層自訂的中文標題不覆寫。
static bool IsFrameworkDefaultTitle(string? title) =>
    string.IsNullOrEmpty(title) || char.IsAscii(title[0]);

builder.Services.AddControllersWithViews(options =>
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-XSRF-TOKEN";
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "isodocs.auth";
        options.Cookie.HttpOnly = true;

        // HTTP -> 非 Secure
        // HTTPS -> Secure
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;

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
    options.AddPolicy(Policies.AttachmentAccess, policy =>
        policy.RequireAuthenticatedUser().AddRequirements(new AttachmentAccessRequirement()));
    options.AddPolicy(Policies.SystemAdmin, policy =>
        policy.RequireAuthenticatedUser().AddRequirements(new SystemAdminRequirement()));
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ApiCookieAuthenticationEvents>();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<IAuthSession, CookieAuthSession>();
builder.Services.AddScoped<IAuthorizationHandler, CompanyScopeHandler>();
builder.Services.AddScoped<IAuthorizationHandler, DocumentAccessHandler>();
builder.Services.AddScoped<IAuthorizationHandler, AttachmentAccessHandler>();
builder.Services.AddScoped<IAuthorizationHandler, SystemAdminHandler>();
builder.Services.AddScoped<IDocumentAccessStore, EfDocumentAccessStore>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<IValidator<LoginRequest>, LoginRequestValidator>();
builder.Services.AddScoped<IValidator<ChangePasswordRequest>, ChangePasswordRequestValidator>();
builder.Services.AddScoped<IValidator<CreateDeptRequest>, CreateDeptRequestValidator>();
builder.Services.AddScoped<IValidator<UpdateDeptRequest>, UpdateDeptRequestValidator>();
builder.Services.AddScoped<IValidator<CreateDocumentRequest>, CreateDocumentRequestValidator>();
builder.Services.AddScoped<IValidator<UpdateDocumentRequest>, UpdateDocumentRequestValidator>();
builder.Services.AddScoped<IValidator<CreateDocumentVersionRequest>, CreateDocumentVersionRequestValidator>();
builder.Services.AddScoped<IValidator<UploadDocumentVersionFileRequest>, UploadDocumentVersionFileRequestValidator>();
builder.Services.AddScoped<IValidator<CreateAttachmentRequest>, CreateAttachmentRequestValidator>();
builder.Services.AddScoped<IValidator<CreateAttachmentVersionRequest>, CreateAttachmentVersionRequestValidator>();
builder.Services.AddScoped<IValidator<UpdateDocumentDeptPermissionsRequest>, UpdateDocumentDeptPermissionsRequestValidator>();
builder.Services.AddScoped<IValidator<UpdateDocumentPermissionMatrixRequest>, UpdateDocumentPermissionMatrixRequestValidator>();
builder.Services.AddScoped<IValidator<CreateUserRequest>, CreateUserRequestValidator>();
builder.Services.AddScoped<IValidator<UpdateUserRequest>, UpdateUserRequestValidator>();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    // NAS 部署：唯一能連到這個 container 的是同一個 compose network 裡的 frontend(nginx)，
    // 其 IP 由 Docker 動態配發，因此清空 KnownProxies/KnownNetworks（不限制來源）。
    // 只有在 backend 不對外露 port 時，這個設定才是安全的。
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});
builder.Services.AddOptions<StorageOptions>()
    .BindConfiguration(StorageOptions.SectionName)
    .Validate(
        options => !string.IsNullOrWhiteSpace(options.RootPath),
        "Storage:RootPath must be configured.")
    .ValidateOnStart();
builder.Services.AddOptions<AiImportLlmOptions>()
    .BindConfiguration(AiImportLlmOptions.SectionName);
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
builder.Services.AddScoped<ICompanyStore, EfCompanyStore>();
builder.Services.AddScoped<ICompanyService, CompanyService>();
builder.Services.AddScoped<IDeptStore, EfDeptStore>();
builder.Services.AddScoped<IDeptService, DeptService>();
builder.Services.AddScoped<IDocumentStore, EfDocumentStore>();
builder.Services.AddScoped<IDocumentService, DocumentService>();
builder.Services.AddScoped<IDocumentVersionStore, EfDocumentVersionStore>();
builder.Services.AddScoped<IDocumentVersionService, DocumentVersionService>();
builder.Services.AddScoped<IAttachmentStore, EfAttachmentStore>();
builder.Services.AddScoped<IAttachmentService, AttachmentService>();
builder.Services.AddScoped<IAttachmentVersionStore, EfAttachmentVersionStore>();
builder.Services.AddScoped<IAttachmentVersionService, AttachmentVersionService>();
builder.Services.AddScoped<IDocumentPermissionStore, EfDocumentPermissionStore>();
builder.Services.AddScoped<IDocumentPermissionService, DocumentPermissionService>();
builder.Services.AddScoped<IDocumentPermissionMatrixService, DocumentPermissionMatrixService>();
builder.Services.AddScoped<IAuditLogStore, EfAuditLogStore>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddScoped<IUserStore, EfUserStore>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IHealthService, HealthService>();
builder.Services.AddScoped<IBackupStore, EfBackupStore>();
builder.Services.AddScoped<IBackupService, BackupService>();
builder.Services.AddScoped<IDocumentsBrowseStore, EfDocumentsBrowseStore>();
builder.Services.AddScoped<IDocumentsBrowseService, DocumentsBrowseService>();
builder.Services.AddSingleton<IPdfWatermarkService, PdfWatermarkService>();
builder.Services.AddScoped<IAiImportStore, EfAiImportStore>();
builder.Services.AddScoped<IAiImportService, AiImportService>();
builder.Services.AddScoped<ILlmImportAnalyzer, OpenAiImportAnalyzer>();
builder.Services.AddScoped<IValidator<AnalyzeImportRequest>, AnalyzeImportRequestValidator>();
builder.Services.AddScoped<IValidator<CommitImportRequest>, CommitImportRequestValidator>();
builder.Services.AddScoped<IValidator<CommitImportDocumentItem>, CommitImportDocumentItemValidator>();
builder.Services.AddScoped<IValidator<CommitImportAttachmentItem>, CommitImportAttachmentItemValidator>();
builder.Services.AddScoped<IDownloadAuditLogService>(serviceProvider =>
    serviceProvider.GetRequiredService<IAuditLogService>() as IDownloadAuditLogService
    ?? throw new InvalidOperationException("The audit log service does not support download auditing."));
builder.Services.AddScoped<IBackupAuditLogService>(serviceProvider =>
    serviceProvider.GetRequiredService<IAuditLogService>() as IBackupAuditLogService
    ?? throw new InvalidOperationException("The audit log service does not support backup auditing."));
builder.Services.AddScoped<IOperationAuditLogService>(serviceProvider =>
    serviceProvider.GetRequiredService<IAuditLogService>() as IOperationAuditLogService
    ?? throw new InvalidOperationException("The audit log service does not support operation auditing."));

// 主文下載浮水印用字型：PDFsharp 6.x 不吃系統字型（GDI-free），必須在第一次用到 XFont 前
// 設定好 GlobalFontSettings.FontResolver，設定一次即可、全程序共用。
PdfSharp.Fonts.GlobalFontSettings.FontResolver = new WatermarkFontResolver();

var app = builder.Build();

// 必須排在 UseHttpsRedirection 前面，讓 Kestrel 先用 X-Forwarded-Proto 修正 Request.Scheme，
// 否則 reverse proxy 後面的請求會被誤判為 plain HTTP，造成 redirect loop。
app.UseForwardedHeaders();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseStatusCodePages();
/* app.UseHttpsRedirection(); */
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

if (app.Configuration.GetValue("Database:AutoMigrate", true))
{
    using var migrationScope = app.Services.CreateScope();
    migrationScope.ServiceProvider.GetRequiredService<IsoDbContext>().Database.Migrate();
}

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

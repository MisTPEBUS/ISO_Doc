using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using IsoDocument.Api.Data.Entities;
using IsoDocument.Api.Data;
using IsoDocument.Api.Features.AuditLogs;
using IsoDocument.Api.Security;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IsoDocs.Tests.Features.AuditLogs;

public sealed partial class AuditLogServiceTests
{
    [Fact]
    public async Task WriteAsync_PersistsReadableDetailAndSnapshotsActorEmpno()
    {
        var store = new RecordingAuditLogStore();
        var currentUser = new AuditCurrentUser
        {
            UserId = Guid.NewGuid(),
            Empno = "EMP001"
        };
        var service = new AuditLogService(
            store,
            currentUser,
            new AuditFixedTimeProvider(
                new DateTimeOffset(2026, 9, 9, 1, 2, 3, TimeSpan.Zero)));
        var companyId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();

        await service.WriteAsync(
            new AuditLogWriteRequest(
                companyId,
                AuditActions.UpdateDept,
                AuditResourceTypes.Dept,
                resourceId,
                new
                {
                    old_value = new { name = "Old" },
                    new_value = new { name = "New" }
                }),
            CancellationToken.None);
        currentUser.Empno = "CHANGED";

        var entry = Assert.Single(store.Entries);
        Assert.Equal(companyId, entry.CompanyId);
        Assert.Equal(currentUser.UserId, entry.UserId);
        Assert.Equal("EMP001", entry.Empno);
        Assert.Equal(AuditActions.UpdateDept, entry.Action);
        Assert.Equal(AuditResourceTypes.Dept, entry.ResourceType);
        Assert.Equal(resourceId, entry.ResourceId);
        Assert.Equal(
            new DateTimeOffset(2026, 9, 9, 1, 2, 3, TimeSpan.Zero),
            entry.CreatedAt);
        using var detail = JsonDocument.Parse(entry.Detail!);
        Assert.Equal("Old", detail.RootElement.GetProperty("old_value").GetProperty("name").GetString());
        Assert.Equal("New", detail.RootElement.GetProperty("new_value").GetProperty("name").GetString());
        Assert.Equal(1, store.SaveCount);
    }

    [Fact]
    public async Task SpecializedOperations_WriteExpectedActionsResourcesAndPermissionDiff()
    {
        var store = new RecordingAuditLogStore();
        var service = new AuditLogService(
            store,
            new AuditCurrentUser { UserId = Guid.NewGuid(), Empno = "EMP002" },
            TimeProvider.System);
        var companyId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var attachmentId = Guid.NewGuid();
        var oldDeptId = Guid.NewGuid();
        var newDeptId = Guid.NewGuid();

        await service.WriteDocumentDeptPermissionsChangedAsync(
            companyId, documentId, [oldDeptId], [newDeptId],
            CancellationToken.None);
        await service.WriteDocumentDownloadedAsync(
            companyId, versionId, CancellationToken.None);
        await service.WriteAttachmentDownloadedAsync(
            companyId, attachmentId, CancellationToken.None);
        await service.WriteCompanyBackupCreatedAsync(
            companyId, CancellationToken.None);

        Assert.Collection(
            store.Entries,
            entry =>
            {
                Assert.Equal(AuditActions.UpdateDocumentDeptPermissions, entry.Action);
                Assert.Equal(AuditResourceTypes.Document, entry.ResourceType);
                using var detail = JsonDocument.Parse(entry.Detail!);
                Assert.Equal(
                    oldDeptId,
                    detail.RootElement.GetProperty("old_value")[0].GetGuid());
                Assert.Equal(
                    newDeptId,
                    detail.RootElement.GetProperty("new_value")[0].GetGuid());
            },
            entry =>
            {
                Assert.Equal(AuditActions.DownloadDocument, entry.Action);
                Assert.Equal(AuditResourceTypes.DocumentVersion, entry.ResourceType);
                Assert.Null(entry.Detail);
            },
            entry =>
            {
                Assert.Equal(AuditActions.DownloadAttachment, entry.Action);
                Assert.Equal(AuditResourceTypes.Attachment, entry.ResourceType);
                Assert.Null(entry.Detail);
            },
            entry =>
            {
                Assert.Equal(AuditActions.BackupCompanyDocuments, entry.Action);
                Assert.Equal(AuditResourceTypes.Company, entry.ResourceType);
                Assert.Equal(companyId, entry.ResourceId);
                Assert.Null(entry.Detail);
            });
    }

    [Fact]
    public void AuditActionCatalog_UsesUppercaseUnderscoreNames()
    {
        var actions = typeof(AuditActions)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(field => Assert.IsType<string>(field.GetRawConstantValue()))
            .ToArray();

        Assert.NotEmpty(actions);
        Assert.All(actions, action => Assert.Matches(ActionNamePattern(), action));
        Assert.Equal(actions.Length, actions.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void AuditLogModel_MapsDetailToJsonbAndEmpnoAsRequiredSnapshot()
    {
        var options = new DbContextOptionsBuilder<IsoDbContext>()
            .UseNpgsql("Host=localhost;Database=model_only;Username=test;Password=test")
            .Options;
        using var dbContext = new IsoDbContext(options);
        var entity = dbContext.Model.FindEntityType(typeof(AuditLog));

        Assert.NotNull(entity);
        Assert.Equal("jsonb", entity.FindProperty(nameof(AuditLog.Detail))!.GetColumnType());
        Assert.False(entity.FindProperty(nameof(AuditLog.Empno))!.IsNullable);
        Assert.Equal(30, entity.FindProperty(nameof(AuditLog.Empno))!.GetMaxLength());
    }

    [GeneratedRegex("^[A-Z]+(?:_[A-Z]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex ActionNamePattern();
}

internal sealed class RecordingAuditLogStore : IAuditLogStore
{
    public List<AuditLog> Entries { get; } = [];
    public int SaveCount { get; private set; }

    public void Add(AuditLog auditLog) => Entries.Add(auditLog);

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SaveCount++;
        return Task.CompletedTask;
    }
}

internal sealed class AuditCurrentUser : ICurrentUser
{
    public bool IsAuthenticated => UserId.HasValue;
    public Guid? UserId { get; set; }
    public Guid? CompanyId { get; set; }
    public Guid? DeptId { get; set; }
    public string? Empno { get; set; }
    public UserRole? Role { get; set; }

    public bool CanAccessCompany(Guid companyId) => true;
}

internal sealed class AuditFixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;
}

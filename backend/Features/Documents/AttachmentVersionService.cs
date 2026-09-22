using FluentValidation;
using FluentValidation.Results;
using IsoDocument.Api.Common;
using IsoDocument.Api.Data.Entities;
using IsoDocument.Api.Features.AuditLogs;
using IsoDocument.Api.Features.Documents.Dtos;
using IsoDocument.Api.Security;
using IsoDocument.Api.Storage;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace IsoDocument.Api.Features.Documents;

public sealed class AttachmentVersionService(
    IAttachmentVersionStore versionStore,
    IDocumentStorage documentStorage,
    StorageKeyBuilder storageKeyBuilder,
    ICurrentUser currentUser,
    IValidator<CreateAttachmentVersionRequest> validator,
    IOperationAuditLogService auditLogService,
    TimeProvider timeProvider) : IAttachmentVersionService
{
    public async Task<Result<AttachmentVersionResponse>> CreateAsync(
        Guid attachmentId,
        CreateAttachmentVersionRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<AttachmentVersionResponse>.ValidationFailed(ToErrors(validation));
        }

        await using (var validationStream = request.File!.OpenReadStream())
        {
            if (!await AttachmentFileRules.HasValidContentAsync(
                request.File.FileName, validationStream, cancellationToken))
            {
                return Result<AttachmentVersionResponse>.ValidationFailed(
                    new Dictionary<string, string[]>(StringComparer.Ordinal)
                    {
                        ["file"] = ["表單及附件檔案內容與副檔名不符。"]
                    });
            }
        }

        var context = await versionStore.FindCreateContextAsync(attachmentId, cancellationToken);
        if (context is null)
        {
            return Result<AttachmentVersionResponse>.NotFound("找不到指定的表單及附件。");
        }

        if (!context.AttachmentIsActive)
        {
            return Result<AttachmentVersionResponse>.Conflict(
                "已停用的表單及附件無法新增版本。");
        }

        if (!currentUser.CanAccessCompany(context.CompanyId))
        {
            return Result<AttachmentVersionResponse>.Forbidden(
                "您沒有為此表單及附件新增版本的權限。");
        }

        if (currentUser.UserId is not { } userId)
        {
            return Result<AttachmentVersionResponse>.Unauthorized("請先登入後再操作。");
        }

        string? writtenObjectKey = null;
        try
        {
            await using var transaction = await versionStore.BeginTransactionAsync(cancellationToken);
            var latest = await versionStore.FindLatestVersionAsync(attachmentId, cancellationToken);
            var (major, minor) = CalculateNextVersion(latest, request.ChangeType!);
            var versionText = $"{major}.{minor}";
            var publishDate = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
            var effectiveDate = request.EffectiveDate ?? publishDate;
            var objectKey = storageKeyBuilder.BuildAttachmentKey(
                context.CompanyCode,
                context.DocumentNo,
                context.AttachmentNo,
                context.AttachmentId,
                versionText,
                Guid.NewGuid(),
                request.File!.FileName);

            await using var fileStream = request.File.OpenReadStream();
            var writeResult = await documentStorage.WriteAsync(
                objectKey, fileStream, cancellationToken);
            writtenObjectKey = objectKey;

            var previousPublished = await versionStore.ListPublishedVersionsAsync(
                attachmentId, cancellationToken);
            foreach (var previous in previousPublished)
            {
                previous.Status = "OBSOLETE";
                previous.ExpiredDate = effectiveDate;
            }

            if (previousPublished.Count > 0)
            {
                await versionStore.SaveChangesAsync(cancellationToken);
            }

            var version = new AttachmentVersion
            {
                Id = Guid.NewGuid(),
                AttachmentId = attachmentId,
                Version = versionText,
                VersionMajor = major,
                VersionMinor = minor,
                Status = "PUBLISHED",
                PublishDate = publishDate,
                EffectiveDate = effectiveDate,
                FileKey = objectKey,
                OriginalFileName = request.File.FileName,
                ContentType = AttachmentFileRules.GetContentType(request.File.FileName),
                FileSize = writeResult.FileSize,
                Checksum = writeResult.Checksum,
                CreatedBy = userId,
                CreatedAt = timeProvider.GetUtcNow()
            };
            versionStore.Add(version);
            await versionStore.SaveChangesAsync(cancellationToken);
            await auditLogService.WriteAsync(
                new AuditLogWriteRequest(
                    context.CompanyId,
                    AuditActions.UploadAttachment,
                    AuditResourceTypes.AttachmentVersion,
                    version.Id,
                    new
                    {
                        attachment_id = attachmentId,
                        version = version.Version,
                        change_type = request.ChangeType,
                        publish_date = version.PublishDate,
                        effective_date = version.EffectiveDate,
                        previous_published_version_ids = previousPublished
                            .Select(previous => previous.Id)
                            .ToArray()
                    }),
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result<AttachmentVersionResponse>.Success(new(
                version.Id, version.Version, version.Status));
        }
        catch (Exception exception) when (IsDuplicateVersionConflict(exception))
        {
            if (writtenObjectKey is not null)
            {
                await TryMoveToTrashAsync(writtenObjectKey);
            }

            return Result<AttachmentVersionResponse>.Conflict(
                "此表單及附件已存在相同的版本號。");
        }
        catch (Exception exception) when (IsPublishedVersionConflict(exception))
        {
            if (writtenObjectKey is not null)
            {
                await TryMoveToTrashAsync(writtenObjectKey);
            }

            return Result<AttachmentVersionResponse>.Conflict(
                "另一個版本已同時發佈，請重新載入表單及附件後再試一次。");
        }
        catch
        {
            if (writtenObjectKey is not null)
            {
                await TryMoveToTrashAsync(writtenObjectKey);
            }

            throw;
        }
    }

    public async Task<Result<AttachmentVersionDetailResponse>> GetAsync(
        Guid attachmentId,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        var context = await versionStore.FindCreateContextAsync(attachmentId, cancellationToken);
        if (context is null)
        {
            return Result<AttachmentVersionDetailResponse>.NotFound("找不到指定的表單及附件。");
        }

        if (!currentUser.CanAccessCompany(context.CompanyId))
        {
            return Result<AttachmentVersionDetailResponse>.Forbidden(
                "您沒有檢視此表單及附件版本的權限。");
        }

        var version = await versionStore.FindVersionAsync(versionId, cancellationToken);
        if (version is null || version.AttachmentId != attachmentId)
        {
            return Result<AttachmentVersionDetailResponse>.NotFound("找不到指定的表單及附件版本。");
        }

        return Result<AttachmentVersionDetailResponse>.Success(new(
            version.Id,
            version.Version,
            version.Status,
            version.PublishDate,
            version.EffectiveDate,
            version.ExpiredDate,
            version.FileKey is not null));
    }

    private async Task TryMoveToTrashAsync(string objectKey)
    {
        try
        {
            await documentStorage.MoveToTrashAsync(objectKey, CancellationToken.None);
        }
        catch
        {
            // Preserve the database/storage exception that caused the rollback.
        }
    }

    private static (int Major, int Minor) CalculateNextVersion(
        AttachmentVersion? latest,
        string changeType)
    {
        if (latest is null)
        {
            return (1, 0);
        }

        return changeType == "MAJOR"
            ? (checked(latest.VersionMajor + 1), 0)
            : (latest.VersionMajor, checked(latest.VersionMinor + 1));
    }

    private static bool IsPublishedVersionConflict(Exception exception) => exception switch
    {
        DbUpdateException
        {
            InnerException: PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: "uq_attachment_single_published"
            }
        } => true,
        DbUpdateException
        {
            InnerException: PostgresException
            {
                SqlState: PostgresErrorCodes.SerializationFailure
            }
        } => true,
        PostgresException { SqlState: PostgresErrorCodes.SerializationFailure } => true,
        _ => false
    };

    private static bool IsDuplicateVersionConflict(Exception exception) => exception is
        DbUpdateException
        {
            InnerException: PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: "uq_attachment_versions"
            }
        };

    private static Dictionary<string, string[]> ToErrors(ValidationResult validation) =>
        validation.Errors
            .GroupBy(error => error.PropertyName, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.ErrorMessage).ToArray(),
                StringComparer.Ordinal);
}

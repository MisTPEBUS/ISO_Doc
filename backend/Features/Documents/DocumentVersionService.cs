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

public sealed class DocumentVersionService(
    IDocumentVersionStore versionStore,
    IDocumentStorage documentStorage,
    StorageKeyBuilder storageKeyBuilder,
    ICurrentUser currentUser,
    IValidator<CreateDocumentVersionRequest> validator,
    IValidator<UploadDocumentVersionFileRequest> uploadValidator,
    IOperationAuditLogService auditLogService,
    TimeProvider timeProvider) : IDocumentVersionService
{
    private static readonly byte[] PdfMagicBytes = "%PDF-"u8.ToArray();

    public async Task<Result<DocumentVersionResponse>> CreateAsync(
        Guid documentId,
        CreateDocumentVersionRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<DocumentVersionResponse>.ValidationFailed(ToErrors(validation));
        }

        if (!await HasPdfMagicBytesAsync(request.File!, cancellationToken))
        {
            return Result<DocumentVersionResponse>.ValidationFailed(new(StringComparer.Ordinal)
            {
                ["file"] = ["文件內容不是有效的 PDF 檔案。"]
            });
        }

        var document = await versionStore.FindDocumentAsync(documentId, cancellationToken);
        if (document is null)
        {
            return Result<DocumentVersionResponse>.NotFound("找不到指定的文件。");
        }

        if (!document.IsActive)
        {
            return Result<DocumentVersionResponse>.Conflict(
                "已停用的文件無法新增版本。");
        }

        if (!currentUser.CanAccessCompany(document.CompanyId))
        {
            return Result<DocumentVersionResponse>.Forbidden(
                "您沒有為此文件新增版本的權限。");
        }

        if (currentUser.UserId is not { } userId)
        {
            return Result<DocumentVersionResponse>.Unauthorized("請先登入後再操作。");
        }

        if (!DocumentVersionNumber.TryParse(
                request.Version, out var versionText, out var major, out var minor))
        {
            return Result<DocumentVersionResponse>.ValidationFailed(new(StringComparer.Ordinal)
            {
                ["version"] = ["版本格式必須為正整數或「主版號.次版號」，例如 1、1.0、2.1。"]
            });
        }

        if (await versionStore.VersionExistsAsync(
                documentId, versionText, cancellationToken))
        {
            return Result<DocumentVersionResponse>.Conflict("此文件已存在相同的版本號。");
        }

        var companyCode = await versionStore.FindCompanyCodeAsync(
            document.CompanyId, cancellationToken);
        if (companyCode is null)
        {
            return Result<DocumentVersionResponse>.NotFound("找不到文件所屬的公司。");
        }

        string? writtenObjectKey = null;
        try
        {
            await using var transaction = await versionStore.BeginTransactionAsync(cancellationToken);
            var publishDate = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
            var effectiveDate = request.EffectiveDate!.Value;
            var objectKey = storageKeyBuilder.BuildMainKey(
                companyCode,
                document.DocumentNo,
                versionText,
                Guid.NewGuid(),
                request.File!.FileName);

            await using var fileStream = request.File.OpenReadStream();
            var writeResult = await documentStorage.WriteAsync(
                objectKey, fileStream, cancellationToken);
            writtenObjectKey = objectKey;

            var previousPublished = await versionStore.ListPublishedVersionsAsync(
                documentId, cancellationToken);
            foreach (var previous in previousPublished)
            {
                previous.Status = "OBSOLETE";
                previous.ExpiredDate = effectiveDate;
            }

            if (previousPublished.Count > 0)
            {
                await versionStore.SaveChangesAsync(cancellationToken);
            }

            var version = new DocumentVersion
            {
                Id = Guid.NewGuid(),
                DocumentId = documentId,
                Version = versionText,
                VersionMajor = major,
                VersionMinor = minor,
                Status = "PUBLISHED",
                PublishDate = publishDate,
                EffectiveDate = effectiveDate,
                PageCount = request.PageCount,
                Memo = string.IsNullOrWhiteSpace(request.Memo) ? null : request.Memo.Trim(),
                FileKey = objectKey,
                OriginalFileName = request.File.FileName,
                ContentType = "application/pdf",
                FileSize = writeResult.FileSize,
                Checksum = writeResult.Checksum,
                CreatedBy = userId,
                CreatedAt = timeProvider.GetUtcNow()
            };
            versionStore.Add(version);
            await versionStore.SaveChangesAsync(cancellationToken);
            await auditLogService.WriteAsync(
                new AuditLogWriteRequest(
                    document.CompanyId,
                    AuditActions.PublishDocumentVersion,
                    AuditResourceTypes.DocumentVersion,
                    version.Id,
                    new
                    {
                        document_id = document.Id,
                        version = version.Version,
                        effective_date = version.EffectiveDate,
                        previous_published_version_ids = previousPublished
                            .Select(previous => previous.Id)
                            .ToArray()
                    }),
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result<DocumentVersionResponse>.Success(new(
                version.Id, version.Version, version.Status));
        }
        catch (Exception exception) when (IsDuplicateVersionConflict(exception))
        {
            if (writtenObjectKey is not null)
            {
                await TryMoveToTrashAsync(writtenObjectKey);
            }

            return Result<DocumentVersionResponse>.Conflict(
                "此文件已存在相同的版本號。");
        }
        catch (Exception exception) when (IsPublishedVersionConflict(exception))
        {
            if (writtenObjectKey is not null)
            {
                await TryMoveToTrashAsync(writtenObjectKey);
            }

            return Result<DocumentVersionResponse>.Conflict(
                "另一個版本已同時發佈，請重新載入文件後再試一次。");
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

    public async Task<Result<DocumentVersionResponse>> UploadDraftFileAsync(
        Guid documentId,
        Guid versionId,
        UploadDocumentVersionFileRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await uploadValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<DocumentVersionResponse>.ValidationFailed(ToErrors(validation));
        }

        if (!await HasPdfMagicBytesAsync(request.File!, cancellationToken))
        {
            return Result<DocumentVersionResponse>.ValidationFailed(new(StringComparer.Ordinal)
            {
                ["file"] = ["文件內容不是有效的 PDF 檔案。"]
            });
        }

        var version = await versionStore.FindVersionAsync(versionId, cancellationToken);
        if (version is null || version.DocumentId != documentId)
        {
            return Result<DocumentVersionResponse>.NotFound("找不到指定的文件版本。");
        }

        var document = await versionStore.FindDocumentAsync(documentId, cancellationToken);
        if (document is null)
        {
            return Result<DocumentVersionResponse>.NotFound("找不到指定的文件。");
        }

        if (!document.IsActive)
        {
            return Result<DocumentVersionResponse>.Conflict("已停用的文件無法補上ISO管理程序檔案。");
        }

        if (!currentUser.CanAccessCompany(document.CompanyId))
        {
            return Result<DocumentVersionResponse>.Forbidden(
                "您沒有為此文件補上ISO管理程序檔案的權限。");
        }

        if (currentUser.UserId is null)
        {
            return Result<DocumentVersionResponse>.Unauthorized("請先登入後再操作。");
        }

        if (version.Status != "DRAFT")
        {
            return Result<DocumentVersionResponse>.Conflict("只有草稿版本可以補上ISO管理程序檔案。");
        }

        if (version.FileKey is not null)
        {
            return Result<DocumentVersionResponse>.Conflict("此草稿版本已有ISO管理程序檔案。");
        }

        var effectiveDate = version.EffectiveDate ?? request.EffectiveDate;
        if (!effectiveDate.HasValue)
        {
            return Result<DocumentVersionResponse>.ValidationFailed(new(StringComparer.Ordinal)
            {
                ["effectiveDate"] = ["此版本尚未設定生效日期，請輸入生效日期。"]
            });
        }

        var previousPublished = await versionStore.ListPublishedVersionsAsync(
            documentId, cancellationToken);
        var publishDate = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        if (previousPublished.Count > 0 && effectiveDate.Value < publishDate)
        {
            return Result<DocumentVersionResponse>.ValidationFailed(new(StringComparer.Ordinal)
            {
                ["effectiveDate"] = ["已有發布版本時，生效日期不可早於補檔日期。"]
            });
        }

        var companyCode = await versionStore.FindCompanyCodeAsync(
            document.CompanyId, cancellationToken);
        if (companyCode is null)
        {
            return Result<DocumentVersionResponse>.NotFound("找不到文件所屬的公司。");
        }

        string? writtenObjectKey = null;
        try
        {
            await using var transaction = await versionStore.BeginTransactionAsync(cancellationToken);
            var objectKey = storageKeyBuilder.BuildMainKey(
                companyCode,
                document.DocumentNo,
                version.Version,
                Guid.NewGuid(),
                request.File!.FileName);

            await using var fileStream = request.File.OpenReadStream();
            var writeResult = await documentStorage.WriteAsync(
                objectKey, fileStream, cancellationToken);
            writtenObjectKey = objectKey;

            foreach (var previous in previousPublished)
            {
                previous.Status = "OBSOLETE";
                previous.ExpiredDate = effectiveDate.Value;
            }

            version.Status = "PUBLISHED";
            version.PublishDate = publishDate;
            version.EffectiveDate = effectiveDate.Value;
            version.FileKey = objectKey;
            version.OriginalFileName = request.File.FileName;
            version.ContentType = "application/pdf";
            version.FileSize = writeResult.FileSize;
            version.Checksum = writeResult.Checksum;

            await versionStore.SaveChangesAsync(cancellationToken);
            await auditLogService.WriteAsync(
                new AuditLogWriteRequest(
                    document.CompanyId,
                    AuditActions.PublishDocumentVersion,
                    AuditResourceTypes.DocumentVersion,
                    version.Id,
                    new
                    {
                        document_id = document.Id,
                        version = version.Version,
                        effective_date = version.EffectiveDate,
                        supplemented_draft = true,
                        previous_published_version_ids = previousPublished
                            .Select(previous => previous.Id)
                            .ToArray()
                    }),
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result<DocumentVersionResponse>.Success(new(
                version.Id, version.Version, version.Status));
        }
        catch (Exception exception) when (IsPublishedVersionConflict(exception))
        {
            if (writtenObjectKey is not null)
            {
                await TryMoveToTrashAsync(writtenObjectKey);
            }

            return Result<DocumentVersionResponse>.Conflict(
                "另一個版本已同時發佈，請重新載入文件後再試一次。");
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

    public async Task<Result> DeleteDraftAsync(
        Guid documentId,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        var version = await versionStore.FindVersionAsync(versionId, cancellationToken);
        if (version is null || version.DocumentId != documentId)
        {
            return Result.NotFound("找不到指定的文件版本。");
        }

        var document = await versionStore.FindDocumentAsync(documentId, cancellationToken);
        if (document is null)
        {
            return Result.NotFound("找不到指定的文件。");
        }

        if (!currentUser.CanAccessCompany(document.CompanyId))
        {
            return Result.Forbidden("您沒有刪除此文件版本的權限。");
        }

        if (currentUser.UserId is null)
        {
            return Result.Unauthorized("請先登入後再操作。");
        }

        // 僅限 DRAFT 狀態可硬刪除。
        if (version.Status != "DRAFT")
        {
            return Result.Conflict("只有草稿版本可以刪除。");
        }

        await using var transaction = await versionStore.BeginTransactionAsync(cancellationToken);
        if (version.FileKey is { } fileKey)
        {
            await documentStorage.MoveToTrashAsync(fileKey, cancellationToken);
        }

        versionStore.Remove(version);
        await versionStore.SaveChangesAsync(cancellationToken);
        await auditLogService.WriteAsync(
            new AuditLogWriteRequest(
                document.CompanyId,
                AuditActions.DeleteDocumentVersion,
                AuditResourceTypes.DocumentVersion,
                version.Id,
                new
                {
                    old_value = new
                    {
                        document_id = documentId,
                        version = version.Version,
                        status = version.Status
                    }
                }),
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
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

    private static async Task<bool> HasPdfMagicBytesAsync(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        var buffer = new byte[PdfMagicBytes.Length];
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var bytesRead = await stream.ReadAsync(
                buffer.AsMemory(totalRead), cancellationToken);
            if (bytesRead == 0)
            {
                return false;
            }

            totalRead += bytesRead;
        }

        return buffer.AsSpan().SequenceEqual(PdfMagicBytes);
    }

    private static bool IsPublishedVersionConflict(Exception exception) => exception switch
    {
        PublishedVersionConflictException => true,
        DbUpdateException
        {
            InnerException: PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: "uq_doc_single_published"
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
                ConstraintName: "uq_doc_versions"
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

public sealed class PublishedVersionConflictException : Exception;

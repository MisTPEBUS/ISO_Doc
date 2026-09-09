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
                ["file"] = ["The document file content is not a valid PDF."]
            });
        }

        var document = await versionStore.FindDocumentAsync(documentId, cancellationToken);
        if (document is null)
        {
            return Result<DocumentVersionResponse>.NotFound("The document was not found.");
        }

        if (!document.IsActive)
        {
            return Result<DocumentVersionResponse>.Conflict(
                "A new version cannot be added to an inactive document.");
        }

        if (!currentUser.CanAccessCompany(document.CompanyId))
        {
            return Result<DocumentVersionResponse>.Forbidden(
                "You do not have permission to add versions to this document.");
        }

        if (currentUser.UserId is not { } userId)
        {
            return Result<DocumentVersionResponse>.Unauthorized("Authentication is required.");
        }

        var companyCode = await versionStore.FindCompanyCodeAsync(
            document.CompanyId, cancellationToken);
        if (companyCode is null)
        {
            return Result<DocumentVersionResponse>.NotFound("The document company was not found.");
        }

        string? writtenObjectKey = null;
        try
        {
            await using var transaction = await versionStore.BeginTransactionAsync(cancellationToken);
            var latest = await versionStore.FindLatestVersionAsync(documentId, cancellationToken);
            var (major, minor) = CalculateNextVersion(latest, request.ChangeType!);
            var versionText = $"{major}.{minor}";
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
                        change_type = request.ChangeType,
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
        catch (Exception exception) when (IsPublishedVersionConflict(exception))
        {
            if (writtenObjectKey is not null)
            {
                await TryMoveToTrashAsync(writtenObjectKey);
            }

            return Result<DocumentVersionResponse>.Conflict(
                "Another version was published concurrently. Reload the document and try again.");
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
        DocumentVersion? latest,
        string changeType)
    {
        var currentMajor = latest?.VersionMajor ?? 0;
        var currentMinor = latest?.VersionMinor ?? 0;
        return changeType == "MAJOR"
            ? (currentMajor + 1, 0)
            : (currentMajor, currentMinor + 1);
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
                ConstraintName: "uq_doc_single_published" or "uq_doc_versions"
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

    private static Dictionary<string, string[]> ToErrors(ValidationResult validation) =>
        validation.Errors
            .GroupBy(error => error.PropertyName, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.ErrorMessage).ToArray(),
                StringComparer.Ordinal);
}

public sealed class PublishedVersionConflictException : Exception;

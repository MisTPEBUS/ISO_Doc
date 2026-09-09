using FluentValidation;
using FluentValidation.Results;
using IsoDocument.Api.Common;
using IsoDocument.Api.Data.Entities;
using IsoDocument.Api.Features.Documents.Dtos;
using IsoDocument.Api.Security;
using IsoDocument.Api.Storage;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace IsoDocument.Api.Features.Documents;

public sealed class AttachmentService(
    IAttachmentStore attachmentStore,
    IDocumentStorage documentStorage,
    StorageKeyBuilder storageKeyBuilder,
    ICurrentUser currentUser,
    IValidator<CreateAttachmentsRequest> validator,
    TimeProvider timeProvider) : IAttachmentService
{
    public async Task<Result<IReadOnlyList<AttachmentResponse>>> ListAsync(
        Guid documentId,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        var context = await attachmentStore.FindVersionContextAsync(
            documentId, versionId, cancellationToken);
        if (context is null)
        {
            return Result<IReadOnlyList<AttachmentResponse>>.NotFound(
                "The document version was not found.");
        }

        if (!currentUser.CanAccessCompany(context.Document.CompanyId))
        {
            return Result<IReadOnlyList<AttachmentResponse>>.Forbidden(
                "You do not have permission to access attachments for this document.");
        }

        var attachments = await attachmentStore.ListAsync(versionId, cancellationToken);
        return Result<IReadOnlyList<AttachmentResponse>>.Success(
            attachments.Select(ToResponse).ToArray());
    }

    public async Task<Result<CreateAttachmentsResponse>> CreateAsync(
        Guid documentId,
        Guid versionId,
        CreateAttachmentsRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<CreateAttachmentsResponse>.ValidationFailed(ToErrors(validation));
        }

        var context = await attachmentStore.FindVersionContextAsync(
            documentId, versionId, cancellationToken);
        if (context is null)
        {
            return Result<CreateAttachmentsResponse>.NotFound(
                "The document version was not found.");
        }

        if (!currentUser.CanAccessCompany(context.Document.CompanyId))
        {
            return Result<CreateAttachmentsResponse>.Forbidden(
                "You do not have permission to add attachments to this document.");
        }

        if (currentUser.UserId is not { } userId)
        {
            return Result<CreateAttachmentsResponse>.Unauthorized("Authentication is required.");
        }

        var normalizedNumbers = request.Items
            .Select(item => item.AttachmentNo!.Trim())
            .ToArray();
        var duplicateInRequest = normalizedNumbers
            .GroupBy(number => number, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateInRequest is not null)
        {
            return DuplicateAttachmentNumber<CreateAttachmentsResponse>(duplicateInRequest.Key);
        }

        var existingNumbers = await attachmentStore.FindExistingAttachmentNumbersAsync(
            versionId, normalizedNumbers, cancellationToken);
        if (existingNumbers.Count > 0)
        {
            return DuplicateAttachmentNumber<CreateAttachmentsResponse>(existingNumbers.First());
        }

        var writtenKeys = new List<string>();
        try
        {
            await using var transaction = await attachmentStore.BeginTransactionAsync(cancellationToken);
            var now = timeProvider.GetUtcNow();
            var attachments = new List<Attachment>(request.Items.Count);
            for (var index = 0; index < request.Items.Count; index++)
            {
                var item = request.Items[index];
                var attachment = new Attachment
                {
                    Id = Guid.NewGuid(),
                    DocumentVersionId = versionId,
                    AttachmentNo = normalizedNumbers[index],
                    Name = item.Name!.Trim(),
                    CreatedBy = userId,
                    CreatedAt = now
                };

                if (item.File is not null)
                {
                    var objectKey = storageKeyBuilder.BuildAttachmentKey(
                        context.CompanyCode,
                        context.Document.DocumentNo,
                        context.Version.Version,
                        index + 1,
                        Guid.NewGuid(),
                        item.File.FileName);
                    await using var stream = item.File.OpenReadStream();
                    var writeResult = await documentStorage.WriteAsync(
                        objectKey, stream, cancellationToken);
                    writtenKeys.Add(objectKey);
                    attachment.FileKey = objectKey;
                    attachment.OriginalFileName = item.File.FileName;
                    attachment.ContentType = AttachmentFileRules.GetContentType(item.File.FileName);
                    attachment.FileSize = writeResult.FileSize;
                    attachment.Checksum = writeResult.Checksum;
                }

                attachments.Add(attachment);
            }

            attachmentStore.AddRange(attachments);
            await attachmentStore.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result<CreateAttachmentsResponse>.Success(new(
                attachments.Select(attachment => new CreatedAttachmentResponse(
                    attachment.Id,
                    attachment.AttachmentNo,
                    attachment.FileKey is not null)).ToArray()));
        }
        catch (Exception exception) when (IsAttachmentNumberConflict(exception))
        {
            await MoveWrittenFilesToTrashAsync(writtenKeys);
            return DuplicateAttachmentNumber<CreateAttachmentsResponse>();
        }
        catch
        {
            await MoveWrittenFilesToTrashAsync(writtenKeys);
            throw;
        }
    }

    public async Task<Result> DeleteAsync(
        Guid attachmentId,
        CancellationToken cancellationToken)
    {
        var context = await attachmentStore.FindAttachmentContextAsync(
            attachmentId, cancellationToken);
        if (context is null)
        {
            return Result.NotFound("The attachment was not found.");
        }

        if (!currentUser.CanAccessCompany(context.CompanyId))
        {
            return Result.Forbidden("You do not have permission to delete this attachment.");
        }

        await using var transaction = await attachmentStore.BeginTransactionAsync(cancellationToken);
        if (context.Attachment.FileKey is { } objectKey)
        {
            await documentStorage.MoveToTrashAsync(objectKey, cancellationToken);
        }

        attachmentStore.Remove(context.Attachment);
        await attachmentStore.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }

    private async Task MoveWrittenFilesToTrashAsync(IEnumerable<string> objectKeys)
    {
        foreach (var objectKey in objectKeys.Reverse())
        {
            try
            {
                await documentStorage.MoveToTrashAsync(objectKey, CancellationToken.None);
            }
            catch
            {
                // Preserve the original batch failure; trash GC can handle a failed cleanup.
            }
        }
    }

    private static Result<T> DuplicateAttachmentNumber<T>(string? attachmentNo = null) =>
        Result<T>.ValidationFailed(new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["attachmentNo"] =
            [attachmentNo is null
                ? "An attachment number is already in use for this version."
                : $"Attachment number '{attachmentNo}' is already in use for this version."]
        });

    private static bool IsAttachmentNumberConflict(Exception exception) => exception switch
    {
        DbUpdateException
        {
            InnerException: PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: "uq_attachments_version_no"
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

    private static AttachmentResponse ToResponse(Attachment attachment) => new(
        attachment.Id,
        attachment.AttachmentNo,
        attachment.Name,
        attachment.FileKey is not null);

    private static Dictionary<string, string[]> ToErrors(ValidationResult validation) =>
        validation.Errors
            .GroupBy(error => ToCamelCasePath(error.PropertyName), StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.ErrorMessage).ToArray(),
                StringComparer.Ordinal);

    private static string ToCamelCasePath(string path) => path
        .Replace("Items", "items", StringComparison.Ordinal)
        .Replace("AttachmentNo", "attachmentNo", StringComparison.Ordinal)
        .Replace("Name", "name", StringComparison.Ordinal)
        .Replace("File", "file", StringComparison.Ordinal);
}

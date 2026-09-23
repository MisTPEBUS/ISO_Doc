using FluentValidation;
using FluentValidation.Results;
using IsoDocument.Api.Common;
using IsoDocument.Api.Data.Entities;
using IsoDocument.Api.Features.AuditLogs;
using IsoDocument.Api.Features.Documents.Dtos;
using IsoDocument.Api.Security;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace IsoDocument.Api.Features.Documents;

public sealed class AttachmentService(
    IAttachmentStore attachmentStore,
    ICurrentUser currentUser,
    IValidator<CreateAttachmentRequest> validator,
    IOperationAuditLogService auditLogService,
    TimeProvider timeProvider) : IAttachmentService
{
    private const int MaximumBulkImportSize = 200;

    public async Task<Result<IReadOnlyList<AttachmentResponse>>> ListAsync(
        Guid documentId,
        CancellationToken cancellationToken)
    {
        var document = await attachmentStore.FindDocumentAsync(documentId, cancellationToken);
        if (document is null)
        {
            return Result<IReadOnlyList<AttachmentResponse>>.NotFound("找不到指定的文件。");
        }

        if (!currentUser.CanAccessCompany(document.CompanyId))
        {
            return Result<IReadOnlyList<AttachmentResponse>>.Forbidden(
                "您沒有檢視此文件表單及附件的權限。");
        }

        var attachments = await attachmentStore.ListAsync(documentId, cancellationToken);
        return Result<IReadOnlyList<AttachmentResponse>>.Success(
            attachments.Select(ToResponse).ToArray());
    }

    public async Task<Result<AttachmentResponse>> CreateAsync(
        Guid documentId,
        CreateAttachmentRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<AttachmentResponse>.ValidationFailed(ToErrors(validation));
        }

        var document = await attachmentStore.FindDocumentAsync(documentId, cancellationToken);
        if (document is null)
        {
            return Result<AttachmentResponse>.NotFound("找不到指定的文件。");
        }

        if (!currentUser.CanAccessCompany(document.CompanyId))
        {
            return Result<AttachmentResponse>.Forbidden(
                "您沒有為此文件新增表單及附件的權限。");
        }

        if (currentUser.UserId is not { } userId)
        {
            return Result<AttachmentResponse>.Unauthorized("請先登入後再操作。");
        }

        var attachmentNo = NormalizeAttachmentNo(request.AttachmentNo);
        if (attachmentNo is not null
            && await attachmentStore.AttachmentNoExistsAsync(documentId, attachmentNo, cancellationToken))
        {
            return DuplicateAttachmentNo<AttachmentResponse>();
        }

        var now = timeProvider.GetUtcNow();
        var attachment = new Attachment
        {
            Id = Guid.NewGuid(),
            DocumentId = documentId,
            AttachmentNo = attachmentNo,
            Name = request.Name!.Trim(),
            IsActive = true,
            CreatedBy = userId,
            CreatedAt = now,
            UpdatedAt = now
        };
        attachmentStore.Add(attachment);
        try
        {
            await attachmentStore.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateAttachmentNoViolation(exception))
        {
            return DuplicateAttachmentNo<AttachmentResponse>();
        }

        await auditLogService.WriteAsync(
            new AuditLogWriteRequest(
                document.CompanyId,
                AuditActions.CreateAttachmentMetadata,
                AuditResourceTypes.Attachment,
                attachment.Id,
                new { new_value = ToAuditValue(attachment) }),
            cancellationToken);

        return Result<AttachmentResponse>.Success(ToResponse(attachment));
    }

    public async Task<Result<BulkImportAttachmentsResponse>> BulkImportAsync(
        BulkImportAttachmentsRequest request,
        CancellationToken cancellationToken)
    {
        var items = request.Items;
        if (items is null || items.Count == 0)
        {
            return Result<BulkImportAttachmentsResponse>.ValidationFailed(
                FieldError("items", "請至少提供一筆表單及附件資料。"));
        }

        if (items.Count > MaximumBulkImportSize)
        {
            return Result<BulkImportAttachmentsResponse>.ValidationFailed(
                FieldError("items", $"一次最多可匯入 {MaximumBulkImportSize} 筆表單及附件。"));
        }

        if (!currentUser.CanAccessCompany(request.CompanyId))
        {
            return Result<BulkImportAttachmentsResponse>.Forbidden(
                "您沒有為這間公司匯入表單及附件的權限。");
        }

        if (currentUser.UserId is not { } userId)
        {
            return Result<BulkImportAttachmentsResponse>.Unauthorized("請先登入後再操作。");
        }

        var now = timeProvider.GetUtcNow();
        var reservedAttachmentNos = new HashSet<(Guid DocumentId, string AttachmentNo)>();
        var succeeded = new List<BulkImportAttachmentSuccess>();
        var failed = new List<BulkImportAttachmentFailure>();

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var index = i + 1;
            if (string.IsNullOrWhiteSpace(item.DocumentNo))
            {
                failed.Add(new(index, item,
                    FieldError("documentNo", "請輸入文件編號。")));
                continue;
            }

            var createRequest = new CreateAttachmentRequest(item.AttachmentNo, item.Name);
            var validation = await validator.ValidateAsync(createRequest, cancellationToken);
            if (!validation.IsValid)
            {
                failed.Add(new(index, item, ToErrors(validation)));
                continue;
            }

            var documentNo = item.DocumentNo.Trim();
            var document = await attachmentStore.FindDocumentByNoAsync(
                request.CompanyId, documentNo, cancellationToken);
            if (document is null)
            {
                failed.Add(new(index, item,
                    FieldError("documentNo", "找不到對應的文件；請先完成文件批次匯入。")));
                continue;
            }

            var attachmentNo = NormalizeAttachmentNo(item.AttachmentNo);
            if (attachmentNo is not null
                && !reservedAttachmentNos.Add((document.Id, attachmentNo)))
            {
                failed.Add(new(index, item,
                    FieldError("attachmentNo", "此表單及附件編號與同文件的批次資料重複。")));
                continue;
            }

            if (attachmentNo is not null
                && await attachmentStore.AttachmentNoExistsAsync(
                    document.Id, attachmentNo, cancellationToken))
            {
                failed.Add(new(index, item, DuplicateAttachmentNoError()));
                continue;
            }

            var attachment = new Attachment
            {
                Id = Guid.NewGuid(),
                DocumentId = document.Id,
                AttachmentNo = attachmentNo,
                Name = item.Name!.Trim(),
                IsActive = true,
                CreatedBy = userId,
                CreatedAt = now,
                UpdatedAt = now
            };
            attachmentStore.Add(attachment);
            try
            {
                await attachmentStore.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (IsDuplicateAttachmentNoViolation(exception))
            {
                attachmentStore.Detach(attachment);
                failed.Add(new(index, item, DuplicateAttachmentNoError()));
                continue;
            }

            await auditLogService.WriteAsync(
                new AuditLogWriteRequest(
                    document.CompanyId,
                    AuditActions.CreateAttachmentMetadata,
                    AuditResourceTypes.Attachment,
                    attachment.Id,
                    new { new_value = ToAuditValue(attachment), bulk_import = true }),
                cancellationToken);
            succeeded.Add(new(index, new(
                attachment.Id,
                document.Id,
                document.DocumentNo,
                attachment.AttachmentNo,
                attachment.Name)));
        }

        return Result<BulkImportAttachmentsResponse>.Success(new(
            items.Count, succeeded.Count, failed.Count, succeeded, failed));
    }

    public async Task<Result<AttachmentDetailResponse>> GetAsync(
        Guid documentId,
        Guid attachmentId,
        CancellationToken cancellationToken)
    {
        var attachment = await attachmentStore.FindByIdAsync(attachmentId, cancellationToken);
        if (attachment is null || attachment.DocumentId != documentId)
        {
            return Result<AttachmentDetailResponse>.NotFound("找不到指定的表單及附件。");
        }

        var document = await attachmentStore.FindDocumentAsync(documentId, cancellationToken);
        if (document is null)
        {
            return Result<AttachmentDetailResponse>.NotFound("找不到指定的文件。");
        }

        if (!currentUser.CanAccessCompany(document.CompanyId))
        {
            return Result<AttachmentDetailResponse>.Forbidden("您沒有檢視此表單及附件的權限。");
        }

        var versions = await attachmentStore.ListVersionsAsync(attachmentId, cancellationToken);
        return Result<AttachmentDetailResponse>.Success(new(
            attachment.Id,
            attachment.AttachmentNo,
            attachment.Name,
            attachment.IsActive,
            versions.Select(version => new AttachmentVersionSummary(
                version.Id,
                version.Version,
                version.Status,
                version.PublishDate,
                version.EffectiveDate,
                version.ExpiredDate,
                version.FileKey is not null)).ToArray()));
    }

    public async Task<Result> DeleteAsync(
        Guid documentId,
        Guid attachmentId,
        CancellationToken cancellationToken)
    {
        var attachment = await attachmentStore.FindByIdAsync(attachmentId, cancellationToken);
        if (attachment is null || attachment.DocumentId != documentId)
        {
            return Result.NotFound("找不到指定的表單及附件。");
        }

        var document = await attachmentStore.FindDocumentAsync(documentId, cancellationToken);
        if (document is null)
        {
            return Result.NotFound("找不到指定的文件。");
        }

        if (!currentUser.CanAccessCompany(document.CompanyId))
        {
            return Result.Forbidden("您沒有刪除此表單及附件的權限。");
        }

        var wasActive = attachment.IsActive;
        attachment.IsActive = false;
        attachment.UpdatedAt = timeProvider.GetUtcNow();
        await attachmentStore.SaveChangesAsync(cancellationToken);
        await auditLogService.WriteAsync(
            new AuditLogWriteRequest(
                document.CompanyId,
                AuditActions.DeleteAttachment,
                AuditResourceTypes.Attachment,
                attachment.Id,
                new
                {
                    old_value = new { is_active = wasActive },
                    new_value = new { is_active = attachment.IsActive }
                }),
            cancellationToken);
        return Result.Success();
    }

    /// <summary>
    /// 表單及附件編號可留空（部分掃描進來的檔案本來就沒有編號規則）。留空一律正規化為
    /// null，而不是空字串——資料庫 uq_attachments_document_no 是 nullable 唯一鍵，同一份
    /// 文件底下可以有多筆 attachment_no = null，不會互相衝突；空字串則會。
    /// </summary>
    private static string? NormalizeAttachmentNo(string? attachmentNo)
    {
        var trimmed = attachmentNo?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static Result<T> DuplicateAttachmentNo<T>() => Result<T>.ValidationFailed(
        DuplicateAttachmentNoError());

    private static Dictionary<string, string[]> DuplicateAttachmentNoError() =>
        FieldError("attachmentNo", "這份文件已使用相同的表單及附件編號。");

    private static Dictionary<string, string[]> FieldError(string field, string message) =>
        new(StringComparer.Ordinal) { [field] = [message] };

    private static bool IsDuplicateAttachmentNoViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "uq_attachments_document_no"
        };

    private static AttachmentResponse ToResponse(Attachment attachment) => new(
        attachment.Id,
        attachment.AttachmentNo,
        attachment.Name,
        attachment.IsActive);

    private static object ToAuditValue(Attachment attachment) => new
    {
        document_id = attachment.DocumentId,
        attachment_no = attachment.AttachmentNo,
        name = attachment.Name
    };

    private static Dictionary<string, string[]> ToErrors(ValidationResult validation) =>
        validation.Errors
            .GroupBy(error => error.PropertyName, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.ErrorMessage).ToArray(),
                StringComparer.Ordinal);
}

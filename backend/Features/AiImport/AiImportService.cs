using FluentValidation;
using FluentValidation.Results;
using IsoDocument.Api.Common;
using IsoDocument.Api.Data.Entities;
using IsoDocument.Api.Features.AiImport.Dtos;
using IsoDocument.Api.Features.AiImport.Llm;
using IsoDocument.Api.Features.AuditLogs;
using IsoDocument.Api.Features.Documents;
using IsoDocument.Api.Security;
using IsoDocument.Api.Storage;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace IsoDocument.Api.Features.AiImport;

public sealed class AiImportService(
    IAiImportStore aiImportStore,
    IDocumentStore documentStore,
    IDocumentVersionStore documentVersionStore,
    IAttachmentStore attachmentStore,
    IAttachmentVersionStore attachmentVersionStore,
    IDocumentStorage documentStorage,
    StorageKeyBuilder storageKeyBuilder,
    ICurrentUser currentUser,
    IValidator<CommitImportDocumentItem> documentItemValidator,
    IValidator<CommitImportAttachmentItem> attachmentItemValidator,
    ILlmImportAnalyzer llmAnalyzer,
    IOperationAuditLogService auditLogService,
    TimeProvider timeProvider) : IAiImportService
{
    private static readonly byte[] PdfMagicBytes = "%PDF-"u8.ToArray();

    public async Task<Result<AnalyzeImportResponse>> AnalyzeAsync(
        AnalyzeImportRequest request, CancellationToken cancellationToken)
    {
        if (!currentUser.CanAccessCompany(request.CompanyId))
        {
            return Result<AnalyzeImportResponse>.Forbidden("您沒有為這間公司分析匯入資料的權限。");
        }

        var files = request.Files ?? [];
        var ambiguous = files
            .Where(file => file.ParseStatus != ImportParseStatuses.Ok
                || file.Role is ImportFileRoles.Unresolved or ImportFileRoles.MainCandidate
                || string.IsNullOrWhiteSpace(file.DocumentNo))
            .ToList();
        var suggestions = ambiguous.Count > 0
            ? await llmAnalyzer.RefineAsync(ambiguous, cancellationToken)
            : [];
        var suggestionsByPath = suggestions
            .GroupBy(suggestion => suggestion.RelativePath, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        var resolved = new List<ResolvedFile>();
        var unresolved = new List<UnresolvedImportFile>();
        foreach (var file in files)
        {
            var relativePath = file.RelativePath ?? file.OriginalFileName ?? string.Empty;
            var role = file.Role ?? ImportFileRoles.Unresolved;
            var documentNo = file.DocumentNo;
            var attachmentNo = file.AttachmentNo;
            var name = file.DisplayName;
            DateOnly? effectiveDate = null;
            var confidence = "HIGH";

            if (suggestionsByPath.TryGetValue(relativePath, out var suggestion))
            {
                role = suggestion.Role;
                documentNo = string.IsNullOrWhiteSpace(suggestion.DocumentNo) ? documentNo : suggestion.DocumentNo;
                attachmentNo = string.IsNullOrWhiteSpace(suggestion.AttachmentNo)
                    ? attachmentNo
                    : suggestion.AttachmentNo;
                name = string.IsNullOrWhiteSpace(suggestion.Name) ? name : suggestion.Name;
                effectiveDate = suggestion.EffectiveDate;
                confidence = suggestion.Confidence;
            }

            if (role == ImportFileRoles.Unresolved || string.IsNullOrWhiteSpace(documentNo))
            {
                unresolved.Add(new UnresolvedImportFile(relativePath, "無法判斷所屬主文編號，請人工指定。"));
                continue;
            }

            resolved.Add(new ResolvedFile(
                relativePath, role, documentNo.Trim(), attachmentNo?.Trim(),
                string.IsNullOrWhiteSpace(name) ? documentNo.Trim() : name.Trim(),
                effectiveDate, confidence, file.Extension));
        }

        var documents = new List<AnalyzedDocument>();
        foreach (var group in resolved.GroupBy(file => file.DocumentNo, StringComparer.Ordinal))
        {
            var mainFile = ResolveMainFile(group, unresolved);
            var attachmentFiles = group.Where(file => file.Role == ImportFileRoles.Attachment).ToList();

            var documentState = await aiImportStore.FindDocumentStateAsync(
                request.CompanyId, group.Key, cancellationToken);
            var existing = new ExistingDocumentState(
                documentState?.Document.Id,
                documentState?.LatestVersion?.Version,
                documentState?.LatestVersion?.Status,
                documentState?.LatestVersion?.FileKey is not null);

            var (documentAction, suggestedDocumentVersion) = DetermineDocumentAction(documentState, mainFile);

            var attachments = new List<AnalyzedAttachment>();
            foreach (var attachmentFile in attachmentFiles)
            {
                // 沒有編號代表無法判斷身分，一律視為新增，不跟既有附件比對。
                var attachmentNo = string.IsNullOrWhiteSpace(attachmentFile.AttachmentNo)
                    ? null
                    : attachmentFile.AttachmentNo;

                var attachmentState = attachmentNo is null || documentState is null
                    ? null
                    : await aiImportStore.FindAttachmentStateAsync(
                        documentState.Document.Id, attachmentNo, cancellationToken);
                var (attachmentAction, suggestedAttachmentVersion) = attachmentNo is null
                    ? (ImportActions.NewAttachment, "1.0")
                    : DetermineAttachmentAction(attachmentState);

                attachments.Add(new AnalyzedAttachment(
                    attachmentNo,
                    attachmentFile.Name,
                    attachmentFile.EffectiveDate,
                    suggestedAttachmentVersion,
                    attachmentAction,
                    attachmentFile.RelativePath,
                    new ExistingAttachmentState(
                        attachmentState?.Attachment.Id, attachmentState?.LatestVersion?.Version)));
            }

            documents.Add(new AnalyzedDocument(
                group.Key,
                mainFile?.Name ?? group.Key,
                mainFile?.EffectiveDate,
                suggestedDocumentVersion,
                documentAction,
                mainFile?.Confidence ?? "MEDIUM",
                existing,
                mainFile is null ? null : new AnalyzedMainFile(mainFile.RelativePath),
                attachments));
        }

        return Result<AnalyzeImportResponse>.Success(
            new AnalyzeImportResponse(Guid.NewGuid(), documents, unresolved));
    }

    public async Task<Result<CommitImportResponse>> CommitAsync(
        CommitImportRequest request, CancellationToken cancellationToken)
    {
        if (!currentUser.CanAccessCompany(request.CompanyId))
        {
            return Result<CommitImportResponse>.Forbidden("您沒有為這間公司匯入資料的權限。");
        }

        if (currentUser.UserId is not { } userId)
        {
            return Result<CommitImportResponse>.Unauthorized("請先登入後再操作。");
        }

        var documents = request.Documents;
        if (documents is null || documents.Count == 0)
        {
            return Result<CommitImportResponse>.ValidationFailed(
                FieldError("documents", "請至少提供一筆主文資料。"));
        }

        var now = timeProvider.GetUtcNow();
        var documentSucceeded = new List<CommitImportDocumentSuccess>();
        var documentFailed = new List<CommitImportDocumentFailure>();
        var attachmentSucceeded = new List<CommitImportAttachmentSuccess>();
        var attachmentSkipped = new List<CommitImportAttachmentSkipped>();
        var attachmentFailed = new List<CommitImportAttachmentFailure>();
        var totalAttachments = 0;

        for (var documentIndex = 0; documentIndex < documents.Count; documentIndex++)
        {
            var item = documents[documentIndex];
            var attachments = item.Attachments ?? [];
            totalAttachments += attachments.Count;

            var itemValidation = await documentItemValidator.ValidateAsync(item, cancellationToken);
            if (!itemValidation.IsValid)
            {
                documentFailed.Add(new(documentIndex, item.DocumentNo, ToErrors(itemValidation)));
                MarkAttachmentsAsParentFailed(attachments, documentIndex, attachmentSkipped);
                continue;
            }

            var (ok, success, errors) = await ProcessDocumentAsync(
                request.CompanyId, documentIndex, item, userId, now, cancellationToken);
            if (!ok || success is null)
            {
                documentFailed.Add(new(documentIndex, item.DocumentNo, errors!));
                MarkAttachmentsAsParentFailed(attachments, documentIndex, attachmentSkipped);
                continue;
            }

            documentSucceeded.Add(success);

            for (var attachmentIndex = 0; attachmentIndex < attachments.Count; attachmentIndex++)
            {
                var attachmentItem = attachments[attachmentIndex];
                var attachmentValidation = await attachmentItemValidator.ValidateAsync(
                    attachmentItem, cancellationToken);
                if (!attachmentValidation.IsValid)
                {
                    attachmentFailed.Add(new(
                        attachmentIndex, documentIndex, attachmentItem.AttachmentNo,
                        ToErrors(attachmentValidation)));
                    continue;
                }

                await ProcessAttachmentAsync(
                    success.DocumentId, attachmentIndex, documentIndex, attachmentItem, userId, now,
                    attachmentSucceeded, attachmentSkipped, attachmentFailed, cancellationToken);
            }
        }

        return Result<CommitImportResponse>.Success(new CommitImportResponse(
            new CommitImportDocumentsResult(
                documents.Count, documentSucceeded.Count, documentFailed.Count,
                documentSucceeded, documentFailed),
            new CommitImportAttachmentsResult(
                totalAttachments, attachmentSucceeded.Count, attachmentSkipped.Count, attachmentFailed.Count,
                attachmentSucceeded, attachmentSkipped, attachmentFailed)));
    }

    private async Task<(bool Ok, CommitImportDocumentSuccess? Success, Dictionary<string, string[]>? Errors)>
        ProcessDocumentAsync(
            Guid companyId, int documentIndex, CommitImportDocumentItem item, Guid userId, DateTimeOffset now,
            CancellationToken cancellationToken)
    {
        var documentNo = item.DocumentNo!.Trim();
        var state = await aiImportStore.FindDocumentStateAsync(companyId, documentNo, cancellationToken);

        if (state is null)
        {
            return await CreateNewDocumentAsync(companyId, documentIndex, documentNo, item, userId, now, cancellationToken);
        }

        var (document, latestVersion) = state;
        if (latestVersion is { Status: "DRAFT", FileKey: null })
        {
            if (item.MainFile is null)
            {
                return (true, new CommitImportDocumentSuccess(
                    documentIndex, documentNo, document.Id, ImportActions.SkipUnchanged,
                    latestVersion.Id, latestVersion.Version, latestVersion.EffectiveDate, latestVersion.Status), null);
            }

            return await UploadDraftFileAsync(document, latestVersion, documentIndex, item, cancellationToken);
        }

        if (item.MainFile is null)
        {
            return (true, new CommitImportDocumentSuccess(
                documentIndex, documentNo, document.Id, ImportActions.SkipUnchanged,
                latestVersion?.Id, latestVersion?.Version, latestVersion?.EffectiveDate, latestVersion?.Status), null);
        }

        return await CreateNewDocumentVersionAsync(document, documentIndex, item, userId, now, cancellationToken);
    }

    private async Task<(bool Ok, CommitImportDocumentSuccess? Success, Dictionary<string, string[]>? Errors)>
        CreateNewDocumentAsync(
            Guid companyId, int documentIndex, string documentNo, CommitImportDocumentItem item, Guid userId,
            DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (!DocumentVersionNumber.TryParse(item.Version, out var versionText, out var major, out var minor))
        {
            return (false, null, FieldError("version", "版本格式必須為正整數或「主版號.次版號」，例如 1、1.0、2.1。"));
        }

        if (item.MainFile is not null && !await HasPdfMagicBytesAsync(item.MainFile, cancellationToken))
        {
            return (false, null, FieldError("mainFile", "文件內容不是有效的 PDF 檔案。"));
        }

        string? companyCode = null;
        if (item.MainFile is not null)
        {
            companyCode = await documentVersionStore.FindCompanyCodeAsync(companyId, cancellationToken);
            if (companyCode is null)
            {
                return (false, null, FieldError("companyId", "找不到文件所屬的公司。"));
            }
        }

        var document = new Document
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            DocumentNo = documentNo,
            Name = item.Name!.Trim(),
            IsActive = true,
            CreatedBy = userId,
            CreatedAt = now,
            UpdatedAt = now
        };
        var isPublishing = item.MainFile is not null;
        var version = new DocumentVersion
        {
            Id = Guid.NewGuid(),
            DocumentId = document.Id,
            Version = versionText,
            VersionMajor = major,
            VersionMinor = minor,
            Status = isPublishing ? "PUBLISHED" : "DRAFT",
            PublishDate = isPublishing ? DateOnly.FromDateTime(now.UtcDateTime) : null,
            EffectiveDate = item.EffectiveDate,
            PageCount = item.PageCount,
            CreatedBy = userId,
            CreatedAt = now
        };

        string? writtenObjectKey = null;
        try
        {
            await using var transaction = await documentStore.BeginTransactionAsync(cancellationToken);
            if (item.MainFile is not null)
            {
                var objectKey = storageKeyBuilder.BuildMainKey(
                    companyCode!, documentNo, versionText, Guid.NewGuid(), item.MainFile.FileName);
                await using var fileStream = item.MainFile.OpenReadStream();
                var writeResult = await documentStorage.WriteAsync(objectKey, fileStream, cancellationToken);
                writtenObjectKey = objectKey;

                version.FileKey = objectKey;
                version.OriginalFileName = item.MainFile.FileName;
                version.ContentType = "application/pdf";
                version.FileSize = writeResult.FileSize;
                version.Checksum = writeResult.Checksum;
            }

            documentStore.Add(document);
            documentStore.Add(version);
            await documentStore.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateDocumentNoViolation(exception))
        {
            documentStore.Detach(document);
            documentStore.Detach(version);
            if (writtenObjectKey is not null)
            {
                await TryMoveToTrashAsync(writtenObjectKey);
            }

            return (false, null, FieldError("documentNo", "這間公司已使用相同的文件編號。"));
        }
        catch
        {
            if (writtenObjectKey is not null)
            {
                await TryMoveToTrashAsync(writtenObjectKey);
            }

            throw;
        }

        await auditLogService.WriteAsync(
            new AuditLogWriteRequest(
                companyId,
                AuditActions.CreateDocument,
                AuditResourceTypes.Document,
                document.Id,
                new
                {
                    document_no = document.DocumentNo,
                    name = document.Name,
                    version = version.Version,
                    status = version.Status,
                    ai_import = true
                }),
            cancellationToken);

        return (true, new CommitImportDocumentSuccess(
            documentIndex, documentNo, document.Id, ImportActions.NewDocument,
            version.Id, version.Version, version.EffectiveDate, version.Status), null);
    }

    private async Task<(bool Ok, CommitImportDocumentSuccess? Success, Dictionary<string, string[]>? Errors)>
        UploadDraftFileAsync(
            Document document, DocumentVersion draftVersion, int documentIndex, CommitImportDocumentItem item,
            CancellationToken cancellationToken)
    {
        if (!await HasPdfMagicBytesAsync(item.MainFile!, cancellationToken))
        {
            return (false, null, FieldError("mainFile", "文件內容不是有效的 PDF 檔案。"));
        }

        var effectiveDate = draftVersion.EffectiveDate ?? item.EffectiveDate;
        if (effectiveDate is null)
        {
            return (false, null, FieldError("effectiveDate", "請輸入生效日期。"));
        }

        var companyCode = await documentVersionStore.FindCompanyCodeAsync(document.CompanyId, cancellationToken);
        if (companyCode is null)
        {
            return (false, null, FieldError("companyId", "找不到文件所屬的公司。"));
        }

        string? writtenObjectKey = null;
        try
        {
            await using var transaction = await documentVersionStore.BeginTransactionAsync(cancellationToken);
            var objectKey = storageKeyBuilder.BuildMainKey(
                companyCode, document.DocumentNo, draftVersion.Version, Guid.NewGuid(), item.MainFile!.FileName);
            await using var fileStream = item.MainFile.OpenReadStream();
            var writeResult = await documentStorage.WriteAsync(objectKey, fileStream, cancellationToken);
            writtenObjectKey = objectKey;

            var previousPublished = await documentVersionStore.ListPublishedVersionsAsync(
                document.Id, cancellationToken);
            foreach (var previous in previousPublished)
            {
                previous.Status = "OBSOLETE";
                previous.ExpiredDate = effectiveDate.Value;
            }

            draftVersion.Status = "PUBLISHED";
            draftVersion.PublishDate = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
            draftVersion.EffectiveDate = effectiveDate.Value;
            draftVersion.FileKey = objectKey;
            draftVersion.OriginalFileName = item.MainFile.FileName;
            draftVersion.ContentType = "application/pdf";
            draftVersion.FileSize = writeResult.FileSize;
            draftVersion.Checksum = writeResult.Checksum;

            await documentVersionStore.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception exception) when (IsPublishedVersionConflict(exception))
        {
            if (writtenObjectKey is not null)
            {
                await TryMoveToTrashAsync(writtenObjectKey);
            }

            return (false, null, FieldError("version", "另一個版本已同時發佈，請重新整理後再試一次。"));
        }
        catch
        {
            if (writtenObjectKey is not null)
            {
                await TryMoveToTrashAsync(writtenObjectKey);
            }

            throw;
        }

        await auditLogService.WriteAsync(
            new AuditLogWriteRequest(
                document.CompanyId,
                AuditActions.PublishDocumentVersion,
                AuditResourceTypes.DocumentVersion,
                draftVersion.Id,
                new
                {
                    document_id = document.Id,
                    version = draftVersion.Version,
                    effective_date = draftVersion.EffectiveDate,
                    supplemented_draft = true,
                    ai_import = true
                }),
            cancellationToken);

        return (true, new CommitImportDocumentSuccess(
            documentIndex, document.DocumentNo, document.Id, ImportActions.UploadDraftFile,
            draftVersion.Id, draftVersion.Version, draftVersion.EffectiveDate, draftVersion.Status), null);
    }

    private async Task<(bool Ok, CommitImportDocumentSuccess? Success, Dictionary<string, string[]>? Errors)>
        CreateNewDocumentVersionAsync(
            Document document, int documentIndex, CommitImportDocumentItem item, Guid userId, DateTimeOffset now,
            CancellationToken cancellationToken)
    {
        if (!DocumentVersionNumber.TryParse(item.Version, out var versionText, out var major, out var minor))
        {
            return (false, null, FieldError("version", "版本格式必須為正整數或「主版號.次版號」，例如 1、1.0、2.1。"));
        }

        if (item.EffectiveDate is null)
        {
            return (false, null, FieldError("effectiveDate", "請輸入生效日期。"));
        }

        if (!await HasPdfMagicBytesAsync(item.MainFile!, cancellationToken))
        {
            return (false, null, FieldError("mainFile", "文件內容不是有效的 PDF 檔案。"));
        }

        if (await documentVersionStore.VersionExistsAsync(document.Id, versionText, cancellationToken))
        {
            return (false, null, FieldError("version", "此文件已存在相同的版本號。"));
        }

        var companyCode = await documentVersionStore.FindCompanyCodeAsync(document.CompanyId, cancellationToken);
        if (companyCode is null)
        {
            return (false, null, FieldError("companyId", "找不到文件所屬的公司。"));
        }

        string? writtenObjectKey = null;
        try
        {
            await using var transaction = await documentVersionStore.BeginTransactionAsync(cancellationToken);
            var objectKey = storageKeyBuilder.BuildMainKey(
                companyCode, document.DocumentNo, versionText, Guid.NewGuid(), item.MainFile!.FileName);
            await using var fileStream = item.MainFile.OpenReadStream();
            var writeResult = await documentStorage.WriteAsync(objectKey, fileStream, cancellationToken);
            writtenObjectKey = objectKey;

            var previousPublished = await documentVersionStore.ListPublishedVersionsAsync(
                document.Id, cancellationToken);
            foreach (var previous in previousPublished)
            {
                previous.Status = "OBSOLETE";
                previous.ExpiredDate = item.EffectiveDate.Value;
            }

            if (previousPublished.Count > 0)
            {
                await documentVersionStore.SaveChangesAsync(cancellationToken);
            }

            var version = new DocumentVersion
            {
                Id = Guid.NewGuid(),
                DocumentId = document.Id,
                Version = versionText,
                VersionMajor = major,
                VersionMinor = minor,
                Status = "PUBLISHED",
                PublishDate = DateOnly.FromDateTime(now.UtcDateTime),
                EffectiveDate = item.EffectiveDate.Value,
                PageCount = item.PageCount,
                FileKey = objectKey,
                OriginalFileName = item.MainFile.FileName,
                ContentType = "application/pdf",
                FileSize = writeResult.FileSize,
                Checksum = writeResult.Checksum,
                CreatedBy = userId,
                CreatedAt = now
            };
            documentVersionStore.Add(version);
            await documentVersionStore.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

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
                        ai_import = true
                    }),
                cancellationToken);

            return (true, new CommitImportDocumentSuccess(
                documentIndex, document.DocumentNo, document.Id, ImportActions.NewVersion,
                version.Id, version.Version, version.EffectiveDate, version.Status), null);
        }
        catch (Exception exception) when (IsDuplicateVersionConflict(exception))
        {
            if (writtenObjectKey is not null)
            {
                await TryMoveToTrashAsync(writtenObjectKey);
            }

            return (false, null, FieldError("version", "此文件已存在相同的版本號。"));
        }
        catch (Exception exception) when (IsPublishedVersionConflict(exception))
        {
            if (writtenObjectKey is not null)
            {
                await TryMoveToTrashAsync(writtenObjectKey);
            }

            return (false, null, FieldError("version", "另一個版本已同時發佈，請重新整理後再試一次。"));
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

    private async Task ProcessAttachmentAsync(
        Guid documentId, int attachmentIndex, int documentIndex, CommitImportAttachmentItem item, Guid userId,
        DateTimeOffset now,
        List<CommitImportAttachmentSuccess> succeeded,
        List<CommitImportAttachmentSkipped> skipped,
        List<CommitImportAttachmentFailure> failed,
        CancellationToken cancellationToken)
    {
        var attachmentNo = string.IsNullOrWhiteSpace(item.AttachmentNo) ? null : item.AttachmentNo.Trim();
        // 沒有編號代表無法判斷身分，一律視為新增，不跟既有附件比對。
        var state = attachmentNo is null
            ? null
            : await aiImportStore.FindAttachmentStateAsync(documentId, attachmentNo, cancellationToken);

        Attachment attachment;
        if (state is null)
        {
            attachment = new Attachment
            {
                Id = Guid.NewGuid(),
                DocumentId = documentId,
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
                failed.Add(new(attachmentIndex, documentIndex, attachmentNo,
                    FieldError("attachmentNo", "這份文件已使用相同的附件編號。")));
                return;
            }

            await auditLogService.WriteAsync(
                new AuditLogWriteRequest(
                    null,
                    AuditActions.CreateAttachmentMetadata,
                    AuditResourceTypes.Attachment,
                    attachment.Id,
                    new { attachment_no = attachment.AttachmentNo, name = attachment.Name, ai_import = true }),
                cancellationToken);
        }
        else
        {
            attachment = state.Attachment;
        }

        if (item.File is null)
        {
            if (state is null)
            {
                succeeded.Add(new(
                    attachmentIndex, documentIndex, attachmentNo, attachment.Id, ImportActions.NewAttachment,
                    null, null));
            }
            else
            {
                skipped.Add(new(attachmentIndex, documentIndex, attachmentNo, ImportSkipReasons.Unchanged));
            }

            return;
        }

        await using (var validationStream = item.File.OpenReadStream())
        {
            if (!await AttachmentFileRules.HasValidContentAsync(
                    item.File.FileName, validationStream, cancellationToken))
            {
                failed.Add(new(attachmentIndex, documentIndex, attachmentNo,
                    FieldError("file", "附件檔案內容與副檔名不符。")));
                return;
            }
        }

        if (!DocumentVersionNumber.TryParse(item.Version, out var versionText, out var major, out var minor))
        {
            failed.Add(new(attachmentIndex, documentIndex, attachmentNo,
                FieldError("version", "版本格式必須為正整數或「主版號.次版號」，例如 1、1.0、2.1。")));
            return;
        }

        var context = await attachmentVersionStore.FindCreateContextAsync(attachment.Id, cancellationToken);
        if (context is null)
        {
            failed.Add(new(attachmentIndex, documentIndex, attachmentNo,
                FieldError("attachmentNo", "找不到指定的附件。")));
            return;
        }

        string? writtenObjectKey = null;
        try
        {
            await using var transaction = await attachmentVersionStore.BeginTransactionAsync(cancellationToken);
            var objectKey = storageKeyBuilder.BuildAttachmentKey(
                context.CompanyCode, context.DocumentNo, attachmentNo, attachment.Id, versionText,
                Guid.NewGuid(), item.File.FileName);
            await using var fileStream = item.File.OpenReadStream();
            var writeResult = await documentStorage.WriteAsync(objectKey, fileStream, cancellationToken);
            writtenObjectKey = objectKey;

            var latestVersion = await attachmentVersionStore.FindLatestVersionAsync(
                attachment.Id, cancellationToken);
            if (latestVersion is not null && latestVersion.Checksum == writeResult.Checksum)
            {
                await TryMoveToTrashAsync(objectKey);
                skipped.Add(new(attachmentIndex, documentIndex, attachmentNo, ImportSkipReasons.Unchanged));
                return;
            }

            var previousPublished = await attachmentVersionStore.ListPublishedVersionsAsync(
                attachment.Id, cancellationToken);
            foreach (var previous in previousPublished)
            {
                previous.Status = "OBSOLETE";
                previous.ExpiredDate = item.EffectiveDate ?? DateOnly.FromDateTime(now.UtcDateTime);
            }

            if (previousPublished.Count > 0)
            {
                await attachmentVersionStore.SaveChangesAsync(cancellationToken);
            }

            var version = new AttachmentVersion
            {
                Id = Guid.NewGuid(),
                AttachmentId = attachment.Id,
                Version = versionText,
                VersionMajor = major,
                VersionMinor = minor,
                Status = "PUBLISHED",
                PublishDate = DateOnly.FromDateTime(now.UtcDateTime),
                EffectiveDate = item.EffectiveDate ?? DateOnly.FromDateTime(now.UtcDateTime),
                FileKey = objectKey,
                OriginalFileName = item.File.FileName,
                ContentType = AttachmentFileRules.GetContentType(item.File.FileName),
                FileSize = writeResult.FileSize,
                Checksum = writeResult.Checksum,
                CreatedBy = userId,
                CreatedAt = now
            };
            attachmentVersionStore.Add(version);
            await attachmentVersionStore.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            await auditLogService.WriteAsync(
                new AuditLogWriteRequest(
                    context.CompanyId,
                    AuditActions.UploadAttachment,
                    AuditResourceTypes.AttachmentVersion,
                    version.Id,
                    new
                    {
                        attachment_id = attachment.Id,
                        version = version.Version,
                        effective_date = version.EffectiveDate,
                        ai_import = true
                    }),
                cancellationToken);

            succeeded.Add(new(
                attachmentIndex, documentIndex, attachmentNo, attachment.Id,
                state is null ? ImportActions.NewAttachment : ImportActions.NewVersion,
                version.Id, version.Version));
        }
        catch (Exception exception) when (IsDuplicateAttachmentVersionConflict(exception))
        {
            if (writtenObjectKey is not null)
            {
                await TryMoveToTrashAsync(writtenObjectKey);
            }

            failed.Add(new(attachmentIndex, documentIndex, attachmentNo,
                FieldError("version", "此附件已存在相同的版本號。")));
        }
        catch (Exception exception) when (IsPublishedAttachmentVersionConflict(exception))
        {
            if (writtenObjectKey is not null)
            {
                await TryMoveToTrashAsync(writtenObjectKey);
            }

            failed.Add(new(attachmentIndex, documentIndex, attachmentNo,
                FieldError("version", "另一個版本已同時發佈，請重新整理後再試一次。")));
        }
    }

    private static void MarkAttachmentsAsParentFailed(
        IReadOnlyList<CommitImportAttachmentItem> attachments,
        int documentIndex,
        List<CommitImportAttachmentSkipped> sink)
    {
        for (var index = 0; index < attachments.Count; index++)
        {
            sink.Add(new(index, documentIndex, attachments[index].AttachmentNo,
                ImportSkipReasons.ParentDocumentFailed));
        }
    }

    /// <summary>
    /// 一個主文編號只能有一個主文檔案。同一群組出現多個 role=MAIN 時，優先用副檔名判斷：
    /// 剛好只有一個是 PDF 就留它當主文，其餘記入 unresolved 讓使用者調整；
    /// 無法唯一判斷（0 或多個 PDF）則整組都記入 unresolved，不選任何一個當主文。
    /// </summary>
    private static ResolvedFile? ResolveMainFile(
        IEnumerable<ResolvedFile> group, List<UnresolvedImportFile> unresolved)
    {
        var mainCandidates = group.Where(file => file.Role == ImportFileRoles.Main).ToList();
        if (mainCandidates.Count <= 1)
        {
            return mainCandidates.SingleOrDefault();
        }

        var pdfMains = mainCandidates
            .Where(file => string.Equals(file.Extension, "pdf", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (pdfMains.Count == 1)
        {
            var kept = pdfMains[0];
            foreach (var extra in mainCandidates.Where(file => file.RelativePath != kept.RelativePath))
            {
                unresolved.Add(new UnresolvedImportFile(
                    extra.RelativePath, "此主文編號已有主文檔案（PDF），這筆重複，請調整類型或編號。"));
            }

            return kept;
        }

        foreach (var extra in mainCandidates)
        {
            unresolved.Add(new UnresolvedImportFile(
                extra.RelativePath, "同一主文編號有多個主文檔案，且無法從副檔名唯一判斷，請人工指定。"));
        }

        return null;
    }

    private static (string Action, string? SuggestedVersion) DetermineDocumentAction(
        DocumentImportState? state, ResolvedFile? mainFile)
    {
        if (state is null)
        {
            return (ImportActions.NewDocument, "1.0");
        }

        if (state.LatestVersion is { Status: "DRAFT", FileKey: null } draft)
        {
            return mainFile is null
                ? (ImportActions.SkipUnchanged, draft.Version)
                : (ImportActions.UploadDraftFile, draft.Version);
        }

        if (state.LatestVersion is { } latest)
        {
            return mainFile is null
                ? (ImportActions.SkipUnchanged, latest.Version)
                : (ImportActions.NewVersion, $"{latest.VersionMajor}.{latest.VersionMinor + 1}");
        }

        return mainFile is null
            ? (ImportActions.SkipUnchanged, null)
            : (ImportActions.NewVersion, "1.0");
    }

    private static (string Action, string SuggestedVersion) DetermineAttachmentAction(
        AttachmentImportState? state)
    {
        if (state is null)
        {
            return (ImportActions.NewAttachment, "1.0");
        }

        if (state.LatestVersion is { } latest)
        {
            return (ImportActions.NewVersion, $"{latest.VersionMajor}.{latest.VersionMinor + 1}");
        }

        return (ImportActions.NewVersion, "1.0");
    }

    private async Task TryMoveToTrashAsync(string objectKey)
    {
        try
        {
            await documentStorage.MoveToTrashAsync(objectKey, CancellationToken.None);
        }
        catch
        {
            // Preserve the original exception that caused the rollback.
        }
    }

    private static async Task<bool> HasPdfMagicBytesAsync(
        Microsoft.AspNetCore.Http.IFormFile file, CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        var buffer = new byte[PdfMagicBytes.Length];
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var bytesRead = await stream.ReadAsync(buffer.AsMemory(totalRead), cancellationToken);
            if (bytesRead == 0)
            {
                return false;
            }

            totalRead += bytesRead;
        }

        return buffer.AsSpan().SequenceEqual(PdfMagicBytes);
    }

    private static bool IsDuplicateDocumentNoViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "uq_documents_company_no"
        };

    private static bool IsDuplicateAttachmentNoViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "uq_attachments_document_no"
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

    private static bool IsPublishedVersionConflict(Exception exception) => exception switch
    {
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
            InnerException: PostgresException { SqlState: PostgresErrorCodes.SerializationFailure }
        } => true,
        PostgresException { SqlState: PostgresErrorCodes.SerializationFailure } => true,
        _ => false
    };

    private static bool IsDuplicateAttachmentVersionConflict(Exception exception) => exception is
        DbUpdateException
        {
            InnerException: PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: "uq_attachment_versions"
            }
        };

    private static bool IsPublishedAttachmentVersionConflict(Exception exception) => exception switch
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
            InnerException: PostgresException { SqlState: PostgresErrorCodes.SerializationFailure }
        } => true,
        PostgresException { SqlState: PostgresErrorCodes.SerializationFailure } => true,
        _ => false
    };

    private static Dictionary<string, string[]> FieldError(string field, string message) =>
        new(StringComparer.Ordinal) { [field] = [message] };

    private static Dictionary<string, string[]> ToErrors(ValidationResult validation) =>
        validation.Errors
            .GroupBy(error => error.PropertyName, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.ErrorMessage).ToArray(),
                StringComparer.Ordinal);

    private sealed record ResolvedFile(
        string RelativePath,
        string Role,
        string DocumentNo,
        string? AttachmentNo,
        string Name,
        DateOnly? EffectiveDate,
        string Confidence,
        string? Extension);
}

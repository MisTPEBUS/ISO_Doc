using IsoDocument.Api.Data;
using IsoDocument.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IsoDocument.Api.Storage;

public sealed class StorageObjectKeyService(
    StorageKeyBuilder builder,
    IsoDbContext dbContext,
    IOptions<StorageOptions> storageOptions,
    IOptions<GcpStorageOptions> gcpOptions)
{
    public async Task<string> BuildMainKeyAsync(
        Document document,
        string companyCode,
        string version,
        Guid fileId,
        string originalFileName,
        CancellationToken cancellationToken)
    {
        if (storageOptions.Value.Provider == "Local")
        {
            return builder.BuildMainKey(
                companyCode, document.DocumentNo, version, fileId, originalFileName);
        }

        if (storageOptions.Value.Provider != "GoogleCloud")
        {
            throw new InvalidOperationException("Unsupported storage provider.");
        }

        var categoryName = await FindCategoryNameAsync(document.IsoCategoryId, cancellationToken);
        return builder.BuildGcpMainKey(
            gcpOptions.Value.ObjectPrefix,
            document.CompanyId,
            categoryName,
            document.DocumentNo,
            version,
            fileId,
            originalFileName);
    }

    public async Task<string> BuildAttachmentKeyAsync(
        Guid companyId,
        Guid? isoCategoryId,
        string companyCode,
        string documentNo,
        string? attachmentNo,
        Guid attachmentId,
        string version,
        Guid fileId,
        string originalFileName,
        CancellationToken cancellationToken)
    {
        if (storageOptions.Value.Provider == "Local")
        {
            return builder.BuildAttachmentKey(
                companyCode, documentNo, attachmentNo, attachmentId, version, fileId,
                originalFileName);
        }

        if (storageOptions.Value.Provider != "GoogleCloud")
        {
            throw new InvalidOperationException("Unsupported storage provider.");
        }

        var categoryName = await FindCategoryNameAsync(isoCategoryId, cancellationToken);
        return builder.BuildGcpAttachmentKey(
            gcpOptions.Value.ObjectPrefix,
            companyId,
            categoryName,
            documentNo,
            attachmentNo,
            attachmentId,
            version,
            fileId,
            originalFileName);
    }

    private async Task<string?> FindCategoryNameAsync(
        Guid? isoCategoryId,
        CancellationToken cancellationToken)
    {
        if (isoCategoryId is null) return null;
        return await dbContext.IsoCategories.AsNoTracking()
            .Where(category => category.Id == isoCategoryId)
            .Select(category => category.Name)
            .SingleAsync(cancellationToken);
    }
}

using IsoDocument.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace IsoDocument.Api.Features.Backup;

public sealed class EfBackupStore(IsoDbContext dbContext) : IBackupStore
{
    public Task<BackupCompany?> FindCompanyAsync(
        Guid companyId,
        CancellationToken cancellationToken) =>
        dbContext.Companies
            .AsNoTracking()
            .Where(company => company.Id == companyId)
            .Select(company => new BackupCompany(company.Id, company.Name))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<BackupSourceFile>> ListSourceFilesAsync(
        Guid companyId,
        CancellationToken cancellationToken)
    {
        var mainFiles = await (
            from document in dbContext.Documents.AsNoTracking()
            join version in dbContext.DocumentVersions.AsNoTracking()
                on document.Id equals version.DocumentId
            where document.CompanyId == companyId
            select new BackupSourceFile(
                document.DocumentNo,
                version.Version,
                version.Status,
                version.FileKey,
                version.OriginalFileName,
                null))
            .ToListAsync(cancellationToken);

        var attachments = await (
            from document in dbContext.Documents.AsNoTracking()
            join version in dbContext.DocumentVersions.AsNoTracking()
                on document.Id equals version.DocumentId
            join attachment in dbContext.Attachments.AsNoTracking()
                on version.Id equals attachment.DocumentVersionId
            where document.CompanyId == companyId
            select new BackupSourceFile(
                document.DocumentNo,
                version.Version,
                version.Status,
                attachment.FileKey,
                attachment.OriginalFileName,
                attachment.AttachmentNo))
            .ToListAsync(cancellationToken);

        return mainFiles.Concat(attachments).ToArray();
    }
}

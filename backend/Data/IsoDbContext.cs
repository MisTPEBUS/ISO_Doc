using IsoDocument.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace IsoDocument.Api.Data;

public sealed class IsoDbContext(DbContextOptions<IsoDbContext> options) : DbContext(options)
{
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Dept> Depts => Set<Dept>();
    public DbSet<IsoCategory> IsoCategories => Set<IsoCategory>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentVersion> DocumentVersions => Set<DocumentVersion>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<AttachmentVersion> AttachmentVersions => Set<AttachmentVersion>();
    public DbSet<DocumentDeptPermission> DocumentDeptPermissions => Set<DocumentDeptPermission>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Conventions.Remove(typeof(ForeignKeyIndexConvention));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("pgcrypto");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IsoDbContext).Assembly);
    }
}

using IsoDocument.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IsoDocument.Api.Data.Configurations;

public sealed class DocumentDeptPermissionConfiguration : IEntityTypeConfiguration<DocumentDeptPermission>
{
    public void Configure(EntityTypeBuilder<DocumentDeptPermission> builder)
    {
        builder.ToTable("document_dept_permissions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasColumnType("uuid").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.DocumentId).HasColumnName("document_id").HasColumnType("uuid");
        builder.Property(x => x.DeptId).HasColumnName("dept_id").HasColumnType("uuid");
        builder.Property(x => x.GrantedBy).HasColumnName("granted_by").HasColumnType("uuid");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("now()");
        builder.HasIndex(x => new { x.DocumentId, x.DeptId }).IsUnique().HasDatabaseName("uq_doc_dept");
        builder.HasOne<Document>().WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Dept>().WithMany().HasForeignKey(x => x.DeptId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.GrantedBy).OnDelete(DeleteBehavior.NoAction);
    }
}

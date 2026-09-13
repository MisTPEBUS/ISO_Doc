using IsoDocument.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IsoDocument.Api.Data.Configurations;

public sealed class DeptConfiguration : IEntityTypeConfiguration<Dept>
{
    public void Configure(EntityTypeBuilder<Dept> builder)
    {
        builder.ToTable("depts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasColumnType("uuid").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.CompanyId).HasColumnName("company_id").HasColumnType("uuid");
        builder.Property(x => x.Name).HasColumnName("name").HasColumnType("character varying(100)").HasMaxLength(100).IsRequired();
        builder.Property(x => x.Seq).HasColumnName("seq").HasColumnType("integer");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("now()");
        builder.HasIndex(x => new { x.CompanyId, x.Name }).IsUnique().HasDatabaseName("uq_depts_company_name");
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.NoAction);
    }
}

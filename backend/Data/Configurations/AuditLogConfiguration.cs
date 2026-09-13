using IsoDocument.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace IsoDocument.Api.Data.Configurations;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasColumnType("bigint")
            .HasAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.SerialColumn);
        builder.Property(x => x.CompanyId).HasColumnName("company_id").HasColumnType("uuid");
        builder.Property(x => x.UserId).HasColumnName("user_id").HasColumnType("uuid");
        builder.Property(x => x.Empno).HasColumnName("empno").HasColumnType("character varying(30)").HasMaxLength(30).IsRequired();
        builder.Property(x => x.Action).HasColumnName("action").HasColumnType("character varying(50)").HasMaxLength(50).IsRequired();
        builder.Property(x => x.ResourceType).HasColumnName("resource_type").HasColumnType("character varying(50)").HasMaxLength(50).IsRequired();
        builder.Property(x => x.ResourceId).HasColumnName("resource_id").HasColumnType("uuid");
        builder.Property(x => x.Detail).HasColumnName("detail").HasColumnType("jsonb");
        builder.Property(x => x.Ip).HasColumnName("ip").HasColumnType("inet");
        builder.Property(x => x.UserAgent).HasColumnName("user_agent").HasColumnType("character varying(500)").HasMaxLength(500);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("now()");
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
    }
}

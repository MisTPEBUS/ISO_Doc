using IsoDocument.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IsoDocument.Api.Data.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users", table => table.HasCheckConstraint("ck_users_role", "role IN ('USER','COMPANY_ADMIN','SYSTEM_ADMIN')"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasColumnType("uuid").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.CompanyId).HasColumnName("company_id").HasColumnType("uuid");
        builder.Property(x => x.DeptId).HasColumnName("dept_id").HasColumnType("uuid");
        builder.Property(x => x.Empno).HasColumnName("empno").HasColumnType("character varying(30)").HasMaxLength(30).IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasColumnType("character varying(100)").HasMaxLength(100).IsRequired();
        builder.Property(x => x.Email).HasColumnName("email").HasColumnType("character varying(255)").HasMaxLength(255);
        builder.Property(x => x.Role).HasColumnName("role").HasColumnType("character varying(20)").HasMaxLength(20).HasDefaultValue("USER").IsRequired();
        builder.Property(x => x.PasswordDigest).HasColumnName("password_digest").HasColumnType("character varying(255)").HasMaxLength(255);
        builder.Property(x => x.IsActive).HasColumnName("is_active").HasColumnType("boolean").HasDefaultValue(true);
        builder.Property(x => x.MustChangePassword).HasColumnName("must_change_password").HasColumnType("boolean").HasDefaultValue(false);
        builder.Property(x => x.NotifyEmailEnabled).HasColumnName("notify_email_enabled").HasColumnType("boolean").HasDefaultValue(true);
        builder.Property(x => x.LastLoginAt).HasColumnName("last_login_at").HasColumnType("timestamp with time zone");
        builder.Property(x => x.LegacyUserId).HasColumnName("legacy_user_id").HasColumnType("integer");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("now()");
        builder.HasIndex(x => x.Empno).IsUnique().HasDatabaseName("uq_users_empno");
        builder.HasIndex(x => x.LegacyUserId).IsUnique().HasDatabaseName("uq_users_legacy");
        builder.HasOne<Company>().WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Dept>().WithMany().HasForeignKey(x => x.DeptId).OnDelete(DeleteBehavior.NoAction);
    }
}

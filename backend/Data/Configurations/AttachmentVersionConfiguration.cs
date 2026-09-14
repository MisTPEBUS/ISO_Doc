using IsoDocument.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IsoDocument.Api.Data.Configurations;

public sealed class AttachmentVersionConfiguration
    : IEntityTypeConfiguration<AttachmentVersion>
{
    public void Configure(EntityTypeBuilder<AttachmentVersion> builder)
    {
        builder.ToTable("attachment_versions", table =>
        {
            table.HasCheckConstraint(
                "ck_attachment_version_status",
                "status IN ('DRAFT','PUBLISHED','OBSOLETE')"
            );

            table.HasCheckConstraint(
                "ck_attachment_published_requires_dates",
                "status <> 'PUBLISHED' OR (publish_date IS NOT NULL AND effective_date IS NOT NULL)"
            );

            table.HasCheckConstraint(
                "ck_attachment_version_file",
                """
                status = 'DRAFT'
                OR (
                    file_key IS NOT NULL
                    AND original_file_name IS NOT NULL
                    AND content_type IS NOT NULL
                    AND file_size IS NOT NULL
                    AND checksum IS NOT NULL
                )
                """
            );
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.AttachmentId)
            .HasColumnName("attachment_id")
            .HasColumnType("uuid");

        builder.Property(x => x.Version)
            .HasColumnName("version")
            .HasColumnType("character varying(20)")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.VersionMajor)
            .HasColumnName("version_major")
            .HasColumnType("integer");

        builder.Property(x => x.VersionMinor)
            .HasColumnName("version_minor")
            .HasColumnType("integer");

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasColumnType("character varying(20)")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.PublishDate)
            .HasColumnName("publish_date")
            .HasColumnType("date");

        builder.Property(x => x.EffectiveDate)
            .HasColumnName("effective_date")
            .HasColumnType("date");

        builder.Property(x => x.ExpiredDate)
            .HasColumnName("expired_date")
            .HasColumnType("date");

        builder.Property(x => x.FileKey)
            .HasColumnName("file_key")
            .HasColumnType("character varying(500)")
            .HasMaxLength(500);

        builder.Property(x => x.OriginalFileName)
            .HasColumnName("original_file_name")
            .HasColumnType("character varying(255)")
            .HasMaxLength(255);

        builder.Property(x => x.ContentType)
            .HasColumnName("content_type")
            .HasColumnType("character varying(100)")
            .HasMaxLength(100);

        builder.Property(x => x.FileSize)
            .HasColumnName("file_size")
            .HasColumnType("bigint");

        builder.Property(x => x.Checksum)
            .HasColumnName("checksum")
            .HasColumnType("character varying(128)")
            .HasMaxLength(128);

        builder.Property(x => x.CreatedBy)
            .HasColumnName("created_by")
            .HasColumnType("uuid");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("now()");

        builder.HasIndex(x => new { x.AttachmentId, x.Version })
        .IsUnique()
        .HasDatabaseName("uq_attachment_versions");

        builder.HasIndex(x => x.AttachmentId, "IX_attachment_versions_single_published")
            .IsUnique()
            .HasDatabaseName("uq_attachment_single_published")
            .HasFilter("status = 'PUBLISHED'");

        builder.HasIndex(x => x.AttachmentId, "IX_attachment_versions_single_draft")
            .IsUnique()
            .HasDatabaseName("uq_attachment_single_draft")
            .HasFilter("status = 'DRAFT'");

        builder.HasOne<Attachment>()
            .WithMany()
            .HasForeignKey(x => x.AttachmentId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.CreatedBy)
            .OnDelete(DeleteBehavior.NoAction);
    }
}

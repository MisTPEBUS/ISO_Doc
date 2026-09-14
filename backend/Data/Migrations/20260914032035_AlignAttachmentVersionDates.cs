using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IsoDocument.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AlignAttachmentVersionDates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_attachment_versions",
                table: "attachment_versions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_attachment_published_requires_effective_date",
                table: "attachment_versions");

            // Some deployed databases already received this SPEC column manually while the
            // EF model still omitted it. Reconcile both clean and drifted databases safely.
            migrationBuilder.Sql(
                "ALTER TABLE attachment_versions ADD COLUMN IF NOT EXISTS publish_date date;");

            migrationBuilder.CreateIndex(
                name: "uq_attachment_single_draft",
                table: "attachment_versions",
                column: "attachment_id",
                unique: true,
                filter: "status = 'DRAFT'");

            migrationBuilder.CreateIndex(
                name: "uq_attachment_versions",
                table: "attachment_versions",
                columns: new[] { "attachment_id", "version" },
                unique: true);

            // Versions created before publish_date was mapped still need a truthful historical
            // value before the authoritative SPEC constraint can be enabled.
            migrationBuilder.Sql(
                """
                UPDATE attachment_versions
                SET publish_date = (created_at AT TIME ZONE 'UTC')::date
                WHERE status <> 'DRAFT' AND publish_date IS NULL;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "ck_attachment_published_requires_dates",
                table: "attachment_versions",
                sql: "status <> 'PUBLISHED' OR (publish_date IS NOT NULL AND effective_date IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_attachment_single_draft",
                table: "attachment_versions");

            migrationBuilder.DropIndex(
                name: "uq_attachment_versions",
                table: "attachment_versions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_attachment_published_requires_dates",
                table: "attachment_versions");

            migrationBuilder.DropColumn(
                name: "publish_date",
                table: "attachment_versions");

            migrationBuilder.CreateIndex(
                name: "uq_attachment_versions",
                table: "attachment_versions",
                columns: new[] { "attachment_id", "version_major", "version_minor" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_attachment_published_requires_effective_date",
                table: "attachment_versions",
                sql: "status <> 'PUBLISHED' OR effective_date IS NOT NULL");
        }
    }
}

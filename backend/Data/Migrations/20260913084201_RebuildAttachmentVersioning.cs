using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IsoDocument.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RebuildAttachmentVersioning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 舊 attachment 資料不保留。
            // document_version_id 原本存的是 document_versions.id，
            // 不可直接視為 documents.id，因此異動前先清空。
            migrationBuilder.Sql("DELETE FROM attachments;");

            migrationBuilder.DropForeignKey(
                name: "FK_attachments_document_versions_document_version_id",
                table: "attachments");

            migrationBuilder.DropCheckConstraint(
                name: "ck_attachment_file",
                table: "attachments");

            migrationBuilder.DropColumn(
                name: "checksum",
                table: "attachments");

            migrationBuilder.DropColumn(
                name: "content_type",
                table: "attachments");

            migrationBuilder.DropColumn(
                name: "file_key",
                table: "attachments");

            migrationBuilder.DropColumn(
                name: "file_size",
                table: "attachments");

            migrationBuilder.DropColumn(
                name: "original_file_name",
                table: "attachments");

            // 舊：
            // attachments.document_version_id -> document_versions.id
            //
            // 新：
            // attachments.document_id -> documents.id
            //
            // 因為前面已清空資料，所以可以安全 Rename。
            migrationBuilder.RenameColumn(
                name: "document_version_id",
                table: "attachments",
                newName: "document_id");

            migrationBuilder.RenameIndex(
                name: "uq_attachments_version_no",
                table: "attachments",
                newName: "uq_attachments_document_no");

            migrationBuilder.AlterColumn<string>(
                name: "attachment_no",
                table: "attachments",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "attachments",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "updated_at",
                table: "attachments",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.CreateTable(
                name: "attachment_versions",
                columns: table => new
                {
                    id = table.Column<Guid>(
                        type: "uuid",
                        nullable: false,
                        defaultValueSql: "gen_random_uuid()"),

                    attachment_id = table.Column<Guid>(
                        type: "uuid",
                        nullable: false),

                    version = table.Column<string>(
                        type: "character varying(20)",
                        maxLength: 20,
                        nullable: false),

                    version_major = table.Column<int>(
                        type: "integer",
                        nullable: false),

                    version_minor = table.Column<int>(
                        type: "integer",
                        nullable: false),

                    status = table.Column<string>(
                        type: "character varying(20)",
                        maxLength: 20,
                        nullable: false),

                    effective_date = table.Column<DateOnly>(
                        type: "date",
                        nullable: true),

                    expired_date = table.Column<DateOnly>(
                        type: "date",
                        nullable: true),

                    file_key = table.Column<string>(
                        type: "character varying(500)",
                        maxLength: 500,
                        nullable: true),

                    original_file_name = table.Column<string>(
                        type: "character varying(255)",
                        maxLength: 255,
                        nullable: true),

                    content_type = table.Column<string>(
                        type: "character varying(100)",
                        maxLength: 100,
                        nullable: true),

                    file_size = table.Column<long>(
                        type: "bigint",
                        nullable: true),

                    checksum = table.Column<string>(
                        type: "character varying(128)",
                        maxLength: 128,
                        nullable: true),

                    created_by = table.Column<Guid>(
                        type: "uuid",
                        nullable: false),

                    created_at = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false,
                        defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey(
                        "PK_attachment_versions",
                        x => x.id);

                    table.CheckConstraint(
                        "ck_attachment_version_status",
                        "status IN ('DRAFT','PUBLISHED','OBSOLETE')");

                    table.CheckConstraint(
                        "ck_attachment_published_requires_effective_date",
                        "status <> 'PUBLISHED' OR effective_date IS NOT NULL");

                    table.CheckConstraint(
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
                        """);

                    table.ForeignKey(
                        name: "FK_attachment_versions_attachments_attachment_id",
                        column: x => x.attachment_id,
                        principalTable: "attachments",
                        principalColumn: "id");

                    table.ForeignKey(
                        name: "FK_attachment_versions_users_created_by",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id");
                });

            // 同一附件同時間最多只有一個 PUBLISHED
            migrationBuilder.CreateIndex(
                name: "uq_attachment_single_published",
                table: "attachment_versions",
                column: "attachment_id",
                unique: true,
                filter: "status = 'PUBLISHED'");

            // 同一附件不能有重複版本，例如兩筆 1.0
            migrationBuilder.CreateIndex(
                name: "uq_attachment_versions",
                table: "attachment_versions",
                columns: new[]
                {
                    "attachment_id",
                    "version_major",
                    "version_minor"
                },
                unique: true);

            // attachments 改為直接掛在 documents
            migrationBuilder.AddForeignKey(
                name: "FK_attachments_documents_document_id",
                table: "attachments",
                column: "document_id",
                principalTable: "documents",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_attachments_documents_document_id",
                table: "attachments");

            // 先刪 child table
            migrationBuilder.DropTable(
                name: "attachment_versions");

            // rollback 時 document_id 存的是 documents.id，
            // 不能直接變成 document_versions.id。
            // 因此 rollback 同樣不保留附件資料。
            migrationBuilder.Sql("DELETE FROM attachments;");

            migrationBuilder.DropColumn(
                name: "is_active",
                table: "attachments");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "attachments");

            migrationBuilder.RenameColumn(
                name: "document_id",
                table: "attachments",
                newName: "document_version_id");

            migrationBuilder.RenameIndex(
                name: "uq_attachments_document_no",
                table: "attachments",
                newName: "uq_attachments_version_no");

            migrationBuilder.AlterColumn<string>(
                name: "attachment_no",
                table: "attachments",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AddColumn<string>(
                name: "checksum",
                table: "attachments",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "content_type",
                table: "attachments",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "file_key",
                table: "attachments",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "file_size",
                table: "attachments",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "original_file_name",
                table: "attachments",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_attachment_file",
                table: "attachments",
                sql:
                    "(file_key IS NULL " +
                    "AND original_file_name IS NULL " +
                    "AND content_type IS NULL " +
                    "AND file_size IS NULL " +
                    "AND checksum IS NULL) " +
                    "OR " +
                    "(file_key IS NOT NULL " +
                    "AND original_file_name IS NOT NULL " +
                    "AND content_type IS NOT NULL " +
                    "AND file_size IS NOT NULL " +
                    "AND checksum IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_attachments_document_versions_document_version_id",
                table: "attachments",
                column: "document_version_id",
                principalTable: "document_versions",
                principalColumn: "id");
        }
    }
}
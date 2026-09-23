using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IsoDocument.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentDeptId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "dept_id",
                table: "documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_documents_depts_dept_id",
                table: "documents",
                column: "dept_id",
                principalTable: "depts",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_documents_depts_dept_id",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "dept_id",
                table: "documents");
        }
    }
}

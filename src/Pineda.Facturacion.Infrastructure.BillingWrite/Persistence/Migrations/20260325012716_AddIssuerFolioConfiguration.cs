using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIssuerFolioConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "fiscal_series",
                table: "issuer_profile",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "next_fiscal_folio",
                table: "issuer_profile",
                type: "int",
                nullable: true);

            migrationBuilder.Sql("UPDATE fiscal_document SET series = '' WHERE series IS NULL;");

            migrationBuilder.AlterColumn<string>(
                name: "series",
                table: "fiscal_document",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "varchar(20)",
                oldMaxLength: 20,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_fiscal_document_issuer_rfc_series_folio",
                table: "fiscal_document",
                columns: new[] { "issuer_rfc", "series", "folio" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_fiscal_document_issuer_rfc_series_folio",
                table: "fiscal_document");

            migrationBuilder.DropColumn(
                name: "fiscal_series",
                table: "issuer_profile");

            migrationBuilder.DropColumn(
                name: "next_fiscal_folio",
                table: "issuer_profile");

            migrationBuilder.AlterColumn<string>(
                name: "series",
                table: "fiscal_document",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(20)",
                oldMaxLength: 20,
                oldDefaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }
    }
}

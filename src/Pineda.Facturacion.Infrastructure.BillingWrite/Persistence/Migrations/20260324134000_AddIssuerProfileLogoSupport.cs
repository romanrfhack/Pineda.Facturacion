using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Migrations
{
    public partial class AddIssuerProfileLogoSupport : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "logo_content_type",
                table: "issuer_profile",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "logo_file_name",
                table: "issuer_profile",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "logo_storage_path",
                table: "issuer_profile",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "logo_updated_at_utc",
                table: "issuer_profile",
                type: "datetime(6)",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "logo_content_type",
                table: "issuer_profile");

            migrationBuilder.DropColumn(
                name: "logo_file_name",
                table: "issuer_profile");

            migrationBuilder.DropColumn(
                name: "logo_storage_path",
                table: "issuer_profile");

            migrationBuilder.DropColumn(
                name: "logo_updated_at_utc",
                table: "issuer_profile");
        }
    }
}

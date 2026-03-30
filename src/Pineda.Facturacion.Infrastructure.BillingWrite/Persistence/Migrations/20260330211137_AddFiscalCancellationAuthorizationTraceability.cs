using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFiscalCancellationAuthorizationTraceability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "authorization_error_code",
                table: "fiscal_cancellation",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "authorization_error_message",
                table: "fiscal_cancellation",
                type: "varchar(1000)",
                maxLength: 1000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "authorization_provider_code",
                table: "fiscal_cancellation",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "authorization_provider_message",
                table: "fiscal_cancellation",
                type: "varchar(1000)",
                maxLength: 1000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "authorization_provider_operation",
                table: "fiscal_cancellation",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "authorization_provider_tracking_id",
                table: "fiscal_cancellation",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "authorization_raw_response_summary_json",
                table: "fiscal_cancellation",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "authorization_responded_at_utc",
                table: "fiscal_cancellation",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "authorization_responded_by_display_name",
                table: "fiscal_cancellation",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "authorization_responded_by_username",
                table: "fiscal_cancellation",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "authorization_status",
                table: "fiscal_cancellation",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "authorization_error_code",
                table: "fiscal_cancellation");

            migrationBuilder.DropColumn(
                name: "authorization_error_message",
                table: "fiscal_cancellation");

            migrationBuilder.DropColumn(
                name: "authorization_provider_code",
                table: "fiscal_cancellation");

            migrationBuilder.DropColumn(
                name: "authorization_provider_message",
                table: "fiscal_cancellation");

            migrationBuilder.DropColumn(
                name: "authorization_provider_operation",
                table: "fiscal_cancellation");

            migrationBuilder.DropColumn(
                name: "authorization_provider_tracking_id",
                table: "fiscal_cancellation");

            migrationBuilder.DropColumn(
                name: "authorization_raw_response_summary_json",
                table: "fiscal_cancellation");

            migrationBuilder.DropColumn(
                name: "authorization_responded_at_utc",
                table: "fiscal_cancellation");

            migrationBuilder.DropColumn(
                name: "authorization_responded_by_display_name",
                table: "fiscal_cancellation");

            migrationBuilder.DropColumn(
                name: "authorization_responded_by_username",
                table: "fiscal_cancellation");

            migrationBuilder.DropColumn(
                name: "authorization_status",
                table: "fiscal_cancellation");
        }
    }
}

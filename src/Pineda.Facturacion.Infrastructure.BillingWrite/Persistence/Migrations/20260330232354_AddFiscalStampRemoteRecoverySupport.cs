using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFiscalStampRemoteRecoverySupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "last_remote_provider_code",
                table: "fiscal_stamp",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "last_remote_provider_message",
                table: "fiscal_stamp",
                type: "varchar(1000)",
                maxLength: 1000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "last_remote_provider_tracking_id",
                table: "fiscal_stamp",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "last_remote_query_at_utc",
                table: "fiscal_stamp",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "last_remote_raw_response_summary_json",
                table: "fiscal_stamp",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "xml_recovered_from_provider_at_utc",
                table: "fiscal_stamp",
                type: "datetime(6)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "last_remote_provider_code",
                table: "fiscal_stamp");

            migrationBuilder.DropColumn(
                name: "last_remote_provider_message",
                table: "fiscal_stamp");

            migrationBuilder.DropColumn(
                name: "last_remote_provider_tracking_id",
                table: "fiscal_stamp");

            migrationBuilder.DropColumn(
                name: "last_remote_query_at_utc",
                table: "fiscal_stamp");

            migrationBuilder.DropColumn(
                name: "last_remote_raw_response_summary_json",
                table: "fiscal_stamp");

            migrationBuilder.DropColumn(
                name: "xml_recovered_from_provider_at_utc",
                table: "fiscal_stamp");
        }
    }
}

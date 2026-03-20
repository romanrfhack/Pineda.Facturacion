using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFiscalImportApplyAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "applied_at_utc",
                table: "product_fiscal_profile_import_row",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "applied_master_entity_id",
                table: "product_fiscal_profile_import_row",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "apply_error_message",
                table: "product_fiscal_profile_import_row",
                type: "varchar(1000)",
                maxLength: 1000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "apply_status",
                table: "product_fiscal_profile_import_row",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "applied_rows",
                table: "product_fiscal_profile_import_batch",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "apply_failed_rows",
                table: "product_fiscal_profile_import_batch",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "apply_skipped_rows",
                table: "product_fiscal_profile_import_batch",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_applied_at_utc",
                table: "product_fiscal_profile_import_batch",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "applied_at_utc",
                table: "fiscal_receiver_import_row",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "applied_master_entity_id",
                table: "fiscal_receiver_import_row",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "apply_error_message",
                table: "fiscal_receiver_import_row",
                type: "varchar(1000)",
                maxLength: 1000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "apply_status",
                table: "fiscal_receiver_import_row",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "applied_rows",
                table: "fiscal_receiver_import_batch",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "apply_failed_rows",
                table: "fiscal_receiver_import_batch",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "apply_skipped_rows",
                table: "fiscal_receiver_import_batch",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_applied_at_utc",
                table: "fiscal_receiver_import_batch",
                type: "datetime(6)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "applied_at_utc",
                table: "product_fiscal_profile_import_row");

            migrationBuilder.DropColumn(
                name: "applied_master_entity_id",
                table: "product_fiscal_profile_import_row");

            migrationBuilder.DropColumn(
                name: "apply_error_message",
                table: "product_fiscal_profile_import_row");

            migrationBuilder.DropColumn(
                name: "apply_status",
                table: "product_fiscal_profile_import_row");

            migrationBuilder.DropColumn(
                name: "applied_rows",
                table: "product_fiscal_profile_import_batch");

            migrationBuilder.DropColumn(
                name: "apply_failed_rows",
                table: "product_fiscal_profile_import_batch");

            migrationBuilder.DropColumn(
                name: "apply_skipped_rows",
                table: "product_fiscal_profile_import_batch");

            migrationBuilder.DropColumn(
                name: "last_applied_at_utc",
                table: "product_fiscal_profile_import_batch");

            migrationBuilder.DropColumn(
                name: "applied_at_utc",
                table: "fiscal_receiver_import_row");

            migrationBuilder.DropColumn(
                name: "applied_master_entity_id",
                table: "fiscal_receiver_import_row");

            migrationBuilder.DropColumn(
                name: "apply_error_message",
                table: "fiscal_receiver_import_row");

            migrationBuilder.DropColumn(
                name: "apply_status",
                table: "fiscal_receiver_import_row");

            migrationBuilder.DropColumn(
                name: "applied_rows",
                table: "fiscal_receiver_import_batch");

            migrationBuilder.DropColumn(
                name: "apply_failed_rows",
                table: "fiscal_receiver_import_batch");

            migrationBuilder.DropColumn(
                name: "apply_skipped_rows",
                table: "fiscal_receiver_import_batch");

            migrationBuilder.DropColumn(
                name: "last_applied_at_utc",
                table: "fiscal_receiver_import_batch");
        }
    }
}

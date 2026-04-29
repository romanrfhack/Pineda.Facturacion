using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductFiscalReviewCleanupOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "product_fiscal_review_cleanup_batch",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    cleanup_batch_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    operation_name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    is_dry_run = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    status = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    environment_name = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    database_name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    requested_by = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    notes = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    evaluated_count = table.Column<int>(type: "int", nullable: false),
                    eligible_count = table.Column<int>(type: "int", nullable: false),
                    updated_count = table.Column<int>(type: "int", nullable: false),
                    skipped_count = table.Column<int>(type: "int", nullable: false),
                    excluded_manual_source_count = table.Column<int>(type: "int", nullable: false),
                    excluded_import_source_count = table.Column<int>(type: "int", nullable: false),
                    excluded_by_open_manual_source_count = table.Column<int>(type: "int", nullable: false),
                    excluded_by_open_import_source_count = table.Column<int>(type: "int", nullable: false),
                    excluded_by_historical_manual_source_count = table.Column<int>(type: "int", nullable: false),
                    excluded_by_historical_import_source_count = table.Column<int>(type: "int", nullable: false),
                    excluded_manual_audit_count = table.Column<int>(type: "int", nullable: false),
                    already_pending_count = table.Column<int>(type: "int", nullable: false),
                    duplicate_open_assignment_count = table.Column<int>(type: "int", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    committed_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    rolled_back_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_fiscal_review_cleanup_batch", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "product_fiscal_review_cleanup_entry",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    cleanup_batch_record_id = table.Column<long>(type: "bigint", nullable: false),
                    internal_code = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    product_fiscal_profile_id = table.Column<long>(type: "bigint", nullable: true),
                    product_fiscal_assignment_id = table.Column<long>(type: "bigint", nullable: true),
                    outcome = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    skip_reason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    previous_source = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    previous_review_status = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    previous_review_reason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    previous_confidence = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: true),
                    previous_valid_from_utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    previous_valid_to_utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    previous_updated_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    new_source = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    new_review_status = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    new_review_reason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    new_confidence = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: true),
                    new_valid_from_utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    new_valid_to_utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    new_updated_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    product_fiscal_profile_snapshot_json = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    product_fiscal_assignment_before_json = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    product_fiscal_assignment_after_json = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    related_audit_events_snapshot_json = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    billing_document_item_hints_snapshot_json = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_fiscal_review_cleanup_entry", x => x.id);
                    table.ForeignKey(
                        name: "FK_product_fiscal_review_cleanup_entry_product_fiscal_review_cl~",
                        column: x => x.cleanup_batch_record_id,
                        principalTable: "product_fiscal_review_cleanup_batch",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_product_fiscal_review_cleanup_batch_cleanup_batch_id",
                table: "product_fiscal_review_cleanup_batch",
                column: "cleanup_batch_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_product_fiscal_review_cleanup_batch_operation_name_status",
                table: "product_fiscal_review_cleanup_batch",
                columns: new[] { "operation_name", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_product_fiscal_review_cleanup_entry_cleanup_batch_record_id",
                table: "product_fiscal_review_cleanup_entry",
                column: "cleanup_batch_record_id");

            migrationBuilder.CreateIndex(
                name: "IX_product_fiscal_review_cleanup_entry_cleanup_batch_record_id_~",
                table: "product_fiscal_review_cleanup_entry",
                columns: new[] { "cleanup_batch_record_id", "internal_code" });

            migrationBuilder.CreateIndex(
                name: "IX_product_fiscal_review_cleanup_entry_product_fiscal_assignmen~",
                table: "product_fiscal_review_cleanup_entry",
                column: "product_fiscal_assignment_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "product_fiscal_review_cleanup_entry");

            migrationBuilder.DropTable(
                name: "product_fiscal_review_cleanup_batch");
        }
    }
}

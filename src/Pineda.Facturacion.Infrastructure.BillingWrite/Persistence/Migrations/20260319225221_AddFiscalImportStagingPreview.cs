using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFiscalImportStagingPreview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_fiscal_receiver_legal_name",
                table: "fiscal_receiver");

            migrationBuilder.DropIndex(
                name: "IX_fiscal_receiver_search_alias",
                table: "fiscal_receiver");

            migrationBuilder.AddColumn<string>(
                name: "normalized_description",
                table: "product_fiscal_profile",
                type: "varchar(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "normalized_legal_name",
                table: "fiscal_receiver",
                type: "varchar(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "normalized_search_alias",
                table: "fiscal_receiver",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "fiscal_receiver_import_batch",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    source_file_name = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<int>(type: "int", nullable: false),
                    total_rows = table.Column<int>(type: "int", nullable: false),
                    valid_rows = table.Column<int>(type: "int", nullable: false),
                    invalid_rows = table.Column<int>(type: "int", nullable: false),
                    ignored_rows = table.Column<int>(type: "int", nullable: false),
                    existing_master_matches = table.Column<int>(type: "int", nullable: false),
                    duplicate_rows_in_file = table.Column<int>(type: "int", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    completed_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fiscal_receiver_import_batch", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "product_fiscal_profile_import_batch",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    source_file_name = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<int>(type: "int", nullable: false),
                    total_rows = table.Column<int>(type: "int", nullable: false),
                    valid_rows = table.Column<int>(type: "int", nullable: false),
                    invalid_rows = table.Column<int>(type: "int", nullable: false),
                    ignored_rows = table.Column<int>(type: "int", nullable: false),
                    existing_master_matches = table.Column<int>(type: "int", nullable: false),
                    duplicate_rows_in_file = table.Column<int>(type: "int", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    completed_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    default_tax_object_code = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    default_vat_rate = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                    default_unit_text = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_fiscal_profile_import_batch", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "fiscal_receiver_import_row",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    batch_id = table.Column<long>(type: "bigint", nullable: false),
                    row_number = table.Column<int>(type: "int", nullable: false),
                    raw_json = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    source_external_id = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    normalized_rfc = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    normalized_legal_name = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    normalized_cfdi_use_code_default = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    normalized_fiscal_regime_code = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    normalized_postal_code = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    normalized_country_code = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    normalized_foreign_tax_registration = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    normalized_email = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    normalized_phone = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<int>(type: "int", nullable: false),
                    suggested_action = table.Column<int>(type: "int", nullable: false),
                    validation_errors = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    existing_fiscal_receiver_id = table.Column<long>(type: "bigint", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fiscal_receiver_import_row", x => x.id);
                    table.ForeignKey(
                        name: "FK_fiscal_receiver_import_row_fiscal_receiver_import_batch_batc~",
                        column: x => x.batch_id,
                        principalTable: "fiscal_receiver_import_batch",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "product_fiscal_profile_import_row",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    batch_id = table.Column<long>(type: "bigint", nullable: false),
                    row_number = table.Column<int>(type: "int", nullable: false),
                    raw_json = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    source_external_id = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    normalized_internal_code = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    normalized_description = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    normalized_sat_product_service_code = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    normalized_sat_unit_code = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    normalized_tax_object_code = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    normalized_vat_rate = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                    normalized_default_unit_text = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<int>(type: "int", nullable: false),
                    suggested_action = table.Column<int>(type: "int", nullable: false),
                    validation_errors = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    existing_product_fiscal_profile_id = table.Column<long>(type: "bigint", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_fiscal_profile_import_row", x => x.id);
                    table.ForeignKey(
                        name: "FK_product_fiscal_profile_import_row_product_fiscal_profile_imp~",
                        column: x => x.batch_id,
                        principalTable: "product_fiscal_profile_import_batch",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_product_fiscal_profile_normalized_description",
                table: "product_fiscal_profile",
                column: "normalized_description");

            migrationBuilder.CreateIndex(
                name: "IX_fiscal_receiver_normalized_legal_name",
                table: "fiscal_receiver",
                column: "normalized_legal_name");

            migrationBuilder.CreateIndex(
                name: "IX_fiscal_receiver_normalized_search_alias",
                table: "fiscal_receiver",
                column: "normalized_search_alias");

            migrationBuilder.CreateIndex(
                name: "IX_fiscal_receiver_import_row_batch_id",
                table: "fiscal_receiver_import_row",
                column: "batch_id");

            migrationBuilder.CreateIndex(
                name: "IX_fiscal_receiver_import_row_batch_id_row_number",
                table: "fiscal_receiver_import_row",
                columns: new[] { "batch_id", "row_number" });

            migrationBuilder.CreateIndex(
                name: "IX_fiscal_receiver_import_row_normalized_rfc",
                table: "fiscal_receiver_import_row",
                column: "normalized_rfc");

            migrationBuilder.CreateIndex(
                name: "IX_product_fiscal_profile_import_row_batch_id",
                table: "product_fiscal_profile_import_row",
                column: "batch_id");

            migrationBuilder.CreateIndex(
                name: "IX_product_fiscal_profile_import_row_batch_id_row_number",
                table: "product_fiscal_profile_import_row",
                columns: new[] { "batch_id", "row_number" });

            migrationBuilder.CreateIndex(
                name: "IX_product_fiscal_profile_import_row_normalized_internal_code",
                table: "product_fiscal_profile_import_row",
                column: "normalized_internal_code");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fiscal_receiver_import_row");

            migrationBuilder.DropTable(
                name: "product_fiscal_profile_import_row");

            migrationBuilder.DropTable(
                name: "fiscal_receiver_import_batch");

            migrationBuilder.DropTable(
                name: "product_fiscal_profile_import_batch");

            migrationBuilder.DropIndex(
                name: "IX_product_fiscal_profile_normalized_description",
                table: "product_fiscal_profile");

            migrationBuilder.DropIndex(
                name: "IX_fiscal_receiver_normalized_legal_name",
                table: "fiscal_receiver");

            migrationBuilder.DropIndex(
                name: "IX_fiscal_receiver_normalized_search_alias",
                table: "fiscal_receiver");

            migrationBuilder.DropColumn(
                name: "normalized_description",
                table: "product_fiscal_profile");

            migrationBuilder.DropColumn(
                name: "normalized_legal_name",
                table: "fiscal_receiver");

            migrationBuilder.DropColumn(
                name: "normalized_search_alias",
                table: "fiscal_receiver");

            migrationBuilder.CreateIndex(
                name: "IX_fiscal_receiver_legal_name",
                table: "fiscal_receiver",
                column: "legal_name");

            migrationBuilder.CreateIndex(
                name: "IX_fiscal_receiver_search_alias",
                table: "fiscal_receiver",
                column: "search_alias");
        }
    }
}

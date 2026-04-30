using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Pineda.Facturacion.Infrastructure.BillingWrite.Persistence;

#nullable disable

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(BillingDbContext))]
    [Migration("20260429153000_AddLegacyFiscalProductMappings")]
    public partial class AddLegacyFiscalProductMappings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "fiscal_product_mapping_import_batch",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    file_name = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    source_name = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    source_checksum = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    imported_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    imported_by_user_id = table.Column<long>(type: "bigint", nullable: true),
                    imported_by_username = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    total_rows = table.Column<int>(type: "int", nullable: false),
                    valid_rows = table.Column<int>(type: "int", nullable: false),
                    invalid_rows = table.Column<int>(type: "int", nullable: false),
                    ambiguous_rows = table.Column<int>(type: "int", nullable: false),
                    skipped_rows = table.Column<int>(type: "int", nullable: false),
                    status = table.Column<int>(type: "int", nullable: false),
                    error_message = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fiscal_product_mapping_import_batch", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "legacy_fiscal_product_mapping",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    source_name = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    source_concept_id = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    description_raw = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    description_normalized = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    internal_catalog_raw = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    internal_catalog_normalized = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    sat_product_service_code = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    sat_unit_code = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ean_code = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ean_code_normalized = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    sku_code = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    sku_code_normalized = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    is_active = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    is_ambiguous_by_description = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    is_ambiguous_by_internal_code = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    import_batch_id = table.Column<long>(type: "bigint", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_legacy_fiscal_product_mapping", x => x.id);
                    table.ForeignKey(
                        name: "FK_legacy_fiscal_product_mapping_batch",
                        column: x => x.import_batch_id,
                        principalTable: "fiscal_product_mapping_import_batch",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_fiscal_product_mapping_import_batch_source_checksum",
                table: "fiscal_product_mapping_import_batch",
                column: "source_checksum",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_legacy_mapping_description_normalized",
                table: "legacy_fiscal_product_mapping",
                column: "description_normalized");

            migrationBuilder.CreateIndex(
                name: "IX_legacy_mapping_ean_code_normalized",
                table: "legacy_fiscal_product_mapping",
                column: "ean_code_normalized");

            migrationBuilder.CreateIndex(
                name: "IX_legacy_mapping_import_source_concept",
                table: "legacy_fiscal_product_mapping",
                columns: new[] { "import_batch_id", "source_concept_id" });

            migrationBuilder.CreateIndex(
                name: "IX_legacy_mapping_internal_catalog_normalized",
                table: "legacy_fiscal_product_mapping",
                column: "internal_catalog_normalized");

            migrationBuilder.CreateIndex(
                name: "IX_legacy_mapping_sku_code_normalized",
                table: "legacy_fiscal_product_mapping",
                column: "sku_code_normalized");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "legacy_fiscal_product_mapping");

            migrationBuilder.DropTable(
                name: "fiscal_product_mapping_import_batch");
        }
    }
}

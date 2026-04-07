using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFiscalAssignmentCatalogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "product_fiscal_assignment",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    internal_code = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    sat_product_service_code = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    sat_unit_code = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    tax_object_code = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    vat_rate = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: false),
                    default_unit_text = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    source = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    confidence = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: false),
                    review_status = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    review_reason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    valid_from_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    valid_to_utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_fiscal_assignment", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "sat_catalog_imports",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    catalog_type = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    source_file_name = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    source_format = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    source_version = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    source_checksum = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    total_rows = table.Column<int>(type: "int", nullable: false),
                    inserted_rows = table.Column<int>(type: "int", nullable: false),
                    updated_rows = table.Column<int>(type: "int", nullable: false),
                    deactivated_rows = table.Column<int>(type: "int", nullable: false),
                    error_message = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    completed_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sat_catalog_imports", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "sat_clave_unidad",
                columns: table => new
                {
                    code = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    description = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    normalized_description = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    symbol = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    notes = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    is_active = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    source_version = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sat_clave_unidad", x => x.code);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_product_fiscal_assignment_internal_code_review_status",
                table: "product_fiscal_assignment",
                columns: new[] { "internal_code", "review_status" });

            migrationBuilder.CreateIndex(
                name: "IX_product_fiscal_assignment_internal_code_valid_from_utc",
                table: "product_fiscal_assignment",
                columns: new[] { "internal_code", "valid_from_utc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_product_fiscal_assignment_internal_code_valid_to_utc_valid_f~",
                table: "product_fiscal_assignment",
                columns: new[] { "internal_code", "valid_to_utc", "valid_from_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_sat_catalog_imports_catalog_type_source_version",
                table: "sat_catalog_imports",
                columns: new[] { "catalog_type", "source_version" });

            migrationBuilder.CreateIndex(
                name: "IX_sat_catalog_imports_catalog_type_status",
                table: "sat_catalog_imports",
                columns: new[] { "catalog_type", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_sat_clave_unidad_normalized_description",
                table: "sat_clave_unidad",
                column: "normalized_description");

            migrationBuilder.Sql(
                """
                INSERT INTO product_fiscal_assignment
                (
                    internal_code,
                    sat_product_service_code,
                    sat_unit_code,
                    tax_object_code,
                    vat_rate,
                    default_unit_text,
                    source,
                    confidence,
                    review_status,
                    review_reason,
                    valid_from_utc,
                    valid_to_utc,
                    created_at_utc,
                    updated_at_utc
                )
                SELECT
                    p.internal_code,
                    p.sat_product_service_code,
                    p.sat_unit_code,
                    p.tax_object_code,
                    p.vat_rate,
                    p.default_unit_text,
                    'product_fiscal_profile_backfill',
                    1.0000,
                    'approved',
                    NULL,
                    COALESCE(p.updated_at_utc, p.created_at_utc, UTC_TIMESTAMP()),
                    NULL,
                    UTC_TIMESTAMP(),
                    UTC_TIMESTAMP()
                FROM product_fiscal_profile p
                WHERE NOT EXISTS
                (
                    SELECT 1
                    FROM product_fiscal_assignment a
                    WHERE a.internal_code = p.internal_code
                        AND a.valid_to_utc IS NULL
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "product_fiscal_assignment");

            migrationBuilder.DropTable(
                name: "sat_catalog_imports");

            migrationBuilder.DropTable(
                name: "sat_clave_unidad");
        }
    }
}

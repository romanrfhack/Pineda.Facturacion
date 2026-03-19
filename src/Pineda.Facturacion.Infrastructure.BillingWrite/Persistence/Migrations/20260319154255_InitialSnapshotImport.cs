using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSnapshotImport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "legacy_import_record",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    source_system = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    source_table = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    source_document_id = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    source_document_type = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    source_hash = table.Column<string>(type: "char(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    import_status = table.Column<int>(type: "int", nullable: false),
                    imported_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    last_seen_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    billing_document_id = table.Column<long>(type: "bigint", nullable: true),
                    error_message = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_legacy_import_record", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "sales_order",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    legacy_import_record_id = table.Column<long>(type: "bigint", nullable: false),
                    legacy_order_number = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    legacy_order_type = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    customer_legacy_id = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    customer_name = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    customer_rfc = table.Column<string>(type: "varchar(13)", maxLength: 13, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    payment_condition = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    price_list_code = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    delivery_type = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    currency_code = table.Column<string>(type: "char(3)", maxLength: 3, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    subtotal = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    discount_total = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    tax_total = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    total = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    snapshot_taken_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    status = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_order", x => x.id);
                    table.ForeignKey(
                        name: "FK_sales_order_legacy_import_record_legacy_import_record_id",
                        column: x => x.legacy_import_record_id,
                        principalTable: "legacy_import_record",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "sales_order_item",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    sales_order_id = table.Column<long>(type: "bigint", nullable: false),
                    line_number = table.Column<int>(type: "int", nullable: false),
                    legacy_article_id = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    sku = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    description = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    unit_code = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    unit_name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    quantity = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    unit_price = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    discount_amount = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    tax_rate = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: false),
                    tax_amount = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    line_total = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    sat_product_service_code = table.Column<string>(type: "char(8)", maxLength: 8, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    sat_unit_code = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sales_order_item", x => x.id);
                    table.ForeignKey(
                        name: "FK_sales_order_item_sales_order_sales_order_id",
                        column: x => x.sales_order_id,
                        principalTable: "sales_order",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_legacy_import_record_source_system_source_table_source_docum~",
                table: "legacy_import_record",
                columns: new[] { "source_system", "source_table", "source_document_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sales_order_legacy_import_record_id",
                table: "sales_order",
                column: "legacy_import_record_id");

            migrationBuilder.CreateIndex(
                name: "IX_sales_order_item_sales_order_id",
                table: "sales_order_item",
                column: "sales_order_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sales_order_item");

            migrationBuilder.DropTable(
                name: "sales_order");

            migrationBuilder.DropTable(
                name: "legacy_import_record");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBillingDocumentItemRemovalTraceability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_billing_document_item_billing_document_id",
                table: "billing_document_item");

            migrationBuilder.AddColumn<long>(
                name: "sales_order_id",
                table: "billing_document_item",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "sales_order_item_id",
                table: "billing_document_item",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "source_legacy_order_id",
                table: "billing_document_item",
                type: "varchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "source_sales_order_line_number",
                table: "billing_document_item",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "billing_document_item_removal",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    billing_document_id = table.Column<long>(type: "bigint", nullable: false),
                    fiscal_document_id = table.Column<long>(type: "bigint", nullable: true),
                    sales_order_id = table.Column<long>(type: "bigint", nullable: false),
                    sales_order_item_id = table.Column<long>(type: "bigint", nullable: false),
                    billing_document_item_id = table.Column<long>(type: "bigint", nullable: false),
                    source_legacy_order_id = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    source_sales_order_line_number = table.Column<int>(type: "int", nullable: false),
                    product_internal_code = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    description = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    quantity_removed = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    removal_reason = table.Column<int>(type: "int", nullable: false),
                    observations = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    removal_disposition = table.Column<int>(type: "int", nullable: false),
                    removed_by_username = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    removed_by_display_name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    removed_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    billing_document_status_at_removal = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    fiscal_document_status_at_removal = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    removed_from_current_document = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_billing_document_item_removal", x => x.id);
                    table.ForeignKey(
                        name: "FK_billing_document_item_removal_billing_document_billing_docum~",
                        column: x => x.billing_document_id,
                        principalTable: "billing_document",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_billing_document_item_removal_fiscal_document_fiscal_documen~",
                        column: x => x.fiscal_document_id,
                        principalTable: "fiscal_document",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_billing_document_item_removal_sales_order_item_sales_order_i~",
                        column: x => x.sales_order_item_id,
                        principalTable: "sales_order_item",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_billing_document_item_removal_sales_order_sales_order_id",
                        column: x => x.sales_order_id,
                        principalTable: "sales_order",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_billing_document_item_billing_document_id_sales_order_item_id",
                table: "billing_document_item",
                columns: new[] { "billing_document_id", "sales_order_item_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_billing_document_item_sales_order_id",
                table: "billing_document_item",
                column: "sales_order_id");

            migrationBuilder.CreateIndex(
                name: "IX_billing_document_item_sales_order_item_id",
                table: "billing_document_item",
                column: "sales_order_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_billing_document_item_removal_billing_document_id_sales_orde~",
                table: "billing_document_item_removal",
                columns: new[] { "billing_document_id", "sales_order_item_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_billing_document_item_removal_billing_document_item_id",
                table: "billing_document_item_removal",
                column: "billing_document_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_billing_document_item_removal_fiscal_document_id",
                table: "billing_document_item_removal",
                column: "fiscal_document_id");

            migrationBuilder.CreateIndex(
                name: "IX_billing_document_item_removal_sales_order_id",
                table: "billing_document_item_removal",
                column: "sales_order_id");

            migrationBuilder.CreateIndex(
                name: "IX_billing_document_item_removal_sales_order_item_id",
                table: "billing_document_item_removal",
                column: "sales_order_item_id");

            migrationBuilder.AddForeignKey(
                name: "FK_billing_document_item_sales_order_item_sales_order_item_id",
                table: "billing_document_item",
                column: "sales_order_item_id",
                principalTable: "sales_order_item",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_billing_document_item_sales_order_sales_order_id",
                table: "billing_document_item",
                column: "sales_order_id",
                principalTable: "sales_order",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_billing_document_item_sales_order_item_sales_order_item_id",
                table: "billing_document_item");

            migrationBuilder.DropForeignKey(
                name: "FK_billing_document_item_sales_order_sales_order_id",
                table: "billing_document_item");

            migrationBuilder.DropTable(
                name: "billing_document_item_removal");

            migrationBuilder.DropIndex(
                name: "IX_billing_document_item_billing_document_id_sales_order_item_id",
                table: "billing_document_item");

            migrationBuilder.DropIndex(
                name: "IX_billing_document_item_sales_order_id",
                table: "billing_document_item");

            migrationBuilder.DropIndex(
                name: "IX_billing_document_item_sales_order_item_id",
                table: "billing_document_item");

            migrationBuilder.DropColumn(
                name: "sales_order_id",
                table: "billing_document_item");

            migrationBuilder.DropColumn(
                name: "sales_order_item_id",
                table: "billing_document_item");

            migrationBuilder.DropColumn(
                name: "source_legacy_order_id",
                table: "billing_document_item");

            migrationBuilder.DropColumn(
                name: "source_sales_order_line_number",
                table: "billing_document_item");

            migrationBuilder.CreateIndex(
                name: "IX_billing_document_item_billing_document_id",
                table: "billing_document_item",
                column: "billing_document_id");
        }
    }
}

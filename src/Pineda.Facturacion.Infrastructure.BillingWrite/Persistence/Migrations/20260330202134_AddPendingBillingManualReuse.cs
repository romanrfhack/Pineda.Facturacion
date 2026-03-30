using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPendingBillingManualReuse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "available_for_pending_billing_reuse",
                table: "billing_document_item_removal",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("""
                UPDATE billing_document_item_removal
                SET available_for_pending_billing_reuse = 1
                WHERE removal_disposition = 0;
                """);

            migrationBuilder.AddColumn<decimal>(
                name: "discount_amount",
                table: "billing_document_item_removal",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "line_total",
                table: "billing_document_item_removal",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "sat_product_service_code",
                table: "billing_document_item_removal",
                type: "char(8)",
                maxLength: 8,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "sat_unit_code",
                table: "billing_document_item_removal",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "tax_amount",
                table: "billing_document_item_removal",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "tax_rate",
                table: "billing_document_item_removal",
                type: "decimal(9,6)",
                precision: 9,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "unit_price",
                table: "billing_document_item_removal",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<long>(
                name: "source_billing_document_item_removal_id",
                table: "billing_document_item",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "billing_document_pending_item_assignment",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    billing_document_item_removal_id = table.Column<long>(type: "bigint", nullable: false),
                    destination_billing_document_id = table.Column<long>(type: "bigint", nullable: false),
                    destination_fiscal_document_id = table.Column<long>(type: "bigint", nullable: true),
                    assigned_by_username = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    assigned_by_display_name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    assigned_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    released_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    released_by_username = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    released_by_display_name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_billing_document_pending_item_assignment", x => x.id);
                    table.ForeignKey(
                        name: "FK_billing_document_pending_item_assignment_billing_document_de~",
                        column: x => x.destination_billing_document_id,
                        principalTable: "billing_document",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_billing_document_pending_item_assignment_billing_document_it~",
                        column: x => x.billing_document_item_removal_id,
                        principalTable: "billing_document_item_removal",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_billing_document_pending_item_assignment_fiscal_document_des~",
                        column: x => x.destination_fiscal_document_id,
                        principalTable: "fiscal_document",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_billing_document_item_source_billing_document_item_removal_id",
                table: "billing_document_item",
                column: "source_billing_document_item_removal_id");

            migrationBuilder.CreateIndex(
                name: "IX_billing_document_pending_item_assignment_billing_document_i~1",
                table: "billing_document_pending_item_assignment",
                columns: new[] { "billing_document_item_removal_id", "released_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_billing_document_pending_item_assignment_billing_document_it~",
                table: "billing_document_pending_item_assignment",
                column: "billing_document_item_removal_id");

            migrationBuilder.CreateIndex(
                name: "IX_billing_document_pending_item_assignment_destination_billing~",
                table: "billing_document_pending_item_assignment",
                column: "destination_billing_document_id");

            migrationBuilder.CreateIndex(
                name: "IX_billing_document_pending_item_assignment_destination_fiscal_~",
                table: "billing_document_pending_item_assignment",
                column: "destination_fiscal_document_id");

            migrationBuilder.AddForeignKey(
                name: "FK_billing_document_item_billing_document_item_removal_source_b~",
                table: "billing_document_item",
                column: "source_billing_document_item_removal_id",
                principalTable: "billing_document_item_removal",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_billing_document_item_billing_document_item_removal_source_b~",
                table: "billing_document_item");

            migrationBuilder.DropTable(
                name: "billing_document_pending_item_assignment");

            migrationBuilder.DropIndex(
                name: "IX_billing_document_item_source_billing_document_item_removal_id",
                table: "billing_document_item");

            migrationBuilder.DropColumn(
                name: "available_for_pending_billing_reuse",
                table: "billing_document_item_removal");

            migrationBuilder.DropColumn(
                name: "discount_amount",
                table: "billing_document_item_removal");

            migrationBuilder.DropColumn(
                name: "line_total",
                table: "billing_document_item_removal");

            migrationBuilder.DropColumn(
                name: "sat_product_service_code",
                table: "billing_document_item_removal");

            migrationBuilder.DropColumn(
                name: "sat_unit_code",
                table: "billing_document_item_removal");

            migrationBuilder.DropColumn(
                name: "tax_amount",
                table: "billing_document_item_removal");

            migrationBuilder.DropColumn(
                name: "tax_rate",
                table: "billing_document_item_removal");

            migrationBuilder.DropColumn(
                name: "unit_price",
                table: "billing_document_item_removal");

            migrationBuilder.DropColumn(
                name: "source_billing_document_item_removal_id",
                table: "billing_document_item");
        }
    }
}

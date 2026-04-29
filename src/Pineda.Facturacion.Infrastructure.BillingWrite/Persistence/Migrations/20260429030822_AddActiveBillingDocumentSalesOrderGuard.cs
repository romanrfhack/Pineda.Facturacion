using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddActiveBillingDocumentSalesOrderGuard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "active_sales_order_id",
                table: "billing_document",
                type: "bigint",
                nullable: true,
                computedColumnSql: "CASE WHEN `status` <> 5 THEN `sales_order_id` ELSE NULL END",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "ux_billing_document_active_sales_order",
                table: "billing_document",
                column: "active_sales_order_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_billing_document_active_sales_order",
                table: "billing_document");

            migrationBuilder.DropColumn(
                name: "active_sales_order_id",
                table: "billing_document");
        }
    }
}

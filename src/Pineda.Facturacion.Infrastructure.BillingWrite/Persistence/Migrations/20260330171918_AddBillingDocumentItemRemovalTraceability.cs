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
            migrationBuilder.DropForeignKey(
                name: "FK_billing_document_item_removal_billing_document_item_billing_~",
                table: "billing_document_item_removal");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddForeignKey(
                name: "FK_billing_document_item_removal_billing_document_item_billing_~",
                table: "billing_document_item_removal",
                column: "billing_document_item_id",
                principalTable: "billing_document_item",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}

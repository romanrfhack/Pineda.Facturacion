using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountsReceivablePortfolioSlice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "fiscal_receiver_id",
                table: "accounts_receivable_invoice",
                type: "bigint",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE accounts_receivable_invoice invoice
                INNER JOIN fiscal_document document ON document.id = invoice.fiscal_document_id
                SET invoice.fiscal_receiver_id = document.fiscal_receiver_id
                WHERE invoice.fiscal_document_id IS NOT NULL
                  AND document.fiscal_receiver_id IS NOT NULL
                  AND invoice.fiscal_receiver_id IS NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_accounts_receivable_invoice_due_at_utc",
                table: "accounts_receivable_invoice",
                column: "due_at_utc");

            migrationBuilder.CreateIndex(
                name: "IX_accounts_receivable_invoice_fiscal_receiver_id",
                table: "accounts_receivable_invoice",
                column: "fiscal_receiver_id");

            migrationBuilder.CreateIndex(
                name: "IX_accounts_receivable_invoice_status",
                table: "accounts_receivable_invoice",
                column: "status");

            migrationBuilder.AddForeignKey(
                name: "FK_accounts_receivable_invoice_fiscal_receiver_fiscal_receiver_~",
                table: "accounts_receivable_invoice",
                column: "fiscal_receiver_id",
                principalTable: "fiscal_receiver",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_accounts_receivable_invoice_fiscal_receiver_fiscal_receiver_~",
                table: "accounts_receivable_invoice");

            migrationBuilder.DropIndex(
                name: "IX_accounts_receivable_invoice_due_at_utc",
                table: "accounts_receivable_invoice");

            migrationBuilder.DropIndex(
                name: "IX_accounts_receivable_invoice_fiscal_receiver_id",
                table: "accounts_receivable_invoice");

            migrationBuilder.DropIndex(
                name: "IX_accounts_receivable_invoice_status",
                table: "accounts_receivable_invoice");

            migrationBuilder.DropColumn(
                name: "fiscal_receiver_id",
                table: "accounts_receivable_invoice");
        }
    }
}

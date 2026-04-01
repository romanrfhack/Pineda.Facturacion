using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExternalRepOperationSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<long>(
                name: "fiscal_stamp_id",
                table: "payment_complement_related_document",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<long>(
                name: "fiscal_document_id",
                table: "payment_complement_related_document",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AddColumn<long>(
                name: "external_rep_base_document_id",
                table: "payment_complement_related_document",
                type: "bigint",
                nullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "fiscal_stamp_id",
                table: "fiscal_cancellation",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "fiscal_document_id",
                table: "fiscal_cancellation",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AddColumn<long>(
                name: "external_rep_base_document_id",
                table: "accounts_receivable_invoice",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_payment_complement_related_document_external_rep_base_docume~",
                table: "payment_complement_related_document",
                column: "external_rep_base_document_id");

            migrationBuilder.CreateIndex(
                name: "IX_accounts_receivable_invoice_external_rep_base_document_id",
                table: "accounts_receivable_invoice",
                column: "external_rep_base_document_id",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_accounts_receivable_invoice_external_rep_base_document_exter~",
                table: "accounts_receivable_invoice",
                column: "external_rep_base_document_id",
                principalTable: "external_rep_base_document",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_payment_complement_related_document_external_rep_base_docume~",
                table: "payment_complement_related_document",
                column: "external_rep_base_document_id",
                principalTable: "external_rep_base_document",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_accounts_receivable_invoice_external_rep_base_document_exter~",
                table: "accounts_receivable_invoice");

            migrationBuilder.DropForeignKey(
                name: "FK_payment_complement_related_document_external_rep_base_docume~",
                table: "payment_complement_related_document");

            migrationBuilder.DropIndex(
                name: "IX_payment_complement_related_document_external_rep_base_docume~",
                table: "payment_complement_related_document");

            migrationBuilder.DropIndex(
                name: "IX_accounts_receivable_invoice_external_rep_base_document_id",
                table: "accounts_receivable_invoice");

            migrationBuilder.DropColumn(
                name: "external_rep_base_document_id",
                table: "payment_complement_related_document");

            migrationBuilder.DropColumn(
                name: "external_rep_base_document_id",
                table: "accounts_receivable_invoice");

            migrationBuilder.AlterColumn<long>(
                name: "fiscal_stamp_id",
                table: "payment_complement_related_document",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "fiscal_document_id",
                table: "payment_complement_related_document",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "fiscal_stamp_id",
                table: "fiscal_cancellation",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<long>(
                name: "fiscal_document_id",
                table: "fiscal_cancellation",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");
        }
    }
}

using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentComplementMultiplePaymentsSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "payment_complement_payment",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    payment_complement_document_id = table.Column<long>(type: "bigint", nullable: false),
                    accounts_receivable_payment_id = table.Column<long>(type: "bigint", nullable: false),
                    payment_date_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    payment_form_sat = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false),
                    currency_code = table.Column<string>(type: "char(3)", maxLength: 3, nullable: false),
                    amount = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    exchange_rate = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    operation_number = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    ordering_bank_rfc = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    ordering_account_number = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    beneficiary_bank_rfc = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    beneficiary_account_number = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    payment_chain_type = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: true),
                    payment_certificate = table.Column<string>(type: "text", nullable: true),
                    payment_chain = table.Column<string>(type: "text", nullable: true),
                    payment_seal = table.Column<string>(type: "text", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_complement_payment", x => x.id);
                    table.ForeignKey(
                        name: "FK_payment_complement_payment_accounts_receivable_payment_accounts_receivable_payment_id",
                        column: x => x.accounts_receivable_payment_id,
                        principalTable: "accounts_receivable_payment",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payment_complement_payment_payment_complement_document_payment_complement_document_id",
                        column: x => x.payment_complement_document_id,
                        principalTable: "payment_complement_document",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddColumn<string>(
                name: "folio",
                table: "payment_complement_related_document",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "currency_equivalence",
                table: "payment_complement_related_document",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "payment_complement_payment_id",
                table: "payment_complement_related_document",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "series",
                table: "payment_complement_related_document",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "tax_object_code",
                table: "payment_complement_related_document",
                type: "varchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "01");

            migrationBuilder.CreateIndex(
                name: "IX_payment_complement_payment_accounts_receivable_payment_id",
                table: "payment_complement_payment",
                column: "accounts_receivable_payment_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payment_complement_payment_payment_complement_document_id",
                table: "payment_complement_payment",
                column: "payment_complement_document_id");

            migrationBuilder.Sql(
                """
                INSERT INTO payment_complement_payment (
                    payment_complement_document_id,
                    accounts_receivable_payment_id,
                    payment_date_utc,
                    payment_form_sat,
                    currency_code,
                    amount,
                    exchange_rate,
                    operation_number,
                    ordering_bank_rfc,
                    ordering_account_number,
                    beneficiary_bank_rfc,
                    beneficiary_account_number,
                    payment_chain_type,
                    payment_certificate,
                    payment_chain,
                    payment_seal,
                    created_at_utc
                )
                SELECT
                    d.id,
                    d.accounts_receivable_payment_id,
                    d.payment_date_utc,
                    p.payment_form_sat,
                    d.currency_code,
                    d.total_payments_amount,
                    NULL,
                    NULL,
                    NULL,
                    NULL,
                    NULL,
                    NULL,
                    NULL,
                    NULL,
                    NULL,
                    NULL,
                    d.created_at_utc
                FROM payment_complement_document d
                INNER JOIN accounts_receivable_payment p ON p.id = d.accounts_receivable_payment_id;
                """);

            migrationBuilder.Sql(
                """
                UPDATE payment_complement_related_document rd
                INNER JOIN payment_complement_document d ON d.id = rd.payment_complement_document_id
                INNER JOIN payment_complement_payment pp
                    ON pp.payment_complement_document_id = d.id
                   AND pp.accounts_receivable_payment_id = d.accounts_receivable_payment_id
                SET rd.payment_complement_payment_id = pp.id
                WHERE rd.payment_complement_payment_id IS NULL;
                """);

            migrationBuilder.AlterColumn<long>(
                name: "payment_complement_payment_id",
                table: "payment_complement_related_document",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_payment_complement_related_document_payment_complement_payment_id",
                table: "payment_complement_related_document",
                column: "payment_complement_payment_id");

            migrationBuilder.AddForeignKey(
                name: "FK_payment_complement_related_document_payment_complement_payment_payment_complement_payment_id",
                table: "payment_complement_related_document",
                column: "payment_complement_payment_id",
                principalTable: "payment_complement_payment",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_payment_complement_related_document_payment_complement_payment_payment_complement_payment_id",
                table: "payment_complement_related_document");

            migrationBuilder.DropTable(
                name: "payment_complement_payment");

            migrationBuilder.DropIndex(
                name: "IX_payment_complement_related_document_payment_complement_payment_id",
                table: "payment_complement_related_document");

            migrationBuilder.DropColumn(
                name: "folio",
                table: "payment_complement_related_document");

            migrationBuilder.DropColumn(
                name: "currency_equivalence",
                table: "payment_complement_related_document");

            migrationBuilder.DropColumn(
                name: "payment_complement_payment_id",
                table: "payment_complement_related_document");

            migrationBuilder.DropColumn(
                name: "series",
                table: "payment_complement_related_document");

            migrationBuilder.DropColumn(
                name: "tax_object_code",
                table: "payment_complement_related_document");
        }
    }
}

using System;
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
            migrationBuilder.AddColumn<decimal>(
                name: "currency_equivalence",
                table: "payment_complement_related_document",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "folio",
                table: "payment_complement_related_document",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<long>(
                name: "payment_complement_payment_id",
                table: "payment_complement_related_document",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "series",
                table: "payment_complement_related_document",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "tax_object_code",
                table: "payment_complement_related_document",
                type: "varchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "payment_complement_payment",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    payment_complement_document_id = table.Column<long>(type: "bigint", nullable: false),
                    accounts_receivable_payment_id = table.Column<long>(type: "bigint", nullable: false),
                    payment_date_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    payment_form_sat = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    currency_code = table.Column<string>(type: "char(3)", maxLength: 3, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    amount = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    exchange_rate = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    operation_number = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ordering_bank_rfc = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ordering_account_number = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    beneficiary_bank_rfc = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    beneficiary_account_number = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    payment_chain_type = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    payment_certificate = table.Column<string>(type: "text", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    payment_chain = table.Column<string>(type: "text", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    payment_seal = table.Column<string>(type: "text", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_complement_payment", x => x.id);
                    table.ForeignKey(
                        name: "FK_payment_complement_payment_accounts_receivable_payment_accou~",
                        column: x => x.accounts_receivable_payment_id,
                        principalTable: "accounts_receivable_payment",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payment_complement_payment_payment_complement_document_payme~",
                        column: x => x.payment_complement_document_id,
                        principalTable: "payment_complement_document",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_payment_complement_related_document_payment_complement_payme~",
                table: "payment_complement_related_document",
                column: "payment_complement_payment_id");

            migrationBuilder.CreateIndex(
                name: "IX_payment_complement_payment_accounts_receivable_payment_id",
                table: "payment_complement_payment",
                column: "accounts_receivable_payment_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payment_complement_payment_payment_complement_document_id",
                table: "payment_complement_payment",
                column: "payment_complement_document_id");

            migrationBuilder.AddForeignKey(
                name: "FK_payment_complement_related_document_payment_complement_payme~",
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
                name: "FK_payment_complement_related_document_payment_complement_payme~",
                table: "payment_complement_related_document");

            migrationBuilder.DropTable(
                name: "payment_complement_payment");

            migrationBuilder.DropIndex(
                name: "IX_payment_complement_related_document_payment_complement_payme~",
                table: "payment_complement_related_document");

            migrationBuilder.DropColumn(
                name: "currency_equivalence",
                table: "payment_complement_related_document");

            migrationBuilder.DropColumn(
                name: "folio",
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

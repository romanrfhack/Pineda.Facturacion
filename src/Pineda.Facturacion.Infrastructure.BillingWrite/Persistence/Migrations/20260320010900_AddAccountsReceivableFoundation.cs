using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountsReceivableFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "accounts_receivable_invoice",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    billing_document_id = table.Column<long>(type: "bigint", nullable: false),
                    fiscal_document_id = table.Column<long>(type: "bigint", nullable: false),
                    fiscal_stamp_id = table.Column<long>(type: "bigint", nullable: false),
                    status = table.Column<int>(type: "int", nullable: false),
                    payment_method_sat = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    payment_form_sat_initial = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    is_credit_sale = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    credit_days = table.Column<int>(type: "int", nullable: true),
                    issued_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    due_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    currency_code = table.Column<string>(type: "char(3)", maxLength: 3, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    total = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    paid_total = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    outstanding_balance = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounts_receivable_invoice", x => x.id);
                    table.ForeignKey(
                        name: "FK_accounts_receivable_invoice_billing_document_billing_documen~",
                        column: x => x.billing_document_id,
                        principalTable: "billing_document",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_accounts_receivable_invoice_fiscal_document_fiscal_document_~",
                        column: x => x.fiscal_document_id,
                        principalTable: "fiscal_document",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_accounts_receivable_invoice_fiscal_stamp_fiscal_stamp_id",
                        column: x => x.fiscal_stamp_id,
                        principalTable: "fiscal_stamp",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "accounts_receivable_payment",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    payment_date_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    payment_form_sat = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    currency_code = table.Column<string>(type: "char(3)", maxLength: 3, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    amount = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    reference = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    notes = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    received_from_fiscal_receiver_id = table.Column<long>(type: "bigint", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounts_receivable_payment", x => x.id);
                    table.ForeignKey(
                        name: "FK_accounts_receivable_payment_fiscal_receiver_received_from_fi~",
                        column: x => x.received_from_fiscal_receiver_id,
                        principalTable: "fiscal_receiver",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "accounts_receivable_payment_application",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    accounts_receivable_payment_id = table.Column<long>(type: "bigint", nullable: false),
                    accounts_receivable_invoice_id = table.Column<long>(type: "bigint", nullable: false),
                    application_sequence = table.Column<int>(type: "int", nullable: false),
                    applied_amount = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    previous_balance = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    new_balance = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounts_receivable_payment_application", x => x.id);
                    table.ForeignKey(
                        name: "FK_accounts_receivable_payment_application_accounts_receivable_~",
                        column: x => x.accounts_receivable_invoice_id,
                        principalTable: "accounts_receivable_invoice",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_accounts_receivable_payment_application_accounts_receivable~1",
                        column: x => x.accounts_receivable_payment_id,
                        principalTable: "accounts_receivable_payment",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_accounts_receivable_invoice_billing_document_id",
                table: "accounts_receivable_invoice",
                column: "billing_document_id");

            migrationBuilder.CreateIndex(
                name: "IX_accounts_receivable_invoice_fiscal_document_id",
                table: "accounts_receivable_invoice",
                column: "fiscal_document_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_accounts_receivable_invoice_fiscal_stamp_id",
                table: "accounts_receivable_invoice",
                column: "fiscal_stamp_id");

            migrationBuilder.CreateIndex(
                name: "IX_accounts_receivable_payment_received_from_fiscal_receiver_id",
                table: "accounts_receivable_payment",
                column: "received_from_fiscal_receiver_id");

            migrationBuilder.CreateIndex(
                name: "IX_accounts_receivable_payment_application_accounts_receivable_~",
                table: "accounts_receivable_payment_application",
                column: "accounts_receivable_invoice_id");

            migrationBuilder.CreateIndex(
                name: "IX_accounts_receivable_payment_application_accounts_receivable~1",
                table: "accounts_receivable_payment_application",
                column: "accounts_receivable_payment_id");

            migrationBuilder.CreateIndex(
                name: "IX_accounts_receivable_payment_application_accounts_receivable~2",
                table: "accounts_receivable_payment_application",
                columns: new[] { "accounts_receivable_payment_id", "application_sequence" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "accounts_receivable_payment_application");

            migrationBuilder.DropTable(
                name: "accounts_receivable_invoice");

            migrationBuilder.DropTable(
                name: "accounts_receivable_payment");
        }
    }
}

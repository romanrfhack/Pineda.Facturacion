using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentComplementFoundationAndStamping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "payment_complement_document",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    accounts_receivable_payment_id = table.Column<long>(type: "bigint", nullable: false),
                    status = table.Column<int>(type: "int", nullable: false),
                    provider_name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    cfdi_version = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    document_type = table.Column<string>(type: "varchar(5)", maxLength: 5, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    issued_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    payment_date_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    currency_code = table.Column<string>(type: "char(3)", maxLength: 3, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    total_payments_amount = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    issuer_profile_id = table.Column<long>(type: "bigint", nullable: true),
                    fiscal_receiver_id = table.Column<long>(type: "bigint", nullable: true),
                    issuer_rfc = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    issuer_legal_name = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    issuer_fiscal_regime_code = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    issuer_postal_code = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    receiver_rfc = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    receiver_legal_name = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    receiver_fiscal_regime_code = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    receiver_postal_code = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    receiver_country_code = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    receiver_foreign_tax_registration = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    pac_environment = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    certificate_reference = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    private_key_reference = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    private_key_password_reference = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_complement_document", x => x.id);
                    table.ForeignKey(
                        name: "FK_payment_complement_document_accounts_receivable_payment_acco~",
                        column: x => x.accounts_receivable_payment_id,
                        principalTable: "accounts_receivable_payment",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payment_complement_document_fiscal_receiver_fiscal_receiver_~",
                        column: x => x.fiscal_receiver_id,
                        principalTable: "fiscal_receiver",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payment_complement_document_issuer_profile_issuer_profile_id",
                        column: x => x.issuer_profile_id,
                        principalTable: "issuer_profile",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "payment_complement_related_document",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    payment_complement_document_id = table.Column<long>(type: "bigint", nullable: false),
                    accounts_receivable_invoice_id = table.Column<long>(type: "bigint", nullable: false),
                    fiscal_document_id = table.Column<long>(type: "bigint", nullable: false),
                    fiscal_stamp_id = table.Column<long>(type: "bigint", nullable: false),
                    related_document_uuid = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    installment_number = table.Column<int>(type: "int", nullable: false),
                    previous_balance = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    paid_amount = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    remaining_balance = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    currency_code = table.Column<string>(type: "char(3)", maxLength: 3, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_complement_related_document", x => x.id);
                    table.ForeignKey(
                        name: "FK_payment_complement_related_document_accounts_receivable_invo~",
                        column: x => x.accounts_receivable_invoice_id,
                        principalTable: "accounts_receivable_invoice",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payment_complement_related_document_fiscal_document_fiscal_d~",
                        column: x => x.fiscal_document_id,
                        principalTable: "fiscal_document",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payment_complement_related_document_fiscal_stamp_fiscal_stam~",
                        column: x => x.fiscal_stamp_id,
                        principalTable: "fiscal_stamp",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payment_complement_related_document_payment_complement_docum~",
                        column: x => x.payment_complement_document_id,
                        principalTable: "payment_complement_document",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "payment_complement_stamp",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    payment_complement_document_id = table.Column<long>(type: "bigint", nullable: false),
                    provider_name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    provider_operation = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<int>(type: "int", nullable: false),
                    provider_request_hash = table.Column<string>(type: "char(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    provider_tracking_id = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    provider_code = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    provider_message = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    uuid = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    stamped_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    xml_content = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    xml_hash = table.Column<string>(type: "char(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    original_string = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    qr_code_text_or_url = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    raw_response_summary_json = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    error_code = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    error_message = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_complement_stamp", x => x.id);
                    table.ForeignKey(
                        name: "FK_payment_complement_stamp_payment_complement_document_payment~",
                        column: x => x.payment_complement_document_id,
                        principalTable: "payment_complement_document",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_payment_complement_document_accounts_receivable_payment_id",
                table: "payment_complement_document",
                column: "accounts_receivable_payment_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payment_complement_document_fiscal_receiver_id",
                table: "payment_complement_document",
                column: "fiscal_receiver_id");

            migrationBuilder.CreateIndex(
                name: "IX_payment_complement_document_issuer_profile_id",
                table: "payment_complement_document",
                column: "issuer_profile_id");

            migrationBuilder.CreateIndex(
                name: "IX_payment_complement_related_document_accounts_receivable_invo~",
                table: "payment_complement_related_document",
                column: "accounts_receivable_invoice_id");

            migrationBuilder.CreateIndex(
                name: "IX_payment_complement_related_document_fiscal_document_id",
                table: "payment_complement_related_document",
                column: "fiscal_document_id");

            migrationBuilder.CreateIndex(
                name: "IX_payment_complement_related_document_fiscal_stamp_id",
                table: "payment_complement_related_document",
                column: "fiscal_stamp_id");

            migrationBuilder.CreateIndex(
                name: "IX_payment_complement_related_document_payment_complement_docum~",
                table: "payment_complement_related_document",
                column: "payment_complement_document_id");

            migrationBuilder.CreateIndex(
                name: "IX_payment_complement_related_document_related_document_uuid",
                table: "payment_complement_related_document",
                column: "related_document_uuid");

            migrationBuilder.CreateIndex(
                name: "IX_payment_complement_stamp_payment_complement_document_id",
                table: "payment_complement_stamp",
                column: "payment_complement_document_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payment_complement_stamp_uuid",
                table: "payment_complement_stamp",
                column: "uuid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "payment_complement_related_document");

            migrationBuilder.DropTable(
                name: "payment_complement_stamp");

            migrationBuilder.DropTable(
                name: "payment_complement_document");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountsReceivableCollectionSlice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "collection_commitment",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    accounts_receivable_invoice_id = table.Column<long>(type: "bigint", nullable: false),
                    promised_amount = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    promised_date_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    status = table.Column<int>(type: "int", nullable: false),
                    notes = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    created_by_username = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_collection_commitment", x => x.id);
                    table.ForeignKey(
                        name: "FK_collection_commitment_accounts_receivable_invoice_accounts_r~",
                        column: x => x.accounts_receivable_invoice_id,
                        principalTable: "accounts_receivable_invoice",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "collection_note",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    accounts_receivable_invoice_id = table.Column<long>(type: "bigint", nullable: false),
                    note_type = table.Column<int>(type: "int", nullable: false),
                    content = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    next_follow_up_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    created_by_username = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_collection_note", x => x.id);
                    table.ForeignKey(
                        name: "FK_collection_note_accounts_receivable_invoice_accounts_receiva~",
                        column: x => x.accounts_receivable_invoice_id,
                        principalTable: "accounts_receivable_invoice",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_collection_commitment_accounts_receivable_invoice_id",
                table: "collection_commitment",
                column: "accounts_receivable_invoice_id");

            migrationBuilder.CreateIndex(
                name: "IX_collection_commitment_promised_date_utc",
                table: "collection_commitment",
                column: "promised_date_utc");

            migrationBuilder.CreateIndex(
                name: "IX_collection_commitment_status",
                table: "collection_commitment",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_collection_note_accounts_receivable_invoice_id",
                table: "collection_note",
                column: "accounts_receivable_invoice_id");

            migrationBuilder.CreateIndex(
                name: "IX_collection_note_next_follow_up_at_utc",
                table: "collection_note",
                column: "next_follow_up_at_utc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "collection_commitment");

            migrationBuilder.DropTable(
                name: "collection_note");
        }
    }
}

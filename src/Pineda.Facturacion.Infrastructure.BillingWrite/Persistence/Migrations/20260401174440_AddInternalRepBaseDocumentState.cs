using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInternalRepBaseDocumentState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "internal_rep_base_document_state",
                columns: table => new
                {
                    fiscal_document_id = table.Column<long>(type: "bigint", nullable: false),
                    last_eligibility_evaluated_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    last_eligibility_status = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    last_primary_reason_code = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    last_primary_reason_message = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    rep_pending_flag = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    last_rep_issued_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    rep_count = table.Column<int>(type: "int", nullable: false),
                    total_paid_applied = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_internal_rep_base_document_state", x => x.fiscal_document_id);
                    table.ForeignKey(
                        name: "FK_internal_rep_base_document_state_fiscal_document_fiscal_docu~",
                        column: x => x.fiscal_document_id,
                        principalTable: "fiscal_document",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "internal_rep_base_document_state");
        }
    }
}

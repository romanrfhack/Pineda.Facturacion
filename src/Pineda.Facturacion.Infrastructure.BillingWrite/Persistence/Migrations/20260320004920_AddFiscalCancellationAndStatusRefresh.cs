using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFiscalCancellationAndStatusRefresh : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "last_known_external_status",
                table: "fiscal_stamp",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "last_status_check_at_utc",
                table: "fiscal_stamp",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "last_status_provider_code",
                table: "fiscal_stamp",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "last_status_provider_message",
                table: "fiscal_stamp",
                type: "varchar(1000)",
                maxLength: 1000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "last_status_raw_response_summary_json",
                table: "fiscal_stamp",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "fiscal_cancellation",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    fiscal_document_id = table.Column<long>(type: "bigint", nullable: false),
                    fiscal_stamp_id = table.Column<long>(type: "bigint", nullable: false),
                    status = table.Column<int>(type: "int", nullable: false),
                    cancellation_reason_code = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    replacement_uuid = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    provider_name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    provider_operation = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    provider_tracking_id = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    provider_code = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    provider_message = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    requested_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    cancelled_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
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
                    table.PrimaryKey("PK_fiscal_cancellation", x => x.id);
                    table.ForeignKey(
                        name: "FK_fiscal_cancellation_fiscal_document_fiscal_document_id",
                        column: x => x.fiscal_document_id,
                        principalTable: "fiscal_document",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_fiscal_cancellation_fiscal_stamp_fiscal_stamp_id",
                        column: x => x.fiscal_stamp_id,
                        principalTable: "fiscal_stamp",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_fiscal_cancellation_fiscal_document_id",
                table: "fiscal_cancellation",
                column: "fiscal_document_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_fiscal_cancellation_fiscal_stamp_id",
                table: "fiscal_cancellation",
                column: "fiscal_stamp_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fiscal_cancellation");

            migrationBuilder.DropColumn(
                name: "last_known_external_status",
                table: "fiscal_stamp");

            migrationBuilder.DropColumn(
                name: "last_status_check_at_utc",
                table: "fiscal_stamp");

            migrationBuilder.DropColumn(
                name: "last_status_provider_code",
                table: "fiscal_stamp");

            migrationBuilder.DropColumn(
                name: "last_status_provider_message",
                table: "fiscal_stamp");

            migrationBuilder.DropColumn(
                name: "last_status_raw_response_summary_json",
                table: "fiscal_stamp");
        }
    }
}

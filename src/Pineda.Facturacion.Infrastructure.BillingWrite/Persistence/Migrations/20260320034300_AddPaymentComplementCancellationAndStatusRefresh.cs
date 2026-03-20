using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentComplementCancellationAndStatusRefresh : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "last_known_external_status",
                table: "payment_complement_stamp",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "last_status_check_at_utc",
                table: "payment_complement_stamp",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "last_status_provider_code",
                table: "payment_complement_stamp",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "last_status_provider_message",
                table: "payment_complement_stamp",
                type: "varchar(1000)",
                maxLength: 1000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "last_status_raw_response_summary_json",
                table: "payment_complement_stamp",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "payment_complement_cancellation",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    payment_complement_document_id = table.Column<long>(type: "bigint", nullable: false),
                    payment_complement_stamp_id = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("PK_payment_complement_cancellation", x => x.id);
                    table.ForeignKey(
                        name: "FK_payment_complement_cancellation_payment_complement_document_~",
                        column: x => x.payment_complement_document_id,
                        principalTable: "payment_complement_document",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_payment_complement_cancellation_payment_complement_stamp_pay~",
                        column: x => x.payment_complement_stamp_id,
                        principalTable: "payment_complement_stamp",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_payment_complement_cancellation_payment_complement_document_~",
                table: "payment_complement_cancellation",
                column: "payment_complement_document_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payment_complement_cancellation_payment_complement_stamp_id",
                table: "payment_complement_cancellation",
                column: "payment_complement_stamp_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "payment_complement_cancellation");

            migrationBuilder.DropColumn(
                name: "last_known_external_status",
                table: "payment_complement_stamp");

            migrationBuilder.DropColumn(
                name: "last_status_check_at_utc",
                table: "payment_complement_stamp");

            migrationBuilder.DropColumn(
                name: "last_status_provider_code",
                table: "payment_complement_stamp");

            migrationBuilder.DropColumn(
                name: "last_status_provider_message",
                table: "payment_complement_stamp");

            migrationBuilder.DropColumn(
                name: "last_status_raw_response_summary_json",
                table: "payment_complement_stamp");
        }
    }
}

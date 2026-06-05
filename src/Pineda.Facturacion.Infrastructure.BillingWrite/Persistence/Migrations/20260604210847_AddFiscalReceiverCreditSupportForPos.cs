using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFiscalReceiverCreditSupportForPos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "approved_credit_limit_amount",
                table: "fiscal_receiver",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "credit_days",
                table: "fiscal_receiver",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "credit_enabled",
                table: "fiscal_receiver",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "credit_updated_at_utc",
                table: "fiscal_receiver",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "credit_updated_by",
                table: "fiscal_receiver",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "approved_credit_limit_amount",
                table: "fiscal_receiver");

            migrationBuilder.DropColumn(
                name: "credit_days",
                table: "fiscal_receiver");

            migrationBuilder.DropColumn(
                name: "credit_enabled",
                table: "fiscal_receiver");

            migrationBuilder.DropColumn(
                name: "credit_updated_at_utc",
                table: "fiscal_receiver");

            migrationBuilder.DropColumn(
                name: "credit_updated_by",
                table: "fiscal_receiver");
        }
    }
}

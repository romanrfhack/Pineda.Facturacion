using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAppUserLoginHardeningPhase1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "failed_login_attempt_count",
                table: "app_user",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_failed_login_at_utc",
                table: "app_user",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "lockout_end_at_utc",
                table: "app_user",
                type: "datetime(6)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "failed_login_attempt_count",
                table: "app_user");

            migrationBuilder.DropColumn(
                name: "last_failed_login_at_utc",
                table: "app_user");

            migrationBuilder.DropColumn(
                name: "lockout_end_at_utc",
                table: "app_user");
        }
    }
}

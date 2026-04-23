using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIssuerProfileActiveSingletonGuard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "active_singleton_key",
                table: "issuer_profile",
                type: "int",
                nullable: true,
                computedColumnSql: "CASE WHEN is_active THEN 1 ELSE NULL END",
                stored: true);

            migrationBuilder.Sql(
                """
                UPDATE issuer_profile
                SET is_active = 0,
                    updated_at_utc = UTC_TIMESTAMP()
                WHERE is_active = 1
                  AND id NOT IN (
                      SELECT keep_active.id
                      FROM (
                          SELECT id
                          FROM issuer_profile
                          WHERE is_active = 1
                          ORDER BY updated_at_utc DESC, created_at_utc DESC, id DESC
                          LIMIT 1
                      ) AS keep_active
                  );
                """);

            migrationBuilder.CreateIndex(
                name: "ux_issuer_profile_active_singleton_key",
                table: "issuer_profile",
                column: "active_singleton_key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_issuer_profile_active_singleton_key",
                table: "issuer_profile");

            migrationBuilder.DropColumn(
                name: "active_singleton_key",
                table: "issuer_profile");
        }
    }
}

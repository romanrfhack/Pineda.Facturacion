using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIssuerProfileLogoBlobStorage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "logo_data",
                table: "issuer_profile",
                type: "longblob",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "logo_size_bytes",
                table: "issuer_profile",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "logo_data",
                table: "issuer_profile");

            migrationBuilder.DropColumn(
                name: "logo_size_bytes",
                table: "issuer_profile");
        }
    }
}

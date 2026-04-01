using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLegacyImportRevisionHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "legacy_import_revision",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    legacy_import_record_id = table.Column<long>(type: "bigint", nullable: false),
                    legacy_order_id = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    revision_number = table.Column<int>(type: "int", nullable: false),
                    previous_revision_number = table.Column<int>(type: "int", nullable: true),
                    action_type = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    outcome = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    source_hash = table.Column<string>(type: "char(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    previous_source_hash = table.Column<string>(type: "char(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    applied_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    is_current = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    actor_user_id = table.Column<long>(type: "bigint", nullable: true),
                    actor_username = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    sales_order_id = table.Column<long>(type: "bigint", nullable: true),
                    billing_document_id = table.Column<long>(type: "bigint", nullable: true),
                    fiscal_document_id = table.Column<long>(type: "bigint", nullable: true),
                    added_lines = table.Column<int>(type: "int", nullable: false),
                    removed_lines = table.Column<int>(type: "int", nullable: false),
                    modified_lines = table.Column<int>(type: "int", nullable: false),
                    unchanged_lines = table.Column<int>(type: "int", nullable: false),
                    old_subtotal = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    new_subtotal = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    old_total = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    new_total = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    eligibility_status = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    eligibility_reason_code = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    eligibility_reason_message = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    snapshot_json = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    diff_json = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_legacy_import_revision", x => x.id);
                    table.ForeignKey(
                        name: "FK_legacy_import_revision_legacy_import_record_legacy_import_re~",
                        column: x => x.legacy_import_record_id,
                        principalTable: "legacy_import_record",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_legacy_import_revision_legacy_import_record_id_is_current",
                table: "legacy_import_revision",
                columns: new[] { "legacy_import_record_id", "is_current" });

            migrationBuilder.CreateIndex(
                name: "IX_legacy_import_revision_legacy_import_record_id_revision_numb~",
                table: "legacy_import_revision",
                columns: new[] { "legacy_import_record_id", "revision_number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "legacy_import_revision");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pineda.Facturacion.Infrastructure.BillingWrite.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFiscalDocumentSpecialFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "fiscal_receiver_special_field_definition",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    fiscal_receiver_id = table.Column<long>(type: "bigint", nullable: false),
                    code = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    label = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    data_type = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    max_length = table.Column<int>(type: "int", nullable: true),
                    help_text = table.Column<string>(type: "varchar(250)", maxLength: 250, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    is_required = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    is_active = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    display_order = table.Column<int>(type: "int", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fiscal_receiver_special_field_definition", x => x.id);
                    table.ForeignKey(
                        name: "FK_fiscal_receiver_special_field_definition_fiscal_receiver_fis~",
                        column: x => x.fiscal_receiver_id,
                        principalTable: "fiscal_receiver",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "fiscal_document_special_field_value",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    fiscal_document_id = table.Column<long>(type: "bigint", nullable: false),
                    fiscal_receiver_special_field_definition_id = table.Column<long>(type: "bigint", nullable: false),
                    field_code = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    field_label_snapshot = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    data_type = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    value = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    display_order = table.Column<int>(type: "int", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fiscal_document_special_field_value", x => x.id);
                    table.ForeignKey(
                        name: "FK_fiscal_document_special_field_value_fiscal_document_fiscal_d~",
                        column: x => x.fiscal_document_id,
                        principalTable: "fiscal_document",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_fiscal_document_special_field_value_fiscal_receiver_special_~",
                        column: x => x.fiscal_receiver_special_field_definition_id,
                        principalTable: "fiscal_receiver_special_field_definition",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_fiscal_document_special_field_value_fiscal_document_id_field~",
                table: "fiscal_document_special_field_value",
                columns: new[] { "fiscal_document_id", "field_code" });

            migrationBuilder.CreateIndex(
                name: "IX_fiscal_document_special_field_value_fiscal_receiver_special_~",
                table: "fiscal_document_special_field_value",
                column: "fiscal_receiver_special_field_definition_id");

            migrationBuilder.CreateIndex(
                name: "IX_fiscal_receiver_special_field_definition_fiscal_receiver_id_~",
                table: "fiscal_receiver_special_field_definition",
                columns: new[] { "fiscal_receiver_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_fiscal_receiver_special_field_definition_fiscal_receiver_id~1",
                table: "fiscal_receiver_special_field_definition",
                columns: new[] { "fiscal_receiver_id", "display_order" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fiscal_document_special_field_value");

            migrationBuilder.DropTable(
                name: "fiscal_receiver_special_field_definition");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControClient.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRecordSecretFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "record_secret_flags",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    record_id = table.Column<Guid>(type: "uuid", nullable: false),
                    field_key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    is_secret = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_record_secret_flags", x => x.id);
                    table.ForeignKey(
                        name: "fk_record_secret_flags_record",
                        column: x => x.record_id,
                        principalTable: "record_instances",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_record_secret_flags_record_field",
                table: "record_secret_flags",
                columns: new[] { "record_id", "field_key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "record_secret_flags");
        }
    }
}

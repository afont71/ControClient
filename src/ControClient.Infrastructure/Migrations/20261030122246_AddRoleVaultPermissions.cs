using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControClient.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRoleVaultPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "role_vault_permissions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<int>(type: "integer", nullable: false),
                    permission = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_vault_permissions", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ux_role_vault_permissions_role_permission",
                table: "role_vault_permissions",
                columns: new[] { "role", "permission" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "role_vault_permissions");
        }
    }
}

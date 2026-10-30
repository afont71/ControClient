using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ControClient.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClientDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "address",
                table: "clients",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "city",
                table: "clients",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "contact_email",
                table: "clients",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "contact_mobile",
                table: "clients",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "contact_name",
                table: "clients",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "contact_phone",
                table: "clients",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "contact_role",
                table: "clients",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "country",
                table: "clients",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "notes",
                table: "clients",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "postal_code",
                table: "clients",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "province",
                table: "clients",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "tax_id",
                table: "clients",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "website",
                table: "clients",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "address",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "city",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "contact_email",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "contact_mobile",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "contact_name",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "contact_phone",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "contact_role",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "country",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "notes",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "postal_code",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "province",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "tax_id",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "website",
                table: "clients");
        }
    }
}

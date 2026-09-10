using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GlpiNg.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddPrinterSnmpSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IpAddress",
                table: "Printers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SnmpAuthPassphrase",
                table: "Printers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SnmpAuthProtocol",
                table: "Printers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SnmpCommunity",
                table: "Printers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SnmpPort",
                table: "Printers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SnmpPrivPassphrase",
                table: "Printers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SnmpPrivProtocol",
                table: "Printers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SnmpUsername",
                table: "Printers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SnmpVersion",
                table: "Printers",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IpAddress",
                table: "Printers");

            migrationBuilder.DropColumn(
                name: "SnmpAuthPassphrase",
                table: "Printers");

            migrationBuilder.DropColumn(
                name: "SnmpAuthProtocol",
                table: "Printers");

            migrationBuilder.DropColumn(
                name: "SnmpCommunity",
                table: "Printers");

            migrationBuilder.DropColumn(
                name: "SnmpPort",
                table: "Printers");

            migrationBuilder.DropColumn(
                name: "SnmpPrivPassphrase",
                table: "Printers");

            migrationBuilder.DropColumn(
                name: "SnmpPrivProtocol",
                table: "Printers");

            migrationBuilder.DropColumn(
                name: "SnmpUsername",
                table: "Printers");

            migrationBuilder.DropColumn(
                name: "SnmpVersion",
                table: "Printers");
        }
    }
}

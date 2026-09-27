using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GlpiNg.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddPluginImportSourceIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SourceGlpiId",
                table: "SnmpCredentials",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceGlpiId",
                table: "IpRanges",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceGlpiId",
                table: "DeploymentPackages",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SourceGlpiId",
                table: "SnmpCredentials");

            migrationBuilder.DropColumn(
                name: "SourceGlpiId",
                table: "IpRanges");

            migrationBuilder.DropColumn(
                name: "SourceGlpiId",
                table: "DeploymentPackages");
        }
    }
}

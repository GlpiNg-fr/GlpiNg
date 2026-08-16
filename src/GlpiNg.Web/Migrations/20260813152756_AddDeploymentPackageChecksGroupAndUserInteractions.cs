using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GlpiNg.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddDeploymentPackageChecksGroupAndUserInteractions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ChecksJson",
                table: "DeploymentPackages",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "GroupId",
                table: "DeploymentPackages",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserInteractionsJson",
                table: "DeploymentPackages",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentPackages_GroupId",
                table: "DeploymentPackages",
                column: "GroupId");

            migrationBuilder.AddForeignKey(
                name: "FK_DeploymentPackages_Groups_GroupId",
                table: "DeploymentPackages",
                column: "GroupId",
                principalTable: "Groups",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DeploymentPackages_Groups_GroupId",
                table: "DeploymentPackages");

            migrationBuilder.DropIndex(
                name: "IX_DeploymentPackages_GroupId",
                table: "DeploymentPackages");

            migrationBuilder.DropColumn(
                name: "ChecksJson",
                table: "DeploymentPackages");

            migrationBuilder.DropColumn(
                name: "GroupId",
                table: "DeploymentPackages");

            migrationBuilder.DropColumn(
                name: "UserInteractionsJson",
                table: "DeploymentPackages");
        }
    }
}

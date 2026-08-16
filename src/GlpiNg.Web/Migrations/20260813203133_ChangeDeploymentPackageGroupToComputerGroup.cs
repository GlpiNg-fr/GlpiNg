using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GlpiNg.Web.Migrations
{
    /// <inheritdoc />
    public partial class ChangeDeploymentPackageGroupToComputerGroup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DeploymentPackages_Groups_GroupId",
                table: "DeploymentPackages");

            migrationBuilder.AddForeignKey(
                name: "FK_DeploymentPackages_DeployComputerGroups_GroupId",
                table: "DeploymentPackages",
                column: "GroupId",
                principalTable: "DeployComputerGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DeploymentPackages_DeployComputerGroups_GroupId",
                table: "DeploymentPackages");

            migrationBuilder.AddForeignKey(
                name: "FK_DeploymentPackages_Groups_GroupId",
                table: "DeploymentPackages",
                column: "GroupId",
                principalTable: "Groups",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}

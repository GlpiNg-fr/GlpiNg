using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GlpiNg.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddDeploymentPackageSupersededBy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SupersededByPackageId",
                table: "DeploymentPackages",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentPackages_SupersededByPackageId",
                table: "DeploymentPackages",
                column: "SupersededByPackageId");

            migrationBuilder.AddForeignKey(
                name: "FK_DeploymentPackages_DeploymentPackages_SupersededByPackageId",
                table: "DeploymentPackages",
                column: "SupersededByPackageId",
                principalTable: "DeploymentPackages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DeploymentPackages_DeploymentPackages_SupersededByPackageId",
                table: "DeploymentPackages");

            migrationBuilder.DropIndex(
                name: "IX_DeploymentPackages_SupersededByPackageId",
                table: "DeploymentPackages");

            migrationBuilder.DropColumn(
                name: "SupersededByPackageId",
                table: "DeploymentPackages");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GlpiNg.Web.Migrations
{
    /// <inheritdoc />
    public partial class AllowDuplicateDeploymentFileParts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DeploymentPackageFileParts_Sha512",
                table: "DeploymentPackageFileParts");

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentPackageFileParts_Sha512",
                table: "DeploymentPackageFileParts",
                column: "Sha512");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DeploymentPackageFileParts_Sha512",
                table: "DeploymentPackageFileParts");

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentPackageFileParts_Sha512",
                table: "DeploymentPackageFileParts",
                column: "Sha512",
                unique: true);
        }
    }
}

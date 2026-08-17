using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GlpiNg.Web.Migrations
{
    /// <inheritdoc />
    public partial class UpdateDeploymentTaskFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DeploymentTasks_DeploymentPackages_PackageId",
                table: "DeploymentTasks");

            migrationBuilder.AlterColumn<int>(
                name: "PackageId",
                table: "DeploymentTasks",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<bool>(
                name: "AllowRePreparation",
                table: "DeploymentTasks",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddForeignKey(
                name: "FK_DeploymentTasks_DeploymentPackages_PackageId",
                table: "DeploymentTasks",
                column: "PackageId",
                principalTable: "DeploymentPackages",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DeploymentTasks_DeploymentPackages_PackageId",
                table: "DeploymentTasks");

            migrationBuilder.DropColumn(
                name: "AllowRePreparation",
                table: "DeploymentTasks");

            migrationBuilder.AlterColumn<int>(
                name: "PackageId",
                table: "DeploymentTasks",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_DeploymentTasks_DeploymentPackages_PackageId",
                table: "DeploymentTasks",
                column: "PackageId",
                principalTable: "DeploymentPackages",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}

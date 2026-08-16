using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GlpiNg.Web.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceDeploymentPackageTargetsWithSingleGroup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeploymentPackageTargets");

            migrationBuilder.AddColumn<int>(
                name: "DeployComputerGroupId",
                table: "DeploymentPackages",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentPackages_DeployComputerGroupId",
                table: "DeploymentPackages",
                column: "DeployComputerGroupId");

            migrationBuilder.AddForeignKey(
                name: "FK_DeploymentPackages_DeployComputerGroups_DeployComputerGroupId",
                table: "DeploymentPackages",
                column: "DeployComputerGroupId",
                principalTable: "DeployComputerGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DeploymentPackages_DeployComputerGroups_DeployComputerGroupId",
                table: "DeploymentPackages");

            migrationBuilder.DropIndex(
                name: "IX_DeploymentPackages_DeployComputerGroupId",
                table: "DeploymentPackages");

            migrationBuilder.DropColumn(
                name: "DeployComputerGroupId",
                table: "DeploymentPackages");

            migrationBuilder.CreateTable(
                name: "DeploymentPackageTargets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ComputerGroupId = table.Column<int>(type: "int", nullable: true),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    DeploymentPackageId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeploymentPackageTargets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeploymentPackageTargets_DeployComputerGroups_ComputerGroupId",
                        column: x => x.ComputerGroupId,
                        principalTable: "DeployComputerGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DeploymentPackageTargets_DeploymentPackages_DeploymentPackageId",
                        column: x => x.DeploymentPackageId,
                        principalTable: "DeploymentPackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DeploymentPackageTargets_Entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentPackageTargets_ComputerGroupId",
                table: "DeploymentPackageTargets",
                column: "ComputerGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentPackageTargets_DeploymentPackageId",
                table: "DeploymentPackageTargets",
                column: "DeploymentPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentPackageTargets_EntityId",
                table: "DeploymentPackageTargets",
                column: "EntityId");
        }
    }
}

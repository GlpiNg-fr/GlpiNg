using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GlpiNg.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddDeploymentTaskPackagesAndTargets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DeploymentTasks_DeployComputerGroups_TargetGroupId",
                table: "DeploymentTasks");

            migrationBuilder.DropForeignKey(
                name: "FK_DeploymentTasks_DeploymentPackages_PackageId",
                table: "DeploymentTasks");

            migrationBuilder.DropIndex(
                name: "IX_DeploymentTasks_PackageId",
                table: "DeploymentTasks");

            migrationBuilder.DropIndex(
                name: "IX_DeploymentTasks_TargetGroupId",
                table: "DeploymentTasks");

            migrationBuilder.DropColumn(
                name: "PackageId",
                table: "DeploymentTasks");

            migrationBuilder.DropColumn(
                name: "TargetGroupId",
                table: "DeploymentTasks");

            migrationBuilder.CreateTable(
                name: "DeploymentTaskPackages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DeploymentTaskId = table.Column<int>(type: "int", nullable: false),
                    PackageId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeploymentTaskPackages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeploymentTaskPackages_DeploymentPackages_PackageId",
                        column: x => x.PackageId,
                        principalTable: "DeploymentPackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DeploymentTaskPackages_DeploymentTasks_DeploymentTaskId",
                        column: x => x.DeploymentTaskId,
                        principalTable: "DeploymentTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DeploymentTaskTargets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DeploymentTaskId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    GroupId = table.Column<int>(type: "int", nullable: true),
                    ComputerId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeploymentTaskTargets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeploymentTaskTargets_Computers_ComputerId",
                        column: x => x.ComputerId,
                        principalTable: "Computers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DeploymentTaskTargets_DeployComputerGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "DeployComputerGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DeploymentTaskTargets_DeploymentTasks_DeploymentTaskId",
                        column: x => x.DeploymentTaskId,
                        principalTable: "DeploymentTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentTaskPackages_DeploymentTaskId_PackageId",
                table: "DeploymentTaskPackages",
                columns: new[] { "DeploymentTaskId", "PackageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentTaskPackages_PackageId",
                table: "DeploymentTaskPackages",
                column: "PackageId");

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentTaskTargets_ComputerId",
                table: "DeploymentTaskTargets",
                column: "ComputerId");

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentTaskTargets_DeploymentTaskId",
                table: "DeploymentTaskTargets",
                column: "DeploymentTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentTaskTargets_GroupId",
                table: "DeploymentTaskTargets",
                column: "GroupId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeploymentTaskPackages");

            migrationBuilder.DropTable(
                name: "DeploymentTaskTargets");

            migrationBuilder.AddColumn<int>(
                name: "PackageId",
                table: "DeploymentTasks",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TargetGroupId",
                table: "DeploymentTasks",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentTasks_PackageId",
                table: "DeploymentTasks",
                column: "PackageId");

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentTasks_TargetGroupId",
                table: "DeploymentTasks",
                column: "TargetGroupId");

            migrationBuilder.AddForeignKey(
                name: "FK_DeploymentTasks_DeployComputerGroups_TargetGroupId",
                table: "DeploymentTasks",
                column: "TargetGroupId",
                principalTable: "DeployComputerGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_DeploymentTasks_DeploymentPackages_PackageId",
                table: "DeploymentTasks",
                column: "PackageId",
                principalTable: "DeploymentPackages",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}

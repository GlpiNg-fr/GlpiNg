using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GlpiNg.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddDeploymentTasks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TaskId",
                table: "DeploymentJobs",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DeploymentTasks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    PackageId = table.Column<int>(type: "int", nullable: false),
                    TargetGroupId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastLaunchedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeploymentTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeploymentTasks_DeployComputerGroups_TargetGroupId",
                        column: x => x.TargetGroupId,
                        principalTable: "DeployComputerGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DeploymentTasks_DeploymentPackages_PackageId",
                        column: x => x.PackageId,
                        principalTable: "DeploymentPackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentJobs_TaskId",
                table: "DeploymentJobs",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentTasks_PackageId",
                table: "DeploymentTasks",
                column: "PackageId");

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentTasks_TargetGroupId",
                table: "DeploymentTasks",
                column: "TargetGroupId");

            migrationBuilder.AddForeignKey(
                name: "FK_DeploymentJobs_DeploymentTasks_TaskId",
                table: "DeploymentJobs",
                column: "TaskId",
                principalTable: "DeploymentTasks",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DeploymentJobs_DeploymentTasks_TaskId",
                table: "DeploymentJobs");

            migrationBuilder.DropTable(
                name: "DeploymentTasks");

            migrationBuilder.DropIndex(
                name: "IX_DeploymentJobs_TaskId",
                table: "DeploymentJobs");

            migrationBuilder.DropColumn(
                name: "TaskId",
                table: "DeploymentJobs");
        }
    }
}

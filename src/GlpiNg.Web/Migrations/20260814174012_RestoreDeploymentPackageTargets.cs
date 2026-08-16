using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GlpiNg.Web.Migrations
{
    /// <inheritdoc />
    public partial class RestoreDeploymentPackageTargets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DeploymentPackageTargets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DeploymentPackageId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    ItemId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeploymentPackageTargets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeploymentPackageTargets_DeploymentPackages_DeploymentPackageId",
                        column: x => x.DeploymentPackageId,
                        principalTable: "DeploymentPackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentPackageTargets_DeploymentPackageId",
                table: "DeploymentPackageTargets",
                column: "DeploymentPackageId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeploymentPackageTargets");
        }
    }
}

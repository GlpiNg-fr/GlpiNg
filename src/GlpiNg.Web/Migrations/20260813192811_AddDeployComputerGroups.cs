using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GlpiNg.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddDeployComputerGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DeployComputerGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Type = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeployComputerGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeployComputerGroupMembers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DeployComputerGroupId = table.Column<int>(type: "int", nullable: false),
                    ComputerId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeployComputerGroupMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeployComputerGroupMembers_Computers_ComputerId",
                        column: x => x.ComputerId,
                        principalTable: "Computers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DeployComputerGroupMembers_DeployComputerGroups_DeployComputerGroupId",
                        column: x => x.DeployComputerGroupId,
                        principalTable: "DeployComputerGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeployComputerGroupMembers_ComputerId",
                table: "DeployComputerGroupMembers",
                column: "ComputerId");

            migrationBuilder.CreateIndex(
                name: "IX_DeployComputerGroupMembers_DeployComputerGroupId_ComputerId",
                table: "DeployComputerGroupMembers",
                columns: new[] { "DeployComputerGroupId", "ComputerId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeployComputerGroupMembers");

            migrationBuilder.DropTable(
                name: "DeployComputerGroups");
        }
    }
}

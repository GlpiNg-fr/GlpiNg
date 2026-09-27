using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GlpiNg.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddDeploymentRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DeploymentRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeploymentRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeploymentRuleAction",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DeploymentRuleId = table.Column<int>(type: "int", nullable: false),
                    PackageId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeploymentRuleAction", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeploymentRuleAction_DeploymentPackages_PackageId",
                        column: x => x.PackageId,
                        principalTable: "DeploymentPackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DeploymentRuleAction_DeploymentRules_DeploymentRuleId",
                        column: x => x.DeploymentRuleId,
                        principalTable: "DeploymentRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DeploymentRuleCriterion",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DeploymentRuleId = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Link = table.Column<int>(type: "int", nullable: false),
                    Field = table.Column<int>(type: "int", nullable: false),
                    Operator = table.Column<int>(type: "int", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeploymentRuleCriterion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeploymentRuleCriterion_DeploymentRules_DeploymentRuleId",
                        column: x => x.DeploymentRuleId,
                        principalTable: "DeploymentRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentRuleAction_DeploymentRuleId",
                table: "DeploymentRuleAction",
                column: "DeploymentRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentRuleAction_PackageId",
                table: "DeploymentRuleAction",
                column: "PackageId");

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentRuleCriterion_DeploymentRuleId",
                table: "DeploymentRuleCriterion",
                column: "DeploymentRuleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeploymentRuleAction");

            migrationBuilder.DropTable(
                name: "DeploymentRuleCriterion");

            migrationBuilder.DropTable(
                name: "DeploymentRules");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GlpiNg.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddComputerRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ComputerRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    LogicalOperator = table.Column<int>(type: "int", nullable: false),
                    AppliesTo = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComputerRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ComputerRuleAction",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ComputerRuleId = table.Column<int>(type: "int", nullable: false),
                    Field = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ActionType = table.Column<int>(type: "int", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComputerRuleAction", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComputerRuleAction_ComputerRules_ComputerRuleId",
                        column: x => x.ComputerRuleId,
                        principalTable: "ComputerRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ComputerRuleCriterion",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ComputerRuleId = table.Column<int>(type: "int", nullable: false),
                    Field = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Operator = table.Column<int>(type: "int", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComputerRuleCriterion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComputerRuleCriterion_ComputerRules_ComputerRuleId",
                        column: x => x.ComputerRuleId,
                        principalTable: "ComputerRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ComputerRuleAction_ComputerRuleId",
                table: "ComputerRuleAction",
                column: "ComputerRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_ComputerRuleCriterion_ComputerRuleId",
                table: "ComputerRuleCriterion",
                column: "ComputerRuleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ComputerRuleAction");

            migrationBuilder.DropTable(
                name: "ComputerRuleCriterion");

            migrationBuilder.DropTable(
                name: "ComputerRules");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GlpiNg.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddImportAssignmentRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ImportAssignmentRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    LogicalOperator = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportAssignmentRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ImportBlacklistEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportBlacklistEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RefusedImportLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RuleName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ComputerName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SerialNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Domain = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Tag = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AgentIdentifier = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefusedImportLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ImportAssignmentRuleAction",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ImportAssignmentRuleId = table.Column<int>(type: "int", nullable: false),
                    ActionType = table.Column<int>(type: "int", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportAssignmentRuleAction", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImportAssignmentRuleAction_ImportAssignmentRules_ImportAssignmentRuleId",
                        column: x => x.ImportAssignmentRuleId,
                        principalTable: "ImportAssignmentRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ImportAssignmentRuleCriterion",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ImportAssignmentRuleId = table.Column<int>(type: "int", nullable: false),
                    Field = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Operator = table.Column<int>(type: "int", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportAssignmentRuleCriterion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImportAssignmentRuleCriterion_ImportAssignmentRules_ImportAssignmentRuleId",
                        column: x => x.ImportAssignmentRuleId,
                        principalTable: "ImportAssignmentRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ImportAssignmentRuleAction_ImportAssignmentRuleId",
                table: "ImportAssignmentRuleAction",
                column: "ImportAssignmentRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_ImportAssignmentRuleCriterion_ImportAssignmentRuleId",
                table: "ImportAssignmentRuleCriterion",
                column: "ImportAssignmentRuleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ImportAssignmentRuleAction");

            migrationBuilder.DropTable(
                name: "ImportAssignmentRuleCriterion");

            migrationBuilder.DropTable(
                name: "ImportBlacklistEntries");

            migrationBuilder.DropTable(
                name: "RefusedImportLogs");

            migrationBuilder.DropTable(
                name: "ImportAssignmentRules");
        }
    }
}

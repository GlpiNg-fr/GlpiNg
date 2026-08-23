using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GlpiNg.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddAutomaticActions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AutomaticActionRunLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TaskKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RanAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Success = table.Column<bool>(type: "bit", nullable: false),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
                    Error = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutomaticActionRunLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AutomaticActionStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TaskKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    FrequencyMinutes = table.Column<int>(type: "int", nullable: true),
                    LastRunAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastRunSuccess = table.Column<bool>(type: "bit", nullable: true),
                    LastRunDurationMs = table.Column<long>(type: "bigint", nullable: true),
                    LastRunError = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutomaticActionStates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AutomaticActionRunLogs_TaskKey_RanAt",
                table: "AutomaticActionRunLogs",
                columns: new[] { "TaskKey", "RanAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AutomaticActionStates_TaskKey",
                table: "AutomaticActionStates",
                column: "TaskKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AutomaticActionRunLogs");

            migrationBuilder.DropTable(
                name: "AutomaticActionStates");
        }
    }
}

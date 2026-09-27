using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GlpiNg.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddNetworkTasks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NetworkTasks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Method = table.Column<int>(type: "int", nullable: false),
                    ScheduledStartTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ScheduledEndTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExecutionTimeSlotId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastLaunchedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NetworkTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NetworkTasks_TimeSlots_ExecutionTimeSlotId",
                        column: x => x.ExecutionTimeSlotId,
                        principalTable: "TimeSlots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NetworkTaskActors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NetworkTaskId = table.Column<int>(type: "int", nullable: false),
                    AgentId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NetworkTaskActors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NetworkTaskActors_Agents_AgentId",
                        column: x => x.AgentId,
                        principalTable: "Agents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NetworkTaskActors_NetworkTasks_NetworkTaskId",
                        column: x => x.NetworkTaskId,
                        principalTable: "NetworkTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NetworkTaskCredentials",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NetworkTaskId = table.Column<int>(type: "int", nullable: false),
                    SnmpCredentialId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NetworkTaskCredentials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NetworkTaskCredentials_NetworkTasks_NetworkTaskId",
                        column: x => x.NetworkTaskId,
                        principalTable: "NetworkTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NetworkTaskCredentials_SnmpCredentials_SnmpCredentialId",
                        column: x => x.SnmpCredentialId,
                        principalTable: "SnmpCredentials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NetworkTaskIpRanges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NetworkTaskId = table.Column<int>(type: "int", nullable: false),
                    IpRangeId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NetworkTaskIpRanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NetworkTaskIpRanges_IpRanges_IpRangeId",
                        column: x => x.IpRangeId,
                        principalTable: "IpRanges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NetworkTaskIpRanges_NetworkTasks_NetworkTaskId",
                        column: x => x.NetworkTaskId,
                        principalTable: "NetworkTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NetworkTaskJobs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AgentId = table.Column<int>(type: "int", nullable: false),
                    NetworkTaskId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Log = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NetworkTaskJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NetworkTaskJobs_Agents_AgentId",
                        column: x => x.AgentId,
                        principalTable: "Agents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NetworkTaskJobs_NetworkTasks_NetworkTaskId",
                        column: x => x.NetworkTaskId,
                        principalTable: "NetworkTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NetworkTaskActors_AgentId",
                table: "NetworkTaskActors",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkTaskActors_NetworkTaskId_AgentId",
                table: "NetworkTaskActors",
                columns: new[] { "NetworkTaskId", "AgentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NetworkTaskCredentials_NetworkTaskId_SnmpCredentialId",
                table: "NetworkTaskCredentials",
                columns: new[] { "NetworkTaskId", "SnmpCredentialId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NetworkTaskCredentials_SnmpCredentialId",
                table: "NetworkTaskCredentials",
                column: "SnmpCredentialId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkTaskIpRanges_IpRangeId",
                table: "NetworkTaskIpRanges",
                column: "IpRangeId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkTaskIpRanges_NetworkTaskId_IpRangeId",
                table: "NetworkTaskIpRanges",
                columns: new[] { "NetworkTaskId", "IpRangeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NetworkTaskJobs_AgentId",
                table: "NetworkTaskJobs",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkTaskJobs_NetworkTaskId",
                table: "NetworkTaskJobs",
                column: "NetworkTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkTasks_ExecutionTimeSlotId",
                table: "NetworkTasks",
                column: "ExecutionTimeSlotId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NetworkTaskActors");

            migrationBuilder.DropTable(
                name: "NetworkTaskCredentials");

            migrationBuilder.DropTable(
                name: "NetworkTaskIpRanges");

            migrationBuilder.DropTable(
                name: "NetworkTaskJobs");

            migrationBuilder.DropTable(
                name: "NetworkTasks");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GlpiNg.Web.Migrations
{
    /// <summary>Nom trompeur : générée pendant qu'une autre session travaillait en parallèle sur
    /// un import "Administration GLPI" (SourceGlpiId sur Users/Profiles/Groups/Entities) sans
    /// encore de migration dédiée — <c>dotnet ef migrations add</c> capture tout le diff de modèle
    /// en attente, pas seulement WakeOnLan. Les colonnes/index SourceGlpiId ci-dessous appartiennent
    /// à cette autre fonctionnalité, pas à WakeOnLanTask ; ne pas les retirer à la main (le snapshot
    /// du modèle et le Up() doivent rester cohérents) — voir le commentaire de tête de
    /// <see cref="GlpiNg.Modules.Deployment.Models.WakeOnLanTask"/> pour la partie qui concerne
    /// réellement cette migration.</summary>
    /// <inheritdoc />
    public partial class AddWakeOnLanTasks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SourceGlpiId",
                table: "Users",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceGlpiId",
                table: "Profiles",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceGlpiId",
                table: "Groups",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceGlpiId",
                table: "Entities",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WakeOnLanTasks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    ScheduledStartTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ScheduledEndTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExecutionTimeSlotId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastLaunchedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WakeOnLanTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WakeOnLanTasks_TimeSlots_ExecutionTimeSlotId",
                        column: x => x.ExecutionTimeSlotId,
                        principalTable: "TimeSlots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WakeOnLanTaskActors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WakeOnLanTaskId = table.Column<int>(type: "int", nullable: false),
                    AgentId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WakeOnLanTaskActors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WakeOnLanTaskActors_Agents_AgentId",
                        column: x => x.AgentId,
                        principalTable: "Agents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WakeOnLanTaskActors_WakeOnLanTasks_WakeOnLanTaskId",
                        column: x => x.WakeOnLanTaskId,
                        principalTable: "WakeOnLanTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WakeOnLanTaskJobs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AgentId = table.Column<int>(type: "int", nullable: false),
                    WakeOnLanTaskId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TargetMacsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Log = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WakeOnLanTaskJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WakeOnLanTaskJobs_Agents_AgentId",
                        column: x => x.AgentId,
                        principalTable: "Agents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WakeOnLanTaskJobs_WakeOnLanTasks_WakeOnLanTaskId",
                        column: x => x.WakeOnLanTaskId,
                        principalTable: "WakeOnLanTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WakeOnLanTaskTargets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WakeOnLanTaskId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    GroupId = table.Column<int>(type: "int", nullable: true),
                    ComputerId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WakeOnLanTaskTargets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WakeOnLanTaskTargets_Computers_ComputerId",
                        column: x => x.ComputerId,
                        principalTable: "Computers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WakeOnLanTaskTargets_DeployComputerGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "DeployComputerGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WakeOnLanTaskTargets_WakeOnLanTasks_WakeOnLanTaskId",
                        column: x => x.WakeOnLanTaskId,
                        principalTable: "WakeOnLanTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_SourceGlpiId",
                table: "Users",
                column: "SourceGlpiId",
                unique: true,
                filter: "\"SourceGlpiId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Profiles_SourceGlpiId",
                table: "Profiles",
                column: "SourceGlpiId",
                unique: true,
                filter: "\"SourceGlpiId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Groups_SourceGlpiId",
                table: "Groups",
                column: "SourceGlpiId",
                unique: true,
                filter: "\"SourceGlpiId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Entities_SourceGlpiId",
                table: "Entities",
                column: "SourceGlpiId",
                unique: true,
                filter: "\"SourceGlpiId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_WakeOnLanTaskActors_AgentId",
                table: "WakeOnLanTaskActors",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_WakeOnLanTaskActors_WakeOnLanTaskId_AgentId",
                table: "WakeOnLanTaskActors",
                columns: new[] { "WakeOnLanTaskId", "AgentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WakeOnLanTaskJobs_AgentId",
                table: "WakeOnLanTaskJobs",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_WakeOnLanTaskJobs_WakeOnLanTaskId",
                table: "WakeOnLanTaskJobs",
                column: "WakeOnLanTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_WakeOnLanTasks_ExecutionTimeSlotId",
                table: "WakeOnLanTasks",
                column: "ExecutionTimeSlotId");

            migrationBuilder.CreateIndex(
                name: "IX_WakeOnLanTaskTargets_ComputerId",
                table: "WakeOnLanTaskTargets",
                column: "ComputerId");

            migrationBuilder.CreateIndex(
                name: "IX_WakeOnLanTaskTargets_GroupId",
                table: "WakeOnLanTaskTargets",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_WakeOnLanTaskTargets_WakeOnLanTaskId",
                table: "WakeOnLanTaskTargets",
                column: "WakeOnLanTaskId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WakeOnLanTaskActors");

            migrationBuilder.DropTable(
                name: "WakeOnLanTaskJobs");

            migrationBuilder.DropTable(
                name: "WakeOnLanTaskTargets");

            migrationBuilder.DropTable(
                name: "WakeOnLanTasks");

            migrationBuilder.DropIndex(
                name: "IX_Users_SourceGlpiId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Profiles_SourceGlpiId",
                table: "Profiles");

            migrationBuilder.DropIndex(
                name: "IX_Groups_SourceGlpiId",
                table: "Groups");

            migrationBuilder.DropIndex(
                name: "IX_Entities_SourceGlpiId",
                table: "Entities");

            migrationBuilder.DropColumn(
                name: "SourceGlpiId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SourceGlpiId",
                table: "Profiles");

            migrationBuilder.DropColumn(
                name: "SourceGlpiId",
                table: "Groups");

            migrationBuilder.DropColumn(
                name: "SourceGlpiId",
                table: "Entities");
        }
    }
}

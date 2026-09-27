using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GlpiNg.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddDeploymentTaskSchedulingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AgentWakeUpCount",
                table: "DeploymentTasks",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "AgentWakeUpIntervalMinutes",
                table: "DeploymentTasks",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ExecutionTimeSlotId",
                table: "DeploymentTasks",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PreparationTimeSlotId",
                table: "DeploymentTasks",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ScheduledEndTime",
                table: "DeploymentTasks",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ScheduledStartTime",
                table: "DeploymentTasks",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentTasks_ExecutionTimeSlotId",
                table: "DeploymentTasks",
                column: "ExecutionTimeSlotId");

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentTasks_PreparationTimeSlotId",
                table: "DeploymentTasks",
                column: "PreparationTimeSlotId");

            migrationBuilder.AddForeignKey(
                name: "FK_DeploymentTasks_TimeSlots_ExecutionTimeSlotId",
                table: "DeploymentTasks",
                column: "ExecutionTimeSlotId",
                principalTable: "TimeSlots",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DeploymentTasks_TimeSlots_PreparationTimeSlotId",
                table: "DeploymentTasks",
                column: "PreparationTimeSlotId",
                principalTable: "TimeSlots",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DeploymentTasks_TimeSlots_ExecutionTimeSlotId",
                table: "DeploymentTasks");

            migrationBuilder.DropForeignKey(
                name: "FK_DeploymentTasks_TimeSlots_PreparationTimeSlotId",
                table: "DeploymentTasks");

            migrationBuilder.DropIndex(
                name: "IX_DeploymentTasks_ExecutionTimeSlotId",
                table: "DeploymentTasks");

            migrationBuilder.DropIndex(
                name: "IX_DeploymentTasks_PreparationTimeSlotId",
                table: "DeploymentTasks");

            migrationBuilder.DropColumn(
                name: "AgentWakeUpCount",
                table: "DeploymentTasks");

            migrationBuilder.DropColumn(
                name: "AgentWakeUpIntervalMinutes",
                table: "DeploymentTasks");

            migrationBuilder.DropColumn(
                name: "ExecutionTimeSlotId",
                table: "DeploymentTasks");

            migrationBuilder.DropColumn(
                name: "PreparationTimeSlotId",
                table: "DeploymentTasks");

            migrationBuilder.DropColumn(
                name: "ScheduledEndTime",
                table: "DeploymentTasks");

            migrationBuilder.DropColumn(
                name: "ScheduledStartTime",
                table: "DeploymentTasks");
        }
    }
}

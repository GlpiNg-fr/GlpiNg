using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GlpiNg.Web.Migrations
{
    /// <inheritdoc />
    public partial class SuiviDeploiementAutomatiqueAgent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AgentDeployAttempts",
                table: "DiscoveredNetworkDevices",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "AgentDeployLastAttemptAt",
                table: "DiscoveredNetworkDevices",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AgentDeployLastError",
                table: "DiscoveredNetworkDevices",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AgentDeployAttempts",
                table: "DiscoveredNetworkDevices");

            migrationBuilder.DropColumn(
                name: "AgentDeployLastAttemptAt",
                table: "DiscoveredNetworkDevices");

            migrationBuilder.DropColumn(
                name: "AgentDeployLastError",
                table: "DiscoveredNetworkDevices");
        }
    }
}

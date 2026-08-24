using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GlpiNg.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddDiscoveredNetworkDevices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DiscoveredNetworkDevices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IpAddress = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MacAddress = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Hostname = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SysDescr = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SysName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SysContact = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SysLocation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GuessedType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DiscoveredViaNetworkTaskId = table.Column<int>(type: "int", nullable: true),
                    FirstSeenAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PromotedNetworkEquipmentId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscoveredNetworkDevices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiscoveredNetworkDevices_NetworkEquipments_PromotedNetworkEquipmentId",
                        column: x => x.PromotedNetworkEquipmentId,
                        principalTable: "NetworkEquipments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DiscoveredNetworkDevices_NetworkTasks_DiscoveredViaNetworkTaskId",
                        column: x => x.DiscoveredViaNetworkTaskId,
                        principalTable: "NetworkTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DiscoveredNetworkDevices_DiscoveredViaNetworkTaskId",
                table: "DiscoveredNetworkDevices",
                column: "DiscoveredViaNetworkTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_DiscoveredNetworkDevices_PromotedNetworkEquipmentId",
                table: "DiscoveredNetworkDevices",
                column: "PromotedNetworkEquipmentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DiscoveredNetworkDevices");
        }
    }
}

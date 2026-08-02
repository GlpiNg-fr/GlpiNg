using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GlpiNg.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddNetworkPortsAndHardwareExtras : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LastLoggedUser",
                table: "Computers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OsKernelVersion",
                table: "Computers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VmSystem",
                table: "Computers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ComputerNetworkPorts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ComputerId = table.Column<int>(type: "int", nullable: false),
                    Designation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MacAddress = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Manufacturer = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IpMask = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IpGateway = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IpSubnet = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IpDhcp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Mtu = table.Column<int>(type: "int", nullable: true),
                    SpeedMbps = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsVirtual = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComputerNetworkPorts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComputerNetworkPorts_Computers_ComputerId",
                        column: x => x.ComputerId,
                        principalTable: "Computers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ComputerNetworkPorts_ComputerId",
                table: "ComputerNetworkPorts",
                column: "ComputerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ComputerNetworkPorts");

            migrationBuilder.DropColumn(
                name: "LastLoggedUser",
                table: "Computers");

            migrationBuilder.DropColumn(
                name: "OsKernelVersion",
                table: "Computers");

            migrationBuilder.DropColumn(
                name: "VmSystem",
                table: "Computers");
        }
    }
}

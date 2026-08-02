using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GlpiNg.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddComputerVolumes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ComputerVolumes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ComputerId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Partition = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MountPoint = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FileSystem = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TotalSizeMb = table.Column<long>(type: "bigint", nullable: true),
                    FreeSizeMb = table.Column<long>(type: "bigint", nullable: true),
                    Encryption = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComputerVolumes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComputerVolumes_Computers_ComputerId",
                        column: x => x.ComputerId,
                        principalTable: "Computers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ComputerVolumes_ComputerId",
                table: "ComputerVolumes",
                column: "ComputerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ComputerVolumes");
        }
    }
}

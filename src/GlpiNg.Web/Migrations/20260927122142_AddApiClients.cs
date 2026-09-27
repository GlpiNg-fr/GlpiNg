using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GlpiNg.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddApiClients : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ApiTokenDate",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApiTokenHash",
                table: "Users",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ApiClients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Ipv4RangeStart = table.Column<long>(type: "bigint", nullable: true),
                    Ipv4RangeEnd = table.Column<long>(type: "bigint", nullable: true),
                    Ipv6 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AppTokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    AppTokenDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LogMethod = table.Column<int>(type: "int", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiClients", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_ApiTokenHash",
                table: "Users",
                column: "ApiTokenHash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApiClients");

            migrationBuilder.DropIndex(
                name: "IX_Users_ApiTokenHash",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ApiTokenDate",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ApiTokenHash",
                table: "Users");
        }
    }
}

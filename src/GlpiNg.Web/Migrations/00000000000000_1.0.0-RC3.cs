using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GlpiNg.Web.Migrations
{
    /// <inheritdoc />
    public partial class Release_1_0_0_RC3 : Migration
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

            migrationBuilder.CreateTable(
                name: "MailCollectors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Host = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Port = table.Column<int>(type: "int", nullable: true),
                    Encryption = table.Column<int>(type: "int", nullable: false),
                    ValidateCertificate = table.Column<bool>(type: "bit", nullable: false),
                    Login = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PasswordProtected = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Folder = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MoveToFolder = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    MaxFetchPerRun = table.Column<int>(type: "int", nullable: false),
                    ImportAttachments = table.Column<bool>(type: "bit", nullable: false),
                    LastRunAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastImportedCount = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MailCollectors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MailCollectors_Entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ReservableItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    IsRecursive = table.Column<bool>(type: "bit", nullable: false),
                    ItemType = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ItemId = table.Column<int>(type: "int", nullable: false),
                    ItemName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ItemUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReservableItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReservableItems_Entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Reservations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReservableItemId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Begin = table.Column<DateTime>(type: "datetime2", nullable: false),
                    End = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SeriesId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AlertSentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reservations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Reservations_ReservableItems_ReservableItemId",
                        column: x => x.ReservableItemId,
                        principalTable: "ReservableItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MailCollectors_EntityId",
                table: "MailCollectors",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_MailCollectors_Name",
                table: "MailCollectors",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReservableItems_EntityId",
                table: "ReservableItems",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_ReservableItems_ItemType_ItemId",
                table: "ReservableItems",
                columns: new[] { "ItemType", "ItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_Begin",
                table: "Reservations",
                column: "Begin");

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_ReservableItemId_Begin",
                table: "Reservations",
                columns: new[] { "ReservableItemId", "Begin" });

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_UserId",
                table: "Reservations",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MailCollectors");

            migrationBuilder.DropTable(
                name: "Reservations");

            migrationBuilder.DropTable(
                name: "ReservableItems");

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

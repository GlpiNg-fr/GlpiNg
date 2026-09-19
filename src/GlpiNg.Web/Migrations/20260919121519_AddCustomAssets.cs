using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GlpiNg.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomAssets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CustomAssetDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SystemName = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LabelSingular = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LabelPlural = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Icon = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DocumentsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    NotesEnabled = table.Column<bool>(type: "bit", nullable: false),
                    HistoryEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomAssetDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CustomAssetFields",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomAssetDefinitionId = table.Column<int>(type: "int", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    DropdownType = table.Column<int>(type: "int", nullable: true),
                    IsMandatory = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomAssetFields", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomAssetFields_CustomAssetDefinitions_CustomAssetDefinitionId",
                        column: x => x.CustomAssetDefinitionId,
                        principalTable: "CustomAssetDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CustomAssets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    IsRecursive = table.Column<bool>(type: "bit", nullable: false),
                    CustomAssetDefinitionId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomAssets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomAssets_CustomAssetDefinitions_CustomAssetDefinitionId",
                        column: x => x.CustomAssetDefinitionId,
                        principalTable: "CustomAssetDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CustomAssets_Entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CustomAssetHistoryEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomAssetId = table.Column<int>(type: "int", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    User = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Field = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomAssetHistoryEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomAssetHistoryEntries_CustomAssets_CustomAssetId",
                        column: x => x.CustomAssetId,
                        principalTable: "CustomAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CustomAssetValues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomAssetId = table.Column<int>(type: "int", nullable: false),
                    CustomAssetFieldId = table.Column<int>(type: "int", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomAssetValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomAssetValues_CustomAssetFields_CustomAssetFieldId",
                        column: x => x.CustomAssetFieldId,
                        principalTable: "CustomAssetFields",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CustomAssetValues_CustomAssets_CustomAssetId",
                        column: x => x.CustomAssetId,
                        principalTable: "CustomAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomAssetDefinitions_SystemName",
                table: "CustomAssetDefinitions",
                column: "SystemName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomAssetFields_CustomAssetDefinitionId",
                table: "CustomAssetFields",
                column: "CustomAssetDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomAssetHistoryEntries_CustomAssetId_OccurredAt",
                table: "CustomAssetHistoryEntries",
                columns: new[] { "CustomAssetId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomAssets_CustomAssetDefinitionId",
                table: "CustomAssets",
                column: "CustomAssetDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomAssets_EntityId",
                table: "CustomAssets",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomAssetValues_CustomAssetFieldId",
                table: "CustomAssetValues",
                column: "CustomAssetFieldId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomAssetValues_CustomAssetId_CustomAssetFieldId",
                table: "CustomAssetValues",
                columns: new[] { "CustomAssetId", "CustomAssetFieldId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomAssetHistoryEntries");

            migrationBuilder.DropTable(
                name: "CustomAssetValues");

            migrationBuilder.DropTable(
                name: "CustomAssetFields");

            migrationBuilder.DropTable(
                name: "CustomAssets");

            migrationBuilder.DropTable(
                name: "CustomAssetDefinitions");
        }
    }
}

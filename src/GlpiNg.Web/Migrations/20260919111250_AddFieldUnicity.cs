using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GlpiNg.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddFieldUnicity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FieldUnicityCriteria",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    IsRecursive = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ItemType = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    RefuseCreation = table.Column<bool>(type: "bit", nullable: false),
                    NotifyOnDuplicate = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FieldUnicityCriteria", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FieldUnicityCriteria_Entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FieldUnicityFields",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FieldUnicityCriterionId = table.Column<int>(type: "int", nullable: false),
                    FieldName = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FieldUnicityFields", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FieldUnicityFields_FieldUnicityCriteria_FieldUnicityCriterionId",
                        column: x => x.FieldUnicityCriterionId,
                        principalTable: "FieldUnicityCriteria",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FieldUnicityCriteria_EntityId",
                table: "FieldUnicityCriteria",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_FieldUnicityCriteria_ItemType_IsActive",
                table: "FieldUnicityCriteria",
                columns: new[] { "ItemType", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_FieldUnicityFields_FieldUnicityCriterionId_FieldName",
                table: "FieldUnicityFields",
                columns: new[] { "FieldUnicityCriterionId", "FieldName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FieldUnicityFields");

            migrationBuilder.DropTable(
                name: "FieldUnicityCriteria");
        }
    }
}

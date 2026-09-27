using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GlpiNg.Web.Migrations
{
    /// <inheritdoc />
    public partial class AjouteNiveauxAccesEtHistoriqueBaseDeConnaissances : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_KnowledgeBaseArticleTargets_ArticleId_Type_ItemId",
                table: "KnowledgeBaseArticleTargets");

            migrationBuilder.AddColumn<bool>(
                name: "IsRecursive",
                table: "KnowledgeBaseArticleTargets",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ScopeEntityId",
                table: "KnowledgeBaseArticleTargets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VisibleFrom",
                table: "KnowledgeBaseArticles",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VisibleUntil",
                table: "KnowledgeBaseArticles",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "KnowledgeBaseArticleHistoryEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ArticleId = table.Column<int>(type: "int", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    User = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Field = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnowledgeBaseArticleHistoryEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KnowledgeBaseArticleHistoryEntries_KnowledgeBaseArticles_ArticleId",
                        column: x => x.ArticleId,
                        principalTable: "KnowledgeBaseArticles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeBaseArticleTargets_ArticleId_Type_ItemId_ScopeEntityId",
                table: "KnowledgeBaseArticleTargets",
                columns: new[] { "ArticleId", "Type", "ItemId", "ScopeEntityId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeBaseArticleHistoryEntries_ArticleId_OccurredAt",
                table: "KnowledgeBaseArticleHistoryEntries",
                columns: new[] { "ArticleId", "OccurredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KnowledgeBaseArticleHistoryEntries");

            migrationBuilder.DropIndex(
                name: "IX_KnowledgeBaseArticleTargets_ArticleId_Type_ItemId_ScopeEntityId",
                table: "KnowledgeBaseArticleTargets");

            migrationBuilder.DropColumn(
                name: "IsRecursive",
                table: "KnowledgeBaseArticleTargets");

            migrationBuilder.DropColumn(
                name: "ScopeEntityId",
                table: "KnowledgeBaseArticleTargets");

            migrationBuilder.DropColumn(
                name: "VisibleFrom",
                table: "KnowledgeBaseArticles");

            migrationBuilder.DropColumn(
                name: "VisibleUntil",
                table: "KnowledgeBaseArticles");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeBaseArticleTargets_ArticleId_Type_ItemId",
                table: "KnowledgeBaseArticleTargets",
                columns: new[] { "ArticleId", "Type", "ItemId" },
                unique: true);
        }
    }
}

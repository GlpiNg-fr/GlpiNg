using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GlpiNg.Web.Migrations
{
    /// <inheritdoc />
    public partial class AjouteBaseDeConnaissances : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KnowledgeBaseCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    IsRecursive = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ParentId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnowledgeBaseCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KnowledgeBaseCategories_Entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KnowledgeBaseCategories_KnowledgeBaseCategories_ParentId",
                        column: x => x.ParentId,
                        principalTable: "KnowledgeBaseCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "KnowledgeBaseArticles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    IsRecursive = table.Column<bool>(type: "bit", nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CategoryId = table.Column<int>(type: "int", nullable: true),
                    IsFaq = table.Column<bool>(type: "bit", nullable: false),
                    IsPinned = table.Column<bool>(type: "bit", nullable: false),
                    ViewCount = table.Column<int>(type: "int", nullable: false),
                    AuthorUserId = table.Column<int>(type: "int", nullable: true),
                    AuthorName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastEditorName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnowledgeBaseArticles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KnowledgeBaseArticles_Entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KnowledgeBaseArticles_KnowledgeBaseCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "KnowledgeBaseCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "KnowledgeBaseArticleRevisions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ArticleId = table.Column<int>(type: "int", nullable: false),
                    Number = table.Column<int>(type: "int", nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EditorName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RevisedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnowledgeBaseArticleRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KnowledgeBaseArticleRevisions_KnowledgeBaseArticles_ArticleId",
                        column: x => x.ArticleId,
                        principalTable: "KnowledgeBaseArticles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "KnowledgeBaseArticleTargets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ArticleId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    ItemId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnowledgeBaseArticleTargets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KnowledgeBaseArticleTargets_KnowledgeBaseArticles_ArticleId",
                        column: x => x.ArticleId,
                        principalTable: "KnowledgeBaseArticles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeBaseArticleRevisions_ArticleId_Number",
                table: "KnowledgeBaseArticleRevisions",
                columns: new[] { "ArticleId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeBaseArticles_CategoryId",
                table: "KnowledgeBaseArticles",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeBaseArticles_EntityId",
                table: "KnowledgeBaseArticles",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeBaseArticleTargets_ArticleId_Type_ItemId",
                table: "KnowledgeBaseArticleTargets",
                columns: new[] { "ArticleId", "Type", "ItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeBaseCategories_EntityId",
                table: "KnowledgeBaseCategories",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeBaseCategories_ParentId",
                table: "KnowledgeBaseCategories",
                column: "ParentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KnowledgeBaseArticleRevisions");

            migrationBuilder.DropTable(
                name: "KnowledgeBaseArticleTargets");

            migrationBuilder.DropTable(
                name: "KnowledgeBaseArticles");

            migrationBuilder.DropTable(
                name: "KnowledgeBaseCategories");
        }
    }
}

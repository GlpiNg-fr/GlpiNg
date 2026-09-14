using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GlpiNg.Web.Migrations
{
    /// <inheritdoc />
    public partial class AjouteSourceGlpiIdBaseDeConnaissances : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SourceGlpiId",
                table: "KnowledgeBaseCategories",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceGlpiId",
                table: "KnowledgeBaseArticles",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SourceGlpiId",
                table: "KnowledgeBaseCategories");

            migrationBuilder.DropColumn(
                name: "SourceGlpiId",
                table: "KnowledgeBaseArticles");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GlpiNg.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddDeploymentPackageFileParts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StoragePath",
                table: "DeploymentPackageFiles");

            migrationBuilder.CreateTable(
                name: "DeploymentPackageFileParts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DeploymentPackageFileId = table.Column<int>(type: "int", nullable: false),
                    PartIndex = table.Column<int>(type: "int", nullable: false),
                    Sha512 = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    StoragePath = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeploymentPackageFileParts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeploymentPackageFileParts_DeploymentPackageFiles_DeploymentPackageFileId",
                        column: x => x.DeploymentPackageFileId,
                        principalTable: "DeploymentPackageFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentPackageFileParts_DeploymentPackageFileId",
                table: "DeploymentPackageFileParts",
                column: "DeploymentPackageFileId");

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentPackageFileParts_Sha512",
                table: "DeploymentPackageFileParts",
                column: "Sha512",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeploymentPackageFileParts");

            migrationBuilder.AddColumn<string>(
                name: "StoragePath",
                table: "DeploymentPackageFiles",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}

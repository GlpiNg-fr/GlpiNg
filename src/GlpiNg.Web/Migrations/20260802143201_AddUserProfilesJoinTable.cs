using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GlpiNg.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddUserProfilesJoinTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Un utilisateur peut avoir plusieurs habilitations (entité + profil + récursif),
            // comme dans GLPI (glpi_profiles_users) : Users.EntityId/ProfileId (une seule
            // habilitation par utilisateur) étaient une simplification excessive. La table
            // UserProfiles est créée puis peuplée à partir des habilitations existantes AVANT que
            // les colonnes Users.EntityId/ProfileId ne soient supprimées, pour ne pas perdre ces
            // données.
            migrationBuilder.CreateTable(
                name: "UserProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    EntityId = table.Column<int>(type: "int", nullable: false),
                    ProfileId = table.Column<int>(type: "int", nullable: false),
                    IsRecursive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserProfiles_Entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserProfiles_Profiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "Profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserProfiles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_EntityId",
                table: "UserProfiles",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_ProfileId",
                table: "UserProfiles",
                column: "ProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_UserId",
                table: "UserProfiles",
                column: "UserId");

            // Ne migre que les utilisateurs qui avaient à la fois une entité ET un profil : une
            // habilitation exige les deux dans le nouveau modèle, un seul des deux ne suffit pas
            // à en reconstituer une.
            migrationBuilder.Sql(
                """
                INSERT INTO [UserProfiles] ([UserId], [EntityId], [ProfileId], [IsRecursive])
                SELECT [Id], [EntityId], [ProfileId], CAST(0 AS bit)
                FROM [Users]
                WHERE [EntityId] IS NOT NULL AND [ProfileId] IS NOT NULL;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Entities_EntityId",
                table: "Users");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Profiles_ProfileId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_EntityId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_ProfileId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "EntityId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ProfileId",
                table: "Users");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EntityId",
                table: "Users",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProfileId",
                table: "Users",
                type: "int",
                nullable: true);

            // Best effort : ne récupère qu'une habilitation par utilisateur (la plus récente par
            // Id), le modèle à une seule habilitation ne pouvant de toute façon pas en représenter
            // plusieurs.
            migrationBuilder.Sql(
                """
                UPDATE u
                SET u.[EntityId] = up.[EntityId], u.[ProfileId] = up.[ProfileId]
                FROM [Users] u
                CROSS APPLY (
                    SELECT TOP 1 [EntityId], [ProfileId]
                    FROM [UserProfiles]
                    WHERE [UserProfiles].[UserId] = u.[Id]
                    ORDER BY [Id] DESC
                ) up;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Users_EntityId",
                table: "Users",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_ProfileId",
                table: "Users",
                column: "ProfileId");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Entities_EntityId",
                table: "Users",
                column: "EntityId",
                principalTable: "Entities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Profiles_ProfileId",
                table: "Users",
                column: "ProfileId",
                principalTable: "Profiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.DropTable(
                name: "UserProfiles");
        }
    }
}

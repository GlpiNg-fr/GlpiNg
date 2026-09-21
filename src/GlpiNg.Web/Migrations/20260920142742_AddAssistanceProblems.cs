using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GlpiNg.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddAssistanceProblems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // TicketFollowups/TicketTasks deviennent ItilFollowups/ItilTasks, polymorphes
            // (ItemType + ItemId) pour être partagés avec les problèmes. EF engendrait un
            // DROP suivi d'un CREATE, qui aurait effacé les suivis et les tâches déjà saisis :
            // les nouvelles tables sont donc créées d'abord, remplies depuis les anciennes, et
            // les anciennes supprimées ensuite (plus bas).
            migrationBuilder.CreateTable(
                name: "ItilFollowups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ItemType = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ItemId = table.Column<int>(type: "int", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AuthorName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsPrivate = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItilFollowups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ItilTasks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ItemType = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ItemId = table.Column<int>(type: "int", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    State = table.Column<int>(type: "int", nullable: false),
                    AssignedUserId = table.Column<int>(type: "int", nullable: true),
                    PlannedStart = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PlannedEnd = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DurationMinutes = table.Column<int>(type: "int", nullable: true),
                    AuthorName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItilTasks", x => x.Id);
                });

            // Reprise des lignes existantes : tout ce qui était rattaché à un ticket le reste,
            // sous le nom de type que GLPI donne à ce type (voir Abstractions/Items/ItemTypes).
            migrationBuilder.Sql(
                """
                INSERT INTO ItilFollowups (ItemType, ItemId, Content, AuthorName, CreatedAt, IsPrivate)
                SELECT 'Ticket', TicketId, Content, AuthorName, CreatedAt, IsPrivate
                FROM TicketFollowups;
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO ItilTasks (ItemType, ItemId, Content, State, AssignedUserId, PlannedStart,
                                       PlannedEnd, DurationMinutes, AuthorName, CreatedAt, CompletedAt)
                SELECT 'Ticket', TicketId, Content, State, AssignedUserId, PlannedStart,
                       PlannedEnd, DurationMinutes, AuthorName, CreatedAt, CompletedAt
                FROM TicketTasks;
                """);

            migrationBuilder.DropTable(
                name: "TicketFollowups");

            migrationBuilder.DropTable(
                name: "TicketTasks");

            migrationBuilder.CreateTable(
                name: "Problems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    IsRecursive = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Urgency = table.Column<int>(type: "int", nullable: false),
                    Impact = table.Column<int>(type: "int", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    IsPriorityManual = table.Column<bool>(type: "bit", nullable: false),
                    CategoryId = table.Column<int>(type: "int", nullable: true),
                    AuthorUserId = table.Column<int>(type: "int", nullable: true),
                    AssignedUserId = table.Column<int>(type: "int", nullable: true),
                    AssignedGroupId = table.Column<int>(type: "int", nullable: true),
                    OpenedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SymptomContent = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CauseContent = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ImpactContent = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Workaround = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Solution = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SolutionType = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Problems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Problems_Entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Problems_TicketCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "TicketCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProblemTickets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProblemId = table.Column<int>(type: "int", nullable: false),
                    TicketId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProblemTickets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProblemTickets_Problems_ProblemId",
                        column: x => x.ProblemId,
                        principalTable: "Problems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProblemTickets_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ItilFollowups_ItemType_ItemId_CreatedAt",
                table: "ItilFollowups",
                columns: new[] { "ItemType", "ItemId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ItilTasks_ItemType_ItemId",
                table: "ItilTasks",
                columns: new[] { "ItemType", "ItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_Problems_AssignedUserId",
                table: "Problems",
                column: "AssignedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Problems_CategoryId",
                table: "Problems",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Problems_EntityId",
                table: "Problems",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_Problems_Status_OpenedAt",
                table: "Problems",
                columns: new[] { "Status", "OpenedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProblemTickets_ProblemId_TicketId",
                table: "ProblemTickets",
                columns: new[] { "ProblemId", "TicketId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProblemTickets_TicketId",
                table: "ProblemTickets",
                column: "TicketId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProblemTickets");

            migrationBuilder.DropTable(
                name: "Problems");

            migrationBuilder.CreateTable(
                name: "TicketFollowups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TicketId = table.Column<int>(type: "int", nullable: false),
                    AuthorName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsPrivate = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketFollowups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketFollowups_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TicketTasks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TicketId = table.Column<int>(type: "int", nullable: false),
                    AssignedUserId = table.Column<int>(type: "int", nullable: true),
                    AuthorName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DurationMinutes = table.Column<int>(type: "int", nullable: true),
                    PlannedEnd = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PlannedStart = table.Column<DateTime>(type: "datetime2", nullable: true),
                    State = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketTasks_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TicketFollowups_TicketId",
                table: "TicketFollowups",
                column: "TicketId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketTasks_TicketId",
                table: "TicketTasks",
                column: "TicketId");

            // Retour en arrière symétrique de l'aller : seuls les suivis et tâches de tickets
            // peuvent revenir, ceux des problèmes n'ayant plus de table où aller — c'est le prix
            // d'un retour en arrière, et la raison pour laquelle il se fait avant une sauvegarde.
            migrationBuilder.Sql(
                """
                INSERT INTO TicketFollowups (TicketId, Content, AuthorName, CreatedAt, IsPrivate)
                SELECT ItemId, Content, AuthorName, CreatedAt, IsPrivate
                FROM ItilFollowups
                WHERE ItemType = 'Ticket';
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO TicketTasks (TicketId, Content, State, AssignedUserId, PlannedStart,
                                         PlannedEnd, DurationMinutes, AuthorName, CreatedAt, CompletedAt)
                SELECT ItemId, Content, State, AssignedUserId, PlannedStart,
                       PlannedEnd, DurationMinutes, AuthorName, CreatedAt, CompletedAt
                FROM ItilTasks
                WHERE ItemType = 'Ticket';
                """);

            migrationBuilder.DropTable(
                name: "ItilFollowups");

            migrationBuilder.DropTable(
                name: "ItilTasks");
        }
    }
}

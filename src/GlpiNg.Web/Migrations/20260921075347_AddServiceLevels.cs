using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GlpiNg.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceLevels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "InternalTimeToOwn",
                table: "Tickets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InternalTimeToResolve",
                table: "Tickets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OlaStartedAt",
                table: "Tickets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OlaTimeToOwnId",
                table: "Tickets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OlaTimeToResolveId",
                table: "Tickets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SlaTimeToOwnId",
                table: "Tickets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SlaTimeToResolveId",
                table: "Tickets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TakenIntoAccountAt",
                table: "Tickets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TimeToOwn",
                table: "Tickets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TimeToResolve",
                table: "Tickets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Calendars",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    IsRecursive = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Calendars", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Calendars_Entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CalendarHolidays",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CalendarId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    IsPerpetual = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarHolidays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CalendarHolidays_Calendars_CalendarId",
                        column: x => x.CalendarId,
                        principalTable: "Calendars",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CalendarSegments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CalendarId = table.Column<int>(type: "int", nullable: false),
                    DayOfWeek = table.Column<int>(type: "int", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CalendarSegments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CalendarSegments_Calendars_CalendarId",
                        column: x => x.CalendarId,
                        principalTable: "Calendars",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ServiceLevels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    IsRecursive = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CalendarId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceLevels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceLevels_Calendars_CalendarId",
                        column: x => x.CalendarId,
                        principalTable: "Calendars",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ServiceLevels_Entities_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ServiceLevelAgreements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ServiceLevelId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Target = table.Column<int>(type: "int", nullable: false),
                    DurationValue = table.Column<int>(type: "int", nullable: false),
                    DurationUnit = table.Column<int>(type: "int", nullable: false),
                    EndOfWorkingDay = table.Column<bool>(type: "bit", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceLevelAgreements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceLevelAgreements_ServiceLevels_ServiceLevelId",
                        column: x => x.ServiceLevelId,
                        principalTable: "ServiceLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ServiceLevelEscalations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ServiceLevelAgreementId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OffsetMinutes = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceLevelEscalations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceLevelEscalations_ServiceLevelAgreements_ServiceLevelAgreementId",
                        column: x => x.ServiceLevelAgreementId,
                        principalTable: "ServiceLevelAgreements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ServiceLevelEscalationActions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ServiceLevelEscalationId = table.Column<int>(type: "int", nullable: false),
                    ActionType = table.Column<int>(type: "int", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceLevelEscalationActions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceLevelEscalationActions_ServiceLevelEscalations_ServiceLevelEscalationId",
                        column: x => x.ServiceLevelEscalationId,
                        principalTable: "ServiceLevelEscalations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TicketEscalations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TicketId = table.Column<int>(type: "int", nullable: false),
                    ServiceLevelEscalationId = table.Column<int>(type: "int", nullable: false),
                    ExecutedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketEscalations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketEscalations_ServiceLevelEscalations_ServiceLevelEscalationId",
                        column: x => x.ServiceLevelEscalationId,
                        principalTable: "ServiceLevelEscalations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TicketEscalations_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_OlaTimeToOwnId",
                table: "Tickets",
                column: "OlaTimeToOwnId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_OlaTimeToResolveId",
                table: "Tickets",
                column: "OlaTimeToResolveId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_SlaTimeToOwnId",
                table: "Tickets",
                column: "SlaTimeToOwnId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_SlaTimeToResolveId",
                table: "Tickets",
                column: "SlaTimeToResolveId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_TimeToResolve",
                table: "Tickets",
                column: "TimeToResolve");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarHolidays_CalendarId",
                table: "CalendarHolidays",
                column: "CalendarId");

            migrationBuilder.CreateIndex(
                name: "IX_Calendars_EntityId",
                table: "Calendars",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarSegments_CalendarId",
                table: "CalendarSegments",
                column: "CalendarId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceLevelAgreements_ServiceLevelId",
                table: "ServiceLevelAgreements",
                column: "ServiceLevelId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceLevelEscalationActions_ServiceLevelEscalationId",
                table: "ServiceLevelEscalationActions",
                column: "ServiceLevelEscalationId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceLevelEscalations_ServiceLevelAgreementId",
                table: "ServiceLevelEscalations",
                column: "ServiceLevelAgreementId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceLevels_CalendarId",
                table: "ServiceLevels",
                column: "CalendarId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceLevels_EntityId",
                table: "ServiceLevels",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketEscalations_ServiceLevelEscalationId",
                table: "TicketEscalations",
                column: "ServiceLevelEscalationId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketEscalations_TicketId_ServiceLevelEscalationId",
                table: "TicketEscalations",
                columns: new[] { "TicketId", "ServiceLevelEscalationId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_ServiceLevelAgreements_OlaTimeToOwnId",
                table: "Tickets",
                column: "OlaTimeToOwnId",
                principalTable: "ServiceLevelAgreements",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_ServiceLevelAgreements_OlaTimeToResolveId",
                table: "Tickets",
                column: "OlaTimeToResolveId",
                principalTable: "ServiceLevelAgreements",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_ServiceLevelAgreements_SlaTimeToOwnId",
                table: "Tickets",
                column: "SlaTimeToOwnId",
                principalTable: "ServiceLevelAgreements",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_ServiceLevelAgreements_SlaTimeToResolveId",
                table: "Tickets",
                column: "SlaTimeToResolveId",
                principalTable: "ServiceLevelAgreements",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_ServiceLevelAgreements_OlaTimeToOwnId",
                table: "Tickets");

            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_ServiceLevelAgreements_OlaTimeToResolveId",
                table: "Tickets");

            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_ServiceLevelAgreements_SlaTimeToOwnId",
                table: "Tickets");

            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_ServiceLevelAgreements_SlaTimeToResolveId",
                table: "Tickets");

            migrationBuilder.DropTable(
                name: "CalendarHolidays");

            migrationBuilder.DropTable(
                name: "CalendarSegments");

            migrationBuilder.DropTable(
                name: "ServiceLevelEscalationActions");

            migrationBuilder.DropTable(
                name: "TicketEscalations");

            migrationBuilder.DropTable(
                name: "ServiceLevelEscalations");

            migrationBuilder.DropTable(
                name: "ServiceLevelAgreements");

            migrationBuilder.DropTable(
                name: "ServiceLevels");

            migrationBuilder.DropTable(
                name: "Calendars");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_OlaTimeToOwnId",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_OlaTimeToResolveId",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_SlaTimeToOwnId",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_SlaTimeToResolveId",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_TimeToResolve",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "InternalTimeToOwn",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "InternalTimeToResolve",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "OlaStartedAt",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "OlaTimeToOwnId",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "OlaTimeToResolveId",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "SlaTimeToOwnId",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "SlaTimeToResolveId",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "TakenIntoAccountAt",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "TimeToOwn",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "TimeToResolve",
                table: "Tickets");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GlpiNg.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddDropdownStatusToComputerPeripheralAndMonitor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "StatusId",
                table: "Peripherals",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StatusId",
                table: "Computers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StatusId",
                table: "ComputerPeripherals",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Peripherals_StatusId",
                table: "Peripherals",
                column: "StatusId");

            migrationBuilder.CreateIndex(
                name: "IX_Computers_StatusId",
                table: "Computers",
                column: "StatusId");

            migrationBuilder.CreateIndex(
                name: "IX_ComputerPeripherals_StatusId",
                table: "ComputerPeripherals",
                column: "StatusId");

            migrationBuilder.AddForeignKey(
                name: "FK_ComputerPeripherals_DropdownItems_StatusId",
                table: "ComputerPeripherals",
                column: "StatusId",
                principalTable: "DropdownItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Computers_DropdownItems_StatusId",
                table: "Computers",
                column: "StatusId",
                principalTable: "DropdownItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Peripherals_DropdownItems_StatusId",
                table: "Peripherals",
                column: "StatusId",
                principalTable: "DropdownItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // Préserve les valeurs existantes de l'ancienne énumération ComputerStatus (0=En stock,
            // 1=En production, 2=En panne, 3=Réformé, stockée telle quelle par EF Core avant ce
            // changement) en les recréant comme Intitulés de type Status (6 = DropdownType.Status)
            // et en reportant leur Id sur StatusId, avant de supprimer la colonne Status ci-dessous.
            migrationBuilder.Sql("""
                INSERT INTO [DropdownItems] ([Type], [Name])
                SELECT 6, N'En stock' WHERE NOT EXISTS (SELECT 1 FROM [DropdownItems] WHERE [Type] = 6 AND [Name] = N'En stock');
                INSERT INTO [DropdownItems] ([Type], [Name])
                SELECT 6, N'En production' WHERE NOT EXISTS (SELECT 1 FROM [DropdownItems] WHERE [Type] = 6 AND [Name] = N'En production');
                INSERT INTO [DropdownItems] ([Type], [Name])
                SELECT 6, N'En panne' WHERE NOT EXISTS (SELECT 1 FROM [DropdownItems] WHERE [Type] = 6 AND [Name] = N'En panne');
                INSERT INTO [DropdownItems] ([Type], [Name])
                SELECT 6, N'Réformé' WHERE NOT EXISTS (SELECT 1 FROM [DropdownItems] WHERE [Type] = 6 AND [Name] = N'Réformé');

                UPDATE c
                SET c.[StatusId] = d.[Id]
                FROM [Computers] c
                JOIN [DropdownItems] d ON d.[Type] = 6 AND d.[Name] = CASE c.[Status]
                    WHEN 0 THEN N'En stock'
                    WHEN 1 THEN N'En production'
                    WHEN 2 THEN N'En panne'
                    WHEN 3 THEN N'Réformé'
                END;

                UPDATE p
                SET p.[StatusId] = d.[Id]
                FROM [Peripherals] p
                JOIN [DropdownItems] d ON d.[Type] = 6 AND d.[Name] = CASE p.[Status]
                    WHEN 0 THEN N'En stock'
                    WHEN 1 THEN N'En production'
                    WHEN 2 THEN N'En panne'
                    WHEN 3 THEN N'Réformé'
                END;
                """);

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Peripherals");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Computers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ComputerPeripherals_DropdownItems_StatusId",
                table: "ComputerPeripherals");

            migrationBuilder.DropForeignKey(
                name: "FK_Computers_DropdownItems_StatusId",
                table: "Computers");

            migrationBuilder.DropForeignKey(
                name: "FK_Peripherals_DropdownItems_StatusId",
                table: "Peripherals");

            migrationBuilder.DropIndex(
                name: "IX_Peripherals_StatusId",
                table: "Peripherals");

            migrationBuilder.DropIndex(
                name: "IX_Computers_StatusId",
                table: "Computers");

            migrationBuilder.DropIndex(
                name: "IX_ComputerPeripherals_StatusId",
                table: "ComputerPeripherals");

            migrationBuilder.DropColumn(
                name: "StatusId",
                table: "Peripherals");

            migrationBuilder.DropColumn(
                name: "StatusId",
                table: "Computers");

            migrationBuilder.DropColumn(
                name: "StatusId",
                table: "ComputerPeripherals");

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Peripherals",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Computers",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}

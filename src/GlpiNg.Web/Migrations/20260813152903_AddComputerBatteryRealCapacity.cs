using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GlpiNg.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddComputerBatteryRealCapacity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RealCapacityMwh",
                table: "ComputerBatteries",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RealCapacityMwh",
                table: "ComputerBatteries");
        }
    }
}

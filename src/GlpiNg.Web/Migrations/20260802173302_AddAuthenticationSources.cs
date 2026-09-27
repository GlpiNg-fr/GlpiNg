using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GlpiNg.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthenticationSources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AuthSource",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ExternalDn",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LdapServerId",
                table: "Users",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AuthLdapServers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Host = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Port = table.Column<int>(type: "int", nullable: false),
                    UseSsl = table.Column<bool>(type: "bit", nullable: false),
                    LoginFilter = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BaseDn = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UseBind = table.Column<bool>(type: "bit", nullable: false),
                    BindDn = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BindPasswordProtected = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LoginField = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SyncField = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuthLdapServers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuthMailServers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Host = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Port = table.Column<int>(type: "int", nullable: true),
                    Protocol = table.Column<int>(type: "int", nullable: false),
                    Encryption = table.Column<int>(type: "int", nullable: false),
                    ValidateCertificate = table.Column<bool>(type: "bit", nullable: false),
                    EmailDomain = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuthMailServers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_LdapServerId",
                table: "Users",
                column: "LdapServerId");

            migrationBuilder.CreateIndex(
                name: "IX_AuthLdapServers_Name",
                table: "AuthLdapServers",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuthMailServers_Name",
                table: "AuthMailServers",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_AuthLdapServers_LdapServerId",
                table: "Users",
                column: "LdapServerId",
                principalTable: "AuthLdapServers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_AuthLdapServers_LdapServerId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "AuthLdapServers");

            migrationBuilder.DropTable(
                name: "AuthMailServers");

            migrationBuilder.DropIndex(
                name: "IX_Users_LdapServerId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "AuthSource",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ExternalDn",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LdapServerId",
                table: "Users");
        }
    }
}

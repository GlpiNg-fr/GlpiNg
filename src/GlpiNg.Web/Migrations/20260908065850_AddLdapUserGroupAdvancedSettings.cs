using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GlpiNg.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddLdapUserGroupAdvancedSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CommentField",
                table: "AuthLdapServers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DisplayNameField",
                table: "AuthLdapServers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmailField",
                table: "AuthLdapServers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EntityAssignmentTag",
                table: "AuthLdapServers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FirstNameField",
                table: "AuthLdapServers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GroupFilter",
                table: "AuthLdapServers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GroupMemberField",
                table: "AuthLdapServers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GroupMemberOfField",
                table: "AuthLdapServers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GroupNameField",
                table: "AuthLdapServers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GroupSearchType",
                table: "AuthLdapServers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "LastNameField",
                table: "AuthLdapServers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LocationField",
                table: "AuthLdapServers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxResults",
                table: "AuthLdapServers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "MobileField",
                table: "AuthLdapServers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PageSize",
                table: "AuthLdapServers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PhoneField",
                table: "AuthLdapServers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SynchronizeGroups",
                table: "AuthLdapServers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "TimeoutSeconds",
                table: "AuthLdapServers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "TitleField",
                table: "AuthLdapServers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "UseDnForGroupSearch",
                table: "AuthLdapServers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "UseStartTls",
                table: "AuthLdapServers",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CommentField",
                table: "AuthLdapServers");

            migrationBuilder.DropColumn(
                name: "DisplayNameField",
                table: "AuthLdapServers");

            migrationBuilder.DropColumn(
                name: "EmailField",
                table: "AuthLdapServers");

            migrationBuilder.DropColumn(
                name: "EntityAssignmentTag",
                table: "AuthLdapServers");

            migrationBuilder.DropColumn(
                name: "FirstNameField",
                table: "AuthLdapServers");

            migrationBuilder.DropColumn(
                name: "GroupFilter",
                table: "AuthLdapServers");

            migrationBuilder.DropColumn(
                name: "GroupMemberField",
                table: "AuthLdapServers");

            migrationBuilder.DropColumn(
                name: "GroupMemberOfField",
                table: "AuthLdapServers");

            migrationBuilder.DropColumn(
                name: "GroupNameField",
                table: "AuthLdapServers");

            migrationBuilder.DropColumn(
                name: "GroupSearchType",
                table: "AuthLdapServers");

            migrationBuilder.DropColumn(
                name: "LastNameField",
                table: "AuthLdapServers");

            migrationBuilder.DropColumn(
                name: "LocationField",
                table: "AuthLdapServers");

            migrationBuilder.DropColumn(
                name: "MaxResults",
                table: "AuthLdapServers");

            migrationBuilder.DropColumn(
                name: "MobileField",
                table: "AuthLdapServers");

            migrationBuilder.DropColumn(
                name: "PageSize",
                table: "AuthLdapServers");

            migrationBuilder.DropColumn(
                name: "PhoneField",
                table: "AuthLdapServers");

            migrationBuilder.DropColumn(
                name: "SynchronizeGroups",
                table: "AuthLdapServers");

            migrationBuilder.DropColumn(
                name: "TimeoutSeconds",
                table: "AuthLdapServers");

            migrationBuilder.DropColumn(
                name: "TitleField",
                table: "AuthLdapServers");

            migrationBuilder.DropColumn(
                name: "UseDnForGroupSearch",
                table: "AuthLdapServers");

            migrationBuilder.DropColumn(
                name: "UseStartTls",
                table: "AuthLdapServers");
        }
    }
}

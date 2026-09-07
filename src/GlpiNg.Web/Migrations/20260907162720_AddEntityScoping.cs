using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GlpiNg.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddEntityScoping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EntityId",
                table: "WakeOnLanTasks",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRecursive",
                table: "WakeOnLanTasks",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "EntityId",
                table: "TimeSlots",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRecursive",
                table: "TimeSlots",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "EntityId",
                table: "SnmpCredentials",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRecursive",
                table: "SnmpCredentials",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "EntityId",
                table: "SavedSearches",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRecursive",
                table: "SavedSearches",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "EntityId",
                table: "Racks",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRecursive",
                table: "Racks",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "EntityId",
                table: "Printers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRecursive",
                table: "Printers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "EntityId",
                table: "Phones",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRecursive",
                table: "Phones",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "EntityId",
                table: "Peripherals",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRecursive",
                table: "Peripherals",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "EntityId",
                table: "Pdus",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRecursive",
                table: "Pdus",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "EntityId",
                table: "PassiveEquipments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRecursive",
                table: "PassiveEquipments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "EntityId",
                table: "NotificationTemplates",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRecursive",
                table: "NotificationTemplates",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "EntityId",
                table: "Notifications",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRecursive",
                table: "Notifications",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "EntityId",
                table: "NetworkTasks",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRecursive",
                table: "NetworkTasks",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "EntityId",
                table: "NetworkEquipments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRecursive",
                table: "NetworkEquipments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "EntityId",
                table: "IpRanges",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRecursive",
                table: "IpRanges",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "EntityId",
                table: "ImportBlacklistEntries",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRecursive",
                table: "ImportBlacklistEntries",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "EntityId",
                table: "ImportAssignmentRules",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRecursive",
                table: "ImportAssignmentRules",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "EntityId",
                table: "Groups",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EntityId",
                table: "Enclosures",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRecursive",
                table: "Enclosures",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "EntityId",
                table: "DropdownItems",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRecursive",
                table: "DropdownItems",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "EntityId",
                table: "DiscoveredNetworkDevices",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRecursive",
                table: "DiscoveredNetworkDevices",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "EntityId",
                table: "DictionaryRules",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRecursive",
                table: "DictionaryRules",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "EntityId",
                table: "DeploymentUserInteractionTemplates",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRecursive",
                table: "DeploymentUserInteractionTemplates",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "EntityId",
                table: "DeploymentTasks",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRecursive",
                table: "DeploymentTasks",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "EntityId",
                table: "DeploymentRules",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRecursive",
                table: "DeploymentRules",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "EntityId",
                table: "DeploymentPackages",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRecursive",
                table: "DeploymentPackages",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "EntityId",
                table: "DeploymentMirrorServers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRecursive",
                table: "DeploymentMirrorServers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "EntityId",
                table: "DeployComputerGroups",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRecursive",
                table: "DeployComputerGroups",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "EntityId",
                table: "ConsumableItems",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRecursive",
                table: "ConsumableItems",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "EntityId",
                table: "Computers",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRecursive",
                table: "Computers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "EntityId",
                table: "ComputerRules",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRecursive",
                table: "ComputerRules",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "EntityId",
                table: "CollectDefinitions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRecursive",
                table: "CollectDefinitions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "EntityId",
                table: "CartridgeItems",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRecursive",
                table: "CartridgeItems",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "EntityId",
                table: "Cables",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRecursive",
                table: "Cables",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "EntityId",
                table: "Agents",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRecursive",
                table: "Agents",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_WakeOnLanTasks_EntityId",
                table: "WakeOnLanTasks",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_TimeSlots_EntityId",
                table: "TimeSlots",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_SnmpCredentials_EntityId",
                table: "SnmpCredentials",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_SavedSearches_EntityId",
                table: "SavedSearches",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_Racks_EntityId",
                table: "Racks",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_Printers_EntityId",
                table: "Printers",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_Phones_EntityId",
                table: "Phones",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_Peripherals_EntityId",
                table: "Peripherals",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_Pdus_EntityId",
                table: "Pdus",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_PassiveEquipments_EntityId",
                table: "PassiveEquipments",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationTemplates_EntityId",
                table: "NotificationTemplates",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_EntityId",
                table: "Notifications",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkTasks_EntityId",
                table: "NetworkTasks",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkEquipments_EntityId",
                table: "NetworkEquipments",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_IpRanges_EntityId",
                table: "IpRanges",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_ImportBlacklistEntries_EntityId",
                table: "ImportBlacklistEntries",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_ImportAssignmentRules_EntityId",
                table: "ImportAssignmentRules",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_Groups_EntityId",
                table: "Groups",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_Enclosures_EntityId",
                table: "Enclosures",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_DropdownItems_EntityId",
                table: "DropdownItems",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_DiscoveredNetworkDevices_EntityId",
                table: "DiscoveredNetworkDevices",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_DictionaryRules_EntityId",
                table: "DictionaryRules",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentUserInteractionTemplates_EntityId",
                table: "DeploymentUserInteractionTemplates",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentTasks_EntityId",
                table: "DeploymentTasks",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentRules_EntityId",
                table: "DeploymentRules",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentPackages_EntityId",
                table: "DeploymentPackages",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentMirrorServers_EntityId",
                table: "DeploymentMirrorServers",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_DeployComputerGroups_EntityId",
                table: "DeployComputerGroups",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsumableItems_EntityId",
                table: "ConsumableItems",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_Computers_EntityId",
                table: "Computers",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_ComputerRules_EntityId",
                table: "ComputerRules",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_CollectDefinitions_EntityId",
                table: "CollectDefinitions",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_CartridgeItems_EntityId",
                table: "CartridgeItems",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_Cables_EntityId",
                table: "Cables",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_Agents_EntityId",
                table: "Agents",
                column: "EntityId");

            migrationBuilder.AddForeignKey(
                name: "FK_Agents_Entities_EntityId",
                table: "Agents",
                column: "EntityId",
                principalTable: "Entities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Cables_Entities_EntityId",
                table: "Cables",
                column: "EntityId",
                principalTable: "Entities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CartridgeItems_Entities_EntityId",
                table: "CartridgeItems",
                column: "EntityId",
                principalTable: "Entities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CollectDefinitions_Entities_EntityId",
                table: "CollectDefinitions",
                column: "EntityId",
                principalTable: "Entities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ComputerRules_Entities_EntityId",
                table: "ComputerRules",
                column: "EntityId",
                principalTable: "Entities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Computers_Entities_EntityId",
                table: "Computers",
                column: "EntityId",
                principalTable: "Entities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ConsumableItems_Entities_EntityId",
                table: "ConsumableItems",
                column: "EntityId",
                principalTable: "Entities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DeployComputerGroups_Entities_EntityId",
                table: "DeployComputerGroups",
                column: "EntityId",
                principalTable: "Entities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DeploymentMirrorServers_Entities_EntityId",
                table: "DeploymentMirrorServers",
                column: "EntityId",
                principalTable: "Entities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DeploymentPackages_Entities_EntityId",
                table: "DeploymentPackages",
                column: "EntityId",
                principalTable: "Entities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DeploymentRules_Entities_EntityId",
                table: "DeploymentRules",
                column: "EntityId",
                principalTable: "Entities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DeploymentTasks_Entities_EntityId",
                table: "DeploymentTasks",
                column: "EntityId",
                principalTable: "Entities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DeploymentUserInteractionTemplates_Entities_EntityId",
                table: "DeploymentUserInteractionTemplates",
                column: "EntityId",
                principalTable: "Entities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DictionaryRules_Entities_EntityId",
                table: "DictionaryRules",
                column: "EntityId",
                principalTable: "Entities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DiscoveredNetworkDevices_Entities_EntityId",
                table: "DiscoveredNetworkDevices",
                column: "EntityId",
                principalTable: "Entities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DropdownItems_Entities_EntityId",
                table: "DropdownItems",
                column: "EntityId",
                principalTable: "Entities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Enclosures_Entities_EntityId",
                table: "Enclosures",
                column: "EntityId",
                principalTable: "Entities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Groups_Entities_EntityId",
                table: "Groups",
                column: "EntityId",
                principalTable: "Entities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ImportAssignmentRules_Entities_EntityId",
                table: "ImportAssignmentRules",
                column: "EntityId",
                principalTable: "Entities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ImportBlacklistEntries_Entities_EntityId",
                table: "ImportBlacklistEntries",
                column: "EntityId",
                principalTable: "Entities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_IpRanges_Entities_EntityId",
                table: "IpRanges",
                column: "EntityId",
                principalTable: "Entities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_NetworkEquipments_Entities_EntityId",
                table: "NetworkEquipments",
                column: "EntityId",
                principalTable: "Entities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_NetworkTasks_Entities_EntityId",
                table: "NetworkTasks",
                column: "EntityId",
                principalTable: "Entities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_Entities_EntityId",
                table: "Notifications",
                column: "EntityId",
                principalTable: "Entities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_NotificationTemplates_Entities_EntityId",
                table: "NotificationTemplates",
                column: "EntityId",
                principalTable: "Entities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PassiveEquipments_Entities_EntityId",
                table: "PassiveEquipments",
                column: "EntityId",
                principalTable: "Entities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Pdus_Entities_EntityId",
                table: "Pdus",
                column: "EntityId",
                principalTable: "Entities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Peripherals_Entities_EntityId",
                table: "Peripherals",
                column: "EntityId",
                principalTable: "Entities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Phones_Entities_EntityId",
                table: "Phones",
                column: "EntityId",
                principalTable: "Entities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Printers_Entities_EntityId",
                table: "Printers",
                column: "EntityId",
                principalTable: "Entities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Racks_Entities_EntityId",
                table: "Racks",
                column: "EntityId",
                principalTable: "Entities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SavedSearches_Entities_EntityId",
                table: "SavedSearches",
                column: "EntityId",
                principalTable: "Entities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SnmpCredentials_Entities_EntityId",
                table: "SnmpCredentials",
                column: "EntityId",
                principalTable: "Entities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TimeSlots_Entities_EntityId",
                table: "TimeSlots",
                column: "EntityId",
                principalTable: "Entities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WakeOnLanTasks_Entities_EntityId",
                table: "WakeOnLanTasks",
                column: "EntityId",
                principalTable: "Entities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Rattache l'existant à l'entité racine. Sans ça, tout garde EntityId NULL, cas que
            // EntityScope.Allows laisse volontairement visible de tous (objet « pas encore rattaché ») :
            // le cloisonnement ne s'appliquerait donc à aucune donnée déjà en base. Rattacher à la
            // racine reproduit ce que fait GLPI (entities_id = 0 par défaut) et laisse l'administrateur
            // réaffecter ensuite ce qui doit l'être.
            //
            // Guillemets doubles autour des identifiants : accepté par SQL Server (QUOTED_IDENTIFIER
            // est actif par défaut avec SqlClient) comme par PostgreSQL. MySQL/PostgreSQL passent de
            // toute façon par EnsureCreated (base neuve, rien à reprendre) — voir le README.
            migrationBuilder.Sql(
                """UPDATE "Agents" SET "EntityId" = (SELECT MIN("Id") FROM "Entities" WHERE "ParentId" IS NULL) WHERE "EntityId" IS NULL;""");
            migrationBuilder.Sql(
                """UPDATE "Cables" SET "EntityId" = (SELECT MIN("Id") FROM "Entities" WHERE "ParentId" IS NULL) WHERE "EntityId" IS NULL;""");
            migrationBuilder.Sql(
                """UPDATE "CartridgeItems" SET "EntityId" = (SELECT MIN("Id") FROM "Entities" WHERE "ParentId" IS NULL) WHERE "EntityId" IS NULL;""");
            migrationBuilder.Sql(
                """UPDATE "CollectDefinitions" SET "EntityId" = (SELECT MIN("Id") FROM "Entities" WHERE "ParentId" IS NULL) WHERE "EntityId" IS NULL;""");
            migrationBuilder.Sql(
                """UPDATE "ComputerRules" SET "EntityId" = (SELECT MIN("Id") FROM "Entities" WHERE "ParentId" IS NULL) WHERE "EntityId" IS NULL;""");
            migrationBuilder.Sql(
                """UPDATE "Computers" SET "EntityId" = (SELECT MIN("Id") FROM "Entities" WHERE "ParentId" IS NULL) WHERE "EntityId" IS NULL;""");
            migrationBuilder.Sql(
                """UPDATE "ConsumableItems" SET "EntityId" = (SELECT MIN("Id") FROM "Entities" WHERE "ParentId" IS NULL) WHERE "EntityId" IS NULL;""");
            migrationBuilder.Sql(
                """UPDATE "DeployComputerGroups" SET "EntityId" = (SELECT MIN("Id") FROM "Entities" WHERE "ParentId" IS NULL) WHERE "EntityId" IS NULL;""");
            migrationBuilder.Sql(
                """UPDATE "DeploymentMirrorServers" SET "EntityId" = (SELECT MIN("Id") FROM "Entities" WHERE "ParentId" IS NULL) WHERE "EntityId" IS NULL;""");
            migrationBuilder.Sql(
                """UPDATE "DeploymentPackages" SET "EntityId" = (SELECT MIN("Id") FROM "Entities" WHERE "ParentId" IS NULL) WHERE "EntityId" IS NULL;""");
            migrationBuilder.Sql(
                """UPDATE "DeploymentRules" SET "EntityId" = (SELECT MIN("Id") FROM "Entities" WHERE "ParentId" IS NULL) WHERE "EntityId" IS NULL;""");
            migrationBuilder.Sql(
                """UPDATE "DeploymentTasks" SET "EntityId" = (SELECT MIN("Id") FROM "Entities" WHERE "ParentId" IS NULL) WHERE "EntityId" IS NULL;""");
            migrationBuilder.Sql(
                """UPDATE "DeploymentUserInteractionTemplates" SET "EntityId" = (SELECT MIN("Id") FROM "Entities" WHERE "ParentId" IS NULL) WHERE "EntityId" IS NULL;""");
            migrationBuilder.Sql(
                """UPDATE "DictionaryRules" SET "EntityId" = (SELECT MIN("Id") FROM "Entities" WHERE "ParentId" IS NULL) WHERE "EntityId" IS NULL;""");
            migrationBuilder.Sql(
                """UPDATE "DiscoveredNetworkDevices" SET "EntityId" = (SELECT MIN("Id") FROM "Entities" WHERE "ParentId" IS NULL) WHERE "EntityId" IS NULL;""");
            migrationBuilder.Sql(
                """UPDATE "DropdownItems" SET "EntityId" = (SELECT MIN("Id") FROM "Entities" WHERE "ParentId" IS NULL) WHERE "EntityId" IS NULL;""");
            migrationBuilder.Sql(
                """UPDATE "Enclosures" SET "EntityId" = (SELECT MIN("Id") FROM "Entities" WHERE "ParentId" IS NULL) WHERE "EntityId" IS NULL;""");
            migrationBuilder.Sql(
                """UPDATE "Groups" SET "EntityId" = (SELECT MIN("Id") FROM "Entities" WHERE "ParentId" IS NULL) WHERE "EntityId" IS NULL;""");
            migrationBuilder.Sql(
                """UPDATE "ImportAssignmentRules" SET "EntityId" = (SELECT MIN("Id") FROM "Entities" WHERE "ParentId" IS NULL) WHERE "EntityId" IS NULL;""");
            migrationBuilder.Sql(
                """UPDATE "ImportBlacklistEntries" SET "EntityId" = (SELECT MIN("Id") FROM "Entities" WHERE "ParentId" IS NULL) WHERE "EntityId" IS NULL;""");
            migrationBuilder.Sql(
                """UPDATE "IpRanges" SET "EntityId" = (SELECT MIN("Id") FROM "Entities" WHERE "ParentId" IS NULL) WHERE "EntityId" IS NULL;""");
            migrationBuilder.Sql(
                """UPDATE "NetworkEquipments" SET "EntityId" = (SELECT MIN("Id") FROM "Entities" WHERE "ParentId" IS NULL) WHERE "EntityId" IS NULL;""");
            migrationBuilder.Sql(
                """UPDATE "NetworkTasks" SET "EntityId" = (SELECT MIN("Id") FROM "Entities" WHERE "ParentId" IS NULL) WHERE "EntityId" IS NULL;""");
            migrationBuilder.Sql(
                """UPDATE "NotificationTemplates" SET "EntityId" = (SELECT MIN("Id") FROM "Entities" WHERE "ParentId" IS NULL) WHERE "EntityId" IS NULL;""");
            migrationBuilder.Sql(
                """UPDATE "Notifications" SET "EntityId" = (SELECT MIN("Id") FROM "Entities" WHERE "ParentId" IS NULL) WHERE "EntityId" IS NULL;""");
            migrationBuilder.Sql(
                """UPDATE "PassiveEquipments" SET "EntityId" = (SELECT MIN("Id") FROM "Entities" WHERE "ParentId" IS NULL) WHERE "EntityId" IS NULL;""");
            migrationBuilder.Sql(
                """UPDATE "Pdus" SET "EntityId" = (SELECT MIN("Id") FROM "Entities" WHERE "ParentId" IS NULL) WHERE "EntityId" IS NULL;""");
            migrationBuilder.Sql(
                """UPDATE "Peripherals" SET "EntityId" = (SELECT MIN("Id") FROM "Entities" WHERE "ParentId" IS NULL) WHERE "EntityId" IS NULL;""");
            migrationBuilder.Sql(
                """UPDATE "Phones" SET "EntityId" = (SELECT MIN("Id") FROM "Entities" WHERE "ParentId" IS NULL) WHERE "EntityId" IS NULL;""");
            migrationBuilder.Sql(
                """UPDATE "Printers" SET "EntityId" = (SELECT MIN("Id") FROM "Entities" WHERE "ParentId" IS NULL) WHERE "EntityId" IS NULL;""");
            migrationBuilder.Sql(
                """UPDATE "Racks" SET "EntityId" = (SELECT MIN("Id") FROM "Entities" WHERE "ParentId" IS NULL) WHERE "EntityId" IS NULL;""");
            migrationBuilder.Sql(
                """UPDATE "SavedSearches" SET "EntityId" = (SELECT MIN("Id") FROM "Entities" WHERE "ParentId" IS NULL) WHERE "EntityId" IS NULL;""");
            migrationBuilder.Sql(
                """UPDATE "SnmpCredentials" SET "EntityId" = (SELECT MIN("Id") FROM "Entities" WHERE "ParentId" IS NULL) WHERE "EntityId" IS NULL;""");
            migrationBuilder.Sql(
                """UPDATE "TimeSlots" SET "EntityId" = (SELECT MIN("Id") FROM "Entities" WHERE "ParentId" IS NULL) WHERE "EntityId" IS NULL;""");
            migrationBuilder.Sql(
                """UPDATE "WakeOnLanTasks" SET "EntityId" = (SELECT MIN("Id") FROM "Entities" WHERE "ParentId" IS NULL) WHERE "EntityId" IS NULL;""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Agents_Entities_EntityId",
                table: "Agents");

            migrationBuilder.DropForeignKey(
                name: "FK_Cables_Entities_EntityId",
                table: "Cables");

            migrationBuilder.DropForeignKey(
                name: "FK_CartridgeItems_Entities_EntityId",
                table: "CartridgeItems");

            migrationBuilder.DropForeignKey(
                name: "FK_CollectDefinitions_Entities_EntityId",
                table: "CollectDefinitions");

            migrationBuilder.DropForeignKey(
                name: "FK_ComputerRules_Entities_EntityId",
                table: "ComputerRules");

            migrationBuilder.DropForeignKey(
                name: "FK_Computers_Entities_EntityId",
                table: "Computers");

            migrationBuilder.DropForeignKey(
                name: "FK_ConsumableItems_Entities_EntityId",
                table: "ConsumableItems");

            migrationBuilder.DropForeignKey(
                name: "FK_DeployComputerGroups_Entities_EntityId",
                table: "DeployComputerGroups");

            migrationBuilder.DropForeignKey(
                name: "FK_DeploymentMirrorServers_Entities_EntityId",
                table: "DeploymentMirrorServers");

            migrationBuilder.DropForeignKey(
                name: "FK_DeploymentPackages_Entities_EntityId",
                table: "DeploymentPackages");

            migrationBuilder.DropForeignKey(
                name: "FK_DeploymentRules_Entities_EntityId",
                table: "DeploymentRules");

            migrationBuilder.DropForeignKey(
                name: "FK_DeploymentTasks_Entities_EntityId",
                table: "DeploymentTasks");

            migrationBuilder.DropForeignKey(
                name: "FK_DeploymentUserInteractionTemplates_Entities_EntityId",
                table: "DeploymentUserInteractionTemplates");

            migrationBuilder.DropForeignKey(
                name: "FK_DictionaryRules_Entities_EntityId",
                table: "DictionaryRules");

            migrationBuilder.DropForeignKey(
                name: "FK_DiscoveredNetworkDevices_Entities_EntityId",
                table: "DiscoveredNetworkDevices");

            migrationBuilder.DropForeignKey(
                name: "FK_DropdownItems_Entities_EntityId",
                table: "DropdownItems");

            migrationBuilder.DropForeignKey(
                name: "FK_Enclosures_Entities_EntityId",
                table: "Enclosures");

            migrationBuilder.DropForeignKey(
                name: "FK_Groups_Entities_EntityId",
                table: "Groups");

            migrationBuilder.DropForeignKey(
                name: "FK_ImportAssignmentRules_Entities_EntityId",
                table: "ImportAssignmentRules");

            migrationBuilder.DropForeignKey(
                name: "FK_ImportBlacklistEntries_Entities_EntityId",
                table: "ImportBlacklistEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_IpRanges_Entities_EntityId",
                table: "IpRanges");

            migrationBuilder.DropForeignKey(
                name: "FK_NetworkEquipments_Entities_EntityId",
                table: "NetworkEquipments");

            migrationBuilder.DropForeignKey(
                name: "FK_NetworkTasks_Entities_EntityId",
                table: "NetworkTasks");

            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_Entities_EntityId",
                table: "Notifications");

            migrationBuilder.DropForeignKey(
                name: "FK_NotificationTemplates_Entities_EntityId",
                table: "NotificationTemplates");

            migrationBuilder.DropForeignKey(
                name: "FK_PassiveEquipments_Entities_EntityId",
                table: "PassiveEquipments");

            migrationBuilder.DropForeignKey(
                name: "FK_Pdus_Entities_EntityId",
                table: "Pdus");

            migrationBuilder.DropForeignKey(
                name: "FK_Peripherals_Entities_EntityId",
                table: "Peripherals");

            migrationBuilder.DropForeignKey(
                name: "FK_Phones_Entities_EntityId",
                table: "Phones");

            migrationBuilder.DropForeignKey(
                name: "FK_Printers_Entities_EntityId",
                table: "Printers");

            migrationBuilder.DropForeignKey(
                name: "FK_Racks_Entities_EntityId",
                table: "Racks");

            migrationBuilder.DropForeignKey(
                name: "FK_SavedSearches_Entities_EntityId",
                table: "SavedSearches");

            migrationBuilder.DropForeignKey(
                name: "FK_SnmpCredentials_Entities_EntityId",
                table: "SnmpCredentials");

            migrationBuilder.DropForeignKey(
                name: "FK_TimeSlots_Entities_EntityId",
                table: "TimeSlots");

            migrationBuilder.DropForeignKey(
                name: "FK_WakeOnLanTasks_Entities_EntityId",
                table: "WakeOnLanTasks");

            migrationBuilder.DropIndex(
                name: "IX_WakeOnLanTasks_EntityId",
                table: "WakeOnLanTasks");

            migrationBuilder.DropIndex(
                name: "IX_TimeSlots_EntityId",
                table: "TimeSlots");

            migrationBuilder.DropIndex(
                name: "IX_SnmpCredentials_EntityId",
                table: "SnmpCredentials");

            migrationBuilder.DropIndex(
                name: "IX_SavedSearches_EntityId",
                table: "SavedSearches");

            migrationBuilder.DropIndex(
                name: "IX_Racks_EntityId",
                table: "Racks");

            migrationBuilder.DropIndex(
                name: "IX_Printers_EntityId",
                table: "Printers");

            migrationBuilder.DropIndex(
                name: "IX_Phones_EntityId",
                table: "Phones");

            migrationBuilder.DropIndex(
                name: "IX_Peripherals_EntityId",
                table: "Peripherals");

            migrationBuilder.DropIndex(
                name: "IX_Pdus_EntityId",
                table: "Pdus");

            migrationBuilder.DropIndex(
                name: "IX_PassiveEquipments_EntityId",
                table: "PassiveEquipments");

            migrationBuilder.DropIndex(
                name: "IX_NotificationTemplates_EntityId",
                table: "NotificationTemplates");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_EntityId",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_NetworkTasks_EntityId",
                table: "NetworkTasks");

            migrationBuilder.DropIndex(
                name: "IX_NetworkEquipments_EntityId",
                table: "NetworkEquipments");

            migrationBuilder.DropIndex(
                name: "IX_IpRanges_EntityId",
                table: "IpRanges");

            migrationBuilder.DropIndex(
                name: "IX_ImportBlacklistEntries_EntityId",
                table: "ImportBlacklistEntries");

            migrationBuilder.DropIndex(
                name: "IX_ImportAssignmentRules_EntityId",
                table: "ImportAssignmentRules");

            migrationBuilder.DropIndex(
                name: "IX_Groups_EntityId",
                table: "Groups");

            migrationBuilder.DropIndex(
                name: "IX_Enclosures_EntityId",
                table: "Enclosures");

            migrationBuilder.DropIndex(
                name: "IX_DropdownItems_EntityId",
                table: "DropdownItems");

            migrationBuilder.DropIndex(
                name: "IX_DiscoveredNetworkDevices_EntityId",
                table: "DiscoveredNetworkDevices");

            migrationBuilder.DropIndex(
                name: "IX_DictionaryRules_EntityId",
                table: "DictionaryRules");

            migrationBuilder.DropIndex(
                name: "IX_DeploymentUserInteractionTemplates_EntityId",
                table: "DeploymentUserInteractionTemplates");

            migrationBuilder.DropIndex(
                name: "IX_DeploymentTasks_EntityId",
                table: "DeploymentTasks");

            migrationBuilder.DropIndex(
                name: "IX_DeploymentRules_EntityId",
                table: "DeploymentRules");

            migrationBuilder.DropIndex(
                name: "IX_DeploymentPackages_EntityId",
                table: "DeploymentPackages");

            migrationBuilder.DropIndex(
                name: "IX_DeploymentMirrorServers_EntityId",
                table: "DeploymentMirrorServers");

            migrationBuilder.DropIndex(
                name: "IX_DeployComputerGroups_EntityId",
                table: "DeployComputerGroups");

            migrationBuilder.DropIndex(
                name: "IX_ConsumableItems_EntityId",
                table: "ConsumableItems");

            migrationBuilder.DropIndex(
                name: "IX_Computers_EntityId",
                table: "Computers");

            migrationBuilder.DropIndex(
                name: "IX_ComputerRules_EntityId",
                table: "ComputerRules");

            migrationBuilder.DropIndex(
                name: "IX_CollectDefinitions_EntityId",
                table: "CollectDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_CartridgeItems_EntityId",
                table: "CartridgeItems");

            migrationBuilder.DropIndex(
                name: "IX_Cables_EntityId",
                table: "Cables");

            migrationBuilder.DropIndex(
                name: "IX_Agents_EntityId",
                table: "Agents");

            migrationBuilder.DropColumn(
                name: "EntityId",
                table: "WakeOnLanTasks");

            migrationBuilder.DropColumn(
                name: "IsRecursive",
                table: "WakeOnLanTasks");

            migrationBuilder.DropColumn(
                name: "EntityId",
                table: "TimeSlots");

            migrationBuilder.DropColumn(
                name: "IsRecursive",
                table: "TimeSlots");

            migrationBuilder.DropColumn(
                name: "EntityId",
                table: "SnmpCredentials");

            migrationBuilder.DropColumn(
                name: "IsRecursive",
                table: "SnmpCredentials");

            migrationBuilder.DropColumn(
                name: "EntityId",
                table: "SavedSearches");

            migrationBuilder.DropColumn(
                name: "IsRecursive",
                table: "SavedSearches");

            migrationBuilder.DropColumn(
                name: "EntityId",
                table: "Racks");

            migrationBuilder.DropColumn(
                name: "IsRecursive",
                table: "Racks");

            migrationBuilder.DropColumn(
                name: "EntityId",
                table: "Printers");

            migrationBuilder.DropColumn(
                name: "IsRecursive",
                table: "Printers");

            migrationBuilder.DropColumn(
                name: "EntityId",
                table: "Phones");

            migrationBuilder.DropColumn(
                name: "IsRecursive",
                table: "Phones");

            migrationBuilder.DropColumn(
                name: "EntityId",
                table: "Peripherals");

            migrationBuilder.DropColumn(
                name: "IsRecursive",
                table: "Peripherals");

            migrationBuilder.DropColumn(
                name: "EntityId",
                table: "Pdus");

            migrationBuilder.DropColumn(
                name: "IsRecursive",
                table: "Pdus");

            migrationBuilder.DropColumn(
                name: "EntityId",
                table: "PassiveEquipments");

            migrationBuilder.DropColumn(
                name: "IsRecursive",
                table: "PassiveEquipments");

            migrationBuilder.DropColumn(
                name: "EntityId",
                table: "NotificationTemplates");

            migrationBuilder.DropColumn(
                name: "IsRecursive",
                table: "NotificationTemplates");

            migrationBuilder.DropColumn(
                name: "EntityId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "IsRecursive",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "EntityId",
                table: "NetworkTasks");

            migrationBuilder.DropColumn(
                name: "IsRecursive",
                table: "NetworkTasks");

            migrationBuilder.DropColumn(
                name: "EntityId",
                table: "NetworkEquipments");

            migrationBuilder.DropColumn(
                name: "IsRecursive",
                table: "NetworkEquipments");

            migrationBuilder.DropColumn(
                name: "EntityId",
                table: "IpRanges");

            migrationBuilder.DropColumn(
                name: "IsRecursive",
                table: "IpRanges");

            migrationBuilder.DropColumn(
                name: "EntityId",
                table: "ImportBlacklistEntries");

            migrationBuilder.DropColumn(
                name: "IsRecursive",
                table: "ImportBlacklistEntries");

            migrationBuilder.DropColumn(
                name: "EntityId",
                table: "ImportAssignmentRules");

            migrationBuilder.DropColumn(
                name: "IsRecursive",
                table: "ImportAssignmentRules");

            migrationBuilder.DropColumn(
                name: "EntityId",
                table: "Groups");

            migrationBuilder.DropColumn(
                name: "EntityId",
                table: "Enclosures");

            migrationBuilder.DropColumn(
                name: "IsRecursive",
                table: "Enclosures");

            migrationBuilder.DropColumn(
                name: "EntityId",
                table: "DropdownItems");

            migrationBuilder.DropColumn(
                name: "IsRecursive",
                table: "DropdownItems");

            migrationBuilder.DropColumn(
                name: "EntityId",
                table: "DiscoveredNetworkDevices");

            migrationBuilder.DropColumn(
                name: "IsRecursive",
                table: "DiscoveredNetworkDevices");

            migrationBuilder.DropColumn(
                name: "EntityId",
                table: "DictionaryRules");

            migrationBuilder.DropColumn(
                name: "IsRecursive",
                table: "DictionaryRules");

            migrationBuilder.DropColumn(
                name: "EntityId",
                table: "DeploymentUserInteractionTemplates");

            migrationBuilder.DropColumn(
                name: "IsRecursive",
                table: "DeploymentUserInteractionTemplates");

            migrationBuilder.DropColumn(
                name: "EntityId",
                table: "DeploymentTasks");

            migrationBuilder.DropColumn(
                name: "IsRecursive",
                table: "DeploymentTasks");

            migrationBuilder.DropColumn(
                name: "EntityId",
                table: "DeploymentRules");

            migrationBuilder.DropColumn(
                name: "IsRecursive",
                table: "DeploymentRules");

            migrationBuilder.DropColumn(
                name: "EntityId",
                table: "DeploymentPackages");

            migrationBuilder.DropColumn(
                name: "IsRecursive",
                table: "DeploymentPackages");

            migrationBuilder.DropColumn(
                name: "EntityId",
                table: "DeploymentMirrorServers");

            migrationBuilder.DropColumn(
                name: "IsRecursive",
                table: "DeploymentMirrorServers");

            migrationBuilder.DropColumn(
                name: "EntityId",
                table: "DeployComputerGroups");

            migrationBuilder.DropColumn(
                name: "IsRecursive",
                table: "DeployComputerGroups");

            migrationBuilder.DropColumn(
                name: "EntityId",
                table: "ConsumableItems");

            migrationBuilder.DropColumn(
                name: "IsRecursive",
                table: "ConsumableItems");

            migrationBuilder.DropColumn(
                name: "EntityId",
                table: "Computers");

            migrationBuilder.DropColumn(
                name: "IsRecursive",
                table: "Computers");

            migrationBuilder.DropColumn(
                name: "EntityId",
                table: "ComputerRules");

            migrationBuilder.DropColumn(
                name: "IsRecursive",
                table: "ComputerRules");

            migrationBuilder.DropColumn(
                name: "EntityId",
                table: "CollectDefinitions");

            migrationBuilder.DropColumn(
                name: "IsRecursive",
                table: "CollectDefinitions");

            migrationBuilder.DropColumn(
                name: "EntityId",
                table: "CartridgeItems");

            migrationBuilder.DropColumn(
                name: "IsRecursive",
                table: "CartridgeItems");

            migrationBuilder.DropColumn(
                name: "EntityId",
                table: "Cables");

            migrationBuilder.DropColumn(
                name: "IsRecursive",
                table: "Cables");

            migrationBuilder.DropColumn(
                name: "EntityId",
                table: "Agents");

            migrationBuilder.DropColumn(
                name: "IsRecursive",
                table: "Agents");
        }
    }
}

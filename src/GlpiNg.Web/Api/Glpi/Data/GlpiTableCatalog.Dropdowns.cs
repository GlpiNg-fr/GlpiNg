using GlpiNg.Modules.Inventory.Models;

namespace GlpiNg.Web.Api.Glpi.Data;

public sealed partial class GlpiTableCatalog
{
    /// <summary>
    /// Intitulés et catalogues de composants : GlpiNg les range tous dans <see cref="DropdownItem"/>,
    /// discriminés par <see cref="DropdownType"/> ; GLPI a une table par catégorie.
    /// </summary>
    private void RegisterDropdowns()
    {
        Dropdown("glpi_manufacturers", DropdownType.Manufacturer);
        Dropdown("glpi_computertypes", DropdownType.ComputerType);
        Dropdown("glpi_computermodels", DropdownType.ComputerModel);
        Dropdown("glpi_operatingsystems", DropdownType.OperatingSystem);
        Dropdown("glpi_operatingsystemversions", DropdownType.OperatingSystemVersion);
        Dropdown("glpi_locations", DropdownType.Location, parentColumn: "locations_id");
        Dropdown("glpi_states", DropdownType.Status, parentColumn: "states_id");

        // Catalogues de composants : GLPI nomme la colonne « designation ».
        Dropdown("glpi_deviceprocessors", DropdownType.Processor, nameColumn: "designation");
        Dropdown("glpi_devicememories", DropdownType.Memory, nameColumn: "designation");
        Dropdown("glpi_deviceharddrives", DropdownType.HardDrive, nameColumn: "designation");
        Dropdown("glpi_devicenetworkcards", DropdownType.NetworkCard, nameColumn: "designation");
        Dropdown("glpi_devicegraphiccards", DropdownType.GraphicCard, nameColumn: "designation");
        Dropdown("glpi_devicesoundcards", DropdownType.SoundCard, nameColumn: "designation");
        Dropdown("glpi_devicedrives", DropdownType.Drive, nameColumn: "designation");
        Dropdown("glpi_devicepcis", DropdownType.PciDevice, nameColumn: "designation");
        Dropdown("glpi_devicecameras", DropdownType.Camera, nameColumn: "designation");
        Dropdown("glpi_devicepowersupplies", DropdownType.PowerSupply, nameColumn: "designation");
        Dropdown("glpi_devicebatteries", DropdownType.Battery, nameColumn: "designation");
        Dropdown("glpi_devicecases", DropdownType.Case, nameColumn: "designation");
        Dropdown("glpi_devicemotherboards", DropdownType.Motherboard, nameColumn: "designation");
        Dropdown("glpi_devicegenerics", DropdownType.GenericDevice, nameColumn: "designation");
        Dropdown("glpi_devicecontrols", DropdownType.Controller, nameColumn: "designation");
        Dropdown("glpi_devicefirmwares", DropdownType.Firmware, nameColumn: "designation");
        Dropdown("glpi_devicesensors", DropdownType.Sensor, nameColumn: "designation");
        Dropdown("glpi_devicesimcards", DropdownType.SimCard, nameColumn: "designation");
    }

    private void Dropdown(string table, DropdownType type, string nameColumn = "name", string? parentColumn = null)
    {
        GlpiTableBuilder<DropdownItem> builder = Table<DropdownItem>(table, d => d.Id)
            .Where(d => d.Type == type)
            .Entity(d => d.EntityId, d => d.IsRecursive)
            .Col(nameColumn, d => d.Name)
            .Col("comment", d => d.Comment)
            .OnCreate((d, ctx) => d.Type = type);

        if (parentColumn is not null)
        {
            builder.Fk(parentColumn, d => d.ParentId).Tree(parentColumn, nameColumn);
        }
    }
}

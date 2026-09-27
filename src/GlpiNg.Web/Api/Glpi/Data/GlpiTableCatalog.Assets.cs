using System.Globalization;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using GlpiNg.Modules.Inventory.Models;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Web.Api.Glpi.Data;

public sealed partial class GlpiTableCatalog
{
    private const string Users = "glpi_users";

    /// <summary>Parc : ordinateurs et leurs éléments d'inventaire, autres actifs, consommables, agents.</summary>
    private void RegisterAssets()
    {
        Table<Computer>("glpi_computers", c => c.Id)
            .Entity(c => c.EntityId, c => c.IsRecursive)
            .Col("name", c => c.Name)
            .Col("serial", c => c.SerialNumber)
            .Col("contact", c => c.LastLoggedUser)
            .Named("users_id", c => c.AssignedUser, Users)
            .Fk("locations_id", c => c.LocationId)
            .Fk("states_id", c => c.StatusId)
            .Named("manufacturers_id", c => c.Manufacturer, "glpi_manufacturers")
            .Named("computermodels_id", c => c.Model, "glpi_computermodels")
            // L'inventaire GLPI déduit le type d'ordinateur du châssis remonté par l'agent.
            .Named("computertypes_id", c => c.ChassisType, "glpi_computertypes")
            .Col("uuid", c => c.HardwareUuid)
            .Trash(c => c.IsDeleted)
            .Col("is_dynamic", c => c.AgentId != null, readOnly: true)
            .Col("date_creation", c => c.CreatedAt, readOnly: true)
            .Col("date_mod", c => c.LastInventoryAt ?? c.CreatedAt, readOnly: true)
            .Col("last_inventory_update", c => c.LastInventoryAt, readOnly: true)
            .OnCreate((c, ctx) => c.CreatedAt = Now);

        // Éléments d'inventaire d'un ordinateur : visibles si l'ordinateur l'est.
        ComputerChild<ComputerVolume>("glpi_items_disks", v => v.Id, v => v.ComputerId)
            .Col("name", v => v.Name)
            .Col("device", v => v.Partition)
            .Col("mountpoint", v => v.MountPoint)
            .Named("filesystems_id", v => v.FileSystem, "glpi_filesystems")
            .Col("totalsize", v => v.TotalSizeMb)
            .Col("freesize", v => v.FreeSizeMb)
            .Col("encryption_status", v => v.Encryption != null && v.Encryption != "" ? 1 : 0, readOnly: true)
            .Col("encryption_tool", v => v.Encryption);

        ComputerChild<ComputerAntivirus>("glpi_itemantiviruses", a => a.Id, a => a.ComputerId, withEntity: false)
            .Col("name", a => a.Name)
            .Named("manufacturers_id", a => a.Company, "glpi_manufacturers")
            .Col("antivirus_version", a => a.Version)
            .Col("signature_version", a => a.BaseVersion)
            .Col("is_active", a => a.Enabled ?? false)
            .Col("is_uptodate", a => a.UpToDate ?? false)
            .Computed("date_expiration", (a, ctx) => ParseLooseDate(a.Expiration));

        ComputerChild<ComputerNetworkPort>("glpi_networkports", p => p.Id, p => p.ComputerId)
            .Col("name", p => p.Designation)
            .Col("mac", p => p.MacAddress)
            .Col("instantiation_type", p => p.Type != null && p.Type.ToLower().Contains("wifi") ? "NetworkPortWifi" : "NetworkPortEthernet", readOnly: true)
            .Col("ifmtu", p => p.Mtu)
            .Col("ifspeed", p => p.SpeedMbps)
            .Col("ifstatus", p => p.Status)
            .Col("ifdescr", p => p.Type)
            .Const("logical_number", 0L);

        // Adresses IP : une par port qui en porte une (GLPI les rattache au nom réseau du port).
        Table<ComputerNetworkPort>("glpi_ipaddresses", p => p.Id)
            .Where(ctx => p => p.IpAddress != null && p.IpAddress != "" && ctx.Db.Set<Computer>().Any(c => c.Id == p.ComputerId))
            .Col("name", p => p.IpAddress, readOnly: true)
            .Col("items_id", p => p.Id, readOnly: true)
            .Const("itemtype", "NetworkName")
            .Const("mainitemtype", "Computer")
            .Col("mainitems_id", p => p.ComputerId, readOnly: true)
            .Col("version", p => p.IpAddress!.Contains(':') ? 6 : 4, readOnly: true)
            .Const("is_dynamic", 1L)
            .Const("is_deleted", 0L)
            .Computed("entities_id", (p, ctx) => ParentEntity(ctx, "glpi_computers", p.ComputerId))
            .Preload(PreloadEntities("glpi_computers"))
            .ReadOnly();

        // Système d'exploitation : GlpiNg le porte sur l'ordinateur, GLPI dans une table de liaison.
        Table<Computer>("glpi_items_operatingsystems", c => c.Id)
            .Where(c => c.OperatingSystem != null && c.OperatingSystem != "")
            .Const("itemtype", "Computer")
            .Col("items_id", c => c.Id, readOnly: true)
            .Named("operatingsystems_id", c => c.OperatingSystem, "glpi_operatingsystems")
            .Named("operatingsystemversions_id", c => c.OsVersion, "glpi_operatingsystemversions")
            .Entity(c => c.EntityId, c => c.IsRecursive, readOnly: true)
            .Col("is_dynamic", c => c.AgentId != null, readOnly: true)
            .Const("is_deleted", 0L);

        Table<Computer>("glpi_items_remotemanagements", c => c.Id)
            .Where(c => c.RemoteManagementId != null && c.RemoteManagementId != "")
            .Const("itemtype", "Computer")
            .Col("items_id", c => c.Id, readOnly: true)
            .Col("remoteid", c => c.RemoteManagementId)
            .Col("type", c => c.RemoteManagementType)
            .Col("is_dynamic", c => c.AgentId != null, readOnly: true)
            .Const("is_deleted", 0L);

        // Composants : une table GLPI par type de composant, et un catalogue par type.
        Component("glpi_items_deviceprocessors", ComponentType.Cpu, "deviceprocessors_id", "glpi_deviceprocessors");
        Component("glpi_items_devicememories", ComponentType.Ram, "devicememories_id", "glpi_devicememories", sizeColumn: "size");
        Component("glpi_items_deviceharddrives", ComponentType.Disk, "deviceharddrives_id", "glpi_deviceharddrives", sizeColumn: "capacity");
        Component("glpi_items_devicenetworkcards", ComponentType.NetworkCard, "devicenetworkcards_id", "glpi_devicenetworkcards");
        Component("glpi_items_devicegraphiccards", ComponentType.Gpu, "devicegraphiccards_id", "glpi_devicegraphiccards", sizeColumn: "memory");
        Component("glpi_items_devicemotherboards", ComponentType.Motherboard, "devicemotherboards_id", "glpi_devicemotherboards");
        Component("glpi_items_devicecontrols", ComponentType.Controller, "devicecontrols_id", "glpi_devicecontrols");
        Component("glpi_items_devicesoundcards", ComponentType.SoundCard, "devicesoundcards_id", "glpi_devicesoundcards");
        Component("glpi_items_devicefirmwares", ComponentType.Firmware, "devicefirmwares_id", "glpi_devicefirmwares");

        ComputerChild<ComputerBattery>("glpi_items_devicebatteries", b => b.Id, b => b.ComputerId)
            .Named("devicebatteries_id", b => b.Name, "glpi_devicebatteries", "designation")
            .Col("serial", b => b.Serial)
            .Col("real_capacity", b => b.RealCapacityMwh)
            .Computed("manufacturing_date", (b, ctx) => ParseLooseDate(b.ManufactureDate) is DateTime d ? DateOnly.FromDateTime(d) : null);

        // Moniteurs détectés par l'inventaire : GlpiNg les porte comme périphériques d'un ordinateur.
        ComputerChild<ComputerPeripheral>("glpi_monitors", p => p.Id, p => p.ComputerId, withItem: false)
            .Where(p => p.Kind == PeripheralKind.Monitor)
            .Col("name", p => p.Designation)
            .Col("serial", p => p.Serial)
            .Named("manufacturers_id", p => p.Manufacturer, "glpi_manufacturers")
            .Fk("states_id", p => p.StatusId)
            .ReadOnly();

        Table<Peripheral>("glpi_peripherals", p => p.Id)
            .Entity(p => p.EntityId, p => p.IsRecursive)
            .Col("name", p => p.Name)
            .Fk("states_id", p => p.StatusId)
            .Named("peripheraltypes_id", p => p.Type, "glpi_peripheraltypes")
            .Named("manufacturers_id", p => p.Manufacturer, "glpi_manufacturers")
            .Named("peripheralmodels_id", p => p.Model, "glpi_peripheralmodels")
            .Col("brand", p => p.Brand)
            .Col("serial", p => p.SerialNumber)
            .Col("otherserial", p => p.InventoryNumber)
            .Col("uuid", p => p.Uuid)
            .Named("users_id_tech", p => p.TechnicianInCharge, Users)
            .Named("users_id", p => p.AssignedUser, Users)
            .Col("contact", p => p.Contact)
            .Col("contact_num", p => p.ContactNumber)
            .Col("is_global", p => p.IsGlobalManagement)
            .Col("comment", p => p.Comment)
            .Dates(p => p.CreatedAt, p => p.UpdatedAt, (p, v) => p.CreatedAt = v, (p, v) => p.UpdatedAt = v);

        Table<NetworkEquipment>("glpi_networkequipments", n => n.Id)
            .Entity(n => n.EntityId, n => n.IsRecursive)
            .Col("name", n => n.Name)
            .Fk("states_id", n => n.StatusId)
            .Fk("locations_id", n => n.LocationId)
            .Named("networkequipmenttypes_id", n => n.Type, "glpi_networkequipmenttypes")
            .Named("manufacturers_id", n => n.Manufacturer, "glpi_manufacturers")
            .Named("networkequipmentmodels_id", n => n.Model, "glpi_networkequipmentmodels")
            .Col("serial", n => n.SerialNumber)
            .Col("otherserial", n => n.InventoryNumber)
            .Col("uuid", n => n.Uuid)
            .Named("users_id_tech", n => n.TechnicianInCharge, Users)
            .Named("users_id", n => n.AssignedUser, Users)
            .Col("ram", n => n.MemoryMb)
            .Col("comment", n => n.Comment)
            .Dates(n => n.CreatedAt, n => n.UpdatedAt, (n, v) => n.CreatedAt = v, (n, v) => n.UpdatedAt = v);

        Table<Printer>("glpi_printers", p => p.Id)
            .Entity(p => p.EntityId, p => p.IsRecursive)
            .Col("name", p => p.Name)
            .Fk("states_id", p => p.StatusId)
            .Fk("locations_id", p => p.LocationId)
            .Named("printertypes_id", p => p.Type, "glpi_printertypes")
            .Named("manufacturers_id", p => p.Manufacturer, "glpi_manufacturers")
            .Named("printermodels_id", p => p.Model, "glpi_printermodels")
            .Col("serial", p => p.SerialNumber)
            .Col("otherserial", p => p.InventoryNumber)
            .Col("uuid", p => p.Uuid)
            .Named("users_id_tech", p => p.TechnicianInCharge, Users)
            .Named("users_id", p => p.AssignedUser, Users)
            .Col("init_pages_counter", p => p.InitialPageCount)
            .Col("last_pages_counter", p => p.CurrentPageCount)
            .Col("comment", p => p.Comment)
            .Dates(p => p.CreatedAt, p => p.UpdatedAt, (p, v) => p.CreatedAt = v, (p, v) => p.UpdatedAt = v);

        Table<Phone>("glpi_phones", p => p.Id)
            .Entity(p => p.EntityId, p => p.IsRecursive)
            .Col("name", p => p.Name)
            .Fk("states_id", p => p.StatusId)
            .Fk("locations_id", p => p.LocationId)
            .Named("phonetypes_id", p => p.Type, "glpi_phonetypes")
            .Named("manufacturers_id", p => p.Manufacturer, "glpi_manufacturers")
            .Named("phonemodels_id", p => p.Model, "glpi_phonemodels")
            .Col("serial", p => p.SerialNumber)
            .Col("otherserial", p => p.InventoryNumber)
            .Col("uuid", p => p.Uuid)
            .Named("users_id_tech", p => p.TechnicianInCharge, Users)
            .Named("users_id", p => p.AssignedUser, Users)
            .Col("number_line", p => p.LineCount)
            .Col("comment", p => p.Comment)
            .Dates(p => p.CreatedAt, p => p.UpdatedAt, (p, v) => p.CreatedAt = v, (p, v) => p.UpdatedAt = v);

        Table<Rack>("glpi_racks", r => r.Id)
            .Entity(r => r.EntityId, r => r.IsRecursive)
            .Col("name", r => r.Name)
            .Fk("states_id", r => r.StatusId)
            .Fk("locations_id", r => r.LocationId)
            .Named("racktypes_id", r => r.Type, "glpi_racktypes")
            .Named("manufacturers_id", r => r.Manufacturer, "glpi_manufacturers")
            .Named("rackmodels_id", r => r.Model, "glpi_rackmodels")
            .Col("serial", r => r.SerialNumber)
            .Col("otherserial", r => r.InventoryNumber)
            .Named("users_id_tech", r => r.TechnicianInCharge, Users)
            .Named("users_id", r => r.AssignedUser, Users)
            .Col("number_units", r => r.UnitCount)
            .Col("comment", r => r.Comment)
            .Dates(r => r.CreatedAt, r => r.UpdatedAt, (r, v) => r.CreatedAt = v, (r, v) => r.UpdatedAt = v);

        Table<Enclosure>("glpi_enclosures", e => e.Id)
            .Entity(e => e.EntityId, e => e.IsRecursive)
            .Col("name", e => e.Name)
            .Fk("states_id", e => e.StatusId)
            .Fk("locations_id", e => e.LocationId)
            .Named("manufacturers_id", e => e.Manufacturer, "glpi_manufacturers")
            .Named("enclosuremodels_id", e => e.Model, "glpi_enclosuremodels")
            .Col("serial", e => e.SerialNumber)
            .Col("otherserial", e => e.InventoryNumber)
            .Named("users_id_tech", e => e.TechnicianInCharge, Users)
            .Named("users_id", e => e.AssignedUser, Users)
            .Col("comment", e => e.Comment)
            .Dates(e => e.CreatedAt, e => e.UpdatedAt, (e, v) => e.CreatedAt = v, (e, v) => e.UpdatedAt = v);

        Table<Pdu>("glpi_pdus", p => p.Id)
            .Entity(p => p.EntityId, p => p.IsRecursive)
            .Col("name", p => p.Name)
            .Fk("states_id", p => p.StatusId)
            .Fk("locations_id", p => p.LocationId)
            .Named("pdutypes_id", p => p.Type, "glpi_pdutypes")
            .Named("manufacturers_id", p => p.Manufacturer, "glpi_manufacturers")
            .Named("pdumodels_id", p => p.Model, "glpi_pdumodels")
            .Col("serial", p => p.SerialNumber)
            .Col("otherserial", p => p.InventoryNumber)
            .Named("users_id_tech", p => p.TechnicianInCharge, Users)
            .Named("users_id", p => p.AssignedUser, Users)
            .Col("comment", p => p.Comment)
            .Dates(p => p.CreatedAt, p => p.UpdatedAt, (p, v) => p.CreatedAt = v, (p, v) => p.UpdatedAt = v);

        Table<PassiveEquipment>("glpi_passivedcequipments", p => p.Id)
            .Entity(p => p.EntityId, p => p.IsRecursive)
            .Col("name", p => p.Name)
            .Fk("states_id", p => p.StatusId)
            .Fk("locations_id", p => p.LocationId)
            .Named("passivedcequipmenttypes_id", p => p.Type, "glpi_passivedcequipmenttypes")
            .Named("manufacturers_id", p => p.Manufacturer, "glpi_manufacturers")
            .Named("passivedcequipmentmodels_id", p => p.Model, "glpi_passivedcequipmentmodels")
            .Col("serial", p => p.SerialNumber)
            .Col("otherserial", p => p.InventoryNumber)
            .Named("users_id_tech", p => p.TechnicianInCharge, Users)
            .Named("users_id", p => p.AssignedUser, Users)
            .Col("comment", p => p.Comment)
            .Dates(p => p.CreatedAt, p => p.UpdatedAt, (p, v) => p.CreatedAt = v, (p, v) => p.UpdatedAt = v);

        Table<Cable>("glpi_cables", c => c.Id)
            .Entity(c => c.EntityId, c => c.IsRecursive)
            .Col("name", c => c.Name)
            .Fk("states_id", c => c.StatusId)
            .Named("cabletypes_id", c => c.Type, "glpi_cabletypes")
            .Col("color", c => c.Color)
            .Col("comment", c => c.Comment)
            .Computed("itemtype_endpoint_a", (c, ctx) => CableItemtype(c.EndpointAType))
            .Col("items_id_endpoint_a", c => c.EndpointAId)
            .Computed("itemtype_endpoint_b", (c, ctx) => c.EndpointBType is CableEndpointType b ? CableItemtype(b) : null)
            .Col("items_id_endpoint_b", c => c.EndpointBId ?? 0, readOnly: true)
            .Dates(c => c.CreatedAt, c => c.UpdatedAt, (c, v) => c.CreatedAt = v, (c, v) => c.UpdatedAt = v);

        Table<CartridgeItem>("glpi_cartridgeitems", c => c.Id)
            .Entity(c => c.EntityId, c => c.IsRecursive)
            .Col("name", c => c.Name)
            .Col("ref", c => c.Reference)
            .Fk("locations_id", c => c.LocationId)
            .Named("cartridgeitemtypes_id", c => c.Type, "glpi_cartridgeitemtypes")
            .Named("manufacturers_id", c => c.Manufacturer, "glpi_manufacturers")
            .Named("users_id_tech", c => c.TechnicianInCharge, Users)
            .Col("alarm_threshold", c => c.AlertThreshold)
            .Col("comment", c => c.Comment)
            .Dates(c => c.CreatedAt, c => c.UpdatedAt, (c, v) => c.CreatedAt = v, (c, v) => c.UpdatedAt = v);

        Table<Cartridge>("glpi_cartridges", c => c.Id)
            .Where(ctx => c => ctx.Db.Set<CartridgeItem>().Any(i => i.Id == c.CartridgeItemId))
            .Entity(c => c.CartridgeItem!.EntityId, readOnly: true)
            .Col("cartridgeitems_id", c => c.CartridgeItemId)
            .Fk("printers_id", c => c.PrinterId)
            .Col("date_in", c => c.DateIn)
            .Col("date_use", c => c.DateUse)
            .Col("date_out", c => c.DateOut)
            .OnCreate((c, ctx) => c.DateIn = Now);

        Table<ConsumableItem>("glpi_consumableitems", c => c.Id)
            .Entity(c => c.EntityId, c => c.IsRecursive)
            .Col("name", c => c.Name)
            .Col("ref", c => c.Reference)
            .Fk("locations_id", c => c.LocationId)
            .Named("consumableitemtypes_id", c => c.Type, "glpi_consumableitemtypes")
            .Named("manufacturers_id", c => c.Manufacturer, "glpi_manufacturers")
            .Named("users_id_tech", c => c.TechnicianInCharge, Users)
            .Col("alarm_threshold", c => c.AlertThreshold)
            .Col("comment", c => c.Comment)
            .Dates(c => c.CreatedAt, c => c.UpdatedAt, (c, v) => c.CreatedAt = v, (c, v) => c.UpdatedAt = v);

        Table<Consumable>("glpi_consumables", c => c.Id)
            .Where(ctx => c => ctx.Db.Set<ConsumableItem>().Any(i => i.Id == c.ConsumableItemId))
            .Col("consumableitems_id", c => c.ConsumableItemId)
            .Col("date_in", c => c.DateIn)
            .Col("date_out", c => c.DateOut)
            .OnCreate((c, ctx) => c.DateIn = Now);

        // Carte SIM : GLPI n'en fait pas un actif mais un composant (Item_DeviceSimcard), sans ordinateur ici.
        Table<SimCard>("glpi_items_devicesimcards", s => s.Id)
            .Entity(s => s.EntityId, s => s.IsRecursive)
            .Const("itemtype", "")
            .Const("items_id", 0L)
            .Col("serial", s => s.SerialNumber)
            .Col("otherserial", s => s.InventoryNumber)
            .Fk("states_id", s => s.StatusId)
            .Fk("locations_id", s => s.LocationId)
            .Named("users_id", s => s.AssignedUser, Users)
            .Named("users_id_tech", s => s.TechnicianInCharge, Users)
            .Col("pin", s => s.Pin)
            .Col("pin2", s => s.Pin2)
            .Col("puk", s => s.Puk)
            .Col("puk2", s => s.Puk2)
            .Col("msin", s => s.Msin)
            .Col("comment", s => s.Comment)
            .Const("is_dynamic", 0L)
            .Const("is_deleted", 0L)
            .OnCreate((s, ctx) => s.CreatedAt = Now)
            .OnSave((s, ctx) => s.UpdatedAt = Now);

        Table<GlpiAgent>("glpi_agents", a => a.Id)
            .Entity(a => a.EntityId, a => a.IsRecursive, readOnly: true)
            .Col("deviceid", a => a.DeviceId, readOnly: true)
            .Col("name", a => a.AgentName ?? a.Hostname, readOnly: true)
            .Const("agenttypes_id", 1L)
            .Col("last_contact", a => a.LastContactAt, readOnly: true)
            .Col("version", a => a.AgentVersion, readOnly: true)
            .Const("itemtype", "Computer")
            .Col("items_id", a => a.Computer != null ? a.Computer.Id : 0, readOnly: true)
            .Col("useragent", a => a.LastUserAgent, readOnly: true)
            .Col("tag", a => a.Tag, readOnly: true)
            .Col("remote_addr", a => a.LastContactIp, readOnly: true)
            .ReadOnly();
    }

    /// <summary>Élément d'inventaire d'un ordinateur (itemtype = Computer, items_id = son id).</summary>
    private GlpiTableBuilder<T> ComputerChild<T>(string table, Expression<Func<T, int>> id, Expression<Func<T, int>> computerId,
        bool withEntity = true, bool withItem = true) where T : class
    {
        Func<T, int> getComputerId = computerId.Compile();
        ParameterExpression p = computerId.Parameters[0];

        GlpiTableBuilder<T> builder = Table(table, id)
            .Where(ctx => VisibleComputer(ctx, computerId))
            .Const("is_dynamic", 1L)
            .Const("is_deleted", 0L)
            .BeforeSave(async (e, ctx, input, ct) =>
            {
                int parent = getComputerId(e);
                if (!await ctx.Db.Set<Computer>().AnyAsync(c => c.Id == parent, ct))
                {
                    throw GlpiWriteException.Right();
                }
            });

        if (withItem)
        {
            builder.Const("itemtype", "Computer").Col("items_id", computerId);
        }
        if (withEntity)
        {
            builder.Computed("entities_id", (e, ctx) => ParentEntity(ctx, "glpi_computers", getComputerId(e)))
                .Preload(PreloadEntities("glpi_computers"));
        }
        return builder;
    }

    private static Expression<Func<T, bool>> VisibleComputer<T>(GlpiDataContext ctx, Expression<Func<T, int>> computerId)
        => VisibleParent<T, Computer>(ctx, computerId);

    /// <summary>
    /// <c>x =&gt; ctx.Db.Set&lt;TParent&gt;().Any(p =&gt; p.Id == parentId(x))</c> : l'objet parent est visible
    /// (cloisonnement par entité compris, puisque le filtre global s'applique aussi aux sous-requêtes).
    /// </summary>
    private static Expression<Func<T, bool>> VisibleParent<T, TParent>(GlpiDataContext ctx, Expression<Func<T, int>> parentId) where TParent : class
    {
        ParameterExpression x = parentId.Parameters[0];
        ParameterExpression p = Expression.Parameter(typeof(TParent), "p");
        Expression<Func<TParent, bool>> match = Expression.Lambda<Func<TParent, bool>>(
            Expression.Equal(Expression.Property(p, "Id"), parentId.Body), p);
        // ctx.Db.Set<TParent>() écrit comme dans un lambda ordinaire (accès membre sur une
        // constante) : EF y reconnaît la racine de requête du même contexte, filtres globaux compris.
        Expression set = Expression.Call(
            Expression.Property(Expression.Constant(ctx), nameof(GlpiDataContext.Db)),
            nameof(Microsoft.EntityFrameworkCore.DbContext.Set), [typeof(TParent)]);
        Expression any = Expression.Call(typeof(Queryable), nameof(Queryable.Any), [typeof(TParent)], set, match);
        return Expression.Lambda<Func<T, bool>>(any, x);
    }

    private void Component(string table, ComponentType type, string deviceColumn, string deviceTable, string? sizeColumn = null)
    {
        GlpiTableBuilder<ComputerComponent> builder = ComputerChild<ComputerComponent>(table, c => c.Id, c => c.ComputerId)
            .Where(c => c.Type == type)
            .Named(deviceColumn, c => c.Designation, deviceTable, "designation")
            .Col("serial", c => c.Serial)
            .OnCreate((c, ctx) => c.Type = type);
        if (sizeColumn is not null)
        {
            builder.Computed(sizeColumn, (c, ctx) => LeadingNumber(c.Capacity), (c, v, ctx) => c.Capacity = GlpiValue.ToText(v));
        }
    }

    /// <summary>Entité GLPI d'un parent, lue dans le cache préchargé par <see cref="PreloadEntities"/>.</summary>
    private static object? ParentEntity(GlpiDataContext ctx, string parentTable, int parentId)
        => ctx.Cache.TryGetValue("entities:" + parentTable, out object? map)
            ? ((Dictionary<int, object?>)map).GetValueOrDefault(parentId, 0L)
            : 0L;

    private static Func<GlpiDataContext, CancellationToken, Task> PreloadEntities(string parentTable) => async (ctx, ct) =>
    {
        string key = "entities:" + parentTable;
        if (!ctx.Cache.ContainsKey(key))
        {
            ctx.Cache[key] = await ctx.Table(parentTable).GetColumnAsync(ctx, "entities_id", null, ct);
        }
    };

    private static string CableItemtype(CableEndpointType type) => type switch
    {
        CableEndpointType.PassiveEquipment => "PassiveDCEquipment",
        _ => type.ToString(),
    };

    private static long? LeadingNumber(string? text)
    {
        Match m = Regex.Match(text ?? string.Empty, @"\d+");
        return m.Success && long.TryParse(m.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long n) ? n : null;
    }

    private static DateTime? ParseLooseDate(string? text)
        => GlpiValue.TryToDateTime(text, out DateTime d) ? d
            : DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime any) ? any : null;
}

/// <summary>Colonnes date_creation / date_mod des modèles qui portent CreatedAt / UpdatedAt.</summary>
internal static class GlpiTableBuilderDates
{
    public static GlpiTableBuilder<T> Dates<T>(this GlpiTableBuilder<T> builder,
        Expression<Func<T, DateTime>> created, Expression<Func<T, DateTime?>> updated,
        Action<T, DateTime> setCreated, Action<T, DateTime> setUpdated) where T : class
    {
        ParameterExpression p = created.Parameters[0];
        Expression<Func<T, DateTime>> modified = Expression.Lambda<Func<T, DateTime>>(
            Expression.Coalesce(EfGlpiTable<T>.Replace(updated, p), created.Body), p);

        return builder
            .Col("date_creation", created, readOnly: true)
            .Col("date_mod", modified, readOnly: true)
            .OnCreate((e, ctx) => setCreated(e, DateTime.UtcNow))
            .OnSave((e, ctx) => setUpdated(e, DateTime.UtcNow));
    }

    /// <summary>Variante pour les modèles dont UpdatedAt n'est pas nullable.</summary>
    public static GlpiTableBuilder<T> Dates<T>(this GlpiTableBuilder<T> builder,
        Expression<Func<T, DateTime>> created, Expression<Func<T, DateTime>> updated,
        Action<T, DateTime> setCreated, Action<T, DateTime> setUpdated) where T : class
        => builder
            .Col("date_creation", created, readOnly: true)
            .Col("date_mod", updated, readOnly: true)
            .OnCreate((e, ctx) => setCreated(e, DateTime.UtcNow))
            .OnSave((e, ctx) => setUpdated(e, DateTime.UtcNow));
}

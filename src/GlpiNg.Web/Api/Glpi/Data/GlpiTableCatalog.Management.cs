using GlpiNg.Modules.Management.Models;
using GlpiNg.Web.Models.Documents;

namespace GlpiNg.Web.Api.Glpi.Data;

public sealed partial class GlpiTableCatalog
{
    /// <summary>Gestion : tiers, contrats, budgets, licences, lignes, certificats... et documents.</summary>
    private void RegisterManagement()
    {
        Table<Supplier>("glpi_suppliers", s => s.Id)
            .Entity(s => s.EntityId, s => s.IsRecursive)
            .Col("name", s => s.Name)
            .Named("suppliertypes_id", s => s.Type, "glpi_suppliertypes")
            .Col("registration_number", s => s.RegistrationNumber)
            .Col("address", s => s.Address)
            .Col("postcode", s => s.PostCode)
            .Col("town", s => s.Town)
            .Col("country", s => s.Country)
            .Col("website", s => s.Website)
            .Col("phonenumber", s => s.Phone)
            .Col("fax", s => s.Fax)
            .Col("email", s => s.Email)
            .Col("comment", s => s.Comment)
            .Col("is_active", s => s.IsActive)
            .Dates(s => s.CreatedAt, s => s.UpdatedAt, (s, v) => s.CreatedAt = v, (s, v) => s.UpdatedAt = v)
            .OnCreate((s, ctx) => s.IsActive = true);

        Table<Contact>("glpi_contacts", c => c.Id)
            .Entity(c => c.EntityId, c => c.IsRecursive)
            .Col("name", c => c.Name)
            .Col("firstname", c => c.FirstName)
            .Named("contacttypes_id", c => c.Type, "glpi_contacttypes")
            .Named("usertitles_id", c => c.Title, "glpi_usertitles")
            .Col("phone", c => c.Phone)
            .Col("phone2", c => c.Phone2)
            .Col("mobile", c => c.Mobile)
            .Col("fax", c => c.Fax)
            .Col("email", c => c.Email)
            .Col("address", c => c.Address)
            .Col("postcode", c => c.PostCode)
            .Col("town", c => c.Town)
            .Col("country", c => c.Country)
            .Col("comment", c => c.Comment)
            .Dates(c => c.CreatedAt, c => c.UpdatedAt, (c, v) => c.CreatedAt = v, (c, v) => c.UpdatedAt = v)
            .OnCreate((c, ctx) => c.IsActive = true);

        Table<SupplierContact>("glpi_contacts_suppliers", l => l.Id)
            .Where(ctx => l => ctx.Db.Set<Supplier>().Any(s => s.Id == l.SupplierId))
            .Col("suppliers_id", l => l.SupplierId)
            .Col("contacts_id", l => l.ContactId);

        Table<Contract>("glpi_contracts", c => c.Id)
            .Entity(c => c.EntityId, c => c.IsRecursive)
            .Col("name", c => c.Name)
            .Col("num", c => c.Number)
            .Named("contracttypes_id", c => c.Type, "glpi_contracttypes")
            .Col("begin_date", c => c.StartDate, wallClock: true)
            .Col("duration", c => c.DurationMonths)
            .Col("notice", c => c.NoticeMonths)
            .Col("periodicity", c => c.Periodicity)
            .Col("billing", c => c.BillingPeriodicity)
            .Col("renewal", c => c.Renewal)
            .Col("accounting_number", c => c.AccountNumber)
            .Col("comment", c => c.Comment)
            .Dates(c => c.CreatedAt, c => c.UpdatedAt, (c, v) => c.CreatedAt = v, (c, v) => c.UpdatedAt = v)
            .OnCreate((c, ctx) => c.IsActive = true);

        Table<ContractSupplier>("glpi_contracts_suppliers", l => l.Id)
            .Where(ctx => l => ctx.Db.Set<Contract>().Any(c => c.Id == l.ContractId))
            .Col("suppliers_id", l => l.SupplierId)
            .Col("contracts_id", l => l.ContractId);

        Table<ContractCost>("glpi_contractcosts", c => c.Id)
            .Where(ctx => c => ctx.Db.Set<Contract>().Any(k => k.Id == c.ContractId))
            .Entity(c => c.Contract!.EntityId, readOnly: true)
            .Col("contracts_id", c => c.ContractId)
            .Col("name", c => c.Name)
            .Col("begin_date", c => c.StartDate, wallClock: true)
            .Col("end_date", c => c.EndDate, wallClock: true)
            .Col("cost", c => c.Amount)
            .Fk("budgets_id", c => c.BudgetId);

        Table<Budget>("glpi_budgets", b => b.Id)
            .Entity(b => b.EntityId, b => b.IsRecursive)
            .Col("name", b => b.Name)
            .Named("budgettypes_id", b => b.Type, "glpi_budgettypes")
            .Col("begin_date", b => b.StartDate, wallClock: true)
            .Col("end_date", b => b.EndDate, wallClock: true)
            .Col("value", b => b.Amount)
            .Col("comment", b => b.Comment)
            .Dates(b => b.CreatedAt, b => b.UpdatedAt, (b, v) => b.CreatedAt = v, (b, v) => b.UpdatedAt = v)
            .OnCreate((b, ctx) => b.IsActive = true);

        Table<SoftwareLicense>("glpi_softwarelicenses", l => l.Id)
            .Entity(l => l.EntityId, l => l.IsRecursive)
            .Col("name", l => l.Name)
            .Named("softwarelicensetypes_id", l => l.Type, "glpi_softwarelicensetypes")
            // Dans GLPI, « serial » porte la clé de licence et « otherserial » le numéro d'inventaire.
            .Col("serial", l => l.LicenseKey)
            .Col("otherserial", l => l.SerialNumber)
            .Col("number", l => l.Seats)
            .Col("expire", l => l.ExpirationDate, wallClock: true)
            .Col("is_valid", l => l.Seats <= 0 || l.UsedSeats <= l.Seats, readOnly: true)
            .Col("comment", l => l.Comment)
            .Dates(l => l.CreatedAt, l => l.UpdatedAt, (l, v) => l.CreatedAt = v, (l, v) => l.UpdatedAt = v)
            .OnCreate((l, ctx) => l.IsActive = true);

        Table<PhoneLine>("glpi_lines", l => l.Id)
            .Entity(l => l.EntityId, l => l.IsRecursive)
            .Col("name", l => l.Name)
            .Col("caller_num", l => l.Number)
            .Named("linetypes_id", l => l.Type, "glpi_linetypes")
            .Named("users_id", l => l.AssignedUser, Users)
            .Col("comment", l => l.Comment)
            .Dates(l => l.CreatedAt, l => l.UpdatedAt, (l, v) => l.CreatedAt = v, (l, v) => l.UpdatedAt = v)
            .OnCreate((l, ctx) => l.IsActive = true);

        Table<Certificate>("glpi_certificates", c => c.Id)
            .Entity(c => c.EntityId, c => c.IsRecursive)
            .Col("name", c => c.Name)
            .Named("certificatetypes_id", c => c.Type, "glpi_certificatetypes")
            .Col("dns_name", c => c.DnsName)
            .Col("serial", c => c.SerialNumber)
            // GLPI range l'autorité émettrice dans le fabricant (« Fabricant (AC racine) »).
            .Named("manufacturers_id", c => c.Issuer, "glpi_manufacturers")
            .Col("date_expiration", c => c.ExpirationDate, wallClock: true)
            .Col("comment", c => c.Comment)
            .Dates(c => c.CreatedAt, c => c.UpdatedAt, (c, v) => c.CreatedAt = v, (c, v) => c.UpdatedAt = v)
            .OnCreate((c, ctx) => c.IsActive = true);

        Table<Domain>("glpi_domains", d => d.Id)
            .Entity(d => d.EntityId, d => d.IsRecursive)
            .Col("name", d => d.Name)
            .Named("domaintypes_id", d => d.Type, "glpi_domaintypes")
            .Col("date_domaincreation", d => d.CreationDate, wallClock: true)
            .Col("date_expiration", d => d.ExpirationDate, wallClock: true)
            .Col("is_active", d => d.IsActive)
            .Col("comment", d => d.Comment)
            .Dates(d => d.CreatedAt, d => d.UpdatedAt, (d, v) => d.CreatedAt = v, (d, v) => d.UpdatedAt = v)
            .OnCreate((d, ctx) => d.IsActive = true);

        Table<Datacenter>("glpi_datacenters", d => d.Id)
            .Entity(d => d.EntityId, d => d.IsRecursive)
            .Col("name", d => d.Name)
            .Named("locations_id", d => d.Location, "glpi_locations")
            .Dates(d => d.CreatedAt, d => d.UpdatedAt, (d, v) => d.CreatedAt = v, (d, v) => d.UpdatedAt = v)
            .OnCreate((d, ctx) => d.IsActive = true);

        Table<Cluster>("glpi_clusters", c => c.Id)
            .Entity(c => c.EntityId, c => c.IsRecursive)
            .Col("name", c => c.Name)
            .Named("clustertypes_id", c => c.Type, "glpi_clustertypes")
            .Col("version", c => c.Version)
            .Col("comment", c => c.Comment)
            .Dates(c => c.CreatedAt, c => c.UpdatedAt, (c, v) => c.CreatedAt = v, (c, v) => c.UpdatedAt = v)
            .OnCreate((c, ctx) => c.IsActive = true);

        Table<Appliance>("glpi_appliances", a => a.Id)
            .Entity(a => a.EntityId, a => a.IsRecursive)
            .Col("name", a => a.Name)
            .Named("appliancetypes_id", a => a.Type, "glpi_appliancetypes")
            .Named("applianceenvironments_id", a => a.Environment, "glpi_applianceenvironments")
            .Named("users_id", a => a.OwnerName, Users)
            .Col("comment", a => a.Comment)
            .Dates(a => a.CreatedAt, a => a.UpdatedAt, (a, v) => a.CreatedAt = v, (a, v) => a.UpdatedAt = v)
            .OnCreate((a, ctx) => a.IsActive = true);

        Table<DatabaseInstance>("glpi_databaseinstances", d => d.Id)
            .Entity(d => d.EntityId, d => d.IsRecursive)
            .Col("name", d => d.Name)
            .Named("databaseinstancetypes_id", d => d.Engine, "glpi_databaseinstancetypes")
            .Col("version", d => d.Version)
            .Col("port", d => d.Port)
            .Col("path", d => d.Path)
            .Col("is_onbackup", d => d.IsBackedUp)
            .Col("is_active", d => d.IsActive)
            .Col("comment", d => d.Comment)
            .Dates(d => d.CreatedAt, d => d.UpdatedAt, (d, v) => d.CreatedAt = v, (d, v) => d.UpdatedAt = v)
            .OnCreate((d, ctx) => d.IsActive = true);

        Table<Document>("glpi_documents", d => d.Id)
            .Entity(d => d.EntityId, d => d.IsRecursive)
            .Col("name", d => d.Name)
            .Col("filename", d => d.FileName, readOnly: true)
            .Col("filepath", d => d.StoragePath, readOnly: true)
            .Col("mime", d => d.MimeType, readOnly: true)
            .Fk("documentcategories_id", d => d.CategoryId)
            .Col("comment", d => d.Comment)
            .Col("link", d => d.Link)
            .Fk("users_id", d => d.AuthorUserId, readOnly: true)
            .Col("sha1sum", d => d.SourceSha1, readOnly: true)
            .Col("date_creation", d => d.CreatedAt, readOnly: true)
            .Col("date_mod", d => d.CreatedAt, readOnly: true)
            .OnCreate((d, ctx) =>
            {
                d.CreatedAt = Now;
                d.AuthorUserId = ctx.UserId;
                d.AuthorName = ctx.UserName;
            });

        Table<DocumentItem>("glpi_documents_items", di => di.Id)
            .Where(ctx => di => ctx.Db.Set<Document>().Any(d => d.Id == di.DocumentId))
            .Entity(di => di.Document!.EntityId, readOnly: true)
            .Col("documents_id", di => di.DocumentId)
            .Col("items_id", di => di.ItemId)
            .Col("itemtype", di => di.ItemType)
            .Col("date_creation", di => di.AttachedAt, readOnly: true)
            .Col("date_mod", di => di.AttachedAt, readOnly: true)
            .Col("date", di => di.AttachedAt, readOnly: true)
            .OnCreate((di, ctx) => di.AttachedAt = Now);

        Table<DocumentCategory>("glpi_documentcategories", c => c.Id)
            .Col("name", c => c.Name)
            .Col("comment", c => c.Comment)
            .Fk("documentcategories_id", c => c.ParentId)
            .Tree("documentcategories_id")
            .Col("date_creation", c => c.CreatedAt, readOnly: true)
            .Col("date_mod", c => c.CreatedAt, readOnly: true)
            .OnCreate((c, ctx) => c.CreatedAt = Now);
    }
}

using System.Linq.Expressions;
using GlpiNg.Web.Models;
using Microsoft.AspNetCore.Identity;

namespace GlpiNg.Web.Api.Glpi.Data;

public sealed partial class GlpiTableCatalog
{
    private static readonly PasswordHasher<GlpiUser> UserPasswordHasher = new();

    /// <summary>Colonne interne de glpi_users : nom affiché (hors schéma GLPI).</summary>
    private const string DisplayNameColumn = "_displayname";

    /// <summary>Utilisateurs, groupes, entités, profils et habilitations.</summary>
    private void RegisterAdministration()
    {
        Table<GlpiUser>("glpi_users", u => u.Id)
            .Col("name", u => u.UserName)
            .Col("realname", u => u.LastName)
            .Col("firstname", u => u.FirstName)
            .Col("phone", u => u.Phone)
            .Named("locations_id", u => u.Location, "glpi_locations")
            .Col("language", u => u.Language)
            .Col("list_limit", u => u.ItemsPerPage)
            .Col("is_active", u => u.IsActive)
            // GLPI : 1 = base GLPI, 3 = annuaire LDAP (Auth::DB_GLPI, Auth::LDAP).
            .Col("authtype", u => u.AuthSource == UserAuthSource.Ldap ? 3 : 1, readOnly: true)
            .Fk("auths_id", u => u.LdapServerId)
            .Col("user_dn", u => u.ExternalDn, readOnly: true)
            .Col("timezone", u => u.Timezone)
            .Col("palette", u => u.ColorPalette)
            .Col("highcontrast_css", u => u.HighContrast ?? false)
            .Col("is_ids_visible", u => u.ShowGlpiIds ?? false)
            .Col("csv_delimiter", u => u.CsvDelimiter)
            .Col("show_count_on_tabs", u => u.ShowCounters ?? true)
            .Col("notification_to_myself", u => u.NotifyOnMyChanges ?? true)
            .Col("2fa_unenforced", u => u.TwoFactorAuthDisabled)
            .Col("date_creation", u => u.CreatedAt, readOnly: true)
            .Col("date_mod", u => u.CreatedAt, readOnly: true)
            .Col("api_token_date", u => u.ApiTokenDate, readOnly: true)
            // Hors table GLPI (donc jamais rendue) : sert à retrouver l'auteur d'un suivi ou d'une
            // tâche, que GlpiNg fige sous son nom affiché.
            .Col(DisplayNameColumn, u => u.DisplayName ?? u.UserName, readOnly: true)
            .OnCreate((u, ctx) =>
            {
                u.CreatedAt = Now;
                u.IsActive = true;
                u.PasswordHash = string.Empty;
            })
            .BeforeSave((u, ctx, input, ct) =>
            {
                // Champs d'écriture propres à User::prepareInputForAdd/Update de GLPI.
                string? password = input.TryGetValue("password", out var p) ? GlpiValue.ToText(GlpiValue.FromJson(p)) : null;
                string? confirm = input.TryGetValue("password2", out var p2) ? GlpiValue.ToText(GlpiValue.FromJson(p2)) : null;
                if (!string.IsNullOrEmpty(password))
                {
                    if (confirm is not null && confirm != password)
                    {
                        throw new GlpiWriteException("Error: the two passwords do not match");
                    }
                    u.PasswordHash = UserPasswordHasher.HashPassword(u, password);
                }
                if (input.TryGetValue("_useremails", out var emails))
                {
                    u.Email = emails switch
                    {
                        System.Text.Json.Nodes.JsonArray a => a.Select(e => GlpiValue.ToText(GlpiValue.FromJson(e))).FirstOrDefault(e => !string.IsNullOrWhiteSpace(e)),
                        System.Text.Json.Nodes.JsonObject o => o.Select(e => GlpiValue.ToText(GlpiValue.FromJson(e.Value))).FirstOrDefault(e => !string.IsNullOrWhiteSpace(e)),
                        _ => GlpiValue.ToText(GlpiValue.FromJson(emails)),
                    };
                }
                return Task.CompletedTask;
            });

        // Adresse de courriel principale : GLPI la range dans sa propre table.
        Table<GlpiUser>("glpi_useremails", u => u.Id)
            .Where(u => u.Email != null && u.Email != "")
            .Col("users_id", u => u.Id, readOnly: true)
            .Col("email", u => u.Email)
            .Const("is_default", 1L)
            .Const("is_dynamic", 0L)
            .ReadOnly();

        Table<GlpiGroup>("glpi_groups", g => g.Id)
            .Entity(g => g.EntityId, g => g.IsRecursive)
            .Col("name", g => g.Name)
            .Col("code", g => g.Code)
            .Col("comment", g => g.Comment)
            .Fk("groups_id", g => g.ParentId)
            .Tree("groups_id")
            .Col("is_requester", g => g.VisibleAsRequester)
            .Col("is_watcher", g => g.VisibleAsObserver)
            .Col("is_assign", g => g.VisibleAsAssignee)
            .Col("is_task", g => g.VisibleAsTask)
            .Col("is_notify", g => g.CanBeNotified)
            .Col("is_itemgroup", g => g.CanContainItems)
            .Col("is_usergroup", g => g.CanContainUsers)
            .Col("is_manager", g => g.CanBeProjectSupervisor)
            .Col("2fa_enforced", g => g.TwoFactorAuthRequired)
            .Col("date_creation", g => g.CreatedAt, readOnly: true)
            .Col("date_mod", g => g.UpdatedAt, readOnly: true)
            .OnCreate((g, ctx) =>
            {
                g.CreatedAt = Now;
                g.VisibleAsRequester = g.VisibleAsObserver = g.VisibleAsAssignee = g.VisibleAsTask = true;
                g.CanBeNotified = g.CanContainItems = g.CanContainUsers = g.CanBeProjectSupervisor = true;
            })
            .OnSave((g, ctx) => g.UpdatedAt = Now);

        // Appartenance à un groupe : clé composée côté GlpiNg, identifiant synthétique côté GLPI.
        // ponytail: id = users_id * 32768 + groups_id, unique tant qu'il y a moins de 32768 groupes
        // et 65536 utilisateurs ; passer à une colonne Id si ces volumes sont atteints.
        Table<GlpiGroupUser>("glpi_groups_users", gu => gu.UserId * 32768 + gu.GroupId)
            .Col("users_id", gu => gu.UserId)
            .Col("groups_id", gu => gu.GroupId)
            .Const("is_dynamic", 0L)
            .Const("is_manager", 0L)
            .Const("is_userdelegate", 0L);

        EfGlpiTable<Models.GlpiEntity> entities = Table<Models.GlpiEntity>("glpi_entities", e => e.Id)
            // Une entité n'est visible que dans le périmètre de la session, comme
            // getEntitiesRestrictCriteria('glpi_entities') dans GLPI.
            .Where(ctx => e => ctx.Scope.IsUnrestricted || ctx.Scope.VisibleEntityIds.Contains(e.Id))
            .Col("name", e => e.Name)
            .Custom(new GlpiColumnMap<Models.GlpiEntity>
            {
                // Parent : la racine n'en a pas (null), ses filles ont 0.
                Name = "entities_id",
                Expr = (Expression<Func<Models.GlpiEntity, int?>>)(e => e.ParentId),
                Read = (e, v, ctx) => v is int parent ? (long)ctx.ToApiEntity(parent) : null,
                Write = (e, v, ctx) =>
                {
                    if (GlpiValue.TryToLong(v, out long parent) && parent >= 0)
                    {
                        e.ParentId = ctx.FromApiEntity(parent);
                    }
                },
            })
            .Tree("entities_id")
            .Col("comment", e => e.Comment)
            .Col("registration_number", e => e.Registration)
            .Col("address", e => e.Address)
            .Col("postcode", e => e.Postcode)
            .Col("town", e => e.Town)
            .Col("state", e => e.State)
            .Col("country", e => e.Country)
            .Col("website", e => e.Website)
            .Col("phonenumber", e => e.Phone)
            .Col("fax", e => e.Fax)
            .Col("email", e => e.Email)
            .Col("tag", e => e.AssignmentTag)
            .Col("mail_domain", e => e.AssignmentEmailDomain)
            .Col("latitude", e => e.Latitude)
            .Col("longitude", e => e.Longitude)
            .Col("altitude", e => e.Altitude)
            .Col("agent_base_url", e => e.AgentBaseUrl)
            .Col("enable_custom_css", e => e.CustomCssEnabled ? 1 : 0, readOnly: true)
            .Col("custom_css_code", e => e.CustomCss)
            .Col("2fa_enforcement_strategy", e => e.TwoFactorAuthRequired ? 1 : 0, readOnly: true)
            .Col("date_creation", e => e.CreatedAt, readOnly: true)
            .Col("date_mod", e => e.UpdatedAt, readOnly: true)
            .OnCreate((e, ctx) => e.CreatedAt = Now)
            .OnSave((e, ctx) => e.UpdatedAt = Now)
            .AfterSave(ctx => ctx.EntityTree.Invalidate())
            .Table;
        entities.IdToApiMap = (ctx, id) => ctx.ToApiEntity(id);
        entities.IdFromApiMap = (ctx, id) => ctx.FromApiEntity(id);

        Table<GlpiProfile>("glpi_profiles", p => p.Id)
            .Col("name", p => p.Name)
            .Col("comment", p => p.Comment)
            .Col("is_default", p => p.IsDefault)
            .Const("interface", "central")
            .Col("date_creation", p => p.CreatedAt, readOnly: true)
            .Col("date_mod", p => p.UpdatedAt, readOnly: true)
            .OnCreate((p, ctx) => p.CreatedAt = Now)
            .OnSave((p, ctx) => p.UpdatedAt = Now);

        Table<ApiClient>("glpi_apiclients", c => c.Id)
            .Const("entities_id", 0L)
            .Const("is_recursive", 1L)
            .Col("name", c => c.Name)
            .Col("is_active", c => c.IsActive)
            .Col("ipv4_range_start", c => c.Ipv4RangeStart)
            .Col("ipv4_range_end", c => c.Ipv4RangeEnd)
            .Col("ipv6", c => c.Ipv6)
            .Col("app_token_date", c => c.AppTokenDate, readOnly: true)
            .Col("dolog_method", c => c.LogMethod)
            .Col("comment", c => c.Comment)
            .Dates(c => c.CreatedAt, c => c.UpdatedAt, (c, v) => c.CreatedAt = v, (c, v) => c.UpdatedAt = v);

        Table<GlpiUserProfile>("glpi_profiles_users", h => h.Id)
            .Col("users_id", h => h.UserId)
            .Col("profiles_id", h => h.ProfileId)
            .Custom(new GlpiColumnMap<GlpiUserProfile>
            {
                Name = "entities_id",
                Expr = (Expression<Func<GlpiUserProfile, int>>)(h => h.EntityId),
                Read = (h, v, ctx) => (long)ctx.ToApiEntity((int)v!),
                Translate = (ctx, body, op, value) => EfGlpiTable<GlpiUserProfile>.TranslateDefault(ctx, body, op,
                    GlpiValue.TryToLong(value, out long id) ? ctx.FromApiEntity(id) : value, isUtcDate: false),
                Write = (h, v, ctx) =>
                {
                    if (GlpiValue.TryToLong(v, out long id) && id >= 0)
                    {
                        h.EntityId = ctx.FromApiEntity(id);
                    }
                },
            })
            .Col("is_recursive", h => h.IsRecursive)
            .Const("is_dynamic", 0L)
            .Const("is_default_profile", 0L);
    }
}

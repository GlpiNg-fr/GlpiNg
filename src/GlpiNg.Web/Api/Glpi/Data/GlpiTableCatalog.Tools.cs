using System.Linq.Expressions;
using GlpiNg.Modules.Abstractions.Directory;
using GlpiNg.Modules.Abstractions.Items;
using GlpiNg.Modules.Assistance.Models;
using GlpiNg.Modules.Inventory.Models;
using GlpiNg.Modules.KnowledgeBase.Models;
using GlpiNg.Modules.KnowledgeBase.Services;
using GlpiNg.Modules.Management.Models;
using GlpiNg.Web.Import;
using GlpiNg.Web.Models;
using GlpiNg.Web.Models.Notes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GlpiNg.Web.Api.Glpi.Data;

public sealed partial class GlpiTableCatalog
{
    private const string PrincipalCacheKey = "principal";

    /// <summary>Outils : base de connaissances, notes, historique.</summary>
    private void RegisterTools()
    {
        EfGlpiTable<KnowledgeBaseArticle> articles = Table<KnowledgeBaseArticle>("glpi_knowbaseitems", a => a.Id)
            .Entity(a => a.EntityId, a => a.IsRecursive)
            .Col("name", a => a.Subject)
            .Custom(new GlpiColumnMap<KnowledgeBaseArticle>
            {
                // GlpiNg stocke du Markdown, GLPI du HTML : conversion dans les deux sens, par les
                // deux seuls points de passage prévus (MarkdownRenderer, GlpiHtmlToMarkdown).
                Name = "answer",
                Expr = (Expression<Func<KnowledgeBaseArticle, string>>)(a => a.Content),
                Read = (a, v, ctx) => MarkdownRenderer.ToHtml(v as string),
                Write = (a, v, ctx) =>
                {
                    string text = GlpiValue.ToText(v) ?? string.Empty;
                    a.Content = GlpiHtmlToMarkdown.ContainsHtml(text) ? GlpiHtmlToMarkdown.Convert(text) : text;
                },
            })
            .Col("is_faq", a => a.IsFaq)
            .Col("is_pinned", a => a.IsPinned)
            .Col("view", a => a.ViewCount, readOnly: true)
            .Fk("users_id", a => a.AuthorUserId, readOnly: true)
            .Col("begin_date", a => a.VisibleFrom)
            .Col("end_date", a => a.VisibleUntil)
            .Col("date_creation", a => a.CreatedAt, readOnly: true)
            .Col("date_mod", a => a.UpdatedAt ?? a.CreatedAt, readOnly: true)
            .Preload(PreloadPrincipal)
            .OnCreate((a, ctx) =>
            {
                a.CreatedAt = Now;
                a.AuthorUserId = ctx.UserId;
                a.AuthorName = ctx.UserDisplayName;
            })
            .OnSave((a, ctx) =>
            {
                a.UpdatedAt = Now;
                a.LastEditorName = ctx.UserDisplayName;
            })
            .Table;
        // Visibilité d'un article : la règle de la base de connaissances, appliquée à toute lecture —
        // sinon l'API deviendrait le moyen de contourner le ciblage.
        articles.Shape = q => q.Include(a => a.Targets);
        articles.RowFilter = (a, ctx) => KnowledgeBaseService.IsVisible(a, ctx.Cache.GetValueOrDefault(PrincipalCacheKey) as PrincipalContext);

        Table<KnowledgeBaseArticle>("glpi_knowbaseitems_knowbaseitemcategories", a => a.Id)
            .Where(a => a.CategoryId != null)
            .Col("knowbaseitems_id", a => a.Id, readOnly: true)
            .Fk("knowbaseitemcategories_id", a => a.CategoryId, readOnly: true)
            .ReadOnly();

        Table<KnowledgeBaseArticleRevision>("glpi_knowbaseitems_revisions", r => r.Id)
            .Where(ctx => r => ctx.Db.Set<KnowledgeBaseArticle>().Any(a => a.Id == r.ArticleId))
            .Col("knowbaseitems_id", r => r.ArticleId)
            .Col("revision", r => r.Number)
            .Col("name", r => r.Subject)
            .Custom(new GlpiColumnMap<KnowledgeBaseArticleRevision>
            {
                Name = "answer",
                Expr = (Expression<Func<KnowledgeBaseArticleRevision, string>>)(r => r.Content),
                Read = (r, v, ctx) => MarkdownRenderer.ToHtml(v as string),
            })
            .Named("users_id", r => r.EditorName, Users, DisplayNameColumn)
            .Col("date", r => r.RevisedAt)
            .ReadOnly();

        KnowledgeTarget("glpi_entities_knowbaseitems", PrincipalKind.Entity, "entities_id");
        KnowledgeTarget("glpi_groups_knowbaseitems", PrincipalKind.Group, "groups_id");
        KnowledgeTarget("glpi_profiles_knowbaseitems", PrincipalKind.Profile, "profiles_id");
        KnowledgeTarget("glpi_knowbaseitems_users", PrincipalKind.User, "users_id");

        Table<KnowledgeBaseCategory>("glpi_knowbaseitemcategories", c => c.Id)
            .Entity(c => c.EntityId, c => c.IsRecursive)
            .Col("name", c => c.Name)
            .Col("comment", c => c.Comment)
            .Fk("knowbaseitemcategories_id", c => c.ParentId)
            .Tree("knowbaseitemcategories_id")
            .Col("date_creation", c => c.CreatedAt, readOnly: true)
            .Col("date_mod", c => c.CreatedAt, readOnly: true)
            .OnCreate((c, ctx) => c.CreatedAt = Now);

        Table<Notepad>("glpi_notepads", n => n.Id)
            // Les notes sont visibles si leur objet l'est ; un objet d'un type non vérifié ici ne
            // livre ses notes qu'à un compte non cloisonné.
            .Where(ctx => n => ctx.Scope.IsUnrestricted
                || (n.ItemType == ItemTypes.Computer && ctx.Db.Set<Computer>().Any(c => c.Id == n.ItemId))
                || (n.ItemType == ItemTypes.KnowledgeBaseArticle && ctx.Db.Set<KnowledgeBaseArticle>().Any(a => a.Id == n.ItemId)))
            .Col("itemtype", n => n.ItemType)
            .Col("items_id", n => n.ItemId)
            .Col("content", n => n.Content)
            .Fk("users_id", n => n.AuthorUserId, readOnly: true)
            .Named("users_id_lastupdater", n => n.LastEditorName, Users, DisplayNameColumn, readOnly: true)
            .Col("date_creation", n => n.CreatedAt, readOnly: true)
            .Col("date_mod", n => n.UpdatedAt ?? n.CreatedAt, readOnly: true)
            .OnCreate((n, ctx) =>
            {
                n.CreatedAt = Now;
                n.AuthorUserId = ctx.UserId;
                n.AuthorName = ctx.UserDisplayName ?? string.Empty;
            })
            .OnSave((n, ctx) =>
            {
                n.UpdatedAt = Now;
                n.LastEditorName = ctx.UserDisplayName;
            });

        // Historique : une table par type dans GlpiNg, une seule dans GLPI.
        Union("glpi_logs",
            LogPart<ComputerHistoryEntry, Computer>(ItemTypes.Computer, h => h.Id, h => h.ComputerId, h => h.OccurredAt, h => h.User, h => h.Field, h => h.Description),
            LogPart<PeripheralHistoryEntry, Peripheral>(ItemTypes.Peripheral, h => h.Id, h => h.PeripheralId, h => h.OccurredAt, h => h.User, h => h.Field, h => h.Description),
            LogPart<NetworkEquipmentHistoryEntry, NetworkEquipment>(ItemTypes.NetworkEquipment, h => h.Id, h => h.NetworkEquipmentId, h => h.OccurredAt, h => h.User, h => h.Field, h => h.Description),
            LogPart<PrinterHistoryEntry, Printer>(ItemTypes.Printer, h => h.Id, h => h.PrinterId, h => h.OccurredAt, h => h.User, h => h.Field, h => h.Description),
            LogPart<PhoneHistoryEntry, Phone>(ItemTypes.Phone, h => h.Id, h => h.PhoneId, h => h.OccurredAt, h => h.User, h => h.Field, h => h.Description),
            LogPart<RackHistoryEntry, Rack>(ItemTypes.Rack, h => h.Id, h => h.RackId, h => h.OccurredAt, h => h.User, h => h.Field, h => h.Description),
            LogPart<EnclosureHistoryEntry, Enclosure>(ItemTypes.Enclosure, h => h.Id, h => h.EnclosureId, h => h.OccurredAt, h => h.User, h => h.Field, h => h.Description),
            LogPart<PduHistoryEntry, Pdu>(ItemTypes.Pdu, h => h.Id, h => h.PduId, h => h.OccurredAt, h => h.User, h => h.Field, h => h.Description),
            LogPart<PassiveEquipmentHistoryEntry, PassiveEquipment>("PassiveDCEquipment", h => h.Id, h => h.PassiveEquipmentId, h => h.OccurredAt, h => h.User, h => h.Field, h => h.Description),
            LogPart<CartridgeItemHistoryEntry, CartridgeItem>(ItemTypes.CartridgeItem, h => h.Id, h => h.CartridgeItemId, h => h.OccurredAt, h => h.User, h => h.Field, h => h.Description),
            LogPart<ConsumableItemHistoryEntry, ConsumableItem>(ItemTypes.ConsumableItem, h => h.Id, h => h.ConsumableItemId, h => h.OccurredAt, h => h.User, h => h.Field, h => h.Description),
            LogPart<SimCardHistoryEntry, SimCard>("Item_DeviceSimcard", h => h.Id, h => h.SimCardId, h => h.OccurredAt, h => h.User, h => h.Field, h => h.Description),
            LogPart<CableHistoryEntry, Cable>(ItemTypes.Cable, h => h.Id, h => h.CableId, h => h.OccurredAt, h => h.User, h => h.Field, h => h.Description),
            LogPart<GlpiUserHistoryEntry, GlpiUser>("User", h => h.Id, h => h.UserId, h => h.OccurredAt, h => h.User, h => h.Field, h => h.Description),
            LogPart<GlpiGroupHistoryEntry, GlpiGroup>(ItemTypes.Group, h => h.Id, h => h.GroupId, h => h.OccurredAt, h => h.User, h => h.Field, h => h.Description),
            LogPart<GlpiEntityHistoryEntry, GlpiEntity>(ItemTypes.Entity, h => h.Id, h => h.EntityId, h => h.OccurredAt, h => h.User, h => h.Field, h => h.Description),
            LogPart<GlpiProfileHistoryEntry, GlpiProfile>("Profile", h => h.Id, h => h.ProfileId, h => h.OccurredAt, h => h.User, h => h.Field, h => h.Description),
            LogPart<KnowledgeBaseArticleHistoryEntry, KnowledgeBaseArticle>(ItemTypes.KnowledgeBaseArticle, h => h.Id, h => h.ArticleId, h => h.OccurredAt, h => h.User, h => h.Field, h => h.Description),
            PolymorphicLogPart<ManagementHistoryEntry>(h => h.Id, h => h.ItemType, h => h.ItemId, h => h.OccurredAt, h => h.User, h => h.Field, h => h.Description),
            PolymorphicLogPart<AssistanceHistoryEntry>(h => h.Id, h => h.ItemType, h => h.ItemId, h => h.OccurredAt, h => h.User, h => h.Field, h => h.Description));
    }

    private static async Task PreloadPrincipal(GlpiDataContext ctx, CancellationToken ct)
    {
        if (!ctx.Cache.ContainsKey(PrincipalCacheKey) && ctx.UserId is int userId)
        {
            PrincipalContext? principal = await ctx.Services.GetRequiredService<IPrincipalContextProvider>().GetAsync(userId, ct);
            if (principal is not null)
            {
                ctx.Cache[PrincipalCacheKey] = principal;
            }
        }
    }

    private void KnowledgeTarget(string table, PrincipalKind kind, string column)
    {
        GlpiTableBuilder<KnowledgeBaseArticleTarget> builder = Table<KnowledgeBaseArticleTarget>(table, t => t.Id)
            .Where(t => t.Type == kind)
            .Where(ctx => t => ctx.Db.Set<KnowledgeBaseArticle>().Any(a => a.Id == t.ArticleId))
            .Col("knowbaseitems_id", t => t.ArticleId)
            .OnCreate((t, ctx) => t.Type = kind);

        if (kind == PrincipalKind.Entity)
        {
            builder.Custom(new GlpiColumnMap<KnowledgeBaseArticleTarget>
            {
                Name = "entities_id",
                Expr = (Expression<Func<KnowledgeBaseArticleTarget, int>>)(t => t.ItemId),
                Read = (t, v, ctx) => (long)ctx.ToApiEntity((int)v!),
                Write = (t, v, ctx) => t.ItemId = GlpiValue.TryToLong(v, out long id) ? ctx.FromApiEntity(id) : ctx.RootEntityId,
            });
        }
        else
        {
            builder.Col(column, t => t.ItemId);
            if (kind != PrincipalKind.User)
            {
                builder.Custom(new GlpiColumnMap<KnowledgeBaseArticleTarget>
                {
                    Name = "entities_id",
                    Expr = (Expression<Func<KnowledgeBaseArticleTarget, int?>>)(t => t.ScopeEntityId),
                    Read = (t, v, ctx) => v is int scope ? (long)ctx.ToApiEntity(scope) : -1L,
                    Write = (t, v, ctx) => t.ScopeEntityId = GlpiValue.TryToLong(v, out long id) && id >= 0 ? ctx.FromApiEntity(id) : null,
                });
            }
        }
        if (kind != PrincipalKind.User)
        {
            builder.Col("is_recursive", t => t.IsRecursive);
        }
    }

    /// <summary>Entrée d'historique d'un type dont l'objet parent est désigné par une clé.</summary>
    private static IGlpiTable LogPart<T, TParent>(string itemtype, Expression<Func<T, int>> id, Expression<Func<T, int>> parentId,
        Expression<Func<T, DateTime>> date, Func<T, string?> user, Func<T, string> field, Func<T, string?> description)
        where T : class where TParent : class
        => Part("glpi_logs", id)
            .Where(ctx => VisibleParent<T, TParent>(ctx, parentId))
            .Const("itemtype", itemtype)
            .Col("items_id", parentId, readOnly: true)
            .Col("date_mod", date, readOnly: true)
            .Computed("user_name", (h, ctx) => user(h))
            .Computed("new_value", (h, ctx) => LogMessage(field(h), description(h)))
            .Const("linked_action", 12L)
            .ReadOnly()
            .Table;

    /// <summary>Entrée d'historique d'une table qui mêle plusieurs types (Gestion, Assistance).</summary>
    private static IGlpiTable PolymorphicLogPart<T>(Expression<Func<T, int>> id, Expression<Func<T, string>> itemtype, Expression<Func<T, int>> itemId,
        Expression<Func<T, DateTime>> date, Func<T, string?> user, Func<T, string> field, Func<T, string?> description)
        where T : class
        => Part("glpi_logs", id)
            .Col("itemtype", itemtype, readOnly: true)
            .Col("items_id", itemId, readOnly: true)
            .Col("date_mod", date, readOnly: true)
            .Computed("user_name", (h, ctx) => user(h))
            .Computed("new_value", (h, ctx) => LogMessage(field(h), description(h)))
            .Const("linked_action", 12L)
            .ReadOnly()
            .Table;

    /// <summary>
    /// Message d'historique : GlpiNg écrit une phrase par changement, que GLPI range comme message
    /// simple (Log::HISTORY_LOG_SIMPLE_MESSAGE = 12, texte dans new_value).
    /// </summary>
    private static string LogMessage(string field, string? description)
        => string.IsNullOrWhiteSpace(description) ? field : $"{field} : {description}";
}

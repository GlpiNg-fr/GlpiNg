using System.Reflection;
using GlpiNg.Modules.Abstractions.Entities;
using GlpiNg.Web.Data;
using GlpiNg.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Web.Services;

/// <summary>Nombre d'objets d'un type donné encore rattachés à une entité.</summary>
public sealed record EntityReferenceCount(string TypeName, string Label, int Count);

/// <summary>
/// Compte ce qui empêche la suppression d'une entité, avant de la tenter.
///
/// La FK vers <c>Entities</c> est en <see cref="DeleteBehavior.Restrict"/> — supprimer une entité
/// ne doit surtout pas emporter en cascade le parc qui y est rattaché. Sans ce contrôle préalable,
/// l'utilisateur reçoit une violation de contrainte brute du SGBD, qui ne dit ni combien d'objets
/// bloquent ni lesquels.
///
/// Le balayage part des métadonnées du modèle plutôt que d'une liste écrite à la main : tout type
/// implémentant <see cref="IEntityScoped"/> est couvert, y compris ceux ajoutés plus tard.
/// </summary>
public sealed class EntityDeletionGuard(IDbContextFactory<GlpiNgDbContext> dbFactory)
{
    /// <summary>
    /// Libellés lisibles des types cloisonnés. Un type absent d'ici est compté sous son nom de
    /// classe : le message reste utilisable, il est juste moins joli.
    /// </summary>
    private static readonly Dictionary<string, string> Labels = new(StringComparer.Ordinal)
    {
        ["Computer"] = "ordinateur(s)",
        ["Peripheral"] = "périphérique(s)",
        ["NetworkEquipment"] = "matériel(s) réseau",
        ["Printer"] = "imprimante(s)",
        ["Phone"] = "téléphone(s)",
        ["Rack"] = "baie(s)",
        ["Enclosure"] = "châssis",
        ["Pdu"] = "PDU",
        ["PassiveEquipment"] = "équipement(s) passif(s)",
        ["Cable"] = "câble(s)",
        ["CartridgeItem"] = "cartouche(s)",
        ["ConsumableItem"] = "consommable(s)",
        ["SimCard"] = "carte(s) SIM",
        ["GlpiAgent"] = "agent(s)",
        ["GlpiGroup"] = "groupe(s)",
        ["DropdownItem"] = "intitulé(s)",
        ["SavedSearch"] = "recherche(s) sauvegardée(s)",
        ["ComputerRule"] = "règle(s) pour les actifs",
        ["DictionaryRule"] = "règle(s) de dictionnaire",
        ["ImportAssignmentRule"] = "règle(s) d'import",
        ["DeploymentPackage"] = "paquet(s) de déploiement",
        ["DeploymentTask"] = "tâche(s) de déploiement",
        ["DeployComputerGroup"] = "groupe(s) d'ordinateurs",
        ["NetworkTask"] = "tâche(s) réseau",
        ["WakeOnLanTask"] = "tâche(s) Wake-on-LAN",
        ["DiscoveredNetworkDevice"] = "actif(s) non géré(s)",
        ["Notification"] = "notification(s)",
        ["NotificationTemplate"] = "gabarit(s) de notification",
    };

    /// <summary>
    /// Objets encore rattachés à <paramref name="entityId"/>, par type et seulement ceux qui en ont.
    /// Une liste vide signifie que l'entité est supprimable.
    /// </summary>
    public async Task<List<EntityReferenceCount>> CountReferencesAsync(int entityId, CancellationToken ct = default)
    {
        await using GlpiNgDbContext db = await dbFactory.CreateDbContextAsync(ct);

        List<EntityReferenceCount> counts = [];

        MethodInfo counter = typeof(EntityDeletionGuard)
            .GetMethod(nameof(CountForTypeAsync), BindingFlags.Static | BindingFlags.NonPublic)!;

        foreach (Type clrType in db.Model.GetEntityTypes()
                     .Select(entityType => entityType.ClrType)
                     .Where(typeof(IEntityScoped).IsAssignableFrom)
                     .Distinct())
        {
            // Passage par réflexion : DbContext.Set<T>() exige le type CLR concret, l'interface ne
            // suffit pas. La liste vient du modèle, donc rien à maintenir à la main.
            Task<int> counting = (Task<int>)counter.MakeGenericMethod(clrType).Invoke(null, [db, entityId, ct])!;
            int count = await counting;

            if (count > 0)
            {
                counts.Add(new EntityReferenceCount(clrType.Name, Labels.GetValueOrDefault(clrType.Name, clrType.Name), count));
            }
        }

        return [.. counts.OrderByDescending(reference => reference.Count)];
    }

    /// <summary>
    /// IgnoreQueryFilters : le comptage doit voir tout ce qui bloque la suppression, y compris ce
    /// que le cloisonnement masque à l'utilisateur courant. Sans ça, la suppression échouerait
    /// quand même, sur des objets dont le message n'aurait pas parlé.
    /// </summary>
    private static Task<int> CountForTypeAsync<TEntity>(GlpiNgDbContext db, int entityId, CancellationToken ct)
        where TEntity : class, IEntityScoped
        => db.Set<TEntity>().IgnoreQueryFilters().CountAsync(scoped => scoped.EntityId == entityId, ct);

    /// <summary>Sous-entités de <paramref name="entityId"/>, qui bloquent aussi la suppression (FK auto-référencée en Restrict).</summary>
    public async Task<int> CountChildEntitiesAsync(int entityId, CancellationToken ct = default)
    {
        await using GlpiNgDbContext db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Entities.IgnoreQueryFilters().CountAsync(entity => entity.ParentId == entityId, ct);
    }

    /// <summary>
    /// Message prêt à afficher, ou <c>null</c> si l'entité est supprimable.
    /// </summary>
    public async Task<string?> DescribeBlockersAsync(GlpiEntity entity, CancellationToken ct = default)
    {
        int children = await CountChildEntitiesAsync(entity.Id, ct);
        List<EntityReferenceCount> references = await CountReferencesAsync(entity.Id, ct);

        if (children == 0 && references.Count == 0)
        {
            return null;
        }

        List<string> parts = [];
        if (children > 0)
        {
            parts.Add($"{children} sous-entité(s)");
        }

        parts.AddRange(references.Select(reference => $"{reference.Count} {reference.Label}"));

        return $"« {entity.Name} » ne peut pas être supprimée : elle contient encore {string.Join(", ", parts)}. "
               + "Réaffectez ou supprimez ces éléments d'abord.";
    }
}

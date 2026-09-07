using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using GlpiNg.Web.Data;
using GlpiNg.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Web.Services;

/// <summary>
/// Lit/écrit les sections de réglages de la page /config (Valeurs par défaut, Parc,
/// Assistance, Modules, ...) dans la table <see cref="AppSetting"/>, une ligne par section,
/// et les garde en cache mémoire pour un accès rapide en lecture — y compris depuis des
/// pages consultées à chaque rendu comme MainLayout (menu latéral, piloté par
/// "ModulesSettings"). Écrire une section met aussi à jour le cache immédiatement, donc tout
/// autre service/page voit la nouvelle valeur sans redémarrage ni relecture de la base.
///
/// Remplace <see cref="AppSettingsFileStore"/> pour toutes les sections sauf
/// <see cref="ServerSettings"/> (adresses d'écoute Kestrel + activation de Swagger), qui
/// reste dans appsettings.json — voir le commentaire de tête de AppSettingsFileStore.
/// </summary>
public sealed class SettingsCacheService(IRootDbContextFactory dbFactory, IConfiguration configuration)
{
    private readonly ConcurrentDictionary<string, JsonNode> _cache = new();
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private volatile bool _loaded;

    public async Task<T> ReadSectionAsync<T>(string sectionName, CancellationToken cancellationToken = default) where T : new()
    {
        await EnsureLoadedAsync(cancellationToken);

        if (_cache.TryGetValue(sectionName, out JsonNode? node))
        {
            return node.Deserialize<T>() ?? new T();
        }

        // Pas encore en base : reprend la valeur encore présente dans appsettings.json (transition
        // depuis l'ancien stockage fichier) plutôt que de perdre un réglage déjà personnalisé.
        return configuration.GetSection(sectionName).Get<T>() ?? new T();
    }

    public async Task SaveSectionAsync<T>(string sectionName, T settings, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);

        JsonNode node = JsonSerializer.SerializeToNode(settings) ?? new JsonObject();
        string json = node.ToJsonString();

        await using GlpiNgDbContext db = await dbFactory.CreateDbContextAsync(cancellationToken);
        AppSetting? row = await db.AppSettings.FindAsync([sectionName], cancellationToken);
        if (row is null)
        {
            db.AppSettings.Add(new AppSetting { SectionName = sectionName, ValueJson = json, UpdatedAt = DateTime.UtcNow });
        }
        else
        {
            row.ValueJson = json;
            row.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);

        // Rend la valeur immédiatement visible aux autres services/pages, sans relire la base.
        _cache[sectionName] = node;
    }

    private async Task EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        if (_loaded)
        {
            return;
        }

        await _loadLock.WaitAsync(cancellationToken);
        try
        {
            if (_loaded)
            {
                return;
            }

            await using GlpiNgDbContext db = await dbFactory.CreateDbContextAsync(cancellationToken);
            List<AppSetting> rows = await db.AppSettings.ToListAsync(cancellationToken);
            foreach (AppSetting row in rows)
            {
                _cache[row.SectionName] = JsonNode.Parse(row.ValueJson) ?? new JsonObject();
            }

            _loaded = true;
        }
        finally
        {
            _loadLock.Release();
        }
    }
}

using System.Text.Json;
using GlpiNg.Web.Models;

namespace GlpiNg.Web.Services;

/// <summary>
/// Journal des modifications enregistrées depuis /config, affiché par l'onglet
/// "Historique". Fichier JSON séparé de appsettings.json (ce sont des données, pas de
/// la configuration de démarrage) ; borné à <see cref="MaxEntries"/> lignes, comme le
/// ferait une purge d'historique.
/// </summary>
public class ConfigHistoryService(IWebHostEnvironment environment)
{
    private const int MaxEntries = 500;

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private string FilePath => Path.Combine(environment.ContentRootPath, "config-history.json");

    public async Task<List<HistoryEntry>> ReadAllAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(FilePath))
        {
            return [];
        }

        string json = await File.ReadAllTextAsync(FilePath, cancellationToken);
        return JsonSerializer.Deserialize<List<HistoryEntry>>(json, Options) ?? [];
    }

    public async Task AppendAsync(string user, IEnumerable<(string Field, string Old, string New)> changes, CancellationToken cancellationToken = default)
    {
        var changeList = changes.ToList();
        if (changeList.Count == 0)
        {
            return;
        }

        var entries = await ReadAllAsync(cancellationToken);
        var nextId = entries.Count == 0 ? 1 : entries.Max(e => e.Id) + 1;

        foreach (var (field, oldValue, newValue) in changeList)
        {
            entries.Insert(0, new HistoryEntry(nextId++, DateTimeOffset.Now, user, field, $"Changement de {field} de {oldValue} à {newValue}"));
        }

        if (entries.Count > MaxEntries)
        {
            entries = entries.Take(MaxEntries).ToList();
        }

        string json = JsonSerializer.Serialize(entries, Options);
        await File.WriteAllTextAsync(FilePath, json, cancellationToken);
    }
}

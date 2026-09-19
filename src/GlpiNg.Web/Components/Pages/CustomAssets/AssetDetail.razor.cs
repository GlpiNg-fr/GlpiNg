using BlazorBootstrap;
using GlpiNg.Modules.Abstractions.Documents;
using GlpiNg.Modules.Abstractions.Notes;
using GlpiNg.Modules.Inventory.Models;
using GlpiNg.Web.Data;
using GlpiNg.Web.Models.CustomAssets;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.EntityFrameworkCore;

// BlazorBootstrap (importé ci-dessus pour ToastService) expose lui aussi un « DropdownItem ».
using DropdownItem = GlpiNg.Modules.Inventory.Models.DropdownItem;

namespace GlpiNg.Web.Components.Pages.CustomAssets;

public partial class AssetDetail : ComponentBase
{
    /// <summary>Plafond d'un fichier joint, aligné sur ce que le navigateur envoie sans découpage.</summary>
    private const long MaxDocumentBytes = 64 * 1024 * 1024;

    [Parameter]
    public string SystemName { get; set; } = string.Empty;

    [Parameter]
    public int AssetId { get; set; }

    [Inject]
    private IDbContextFactory<GlpiNgDbContext> DbFactory { get; set; } = null!;

    [Inject]
    private IDocumentAttachments Documents { get; set; } = null!;

    [Inject]
    private IItemNotes Notes { get; set; } = null!;

    [Inject]
    private NavigationManager Navigation { get; set; } = null!;

    [Inject]
    private ToastService ToastService { get; set; } = null!;

    [CascadingParameter]
    private Task<AuthenticationState>? AuthStateTask { get; set; }

    private CustomAssetDefinition? _definition;
    private CustomAsset? _asset;
    private List<CustomAssetField> _fields = [];
    private Dictionary<int, string?> _values = [];
    private Dictionary<DropdownType, List<DropdownItem>> _dropdowns = [];

    private List<DocumentSummary> _documents = [];
    private List<ItemNote> _notes = [];
    private List<CustomAssetHistoryEntry> _history = [];

    private string _activeTab = "fiche";
    private string _newNote = string.Empty;
    private bool _isSaving;
    private string? _saveError;
    private string? _documentError;
    private string _currentUserName = "?";

    /// <summary>Type d'objet des documents, notes et historique — voir <see cref="CustomAssetItemTypes"/>.</summary>
    private string ItemType => CustomAssetItemTypes.For(SystemName);

    private IEnumerable<(string Key, string Label, string Icon, int? Count)> Tabs
    {
        get
        {
            yield return ("fiche", "Fiche", _definition?.Icon ?? "ti-tool", null);

            if (_definition?.DocumentsEnabled == true)
            {
                yield return ("documents", "Documents", "ti-file", _documents.Count);
            }

            if (_definition?.NotesEnabled == true)
            {
                yield return ("notes", "Notes", "ti-notes", _notes.Count);
            }

            if (_definition?.HistoryEnabled == true)
            {
                yield return ("historique", "Historique", "ti-history", _history.Count);
            }
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        if (AuthStateTask is not null)
        {
            AuthenticationState authState = await AuthStateTask;
            _currentUserName = authState.User.Identity?.Name ?? "?";
        }

        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();

        _definition = await db.CustomAssetDefinitions
            .AsNoTracking()
            .Include(definition => definition.Fields)
            .FirstOrDefaultAsync(definition => definition.SystemName == SystemName);

        if (_definition is null)
        {
            _asset = null;
            return;
        }

        // Le type fait partie de la recherche : une adresse qui mêle le type d'un actif et
        // l'identifiant d'un autre ne doit pas ouvrir une fiche dont les champs ne sont pas ceux-là.
        int definitionId = _definition.Id;

        _asset = await db.CustomAssets
            .AsNoTracking()
            .Include(asset => asset.Values)
            .FirstOrDefaultAsync(asset => asset.Id == AssetId && asset.CustomAssetDefinitionId == definitionId);

        if (_asset is null)
        {
            return;
        }

        _fields = [.. _definition.Fields.OrderBy(field => field.SortOrder)];
        _values = _fields.ToDictionary(
            field => field.Id,
            field => _asset.Values.FirstOrDefault(value => value.CustomAssetFieldId == field.Id)?.Value);

        await LoadDropdownsAsync(db);

        _history = _definition.HistoryEnabled
            ? await db.CustomAssetHistoryEntries
                .AsNoTracking()
                .Where(entry => entry.CustomAssetId == AssetId)
                .OrderByDescending(entry => entry.OccurredAt)
                .ToListAsync()
            : [];

        _documents = _definition.DocumentsEnabled
            ? [.. await Documents.GetForItemAsync(ItemType, AssetId)]
            : [];

        _notes = _definition.NotesEnabled
            ? [.. await Notes.GetForItemAsync(ItemType, AssetId)]
            : [];
    }

    private async Task LoadDropdownsAsync(GlpiNgDbContext db)
    {
        List<DropdownType> types = [.. _fields
            .Where(field => field.Type == CustomAssetFieldType.Dropdown && field.DropdownType is not null)
            .Select(field => field.DropdownType!.Value)
            .Distinct()];

        _dropdowns = types.Count == 0
            ? []
            : (await db.Set<DropdownItem>()
                .AsNoTracking()
                .Where(item => types.Contains(item.Type))
                .OrderBy(item => item.Name)
                .ToListAsync())
                .GroupBy(item => item.Type)
                .ToDictionary(group => group.Key, group => group.ToList());
    }

    private IReadOnlyList<DropdownItem> OptionsFor(CustomAssetField field) =>
        field.DropdownType is { } type && _dropdowns.TryGetValue(type, out List<DropdownItem>? options)
            ? options
            : [];

    private Func<int, string?> DropdownName(CustomAssetField field) =>
        id => OptionsFor(field).FirstOrDefault(option => option.Id == id)?.Name;

    private string? GetValue(CustomAssetField field) =>
        _values.TryGetValue(field.Id, out string? value) ? value : null;

    private void SetValue(CustomAssetField field, string? value) => _values[field.Id] = value;

    private void SetTab(string key) => _activeTab = key;

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} o",
        < 1024 * 1024 => $"{bytes / 1024d:0.#} Ko",
        _ => $"{bytes / (1024d * 1024d):0.#} Mo",
    };

    private async Task SaveAsync()
    {
        if (_asset is null || _definition is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_asset.Name))
        {
            _saveError = "Le nom est obligatoire.";
            return;
        }

        if (_fields.FirstOrDefault(field => field.IsMandatory && string.IsNullOrWhiteSpace(GetValue(field))) is { } missing)
        {
            _saveError = $"Le champ « {missing.Label} » est obligatoire.";
            return;
        }

        _saveError = null;
        _isSaving = true;

        try
        {
            await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();

            CustomAsset? stored = await db.CustomAssets
                .Include(asset => asset.Values)
                .FirstOrDefaultAsync(asset => asset.Id == AssetId);

            if (stored is null)
            {
                return;
            }

            List<CustomAssetHistoryEntry> changes = [];

            if (!string.Equals(stored.Name, _asset.Name, StringComparison.Ordinal))
            {
                changes.Add(HistoryEntry("Nom", stored.Name, _asset.Name));
                stored.Name = _asset.Name.Trim();
            }

            if (!string.Equals(stored.Comment, _asset.Comment, StringComparison.Ordinal))
            {
                changes.Add(HistoryEntry("Commentaires", stored.Comment, _asset.Comment));
                stored.Comment = _asset.Comment;
            }

            foreach (CustomAssetField field in _fields)
            {
                string? updated = CustomAssetValueFormat.ToStorage(field.Type, GetValue(field));
                CustomAssetValue? existing = stored.Values.FirstOrDefault(value => value.CustomAssetFieldId == field.Id);
                string? previous = existing?.Value;

                if (string.Equals(previous, updated, StringComparison.Ordinal))
                {
                    continue;
                }

                changes.Add(HistoryEntry(
                    field.Label,
                    CustomAssetValueFormat.ToDisplay(field, previous, DropdownName(field)),
                    CustomAssetValueFormat.ToDisplay(field, updated, DropdownName(field))));

                if (updated is null)
                {
                    if (existing is not null)
                    {
                        db.CustomAssetValues.Remove(existing);
                    }
                }
                else if (existing is null)
                {
                    db.CustomAssetValues.Add(new CustomAssetValue
                    {
                        CustomAssetId = AssetId,
                        CustomAssetFieldId = field.Id,
                        Value = updated,
                    });
                }
                else
                {
                    existing.Value = updated;
                }
            }

            if (changes.Count > 0)
            {
                stored.UpdatedAt = DateTime.UtcNow;

                if (_definition.HistoryEnabled)
                {
                    db.CustomAssetHistoryEntries.AddRange(changes);
                }
            }

            await db.SaveChangesAsync();

            ToastService.Notify(new ToastMessage(ToastType.Success, "Élément enregistré."));
            await LoadAsync();
        }
        finally
        {
            _isSaving = false;
        }
    }

    private CustomAssetHistoryEntry HistoryEntry(string field, string? before, string? after) => new()
    {
        CustomAssetId = AssetId,
        User = _currentUserName,
        Field = field,
        Description = $"« {Displayable(before)} » → « {Displayable(after)} »",
    };

    private static string Displayable(string? value) => string.IsNullOrWhiteSpace(value) ? "(vide)" : value;

    private async Task DeleteAsync()
    {
        await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();

        CustomAsset? stored = await db.CustomAssets.FirstOrDefaultAsync(asset => asset.Id == AssetId);
        if (stored is null)
        {
            return;
        }

        db.CustomAssets.Remove(stored);
        await db.SaveChangesAsync();

        Navigation.NavigateTo($"/parc/custom/{SystemName}");
    }

    private async Task OnDocumentSelectedAsync(InputFileChangeEventArgs args)
    {
        IBrowserFile file = args.File;

        if (file.Size > MaxDocumentBytes)
        {
            _documentError = $"Fichier trop volumineux ({FormatSize(file.Size)}) : {FormatSize(MaxDocumentBytes)} au maximum.";
            return;
        }

        _documentError = null;

        await using Stream content = file.OpenReadStream(MaxDocumentBytes);
        await Documents.UploadAndAttachAsync(ItemType, AssetId, file.Name, content, file.ContentType, _currentUserName, null);

        await LoadAsync();
    }

    private async Task DetachDocumentAsync(int documentId)
    {
        await Documents.DetachAsync(ItemType, AssetId, documentId);
        await LoadAsync();
    }

    private async Task AddNoteAsync()
    {
        if (string.IsNullOrWhiteSpace(_newNote))
        {
            return;
        }

        await Notes.AddAsync(ItemType, AssetId, _newNote, _currentUserName, null);
        _newNote = string.Empty;

        await LoadAsync();
    }

    private async Task DeleteNoteAsync(int noteId)
    {
        await Notes.DeleteAsync(noteId);
        await LoadAsync();
    }
}

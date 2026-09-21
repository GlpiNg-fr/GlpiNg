using BlazorBootstrap;
using GlpiNg.Modules.Inventory.Models;
using GlpiNg.Web.Data;
using GlpiNg.Web.Models.CustomAssets;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Web.Components.Pages.CustomAssets;

public partial class Detail : ComponentBase
{
    /// <summary>
    /// Icônes proposées pour un type. Liste fermée plutôt qu'une saisie libre : une classe
    /// inexistante ne laisserait qu'un vide dans le menu, sans rien pour en comprendre la cause.
    /// </summary>
    private static readonly (string Icon, string Label)[] IconChoices =
    [
        ("ti-tool", "Outil"),
        ("ti-device-projector", "Vidéoprojecteur"),
        ("ti-car", "Véhicule"),
        ("ti-key", "Clé / badge"),
        ("ti-armchair", "Mobilier"),
        ("ti-building", "Bâtiment"),
        ("ti-box", "Boîte"),
        ("ti-camera", "Caméra"),
        ("ti-device-tv", "Écran"),
        ("ti-headphones", "Audio"),
        ("ti-plug", "Électrique"),
        ("ti-shield", "Sécurité"),
        ("ti-book", "Documentation"),
        ("ti-package", "Colis"),
    ];

    [Parameter]
    public int DefinitionId { get; set; }

    [Inject]
    private IDbContextFactory<GlpiNgDbContext> DbFactory { get; set; } = null!;

    [Inject]
    private NavigationManager Navigation { get; set; } = null!;

    [Inject]
    private ToastService ToastService { get; set; } = null!;

    private CustomAssetDefinition? _definition;
    private List<CustomAssetField> _fields = [];
    private int _assetCount;

    private bool _isSaving;
    private string? _fieldError;

    protected override async Task OnParametersSetAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();

        _definition = await db.CustomAssetDefinitions
            .AsNoTracking()
            .Include(definition => definition.Fields)
            .FirstOrDefaultAsync(definition => definition.Id == DefinitionId);

        _fields = [.. (_definition?.Fields ?? []).OrderBy(field => field.SortOrder)];

        _assetCount = _definition is null
            ? 0
            : await db.CustomAssets.CountAsync(asset => asset.CustomAssetDefinitionId == DefinitionId);
    }

    private void AddField() =>
        _fields.Add(new CustomAssetField
        {
            Label = string.Empty,
            CustomAssetDefinitionId = DefinitionId,
            SortOrder = _fields.Count == 0 ? 10 : _fields.Max(field => field.SortOrder) + 10,
        });

    private void RemoveField(CustomAssetField field) => _fields.Remove(field);

    private void OnFieldTypeChanged(CustomAssetField field, ChangeEventArgs args)
    {
        if (Enum.TryParse(args.Value?.ToString(), out CustomAssetFieldType type))
        {
            field.Type = type;
        }

        // Une catégorie d'intitulés n'a de sens que pour une liste déroulante : la garder sur un
        // champ redevenu texte laisserait une valeur muette en base.
        if (field.Type != CustomAssetFieldType.Dropdown)
        {
            field.DropdownType = null;
        }
    }

    private void OnDropdownTypeChanged(CustomAssetField field, ChangeEventArgs args) =>
        field.DropdownType = Enum.TryParse(args.Value?.ToString(), out DropdownType type) ? type : null;

    private void OnSortOrderChanged(CustomAssetField field, ChangeEventArgs args)
    {
        if (int.TryParse(args.Value?.ToString(), out int order))
        {
            field.SortOrder = order;
        }
    }

    private async Task SaveAsync()
    {
        if (_definition is null)
        {
            return;
        }

        if (_fields.Any(field => string.IsNullOrWhiteSpace(field.Label)))
        {
            _fieldError = "Chaque champ doit porter un libellé.";
            return;
        }

        if (_fields.GroupBy(field => field.Label.Trim(), StringComparer.CurrentCultureIgnoreCase).Any(group => group.Count() > 1))
        {
            _fieldError = "Deux champs ne peuvent pas porter le même libellé.";
            return;
        }

        _fieldError = null;
        _isSaving = true;

        try
        {
            await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();

            CustomAssetDefinition? stored = await db.CustomAssetDefinitions
                .Include(definition => definition.Fields)
                .FirstOrDefaultAsync(definition => definition.Id == DefinitionId);

            if (stored is null)
            {
                return;
            }

            stored.LabelSingular = _definition.LabelSingular;
            stored.LabelPlural = _definition.LabelPlural;
            stored.Icon = _definition.Icon;
            stored.Comment = _definition.Comment;
            stored.IsActive = _definition.IsActive;
            stored.DocumentsEnabled = _definition.DocumentsEnabled;
            stored.NotesEnabled = _definition.NotesEnabled;
            stored.HistoryEnabled = _definition.HistoryEnabled;
            stored.UpdatedAt = DateTime.UtcNow;

            // Différentiel : un champ inchangé garde son identifiant, et donc les valeurs déjà
            // saisies pour lui. Tout réécrire les effacerait par cascade à chaque enregistrement.
            HashSet<int> keptIds = [.. _fields.Where(field => field.Id > 0).Select(field => field.Id)];

            foreach (CustomAssetField removed in stored.Fields.Where(field => !keptIds.Contains(field.Id)).ToList())
            {
                db.CustomAssetFields.Remove(removed);
            }

            foreach (CustomAssetField edited in _fields)
            {
                if (edited.Id > 0)
                {
                    CustomAssetField? target = stored.Fields.FirstOrDefault(field => field.Id == edited.Id);
                    if (target is null)
                    {
                        continue;
                    }

                    target.Label = edited.Label.Trim();
                    target.Type = edited.Type;
                    target.DropdownType = edited.DropdownType;
                    target.IsMandatory = edited.IsMandatory;
                    target.SortOrder = edited.SortOrder;
                }
                else
                {
                    db.CustomAssetFields.Add(new CustomAssetField
                    {
                        CustomAssetDefinitionId = DefinitionId,
                        Label = edited.Label.Trim(),
                        Type = edited.Type,
                        DropdownType = edited.DropdownType,
                        IsMandatory = edited.IsMandatory,
                        SortOrder = edited.SortOrder,
                    });
                }
            }

            await db.SaveChangesAsync();

            ToastService.Notify(new ToastMessage(ToastType.Success, "Type d'actif enregistré."));
            await LoadAsync();
        }
        finally
        {
            _isSaving = false;
        }
    }

    private async Task DeleteAsync()
    {
        await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();

        CustomAssetDefinition? stored = await db.CustomAssetDefinitions
            .FirstOrDefaultAsync(definition => definition.Id == DefinitionId);

        if (stored is null)
        {
            return;
        }

        // Les actifs partent d'abord, explicitement : la base ne les emporte pas en cascade avec
        // leur type — voir le commentaire de la relation dans GlpiNgDbContext. Leurs valeurs et
        // leur historique suivent, eux, par cascade.
        List<CustomAsset> assets = await db.CustomAssets
            .Where(asset => asset.CustomAssetDefinitionId == DefinitionId)
            .ToListAsync();

        db.CustomAssets.RemoveRange(assets);
        db.CustomAssetDefinitions.Remove(stored);
        await db.SaveChangesAsync();

        Navigation.NavigateTo("/config/custom-assets");
    }
}

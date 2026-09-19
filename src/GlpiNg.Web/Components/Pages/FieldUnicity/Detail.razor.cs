using BlazorBootstrap;
using GlpiNg.Web.Data;
using GlpiNg.Web.Models.FieldUnicity;
using GlpiNg.Web.Services.FieldUnicity;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Web.Components.Pages.FieldUnicity;

public partial class Detail : ComponentBase
{
    [Parameter]
    public int CriterionId { get; set; }

    [Inject]
    private IDbContextFactory<GlpiNgDbContext> DbFactory { get; set; } = null!;

    [Inject]
    private FieldUnicityCatalog Catalog { get; set; } = null!;

    [Inject]
    private NavigationManager Navigation { get; set; } = null!;

    [Inject]
    private ToastService ToastService { get; set; } = null!;

    private FieldUnicityCriterion? _criterion;
    private readonly HashSet<string> _selectedFields = new(StringComparer.Ordinal);
    private IReadOnlyList<FieldUnicityFieldOption> _fieldOptions = [];

    private bool _isSaving;

    protected override async Task OnParametersSetAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();

        _criterion = await db.FieldUnicityCriteria
            .AsNoTracking()
            .Include(criterion => criterion.Fields)
            .FirstOrDefaultAsync(criterion => criterion.Id == CriterionId);

        _selectedFields.Clear();
        foreach (FieldUnicityField field in _criterion?.Fields ?? [])
        {
            _selectedFields.Add(field.FieldName);
        }

        _fieldOptions = _criterion is null ? [] : Catalog.FieldsFor(_criterion.ItemType);
    }

    /// <summary>
    /// Changer de type d'objet vide les champs cochés : ils désignent des propriétés du type
    /// précédent, que le nouveau n'a aucune raison de porter. Les garder laisserait un critère
    /// silencieusement inopérant, la recherche de doublon ne trouvant plus ces champs.
    /// </summary>
    private void OnItemTypeChanged(ChangeEventArgs args)
    {
        if (_criterion is null)
        {
            return;
        }

        _criterion.ItemType = args.Value?.ToString() ?? _criterion.ItemType;
        _selectedFields.Clear();
        _fieldOptions = Catalog.FieldsFor(_criterion.ItemType);
    }

    private void ToggleField(string fieldName, bool selected)
    {
        if (selected)
        {
            _selectedFields.Add(fieldName);
        }
        else
        {
            _selectedFields.Remove(fieldName);
        }
    }

    private async Task SaveAsync()
    {
        if (_criterion is null)
        {
            return;
        }

        _isSaving = true;

        try
        {
            await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();

            FieldUnicityCriterion? stored = await db.FieldUnicityCriteria
                .Include(criterion => criterion.Fields)
                .FirstOrDefaultAsync(criterion => criterion.Id == CriterionId);

            if (stored is null)
            {
                return;
            }

            stored.Name = _criterion.Name;
            stored.Comment = _criterion.Comment;
            stored.ItemType = _criterion.ItemType;
            stored.IsActive = _criterion.IsActive;
            stored.IsRecursive = _criterion.IsRecursive;
            stored.RefuseCreation = _criterion.RefuseCreation;
            stored.NotifyOnDuplicate = _criterion.NotifyOnDuplicate;
            stored.UpdatedAt = DateTime.UtcNow;

            // Différentiel plutôt que « tout effacer puis tout réécrire » : les lignes inchangées
            // gardent leur identifiant, et une erreur en cours de route ne laisse pas le critère
            // sans aucun champ — c'est-à-dire sans effet.
            foreach (FieldUnicityField field in stored.Fields.Where(f => !_selectedFields.Contains(f.FieldName)).ToList())
            {
                db.FieldUnicityFields.Remove(field);
            }

            HashSet<string> already = [.. stored.Fields.Select(field => field.FieldName)];

            foreach (string fieldName in _selectedFields.Where(fieldName => !already.Contains(fieldName)))
            {
                db.FieldUnicityFields.Add(new FieldUnicityField
                {
                    FieldUnicityCriterionId = CriterionId,
                    FieldName = fieldName,
                });
            }

            await db.SaveChangesAsync();

            ToastService.Notify(new ToastMessage(ToastType.Success, "Critère enregistré."));
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

        FieldUnicityCriterion? stored = await db.FieldUnicityCriteria
            .FirstOrDefaultAsync(criterion => criterion.Id == CriterionId);

        if (stored is null)
        {
            return;
        }

        db.FieldUnicityCriteria.Remove(stored);
        await db.SaveChangesAsync();

        Navigation.NavigateTo("/config/field-unicity");
    }
}

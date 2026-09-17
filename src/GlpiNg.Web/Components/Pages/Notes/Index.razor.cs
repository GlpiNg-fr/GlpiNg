using BlazorBootstrap;
using GlpiNg.Modules.Abstractions.Items;
using GlpiNg.Modules.Abstractions.Notes;
using GlpiNg.Web.Data;
using GlpiNg.Web.Models.Notes;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Web.Components.Pages.Notes;

/// <summary>
/// Écran « Outils &gt; Notes » : toutes les notes libres de l'installation, tous objets confondus —
/// équivalent de l'écran « Notes » que GLPI pose sous Outils.
///
/// Une note se crée et se modifie depuis l'onglet « Notes » de la fiche qui la porte (voir
/// <see cref="IItemNotes"/>) ; cet écran ne fait que les retrouver et les supprimer, avec un lien
/// vers l'objet porteur pour les quelques itemtypes que l'hôte sait résoudre.
/// </summary>
public partial class Index : ComponentBase
{
    [Inject]
    private IDbContextFactory<GlpiNgDbContext> DbFactory { get; set; } = null!;

    [Inject]
    private IItemNotes Notes { get; set; } = null!;

    [Inject]
    private ToastService ToastService { get; set; } = null!;

    private sealed record NoteRow(Notepad Note, string ItemLabel, string? ItemUrl);

    private List<NoteRow> _notes = [];
    private string _search = string.Empty;
    private int? _confirmDeleteId;

    protected override Task OnInitializedAsync() => LoadAsync();

    private async Task LoadAsync()
    {
        await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();

        IQueryable<Notepad> query = db.Notepads.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(_search))
        {
            string needle = _search.Trim();
            query = query.Where(note => note.Content.Contains(needle) || note.AuthorName.Contains(needle));
        }

        List<Notepad> notes = await query
            .OrderByDescending(note => note.CreatedAt)
            .Take(200)
            .ToListAsync();

        // Un aller-retour par itemtype connu plutôt qu'un par note : le nombre d'itemtypes qui
        // portent des notes reste petit (voir ItemTypes), là où le nombre de notes ne l'est pas.
        int[] computerIds = ItemIds(notes, ItemTypes.Computer);
        Dictionary<int, string> computerNames = computerIds.Length == 0
            ? []
            : await db.Computers.AsNoTracking()
                .Where(computer => computerIds.Contains(computer.Id))
                .ToDictionaryAsync(computer => computer.Id, computer => computer.Name);

        int[] articleIds = ItemIds(notes, ItemTypes.KnowledgeBaseArticle);
        Dictionary<int, string> articleSubjects = articleIds.Length == 0
            ? []
            : await db.KnowledgeBaseArticles.AsNoTracking()
                .Where(article => articleIds.Contains(article.Id))
                .ToDictionaryAsync(article => article.Id, article => article.Subject);

        _notes = notes.Select(note => note.ItemType switch
        {
            ItemTypes.Computer => new NoteRow(
                note,
                computerNames.TryGetValue(note.ItemId, out string? name) ? name : $"Ordinateur #{note.ItemId}",
                $"/parc/computer/{note.ItemId}"),
            ItemTypes.KnowledgeBaseArticle => new NoteRow(
                note,
                articleSubjects.TryGetValue(note.ItemId, out string? subject) ? subject : $"Article #{note.ItemId}",
                $"/tools/knowledgebase/article/{note.ItemId}"),
            _ => new NoteRow(note, $"{note.ItemType} #{note.ItemId}", null),
        }).ToList();

        _confirmDeleteId = null;
    }

    private static int[] ItemIds(List<Notepad> notes, string itemType) =>
        notes.Where(note => note.ItemType == itemType).Select(note => note.ItemId).Distinct().ToArray();

    private async Task DeleteAsync(int noteId)
    {
        await Notes.DeleteAsync(noteId);
        await LoadAsync();
        ToastService.Notify(new ToastMessage(ToastType.Success, "Note supprimée."));
    }
}

using GlpiNg.Modules.Abstractions.Notes;
using GlpiNg.Web.Data;
using GlpiNg.Web.Models.Notes;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Web.Services.Notes;

/// <summary>
/// Les règles des notes : lecture, ajout, modification, suppression. Rend <see cref="IItemNotes"/>
/// aux modules, qui n'ont pas accès au modèle de l'hôte.
///
/// Regroupées ici plutôt que recopiées dans chaque fiche pour la même raison que les documents :
/// une note ajoutée depuis un ordinateur et une note ajoutée depuis un article doivent produire
/// exactement la même ligne, avec le même auteur et la même date.
/// </summary>
public sealed class NoteService(IDbContextFactory<GlpiNgDbContext> dbFactory) : IItemNotes
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<ItemNote>> GetForItemAsync(
        string itemType, int itemId, CancellationToken cancellationToken = default)
    {
        await using GlpiNgDbContext db = await dbFactory.CreateDbContextAsync(cancellationToken);

        return await db.Notepads
            .AsNoTracking()
            .Where(note => note.ItemType == itemType && note.ItemId == itemId)
            .OrderByDescending(note => note.CreatedAt)
            .ThenByDescending(note => note.Id)
            .Select(note => new ItemNote(
                note.Id,
                note.Content,
                note.AuthorName,
                note.CreatedAt,
                note.UpdatedAt,
                note.LastEditorName))
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<int> CountForItemAsync(string itemType, int itemId, CancellationToken cancellationToken = default)
    {
        await using GlpiNgDbContext db = await dbFactory.CreateDbContextAsync(cancellationToken);

        return await db.Notepads
            .CountAsync(note => note.ItemType == itemType && note.ItemId == itemId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<int> AddAsync(
        string itemType,
        int itemId,
        string content,
        string authorName,
        int? authorUserId,
        CancellationToken cancellationToken = default)
    {
        await using GlpiNgDbContext db = await dbFactory.CreateDbContextAsync(cancellationToken);

        Notepad note = new()
        {
            ItemType = itemType,
            ItemId = itemId,
            Content = content.Trim(),
            AuthorName = authorName,
            AuthorUserId = authorUserId,
            CreatedAt = DateTime.UtcNow,
        };

        db.Notepads.Add(note);
        await db.SaveChangesAsync(cancellationToken);

        return note.Id;
    }

    /// <inheritdoc />
    public async Task UpdateAsync(int noteId, string content, string editorName, CancellationToken cancellationToken = default)
    {
        await using GlpiNgDbContext db = await dbFactory.CreateDbContextAsync(cancellationToken);

        Notepad? note = await db.Notepads.FirstOrDefaultAsync(item => item.Id == noteId, cancellationToken);

        if (note is null)
        {
            return;
        }

        string trimmed = content.Trim();

        // Une modification qui ne change rien ne doit pas déplacer la date ni le dernier auteur :
        // rouvrir une note pour la refermer telle quelle n'est pas une modification.
        if (note.Content == trimmed)
        {
            return;
        }

        note.Content = trimmed;
        note.UpdatedAt = DateTime.UtcNow;
        note.LastEditorName = editorName;

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(int noteId, CancellationToken cancellationToken = default)
    {
        await using GlpiNgDbContext db = await dbFactory.CreateDbContextAsync(cancellationToken);

        await db.Notepads.Where(note => note.Id == noteId).ExecuteDeleteAsync(cancellationToken);
    }
}

using BlazorBootstrap;
using GlpiNg.Web.Data;
using GlpiNg.Web.Models.Documents;
using GlpiNg.Web.Services.Documents;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Web.Components.Pages.Documents;

/// <summary>
/// Écran « Gestion &gt; Documents » : le fonds documentaire de l'installation.
///
/// Existe parce que les documents sont une entité globale et non des pièces jointes : un fichier
/// rattaché à un article, à un ordinateur ou à rien du tout doit rester trouvable et supprimable
/// quelque part. Les fiches se créent surtout depuis les objets qui les portent — cet écran sert à
/// les retrouver, les compter et faire le ménage.
/// </summary>
public partial class Index : ComponentBase
{
    [Inject]
    private IDbContextFactory<GlpiNgDbContext> DbFactory { get; set; } = null!;

    [Inject]
    private DocumentService Documents { get; set; } = null!;

    [Inject]
    private ToastService ToastService { get; set; } = null!;

    [CascadingParameter]
    private Task<AuthenticationState>? AuthStateTask { get; set; }

    /// <summary>Un document et le nombre d'objets qui s'y rattachent — ce que la suppression emporterait.</summary>
    private sealed record DocumentRow(Document Document, int AttachmentCount);

    private List<DocumentRow> _documents = [];
    private string _search = string.Empty;
    private bool _isUploading;
    private int? _confirmDeleteId;
    private string? _error;

    /// <summary>Même plafond que depuis une fiche d'article — voir Detail.razor.cs du module KB.</summary>
    private const long MaxUploadBytes = 64L * 1024 * 1024;

    protected override Task OnInitializedAsync() => LoadAsync();

    private async Task LoadAsync()
    {
        await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();

        IQueryable<Document> query = db.Documents.AsNoTracking().Include(document => document.Category);

        if (!string.IsNullOrWhiteSpace(_search))
        {
            string needle = _search.Trim();
            query = query.Where(document => document.Name.Contains(needle) || document.FileName.Contains(needle));
        }

        // Le comptage est projeté dans la requête plutôt que chargé par Include : seul le nombre
        // est affiché, et remonter les lignes de rattachement pour les compter côté client serait
        // payer le transport de tout ce qu'on jette.
        _documents = await query
            .OrderByDescending(document => document.CreatedAt)
            .Take(200)
            .Select(document => new DocumentRow(document, document.Items.Count))
            .ToListAsync();

        _confirmDeleteId = null;
    }

    private async Task UploadAsync(InputFileChangeEventArgs args)
    {
        _isUploading = true;
        _error = null;

        try
        {
            IBrowserFile file = args.File;
            await using Stream content = file.OpenReadStream(MaxUploadBytes);

            await Documents.UploadAsync(
                file.Name,
                content,
                file.ContentType,
                await CurrentUserNameAsync(),
                await CurrentUserIdAsync(),
                categoryId: null);

            await LoadAsync();
            ToastService.Notify(new ToastMessage(ToastType.Success, $"« {file.Name} » téléversé."));
        }
        catch (IOException ex)
        {
            _error = $"Fichier refusé (taille maximale {MaxUploadBytes / (1024 * 1024)} Mo) : {ex.Message}";
        }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException or DbUpdateException)
        {
            _error = $"Échec du téléversement : {ex.Message}";
        }
        finally
        {
            _isUploading = false;
        }
    }

    private async Task DeleteAsync(int documentId)
    {
        try
        {
            await Documents.DeleteAsync(documentId);
            await LoadAsync();
            ToastService.Notify(new ToastMessage(ToastType.Success, "Document supprimé."));
        }
        catch (Exception ex) when (ex is InvalidOperationException or DbUpdateException)
        {
            _confirmDeleteId = null;
            ToastService.Notify(new ToastMessage(ToastType.Danger, $"Échec de la suppression : {ex.Message}"));
        }
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} o",
        < 1024 * 1024 => $"{bytes / 1024.0:0.#} Ko",
        < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024):0.#} Mo",
        _ => $"{bytes / (1024.0 * 1024 * 1024):0.##} Go",
    };

    private async Task<int?> CurrentUserIdAsync()
    {
        if (AuthStateTask is null)
        {
            return null;
        }

        AuthenticationState authState = await AuthStateTask;
        string? claim = authState.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        return int.TryParse(claim, out int userId) ? userId : null;
    }

    private async Task<string?> CurrentUserNameAsync()
    {
        if (AuthStateTask is null)
        {
            return null;
        }

        AuthenticationState authState = await AuthStateTask;
        return authState.User.Identity?.Name;
    }
}

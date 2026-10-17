using BlazorBootstrap;
using GlpiNg.Modules.Abstractions.Localization;
using GlpiNg.Web.Data;
using GlpiNg.Web.Models;
using GlpiNg.Web.Services;
using GlpiNg.Web.Services.Mail;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Web.Components.Pages.MailCollectors;

public partial class Detail : ComponentBase, IAsyncDisposable
{
    [Parameter]
    public int CollectorId { get; set; }

    [Inject]
    private IDbContextFactory<GlpiNgDbContext> DbFactory { get; set; } = null!;

    [Inject]
    private AuthSecretProtector Protector { get; set; } = null!;

    [Inject]
    private MailCollectorService Collectors { get; set; } = null!;

    [Inject]
    private NavigationManager Nav { get; set; } = null!;

    [Inject]
    private ToastService ToastService { get; set; } = null!;

    private GlpiNgDbContext? _db;
    private MailCollector? _collector;
    private List<GlpiEntity> _entities = [];
    private string? _password;
    private bool _isSaving;
    private bool _isCollecting;

    protected override async Task OnInitializedAsync()
    {
        _db = await DbFactory.CreateDbContextAsync();
        _collector = await _db.MailCollectors.FirstOrDefaultAsync(c => c.Id == CollectorId);
        _entities = await _db.Entities.AsNoTracking().OrderBy(e => e.Name).ToListAsync();
    }

    private async Task SaveAsync()
    {
        if (_db is null || _collector is null)
        {
            return;
        }

        _isSaving = true;

        try
        {
            // Un champ mot de passe laissé vide conserve celui enregistré : il n'est jamais
            // réaffiché, le vider ne peut donc pas être la volonté de l'effacer.
            if (!string.IsNullOrEmpty(_password))
            {
                _collector.PasswordProtected = Protector.Protect(_password);
                _password = null;
            }

            _collector.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            ToastService.Notify(new ToastMessage(ToastType.Success, Tr.T("Collecteur enregistré.")));
        }
        finally
        {
            _isSaving = false;
        }
    }

    /// <summary>
    /// Relève immédiate, pour vérifier une configuration sans attendre la tâche cron. Les tickets
    /// qu'elle ouvre sont des tickets comme les autres : ce n'est pas un essai à blanc.
    /// </summary>
    private async Task CollectNowAsync()
    {
        if (_collector is null)
        {
            return;
        }

        _isCollecting = true;

        try
        {
            (int imported, string? error) = await Collectors.CollectAsync(_collector.Id);

            ToastService.Notify(error is null
                ? new ToastMessage(ToastType.Success, Tr.T("Relève terminée : {0} message(s).", imported))
                : new ToastMessage(ToastType.Danger, Tr.T("Relève en échec : {0}", error)));

            // Le compte rendu est écrit par le service dans son propre contexte : il faut relire
            // le collecteur pour que la fiche l'affiche.
            if (_db is not null)
            {
                await _db.Entry(_collector).ReloadAsync();
            }
        }
        finally
        {
            _isCollecting = false;
        }
    }

    private async Task DeleteAsync()
    {
        if (_db is null || _collector is null)
        {
            return;
        }

        _db.MailCollectors.Remove(_collector);
        await _db.SaveChangesAsync();

        Nav.NavigateTo("/config/mail-collectors");
    }

    public async ValueTask DisposeAsync()
    {
        if (_db is not null)
        {
            await _db.DisposeAsync();
        }
    }
}

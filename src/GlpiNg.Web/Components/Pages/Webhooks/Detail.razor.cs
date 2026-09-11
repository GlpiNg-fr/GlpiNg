using BlazorBootstrap;
using GlpiNg.Web.Data;
using GlpiNg.Web.Models.Notifications;
using GlpiNg.Web.Models.Webhooks;
using GlpiNg.Web.Services;
using GlpiNg.Web.Services.Webhooks;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Web.Components.Pages.Webhooks;

public partial class Detail : ComponentBase
{
    [Parameter]
    public int WebhookId { get; set; }

    [Inject]
    private IDbContextFactory<GlpiNgDbContext> DbFactory { get; set; } = null!;

    [Inject]
    private SettingsCacheService SettingsStore { get; set; } = null!;

    [Inject]
    private AuthSecretProtector Protector { get; set; } = null!;

    [Inject]
    private WebhookSender Sender { get; set; } = null!;

    [Inject]
    private NavigationManager Navigation { get; set; } = null!;

    [Inject]
    private ToastService ToastService { get; set; } = null!;

    private Webhook? _hook;
    private List<WebhookHeader> _headers = [];

    private string _secretPlain = string.Empty;
    private bool _isSaving;

    private string _addHeaderName = string.Empty;
    private string _addHeaderValue = string.Empty;

    private bool _isTesting;
    private WebhookSendResult? _testResult;
    private string? _testPayload;

    protected override async Task OnParametersSetAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();

        _hook = await db.Webhooks.AsNoTracking().FirstOrDefaultAsync(w => w.Id == WebhookId);
        _headers = _hook is null
            ? []
            : await db.WebhookHeaders.AsNoTracking()
                .Where(h => h.WebhookId == WebhookId)
                .OrderBy(h => h.Name)
                .ToListAsync();
    }

    private void OnSecretChanged(string secret) => _secretPlain = secret;

    private async Task SaveAsync()
    {
        if (_hook is null)
        {
            return;
        }

        _isSaving = true;

        try
        {
            await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();

            Webhook? stored = await db.Webhooks.FirstOrDefaultAsync(w => w.Id == WebhookId);
            if (stored is null)
            {
                return;
            }

            stored.Name = _hook.Name;
            stored.Comment = _hook.Comment;
            stored.IsActive = _hook.IsActive;
            stored.ItemType = _hook.ItemType;
            stored.Event = _hook.Event;
            stored.Url = _hook.Url;
            stored.Method = _hook.Method;
            stored.PayloadMode = _hook.PayloadMode;
            stored.CustomPayload = _hook.CustomPayload;
            stored.SaveResponseBody = _hook.SaveResponseBody;
            stored.UpdatedAt = DateTime.UtcNow;

            // Champ laissé vide = secret inchangé : il est chiffré donc inaffichable, et l'admin
            // qui n'y touche pas ne demande pas sa suppression. Pour l'effacer, on saisit un seul
            // espace — que Trim ramène à vide, ce qui se distingue du champ jamais touché.
            if (_secretPlain.Length > 0)
            {
                stored.SecretProtected = _secretPlain.Trim().Length == 0
                    ? null
                    : Protector.Protect(_secretPlain);
            }

            await db.SaveChangesAsync();

            _secretPlain = string.Empty;
            ToastService.Notify(new ToastMessage(ToastType.Success, "Webhook enregistré."));

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

        Webhook? stored = await db.Webhooks.FirstOrDefaultAsync(w => w.Id == WebhookId);
        if (stored is null)
        {
            return;
        }

        db.Webhooks.Remove(stored);
        await db.SaveChangesAsync();

        Navigation.NavigateTo("/config/webhooks");
    }

    private async Task AddHeaderAsync()
    {
        if (_hook is null || string.IsNullOrWhiteSpace(_addHeaderName))
        {
            return;
        }

        await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();

        string name = _addHeaderName.Trim();

        if (await db.WebhookHeaders.AnyAsync(h => h.WebhookId == WebhookId && h.Name == name))
        {
            ToastService.Notify(new ToastMessage(ToastType.Danger, "Cet en-tête est déjà défini."));
            return;
        }

        db.WebhookHeaders.Add(new WebhookHeader
        {
            WebhookId = WebhookId,
            Name = name,
            Value = _addHeaderValue,
        });

        await db.SaveChangesAsync();

        _addHeaderName = string.Empty;
        _addHeaderValue = string.Empty;

        await LoadAsync();
    }

    private async Task RemoveHeaderAsync(int headerId)
    {
        await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();

        WebhookHeader? header = await db.WebhookHeaders.FirstOrDefaultAsync(h => h.Id == headerId);
        if (header is null)
        {
            return;
        }

        db.WebhookHeaders.Remove(header);
        await db.SaveChangesAsync();

        await LoadAsync();
    }

    /// <summary>
    /// Envoie une livraison réelle construite depuis la version enregistrée, sans passer par la
    /// file : c'est immédiat, et le résultat s'affiche au lieu d'aller se ranger dans un journal
    /// qu'il faudrait ensuite aller consulter.
    /// </summary>
    private async Task SendTestAsync()
    {
        if (_hook is null)
        {
            return;
        }

        _isTesting = true;
        _testResult = null;
        _testPayload = null;

        try
        {
            await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();

            Webhook? stored = await db.Webhooks
                .AsNoTracking()
                .Include(w => w.Headers)
                .FirstOrDefaultAsync(w => w.Id == WebhookId);

            if (stored is null)
            {
                return;
            }

            QueuedWebhook delivery = WebhookDispatchService.BuildDelivery(
                stored, stored.ItemType, stored.Event, 0, SampleVariables(stored), DateTimeOffset.UtcNow);

            // Le test affiche la réponse même quand le webhook ne la conserve pas : c'est
            // justement ce qu'on cherche à voir en testant.
            delivery.SaveResponseBody = true;
            _testPayload = delivery.Payload;

            WebhookSettings settings = await SettingsStore.ReadSectionAsync<WebhookSettings>(
                WebhookDispatchService.SettingsSection);

            _testResult = await Sender.SendAsync(delivery, settings);
        }
        finally
        {
            _isTesting = false;
        }
    }

    /// <summary>
    /// Valeurs d'exemple pour les balises de l'événement. Chacune est préfixée « TEST » : un
    /// destinataire qui agit sur ce qu'il reçoit (ouvrir un ticket, alerter une astreinte) doit
    /// pouvoir distinguer un essai d'un vrai événement, sans avoir à deviner.
    /// </summary>
    private static Dictionary<string, string?> SampleVariables(Webhook hook)
    {
        Dictionary<string, string?> variables = [];

        foreach (NotificationTag tag in NotificationEventCatalog.Find(hook.ItemType, hook.Event)?.Tags ?? [])
        {
            variables[tag.Key] = $"TEST {tag.Label}";
        }

        return variables;
    }
}

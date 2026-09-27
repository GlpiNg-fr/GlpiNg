using BlazorBootstrap;
using GlpiNg.Web.Data;
using GlpiNg.Web.Models;
using GlpiNg.Web.Models.Notifications;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Web.Components.Pages.Notifications.Rules;

public partial class Detail : ComponentBase, IAsyncDisposable
{
    [Parameter]
    public int RuleId { get; set; }

    [Inject]
    private IDbContextFactory<GlpiNgDbContext> DbFactory { get; set; } = null!;

    [Inject]
    private NavigationManager Nav { get; set; } = null!;

    [Inject]
    private ToastService ToastService { get; set; } = null!;

    private GlpiNgDbContext? _db;
    private Notification? _rule;
    private List<NotificationTemplate> _allTemplates = [];
    private List<GlpiUser> _allUsers = [];
    private List<GlpiGroup> _allGroups = [];
    private List<NotificationRecipient> _recipients = [];
    private bool _isSaving;

    private NotificationRecipientType _addType = NotificationRecipientType.FixedEmail;
    private string _addEmail = string.Empty;
    private int _addUserId;
    private int _addGroupId;

    private bool CanAddRecipient => _addType switch
    {
        NotificationRecipientType.FixedEmail => !string.IsNullOrWhiteSpace(_addEmail),
        NotificationRecipientType.User => _addUserId != 0,
        NotificationRecipientType.Group => _addGroupId != 0,
        NotificationRecipientType.AllAdmins => true,
        _ => false
    };

    protected override async Task OnInitializedAsync()
    {
        _db = await DbFactory.CreateDbContextAsync();
        _rule = await _db.Notifications.FirstOrDefaultAsync(r => r.Id == RuleId);

        if (_rule is not null)
        {
            await ReloadReferenceDataAsync();
        }
    }

    private async Task ReloadReferenceDataAsync()
    {
        if (_db is null || _rule is null)
        {
            return;
        }

        _allTemplates = await _db.NotificationTemplates.AsNoTracking().OrderBy(t => t.Name).ToListAsync();
        _allUsers = await _db.Users.AsNoTracking()
            .Where(u => u.IsActive && u.Email != null && u.Email != "")
            .OrderBy(u => u.DisplayName ?? u.UserName)
            .ToListAsync();
        _allGroups = await _db.Groups.AsNoTracking()
            .Where(g => g.CanBeNotified)
            .OrderBy(g => g.Name)
            .ToListAsync();

        _recipients = await _db.NotificationRecipients.AsNoTracking()
            .Where(r => r.NotificationId == _rule.Id)
            .Include(r => r.User)
            .Include(r => r.Group)
            .ToListAsync();
    }

    private async Task SaveAsync()
    {
        if (_db is null || _rule is null)
        {
            return;
        }

        _isSaving = true;

        try
        {
            _rule.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            ToastService.Notify(new ToastMessage(ToastType.Success, "Notification enregistrée."));
        }
        finally
        {
            _isSaving = false;
        }
    }

    private async Task DeleteAsync()
    {
        if (_db is null || _rule is null)
        {
            return;
        }

        _db.Notifications.Remove(_rule);
        await _db.SaveChangesAsync();

        Nav.NavigateTo("/config/notifications/rules");
    }

    private async Task AddRecipientAsync()
    {
        if (_db is null || _rule is null || !CanAddRecipient)
        {
            return;
        }

        NotificationRecipient recipient = new() { NotificationId = _rule.Id, Type = _addType };
        switch (_addType)
        {
            case NotificationRecipientType.FixedEmail:
                recipient.Email = _addEmail.Trim();
                break;
            case NotificationRecipientType.User:
                recipient.UserId = _addUserId;
                break;
            case NotificationRecipientType.Group:
                recipient.GroupId = _addGroupId;
                break;
        }

        _db.NotificationRecipients.Add(recipient);
        await _db.SaveChangesAsync();

        _addEmail = string.Empty;
        _addUserId = 0;
        _addGroupId = 0;

        await ReloadReferenceDataAsync();
    }

    private async Task RemoveRecipientAsync(int recipientId)
    {
        if (_db is null)
        {
            return;
        }

        NotificationRecipient? recipient = await _db.NotificationRecipients.FirstOrDefaultAsync(r => r.Id == recipientId);
        if (recipient is null)
        {
            return;
        }

        _db.NotificationRecipients.Remove(recipient);
        await _db.SaveChangesAsync();

        await ReloadReferenceDataAsync();
    }

    private static string RecipientTypeLabel(NotificationRecipientType type) => type switch
    {
        NotificationRecipientType.FixedEmail => "Adresse e-mail",
        NotificationRecipientType.User => "Utilisateur",
        NotificationRecipientType.Group => "Groupe",
        NotificationRecipientType.AllAdmins => "Tous les administrateurs",
        _ => type.ToString()
    };

    private static string RecipientTargetLabel(NotificationRecipient recipient) => recipient.Type switch
    {
        NotificationRecipientType.FixedEmail => recipient.Email ?? "—",
        NotificationRecipientType.User => recipient.User?.DisplayName ?? recipient.User?.UserName ?? "Utilisateur supprimé",
        NotificationRecipientType.Group => recipient.Group?.Name ?? "Groupe supprimé",
        NotificationRecipientType.AllAdmins => "—",
        _ => "—"
    };

    public async ValueTask DisposeAsync()
    {
        if (_db is not null)
        {
            await _db.DisposeAsync();
        }
    }
}

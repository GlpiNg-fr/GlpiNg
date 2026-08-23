using BlazorBootstrap;
using GlpiNg.Modules.Abstractions.Cron;
using GlpiNg.Modules.Cron;
using GlpiNg.Modules.Cron.Models;
using GlpiNg.Web.Data;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Web.Components.Pages.AutomaticActions;

public partial class Detail : ComponentBase
{
    [Parameter]
    public string TaskKey { get; set; } = string.Empty;

    [Inject]
    private IDbContextFactory<GlpiNgDbContext> DbFactory { get; set; } = null!;

    [Inject]
    private IEnumerable<ICronTask> Tasks { get; set; } = null!;

    [Inject]
    private AutomaticActionRunner Runner { get; set; } = null!;

    [Inject]
    private ToastService ToastService { get; set; } = null!;

    [Inject]
    private NavigationManager Nav { get; set; } = null!;

    private ICronTask? _task;
    private AutomaticActionState? _state;
    private List<AutomaticActionRunLog> _runLog = [];
    private int _frequencyMinutes;
    private bool _isSaving;
    private bool _isRunning;

    protected override async Task OnInitializedAsync()
    {
        _task = Tasks.FirstOrDefault(t => t.Key == TaskKey);
        if (_task is null)
        {
            Nav.NavigateTo("/config/automatic-actions");
            return;
        }

        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        if (_task is null)
        {
            return;
        }

        await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();
        AutomaticActionState? state = await db.AutomaticActionStates.FirstOrDefaultAsync(s => s.TaskKey == TaskKey);
        if (state is null)
        {
            state = new AutomaticActionState { TaskKey = TaskKey, FrequencyMinutes = _task.DefaultFrequencyMinutes };
            db.AutomaticActionStates.Add(state);
            await db.SaveChangesAsync();
        }

        _state = state;
        _frequencyMinutes = state.FrequencyMinutes ?? _task.DefaultFrequencyMinutes;

        _runLog = await db.AutomaticActionRunLogs
            .AsNoTracking()
            .Where(l => l.TaskKey == TaskKey)
            .OrderByDescending(l => l.RanAt)
            .Take(50)
            .ToListAsync();
    }

    private async Task ToggleEnabledAsync()
    {
        await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();
        AutomaticActionState? state = await db.AutomaticActionStates.FirstOrDefaultAsync(s => s.TaskKey == TaskKey);
        if (state is null)
        {
            return;
        }

        state.IsEnabled = !state.IsEnabled;
        await db.SaveChangesAsync();
        await LoadAsync();
    }

    private async Task SaveFrequencyAsync()
    {
        _isSaving = true;

        try
        {
            int clamped = CronIntervalState.Clamp(_frequencyMinutes);

            await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();
            AutomaticActionState? state = await db.AutomaticActionStates.FirstOrDefaultAsync(s => s.TaskKey == TaskKey);
            if (state is null)
            {
                return;
            }

            state.FrequencyMinutes = clamped;
            await db.SaveChangesAsync();
            _frequencyMinutes = clamped;

            ToastService.Notify(new ToastMessage(ToastType.Success, "Fréquence enregistrée."));
        }
        finally
        {
            _isSaving = false;
        }

        await LoadAsync();
    }

    private async Task RunNowAsync()
    {
        _isRunning = true;

        try
        {
            bool success = await Runner.RunAsync(TaskKey, CancellationToken.None);
            ToastService.Notify(new ToastMessage(
                success ? ToastType.Success : ToastType.Danger,
                success ? "Tâche exécutée avec succès." : "L'exécution a échoué — voir le journal ci-dessous."));
        }
        finally
        {
            _isRunning = false;
        }

        await LoadAsync();
    }
}

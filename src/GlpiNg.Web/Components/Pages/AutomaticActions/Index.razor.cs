using BlazorBootstrap;
using GlpiNg.Modules.Abstractions.Cron;
using GlpiNg.Modules.Cron;
using GlpiNg.Modules.Cron.Models;
using GlpiNg.Web.Data;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using GlpiNg.Modules.Abstractions.Localization;

namespace GlpiNg.Web.Components.Pages.AutomaticActions;

public partial class Index : ComponentBase
{
    [Inject]
    private IDbContextFactory<GlpiNgDbContext> DbFactory { get; set; } = null!;

    [Inject]
    private IEnumerable<ICronTask> Tasks { get; set; } = null!;

    [Inject]
    private AutomaticActionRunner Runner { get; set; } = null!;

    [Inject]
    private ToastService ToastService { get; set; } = null!;

    private sealed record Row(ICronTask Task, AutomaticActionState State);

    private List<Row> _rows = [];
    private readonly HashSet<string> _runningKeys = [];

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();
        Dictionary<string, AutomaticActionState> states = await db.AutomaticActionStates
            .AsNoTracking()
            .ToDictionaryAsync(s => s.TaskKey);

        List<Row> rows = [];
        bool hasNewState = false;

        foreach (ICronTask task in Tasks.OrderBy(t => t.Name))
        {
            if (!states.TryGetValue(task.Key, out AutomaticActionState? state))
            {
                state = new AutomaticActionState { TaskKey = task.Key, FrequencyMinutes = task.DefaultFrequencyMinutes };
                db.AutomaticActionStates.Add(state);
                hasNewState = true;
            }

            rows.Add(new Row(task, state));
        }

        if (hasNewState)
        {
            await db.SaveChangesAsync();
        }

        _rows = rows;
    }

    private async Task ToggleEnabledAsync(Row row)
    {
        await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();
        AutomaticActionState? state = await db.AutomaticActionStates.FirstOrDefaultAsync(s => s.TaskKey == row.Task.Key);
        if (state is null)
        {
            return;
        }

        state.IsEnabled = !state.IsEnabled;
        await db.SaveChangesAsync();
        await LoadAsync();
    }

    private async Task RunNowAsync(Row row)
    {
        _runningKeys.Add(row.Task.Key);

        try
        {
            bool success = await Runner.RunAsync(row.Task.Key, CancellationToken.None);
            ToastService.Notify(new ToastMessage(
                success ? ToastType.Success : ToastType.Danger,
                success ? Tr.T("« {0} » exécutée avec succès.", row.Task.Name) : Tr.T("« {0} » a échoué.", row.Task.Name)));
        }
        finally
        {
            _runningKeys.Remove(row.Task.Key);
        }

        await LoadAsync();
    }

    private static string FrequencyLabel(Row row) => FrequencyFormat.Label(row.State.FrequencyMinutes ?? row.Task.DefaultFrequencyMinutes);

    private string LastRunLabel(AutomaticActionState state) =>
        state.LastRunAt is DateTime lastRunAt ? Display.DateTime(lastRunAt)! : Tr.T("Jamais exécutée");
}

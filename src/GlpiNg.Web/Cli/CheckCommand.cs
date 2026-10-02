using System.ComponentModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Spectre.Console;
using Spectre.Console.Cli;

namespace GlpiNg.Web.Cli;

public class CheckCommand : AsyncCommand<CheckCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandOption("--fix")]
        [Description("Applique les migrations en attente (SQL Server uniquement)")]
        public bool Fix { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellation)
    {
        await using var db = ConsoleDbContext.Create();

        int issues = 0;

        issues += await CheckMigrationsAsync(db, settings.Fix);
        issues += await CheckTablesAsync(db);

        AnsiConsole.WriteLine();
        if (issues == 0)
            AnsiConsole.MarkupLine("[green bold]Aucun problème détecté.[/]");
        else
            AnsiConsole.MarkupLine($"[red bold]{issues} problème(s) détecté(s).[/]");

        return issues == 0 ? 0 : 1;
    }

    private static async Task<int> CheckMigrationsAsync(DbContext db, bool fix)
    {
        AnsiConsole.MarkupLine("[bold]Migrations[/]");

        var applied = (await db.Database.GetAppliedMigrationsAsync()).ToList();
        var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();

        AnsiConsole.MarkupLine($"  Appliquées : [green]{applied.Count}[/]");

        if (pending.Count == 0)
        {
            AnsiConsole.MarkupLine("  En attente : [green]0[/]");
            return 0;
        }

        AnsiConsole.MarkupLine($"  En attente : [red]{pending.Count}[/]");
        foreach (var m in pending)
            AnsiConsole.MarkupLine($"    [yellow]- {m.EscapeMarkup()}[/]");

        if (!fix)
        {
            AnsiConsole.MarkupLine("  [dim]Utilisez --fix pour appliquer les migrations en attente.[/]");
            return pending.Count;
        }

        var result = await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Application des migrations...", async _ =>
            {
                try
                {
                    await db.Database.MigrateAsync();
                    return (string?)null;
                }
                catch (Exception ex)
                {
                    return ex.Message;
                }
            });

        if (result is not null)
        {
            AnsiConsole.MarkupLine($"  [red]Échec : {result.EscapeMarkup()}[/]");
            return pending.Count;
        }

        AnsiConsole.MarkupLine($"  [green]{pending.Count} migration(s) appliquée(s).[/]");
        return 0;
    }

    private static async Task<int> CheckTablesAsync(DbContext db)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Tables[/]");

        var model = db.Model;
        var entityTypes = model.GetEntityTypes()
            .Where(e => !e.IsOwned())
            .OrderBy(e => e.GetTableName())
            .ToList();

        var existingTables = await GetExistingTablesAsync(db);
        var existingColumns = await GetExistingColumnsAsync(db);

        int issues = 0;

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Table")
            .AddColumn("État")
            .AddColumn("Détails");

        foreach (var entityType in entityTypes)
        {
            var tableName = entityType.GetTableName();
            if (tableName is null) continue;

            if (!existingTables.Contains(tableName))
            {
                table.AddRow(
                    $"[red]{tableName.EscapeMarkup()}[/]",
                    "[red]Manquante[/]",
                    "");
                issues++;
                continue;
            }

            var expectedColumns = entityType.GetProperties()
                .Select(p => p.GetColumnName())
                .Where(c => c is not null)
                .Cast<string>()
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            existingColumns.TryGetValue(tableName, out var actualColumns);
            actualColumns ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var missing = expectedColumns.Except(actualColumns, StringComparer.OrdinalIgnoreCase).OrderBy(c => c).ToList();
            var extra = actualColumns.Except(expectedColumns, StringComparer.OrdinalIgnoreCase).OrderBy(c => c).ToList();

            if (missing.Count == 0 && extra.Count == 0)
            {
                table.AddRow(
                    tableName.EscapeMarkup(),
                    "[green]OK[/]",
                    $"[dim]{expectedColumns.Count} colonnes[/]");
            }
            else
            {
                var details = new List<string>();
                if (missing.Count > 0)
                    details.Add($"[red]manquantes: {string.Join(", ", missing).EscapeMarkup()}[/]");
                if (extra.Count > 0)
                    details.Add($"[yellow]inconnues: {string.Join(", ", extra).EscapeMarkup()}[/]");

                table.AddRow(
                    $"[yellow]{tableName.EscapeMarkup()}[/]",
                    missing.Count > 0 ? "[red]Écart[/]" : "[yellow]Écart[/]",
                    string.Join(" | ", details));

                issues += missing.Count;
            }
        }

        AnsiConsole.Write(table);
        return issues;
    }

    private static async Task<HashSet<string>> GetExistingTablesAsync(DbContext db)
    {
        var tables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var conn = db.Database.GetDbConnection();
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = db.Database.ProviderName switch
        {
            "Microsoft.EntityFrameworkCore.SqlServer" =>
                "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'",
            "Pomelo.EntityFrameworkCore.MySql" =>
                "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_TYPE = 'BASE TABLE'",
            "Npgsql.EntityFrameworkCore.PostgreSQL" =>
                "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' AND table_type = 'BASE TABLE'",
            _ => throw new NotSupportedException($"Provider non supporté : {db.Database.ProviderName}")
        };

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            tables.Add(reader.GetString(0));

        return tables;
    }

    private static async Task<Dictionary<string, HashSet<string>>> GetExistingColumnsAsync(DbContext db)
    {
        var result = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var conn = db.Database.GetDbConnection();

        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = db.Database.ProviderName switch
        {
            "Microsoft.EntityFrameworkCore.SqlServer" =>
                "SELECT TABLE_NAME, COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS",
            "Pomelo.EntityFrameworkCore.MySql" =>
                "SELECT TABLE_NAME, COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE()",
            "Npgsql.EntityFrameworkCore.PostgreSQL" =>
                "SELECT table_name, column_name FROM information_schema.columns WHERE table_schema = 'public'",
            _ => throw new NotSupportedException($"Provider non supporté : {db.Database.ProviderName}")
        };

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var tableName = reader.GetString(0);
            var columnName = reader.GetString(1);

            if (!result.TryGetValue(tableName, out var columns))
            {
                columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                result[tableName] = columns;
            }
            columns.Add(columnName);
        }

        return result;
    }
}

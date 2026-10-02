using System.ComponentModel;
using Microsoft.EntityFrameworkCore;
using Spectre.Console;
using Spectre.Console.Cli;

namespace GlpiNg.Web.Cli;

public class DisableUserCommand : AsyncCommand<DisableUserCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<username>")]
        [Description("Nom d'utilisateur à désactiver")]
        public string Username { get; init; } = string.Empty;
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellation)
    {
        await using var db = ConsoleDbContext.Create();

        var user = await db.Users.FirstOrDefaultAsync(u => u.UserName == settings.Username);
        if (user is null)
        {
            AnsiConsole.MarkupLine($"[red]Utilisateur introuvable : {settings.Username.EscapeMarkup()}[/]");
            return 1;
        }

        if (!user.IsActive)
        {
            AnsiConsole.MarkupLine($"[yellow]L'utilisateur '{settings.Username.EscapeMarkup()}' est déjà désactivé.[/]");
            return 0;
        }

        user.IsActive = false;
        await db.SaveChangesAsync();

        AnsiConsole.MarkupLine($"[green]Utilisateur '[bold]{settings.Username.EscapeMarkup()}[/]' désactivé.[/]");
        return 0;
    }
}

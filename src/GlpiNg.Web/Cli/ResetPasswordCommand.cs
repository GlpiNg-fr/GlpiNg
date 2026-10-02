using System.ComponentModel;
using GlpiNg.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Spectre.Console;
using Spectre.Console.Cli;

namespace GlpiNg.Web.Cli;

public class ResetPasswordCommand : AsyncCommand<ResetPasswordCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<username>")]
        [Description("Nom d'utilisateur dont le mot de passe sera réinitialisé")]
        public string Username { get; init; } = string.Empty;

        [CommandOption("-p|--password <PASSWORD>")]
        [Description("Nouveau mot de passe (généré automatiquement si omis)")]
        public string? Password { get; init; }
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

        var newPassword = settings.Password ?? GeneratePassword();

        var hasher = new PasswordHasher<GlpiUser>();
        user.PasswordHash = hasher.HashPassword(user, newPassword);
        await db.SaveChangesAsync();

        AnsiConsole.MarkupLine($"[green]Mot de passe de '[bold]{settings.Username.EscapeMarkup()}[/]' réinitialisé.[/]");
        if (settings.Password is null)
            AnsiConsole.MarkupLine($"Nouveau mot de passe : [bold]{newPassword.EscapeMarkup()}[/]");

        return 0;
    }

    private static string GeneratePassword()
    {
        const string chars = "abcdefghijkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789!@#$%";
        var buffer = new char[16];
        for (int i = 0; i < buffer.Length; i++)
            buffer[i] = chars[Random.Shared.Next(chars.Length)];
        return new string(buffer);
    }
}

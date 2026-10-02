using System.ComponentModel;
using GlpiNg.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Spectre.Console;
using Spectre.Console.Cli;

namespace GlpiNg.Web.Cli;

public class CreateUserCommand : AsyncCommand<CreateUserCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<username>")]
        [Description("Nom d'utilisateur à créer")]
        public string Username { get; init; } = string.Empty;

        [CommandOption("-p|--password <PASSWORD>")]
        [Description("Mot de passe (généré automatiquement si omis)")]
        public string? Password { get; init; }

        [CommandOption("--admin")]
        [Description("Créer l'utilisateur en tant qu'administrateur")]
        public bool IsAdmin { get; init; }

        [CommandOption("--display-name <NAME>")]
        [Description("Nom d'affichage de l'utilisateur")]
        public string? DisplayName { get; init; }

        [CommandOption("--email <EMAIL>")]
        [Description("Adresse e-mail de l'utilisateur")]
        public string? Email { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellation)
    {
        await using var db = ConsoleDbContext.Create();

        var existing = await db.Users.AnyAsync(u => u.UserName == settings.Username);
        if (existing)
        {
            AnsiConsole.MarkupLine($"[red]L'utilisateur '{settings.Username.EscapeMarkup()}' existe déjà.[/]");
            return 1;
        }

        var password = settings.Password ?? GeneratePassword();
        var hasher = new PasswordHasher<GlpiUser>();

        var user = new GlpiUser
        {
            UserName = settings.Username,
            DisplayName = settings.DisplayName,
            Email = settings.Email,
            IsAdmin = settings.IsAdmin,
            PasswordHash = string.Empty,
        };
        user.PasswordHash = hasher.HashPassword(user, password);

        db.Users.Add(user);
        await db.SaveChangesAsync();

        AnsiConsole.MarkupLine($"[green]Utilisateur '[bold]{settings.Username.EscapeMarkup()}[/]' créé.[/]");
        if (settings.IsAdmin)
            AnsiConsole.MarkupLine("[yellow]Rôle : administrateur[/]");
        if (settings.Password is null)
            AnsiConsole.MarkupLine($"Mot de passe : [bold]{password.EscapeMarkup()}[/]");

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

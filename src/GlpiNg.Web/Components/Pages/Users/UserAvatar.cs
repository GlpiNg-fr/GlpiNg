using GlpiNg.Web.Models;

namespace GlpiNg.Web.Components.Pages.Users;

/// <summary>Avatar en initiales colorées façon GLPI, utilisé dans la liste et la fiche utilisateur.</summary>
internal static class UserAvatar
{
    private static readonly string[] Palette =
        ["#e63946", "#457b9d", "#2a9d8f", "#e76f51", "#8338ec", "#3a86ff", "#fb8500", "#6d6875"];

    public static string Initials(GlpiUser user)
    {
        string? first = user.FirstName?.Trim();
        string? last = user.LastName?.Trim();

        if (!string.IsNullOrEmpty(first) && !string.IsNullOrEmpty(last))
        {
            return $"{first[0]}{last[0]}".ToUpperInvariant();
        }

        return user.UserName.Length > 0 ? user.UserName[..1].ToUpperInvariant() : "?";
    }

    public static string Color(GlpiUser user)
    {
        int index = Math.Abs(user.UserName.GetHashCode()) % Palette.Length;
        return Palette[index];
    }
}

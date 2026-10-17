using System.Text.RegularExpressions;

namespace GlpiNg.Web.Services;

/// <summary>
/// Filtre de connexion d'un annuaire (<see cref="Models.AuthLdapServer.LoginFilter"/>, le
/// <c>condition</c> de <c>glpi_authldaps</c>) : la condition LDAP qu'un compte doit remplir pour
/// se connecter, combinée à la recherche de l'identifiant comme le fait GLPI —
/// <c>(&amp;(champ=identifiant)filtre)</c>.
///
/// Un filtre mal formé ne restreint rien : il fait échouer la recherche, donc toutes les
/// connexions de l'annuaire, avec pour seule trace un message du serveur LDAP. D'où cette
/// validation à l'enregistrement, et à l'usage pour un filtre enregistré avant elle.
/// </summary>
public static partial class LdapFilter
{
    /// <summary>
    /// Filtre prêt à l'emploi, ou la raison pour laquelle il ne l'est pas. Vide : pas de filtre.
    /// Retire les blancs autour des parenthèses (un filtre collé sur plusieurs lignes) et ajoute les
    /// parenthèses extérieures oubliées (<c>objectClass=user</c>) ; les blancs à l'intérieur d'une
    /// valeur (<c>cn=Support N2</c>) sont conservés.
    /// </summary>
    public static (string? Filter, string? Error) Normalize(string? condition)
    {
        if (string.IsNullOrWhiteSpace(condition))
        {
            return (null, null);
        }

        string filter = BlanksAroundParentheses().Replace(condition.Trim(), "$1");
        if (!filter.StartsWith('('))
        {
            filter = $"({filter})";
        }

        int position = 0;
        try
        {
            do
            {
                ParseFilter(filter, ref position);
            }
            while (position < filter.Length);
        }
        catch (FormatException exception)
        {
            return (null, $"{exception.Message} (position {position + 1} de « {filter} »)");
        }

        return (filter, null);
    }

    /// <summary>
    /// <paramref name="baseFilter"/> restreint par le filtre de connexion. Un filtre invalide lève
    /// plutôt que d'être ignoré : il restreint l'accès, l'ignorer ouvrirait l'annuaire entier.
    /// </summary>
    public static string Combine(string baseFilter, string? condition)
    {
        (string? filter, string? error) = Normalize(condition);

        return error is not null
            ? throw new InvalidOperationException($"Filtre de connexion invalide : {error}")
            : filter is null ? baseFilter : $"(&{baseFilter}{filter})";
    }

    /// <summary>Un filtre entre parenthèses (RFC 4515) : composé (&amp;, |, !) ou élémentaire.</summary>
    private static void ParseFilter(string text, ref int position)
    {
        Expect(text, ref position, '(');

        switch (Peek(text, position))
        {
            case '&' or '|':
                position++;
                if (Peek(text, position) != '(')
                {
                    throw new FormatException($"« {text[position - 1]} » doit être suivi d'au moins un filtre entre parenthèses");
                }

                while (Peek(text, position) == '(')
                {
                    ParseFilter(text, ref position);
                }

                break;

            case '!':
                position++;
                ParseFilter(text, ref position);
                break;

            default:
                ParseItem(text, ref position);
                break;
        }

        Expect(text, ref position, ')');
    }

    /// <summary>Élément <c>attribut opérateur valeur</c> : =, ~=, &gt;=, &lt;=, ou règle étendue (<c>attr:1.2.840...:=</c>).</summary>
    private static void ParseItem(string text, ref int position)
    {
        int start = position;
        while (position < text.Length && (char.IsAsciiLetterOrDigit(text[position]) || text[position] is '-' or ';' or '.' or ':'))
        {
            position++;
        }

        if (position == start)
        {
            throw new FormatException("nom d'attribut attendu");
        }

        if (Peek(text, position) is '~' or '>' or '<')
        {
            position++;
        }

        Expect(text, ref position, '=');

        while (position < text.Length && text[position] != ')')
        {
            if (text[position] == '(')
            {
                throw new FormatException("parenthèse dans une valeur : écrivez-la \\28");
            }

            if (text[position] == '\\')
            {
                if (position + 2 >= text.Length || !char.IsAsciiHexDigit(text[position + 1]) || !char.IsAsciiHexDigit(text[position + 2]))
                {
                    throw new FormatException("« \\ » doit être suivi de deux chiffres hexadécimaux (\\28, \\29, \\2a, \\5c)");
                }

                position += 2;
            }

            position++;
        }
    }

    private static char? Peek(string text, int position) => position < text.Length ? text[position] : null;

    private static void Expect(string text, ref int position, char expected)
    {
        if (Peek(text, position) != expected)
        {
            throw new FormatException(position < text.Length
                ? $"« {expected} » attendu au lieu de « {text[position]} »"
                : $"« {expected} » attendu en fin de filtre");
        }

        position++;
    }

    [GeneratedRegex(@"\s*([()])\s*")]
    private static partial Regex BlanksAroundParentheses();
}

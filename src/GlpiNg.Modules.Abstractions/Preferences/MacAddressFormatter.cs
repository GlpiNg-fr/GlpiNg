namespace GlpiNg.Modules.Abstractions.Preferences;

/// <summary>
/// Réécrit une adresse MAC dans la forme demandée (voir <see cref="MacAddressFormat"/>). Purement
/// cosmétique : la valeur en base n'est pas touchée.
/// </summary>
public static class MacAddressFormatter
{
    /// <summary>Forme appliquée quand rien n'a été choisi, ni par le compte ni par l'instance.</summary>
    public const MacAddressFormat Fallback = MacAddressFormat.HyphenUpper;

    /// <summary>
    /// Rend <paramref name="mac"/> dans la forme demandée.
    ///
    /// Une valeur qui n'est pas une MAC sur 12 chiffres hexadécimaux est rendue telle quelle : ce
    /// qui est affiché doit rester ce qui est stocké, faute de quoi une donnée aberrante devient
    /// invisible au moment même où on la cherche. De même, <c>null</c> et vide ressortent
    /// inchangés — c'est à l'appelant de décider comment il affiche une absence.
    /// </summary>
    public static string? Format(string? mac, MacAddressFormat format)
    {
        if (string.IsNullOrWhiteSpace(mac))
        {
            return mac;
        }

        Span<char> hex = stackalloc char[12];
        int count = 0;

        foreach (char c in mac)
        {
            if (c is ':' or '-' or '.' or ' ')
            {
                continue;
            }

            if (!Uri.IsHexDigit(c) || count == 12)
            {
                return mac;
            }

            hex[count++] = c;
        }

        if (count != 12)
        {
            return mac;
        }

        MacAddressFormat resolved = format == MacAddressFormat.Default ? Fallback : format;

        for (int i = 0; i < hex.Length; i++)
        {
            hex[i] = IsUpper(resolved) ? char.ToUpperInvariant(hex[i]) : char.ToLowerInvariant(hex[i]);
        }

        return resolved switch
        {
            MacAddressFormat.Bare => new string(hex),
            MacAddressFormat.HpUpper => $"{new string(hex[..6])}-{new string(hex[6..])}",
            MacAddressFormat.DotLower => $"{new string(hex[..4])}.{new string(hex[4..8])}.{new string(hex[8..])}",
            _ => Group(hex, Separator(resolved)),
        };
    }

    /// <summary>Exemple affiché dans la liste de choix des préférences.</summary>
    public static string Sample(MacAddressFormat format) => Format("AABBCCDDEEFF", format)!;

    /// <summary>
    /// Traduit les valeurs persistées du réglage global « Affichage adresse MAC », qui sont des
    /// chaînes libres. Une valeur inconnue retombe sur <see cref="Fallback"/>, comme le faisait le
    /// formateur d'origine.
    /// </summary>
    public static MacAddressFormat FromSettingsValue(string? value) => value switch
    {
        "HP" => MacAddressFormat.HpUpper,
        "Cisco" => MacAddressFormat.DotLower,
        _ => Fallback,
    };

    private static bool IsUpper(MacAddressFormat format) => format
        is MacAddressFormat.ColonUpper or MacAddressFormat.HyphenUpper or MacAddressFormat.HpUpper or MacAddressFormat.Bare;

    private static char Separator(MacAddressFormat format) => format
        is MacAddressFormat.HyphenUpper or MacAddressFormat.HyphenLower ? '-' : ':';

    private static string Group(ReadOnlySpan<char> hex, char separator)
    {
        Span<char> result = stackalloc char[17];
        int at = 0;

        for (int pair = 0; pair < 6; pair++)
        {
            if (pair > 0)
            {
                result[at++] = separator;
            }

            result[at++] = hex[pair * 2];
            result[at++] = hex[(pair * 2) + 1];
        }

        return new string(result);
    }
}

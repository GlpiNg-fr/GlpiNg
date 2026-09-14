using System.Text;

namespace GlpiNg.Web.Import;

/// <summary>
/// Répare le texte lu d'une base GLPI dont les colonnes sont déclarées <c>latin1</c> alors
/// qu'elles contiennent des octets UTF-8 — cas très répandu sur les installations anciennes, que
/// GLPI n'a jamais converties.
///
/// Le symptôme est connu : « é » (0xC3 0xA9 en UTF-8) relu octet par octet donne « Ã© », « è »
/// donne « Ã¨ », l'espace insécable donne « Â ». Le connecteur MySQL n'y est pour rien : il
/// respecte le jeu de caractères que la colonne déclare, et c'est la déclaration qui est fausse.
/// Forcer <c>CharSet=utf8mb4</c> sur la chaîne de connexion ne corrige rien non plus — MySQL
/// convertirait alors consciencieusement du latin1 vers de l'UTF-8, produisant le même charabia.
///
/// La réparation se fait donc après coup, en refaisant le chemin à l'envers : ré-encoder la chaîne
/// pour retrouver les octets d'origine, puis les relire en UTF-8.
///
/// <b>Le « latin1 » de MySQL est en fait du Windows-1252</b>, et non de l'ISO 8859-1. La nuance ne
/// porte que sur la plage 0x80-0x9F, mais c'est précisément là que tombent les caractères les plus
/// courants d'un texte français une fois passés en UTF-8 : l'apostrophe typographique « ’ »
/// (U+2019, octets E2 80 99) devient « â€™ », l'euro « â‚¬ », les tirets cadratins « â€“ ». Un
/// décodage en ISO 8859-1 les laisserait passer, puisque 0x80-0x9F y sont des caractères de
/// contrôle : la table ci-dessous est donc indispensable, pas un raffinement.
/// </summary>
public static class GlpiText
{
    /// <summary>
    /// Les 27 positions où Windows-1252 s'écarte d'ISO 8859-1, dans le sens caractère → octet.
    /// Les cinq autres (0x81, 0x8D, 0x8F, 0x90, 0x9D) ne sont pas définies et se comportent comme
    /// en ISO 8859-1 : le point de code vaut l'octet, ce dont le code se charge sans table.
    ///
    /// Écrite à la main plutôt que via <c>Encoding.GetEncoding(1252)</c>, qui exigerait le paquet
    /// System.Text.Encoding.CodePages et son enregistrement au démarrage — beaucoup de cérémonie
    /// pour vingt-sept entrées qui ne bougeront plus jamais.
    /// </summary>
    private static readonly Dictionary<char, byte> Windows1252Specials = new()
    {
        ['€'] = 0x80, ['‚'] = 0x82, ['ƒ'] = 0x83, ['„'] = 0x84,
        ['…'] = 0x85, ['†'] = 0x86, ['‡'] = 0x87, ['ˆ'] = 0x88,
        ['‰'] = 0x89, ['Š'] = 0x8A, ['‹'] = 0x8B, ['Œ'] = 0x8C,
        ['Ž'] = 0x8E, ['‘'] = 0x91, ['’'] = 0x92, ['“'] = 0x93,
        ['”'] = 0x94, ['•'] = 0x95, ['–'] = 0x96, ['—'] = 0x97,
        ['˜'] = 0x98, ['™'] = 0x99, ['š'] = 0x9A, ['›'] = 0x9B,
        ['œ'] = 0x9C, ['ž'] = 0x9E, ['Ÿ'] = 0x9F,
    };

    /// <summary>
    /// UTF-8 strict : une séquence invalide lève au lieu d'être remplacée par « � ». C'est ce qui
    /// permet de distinguer un vrai charabia d'un texte déjà correct — voir <see cref="Repair"/>.
    /// </summary>
    private static readonly Encoding StrictUtf8 = Encoding.GetEncoding(
        "utf-8",
        EncoderFallback.ExceptionFallback,
        DecoderFallback.ExceptionFallback);

    /// <summary>
    /// Renvoie le texte corrigé, ou tel quel s'il n'a pas besoin de l'être.
    ///
    /// Deux garde-fous, parce qu'une réparation appliquée à tort abîmerait du texte correct :
    ///
    /// 1. Chaque caractère doit tenir sur un octet Windows-1252. Un caractère qui n'y figure pas
    ///    (« Ω », un idéogramme, un émoji) prouve que la chaîne a déjà été décodée correctement :
    ///    un charabia issu d'une lecture octet par octet ne peut pas en contenir.
    /// 2. Surtout : les octets retrouvés doivent former de l'UTF-8 <b>valide</b>. C'est ce qui
    ///    protège le texte légitime — « é » seul donne l'octet 0xE9, début d'une séquence UTF-8
    ///    attendant deux octets de continuation qui ne viennent pas : le décodage lève, et la
    ///    chaîne d'origine est conservée. De même « € » déjà correct donne 0x80, un octet de
    ///    continuation orphelin.
    ///
    /// Une seule passe, volontairement. Un texte passé deux fois par la moulinette (« Ã© » devenu
    /// « ÃƒÂ© ») ne serait qu'à moitié réparé, mais boucler jusqu'à l'échec ferait courir le risque
    /// inverse — dégrader un texte correct qui aurait la malchance de survivre à un tour de plus.
    /// </summary>
    public static string? Repair(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        byte[] bytes = new byte[value.Length];
        bool hasHighByte = false;

        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];

            if (character <= 0x00FF)
            {
                bytes[index] = (byte)character;
            }
            else if (Windows1252Specials.TryGetValue(character, out byte mapped))
            {
                bytes[index] = mapped;
            }
            else
            {
                // Hors de Windows-1252 : la chaîne est déjà du vrai Unicode.
                return value;
            }

            hasHighByte |= bytes[index] > 0x7F;
        }

        if (!hasHighByte)
        {
            return value;
        }

        try
        {
            string repaired = StrictUtf8.GetString(bytes);
            return repaired == value ? value : repaired;
        }
        catch (DecoderFallbackException)
        {
            // Les octets ne forment pas de l'UTF-8 : le texte était authentiquement du Windows-1252.
            return value;
        }
    }
}

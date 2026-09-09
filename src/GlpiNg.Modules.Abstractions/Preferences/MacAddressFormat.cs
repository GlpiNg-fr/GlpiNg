namespace GlpiNg.Modules.Abstractions.Preferences;

/// <summary>
/// Écriture d'une adresse MAC à l'affichage. L'inventaire remonte la forme choisie par l'agent —
/// qui varie selon la plateforme — et la comparer à celle notée sur un bon de livraison ou affichée
/// par un switch demande la même écriture des deux côtés. Purement cosmétique : la valeur stockée
/// n'est jamais réécrite.
///
/// Les trois premières formes nommées reprennent celles que proposait déjà le réglage global
/// « Affichage adresse MAC » (Configuration &gt; Général), dont les valeurs persistées sont des
/// chaînes « Windows » / « HP » / « Cisco » : la préférence personnelle les prolonge au lieu de les
/// remplacer.
/// </summary>
public enum MacAddressFormat
{
    /// <summary>
    /// Suit le réglage de l'instance (Configuration &gt; Général). Valeur zéro, donc celle des
    /// comptes existants après migration : sans choix personnel, rien ne change pour eux.
    /// </summary>
    Default = 0,

    /// <summary>AA-BB-CC-DD-EE-FF — la forme « Windows » du réglage global.</summary>
    HyphenUpper = 1,

    /// <summary>AABBCC-DDEEFF — la forme « HP » du réglage global.</summary>
    HpUpper = 2,

    /// <summary>aabb.ccdd.eeff — la forme « Cisco » du réglage global.</summary>
    DotLower = 3,

    /// <summary>AA:BB:CC:DD:EE:FF — la plus répandue hors univers Windows.</summary>
    ColonUpper = 4,

    /// <summary>aa:bb:cc:dd:ee:ff — celle de Linux et de la plupart des équipements réseau.</summary>
    ColonLower = 5,

    /// <summary>aa-bb-cc-dd-ee-ff.</summary>
    HyphenLower = 6,

    /// <summary>AABBCCDDEEFF — sans séparateur, tel qu'attendu par beaucoup d'imports.</summary>
    Bare = 7,
}

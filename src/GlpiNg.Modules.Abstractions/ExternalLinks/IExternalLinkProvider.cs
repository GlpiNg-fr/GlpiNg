namespace GlpiNg.Modules.Abstractions.ExternalLinks;

/// <summary>
/// Balises reconnues dans l'URL d'un lien externe, entre crochets à l'image de GLPI
/// (<c>[NAME]</c>, <c>[IP]</c>, ...).
///
/// Liste fermée, contrairement au <c>[FIELD:colonne]</c> de GLPI qui lit n'importe quelle colonne
/// de la table de l'objet : GlpiNg n'expose pas ses colonnes à la configuration, et une balise
/// qu'on ne saurait pas remplir donnerait une URL cassée sans rien dire. Ce qui est proposé ici
/// est ce qui est réellement fourni.
/// </summary>
public static class ExternalLinkTags
{
    public const string Id = "ID";
    public const string ItemType = "ITEMTYPE";
    public const string Name = "NAME";
    public const string Serial = "SERIAL";
    public const string OtherSerial = "OTHERSERIAL";
    public const string Type = "TYPE";
    public const string Model = "MODEL";
    public const string Manufacturer = "MANUFACTURER";
    public const string State = "STATE";
    public const string Location = "LOCATION";
    public const string User = "USER";
    public const string Tech = "TECH";
    public const string Uuid = "UUID";
    public const string Comment = "COMMENT";

    /// <summary>Adresse IP — seuls les types qui en portent une la fournissent.</summary>
    public const string Ip = "IP";

    /// <summary>Adresse MAC — idem.</summary>
    public const string Mac = "MAC";

    public const string OperatingSystem = "OS";
    public const string Domain = "DOMAIN";

    /// <summary>Libellé lisible de chaque balise, pour l'aide affichée dans l'écran de configuration.</summary>
    public static readonly IReadOnlyList<(string Tag, string Label)> Catalog =
    [
        (Id, "Identifiant de l'objet"),
        (ItemType, "Type d'objet"),
        (Name, "Nom"),
        (Serial, "Numéro de série"),
        (OtherSerial, "Numéro d'inventaire"),
        (Type, "Type"),
        (Model, "Modèle"),
        (Manufacturer, "Fabricant"),
        (State, "Statut"),
        (Location, "Lieu"),
        (User, "Utilisateur assigné"),
        (Tech, "Technicien en charge"),
        (Uuid, "UUID"),
        (Comment, "Commentaire"),
        (Ip, "Adresse IP"),
        (Mac, "Adresse MAC"),
        (OperatingSystem, "Système d'exploitation"),
        (Domain, "Domaine"),
    ];
}

/// <summary>Un lien prêt à être affiché : libellé et URL, balises déjà remplacées.</summary>
/// <param name="Id">Identifiant du lien configuré, pour la clé de rendu.</param>
/// <param name="Name">Libellé affiché.</param>
/// <param name="Url">URL finale.</param>
/// <param name="OpenInNewWindow">Ouvrir dans un nouvel onglet.</param>
public sealed record ResolvedExternalLink(int Id, string Name, string Url, bool OpenInNewWindow);

/// <summary>
/// Donne les liens externes applicables à un objet, balises remplacées. Implémenté par l'hôte
/// (les liens se configurent dans « Configuration &gt; Liens externes » et vivent dans sa base),
/// consommé par les fiches des modules — même montage que <see cref="Preferences.IUserPreferences"/>.
///
/// C'est l'appelant qui fournit les valeurs des balises : lui seul connaît son modèle, et
/// l'hôte n'a pas à savoir ce qu'est un Ordinateur ou une Imprimante pour rendre un lien.
/// </summary>
public interface IExternalLinkProvider
{
    /// <param name="itemType">Type d'objet au sens GLPI (« Computer », « Printer », ...).</param>
    /// <param name="tagValues">
    /// Valeurs des balises de <see cref="ExternalLinkTags"/> pour cet objet. Une balise absente
    /// ou nulle est remplacée par une chaîne vide : mieux vaut une URL incomplète, visiblement
    /// fautive, qu'un <c>[SERIAL]</c> laissé tel quel dans la barre d'adresse.
    /// </param>
    Task<IReadOnlyList<ResolvedExternalLink>> GetForItemAsync(string itemType,
        IReadOnlyDictionary<string, string?> tagValues, CancellationToken cancellationToken = default);
}

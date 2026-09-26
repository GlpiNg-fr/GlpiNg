namespace GlpiNg.Modules.Abstractions.Preferences;

/// <summary>
/// Écriture des dates à l'affichage (« Format des dates » de GLPI). Les valeurs persistées côté
/// hôte sont celles de GLPI : « ymd », « dmy », « mdy ».
/// </summary>
public enum DateDisplayFormat
{
    /// <summary>2026-09-25 — le défaut de GLPI.</summary>
    YearMonthDay,

    /// <summary>25/09/2026 — l'écriture française, celle que GlpiNg affichait partout jusqu'ici.</summary>
    DayMonthYear,

    /// <summary>09/25/2026.</summary>
    MonthDayYear,
}

/// <summary>Ordre d'affichage du nom complet d'une personne (« Ordre d'affichage du nom complet » de GLPI).</summary>
public enum NameDisplayOrder
{
    /// <summary>« Dupont Jean » — le défaut de GLPI.</summary>
    LastFirst,

    /// <summary>« Jean Dupont ».</summary>
    FirstLast,
}

/// <summary>
/// Préférences d'affichage de l'utilisateur connecté, déjà résolues : le choix personnel s'il
/// existe, sinon la valeur de l'instance (Configuration &gt; Valeurs par défaut). Les pages n'ont
/// jamais à connaître cette cascade.
///
/// Porte aussi les règles de formatage qui en découlent, pour qu'il n'y ait qu'un endroit où une
/// date devient du texte : c'est ce qui permet à un réglage de s'appliquer partout.
/// </summary>
public sealed record UserPreferenceValues
{
    /// <summary>Valeurs appliquées hors session applicative, et tant que rien n'est réglé.</summary>
    public static readonly UserPreferenceValues Defaults = new();

    /// <summary>Compte auquel ces préférences s'appliquent ; null hors session (agent, cron...).</summary>
    public int? UserId { get; init; }

    /// <summary>Nombre de lignes affichées par défaut dans les tableaux paginés.</summary>
    public int ItemsPerPage { get; init; } = 25;

    /// <summary>
    /// Écriture des adresses MAC, déjà résolue : jamais <see cref="Preferences.MacAddressFormat.Default"/>.
    /// </summary>
    public MacAddressFormat MacAddressFormat { get; init; } = MacAddressFormatter.Fallback;

    public DateDisplayFormat DateFormat { get; init; } = DateDisplayFormat.DayMonthYear;

    /// <summary>Fuseau dans lequel les instants enregistrés en UTC sont montrés.</summary>
    public TimeZoneInfo TimeZone { get; init; } = TimeZoneInfo.Local;

    public NameDisplayOrder NameOrder { get; init; } = NameDisplayOrder.LastFirst;

    /// <summary>Séparateur des exports CSV.</summary>
    public string CsvDelimiter { get; init; } = ";";

    /// <summary>Faire suivre le nom des acteurs de leur identifiant (« Afficher les ID GLPI »).</summary>
    public bool ShowIds { get; init; }

    /// <summary>Historiques et fils de suivis du plus récent au plus ancien (« Ordre de l'historique »).</summary>
    public bool NewestFirst { get; init; }

    /// <summary>Afficher le nombre d'éléments à côté des onglets des fiches (« Afficher les compteurs »).</summary>
    public bool ShowTabCounters { get; init; } = true;

    /// <summary>Recevoir les notifications des actions qu'on a soi-même faites.</summary>
    public bool NotifyOnMyChanges { get; init; } = true;

    /// <summary>
    /// Palette de couleur (« Palette de couleur » de GLPI), sous sa clé CSS : « auror », « dark »,
    /// « darker », « classic », « midnight », « lightblue », « vintage » ou « icecream » — voir
    /// glping-theme.css, où chacune est définie.
    /// </summary>
    public string Palette { get; init; } = "auror";

    /// <summary>
    /// Palette réellement sombre, qui bascule Tabler en mode sombre. « dark » n'en est pas une :
    /// chez GLPI, c'est un thème clair au menu très sombre.
    /// </summary>
    public bool IsDarkPalette => Palette is "darker" or "midnight";

    /// <summary>Contraste élevé : texte à pleine encre, bordures franches, liens soulignés.</summary>
    public bool HighContrast { get; init; }

    public string? FormatMac(string? mac) => MacAddressFormatter.Format(mac, MacAddressFormat);

    // ---- Dates ------------------------------------------------------------------------------

    /// <summary>
    /// Culture invariante : dans un format personnalisé, « / » et « : » prendraient sinon le
    /// séparateur de la culture du serveur, et l'écriture ne dépendrait plus seulement du réglage.
    /// </summary>
    private static readonly System.Globalization.CultureInfo Invariant = System.Globalization.CultureInfo.InvariantCulture;

    /// <summary>Motif de la partie date, selon <see cref="DateFormat"/>.</summary>
    public string DatePattern => DateFormat switch
    {
        DateDisplayFormat.YearMonthDay => "yyyy-MM-dd",
        DateDisplayFormat.MonthDayYear => "MM/dd/yyyy",
        _ => "dd/MM/yyyy",
    };

    /// <summary>
    /// Instant enregistré en UTC (création, ouverture, dernier contact...) ramené dans le fuseau de
    /// l'utilisateur. EF rend ces dates sans fuseau : on les marque UTC avant de convertir.
    /// </summary>
    public DateTime ToUserTime(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(System.DateTime.SpecifyKind(utc, DateTimeKind.Utc), TimeZone);

    /// <summary>Instant UTC en date et heure, dans le fuseau de l'utilisateur.</summary>
    public string? DateTime(DateTime? utc, bool seconds = false) =>
        utc is { } value ? ToUserTime(value).ToString(DatePattern + (seconds ? " HH:mm:ss" : " HH:mm"), Invariant) : null;

    /// <summary>Instant UTC réduit à sa date, dans le fuseau de l'utilisateur.</summary>
    public string? Date(DateTime? utc) =>
        utc is { } value ? ToUserTime(value).ToString(DatePattern, Invariant) : null;

    /// <summary>
    /// Heure « murale » : saisie dans un champ datetime-local et stockée telle quelle (échéance,
    /// planification d'une tâche). Elle n'a pas de fuseau — la convertir la décalerait —, seule son
    /// écriture suit la préférence.
    /// </summary>
    public string? LocalDateTime(DateTime? wallClock, bool seconds = false) =>
        wallClock?.ToString(DatePattern + (seconds ? " HH:mm:ss" : " HH:mm"), Invariant);

    /// <summary>Date sans heure (début de contrat, date d'achat...) : rien à convertir, seulement à écrire.</summary>
    public string? LocalDate(DateTime? date) => date?.ToString(DatePattern, Invariant);

    /// <inheritdoc cref="LocalDate(DateTime?)"/>
    public string? LocalDate(DateOnly? date) => date?.ToString(DatePattern, Invariant);

    // ---- Personnes et ordre ------------------------------------------------------------------

    /// <summary>
    /// Nom complet selon <see cref="NameOrder"/>, à partir du nom et du prénom quand ils sont
    /// connus ; sinon le repli (nom d'affichage, identifiant de connexion).
    /// </summary>
    public string PersonName(string? firstName, string? lastName, string fallback)
    {
        string? first = string.IsNullOrWhiteSpace(firstName) ? null : firstName.Trim();
        string? last = string.IsNullOrWhiteSpace(lastName) ? null : lastName.Trim();

        if (first is null && last is null)
        {
            return fallback;
        }

        return NameOrder == NameDisplayOrder.FirstLast
            ? string.Join(' ', new[] { first, last }.OfType<string>())
            : string.Join(' ', new[] { last, first }.OfType<string>());
    }

    /// <summary>« Nom (12) » quand <see cref="ShowIds"/> est actif, le nom seul sinon.</summary>
    public string WithId(string name, int id) => ShowIds ? $"{name} ({id})" : name;

    /// <summary>Met un historique ou un fil de suivis dans l'ordre choisi.</summary>
    public IEnumerable<T> Chronological<T>(IEnumerable<T> entries, Func<T, DateTime> date) =>
        NewestFirst ? entries.OrderByDescending(date) : entries.OrderBy(date);
}

/// <summary>
/// Donne accès aux préférences du compte connecté. Implémenté par l'hôte (les préférences sont
/// portées par son modèle d'utilisateur), consommé par les pages des modules.
///
/// La valeur vient de la base, pas des revendications de la session : un réglage déposé dans le
/// cookie à la connexion ne changerait qu'au prochain login, ce qu'un écran de préférences ne doit
/// pas faire. Elle est lue une fois par requête et par circuit.
/// </summary>
public interface IUserPreferences
{
    ValueTask<UserPreferenceValues> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Les préférences déjà chargées, sans attendre — pour le balisage Razor, qui ne peut pas
    /// attendre une requête. L'hôte les charge au début de chaque requête et de chaque circuit ;
    /// avant cela (et hors session), ce sont les valeurs par défaut.
    /// </summary>
    UserPreferenceValues Current { get; }
}

/// <summary>
/// Raccourci des pages vers les règles de formatage de <see cref="UserPreferenceValues"/> : injecté
/// dans chaque composant sous le nom <c>Display</c> (voir les <c>_Imports.razor</c>), il permet
/// d'écrire <c>@Display.DateTime(ticket.OpenedAt)</c> là où l'on écrivait un format en dur.
/// </summary>
/// <remarks>
/// <paramref name="preferences"/> est facultatif : l'hôte injecte ce raccourci dans tous ses
/// composants, y compris ceux de l'assistant d'installation, rendus avant que la base — et donc les
/// préférences — existe. Ce sont alors les valeurs par défaut.
/// </remarks>
public sealed class UserDisplay(IUserPreferences? preferences)
{
    public UserPreferenceValues Values => preferences?.Current ?? UserPreferenceValues.Defaults;

    /// <inheritdoc cref="UserPreferenceValues.DateTime(DateTime?, bool)"/>
    public string? DateTime(DateTime? utc, bool seconds = false) => Values.DateTime(utc, seconds);

    /// <inheritdoc cref="UserPreferenceValues.Date(DateTime?)"/>
    public string? Date(DateTime? utc) => Values.Date(utc);

    /// <inheritdoc cref="UserPreferenceValues.LocalDateTime(DateTime?, bool)"/>
    public string? LocalDateTime(DateTime? wallClock, bool seconds = false) => Values.LocalDateTime(wallClock, seconds);

    /// <inheritdoc cref="UserPreferenceValues.LocalDate(DateTime?)"/>
    public string? LocalDate(DateTime? date) => Values.LocalDate(date);

    /// <inheritdoc cref="UserPreferenceValues.LocalDate(DateOnly?)"/>
    public string? LocalDate(DateOnly? date) => Values.LocalDate(date);

    /// <inheritdoc cref="UserPreferenceValues.ToUserTime(DateTime)"/>
    public DateTime ToUserTime(DateTime utc) => Values.ToUserTime(utc);

    /// <inheritdoc cref="UserPreferenceValues.Chronological{T}(IEnumerable{T}, Func{T, DateTime})"/>
    public IEnumerable<T> Chronological<T>(IEnumerable<T> entries, Func<T, DateTime> date) =>
        Values.Chronological(entries, date);

    public bool ShowTabCounters => Values.ShowTabCounters;
}

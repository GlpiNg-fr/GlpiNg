namespace GlpiNg.Web.Models;

/// <summary>Journalisation des appels d'un client d'API (glpi_apiclients.dolog_method).</summary>
public enum ApiClientLogMethod
{
    None = 0,

    /// <summary>Dans l'historique du client.</summary>
    Historical = 1,

    /// <summary>Dans le journal des événements.</summary>
    Logs = 2,
}

/// <summary>
/// Client de l'API REST (glpi_apiclients de GLPI, onglet « API » de la configuration générale) :
/// l'API n'accepte un appel que si un client actif correspond à l'adresse IP de l'appelant, et, si
/// ce client a un jeton d'application, que l'appel le présente (en-tête <c>App-Token</c>).
///
/// Comme dans GLPI, une installation neuve en compte un : « full access from localhost ».
/// </summary>
public class ApiClient
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Début de la plage IPv4 autorisée, sous forme numérique comme GLPI (ip2long) ; null = toute adresse IPv4.</summary>
    public long? Ipv4RangeStart { get; set; }

    public long? Ipv4RangeEnd { get; set; }

    /// <summary>Adresse IPv6 autorisée ; null = toute adresse IPv6.</summary>
    public string? Ipv6 { get; set; }

    /// <summary>Empreinte SHA-256 du jeton d'application ; null = pas de jeton exigé.</summary>
    public string? AppTokenHash { get; set; }

    public DateTime? AppTokenDate { get; set; }

    public ApiClientLogMethod LogMethod { get; set; }

    public string? Comment { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

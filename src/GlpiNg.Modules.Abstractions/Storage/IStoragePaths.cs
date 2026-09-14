namespace GlpiNg.Modules.Abstractions.Storage;

/// <summary>
/// Emplacements sur disque utilisés par GlpiNg, tous dérivés d'une racine unique configurable
/// (Configuration &gt; Général &gt; Système).
///
/// Une racine unique plutôt qu'un chemin par usage : sauvegarder l'installation, la déplacer ou la
/// monter sur un volume dédié ne doit demander qu'un seul geste. Porté par
/// <c>GlpiNg.Modules.Abstractions</c> pour que les modules — le stockage des paquets de
/// déploiement, notamment — y accèdent sans dépendre du projet hôte.
/// </summary>
public interface IStoragePaths
{
    /// <summary>
    /// Racine du stockage. Le chemin configuré s'il est renseigné, sinon le dossier <c>data</c> à
    /// la racine du programme.
    /// </summary>
    string Root { get; }

    /// <summary>Clés de chiffrement DataProtection (secrets des annuaires LDAP, jetons).</summary>
    string Keys { get; }

    /// <summary>Fragments des fichiers de paquets de déploiement.</summary>
    string Packages { get; }

    /// <summary>Fichiers des documents rattachés aux objets (voir <see cref="Documents.IDocumentAttachments"/>).</summary>
    string Documents { get; }

    /// <summary>
    /// Fichier de configuration propre à l'installation (<c>appsettings.local.json</c> : chaîne de
    /// connexion, état de l'assistant, clé de signature OAuth).
    ///
    /// Il est rangé ici avec le reste plutôt qu'à côté du binaire pour que déplacer ou sauvegarder
    /// une installation ne demande qu'un geste. Corollaire : déplacer la racine impose de déplacer
    /// ce fichier avec elle, sans quoi GlpiNg ne retrouve plus son installation.
    /// </summary>
    string LocalSettings { get; }

    /// <summary>
    /// Crée le dossier s'il n'existe pas et renvoie son chemin. Les emplacements sont créés à la
    /// demande plutôt qu'au démarrage : une racine sur un partage réseau peut n'être disponible
    /// qu'après le lancement du service.
    /// </summary>
    string Ensure(string path);
}

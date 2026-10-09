using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace GlpiNg.Web.Services.Plugins;

/// <summary>
/// Fichiers statiques des plugins, montés sous <c>/_content</c> : <c>/_content/{Nom}/css/x.css</c>
/// est lu dans le <c>wwwroot</c> du plugin actif de ce nom. Le chemin est celui qu'ASP.NET Core
/// donne aux bibliothèques de classes Razor — un plugin qui en deviendrait une garde ses liens.
///
/// Un seul fournisseur qui consulte le registre à chaque requête, plutôt qu'un middleware par
/// plugin figé au démarrage : un plugin chargé ou déchargé à chaud y apparaît ou en disparaît.
/// Seul <c>wwwroot</c> est exposé ; la DLL et ses dépendances, au niveau au-dessus, restent hors
/// de portée d'une requête. Ce qui ne relève d'aucun plugin répond « introuvable », et la requête
/// continue vers les ressources des bibliothèques Razor servies plus loin.
/// </summary>
internal sealed class PluginStaticFileProvider(PluginRegistry registry) : IFileProvider
{
    public IFileInfo GetFileInfo(string subpath)
    {
        string path = subpath.TrimStart('/');
        int slash = path.IndexOf('/');

        return slash > 0 && registry.Find(path[..slash])?.WebRoot is { } webRoot
            ? webRoot.GetFileInfo(path[slash..])
            : new NotFoundFileInfo(subpath);
    }

    public IDirectoryContents GetDirectoryContents(string subpath) => NotFoundDirectoryContents.Singleton;

    public IChangeToken Watch(string filter) => NullChangeToken.Singleton;
}

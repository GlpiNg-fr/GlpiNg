using System.Security.Cryptography;
using GlpiNg.Modules.Abstractions.Storage;

namespace GlpiNg.Web.Services.Documents;

/// <summary>
/// Range et relit les fichiers des documents sous <see cref="IStoragePaths.Documents"/>.
///
/// Le rangement se fait par empreinte SHA-256, sur deux niveaux de sous-dossiers
/// (<c>a/ab/abcdef...</c>) — même découpage que les fragments de paquets de déploiement. Deux
/// raisons : un dossier unique contenant des dizaines de milliers d'entrées devient pénible pour
/// le système de fichiers comme pour l'administrateur qui l'ouvre, et surtout deux documents de
/// contenu identique tombent naturellement sur le même fichier, donc ne sont stockés qu'une fois.
///
/// Le nom d'origine n'apparaît jamais sur le disque : il est rendu au téléchargement depuis la
/// base. C'est ce qui évite d'avoir à assainir un nom de fichier hostile (« ../../appsettings »)
/// au moment de l'écriture.
/// </summary>
public sealed class DocumentStorageService(IStoragePaths storagePaths)
{
    /// <summary>
    /// Écrit le contenu et renvoie son empreinte, sa taille et son chemin relatif.
    ///
    /// Le flux est copié dans un fichier temporaire tout en étant haché, puis déplacé à sa place
    /// définitive une fois l'empreinte connue : on ne peut pas nommer le fichier avant de l'avoir
    /// lu en entier, et hacher d'abord en mémoire interdirait les gros fichiers.
    /// </summary>
    public async Task<(string Sha256, long SizeBytes, string StoragePath)> SaveAsync(
        Stream content, CancellationToken cancellationToken = default)
    {
        string root = storagePaths.Ensure(storagePaths.Documents);
        string tempPath = Path.Combine(root, $".incoming-{Guid.NewGuid():N}");

        string sha256;
        long size;

        try
        {
            await using (FileStream temp = File.Create(tempPath))
            using (IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                byte[] buffer = new byte[81920];
                int read;
                size = 0;

                while ((read = await content.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    hash.AppendData(buffer, 0, read);
                    await temp.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    size += read;
                }

                sha256 = Convert.ToHexStringLower(hash.GetHashAndReset());
            }

            string storagePath = ShardedPath(sha256);
            string fullPath = Path.Combine(root, storagePath);

            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

            // Un fichier déjà présent porte forcément le même contenu — son nom est son empreinte.
            // Le réécrire ne servirait à rien et couperait la lecture d'un autre téléchargement en
            // cours ; le temporaire est simplement jeté.
            if (File.Exists(fullPath))
            {
                File.Delete(tempPath);
            }
            else
            {
                File.Move(tempPath, fullPath);
            }

            return (sha256, size, storagePath);
        }
        catch
        {
            // Un échec en cours de copie ne doit pas laisser de résidu dans la racine.
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            throw;
        }
    }

    /// <summary>Ouvre le fichier en lecture, ou <c>null</c> s'il a disparu du disque.</summary>
    public Stream? OpenRead(string storagePath)
    {
        string fullPath = Path.Combine(storagePaths.Documents, storagePath);

        return File.Exists(fullPath)
            ? new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true)
            : null;
    }

    public bool Exists(string storagePath) => File.Exists(Path.Combine(storagePaths.Documents, storagePath));

    /// <summary>
    /// Supprime le fichier d'un document. À n'appeler qu'après avoir vérifié qu'aucune autre fiche
    /// ne porte la même empreinte : le rangement par contenu fait que deux documents distincts
    /// peuvent partager le même fichier — voir DocumentService.DeleteAsync.
    /// </summary>
    public void Delete(string storagePath)
    {
        string fullPath = Path.Combine(storagePaths.Documents, storagePath);

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
    }

    private static string ShardedPath(string sha256) => Path.Combine(sha256[..1], sha256[..2], sha256);
}

using GlpiNg.Web.Models.Documents;
using GlpiNg.Web.Services.Documents;
using Microsoft.AspNetCore.Mvc;

namespace GlpiNg.Web.Controllers;

/// <summary>
/// Sert le contenu des documents. Un contrôleur et non une page Blazor : rendre un fichier
/// demande d'écrire dans le corps de la réponse avec ses propres en-têtes, ce qu'un composant
/// rendu dans un circuit SignalR ne fait pas.
///
/// L'authentification applicative s'applique (le contrôleur n'est pas exclu de la politique
/// globale, contrairement à AgentController) : un document ne se télécharge pas sans session.
/// </summary>
[Route("documents")]
public sealed class DocumentsController(DocumentService documents) : Controller
{
    [HttpGet("{id:int}/download")]
    public async Task<IActionResult> Download(int id, CancellationToken cancellationToken)
    {
        Document? document = await documents.GetAsync(id, cancellationToken);

        if (document is null)
        {
            return NotFound();
        }

        Stream? content = documents.OpenContent(document);

        if (content is null)
        {
            // Fiche sans fichier (import sans accès au dossier files/ de GLPI), ou fichier disparu
            // du disque. 404 plutôt qu'une réponse vide : le navigateur afficherait sinon un
            // fichier de zéro octet comme s'il était valide.
            return NotFound();
        }

        // Toujours en pièce jointe, et jamais avec le type MIME déclaré à l'import : un fichier
        // HTML ou SVG rendu en ligne s'exécuterait dans l'origine de l'application, avec le cookie
        // de session de celui qui l'ouvre. Le type d'origine reste consultable sur la fiche.
        return File(content, "application/octet-stream", document.FileName);
    }
}

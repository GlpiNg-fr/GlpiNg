using GlpiNg.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Web.Middleware;

/// <summary>
/// Bloque l'accès aux pages tant que des migrations EF Core sont en attente, en redirigeant
/// vers /update (qui permet à un administrateur de confirmer leur application). Le protocole
/// glpi-agent (utilisé par les postes clients) et les ressources nécessaires au fonctionnement
/// de /update lui-même (circuit Blazor, fichiers statiques) restent accessibles.
///
/// Doit être branché après <c>UseSetupMiddleware</c> : tant que l'installation n'est pas
/// terminée, ce dernier redirige déjà tout vers /setup, donc GlpiNgDbContext est garanti
/// résolvable ici.
/// </summary>
public sealed class MigrationsGateMiddleware(RequestDelegate next)
{
    private static readonly string[] ExemptPathPrefixes =
    [
        "/update",
        "/glpi-agent",
        "/_blazor",
        "/_framework",
        "/_content",
    ];

    public async Task InvokeAsync(HttpContext context, GlpiNgDbContext db)
    {
        PathString requestPath = context.Request.Path;

        bool isExempt = Path.HasExtension(requestPath.Value)
            || ExemptPathPrefixes.Any(prefix => requestPath.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase));

        if (!isExempt && (await db.Database.GetPendingMigrationsAsync()).Any())
        {
            context.Response.Redirect("/update");
            return;
        }

        await next(context);
    }
}

public static class MigrationsGateMiddlewareExtensions
{
    public static IApplicationBuilder UseMigrationsGate(this IApplicationBuilder app)
        => app.UseMiddleware<MigrationsGateMiddleware>();
}

using GlpiNg.Web.Services;

namespace GlpiNg.Web.Middleware;

/// <summary>
/// Refuse l'accès direct à une route dont l'utilisateur n'a pas au moins le droit de lecture sur
/// la section (voir <see cref="ProfileSectionMap.ForPath"/>).
///
/// Couvre les entrées « par l'URL » : lien collé, favori, rechargement. La navigation interne au
/// circuit Blazor ne repasse pas par le pipeline HTTP, elle est donc gardée en parallèle dans
/// <c>MainLayout</c> — les deux s'appuient sur la même table de correspondance.
///
/// Placé après <c>UseAuthentication</c> : il lui faut l'utilisateur. Les routes techniques
/// (protocole agent, circuit Blazor, ressources statiques) n'ont aucune section et passent donc
/// sans contrôle.
/// </summary>
public sealed class SectionAccessMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IProfileRightsProvider rights)
    {
        ProfileSection? section = ProfileSectionMap.ForPath(context.Request.Path.Value);

        if (section is not null && !rights.Current.CanRead(section.Value))
        {
            // Redirection vers l'accueil plutôt qu'un 403 nu : la cible est une page de l'UI, et
            // l'accueil n'est soumis à aucun droit. Le paramètre permet d'y afficher la raison.
            context.Response.Redirect("/?denied=" + section.Value.ToString().ToLowerInvariant());
            return;
        }

        await next(context);
    }
}

public static class SectionAccessMiddlewareExtensions
{
    public static IApplicationBuilder UseSectionAccess(this IApplicationBuilder app)
        => app.UseMiddleware<SectionAccessMiddleware>();
}

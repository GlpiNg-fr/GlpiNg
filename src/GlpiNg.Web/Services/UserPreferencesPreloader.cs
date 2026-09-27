using System.Globalization;
using GlpiNg.Modules.Abstractions.Localization;
using GlpiNg.Modules.Abstractions.Preferences;
using Microsoft.AspNetCore.Components.Server.Circuits;

namespace GlpiNg.Web.Services;

/// <summary>
/// Charge les préférences du compte connecté au début de chaque circuit Blazor, avant le rendu de
/// ses composants : c'est ce qui permet au balisage de lire <see cref="IUserPreferences.Current"/>
/// sans attendre — une date écrite en plein <c>@...</c> ne peut pas attendre une requête.
///
/// Le prérendu, lui, se fait dans la requête HTTP et non dans le circuit : il est couvert par
/// <see cref="UseUserPreferencesPreload"/>.
/// </summary>
public sealed class UserPreferencesPreloader(IUserPreferences preferences) : CircuitHandler
{
    public override async Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken) =>
        await preferences.GetAsync(cancellationToken);

    /// <summary>
    /// Précharge les préférences et pose la langue de l'interface, pour les requêtes de page (le
    /// prérendu y lit les préférences sans attendre) et pour la connexion du circuit Blazor
    /// (<c>/_blazor</c>) : le circuit hérite de la culture posée ici, qui suit ensuite chaque rendu.
    /// Connecté ou non — la page de connexion prend la palette et la langue de l'instance.
    ///
    /// Seule la culture d'<b>interface</b> change (<see cref="CultureInfo.CurrentUICulture"/>,
    /// lue par <see cref="Tr"/>) : dates et nombres suivent leurs propres préférences, et la
    /// culture de mise en forme reste celle du serveur. Le protocole agent, les fichiers statiques et
    /// l'API n'en ont pas l'usage et n'en paient pas le coût. Tolère l'absence du service avant la fin
    /// de l'installation, où il n'est pas enregistré.
    /// </summary>
    public static IApplicationBuilder UseUserPreferencesPreload(IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            PathString path = context.Request.Path;
            bool isPage = HttpMethods.IsGet(context.Request.Method)
                && context.Request.Headers.Accept.ToString().Contains("text/html", StringComparison.OrdinalIgnoreCase);
            bool isCircuit = path.StartsWithSegments("/_blazor");

            if ((isPage || isCircuit)
                && context.RequestServices.GetService<IUserPreferences>() is { } preferences)
            {
                UserPreferenceValues values = await preferences.GetAsync(context.RequestAborted);
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(values.CultureName);
            }

            await next();
        });
}

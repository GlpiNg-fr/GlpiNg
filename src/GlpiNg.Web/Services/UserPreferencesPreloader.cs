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
    /// Précharge pour les requêtes de page, connecté ou non — la page de connexion prend ainsi la
    /// palette de l'instance (voir App.razor). Le prérendu des composants se fait dans la portée
    /// de la requête, avec le même <see cref="IUserPreferences"/>. Les autres
    /// requêtes (protocole agent, fichiers statiques, API) n'en ont pas l'usage et n'en paient pas le
    /// coût. Tolère l'absence du service avant la fin de l'installation, où il n'est pas enregistré.
    /// </summary>
    public static IApplicationBuilder UseUserPreferencesPreload(IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            if (HttpMethods.IsGet(context.Request.Method)
                && context.Request.Headers.Accept.ToString().Contains("text/html", StringComparison.OrdinalIgnoreCase)
                && context.RequestServices.GetService<IUserPreferences>() is { } preferences)
            {
                await preferences.GetAsync(context.RequestAborted);
            }

            await next();
        });
}

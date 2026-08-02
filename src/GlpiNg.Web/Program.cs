using System.Reflection;
using AnthoDingo.Setup;
using GlpiNg.Modules.Inventory;
using GlpiNg.Web.Components;
using GlpiNg.Web.Data;
using GlpiNg.Web.Middleware;
using GlpiNg.Web.Options;
using GlpiNg.Web.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;

namespace GlpiNg.Web;

public class Program
{
    public static void Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

        // Fichier écrit par l'assistant d'installation (AnthoDingo.Setup) à la fin du wizard —
        // prioritaire sur appsettings.json une fois l'installation terminée. Ne doit jamais être
        // commité (voir .gitignore).
        builder.Configuration.AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true);

        // UI Blazor Server (rendu interactif)
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();

        // Authentification applicative par cookie : /login (GlpiUser + PasswordHasher, déjà
        // utilisé par l'admin créé au setup — voir GlpiNgSetupInitializer) et /Account/Logout
        // (voir AccountController). AddCascadingAuthenticationState rend l'utilisateur courant
        // disponible aux composants Blazor (ex. MainLayout) via [CascadingParameter] Task<AuthenticationState>.
        builder.Services.AddCascadingAuthenticationState();
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.LoginPath = "/login";
                options.AccessDeniedPath = "/login";
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
                options.SlidingExpiration = true;
            });
        builder.Services.AddAuthorization();

        // API pour l'agent GLPI (contact / inventory / deploy). AddControllersWithViews (plutôt
        // que AddControllers) est nécessaire pour enregistrer les services ViewFeatures dont
        // dépend [ValidateAntiForgeryToken] (voir AccountController.Login) : sans ça, le filtre
        // ne se résout pas et /Account/Login lève une InvalidOperationException au runtime.
        builder.Services.AddControllersWithViews();
        builder.Services.AddScoped<InventoryImportService>();

        // Documentation OpenAPI/Swagger des contrôleurs API (protocole agent + import GLPI).
        // N'inclut pas les pages Blazor, qui ne sont pas des endpoints API.
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "GlpiNg API",
                Version = "v1",
                Description = "Endpoints REST de GlpiNg : protocole GLPI-Agent (/glpi-agent) et import depuis une base GLPI MySQL (/admin/import/glpi)."
            });

            foreach (Assembly assembly in new[] { Assembly.GetExecutingAssembly(), typeof(InventoryModuleServiceCollectionExtensions).Assembly })
            {
                string xmlPath = Path.Combine(AppContext.BaseDirectory, $"{assembly.GetName().Name}.xml");
                if (File.Exists(xmlPath))
                {
                    options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
                }
            }
        });

        // Construction du JSON de job de déploiement au format attendu par GLPI-Agent
        builder.Services.AddSingleton<DeployJobJsonBuilder>();

        // Activation de Swagger pilotée par la config (section "Swagger:Enabled",
        // modifiable depuis la page /config) : IOptionsMonitor permet une bascule à chaud,
        // sans redémarrage — voir l'usage dans le pipeline ci-dessous.
        builder.Services.Configure<SwaggerOptions>(builder.Configuration.GetSection(SwaggerOptions.SectionName));

        // Lecture/écriture de appsettings.json depuis la page /config (adresses d'écoute
        // du serveur, activation de Swagger).
        builder.Services.AddSingleton<AppSettingsFileStore>();
        builder.Services.AddSingleton<ConfigHistoryService>();

        // Module Inventory (modèle de parc + import GLPI MySQL) : voir
        // GlpiNg.Modules.Inventory.InventoryModuleServiceCollectionExtensions. Les futurs
        // modules (Tickets, etc.) suivront le même schéma AddXxxModule(...).
        builder.Services.AddInventoryModule(builder.Configuration);

        // Assistant d'installation premier démarrage (page /setup intégrée). GlpiNg n'autorise
        // que les bases relationnelles serveur — SQLite n'est volontairement pas proposé.
        builder.Services.AddFileBasedSetup<GlpiNgSetupInitializer>(setupOptions =>
            setupOptions.AllowedProviders = [DbProvider.SqlServer, DbProvider.MySql, DbProvider.Postgres]);

        // Le DbContext applicatif n'est enregistré qu'une fois l'installation terminée : tant que
        // ce n'est pas le cas, le middleware de setup redirige toute autre requête vers /setup, donc
        // aucune page ne tente de résoudre GlpiNgDbContext avant que le provider ne soit connu.
        string? configuredProviderRaw = builder.Configuration["Setup:Provider"];
        string? configuredConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");
        // Serveur de secours optionnel ("ConnectionStrings:FallbackConnection") : si le serveur
        // principal n'est pas joignable au démarrage, GlpiNgDbContext.ConfigureProvider bascule
        // dessus. Non renseigné par défaut par l'assistant d'installation ; à ajouter à la main
        // dans appsettings.local.json (ou une variable d'environnement) pour l'activer.
        string? fallbackConnectionString = builder.Configuration.GetConnectionString("FallbackConnection");

        if (builder.Configuration["Setup:IsComplete"] == "true"
            && configuredProviderRaw is not null
            && configuredConnectionString is not null
            && Enum.TryParse(configuredProviderRaw, ignoreCase: true, out DbProvider configuredProvider))
        {
            // IDbContextFactory (singleton) plutôt que AddDbContext seul : les composants Blazor
            // Server doivent créer une instance courte durée par opération plutôt que de partager
            // le DbContext scoped de la requête, car le pré-rendu exécute le layout et la page
            // en parallèle (leurs OnInitializedAsync se chevauchent), ce qui fait lever le
            // ConcurrencyDetector d'EF Core ("A second operation was started on this context
            // instance...") si elles se partagent une seule instance. Voir
            // https://learn.microsoft.com/aspnet/core/blazor/blazor-server-ef-core#new-dbcontext-instances
            builder.Services.AddDbContextFactory<GlpiNgDbContext>(options =>
                GlpiNgDbContext.ConfigureProvider(options, configuredProvider, configuredConnectionString, fallbackConnectionString));

            // GlpiNgDbContext scoped pour les consommateurs non-Blazor (contrôleurs, services) qui
            // ont un vrai cycle de vie par requête et n'ont pas le problème de concurrence
            // ci-dessus : résolu via la factory plutôt que via AddDbContext, car AddDbContext
            // enregistrerait un second DbContextOptions<GlpiNgDbContext> scoped, ce que le
            // IDbContextFactory singleton ci-dessus ne peut pas consommer (conflit de durée de
            // vie au démarrage : "Cannot consume scoped service ... from singleton").
            builder.Services.AddScoped<GlpiNgDbContext>(sp =>
                sp.GetRequiredService<IDbContextFactory<GlpiNgDbContext>>().CreateDbContext());

            // Les modules (ex. GlpiMySqlImportService dans Inventory) dépendent du DbContext de
            // base plutôt que de GlpiNgDbContext, pour ne pas référencer le projet hôte : c'est
            // à l'hôte de faire le lien vers son DbContext concret.
            builder.Services.AddScoped<DbContext>(sp => sp.GetRequiredService<GlpiNgDbContext>());

            // Services dépendant de GlpiNgDbContext : n'ont de sens qu'une fois l'installation
            // terminée, donc enregistrés ici plutôt que plus haut (sinon la validation des
            // services au build échoue en environnement Development, faute de DbContext).
            builder.Services.AddScoped<InventoryImportService>();
        }

        WebApplication app = builder.Build();

        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
            app.UseHsts();
        }

        // Activation de Swagger pilotée par appsettings.json ("Swagger:Enabled", éditable
        // depuis /config) plutôt que par l'environnement : IOptionsMonitor est réévalué à
        // chaque requête, donc le changement s'applique sans redémarrage. Par défaut
        // désactivé, car /admin/import/glpi n'a toujours pas d'authentification (voir README).
        IOptionsMonitor<SwaggerOptions> swaggerOptionsMonitor = app.Services.GetRequiredService<IOptionsMonitor<SwaggerOptions>>();
        app.MapWhen(
            context => context.Request.Path.StartsWithSegments("/swagger") && swaggerOptionsMonitor.CurrentValue.Enabled,
            branch =>
            {
                branch.UseSwagger();
                branch.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "GlpiNg API v1"));
            });

        app.UseHttpsRedirection();

        // Garde d'installation + page /setup intégrée. Doit être branché en tout premier dans le
        // pipeline (hors Swagger, monté conditionnellement au-dessus) : tant que l'installation
        // n'est pas terminée, toute autre requête y est redirigée.
        app.UseSetupMiddleware("GlpiNg");

        // Gate applicatif de migrations EF Core : tant que des migrations sont en attente,
        // redirige toute requête navigateur (hors protocole glpi-agent) vers /update, qui
        // permet à un administrateur de confirmer leur application. Placé après
        // UseSetupMiddleware pour la même raison que GlpiNgDbContext est garanti enregistré ici.
        app.UseMigrationsGate();

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapStaticAssets();
        app.UseAntiforgery();

        // Pas de RequireAuthorization() ici : les agents GLPI (glpi-agent, voir AgentController)
        // et /Account/Login|Logout ne portent pas de cookie de session applicative.
        app.MapControllers();

        // Toutes les pages Blazor exigent une session authentifiée, sauf celles marquées
        // @attribute [AllowAnonymous] (Login.razor).
        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode()
            .RequireAuthorization();

        app.Run();
    }
}

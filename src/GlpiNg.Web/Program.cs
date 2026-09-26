using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using AnthoDingo.Setup;
using GlpiNg.Modules.Abstractions.Cron;
using GlpiNg.Modules.Abstractions.Deployment;
using GlpiNg.Modules.Abstractions.Directory;
using GlpiNg.Modules.Abstractions.Documents;
using GlpiNg.Modules.Abstractions.Notes;
using GlpiNg.Modules.Abstractions.Notifications;
using GlpiNg.Modules.Abstractions.Entities;
using GlpiNg.Modules.Abstractions.FieldUnicity;
using GlpiNg.Modules.Abstractions.Import;
using GlpiNg.Modules.Abstractions.Preferences;
using GlpiNg.Modules.Abstractions.Storage;
using GlpiNg.Modules.Cron;
using GlpiNg.Modules.Deployment;
using GlpiNg.Modules.Inventory;
using GlpiNg.Modules.KnowledgeBase;
using GlpiNg.Modules.Management;
using GlpiNg.Modules.Assistance;
using GlpiNg.Modules.Scheduler;
using GlpiNg.Web.Components;
using GlpiNg.Web.Data;
using GlpiNg.Web.Import;
using GlpiNg.Web.Middleware;
using GlpiNg.Web.Options;
using GlpiNg.Web.Services;
using GlpiNg.Web.Services.Documents;
using GlpiNg.Web.Services.Notes;
using GlpiNg.Web.Services.FieldUnicity;
using GlpiNg.Web.Services.Notifications;
using GlpiNg.Modules.Abstractions.ExternalLinks;
using GlpiNg.Web.Services.ExternalLinks;
using GlpiNg.Web.Services.Webhooks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

namespace GlpiNg.Web;

public class Program
{
    public static void Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

        // Limite Kestrel par défaut (~28,6 Mo) sur la taille du corps d'une requête HTTP,
        // rencontrée en pratique sur l'upload de gros paquets de déploiement (voir
        // DeploymentPackageFilesController) malgré le relèvement par requête via
        // IHttpMaxRequestBodySizeFeature dans le contrôleur : ce dernier ne fait effet que si
        // rien n'a encore touché le corps de la requête (IsReadOnly), ce qui n'est pas garanti
        // selon le contexte. Celle-ci s'applique dès l'ouverture de la connexion, donc sans ce
        // risque — 8 Go de marge au-delà des 4 Go annoncés par l'utilisateur.
        builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 8L * 1024 * 1024 * 1024);

        // Limite par défaut (128 Mo) du corps de chaque partie d'une requête multipart/form-data,
        // utilisée par MVC dès qu'un formulaire est lu via ReadFormAsync()/IFormFile. Relevée
        // globalement par prudence pour tout endpoint qui s'appuierait sur le binding de formulaire
        // classique — l'upload de paquets de déploiement, lui, contourne complètement ce mécanisme
        // via [DisableFormValueModelBinding] (voir DeploymentPackageFilesController), donc cette
        // limite ne le concerne plus directement.
        builder.Services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = 8L * 1024 * 1024 * 1024);

        // Emplacements de stockage : résolus avant tout le reste, car la configuration de
        // DataProtection en dépend et intervient au démarrage, et surtout parce que le fichier de
        // configuration propre à l'installation vit maintenant dans cette racine — il faut donc
        // la connaître pour aller le lire. Instancié à la main plutôt que résolu depuis le
        // conteneur, qui n'est pas encore construit à ce stade.
        //
        // Conséquence à garder en tête : à cet instant, seuls appsettings.json, les variables
        // d'environnement et la ligne de commande sont chargés. « Storage:RootPath » doit donc
        // venir de l'un d'eux, jamais du fichier local — voir la doc de StoragePaths.
        StoragePaths storagePaths = new(builder.Configuration, builder.Environment);
        builder.Services.AddSingleton<IStoragePaths>(storagePaths);

        EnsureLocalSettingsNotLeftBehind(builder.Environment, storagePaths);

        // La racine est créée ici, et non à la demande comme les autres emplacements : le
        // fournisseur de fichiers de configuration surveille le dossier (reloadOnChange) et
        // échoue s'il n'existe pas encore.
        storagePaths.Ensure(storagePaths.Root);

        // Fichier écrit par l'assistant d'installation (AnthoDingo.Setup) à la fin du wizard —
        // prioritaire sur appsettings.json une fois l'installation terminée. Ne doit jamais être
        // commité (voir .gitignore).
        builder.Configuration.AddJsonFile(storagePaths.LocalSettings, optional: true, reloadOnChange: true);

        // Formatage selon les préférences (« Display » dans les _Imports.razor), injecté dans tous
        // les composants — App.razor et les pages de l'assistant d'installation compris. Enregistré
        // ici, avant la garde d'installation, et sans exiger IUserPreferences, qui n'existe qu'une
        // fois la base configurée : sans lui, ce sont les valeurs par défaut.
        builder.Services.AddScoped(services => new UserDisplay(services.GetService<IUserPreferences>()));

        // UI Blazor Server (rendu interactif)
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents()
            // Défaut SignalR de 32 Ko facilement atteint par InputFile sur un gros fichier
            // (paquets de déploiement, voir DeploymentPackageFileStorageService), au-delà
            // duquel le circuit se déconnecte brutalement (NullReferenceException dans
            // ClientProxyExtensions.SendAsync). Marge portée à 64 Ko comme recommandé par
            // https://learn.microsoft.com/aspnet/core/blazor/fundamentals/signalr#maximum-receive-message-size ;
            // le vrai correctif est côté lecture (voir la taille de buffer réduite là-bas),
            // ceci n'est qu'un filet de sécurité supplémentaire.
            .AddHubOptions(options => options.MaximumReceiveMessageSize = 64 * 1024);

        // Toasts BlazorBootstrap (confirmations/erreurs de formulaire, voir <Toasts> dans
        // MainLayout.razor et BlankLayout.razor) : remplace les anciens messages inline "status".
        builder.Services.AddBlazorBootstrap();

        // Utilisé par InventoryImportService pour construire les liens ##computer.url##/##agent.url##
        // des notifications (voir Services/Notifications) à partir de la requête HTTP courante —
        // ce service n'est pas un contrôleur et n'a donc pas accès à HttpContext autrement.
        builder.Services.AddHttpContextAccessor();

        // Authentification applicative par cookie : /login (GlpiUser + PasswordHasher, déjà
        // utilisé par l'admin créé au setup — voir GlpiNgSetupInitializer) et /Account/Logout
        // (voir AccountController). AddCascadingAuthenticationState rend l'utilisateur courant
        // disponible aux composants Blazor (ex. MainLayout) via [CascadingParameter] Task<AuthenticationState>.
        builder.Services.AddCascadingAuthenticationState();
        AuthenticationBuilder authenticationBuilder = builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.LoginPath = "/login";
                options.AccessDeniedPath = "/login";
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
                options.SlidingExpiration = true;
            });
        // FallbackPolicy (authentification requise par défaut) : sans lui, AuthorizeRouteView
        // (voir Routes.razor) n'a aucune politique à appliquer aux pages qui n'ont pas
        // explicitement [Authorize], donc la navigation interne au circuit Blazor (qui ne
        // repasse pas par RequireAuthorization() au niveau des endpoints) les laisserait
        // accessibles sans authentification. Login.razor reste accessible via son
        // [AllowAnonymous] explicite, qui prime toujours sur le FallbackPolicy.
        builder.Services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();

            // Utilisée par GlpiImportController (module Inventory) pour exiger un jeton Bearer
            // valide avec le scope "api" ou "inventory" — remplace le [AllowAnonymous] documenté
            // comme temporaire sur /admin/import/glpi. Référencée par son nom depuis le module
            // (chaîne "OAuthApiAccess") plutôt que par une constante partagée, pour ne pas faire
            // dépendre le module du projet hôte.
            options.AddPolicy("OAuthApiAccess", policy => policy
                .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser()
                .RequireClaim(OAuthTokenConstants.ScopeClaimType, "api", "inventory"));
        });

        // API pour l'agent GLPI (contact / inventory / deploy). AddControllersWithViews (plutôt
        // que AddControllers) est nécessaire pour enregistrer les services ViewFeatures dont
        // dépend [ValidateAntiForgeryToken] (voir AccountController.Login) : sans ça, le filtre
        // ne se résout pas et /Account/Login lève une InvalidOperationException au runtime.
        builder.Services.AddControllersWithViews();

        // Documentation OpenAPI/Swagger des contrôleurs API (protocole agent + import GLPI).
        // N'inclut pas les pages Blazor, qui ne sont pas des endpoints API.
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "GlpiNg API",
                Version = "v1",
                Description = "Endpoints REST de GlpiNg : protocole GLPI-Agent (/inventory) et import depuis une base GLPI MySQL (/admin/import/glpi, protégé par OAuth2 — voir /config/oauth-clients)."
            });

            foreach (Assembly assembly in new[]
                     {
                         Assembly.GetExecutingAssembly(),
                         typeof(InventoryModuleServiceCollectionExtensions).Assembly,
                         typeof(DeploymentModuleServiceCollectionExtensions).Assembly,
                     })
            {
                string xmlPath = Path.Combine(AppContext.BaseDirectory, $"{assembly.GetName().Name}.xml");
                if (File.Exists(xmlPath))
                {
                    options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
                }
            }

            // Permet d'utiliser le bouton "Authorize" de Swagger UI pour obtenir un jeton via un
            // client OAuth "client_credentials" (voir /config/oauth-clients) et l'attacher automatiquement
            // aux appels vers /admin/import/glpi.
            options.AddSecurityDefinition("OAuth2", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.OAuth2,
                Flows = new OpenApiOAuthFlows
                {
                    ClientCredentials = new OpenApiOAuthFlow
                    {
                        TokenUrl = new Uri("/oauth2/token", UriKind.Relative),
                        Scopes = new Dictionary<string, string>
                        {
                            ["api"] = "Accès général à l'API GlpiNg",
                            ["inventory"] = "Accès aux données d'inventaire (parc, import GLPI)",
                        },
                    },
                },
            });
            // N'attache l'exigence de jeton qu'aux opérations réellement protégées (ex.
            // /admin/import/glpi) plutôt qu'à toutes — voir OAuthSecurityRequirementFilter,
            // qui n'agit que sur les actions portant [Authorize(AuthenticationSchemes = "Bearer")].
            options.OperationFilter<OAuthSecurityRequirementFilter>();
        });

        // Client HTTP utilisé pour interroger l'interface web locale de GLPI-Agent
        // (httpd-trust, ex. /status, /now) depuis la fiche ordinateur. UseProxy = false : sans
        // ça, une requête vers une machine du LAN peut rester bloquée jusqu'au HttpClient.Timeout
        // si le process essaie de passer par le proxy système configuré sur le serveur (le
        // navigateur, lui, bypass le proxy pour les adresses locales). ConnectCallback personnalisé
        // car un hostname NetBIOS/LLMNR résout ici vers 5 adresses IPv6 injoignables (link-local
        // d'interfaces virtuelles) en plus des IPv4 valides — la résolution de connexion par défaut
        // de .NET les essaie séquentiellement et épuise largement le timeout avant d'atteindre
        // l'IPv4 qui fonctionne. On ignore l'IPv6 et on course toutes les adresses IPv4 en parallèle.
        builder.Services.AddHttpClient("GlpiAgent", client => client.Timeout = TimeSpan.FromSeconds(10))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                UseProxy = false,
                ConnectTimeout = TimeSpan.FromSeconds(5),
                ConnectCallback = ConnectToGlpiAgentAsync,
            });

        // Client des appels sortants de webhook. UseProxy = false pour la même raison que le
        // client "GlpiAgent" ci-dessus : un destinataire sur le LAN ne doit pas partir dans le
        // proxy système. Redirections non suivies : un webhook signé perdrait sa signature en
        // rejouant la requête ailleurs, et une redirection silencieuse vers un autre hôte est
        // exactement ce qu'un secret partagé sert à empêcher. Le délai réel est fixé par appel
        // depuis WebhookSettings.TimeoutSeconds, donc large ici.
        builder.Services.AddHttpClient(WebhookSender.HttpClientName, client => client.Timeout = TimeSpan.FromMinutes(5))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                UseProxy = false,
                AllowAutoRedirect = false,
                ConnectTimeout = TimeSpan.FromSeconds(10),
            });

        // Activation de Swagger pilotée par la config (section "Swagger:Enabled",
        // modifiable depuis la page /config) : IOptionsMonitor permet une bascule à chaud,
        // sans redémarrage — voir l'usage dans le pipeline ci-dessous.
        builder.Services.Configure<SwaggerOptions>(builder.Configuration.GetSection(SwaggerOptions.SectionName));

        // Lecture/écriture de appsettings.json depuis la page /config (adresses d'écoute
        // du serveur, activation de Swagger).
        builder.Services.AddSingleton<AppSettingsFileStore>();
        builder.Services.AddSingleton<ConfigHistoryService>();

        // Assistant d'installation premier démarrage (page /setup intégrée). GlpiNg n'autorise
        // que les bases relationnelles serveur — SQLite n'est volontairement pas proposé.
        builder.Services.AddFileBasedSetup<GlpiNgSetupInitializer>(setupOptions =>
        {
            setupOptions.AllowedProviders = [DbProvider.SqlServer, DbProvider.MySql, DbProvider.Postgres];
            setupOptions.AllowUsernameAdmin = true;

            // L'assistant écrit dans la racine du stockage et non à côté du binaire, comme le
            // reste de ce qui est propre à l'installation. Un chemin absolu est repris tel quel
            // par AnthoDingo.Setup ; un chemin relatif serait résolu depuis ContentRootPath.
            setupOptions.LocalConfigFileName = storagePaths.LocalSettings;
        });

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
            // Cloisonnement par entité. L'ordre de ces trois enregistrements compte :
            //
            // 1. IRootDbContextFactory (singleton) : accès délibérément NON cloisonné. Construit
            //    le contexte depuis les DbContextOptions enregistrés juste au-dessus, sans passer
            //    par IDbContextFactory<GlpiNgDbContext> — puisque ce service est justement
            //    remplacé ci-dessous par une fabrique scoped, qu'un singleton ne peut pas
            //    consommer. Réservé aux services singleton (SettingsCacheService,
            //    LdapAuthenticationService) et au calcul du cloisonnement lui-même.
            //
            // 2. EntityScopedDbContextFactory remplace IDbContextFactory<GlpiNgDbContext> par une
            //    version scoped qui estampille chaque contexte avec le EntityScope de
            //    l'utilisateur. C'est ce qui rend cloisonnées, sans les modifier, les ~97 pages
            //    qui font déjà DbFactory.CreateDbContextAsync().
            //
            // 3. IDbContextFactory<DbContext> (l'adaptateur utilisé par les pages des modules)
            //    passe de Singleton à Scoped pour la même raison : il délègue désormais à une
            //    fabrique scoped.
            builder.Services.AddSingleton<IRootDbContextFactory, RootDbContextFactory>();
            builder.Services.AddSingleton<EntityTreeCache>();
            builder.Services.AddScoped<IEntityScopeProvider, EntityScopeProvider>();
            builder.Services.AddScoped<IProfileRightsProvider, ProfileRightsProvider>();

            // Préférences d'affichage du compte connecté, consultées par les pages des modules
            // (taille des tableaux, adresses MAC, dates, fuseau, noms...). Scoped : une lecture par
            // requête et par circuit, mémorisée — voir UserPreferencesProvider. Le préchargeur les
            // lit à l'ouverture de chaque circuit, et UserDisplay les expose au balisage sous le
            // nom « Display » (voir les _Imports.razor).
            builder.Services.AddScoped<IUserPreferences, UserPreferencesProvider>();
            builder.Services.AddScoped<Microsoft.AspNetCore.Components.Server.Circuits.CircuitHandler, UserPreferencesPreloader>();
            builder.Services.AddScoped<ProfileRightsService>();
            builder.Services.AddScoped<EntityDeletionGuard>();
            builder.Services.AddScoped<IEntityOptionsProvider, EntityOptionsProvider>();
            builder.Services.AddScoped<LdapAccountProvisioner>();
            builder.Services.AddScoped<LdapUserImportService>();
            builder.Services.AddScoped<UserEntityAccessService>();
            builder.Services.AddScoped<IDbContextFactory<GlpiNgDbContext>, EntityScopedDbContextFactory>();

            builder.Services.AddScoped<GlpiNgDbContext>(sp =>
                sp.GetRequiredService<IDbContextFactory<GlpiNgDbContext>>().CreateDbContext());

            // Les modules (ex. GlpiMySqlImportService dans Inventory) dépendent du DbContext de
            // base plutôt que de GlpiNgDbContext, pour ne pas référencer le projet hôte : c'est
            // à l'hôte de faire le lien vers son DbContext concret.
            builder.Services.AddScoped<DbContext>(sp => sp.GetRequiredService<GlpiNgDbContext>());

            // Même lien pour les pages Razor des modules (ex. Components/Pages/Computers dans
            // Inventory), qui ont besoin d'un IDbContextFactory<DbContext> — pas seulement d'un
            // DbContext scoped — pour la même raison de concurrence au pré-rendu que
            // IDbContextFactory<GlpiNgDbContext> ci-dessus. Voir DbContextFactoryAdapter.
            builder.Services.AddScoped<IDbContextFactory<DbContext>>(sp =>
                new DbContextFactoryAdapter(sp.GetRequiredService<IDbContextFactory<GlpiNgDbContext>>()));

            // Services dépendant de GlpiNgDbContext : n'ont de sens qu'une fois l'installation
            // terminée, donc enregistrés ici plutôt que plus haut (sinon la validation des
            // services au build échoue en environnement Development, faute de DbContext).
            builder.Services.AddScoped<InventoryImportService>();

            // Import des résultats "netdiscovery"/"netinventory" (voir AgentController) —
            // distinct d'InventoryImportService, voir sa doc.
            builder.Services.AddScoped<NetworkDeviceImportService>();

            // Authentification externe par bind LDAP (voir AccountController.Login et la page
            // /config/auth). AuthSecretProtector n'a pas de dépendance DbContext mais est
            // enregistré ici pour rester à proximité de son seul consommateur.
            // SetApplicationName explicite : sans lui, DataProtection dérive son discriminant du
            // chemin de la racine de contenu. Déplacer ou renommer le dossier de l'application
            // rend alors illisible tout ce qui a été chiffré avant — ici le mot de passe du compte
            // de connexion LDAP, qui redevient silencieusement vide. Le bind dégénère en bind non
            // authentifié et Active Directory refuse ensuite toute recherche, sans que la fiche de
            // l'annuaire ne laisse rien paraître. Le nom fixé ici découple les secrets du chemin
            // d'installation.
            // Clés persistées sous la racine de stockage plutôt qu'à l'emplacement par défaut
            // (%LOCALAPPDATA%\ASP.NET\DataProtection-Keys), qui est propre au compte Windows
            // exécutant l'application : lancer le service sous un autre compte y rendait illisibles
            // tous les secrets déjà chiffrés — dont le mot de passe du compte de connexion LDAP.
            // Sous la racine, les clés suivent l'installation et se sauvegardent avec elle.
            builder.Services.AddDataProtection()
                .SetApplicationName("GlpiNg")
                .PersistKeysToFileSystem(new DirectoryInfo(storagePaths.Ensure(storagePaths.Keys)));
            builder.Services.AddSingleton<AuthSecretProtector>();
            builder.Services.AddSingleton<LdapAuthenticationService>();
            builder.Services.AddScoped<UserCredentialAuthenticator>();

            // Émission des jetons OAuth2 (voir /oauth2/token, Controllers.OAuthController) pour
            // les clients gérés depuis /config/oauth-clients.
            builder.Services.AddSingleton<OAuthTokenIssuer>();

            // Documents (voir Models/Documents) : entité de l'hôte, rendue aux modules via
            // IDocumentAttachments — c'est ce contrat que la base de connaissances utilise pour
            // son onglet « Documents », sans jamais voir le modèle ni le stockage sur disque.
            // DocumentStorageService ne touche que le disque (IStoragePaths est un singleton) :
            // il peut l'être aussi. DocumentService, lui, consomme la fabrique de DbContext, qui
            // est enregistrée en scoped ici — d'où une portée scoped, comme les autres services
            // qui lisent la base.
            builder.Services.AddSingleton<DocumentStorageService>();
            builder.Services.AddScoped<DocumentService>();
            builder.Services.AddScoped<IDocumentAttachments>(sp => sp.GetRequiredService<DocumentService>());

            // Notes libres (onglet « Notes » de GLPI) : même montage que les documents — entité de
            // l'hôte, rendue aux modules par un contrat qui ne leur ouvre ni le modèle ni le
            // DbContext concret.
            builder.Services.AddScoped<NoteService>();
            builder.Services.AddScoped<IItemNotes>(sp => sp.GetRequiredService<NoteService>());

            // Cache mémoire des sections de réglages /config (Valeurs par défaut, Parc, Assistance,
            // Modules, ...), lues/écrites dans la table AppSettings plutôt que dans appsettings.json
            // (voir AppSettingsFileStore, qui ne garde plus que Urls/Swagger). Enregistré ici comme
            // AddCronModule ci-dessous, pour la même raison : dépend de IDbContextFactory<GlpiNgDbContext>.
            builder.Services.AddSingleton<SettingsCacheService>();

            // Schéma d'authentification Bearer (jetons émis par /oauth2/token) : enregistré ici
            // plutôt qu'avec AddCookie plus haut, car EnsureOAuthSigningKey persiste la clé de
            // signature dans appsettings.local.json — sûr à cet endroit précis, puisque
            // Setup:IsComplete == "true" garantit que l'assistant d'installation a fini d'écrire
            // ce fichier et ne l'écrira plus. N'est demandé explicitement que par les endpoints
            // qui l'exigent (voir la policy "OAuthApiAccess" plus haut et son usage sur
            // GlpiImportController) : le schéma cookie par défaut n'est pas affecté.
            string oauthSigningKey = EnsureOAuthSigningKey(builder, storagePaths);
            authenticationBuilder.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = OAuthTokenConstants.Issuer,
                    ValidateAudience = true,
                    ValidAudience = OAuthTokenConstants.Audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Convert.FromBase64String(oauthSigningKey)),
                };
            });

            // Module Inventory (modèle de parc + import GLPI MySQL) : voir
            // GlpiNg.Modules.Inventory.InventoryModuleServiceCollectionExtensions. Les futurs
            // modules (Tickets, etc.) suivront le même schéma AddXxxModule(...). Enregistré ici
            // (et non plus haut) car il enregistre GlpiMySqlImportService, qui dépend lui aussi
            // du DbContext de base — même raison que InventoryImportService ci-dessus. Sans
            // conséquence sur la disponibilité de /admin/import/glpi avant la fin de
            // l'installation : UseSetupMiddleware redirige de toute façon tout vers /setup.
            builder.Services.AddInventoryModule(builder.Configuration);

            // Permet à GlpiImportStateService (module Inventory, page /admin/import/glpi) de
            // déclencher aussi l'import "Administration" (entités/groupes/profils/utilisateurs) et
            // configuration générale, sans que ce module dépende de GlpiEntity/GlpiGroup/
            // GlpiProfile/GlpiUser — même principe qu'ICurrentUserDeploymentContextProvider plus bas.
            builder.Services.AddScoped<IGlpiAdminImportService, GlpiAdminMySqlImportService>();

            // Pendant du précédent pour le domaine réseau/déploiement : reprend les données du
            // plugin GLPI Inventory détecté sur la base source (voir GlpiInventoryPluginInfo).
            builder.Services.AddScoped<IGlpiInventoryPluginImportService, GlpiInventoryPluginImportService>();

            // Alimente les sélecteurs de cibles (Entité/Groupe/Profil/Utilisateur) des modules —
            // onglet "Cibles pour le déploiement à la demande" d'un paquet, onglet "Cibles" d'un
            // article de la base de connaissances — sans qu'ils dépendent de ces types
            // (GlpiNg.Web.Models, domaine utilisateurs/groupes/entités/profils non extrait en
            // module). Voir IPrincipalDirectory (GlpiNg.Modules.Abstractions) ; la même
            // implémentation rend aussi l'ancien IDeploymentTargetDirectory, d'où les deux
            // enregistrements sur la même classe.
            builder.Services.AddScoped<IPrincipalDirectory, PrincipalDirectory>();
            builder.Services.AddScoped<IDeploymentTargetDirectory, PrincipalDirectory>();

            // Traduit un utilisateur connecté en ses habilitations (entités, profils, groupes), de
            // quoi évaluer ces mêmes cibles côté lecteur : éligibilité au libre-service (module
            // Déploiement), visibilité d'un article (module Base de connaissances) — même principe
            // qu'IPrincipalDirectory ci-dessus, dans le sens inverse.
            builder.Services.AddScoped<IPrincipalContextProvider, PrincipalContextProvider>();
            builder.Services.AddScoped<ICurrentUserDeploymentContextProvider, PrincipalContextProvider>();

            // Module Deployment (voir GlpiNg.Modules.Deployment.DeploymentModuleServiceCollectionExtensions) :
            // agents GLPI, paquets/jobs de déploiement, groupes d'ordinateurs dynamiques, créneaux
            // horaires, définitions de collecte. Enregistré ici pour la même raison qu'AddInventoryModule
            // ci-dessus : ComputerDeploymentTasksProvider et ses contrôleurs/pages dépendent du DbContext
            // de base.
            builder.Services.AddDeploymentModule();

            // Import de la base de connaissances GLPI (catégories, articles, cibles, révisions) —
            // voir IGlpiKnowledgeBaseImportService, hébergé ici pour la même raison que l'import du
            // plugin d'inventaire : seul l'hôte voit à la fois les modèles du module et les
            // entités/groupes/profils/comptes auxquels les cibles renvoient.
            builder.Services.AddScoped<IGlpiKnowledgeBaseImportService, GlpiKnowledgeBaseImportService>();

            // Module Base de connaissances (voir
            // GlpiNg.Modules.KnowledgeBase.KnowledgeBaseModuleServiceCollectionExtensions) :
            // articles, catégories, révisions et cibles de visibilité. Enregistré ici pour la même
            // raison que les modules ci-dessus : ses pages dépendent du DbContext de base.
            builder.Services.AddKnowledgeBaseModule();

            // Module Gestion (voir
            // GlpiNg.Modules.Management.ManagementModuleServiceCollectionExtensions) : tiers,
            // contrats et budgets du groupe « Gestion ». Même raison que ci-dessus pour l'ordre.
            builder.Services.AddManagementModule();

            // Module Assistance (voir
            // GlpiNg.Modules.Assistance.AssistanceModuleServiceCollectionExtensions) : les tickets
            // du groupe « Assistance ». Même raison que ci-dessus pour l'ordre.
            builder.Services.AddAssistanceModule();

            // Rapports (/tools/reports) : vue unique sur les IReportProvider contribués par les
            // modules ci-dessus (Inventory, Deployment, Base de connaissances). Enregistré après eux, pour que la
            // résolution d'IEnumerable<IReportProvider> les voie tous — l'hôte ne déclare lui-même
            // aucun rapport, voir ReportCatalog.
            builder.Services.AddScoped<ReportCatalog>();

            // Module Scheduler (voir GlpiNg.Modules.Scheduler.SchedulerModuleServiceCollectionExtensions) :
            // contribue un ICronTask qui lance automatiquement les DeploymentTask dont la fenêtre
            // planifiée (ScheduledStartTime/ScheduledEndTime/ExecutionTimeSlotId, voir leur doc) est
            // ouverte — jusqu'ici purement déclaratifs, sans moteur pour les consommer. Enregistré
            // après AddDeploymentModule ci-dessus, dont il dépend (DeploymentTaskLaunchService).
            builder.Services.AddSchedulerModule();

            // Module Cron (voir GlpiNg.Modules.Cron.CronModuleServiceCollectionExtensions) :
            // exécute à intervalle régulier (réglable à chaud depuis /config → "Configuration
            // générale" → "Système", voir SystemSection.razor) les ICronTask contribués par les
            // modules. Enregistré ici pour la même raison qu'AddInventoryModule ci-dessus : son
            // service dépend du DbContext de base.
            builder.Services.AddScoped<ICronTask, HistoryPurgeCronTask>();

            // Rattrapage des articles repris avant que la conversion HTML → Markdown ne
            // fonctionne : relancer l'import les corrigerait aussi, mais en écrasant les
            // retouches faites depuis et en exigeant que la base GLPI source soit joignable.
            builder.Services.AddScoped<ICronTask, KnowledgeBaseHtmlReconversionCronTask>();

            // Jobs de déploiement que l'agent a reçus mais jamais terminés (JSON refusé, poste
            // éteint en pleine installation) : sans elle, ils restent « en cours » pour toujours.
            builder.Services.AddScoped<ICronTask, DeploymentJobTimeoutCronTask>();

            // Notifications (voir /config/notifications, Models/Notifications et
            // Services/Notifications) : NotificationDispatchService dépose des QueuedNotification
            // au fil des événements réels de GlpiNg (nouvel ordinateur, nouvel agent, fin de
            // déploiement — voir ses points d'appel dans InventoryImportService/AgentController),
            // QueuedNotificationSenderCronTask les expédie par SMTP (SmtpMailSender, MailKit) à
            // chaque tick du même service cron que HistoryPurgeCronTask ci-dessus.
            builder.Services.AddScoped<NotificationDispatchService>();

            // Le même service, rendu aux modules par son contrat : c'est ainsi que le module
            // Assistance notifie l'ouverture d'un ticket sans rien connaître des gabarits ni des
            // destinataires.
            builder.Services.AddScoped<INotificationPublisher, ModuleNotificationPublisher>();

            builder.Services.AddSingleton<SmtpMailSender>();
            builder.Services.AddScoped<ICronTask, QueuedNotificationSenderCronTask>();

            // Unicité des champs (voir /config/field-unicity, Models/FieldUnicity et
            // Services/FieldUnicity) : les modules ne connaissent que le contrat
            // IFieldUnicityChecker, qu'ils appellent avant chaque création d'actif, et l'hôte porte
            // seul la table des critères. Le catalogue suit IDbContextFactory, enregistré ici par
            // requête ; sa lecture du modèle EF Core, elle, est mise en cache pour le processus.
            builder.Services.AddScoped<FieldUnicityCatalog>();
            builder.Services.AddScoped<IFieldUnicityChecker, FieldUnicityService>();

            // Webhooks (voir /config/webhooks, Models/Webhooks et Services/Webhooks) : second
            // canal branché sur les mêmes événements que les notifications ci-dessus —
            // NotificationDispatchService délègue à WebhookDispatchService, qui dépose des
            // QueuedWebhook, et QueuedWebhookSenderCronTask les expédie en HTTP au même tick cron.
            builder.Services.AddScoped<WebhookDispatchService>();
            builder.Services.AddScoped<WebhookSender>();
            builder.Services.AddScoped<ICronTask, QueuedWebhookSenderCronTask>();

            // Liens externes (voir /config/external-links) : contrat porté par Abstractions et
            // implémenté ici, consommé par les fiches des modules — même montage
            // qu'IUserPreferences plus haut, l'hôte détenant les données et le module l'affichage.
            builder.Services.AddScoped<IExternalLinkProvider, ExternalLinkProvider>();

            // Journal des évènements système consulté sur /admin/logs (voir Administration →
            // "Journaux", EventLogEntry) : connexions, contacts d'agent GLPI-Agent, ...
            builder.Services.AddScoped<EventLogService>();

            builder.Services.AddCronModule();
        }

        WebApplication app = builder.Build();

        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
            app.UseHsts();
        }

        // Activation de Swagger pilotée par appsettings.json ("Swagger:Enabled", éditable
        // depuis /config) plutôt que par l'environnement : IOptionsMonitor est réévalué à
        // chaque requête, donc le changement s'applique sans redémarrage. Désactivé par défaut
        // par prudence, même si /admin/import/glpi exige désormais un jeton OAuth Bearer
        // (policy "OAuthApiAccess" — voir GlpiImportController).
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
        // redirige toute requête navigateur (hors protocole agent /inventory) vers /update, qui
        // permet à un administrateur de confirmer leur application. Placé après
        // UseSetupMiddleware pour la même raison que GlpiNgDbContext est garanti enregistré ici.
        app.UseMigrationsGate();

        app.UseAuthentication();
        app.UseAuthorization();

        // Droits par profil : refuse l'accès direct par URL à une section que l'utilisateur n'a
        // pas le droit de lire. Après UseAuthentication, qui lui fournit l'utilisateur. La
        // navigation interne au circuit Blazor ne passe pas par ici — elle est gardée dans
        // MainLayout, sur la même table de correspondance (voir ProfileSectionMap).
        app.UseSectionAccess();

        // Préférences d'affichage chargées avant le prérendu des pages, qui les lit sans attendre
        // (voir UserPreferencesPreloader). Après l'authentification, qui fournit l'utilisateur.
        UserPreferencesPreloader.UseUserPreferencesPreload(app);

        // AllowAnonymous() explicite : sans lui, le FallbackPolicy (voir plus haut) exige une
        // session authentifiée même pour les fichiers statiques (CSS/JS), ce qui casserait entre
        // autres le style de la page de login elle-même, accessible avant authentification.
        app.MapStaticAssets().AllowAnonymous();

        // Exclut l'upload de fichiers de paquet (DeploymentPackageFilesController) du middleware
        // antiforgery, en plus de [IgnoreAntiforgeryToken] sur le contrôleur (qui suffirait déjà
        // via les métadonnées d'endpoint) : ceinture et bretelles pour cette route jamais atteinte
        // via un <form>/<AntiforgeryToken /> Blazor (appelée en XHR depuis glping.js) — protection
        // CSRF assurée par SameSite=Lax des cookies d'authentification (voir la doc du contrôleur).
        // La vraie cause de "Unexpected end of Stream, the content may have already been read by
        // another component." sur les gros uploads n'était pas l'antiforgery mais le model binding
        // MVC lui-même (FormValueProviderFactory) — voir [DisableFormValueModelBinding] sur
        // DeploymentPackageFilesController.UploadAsync.
        app.UseWhen(
            context => !context.Request.Path.StartsWithSegments("/deployment-packages"),
            branch => branch.UseAntiforgery());

        // Pas de RequireAuthorization() ici : les agents GLPI (/inventory, voir AgentController)
        // et /Account/Login|Logout ne portent pas de cookie de session applicative.
        app.MapControllers();

        // Toutes les pages Blazor exigent une session authentifiée, sauf celles marquées
        // @attribute [AllowAnonymous] (Login.razor). AddAdditionalAssemblies expose les pages
        // @page définies dans les modules (ex. Components/Pages/Computers dans Inventory), dont
        // l'assembly distincte du projet hôte ne serait sinon pas découverte.
        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode()
            // Tout module apportant des pages @page doit figurer ici, en plus du Router de
            // Routes.razor : ce dernier ne sert que la navigation interne au circuit, tandis que
            // cette liste sert les entrées par l'URL (lien collé, favori, rechargement). Un module
            // déclaré au seul Router donne des pages qui s'ouvrent depuis le menu mais répondent
            // 404 quand on colle leur adresse — c'était le cas de la base de connaissances.
            .AddAdditionalAssemblies(
                typeof(InventoryModuleServiceCollectionExtensions).Assembly,
                typeof(DeploymentModuleServiceCollectionExtensions).Assembly,
                typeof(KnowledgeBaseModuleServiceCollectionExtensions).Assembly,
                typeof(ManagementModuleServiceCollectionExtensions).Assembly,
                typeof(AssistanceModuleServiceCollectionExtensions).Assembly)
            .RequireAuthorization()
            // Les conventions posées ici (RequireAuthorization ci-dessus) ne s'appliquent pas
            // qu'aux pages : elles retombent aussi sur les endpoints du hub SignalR (/_blazor,
            // /_blazor/negotiate, ...) montés par AddInteractiveServerRenderMode. Sans ce
            // rattrapage, /_blazor/negotiate répond 401 à un visiteur non authentifié, le circuit
            // interactif ne s'ouvre jamais, et une page [AllowAnonymous] interactive reste figée
            // sur son rendu serveur — ses @onclick ne sont jamais câblés. C'est ce qui rendait le
            // bouton « Confirmer la mise à niveau » de /update sans effet : cette page est
            // justement atteinte avant toute connexion possible (UseMigrationsGate redirige aussi
            // /login vers /update). Retirer RequireAuthorization ne suffirait pas : le
            // FallbackPolicy (voir plus haut) provoquerait le même 401 sur un endpoint dépourvu
            // de métadonnée d'autorisation.
            // Ouvrir le hub aux anonymes ne rend aucune page accessible : dans le circuit,
            // l'autorisation est assurée par AuthorizeRouteView (voir Routes.razor) adossé au
            // FallbackPolicy — c'est le modèle de sécurité normal d'une Blazor Web App interactive.
            .Add(endpointBuilder =>
            {
                if (endpointBuilder is RouteEndpointBuilder route
                    && route.RoutePattern.RawText?.StartsWith("/_blazor", StringComparison.OrdinalIgnoreCase) == true)
                {
                    endpointBuilder.Metadata.Add(new AllowAnonymousAttribute());
                }
            });

        app.Run();
    }

    /// <summary>
    /// ConnectCallback du client HTTP "GlpiAgent" (voir son enregistrement plus haut) : résout
    /// uniquement les adresses IPv4 de l'hôte et course une connexion TCP en parallèle vers
    /// chacune, en retenant la première qui aboutit. Évite l'attente séquentielle sur des
    /// adresses IPv6 injoignables que la résolution de connexion par défaut de .NET essaierait
    /// avant l'IPv4.
    /// </summary>
    private static async ValueTask<Stream> ConnectToGlpiAgentAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        IPAddress[] addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, AddressFamily.InterNetwork, cancellationToken);
        if (addresses.Length == 0)
        {
            throw new SocketException((int)SocketError.HostNotFound);
        }

        using var attemptsCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        List<Task<Socket>> attempts = addresses.Select(address => ConnectOneAsync(address, context.DnsEndPoint.Port, attemptsCts.Token)).ToList();

        Exception? lastError = null;
        while (attempts.Count > 0)
        {
            Task<Socket> finished = await Task.WhenAny(attempts);
            attempts.Remove(finished);
            try
            {
                Socket socket = await finished;
                await attemptsCts.CancelAsync();
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (Exception ex)
            {
                lastError = ex;
            }
        }

        throw lastError ?? new SocketException((int)SocketError.HostUnreachable);

        static async Task<Socket> ConnectOneAsync(IPAddress address, int port, CancellationToken ct)
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                await socket.ConnectAsync(address, port, ct);
                return socket;
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }
    }

    /// <summary>
    /// Refuse de démarrer si le fichier de configuration est resté à son ancien emplacement (à
    /// côté du binaire) alors que le nouveau, dans la racine du stockage, est absent.
    ///
    /// Sans ce garde-fou, une installation déjà faite repartirait silencieusement à zéro : faute
    /// de trouver « Setup:IsComplete », le middleware d'installation redirigerait tout vers
    /// /setup, et l'assistant proposerait de réinstaller par-dessus une base qui contient déjà
    /// les données. Une erreur au démarrage, qui dit quoi déplacer et où, coûte infiniment moins
    /// cher que cette réinstallation-là.
    /// </summary>
    private static void EnsureLocalSettingsNotLeftBehind(IWebHostEnvironment environment, StoragePaths storagePaths)
    {
        string legacyPath = Path.Combine(environment.ContentRootPath, StoragePaths.LocalSettingsFileName);

        if (!File.Exists(legacyPath) || File.Exists(storagePaths.LocalSettings))
        {
            return;
        }

        throw new InvalidOperationException(
            $"« {StoragePaths.LocalSettingsFileName} » a été trouvé à l'ancien emplacement ({legacyPath}) " +
            $"mais pas au nouveau ({storagePaths.LocalSettings}). Déplacez le fichier vers la racine du " +
            "stockage pour continuer : il contient la chaîne de connexion et l'état de l'installation, sans " +
            "lesquels GlpiNg repartirait sur l'assistant d'installation.");
    }

    /// <summary>
    /// Renvoie la clé de signature des jetons OAuth2 (base64, 256 bits), en la générant et en la
    /// persistant dans appsettings.local.json au premier démarrage si elle est absente. Doit
    /// s'exécuter avant builder.Build() : AddJwtBearer a besoin de la clé pour configurer la
    /// validation des jetons dès l'enregistrement des services, pas seulement au premier appel.
    /// </summary>
    private static string EnsureOAuthSigningKey(WebApplicationBuilder builder, StoragePaths storagePaths)
    {
        string? existing = builder.Configuration["Oauth:SigningKey"];
        if (!string.IsNullOrEmpty(existing))
        {
            return existing;
        }

        string key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        // La racine existe déjà (créée au tout début de Main), mais l'écriture ci-dessous est la
        // première à en dépendre vraiment : la recréer ici coûte un appel et évite d'échouer si
        // elle a disparu entre-temps — une racine sur un partage réseau, typiquement.
        storagePaths.Ensure(storagePaths.Root);

        string localSettingsPath = storagePaths.LocalSettings;
        JsonNode root = File.Exists(localSettingsPath)
            ? JsonNode.Parse(File.ReadAllText(localSettingsPath)) ?? new JsonObject()
            : new JsonObject();

        JsonObject oauthSection = root["Oauth"] as JsonObject ?? new JsonObject();
        oauthSection["SigningKey"] = key;
        root["Oauth"] = oauthSection;

        File.WriteAllText(localSettingsPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        // Rend la clé visible immédiatement dans builder.Configuration, sans dépendre du
        // rechargement automatique du fichier (AddJsonFile(..., reloadOnChange: true) peut ne
        // pas avoir déjà repris la modification à ce stade du démarrage).
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Oauth:SigningKey"] = key });

        return key;
    }
}

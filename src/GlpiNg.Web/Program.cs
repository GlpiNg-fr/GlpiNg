using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using AnthoDingo.Setup;
using GlpiNg.Modules.Abstractions.Cron;
using GlpiNg.Modules.Abstractions.Deployment;
using GlpiNg.Modules.Abstractions.Entities;
using GlpiNg.Modules.Abstractions.Import;
using GlpiNg.Modules.Cron;
using GlpiNg.Modules.Deployment;
using GlpiNg.Modules.Inventory;
using GlpiNg.Modules.Scheduler;
using GlpiNg.Web.Components;
using GlpiNg.Web.Data;
using GlpiNg.Web.Import;
using GlpiNg.Web.Middleware;
using GlpiNg.Web.Options;
using GlpiNg.Web.Services;
using GlpiNg.Web.Services.Notifications;
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

        // Fichier écrit par l'assistant d'installation (AnthoDingo.Setup) à la fin du wizard —
        // prioritaire sur appsettings.json une fois l'installation terminée. Ne doit jamais être
        // commité (voir .gitignore).
        builder.Configuration.AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true);

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
            builder.Services.AddDataProtection().SetApplicationName("GlpiNg");
            builder.Services.AddSingleton<AuthSecretProtector>();
            builder.Services.AddSingleton<LdapAuthenticationService>();
            builder.Services.AddScoped<UserCredentialAuthenticator>();

            // Émission des jetons OAuth2 (voir /oauth2/token, Controllers.OAuthController) pour
            // les clients gérés depuis /config/oauth-clients.
            builder.Services.AddSingleton<OAuthTokenIssuer>();

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
            string oauthSigningKey = EnsureOAuthSigningKey(builder);
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

            // Alimente les sélecteurs de cibles (Entité/Groupe/Profil/Utilisateur) de l'onglet
            // "Cibles pour le déploiement à la demande" de la fiche Paquet (module Deployment)
            // sans que celui-ci dépende de ces types (GlpiNg.Web.Models, domaine utilisateurs/
            // groupes/entités/profils non extrait en module) — voir IDeploymentTargetDirectory
            // (GlpiNg.Modules.Abstractions).
            builder.Services.AddScoped<IDeploymentTargetDirectory, DeploymentTargetDirectory>();

            // Donne au module Déploiement de quoi évaluer l'éligibilité de l'utilisateur connecté
            // au libre-service (page /self-service, voir SelfServiceDeploymentService) sans qu'il
            // dépende de GlpiUser/GlpiUserProfile/GlpiGroupUser — même principe
            // qu'IDeploymentTargetDirectory ci-dessus, dans le sens inverse.
            builder.Services.AddScoped<ICurrentUserDeploymentContextProvider, CurrentUserDeploymentContextProvider>();

            // Module Deployment (voir GlpiNg.Modules.Deployment.DeploymentModuleServiceCollectionExtensions) :
            // agents GLPI, paquets/jobs de déploiement, groupes d'ordinateurs dynamiques, créneaux
            // horaires, définitions de collecte. Enregistré ici pour la même raison qu'AddInventoryModule
            // ci-dessus : ComputerDeploymentTasksProvider et ses contrôleurs/pages dépendent du DbContext
            // de base.
            builder.Services.AddDeploymentModule();

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

            // Notifications (voir /config/notifications, Models/Notifications et
            // Services/Notifications) : NotificationDispatchService dépose des QueuedNotification
            // au fil des événements réels de GlpiNg (nouvel ordinateur, nouvel agent, fin de
            // déploiement — voir ses points d'appel dans InventoryImportService/AgentController),
            // QueuedNotificationSenderCronTask les expédie par SMTP (SmtpMailSender, MailKit) à
            // chaque tick du même service cron que HistoryPurgeCronTask ci-dessus.
            builder.Services.AddScoped<NotificationDispatchService>();
            builder.Services.AddSingleton<SmtpMailSender>();
            builder.Services.AddScoped<ICronTask, QueuedNotificationSenderCronTask>();

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

        // AllowAnonymous() explicite : sans lui, le FallbackPolicy (voir plus haut) exige une
        // session authentifiée même pour les fichiers statiques (CSS/JS), ce qui casserait entre
        // autres le style de la page de login elle-même, accessible avant authentification.
        app.MapStaticAssets().AllowAnonymous();

        // Exclut l'upload de fichiers de paquet (DeploymentPackageFilesController) du middleware
        // antiforgery, en plus de [IgnoreAntiforgeryToken] sur le contrôleur (qui suffirait déjà
        // via les métadonnées d'endpoint) : ceinture et bretelles pour cette route jamais atteinte
        // via un <form>/<AntiforgeryToken /> Blazor (appelée en XHR depuis glpi-ng.js) — protection
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
            .AddAdditionalAssemblies(
                typeof(InventoryModuleServiceCollectionExtensions).Assembly,
                typeof(DeploymentModuleServiceCollectionExtensions).Assembly)
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
    /// Renvoie la clé de signature des jetons OAuth2 (base64, 256 bits), en la générant et en la
    /// persistant dans appsettings.local.json au premier démarrage si elle est absente. Doit
    /// s'exécuter avant builder.Build() : AddJwtBearer a besoin de la clé pour configurer la
    /// validation des jetons dès l'enregistrement des services, pas seulement au premier appel.
    /// </summary>
    private static string EnsureOAuthSigningKey(WebApplicationBuilder builder)
    {
        string? existing = builder.Configuration["Oauth:SigningKey"];
        if (!string.IsNullOrEmpty(existing))
        {
            return existing;
        }

        string key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        string localSettingsPath = Path.Combine(builder.Environment.ContentRootPath, "appsettings.local.json");
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

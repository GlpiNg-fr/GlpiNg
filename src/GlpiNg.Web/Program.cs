using System.Reflection;
using AnthoDingo.Setup;
using GlpiNg.Modules.Inventory;
using GlpiNg.Web.Components;
using GlpiNg.Web.Data;
using GlpiNg.Web.Options;
using GlpiNg.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Fichier écrit par l'assistant d'installation (AnthoDingo.Setup) à la fin du wizard —
// prioritaire sur appsettings.json une fois l'installation terminée. Ne doit jamais être
// commité (voir .gitignore).
builder.Configuration.AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true);

// UI Blazor Server (rendu interactif)
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// API pour l'agent GLPI (contact / inventory / deploy)
builder.Services.AddControllers();

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

if (builder.Configuration["Setup:IsComplete"] == "true"
    && configuredProviderRaw is not null
    && configuredConnectionString is not null
    && Enum.TryParse(configuredProviderRaw, ignoreCase: true, out DbProvider configuredProvider))
{
    builder.Services.AddDbContext<GlpiNgDbContext>(options =>
        GlpiNgDbContext.ConfigureProvider(options, configuredProvider, configuredConnectionString));

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

app.UseStaticFiles();
app.UseAntiforgery();

app.MapControllers();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

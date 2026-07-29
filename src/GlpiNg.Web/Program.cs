using AnthoDingo.Setup;
using GlpiNg.Web.Components;
using GlpiNg.Web.Data;
using GlpiNg.Web.Import;
using GlpiNg.Web.Services;

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
builder.Services.AddScoped<InventoryImportService>();

// Construction du JSON de job de déploiement au format attendu par GLPI-Agent
builder.Services.AddSingleton<DeployJobJsonBuilder>();

// Import de données depuis une base GLPI MySQL existante
builder.Services.Configure<GlpiImportOptions>(builder.Configuration.GetSection(GlpiImportOptions.SectionName));
builder.Services.AddScoped<GlpiMySqlImportService>();

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
}

WebApplication app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// Garde d'installation + page /setup intégrée. Doit être branché en tout premier dans le
// pipeline : tant que l'installation n'est pas terminée, toute autre requête y est redirigée.
app.UseSetupMiddleware("GlpiNg");

app.UseStaticFiles();
app.UseAntiforgery();

app.MapControllers();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

using GlpiNg.Web.Components;
using GlpiNg.Web.Data;
using GlpiNg.Web.Import;
using GlpiNg.Web.Services;
using Microsoft.EntityFrameworkCore;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// UI Blazor Server (rendu interactif)
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// API pour l'agent GLPI (contact / inventory / deploy)
builder.Services.AddControllers();

// Construction du JSON de job de déploiement au format attendu par GLPI-Agent
builder.Services.AddSingleton<DeployJobJsonBuilder>();

// Import de données depuis une base GLPI MySQL existante
builder.Services.Configure<GlpiImportOptions>(builder.Configuration.GetSection(GlpiImportOptions.SectionName));
builder.Services.AddScoped<GlpiMySqlImportService>();

// Base de données — SQLite par défaut en dev, configurable via appsettings
builder.Services.AddDbContext<GlpiNgDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Data Source=glpi-ng.db"));

WebApplication app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapControllers();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

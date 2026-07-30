using GlpiNg.Modules.Inventory;
using GlpiNg.Web.Components;
using GlpiNg.Web.Data;
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

// Module Inventory (modèle de parc + import GLPI MySQL) : voir
// GlpiNg.Modules.Inventory.InventoryModuleServiceCollectionExtensions. Les futurs
// modules (Tickets, etc.) suivront le même schéma AddXxxModule(...).
builder.Services.AddInventoryModule(builder.Configuration);

// Base de données — SQLite par défaut en dev, configurable via appsettings
builder.Services.AddDbContext<GlpiNgDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Data Source=glpi-ng.db"));

// Les modules (ex. GlpiMySqlImportService dans Inventory) dépendent du DbContext de
// base plutôt que de GlpiNgDbContext, pour ne pas référencer le projet hôte : c'est
// à l'hôte de faire le lien vers son DbContext concret.
builder.Services.AddScoped<DbContext>(sp => sp.GetRequiredService<GlpiNgDbContext>());

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

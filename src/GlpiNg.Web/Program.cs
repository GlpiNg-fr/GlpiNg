using System.Reflection;
using GlpiNg.Modules.Inventory;
using GlpiNg.Web.Components;
using GlpiNg.Web.Data;
using GlpiNg.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

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
else
{
    // Réservé au développement : /admin/import/glpi n'a pas encore d'authentification
    // (voir README), donc pas d'exposition de sa documentation hors de cet environnement.
    app.UseSwagger();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "GlpiNg API v1"));
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapControllers();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

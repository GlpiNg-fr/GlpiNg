using GlpiNg.Web.Components;
using GlpiNg.Web.Data;
using GlpiNg.Web.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// UI Blazor Server (rendu interactif)
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// API pour l'agent GLPI (contact / inventory / deploy)
builder.Services.AddControllers();
builder.Services.AddScoped<InventoryImportService>();

// Base de données — SQLite par défaut en dev, configurable via appsettings
builder.Services.AddDbContext<GlpiNgDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Data Source=glpi-ng.db"));

var app = builder.Build();

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

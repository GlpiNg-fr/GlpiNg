# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

GlpiNg is a reimplementation of a GLPI-Agent-compatible IT asset management server
(.NET / Blazor Server). It exposes the GLPI-Agent protocol at `/inventory` so real GLPI-Agent
clients (contact, inventory, deploy) can talk to it, and provides a Blazor Server UI
for managing the asset park. The README (in French) is the canonical project
overview — read it for feature scope, current state, and known limitations.

## Commands

```bash
# Restore, build, run (from repo root or src/GlpiNg.Web)
dotnet restore
dotnet build GlpiNg.sln
cd src/GlpiNg.Web && dotnet run

# EF Core migrations (run from src/GlpiNg.Web)
dotnet ef migrations add <Name>
dotnet ef database update
```

There is no test project and no CI workflow in this repo currently — don't assume
`dotnet test` or a `.github/workflows` pipeline exists.

**Never overwrite or edit an existing EF Core migration** (including regenerating one
under the same name/timestamp after a model change). Always add a new migration
instead — existing migrations may already be applied to a real database, and
`GlpiNgDbContextModelSnapshot.cs` must stay in sync with the applied migration
history, not just the latest model.

## Architecture

**Solution layout**: `GlpiNg.Web` (host) + `GlpiNg.Modules.*` (feature modules), each
a plain class library referenced only by the host — never the reverse.

- `GlpiNg.Modules.Abstractions` — shared contracts modules use to plug into the host
  (currently `Menu.IMenuProvider` / `MenuGroup` / `MenuItem`).
- `GlpiNg.Modules.Inventory` — the asset park domain (Computer, ComputerComponent,
  GlpiAgent), the GLPI MySQL import (`Import/GlpiMySqlImportService`), and its own
  controllers. It only depends on the base EF Core `DbContext`, never on the host's
  concrete `GlpiNgDbContext` — see the comment in `GlpiNg.Modules.Inventory.csproj`.
- `GlpiNg.Web` — the host: Blazor UI, the `/inventory` protocol controller, the
  concrete `GlpiNgDbContext` (composed from module entities), auth, config, setup.

**Module registration pattern**: each module exposes a single `AddXxxModule(...)`
extension (e.g. `InventoryModuleServiceCollectionExtensions.AddInventoryModule`)
called from `Program.cs`. A module registers its own controllers via
`AddControllers().AddApplicationPart(...)` (its assembly isn't otherwise discovered)
and contributes to DI itself rather than being wired up by the host. A future
Tickets module would follow the same shape in `src/GlpiNg.Modules.Tickets/`.

**Menu contribution**: sidebar menu groups are not hardcoded in `MainLayout` —
modules contribute their own `IMenuProvider` (see `InventoryMenuProvider`,
registered as `AddSingleton<IMenuProvider, ...>`), and the host resolves
`IEnumerable<IMenuProvider>` and merges the groups. When adding a menu entry for a
module's own feature, add/extend that module's `IMenuProvider` rather than editing
`MainLayout` directly. `ModulesSettings` (keyed `"{groupKey}:{label}"`) tracks which
menu entries/module groups are toggled on/off from the `/config` UI; an entry absent
from the dictionary is treated as enabled.

**Startup gating in `Program.cs`** (order matters, see comments there):
1. `AnthoDingo.Setup`'s file-based first-run wizard (`/setup`) — GlpiNg restricts
   `AllowedProviders` to SQL Server, MySQL, Postgres (SQLite intentionally excluded).
   `GlpiNgDbContext` is only registered in DI once `Setup:IsComplete == true`.
2. `UseSetupMiddleware` — redirects everything to `/setup` until install completes.
3. `UseMigrationsGate` (`Middleware/MigrationsGateMiddleware`) — once installed, blocks
   all UI routes (except `/update`, `/inventory`, and Blazor/static asset paths) and
   redirects to `/update` while EF Core migrations are pending, so an admin can apply
   them explicitly rather than have them silently auto-applied.
4. Auth (cookie-based, `/login`) and authorization — `MapRazorComponents` calls
   `.RequireAuthorization()` globally; anonymous pages must opt out with
   `@attribute [AllowAnonymous]` (see `Login.razor`). `AgentController`
   (`/inventory`) and `/Account/Login|Logout` are excluded from the authorization
   requirement since GLPI-Agent clients don't carry an application session cookie.

**Configuration**: `appsettings.local.json` is written by the AnthoDingo.Setup wizard
post-install and takes priority over `appsettings.json`; it's gitignored and must
never be committed. Most `/config` settings sections (see `ConfigSections/*.razor`) are
stored in the `AppSettings` table (one row per section, JSON-serialized) and read/written
through `SettingsCacheService` — a singleton that keeps every section in an in-memory
cache, so reads never hit the database and a save updates the DB row and the cache
together, making the new value visible to every other service/page immediately without a
restart. On first read of a section not yet in the DB, it falls back to the matching
`appsettings.json` section (if still present) rather than losing an already-customized
value — see the fallback in `SettingsCacheService.ReadSectionAsync`. Only server listen
addresses (`Urls`) and Swagger enabled/disabled stay in `appsettings.json` via
`AppSettingsFileStore`: `Urls` is read by Kestrel at startup, before the app has DB
access, and needs a restart anyway; Swagger's toggle relies on
`IOptionsMonitor<SwaggerOptions>` + the file's `reloadOnChange` to flip without a
restart — it's `MapWhen`-mounted conditionally rather than statically registered.
Changes made from either store are tracked by `ConfigHistoryService`, called separately
by each Razor page after a successful save.

**GLPI MySQL import** (`GlpiMySqlImportService`, triggered via
`POST /admin/import/glpi`): read-only, idempotent import from an existing GLPI MySQL
database, keyed by `Computer.SourceGlpiId` so re-running updates rather than
duplicates. It detects which hardware-component columns exist via
`information_schema` and degrades gracefully rather than failing, because column
names vary across GLPI versions. See the README's "Import depuis une base GLPI
MySQL" section for exact coverage/limits — this endpoint currently has no
authentication guard.

**Deploy protocol**: the `/inventory` endpoint (`AgentController`) uses a single
POST route with an `action` field (`getJobs`, `setStatus`) rather than the
`?action=...` query-string style of the original GlpiInventory plugin — a deliberate
adaptation, not a certified reproduction of the wire protocol. `DeployJobJsonBuilder`
constructs the job JSON (`jobs.checks/associatedFiles/actions`, files keyed by SHA512
hash) served to agents; `GET /inventory/deploy/file/{sha512}` serves package files
by hash.

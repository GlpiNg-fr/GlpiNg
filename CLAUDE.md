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

- `GlpiNg.Modules.Abstractions` — shared contracts modules use to plug into the host:
  `Menu.IMenuProvider`, `Reports.IReportProvider` (a module contributes its own reports to
  `/tools/reports`; the host only renders tables and exports them), `Directory.IPrincipalDirectory`
  / `IPrincipalContextProvider` (entities/groups/profiles/users, and a user's habilitations, for
  modules that restrict an object's visibility), plus the older Deployment/Import/Preferences ones.
  `PrincipalContext.Matches` is the single place where « is this user targeted? » is decided,
  entity scope and recursion included; the host expands recursive habilitations into
  `EntityIds`/`EntityAncestorIds` since only it can read the entity tree.
- `GlpiNg.Modules.Inventory` — the asset park domain (Computer, ComputerComponent,
  GlpiAgent), the GLPI MySQL import (`Import/GlpiMySqlImportService`), and its own
  controllers. It only depends on the base EF Core `DbContext`, never on the host's
  concrete `GlpiNgDbContext` — see the comment in `GlpiNg.Modules.Inventory.csproj`.
- `GlpiNg.Modules.KnowledgeBase` — the knowledge base (articles, categories, revisions,
  visibility targets, per-article change history). The most self-contained module: it
  references `Abstractions` only. Visibility lives entirely in `KnowledgeBaseService.IsVisible`
  (author exception, then the `VisibleFrom`/`VisibleUntil` window, then targets) and is applied
  by both the list and the detail page — a new screen reading articles must call it too, or a
  pasted URL becomes a way around the targeting. Its
  Razor pages live in its own assembly, so they must be listed in `Routes.razor`'s
  `AdditionalAssemblies` — a module page that 404s is usually that line. Article content is
  **Markdown**, rendered only through `Services/MarkdownRenderer` (Markdig with `DisableHtml()`
  plus URL-scheme filtering): never render article content as `MarkupString` any other way, and
  never store HTML — that renderer is the single place where user-authored content becomes markup.
- `GlpiNg.Web` — the host: Blazor UI, the `/inventory` protocol controller, the
  concrete `GlpiNgDbContext` (composed from module entities), auth, config, setup. It also
  owns **Documents** (`Models/Documents`, `Services/Documents`, `/management/documents`), a
  GLPI-shaped global entity: files are attached to any item through the polymorphic
  `DocumentItem` (`ItemType` + `ItemId`, GLPI's own type names). Modules never touch that
  model — they go through `Abstractions/Documents/IDocumentAttachments`, the way the
  knowledge base's "Documents" tab does. Bytes live under `StoragePaths.Documents`, sharded
  by SHA-256, so identical content is stored once; `/documents/{id}/download` always serves
  `application/octet-stream` as an attachment, never the declared MIME type.

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
never be committed. It lives **inside the storage root** (`StoragePaths.Root` — `data/`
by default, or `Storage:RootPath`), next to `keys/` and `packages/`, so everything
install-specific sits in one backup-able place. That ordering constraint is load-bearing
in `Program.cs`: `StoragePaths` is built *before* the file is added to configuration, so
`Storage:RootPath` must come from `appsettings.json`, env, or CLI — never from the local
file, which would need the root to find the root. `EnsureLocalSettingsNotLeftBehind`
throws at startup if the file is still at the old path (beside the binary) and missing
from the new one, rather than silently redirecting to `/setup` over a populated database.
On Windows, `data` resolves onto the existing `Data/` source folder (case-insensitive FS),
so runtime files land in `src/GlpiNg.Web/Data/`; the `.gitignore` covers both spellings. Most `/config` settings sections (see `ConfigSections/*.razor`) are
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

**Collect task protocol**: a real GLPI-Agent's Collect task does *not* speak the single-POST
`action` convention. It calls `GET /inventory?action=getConfig`, reads `schedule[].remote`, then
hits that URL with `action=getJobs` (GET, query string) and `action=setAnswer` (POST,
`application/x-www-form-urlencoded`, one result per request) — via GLPI-Agent's "Fusion" HTTP
client, which sends **no `GLPI-Agent-ID` header**, so the agent is matched on `machineid` (its
`deviceid`). Jobs must carry a `uuid`; `_sid` is only echoed back when the job had one, so
`StoreCollectAnswersAsync` falls back to `uuid`. The older `getCollectJobs`/`setCollectAnswer`
actions are kept but no real agent emits them. Note the server cannot enable the task remotely:
`tasks` in a contact answer only records server-side support, and a task missing from the agent's
own `tasks`/`no-task` config never runs.

**Loading a Computer with its collections**: always go through
`InventoryImportService.WithInventoryCollections` (or copy its `AsSplitQuery()`), never a bare
chain of collection `Include`s. EF Core emits a single SQL joining every included collection, so
the row count is their *product* — on an ordinary desktop (840 softwares × ~100 components ×
15 peripherals × volumes × ports) that is hundreds of millions of rows and the query never
returns. The agent then gives up after its 180-second read timeout and logs
`[http client] internal response: 500 read timeout`, which looks like a server 500 but is
synthesised client-side: nothing was ever returned. `Computers/Detail.razor.cs` already splits;
the import did not, and `/inventory` hung on every real machine.

**GLPI HTML → Markdown** (`Import/GlpiHtmlToMarkdown`): GLPI often stores knowledge-base
answers **HTML-escaped** (`&lt;p&gt;`), which a converter reads as plain text — the article
then shows every tag, since `MarkdownRenderer` never interprets HTML. `Unescape` undoes that
first, but only while the content carries *no* real tag, so an article demonstrating
`&lt;VirtualHost&gt;` keeps its example. Unknown tags use `Bypass` (drop the tag, keep the
content) rather than `Drop`, which silently swallowed anything wrapped in `<font>` or Word's
`<o:p>`; `script`/`style`/`iframe` are excluded with their content instead.

**GLPI text encoding**: many GLPI installs declare columns `latin1` while storing UTF-8,
so imported text arrives as "ProcÃ©dure". Every string read from a GLPI database should go
through `Import/GlpiText.Repair` (already wired into the knowledge-base import's
`GetNullableString`). It reverses the decode via **Windows-1252** — MySQL's `latin1` is
cp1252, which is where `’`, `€` and the dashes live — and only rewrites when the recovered
bytes are valid UTF-8, so correct text is left alone. The admin and inventory importers
read strings inline and are not yet wired to it.

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

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
# Every GlpiNg.Modules.* project and the plugin SDK is a git submodule (own AGPL-3.0 repo under GlpiNg-fr)
git submodule update --init

# Restore, build, run (from repo root or src/GlpiNg.Web)
dotnet restore
dotnet build GlpiNg.sln
cd src/GlpiNg.Web && dotnet run   # launchSettings passes `serve`
```

`GlpiNg.Web` builds as `glping(.exe)`, which is **both the server and the admin CLI**
(`Cli/`, Spectre.Console.Cli): no argument → help, `glping serve` → server, `db:install`,
`db:check`, `user:*` → CLI. `Program.Main` routes `serve` straight to `RunServer` (Spectre would
swallow `--urls` & co.), and also starts the server without `serve` under `dotnet ef`
(`EF.IsDesignTime`) and under IIS (`ASPNETCORE_IIS_PHYSICAL_PATH`, set by ANCM) — the shipped
`web.config` passes `arguments="serve"` anyway. Docker: `ENTRYPOINT glping.dll`, `CMD ["serve"]`.
The CLI reads config from the current directory, like the server's content root — and **only** from
there: it takes `Storage:RootPath` from `./appsettings.json`, never from a `Storage__RootPath` env var.
Run from `src/GlpiNg.Web`, it therefore hits whatever database `Data/appsettings.local.json` points at
(a real one on a dev machine). To target a test install, run it from a folder whose `appsettings.json`
sets `Storage:RootPath` to that install.

```bash
# EF Core migrations (run from src/GlpiNg.Web)
dotnet ef migrations add <Name>
dotnet ef database update
```

There is no test project — don't assume `dotnet test` exists. `.github/workflows` has `ci.yml`
(Release build with submodules, vulnerable-package audit, and `dotnet ef migrations
has-pending-model-changes` against a simulated install), `codeql.yml` (whose results the `dev/main`
ruleset requires, so direct pushes there are refused — go through a PR) and `release.yml` (a version
tag such as `1.0.0-RC1` publishes one `glping` zip per RID (linux-x64/win-x64) as a GitHub release).

**EF Core migrations — one rule per branch.** On `dev/main`, add them freely: one per change,
named after the change. On `main`, a version ships with **a single migration covering everything
since the previous version** — cutting a release squashes `dev/main`'s migrations into one
regenerated from the model and named after the version (`1.0.0-RC1`).

**Never edit a migration that has already applied anywhere**, and never regenerate one under a
name/timestamp already in use: it may be applied to a real database. On `dev/main` that means
adding a new migration rather than amending the last one. The release squash is the only
exception, and only because it happens before that version exists anywhere.

Squashing has three traps, all found the hard way:
- A regenerated migration carries **schema only** — every `InsertData` is lost. Fold the seeds
  back into `Up()` by hand, or a fresh install comes up with no root entity, no profile, and an
  API that refuses every call for want of an API client.
- A seed written before a column existed omits it. That column arrived later via an `AddColumn`
  carrying a `defaultValue`; a table created in one block has no such default, so every `NOT NULL`
  column without one must now be supplied explicitly (this broke a preprod install on
  `Entities.TwoFactorAuthRequired`).
- The migration id must keep a 14-character prefix (`00000000000000_1.0.0-RC1`). EF's scaffolder
  does `lastMigrationId.Substring(15)`, so a bare `1.0.0-RC1` breaks `migrations add`, `list` and
  `remove` outright.

Prove a squash by running `dotnet ef migrations script` against an **empty** database rather than
by reading the diff — `sqlcmd -S "(localdb)\MSSQLLocalDB" -I -i script.sql`, where `-I` is
required or the filtered indexes are rejected. `GlpiNgDbContextModelSnapshot.cs` must stay in sync
with the applied migration history, not just the latest model; CI checks it with
`migrations has-pending-model-changes`.

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

**Plugins** (`GlpiNg.Plugins.Sdk`, packed to NuGet together with `GlpiNg.Modules.Abstractions`, its
dependency): a plugin is a class library with one public `IGlpiNgPlugin`. `Services/Plugins/PluginRegistry`
loads `{StoragePaths.Plugins}/{Name}/{Name}.dll` at startup (setup complete only, after `Build`) and
**at runtime** — `/config` → Plugins installs a `dotnet publish` zip, enables/disables (persisted in
`plugins/disabled.json`, a file so startup never needs the DB), reloads, deletes (issue #12). Rules that
keep hot loading working:
- Each plugin gets its own **collectible** `PluginLoadContext`, loaded from a **shadow copy**
  (`plugins/.loaded/`, purged at startup) — Windows locks a loaded DLL, and the copy is what lets an
  update replace the folder. Anything in the host's TPA list resolves to the default context, so shared
  contracts unify; plugins still reference the SDK with `ExcludeAssets="runtime"`.
- `ConfigureServices` fills a **private** `ServiceCollection`, served by `PluginServiceProvider` (plugin
  first, then the current host scope) through the scoped `PluginScopes`. Only descriptors whose
  implementation comes from the plugin are kept; framework ones, keyed, open generics and
  `IHostedService` are dropped and listed as warnings.
- Contributions reach the host through `IEnumerable<T>` for `IMenuProvider`/`IReportProvider`/`ICronTask`
  only: `AddPluginHost` (called after every module) moves the host's registrations under a key and
  re-registers `IEnumerable<T>` explicitly — the container prefers an exact registration — so consumers
  are untouched. A new contribution contract goes in `PluginServiceCollectionExtensions.Contributions`.
  A consumer that must see changes live re-resolves on `PluginRegistry.Changed` (see `MainLayout`).
- Plugin components and controllers are built by `PluginComponentActivator`/`PluginControllerActivator`
  with the plugin's services — **constructor injection**. Before .NET 11, `@inject` only sees host
  services (warned at load); `#if NET11_0_OR_GREATER` code (`IComponentPropertyActivator`) opens it.
- Pages must live under `/plugins/` and not repeat an existing route (a duplicate breaks the Router
  for everyone). URL entry hits the host's catch-all `Pages/Plugins/PluginFallback` (`/plugins/{*Path}`),
  since `MapRazorComponents` assemblies are fixed at startup; once interactive, `Routes.razor`'s Router —
  fed `PluginRegistry.Assemblies` and re-rendered on `Changed` — picks the plugin page.
- `wwwroot/` is served at `/_content/{AssemblyName}/` by `PluginStaticFileProvider`; only that folder.
- Unloading is logical: memory comes back only at restart once a plugin page was rendered — Blazor
  caches component types statically and clears them only under hot reload. `RetainedInMemory` reports it.
A plugin outside `MinimumHostVersion`/`MaximumHostVersion`, or any unloadable DLL, is **skipped, not
fatal**; the reason shows in `/config` → Plugins and Système. No DB tables for plugins yet.

**GLPI REST APIs** (`GlpiNg.Web/Api`, mapped in `Program.cs` with `MapMethods` on `/apirest.php`,
`/api/` → v1 `Legacy/LegacyApiHandler`, and `/api.php` → v2 `HighLevel/HighLevelApiHandler`, v1 when
prefixed `/v1`). Both run on **virtual GLPI tables** (`Api/Glpi/Data`): `GlpiTableCatalog.*.cs`
declares, per GLPI table, which model property feeds which GLPI column (`Col`, `Fk` 0↔null, `Entity`
root↔0, `Named` for dropdowns GlpiNg stores as text, `Computed` for in-memory values, `UnionGlpiTable`
when GLPI has rows GlpiNg keeps as fields). Undeclared tables are valid but empty; undeclared columns
render GLPI's default. `EfGlpiTable` pushes filters/sort/paging to SQL when every column involved has an
expression, else evaluates in memory — keep big tables translatable. GLPI metadata (columns, search
options, v2 schemas with `x-field`/`x-join`, OpenAPI) is **generated, never hand-edited**: rerun
`tools/glpi-api-metadata/generate.php <glpi release> src/GlpiNg.Web/Api/Glpi/Metadata`. Auth:
`GlpiApiAccess` checks API clients (IP + App-Token hash), opens a `GlpiApiSession`, and attaches it to
`HttpContext.Items` — `EntityScopeProvider`/`ProfileRightsProvider` read it first, so every EF context
and service is scoped to the session's active entities and profile.

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
value — see the fallback in `SettingsCacheService.ReadSectionAsync`. Every save raises
`SettingsCacheService.SectionSaved` (the service is a singleton shared by all Blazor circuits):
`MainLayout` rebuilds its side menu on `ModulesSettings`, and the module cards of `/config` refresh
their switches, so enabling/disabling a module or menu entry shows up live for every connected user.
Subscribers must go through `InvokeAsync` and unsubscribe in `Dispose`. A section edited piecemeal
by several screens (`ModulesSettings`) must be changed with `UpdateSectionAsync(name, mutate)` — it
re-reads the latest value under a lock — never by saving a copy read earlier, which would silently
undo a toggle made meanwhile by another card or another admin. Only server listen
addresses (`Urls`) and Swagger enabled/disabled stay in `appsettings.json` via
`AppSettingsFileStore`: `Urls` is read by Kestrel at startup, before the app has DB
access, and needs a restart anyway; Swagger's toggle relies on
`IOptionsMonitor<SwaggerOptions>` + the file's `reloadOnChange` to flip without a
restart — it's `MapWhen`-mounted conditionally rather than statically registered.
Changes made from either store are tracked by `ConfigHistoryService`, called separately
by each Razor page after a successful save.

**Notes** (`Models/Notes/Notepad`, `Services/Notes/NoteService`, `Abstractions/Notes/IItemNotes`):
GLPI's notepad, polymorphic over `ItemType` + `ItemId` (names from `Abstractions/Items/ItemTypes`,
the single source shared with documents). Wired to the Computer and knowledge-base article pages.
`Notepad` deliberately does **not** implement `IEntityScoped` — see the attachment-visibility rule
below, which it follows for the same reason. Entity and group notes still live in their own
pre-existing tables (`GlpiEntityNote`, `GlpiGroupNote`); converging them is a follow-up.

**Displaying dates**: never format a date by hand (`.ToLocalTime().ToString("dd/MM/yyyy")`) —
it would ignore the user's date-format and time-zone preferences. Every component has
`Display` injected (`UserDisplay`, see the `_Imports.razor` files): `Display.DateTime(x)` /
`Display.Date(x)` for instants stored in UTC (converted to the user's zone),
`Display.LocalDateTime(x)` / `Display.LocalDate(x)` for wall-clock values typed into a
`datetime-local`/`date` input (due dates, task planning, contract dates) — those are stored as-is
and must never be converted. History and timelines go through `Display.Chronological`. Outside
components, take `UserPreferenceValues` from `IUserPreferences`. History *texts* written at save
time stay in a fixed format on purpose (shared by all readers).

**Translations (i18n)**: gettext-style — the **French text is the key**. Write user-facing text as
`@T("Enregistrer")` in markup (`using static ...Localization.Tr` is in every `_Imports.razor`),
`Tr.T("...")` in C#, and `T("Supprimer {0} élément(s) ?", count)` with values (never a `$"..."`
interpolation: it can't be a key). Catalogs are `src/GlpiNg.Web/i18n/{lang}.json` (French → translation,
one per language, `en`/`pt` shared by their variants); a missing key just shows the French, so a new
text works immediately and only needs adding to the catalogs. `CurrentUICulture` is set per request and
per circuit by `UserPreferencesPreloader`, so a language change needs a page reload (`forceLoad`).
Rules that matter:
- Never call `T` in a **static** initializer (`static readonly` list/dictionary): it would freeze the
  language of the first reader. Keep such catalogs French and translate at display time instead —
  `@T(tab.Label)`, `@T(column.Label)`, `@T(report.Title)`. Don't do both (translate in code *and* at
  display): a translation that happens to equal another French key would be translated twice.
- Never translate what is **stored**: history texts (`Track`, `AddHistory`, `Field =`), log/audit
  messages, default setting values, names given to created records. They are read by every user in
  whatever language; they stay French.
- Keys, CSS classes, enum/ItemType names and anything compared in code stay untranslated.
- In a generic component (`@typeparam T`), `T(...)` is the type parameter — use `Tr.T(...)`.
- Day/month names follow `CultureInfo.CurrentUICulture`; numbers and date *formats* follow the user's
  own preferences (see "Displaying dates"), not the language.

**Naming (front end)**: our own CSS classes, custom properties, `data-*` attributes, DOM ids and the
JS global are prefixed `glping` (`glping-fiche`, `--glping-surface`, `data-glping-theme`,
`window.glping`), never `glpi` — the project avoids using the GLPI name for its own identifiers.
Names that belong to GLPI itself stay as they are, because they are wire or storage contracts:
GLPI database tables (`glpi_users`...), the GLPI-Agent protocol (`GLPI-Agent-ID`, `_glpi_csrf_token`)
and links to GLPI's sources.

**Colors / themes**: `glping-theme.css` must not hard-code neutral colors. Backgrounds, borders and
text go through the tokens at the top of the file (`--glping-surface`, `--glping-surface-2/3`,
`--glping-border(-strong)`, `--glping-text`, `--glping-text-2`, `--glping-text-muted`, `--glping-text-faint`),
primary-as-text through `--glping-primary-text`, and text on a primary background through
`--tblr-primary-fg` — several GLPI palettes have a *light* primary. A hard-coded `#fff` background
is what breaks the dark palettes (Darker, Midnight), which `App.razor` switches on via
`data-bs-theme="dark"`.

**Attachment visibility**: a document's own entity scope must not gate the *attachment list* of
an item — `DocumentService.GetForItemAsync` (and the fallback in `GetAsync`) deliberately
`IgnoreQueryFilters()`. The authorization boundary is the item: the knowledge-base page already
checks `IsVisible` on the article before rendering the tab. Without this, a non-recursive document
in an ancestor entity vanished from the tab of an article that was itself visible because GLPI
marks articles recursive and documents not — and it vanished silently, the join dropping the whole
row. A document attached to nothing stays scoped, so `/management/documents` still honours the
entity perimeter.

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

## graphify

This project has a knowledge graph at graphify-out/ with god nodes, community structure, and cross-file relationships.

Rules:
- For codebase questions, first run `graphify query "<question>"` when graphify-out/graph.json exists. Use `graphify path "<A>" "<B>"` for relationships and `graphify explain "<concept>"` for focused concepts. These return a scoped subgraph, usually much smaller than GRAPH_REPORT.md or raw grep output.
- If graphify-out/wiki/index.md exists, use it for broad navigation instead of raw source browsing.
- Read graphify-out/GRAPH_REPORT.md only for broad architecture review or when query/path/explain do not surface enough context.
- After modifying code, run `graphify update .` to keep the graph current (AST-only, no API cost).

# GlpiNg

Réimplémentation d'un serveur de gestion de parc informatique compatible avec le
protocole **GLPI-Agent**, en .NET / Blazor Server. GlpiNg expose l'endpoint
`/glpi-agent` afin que de vrais agents GLPI-Agent (contact, inventory, deploy)
puissent dialoguer avec lui, et fournit une UI Blazor Server pour administrer le
parc — avec une sidebar qui reprend la structure de navigation de GLPI (Parc,
Assistance, Gestion, Outils, Administration, Configuration).

## Objectifs

- Inventaire de parc (postes, composants, statuts, affectations) alimenté par de
  vrais agents GLPI-Agent
- Compatibilité protocole GLPI-Agent : `contact`, `inventory`, `getJobs`/`setStatus`
  (déploiement de paquets)
- Import (lecture seule, idempotent) depuis une base GLPI MySQL existante
- Administration : utilisateurs, groupes, entités, profils, sources
  d'authentification (LDAP), clients OAuth2
- Assistant d'installation intégré (`/setup`) et gestion des migrations EF Core
  depuis l'UI (`/update`)

## Stack

- .NET 10 / Blazor Server (Interactive Server Components) pour `GlpiNg.Web`
  (le module `GlpiNg.Modules.Inventory` cible encore .NET 8, cf. son `.csproj`)
- EF Core — SQL Server, MySQL ou PostgreSQL (SQLite volontairement exclu, y
  compris de l'assistant d'installation)
- Authentification par cookie (`/login`), avec bind LDAP optionnel et
  provisionnement automatique des comptes externes
- Émission de jetons OAuth2 (`client_credentials` / `password`) pour l'accès
  machine-à-machine à l'API
- Swagger/OpenAPI, activable à chaud depuis `/config`

## Structure

```
GlpiNg.sln
src/
  GlpiNg.Web/                       # Hôte : UI Blazor, protocole agent, auth, DbContext, setup
    Components/Pages/               # Pages Blazor : Computers, Users, Groups, Entities,
                                     #   Profiles, Authentication (Ldap/Mail), Config(Sections),
                                     #   Login, Update, Agents, Monitors, Software...
    Controllers/                    # AgentController (/glpi-agent), AccountController (login),
                                     #   OAuthController (/oauth2/token)
    Middleware/                     # MigrationsGateMiddleware (redirige vers /update)
    Data/                           # GlpiNgDbContext (composé à partir des modules)
    Models/                         # Entités hôte : GlpiUser, GlpiGroup, GlpiEntity, GlpiProfile,
                                     #   AuthLdapServer/AuthMailServer, OAuthClient, *Settings...
    Models/Agent/                   # Domaine Deploy (DeploymentJob/Package/File) + payload inventory
    Services/                       # UserCredentialAuthenticator, LdapAuthenticationService,
                                     #   OAuthTokenIssuer, AppSettingsFileStore, InventoryImportService...
    Migrations/                     # Migrations EF Core (SQL Server)
  GlpiNg.Modules.Inventory/         # Module Inventory (parc, agents GLPI, import MySQL)
    Models/                         # Computer, ComputerComponent, GlpiAgent
    Import/                         # Import depuis une base GLPI MySQL source
    Controllers/                    # POST /admin/import/glpi (protégé OAuth2)
    InventoryMenuProvider.cs        # Contribue les groupes "Parc" et "Outils" à la sidebar
  GlpiNg.Modules.Abstractions/      # Contrats de contribution (IMenuProvider / MenuGroup / MenuItem)
```

Les modules (`GlpiNg.Modules.*`) sont des bibliothèques de classes autonomes,
référencées uniquement par `GlpiNg.Web` (jamais l'inverse) : chacune expose une
extension `AddXxxModule(...)` appelée depuis `Program.cs`, enregistre ses propres
contrôleurs (`AddApplicationPart`) et ne dépend que du `DbContext` EF Core de base
(pas du `GlpiNgDbContext` concret de l'hôte). Un module contribue aussi son propre
menu sidebar via `IMenuProvider` plutôt que de faire modifier `MainLayout`
directement par le code hôte. Un futur module Tickets suivrait le même schéma dans
`src/GlpiNg.Modules.Tickets/`.

## État actuel

### Démarrage et installation

- Assistant d'installation intégré (`/setup`, fourni par `AnthoDingo.Setup`) :
  choix du SGBD (SQL Server, MySQL ou PostgreSQL), création du schéma et du
  premier compte administrateur. Tant que l'installation n'est pas terminée,
  toute requête est redirigée vers `/setup`.
- Une fois installé, `GlpiNgDbContext` n'est enregistré qu'à ce moment-là ; un
  garde de migrations (`MigrationsGateMiddleware`) redirige ensuite vers
  `/update` tant que des migrations EF Core sont en attente, plutôt que de les
  appliquer automatiquement.
- Serveur de secours optionnel (`ConnectionStrings:FallbackConnection`) : si le
  serveur principal n'est pas joignable au démarrage, `GlpiNgDbContext` bascule
  dessus automatiquement.

### Authentification et accès

- Connexion par cookie (`/login`), avec un profil admin créé par l'assistant
  d'installation.
- Sources d'authentification externes : annuaires LDAP (CRUD sous
  `/authentication`), avec bind LDAP au login et provisionnement automatique
  des comptes externes si activé (`AuthSettings.AutoAddUsersFromExternalAuth`).
  Un compte peut être forcé sur une source précise (local ou un annuaire donné)
  depuis le sélecteur de la page de login.
- Émission de jetons OAuth2 (`POST /oauth2/token`) pour des clients gérés
  depuis `/oauth-clients` — grants `client_credentials` et `password`,
  restriction par IP, scopes (`api`, `inventory`, ...).
- `POST /admin/import/glpi` est protégé par la policy `OAuthApiAccess` (jeton
  Bearer avec le scope `api` ou `inventory`) plutôt que par la session cookie
  applicative, pour permettre un appel machine-à-machine.

### Administration

- Gestion CRUD des utilisateurs (`/users`), groupes (`/groups`, hiérarchie
  parent/enfants), entités (`/entities`, hiérarchie + adresses), profils
  (`/profiles`) — avec historique des modifications par entité.
- Page `/config` : sections mirroir de la configuration générale GLPI
  (Général, Assistance, Gestion, Parc, Sécurité, API, Système, Purge,
  Colonnes d'affichage, Valeurs par défaut, ...), avec suivi d'historique des
  changements (`ConfigHistoryService`).
- La sidebar reprend l'arborescence complète de menu de GLPI ; seules les
  entrées avec une route (Ordinateurs, Moniteurs, Logiciels, Agents,
  Utilisateurs, Groupes, Entités, Profils, Authentification, Générale, ...)
  sont implémentées, les autres apparaissent en placeholder.

### Protocole GLPI-Agent (`/glpi-agent`)

Endpoint POST unique dispatché sur un champ `action` :

- `contact` : enregistre/rafraîchit l'agent et lui signale les jobs de
  déploiement en attente
- `inventory` : importe le payload d'inventaire (hardware/composants/logiciels/
  réseau) envoyé par l'agent
- `getJobs` : renvoie le prochain job de déploiement au format JSON GLPI-Agent
  (`jobs.checks/associatedFiles/actions`, fichiers indexés par hash SHA512)
- `setStatus` : rapport d'avancement/résultat d'un job par l'agent
- `GET /glpi-agent/deploy/file/{sha512}` : téléchargement d'un fichier de
  package par son hash

Le PROLOG XML historique (probe FusionInventory/OCS) est accepté en entrée et
répondu en JSON pour faire basculer l'agent sur le protocole natif. Les corps
compressés zlib/gzip (Content-Type ou Content-Encoding) sont décompressés
automatiquement. Non couvert : brotli, chiffrement (`GLPI-CryptoKey-ID`), proxy
agent (`GLPI-Proxy-ID`).

Le routage (un seul POST `/glpi-agent` + champ `action`, plutôt que les
endpoints `?action=...` à base de query-string du plugin GlpiInventory) est une
adaptation propre à ce projet, pas une reproduction certifiée du protocole
d'origine — à valider face à un agent réel avant mise en production.

### Import depuis une base GLPI MySQL

`POST /admin/import/glpi` déclenche un import (lecture seule) depuis une base
GLPI MySQL existante, configurée via `GlpiImport:ConnectionString` (appsettings
ou user-secrets — ne pas committer d'identifiants réels) :

```json
{ "GlpiImport": { "ConnectionString": "Server=host;Database=glpi;User=ro_user;Password=***" } }
```

L'import est idempotent : chaque poste est rattaché à son `glpi_computers.id`
d'origine (`Computer.SourceGlpiId`), donc relancer l'import met à jour les
postes existants au lieu de les dupliquer. Un historique des imports
(`ComputerImportHistory`) et des modifications par poste
(`ComputerHistoryEntry`) est conservé.

**Couvert** : postes (`glpi_computers`), fabricant/modèle/type/état/emplacement,
système d'exploitation, agents (`glpi_agents`, rattachement par
`itemtype='Computer'`), composants matériels CPU/RAM/disques/cartes réseau,
logiciels, périphériques, volumes, batteries.

**Non couvert / limites connues** :
- Un seul compte utilisateur MySQL en lecture seule est supposé ; aucune
  écriture n'est faite sur la base GLPI source.
- Les composants matériels (nom exact des colonnes fréquence/capacité) varient
  selon la version de GLPI installée : l'import détecte les colonnes présentes
  via `information_schema` et se dégrade proprement (valeur vide) plutôt que
  d'échouer, mais le résultat peut donc être incomplet selon la version source.
- La correspondance état GLPI → `ComputerStatus` est une heuristique par
  mots-clés (les noms d'état sont libres dans GLPI) : à vérifier sur un
  premier import.
- L'emplacement GLPI (`locations_id`) est mappé sur un seul champ `Site` ;
  `Building`/`Room` ne sont pas déduits de la hiérarchie d'emplacement.

## À faire

- [ ] Parsing exhaustif du payload `inventory` (couverture complète
      hardware/software/réseau selon les versions d'agent)
- [ ] Stockage réel des fichiers de package (upload, découpage en fragments)
- [ ] Application effective des droits par profil (`ProfileRightLevel` existe
      mais n'est pas encore vérifié à l'exécution)
- [ ] Migrations EF Core dédiées pour MySQL/PostgreSQL (l'installation utilise
      `EnsureCreatedAsync` pour ces providers, donc `dotnet ef database update`
      n'y est pas encore utilisable pour les évolutions de schéma futures)
- [ ] Pages restantes de la sidebar (Assistance, Gestion, Règles,
      Dictionnaires, Notifications, Webhooks, ...) : entrées de menu présentes
      mais sans route pour l'instant
- [ ] Compression brotli et chiffrement (`GLPI-CryptoKey-ID`) côté protocole agent

## Démarrage

```bash
# Restore, build (depuis la racine du repo)
dotnet restore
dotnet build GlpiNg.sln

# Lancer l'application (ouvre l'assistant /setup au premier démarrage)
cd src/GlpiNg.Web
dotnet run
```

```bash
# EF Core (depuis src/GlpiNg.Web) — voir CLAUDE.md : ne jamais éditer une
# migration existante, toujours en ajouter une nouvelle
dotnet ef migrations add <Name>
dotnet ef database update
```

Il n'y a pas de projet de tests ni de pipeline CI dans ce repo actuellement.
`appsettings.local.json` (écrit par l'assistant d'installation, gitignored) a
priorité sur `appsettings.json` une fois l'installation terminée et ne doit
jamais être committé.

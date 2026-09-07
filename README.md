# GlpiNg

Réimplémentation d'un serveur de gestion de parc informatique compatible avec le
protocole **GLPI-Agent**, en .NET / Blazor Server. GlpiNg expose l'endpoint
`/glpi-agent` afin que de vrais agents GLPI-Agent (contact, inventory, deploy)
puissent dialoguer avec lui, et fournit une UI Blazor Server pour administrer le
parc — avec une sidebar qui reprend la structure de navigation de GLPI (Parc,
Assistance, Gestion, Outils, Administration, Configuration).

## Objectifs

- Inventaire de parc (postes, composants, statuts, affectations) alimenté par de
  vrais agents GLPI-Agent, étendu à 14 types d'actifs
- Compatibilité protocole GLPI-Agent : `contact`, `inventory`, `getJobs`/`setStatus`
  (déploiement de paquets)
- Déploiement de paquets, découverte/inventaire réseau SNMP et Wake-on-LAN,
  planifiés par tâches et créneaux horaires
- Import (lecture seule, idempotent) depuis une base GLPI MySQL existante
- Administration : utilisateurs, groupes, entités, profils, sources
  d'authentification (LDAP), clients OAuth2
- Moteur de règles et dictionnaires (ordinateurs, import, intitulés)
- Notifications par courriel (gabarits, règles, file d'attente) et actions
  automatiques (cron applicatif)
- Assistant d'installation intégré (`/setup`) et gestion des migrations EF Core
  depuis l'UI (`/update`), doublés d'une CLI d'administration (`GlpiNg.Console`)

## Stack

- .NET 10 / Blazor Server (Interactive Server Components) pour `GlpiNg.Web` ;
  tous les modules ciblent également .NET 10
- EF Core — SQL Server, MySQL ou PostgreSQL (SQLite volontairement exclu, y
  compris de l'assistant d'installation)
- CLI d'administration `GlpiNg.Console` (Spectre.Console.Cli) : installation et
  vérification de la base, gestion des comptes
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
    Components/Pages/               # Pages Blazor hôte : Users, Groups, Entities, Profiles,
                                     #   Authentication (Ldap/Mail), Config(Sections), Notifications
                                     #   (Templates/Rules/Queue), AutomaticActions, OAuthClients,
                                     #   Logs, MyAccount, Login, Update, Home (tableau de bord)
    Controllers/                    # AgentController (/glpi-agent), AccountController (login),
                                     #   OAuthController (/oauth2/token)
    Middleware/                     # MigrationsGateMiddleware (redirige vers /update)
    Data/                           # GlpiNgDbContext (composé à partir des modules)
    Models/                         # Entités hôte : GlpiUser, GlpiGroup, GlpiEntity, GlpiProfile,
                                     #   AuthLdapServer/AuthMailServer, OAuthClient, Notifications, *Settings...
    Models/Agent/                   # Payloads envoyés par l'agent (InventoryPayload, NetworkInventoryPayload)
    Import/                         # Import "Administration" GLPI (entités/groupes/profils/utilisateurs)
    Services/                       # UserCredentialAuthenticator, LdapAuthenticationService,
                                     #   OAuthTokenIssuer, SettingsCacheService, AppSettingsFileStore,
                                     #   InventoryImportService, Notifications/...
    Migrations/                     # Migrations EF Core (SQL Server)
  GlpiNg.Modules.Inventory/         # Module Inventory (parc, agents GLPI, règles, import MySQL)
    Models/                         # Computer, ComputerComponent, GlpiAgent, Monitor/Printer/Phone/
                                     #   Rack/Pdu/Cable/... , DropdownItem, ComputerRule, DictionaryRule,
                                     #   ImportAssignmentRule, SavedSearch
    Import/                         # Import depuis une base GLPI MySQL source
    Controllers/                    # POST /admin/import/glpi (protégé OAuth2)
    InventoryMenuProvider.cs        # Contribue "Parc", "Outils", "Administration", "Configuration"
  GlpiNg.Modules.Deployment/        # Module Déploiement + réseau
    Models/                         # DeploymentPackage/Job/Task, DeployComputerGroup, DeploymentRule,
                                     #   TimeSlot, DeploymentMirrorServer, CollectDefinition, IpRange,
                                     #   SnmpCredential, NetworkTask, DiscoveredNetworkDevice, WakeOnLanTask
    Services/                       # DeployJobJsonBuilder, DeploymentRuleEngine, DeployGroupCriteriaEvaluator,
                                     #   DeploymentPackageFileStorageService, *LaunchService...
    Controllers/                    # Upload fragmenté des fichiers de paquet
  GlpiNg.Modules.Cron/              # Ordonnanceur applicatif : CronBackgroundService + AutomaticActionRunner
  GlpiNg.Modules.Scheduler/         # Tâches cron qui déclenchent déploiements / tâches réseau / WoL
  GlpiNg.Modules.Abstractions/      # Contrats de contribution : Menu (IMenuProvider / MenuGroup /
                                     #   MenuItem), Cron (ICronTask), Deployment (IDeploymentTargetDirectory,
                                     #   ICurrentUserDeploymentContextProvider, ...), Import
  GlpiNg.Console/                   # CLI d'administration (db:install, db:check, user:*)
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
  `/config/auth`), avec bind LDAP au login et provisionnement automatique
  des comptes externes si activé (`AuthSettings.AutoAddUsersFromExternalAuth`).
  Un compte peut être forcé sur une source précise (local ou un annuaire donné)
  depuis le sélecteur de la page de login.
- Émission de jetons OAuth2 (`POST /oauth2/token`) pour des clients gérés
  depuis `/config/oauth-clients` — grants `client_credentials` et `password`,
  restriction par IP, scopes (`api`, `inventory`, ...).
- `POST /admin/import/glpi` est protégé par la policy `OAuthApiAccess` (jeton
  Bearer avec le scope `api` ou `inventory`) plutôt que par la session cookie
  applicative, pour permettre un appel machine-à-machine.

### Administration

- Gestion CRUD des utilisateurs (`/admin/users`), groupes (`/admin/groups`,
  hiérarchie parent/enfants), entités (`/admin/entities`, hiérarchie + adresses),
  profils (`/admin/profiles`) — avec historique des modifications par fiche.
- Journaux applicatifs (`/admin/logs`) et préférences de compte
  (`/mon-profil`, `/preferences`).
- Page `/config` : sections mirroir de la configuration générale GLPI
  (Général, Assistance, Gestion, Parc, Sécurité, API, Système, Purge,
  Colonnes d'affichage, Valeurs par défaut, ...), avec suivi d'historique des
  changements (`ConfigHistoryService`).
- Intitulés (`/config/dropdowns`) et composants (`/config/components`), pilotés
  par catalogue de types (`DropdownTypeCatalog`).
- La sidebar reprend l'arborescence complète de menu de GLPI ; seules les
  entrées avec une route sont implémentées, les autres apparaissent en
  placeholder — voir « Écarts avec GLPI » plus bas. `ModulesSettings` permet en
  plus d'activer/désactiver chaque entrée depuis `/config`.

### Parc

Fiches liste + détail pour : ordinateurs (`/parc/computer`), moniteurs,
logiciels, matériels réseau, périphériques, imprimantes, cartouches,
consommables, téléphones, baies, châssis, PDU, équipements passifs, câbles,
plus une vue globale (`/parc/allassets`). Les fiches ordinateurs exposent
composants, logiciels, volumes, batteries, ports réseau, antivirus, historique
des modifications et historique d'import.

### Déploiement et réseau (`/tools/deployments`)

- Paquets et jobs de déploiement, fichiers stockés et servis par hash SHA512,
  upload fragmenté (`DeploymentPackageFilesController`).
- Tâches de déploiement planifiées, groupes d'ordinateurs dynamiques (critères
  évalués par `DeployGroupCriteriaEvaluator`), règles (`DeploymentRuleEngine`),
  créneaux horaires, serveurs miroirs, gabarits d'interaction utilisateur.
- Découverte et inventaire réseau SNMP : plages IP, identifiants SNMP, tâches
  réseau, équipements découverts.
- Tâches Wake-on-LAN et supervision des agents (`/tools/deployments/supervision`).
- Les cibles « libre-service » (entité/groupe/profil/utilisateur) se configurent
  déjà sur la fiche paquet, mais la page `/self-service` correspondante n'existe
  pas encore.

### Règles, dictionnaires et recherches

- Règles ordinateurs (`/admin/rules`), dictionnaires (`/admin/dictionaries`,
  `DictionaryRuleEngine`), règles d'import/affectation avec liste noire et
  journal des imports refusés (`/admin/import-rules`).
- Recherches sauvegardées (`/tools/saved-searches`) et préférences de colonnes
  par table (`TableColumnPreference`).

### Notifications et actions automatiques

- Notifications par courriel : gabarits avec balises substituables
  (`##computer.name##`, ...), règles de déclenchement par événement
  (`NotificationEventCatalog` : inventaire, agent, job de déploiement, tâche
  réseau, équipement découvert, WoL), file d'attente consultable
  (`/config/notifications/queue`) et envoi SMTP (`SmtpMailSender`).
- Cron applicatif (`GlpiNg.Modules.Cron`) piloté depuis
  `/config/automatic-actions` : purge d'historique, envoi de la file de
  notifications, nettoyage des agents, déclenchement des tâches de déploiement,
  réseau et Wake-on-LAN.

### CLI d'administration (`GlpiNg.Console`)

`db:install`, `db:check --fix`, `user:create`, `user:resetpassword`,
`user:enable`, `user:disable` — utile quand l'UI n'est pas accessible (base à
initialiser, mot de passe admin perdu).

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

La page `/admin/import/glpi` (ou `POST /admin/import/glpi`) déclenche un import
(lecture seule) depuis une base GLPI MySQL existante, configurée via
`GlpiImport:ConnectionString` (appsettings ou user-secrets — ne pas committer
d'identifiants réels) :

```json
{ "GlpiImport": { "ConnectionString": "Server=host;Database=glpi;User=ro_user;Password=***" } }
```

L'import est idempotent : chaque poste est rattaché à son `glpi_computers.id`
d'origine (`Computer.SourceGlpiId`), donc relancer l'import met à jour les
postes existants au lieu de les dupliquer. Un historique des imports
(`ComputerImportHistory`) et des modifications par poste
(`ComputerHistoryEntry`) est conservé.

**Couvert (parc)** : postes (`glpi_computers`),
fabricant/modèle/type/état/emplacement, système d'exploitation, agents
(`glpi_agents`, rattachement par `itemtype='Computer'`), composants matériels
CPU/RAM/disques/cartes réseau, logiciels, périphériques, volumes, batteries.

**Couvert (administration)** — `GlpiAdminMySqlImportService`, sélectionnable
depuis la page d'import : entités et groupes (hiérarchies reconstruites en deux
passes), profils, utilisateurs, habilitations (`glpi_profiles_users`),
appartenances aux groupes (`glpi_groups_users`), et un sous-ensemble de la
configuration générale (`glpi_configs`, contexte `core`).

**Non couvert / limites connues** :
- Un seul compte utilisateur MySQL en lecture seule est supposé ; aucune
  écriture n'est faite sur la base GLPI source.
- Les droits fins par module d'un profil GLPI (`glpi_profilerights`) ne sont pas
  traduits vers les 6 `ProfileRightLevel` de GlpiNg : seuls nom, commentaire et
  statut par défaut sont importés, les droits sont à reconfigurer à la main.
- Les mots de passe ne sont pas repris (hash GLPI incompatible) : un compte
  nouvellement importé reçoit un mot de passe aléatoire inutilisable, à
  réinitialiser depuis `/admin/users` ou à déléguer à une source
  d'authentification externe. Un compte déjà importé conserve son mot de passe
  GlpiNg existant.
- Les composants matériels (nom exact des colonnes fréquence/capacité) varient
  selon la version de GLPI installée : l'import détecte les colonnes présentes
  via `information_schema` et se dégrade proprement (valeur vide) plutôt que
  d'échouer, mais le résultat peut donc être incomplet selon la version source.
- La correspondance état GLPI → `ComputerStatus` est une heuristique par
  mots-clés (les noms d'état sont libres dans GLPI) : à vérifier sur un
  premier import.
- L'emplacement GLPI (`locations_id`) est mappé sur un seul champ `Site` ;
  `Building`/`Room` ne sont pas déduits de la hiérarchie d'emplacement.

## Écarts avec GLPI

GlpiNg couvre aujourd'hui le périmètre « gestion de parc + déploiement ». Les
manques suivants sont connus et assumés à ce stade.

### Modules fonctionnels absents

- **Assistance** (groupe entier) : aucun modèle `Ticket`/`Problem`/`Change`
  n'existe. Donc pas de tickets, problèmes, changements, planning,
  statistiques, suivis/tâches, catégories ITIL, gabarits de tickets, enquêtes
  de satisfaction.
- **Gestion** (groupe entier) : licences, budgets, fournisseurs, contacts,
  contrats, documents, lignes téléphoniques, certificats, data centers,
  clusters, domaines, applicatifs, bases de données.
- **Outils** : réservations, rapports, base de connaissances (et, absents même
  de la sidebar : projets, rappels, flux RSS).
- **Parc** : actifs non gérés, cartes SIM.
- **Administration** : formulaires.
- **Configuration** : actifs personnalisés, webhooks, niveaux de services
  (SLA/OLA), unicité des champs, collecteurs, liens externes, plugins.

À noter : les sections `Assistance`, `Helpdesk` et `Analyse d'impact` de
`/config` configurent des fonctionnalités qui n'existent pas encore.

### Manques transverses

- **Droits par profil non appliqués** — `ProfileRightLevel` est stocké,
  éditable et importé, mais aucune page ne porte `[Authorize(...)]` et rien ne
  le vérifie à l'exécution : tout utilisateur authentifié accède à tout. C'est
  le point le plus sensible de cette liste.
- **Pas de cloisonnement par entité** — les actifs n'ont pas d'`EntityId` ;
  seules quelques entités hôte y font référence. Ni isolation des données, ni
  récursivité, ni sélecteur d'entité active : les entités ne sont pour l'instant
  qu'un annuaire.
- **Pas d'API REST générique** — rien d'équivalent à `apirest.php` (CRUD et
  recherche par itemtype). Les seuls endpoints exposés sont `/glpi-agent`,
  `/oauth2/token`, `/admin/import/glpi` et l'upload de fichiers de paquet.
- **Pas de gestion documentaire** — aucune entité `Document`, donc pas de
  pièces jointes sur les fiches.
- **Pas d'internationalisation** — aucun `.resx` ni `IStringLocalizer`, l'UI est
  en français en dur.
- **Pas d'actions massives**, et export limité à la page Ordinateurs
  (`ComputerExportWriter`) au lieu d'un export générique sur toutes les listes.
- **2FA inerte** — `GlpiEntity.TwoFactorAuthRequired` et
  `GlpiUser.TwoFactorAuthDisabled` sont stockés et éditables, mais il n'y a ni
  TOTP, ni enrôlement, ni vérification au login.
- **Pas de système de plugins** — l'architecture `GlpiNg.Modules.*` est interne
  et compilée, sans chargement à chaud ni marketplace.
- **Moteur de règles partiel** — absents : règles d'habilitations LDAP, règles
  métier tickets, règles d'affectation d'entité, règles de localisation.
- **Recherche** — les recherches sauvegardées existent, mais pas le moteur de
  recherche multi-critères générique qui les alimente dans GLPI.

## À faire

- [ ] Application effective des droits par profil (`ProfileRightLevel`)
- [ ] Parsing exhaustif du payload `inventory` (couverture complète
      hardware/software/réseau selon les versions d'agent)
- [ ] Page `/self-service` (les cibles libre-service sont déjà configurables
      sur la fiche paquet)
- [ ] Migrations EF Core dédiées pour MySQL/PostgreSQL (l'installation utilise
      `EnsureCreatedAsync` pour ces providers, donc `dotnet ef database update`
      n'y est pas encore utilisable pour les évolutions de schéma futures)
- [ ] Compression brotli et chiffrement (`GLPI-CryptoKey-ID`) côté protocole agent
- [ ] Pages restantes de la sidebar (voir « Écarts avec GLPI » ci-dessus)

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

```bash
# CLI d'administration (depuis src/GlpiNg.Console) — alternative à l'UI
dotnet run -- db:install --provider SqlServer --connection-string "..."
dotnet run -- db:check --fix
dotnet run -- user:resetpassword admin
```

Il n'y a pas de projet de tests ni de pipeline CI dans ce repo actuellement.
`appsettings.local.json` (écrit par l'assistant d'installation, gitignored) a
priorité sur `appsettings.json` une fois l'installation terminée et ne doit
jamais être committé.

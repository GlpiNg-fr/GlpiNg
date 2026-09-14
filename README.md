# GlpiNg

Réimplémentation d'un serveur de gestion de parc informatique compatible avec le
protocole **GLPI-Agent**, en .NET / Blazor Server. GlpiNg expose l'endpoint
`/inventory` afin que de vrais agents GLPI-Agent (contact, inventory, deploy)
puissent dialoguer avec lui, et fournit une UI Blazor Server pour administrer le
parc — avec une sidebar qui reprend la structure de navigation de GLPI (Parc,
Assistance, Gestion, Outils, Administration, Configuration).

## Objectifs

- Inventaire de parc (postes, composants, statuts, affectations) alimenté par de
  vrais agents GLPI-Agent, étendu à 15 types d'actifs
- Compatibilité protocole GLPI-Agent : `contact`, `inventory`, `getJobs`/`setStatus`,
  collectes (`getCollectJobs`/`setCollectAnswer`)
  (déploiement de paquets)
- Déploiement de paquets, découverte/inventaire réseau SNMP et Wake-on-LAN,
  planifiés par tâches et créneaux horaires
- Import (lecture seule, idempotent) depuis une base GLPI MySQL existante
- Administration : utilisateurs, groupes, entités, profils, sources
  d'authentification (LDAP), clients OAuth2
- Moteur de règles et dictionnaires (ordinateurs, import, intitulés)
- Notifications par courriel (gabarits, règles, file d'attente), webhooks
  sortants signés, et actions automatiques (cron applicatif)
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
    Controllers/                    # AgentController (/inventory), AccountController (login),
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
    Reports/                        # Rapports de parc, logiciels, réseau et fraîcheur d'inventaire
    InventoryMenuProvider.cs        # Contribue "Parc", "Outils", "Administration", "Configuration"
  GlpiNg.Modules.Deployment/        # Module Déploiement + réseau
    Models/                         # DeploymentPackage/Job/Task, DeployComputerGroup, DeploymentRule,
                                     #   TimeSlot, DeploymentMirrorServer, CollectDefinition, IpRange,
                                     #   SnmpCredential, NetworkTask, DiscoveredNetworkDevice, WakeOnLanTask
    Services/                       # DeployJobJsonBuilder, DeploymentRuleEngine, DeployGroupCriteriaEvaluator,
                                     #   DeploymentPackageFileStorageService, *LaunchService...
    Controllers/                    # Upload fragmenté des fichiers de paquet
    Reports/                        # Rapports déploiements / tâches / équipements découverts
  GlpiNg.Modules.KnowledgeBase/     # Module Base de connaissances (articles, catégories, révisions)
    Models/                         # KnowledgeBaseArticle/Category/ArticleRevision/ArticleTarget
    Services/                       # KnowledgeBaseService (révisions, cibles, vues), CategoryTree,
                                     #   MarkdownRenderer (rendu sûr du Markdown des articles)
    Components/Pages/KnowledgeBase/ # Consultation, fiche d'article, gestion des catégories
    Reports/                        # Rapport « Base de connaissances »
  GlpiNg.Modules.Cron/              # Ordonnanceur applicatif : CronBackgroundService + AutomaticActionRunner
  GlpiNg.Modules.Scheduler/         # Tâches cron qui déclenchent déploiements / tâches réseau / WoL
  GlpiNg.Modules.Abstractions/      # Contrats de contribution : Menu (IMenuProvider / MenuGroup /
                                     #   MenuItem), Reports (IReportProvider / ReportDefinition /
                                     #   ReportResult), Directory (IPrincipalDirectory /
                                     #   IPrincipalContextProvider), Cron (ICronTask), Deployment (IDeploymentTargetDirectory,
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
- Préférences personnelles (`/preferences`) : repli du menu latéral, nombre
  d'éléments par page dans les listes, et format d'affichage des adresses MAC
  (laissé sur « Réglage de l'instance », il suit « Affichage adresse MAC » de la
  configuration générale). Contrat `IUserPreferences` côté `Abstractions`, pour
  que les pages des modules les consultent sans connaître le modèle de compte.
- Sources d'authentification externes : annuaires LDAP (CRUD sous
  `/config/auth`), avec bind LDAP au login et provisionnement automatique
  des comptes externes si activé (`AuthSettings.AutoAddUsersFromExternalAuth`).
  Un compte peut être forcé sur une source précise (local ou un annuaire donné)
  depuis le sélecteur de la page de login.
- La fiche d'un annuaire a quatre onglets, calqués sur GLPI :
  **Annuaire LDAP** (connexion), **Utilisateurs** (correspondance attribut LDAP →
  champ du compte, recopiée à chaque connexion), **Groupes** (synchronisation des
  appartenances, recherche dans les utilisateurs, dans les groupes ou les deux) et
  **Informations avancées** (STARTTLS, délai d'attente, taille de page, nombre
  maximum de résultats, TAG d'entité des comptes provisionnés).
- Émission de jetons OAuth2 (`POST /oauth2/token`) pour des clients gérés
  depuis `/config/oauth-clients` — grants `client_credentials` et `password`,
  restriction par IP, scopes (`api`, `inventory`, ...).
- `POST /admin/import/glpi` est protégé par la policy `OAuthApiAccess` (jeton
  Bearer avec le scope `api` ou `inventory`) plutôt que par la session cookie
  applicative, pour permettre un appel machine-à-machine.

### Administration

- Gestion CRUD des utilisateurs (`/admin/users`), avec « Ajout depuis une source
  externe » dans le menu du bouton d'ajout : recherche dans un annuaire LDAP et
  import des comptes choisis, sans attendre leur première connexion ni activer le
  provisionnement automatique.
- Gestion CRUD des groupes (`/admin/groups`,
  hiérarchie parent/enfants), entités (`/admin/entities`, hiérarchie + adresses),
  profils (`/admin/profiles`) — avec historique des modifications par fiche.
- Journaux applicatifs (`/admin/logs`) et préférences de compte
  (`/mon-profil`, `/preferences`).
- Emplacement de stockage des fichiers (`/config`, onglet Système) : racine
  unique sous laquelle vivent les paquets de déploiement et les clés de
  chiffrement (secrets des annuaires LDAP). Vide = dossier `data` à la racine du
  programme. Stocké dans `appsettings.json` et non en base, comme `Urls` : les
  clés sont configurées au démarrage, avant tout accès à la base. Nécessite un
  redémarrage ; les chemins effectivement retenus sont affichés dans l'écran.
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
cartes SIM (ICCID, ligne, PIN/PUK, opérateur), plus une vue globale
(`/parc/allassets`). Les actifs non gérés (`/parc/unmanaged`) exposent côté parc
les équipements remontés par la découverte réseau, avec conversion en matériel
réseau, imprimante ou téléphone. Les fiches ordinateurs exposent
composants (dont le BIOS, en composant Firmware comme dans GLPI), logiciels,
volumes, batteries, ports réseau, connecteurs, antivirus,
historique des modifications et historique d'import. L'onglet « Connecteurs »
reprend la section `ports` de l'inventaire — les prises physiques du châssis (USB,
série, vidéo...), à distinguer de « Connexions » (ce qui y est branché) et de
« Ports réseau » (les interfaces IP configurées).

Une imprimante porte ses paramètres d'**interrogation SNMP** : adresse IP, port,
et version (aucune / v1 / v2c / v3). Le formulaire suit la version choisie —
communauté en v1 et v2c, nom de sécurité puis authentification et chiffrement en
v3, ce dernier n'apparaîtssant qu'une fois l'authentification choisie (SNMPv3 n'a
pas de mode chiffré sans authentification). Changer de version efface ce qui n'a
plus cours, pour ne pas laisser un secret oublié en base. Ce qui manque est
signalé sans empêcher d'enregistrer, et les phrases secrètes n'entrent dans
l'historique que sous forme de « renseignée ou non ».

La fiche d'une imprimante a un onglet **Cartouches** : celles en service, avec le
lien vers leur référence et la date de pose, celles retirées avec leur durée de
vie, et l'installation d'une unité en stock. Seules les références ayant une
unité disponible sont proposées, et c'est la plus anciennement reçue qui part la
première. Chaque pose et chaque retrait sont tracés des deux côtés — historique
de l'imprimante et de la référence.

Une référence de cartouche porte un **OID SNMP** optionnel, adresse à laquelle une
imprimante expose le niveau restant de cette cartouche (norme
`prtMarkerSuppliesLevel`, `1.3.6.1.2.1.43.11.1.1.9.1.n`). Sa syntaxe est vérifiée à la saisie, et une
référence sans OID n'est simplement pas relevée.

La liste des imprimantes se déplie, comme celle des logiciels : chaque ligne
affiche le nombre de cartouches en service et le **plus bas** de leurs niveaux —
c'est lui qui commande, une imprimante n'étant utilisable que jusqu'à ce que sa
cartouche la plus basse soit vide — et s'ouvre sur le détail de chacune avec son
niveau et la date de son relevé.

L'action automatique **« Relevé SNMP des imprimantes »**
(`printer_snmp_poll`, 12 h par défaut) interroge les imprimantes dont la fiche
porte une adresse et une version, et met à jour leur compteur de pages
(`prtMarkerLifeCount`, standardisé) ainsi que le niveau des cartouches
installées. Le pourcentage est dérivé en lisant la capacité maximale au même rang
(colonne 8 au lieu de 9 du même sous-arbre), ce qui évite un second OID à saisir ;
à défaut, une valeur inférieure à 100 est prise pour un pourcentage et les autres
sont abandonnées. Les valeurs réservées de la norme (-1 « inconnu », -2 « sans
limite », -3 « il en reste ») ne sont pas enregistrées comme des niveaux. Une
imprimante injoignable est ignorée et réessayée au cycle suivant, jamais au prix
du relevé des autres.

Le bouton « Actions » de la fiche ordinateur permet de réveiller le poste
(Wake-on-LAN) : le serveur diffuse un magic packet sur **toutes** les adresses MAC
connues du poste, en broadcast limité et — quand l'inventaire connaît l'IP et le
masque de l'interface — en broadcast dirigé vers son sous-réseau, sur les ports
UDP 9 et 7. C'est un envoi immédiat et sans relais, mais un routeur ne fait
normalement pas traverser un broadcast : pour un poste hors du segment du
serveur, ce sont les tâches Wake-on-LAN (`/tools/deployments`), relayées par un
agent sur place, qui restent la voie fiable. Le détail de l'envoi (MAC visées,
adresses de diffusion) est affiché, un magic packet ne donnant aucun accusé de
réception ; la demande est tracée dans l'historique du poste.

Chaque champ de la fiche ordinateur alimenté par l'inventaire porte un cadenas
(équivalent de `glpi_lockedfields`) : verrouillé, il n'est plus réécrit par les
remontées d'agent, et la valeur saisie à la main fait foi. Le cadenas reste
visible même déverrouillé — c'est ce qui fait savoir qu'un champ est alimenté par
l'agent, donc qu'une correction manuelle y serait écrasée sans lui. Chaque
bascule est tracée dans l'historique du poste. L'onglet « Verrous » de la fiche
en donne la vue d'ensemble : tous les champs verrouillables, leur valeur
courante, leur état, qui a posé le verrou et depuis quand — les champs
verrouillables que la fiche n'affiche pas (domaine, prise en main à distance) n'y
sont accessibles que là.

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

### Base de connaissances (`/tools/knowledgebase`)

Module à part entière (`GlpiNg.Modules.KnowledgeBase`), équivalent de « Outils > Base de
connaissances » de GLPI.

- **Articles** : sujet, contenu, catégorie, drapeau FAQ, épinglage. Recherche plein
  texte sur le sujet et le contenu, tri par date, par consultations ou alphabétique,
  les articles épinglés restant en tête quel que soit le tri.
- **Éditeur Markdown** : barre d'outils (gras, italique, barré, titre, listes, cases à
  cocher, citation, code en ligne et bloc, lien, image, tableau, séparateur) agissant sur
  la sélection courante, et bascule « Rédaction / Aperçu ». Le rendu est fait par Markdig
  avec le HTML brut désactivé — un `<script>` collé dans un article s'affiche comme du
  texte — et les schémas d'URL dangereux (`javascript:`, `data:`, `vbscript:`, `file:`)
  sont neutralisés avant rendu, comme pour les liens externes. Un retour à la ligne simple
  vaut saut de ligne, pour que les articles tapés au fil de l'eau s'affichent tels quels.
- **Catégories** arborescentes (`/tools/knowledgebase/categories`) : création,
  renommage, déplacement, suppression. Supprimer une catégorie ne supprime rien de ce
  qu'elle rangeait — sous-catégories et articles sont rattachés à sa catégorie parente.
- **Révisions** : chaque modification du sujet ou du contenu archive l'état antérieur,
  consultable et restaurable depuis la fiche. Une modification qui ne touche qu'un
  drapeau ou la catégorie n'en crée pas.
- **Cibles de visibilité** (entité, groupe, profil, utilisateur) : sans cible, l'article
  est visible de tous ceux qui accèdent à la base ; avec des cibles, seuls les acteurs
  visés — et l'auteur — y ont accès, y compris par URL directe. Le cloisonnement par
  entité s'applique en amont, comme partout.
- **Compteur de consultations**, comptées une fois par lecteur et par article sur une
  fenêtre de 30 minutes : sans cela, le double rendu de Blazor Server (pré-rendu puis
  circuit) et le moindre aller-retour d'onglet gonfleraient le compteur, et « les
  articles les plus consultés » ne voudrait plus rien dire.
- Un **rapport** « Base de connaissances » (voir ci-dessous) montre les articles par
  catégorie, les plus consultés et ceux que personne n'ouvre.

Limites connues : l'éditeur est un éditeur **Markdown** (barre d'outils et aperçu), pas
un WYSIWYG, et le HTML n'y est jamais interprété ; il n'y a **ni commentaires, ni pièces
jointes, ni corbeille** (la suppression d'un article est définitive, après confirmation)
— les images doivent donc être hébergées ailleurs et référencées par URL, faute de
gestion documentaire (voir « Manques transverses ») ; et le drapeau FAQ sert de filtre et
de marquage éditorial plutôt que de publication vers une interface simplifiée, qui
n'existe pas encore.

### Rapports (`/tools/reports`)

Équivalent de « Outils > Rapports » de GLPI. La page liste les rapports par
catégorie ; chacun s'ouvre sur `/tools/reports/{clé}`, se génère dès l'ouverture
avec ses filtres par défaut, et s'exporte en CSV, XLSX, ODS ou PDF
(paysage/portrait). Ce qu'un rapport compte est ce que celui qui le demande a le
droit de voir : le cloisonnement par entité s'applique comme partout ailleurs, et
les postes en corbeille sont exclus.

Aucun rapport n'est codé dans l'hôte : chaque module contribue les siens via
`IReportProvider` (`GlpiNg.Modules.Abstractions/Reports`), comme il contribue ses
entrées de menu via `IMenuProvider`. L'hôte (`ReportCatalog` et les pages
`Components/Pages/Reports`) ne manipule que des tableaux de texte — un rapport
nouveau n'a donc rien à ajouter côté hôte, et un module désactivé emporte les
siens avec lui.

Rapports du module Inventory :

- **Rapport par défaut** — actifs par type, puis par statut.
- **État des matériels** — tableau croisé type d'actif × statut.
- **Matériels par lieu** — répartition géographique, ordinateurs / moniteurs /
  imprimantes / matériels réseau / autres.
- **Matériel par fabricant et modèle** — filtrable par type d'actif et par
  fabricant.
- **Entrées dans le parc par année** — filtrable sur une période.
- **Logiciels installés** — versions, installations, part des postes ; filtrable
  par éditeur et par nombre minimal d'installations.
- **Systèmes d'exploitation** — postes par OS, puis par version.
- **Rapport réseau** — matériels réseau par type, interfaces des postes par
  sous-réseau (interfaces virtuelles exclues par défaut).
- **Fraîcheur des inventaires** — ancienneté des remontées par tranches, postes
  silencieux et agents sans contact au-delà d'un seuil réglable.

Rapports du module Déploiement :

- **État des déploiements** — jobs par paquet (en attente / en cours / succès /
  erreur) et derniers échecs, sur une période.
- **Tâches de déploiement** — paquets, cibles et résultats de chaque tâche.
- **Équipements découverts** — découvertes réseau par statut, par type deviné, et
  celles qui restent à traiter.

Limites connues : les rapports de contrats, de licences et financiers de GLPI
n'ont pas d'équivalent, faute des modèles correspondants ; « Entrées dans le parc
par année » s'appuie sur la date d'entrée en base (`CreatedAt`) et non sur une
date d'achat, qui n'existe pas ici ; les tableaux de détail sont plafonnés à 200
lignes, leur titre indiquant alors le nombre total de correspondances ; les
cartouches et consommables, qui sont des références de stock et non des actifs
installés, ne sont comptés dans aucun rapport de parc.

### Cloisonnement par entité

Reprend la règle de visibilité de GLPI : un objet est visible si son entité fait partie des
entités visibles, ou s'il est marqué « visible dans les sous-entités » et rattaché à l'une de
leurs entités parentes.

- 35 types d'objets portent `EntityId` / `IsRecursive` via `IEntityScoped`
  (`GlpiNg.Modules.Abstractions/Entities`) : les 15 types d'actifs, les agents, les
  intitulés, les règles et dictionnaires, les recherches sauvegardées, l'ensemble
  des objets de déploiement et réseau, les groupes et les notifications.
- Le filtrage n'est pas écrit page par page : `GlpiNgDbContext` pose un filtre
  global sur chaque type implémentant l'interface, paramétré par le
  `EntityScope` dont la fabrique de contextes estampille chaque instance
  (`EntityScopedDbContextFactory`). Les ~97 fichiers de pages sont donc
  cloisonnés sans modification, et un accès direct par URL à un objet hors
  périmètre se comporte comme un objet inexistant.
- L'entité active est portée par le cookie d'authentification et se change
  depuis le sélecteur du bandeau (`/Account/SwitchEntity`), avec l'option
  « voir les sous-entités ». La cible est revalidée côté serveur contre les
  habilitations de l'utilisateur.
- Les objets créés sont rattachés automatiquement à l'entité active
  (`GlpiNgDbContext.SaveChanges`), et le rattachement est affiché et modifiable
  sur les fiches d'actifs (`EntityScopeFields`), dans la limite des entités
  visibles par l'utilisateur.
- Les postes remontés par un agent, qui arrivent hors session, sont rattachés par
  correspondance entre le TAG de l'agent et le TAG d'affectation d'une entité
  (`GlpiEntity.AssignmentTag`), et déposés dans l'entité racine à défaut. Un
  poste resté dans la racine est repris automatiquement si un TAG correspond
  plus tard ; un rattachement décidé manuellement n'est jamais écrasé.
- Supprimer une entité encore occupée est refusé avec le détail de ce qui bloque
  (`EntityDeletionGuard`) plutôt qu'avec une violation de clé étrangère brute.
- Hors session applicative — protocole agent, tâches cron, import
  machine-à-machine par jeton OAuth, services singleton via
  `IRootDbContextFactory` — le cloisonnement est neutre : ces chemins voient
  tout. Un compte administrateur (`GlpiUser.IsAdmin`) n'est pas cloisonné non
  plus ; un utilisateur connecté sans aucune habilitation ne voit rien.

### Droits par profil

Un profil porte un droit par grande section du menu (Parc, Assistance, Gestion, Outils,
Administration, Configuration), à trois niveaux : aucun, lecture, écriture. C'est une
simplification assumée de la matrice très fine de GLPI.

- Les droits sont résolus **pour l'entité active** — une habilitation associe un
  profil à une entité, donc changer d'entité active recalcule les droits — puis
  portés par le cookie d'authentification (`ProfileRightsService`).
- Trois points d'application, sur une table de correspondance unique
  (`ProfileSectionMap`) : le menu masque les groupes non lisibles,
  `SectionAccessMiddleware` refuse l'accès direct par URL, et `MainLayout` fait
  de même pour la navigation interne au circuit Blazor, que le middleware ne
  voit pas passer.
- L'écriture est refusée au niveau du `GlpiNgDbContext`
  (`EnforceWriteRights`) : la section est déduite du type d'objet, ce qui couvre
  d'un coup les quelque 40 formulaires sans en modifier un seul. C'est un filet
  de second rang — la protection de premier rang reste le garde-fou de routes.
  Sont exemptés les objets écrits par le système au nom de l'utilisateur
  (préférences d'affichage, journal d'événements, file de notifications), sans
  quoi un utilisateur sans droit d'administration ne pourrait ni se déconnecter
  ni réorganiser son tableau de bord.
- Comme pour le cloisonnement, les chemins hors session applicative (protocole
  agent, cron, import par jeton OAuth) et les comptes `IsAdmin` ne sont soumis à
  aucun de ces contrôles.

### Règles, dictionnaires et recherches

- Règles ordinateurs (`/admin/rules`) : critères sur les champs texte, sur
  l'ancienneté du dernier inventaire (« remonte à plus de N heures ») et actions
  sur le statut. Une règle peut s'appliquer à l'ajout, à la mise à jour et/ou à
  l'exécution périodique — ce dernier moment est indispensable aux conditions
  d'ancienneté, qu'un inventaire entrant ne peut jamais vérifier.
- Catalogue de composants (`/config/components`) : alimenté par les inventaires,
  une entrée par modèle distinct rencontré dans le parc (processeurs, mémoires,
  disques, cartes...). Un bouton « Reconstruire depuis le parc » rattrape les
  modèles remontés par les inventaires antérieurs, sans attendre que chaque poste
  repasse ; il n'ajoute que ce qui manque.
- Dictionnaires (`/admin/dictionaries`,
  `DictionaryRuleEngine`), règles d'import/affectation avec liste noire et
  journal des imports refusés (`/admin/import-rules`).
- Recherche multi-critères sur toutes les listes du parc : critères enchaînés par
  ET/OU, opérateurs par type de champ (texte, nombre, date), tris multiples.
  Moteur et panneaux partagés (`Search/SearchEngine`, `Components/Search`) —
  une liste n'a qu'à déclarer ses champs interrogeables.
- Recherches sauvegardées (`/tools/saved-searches`) et préférences de colonnes
  par table (`TableColumnPreference`).

### Notifications et actions automatiques

- Notifications par courriel : gabarits avec balises substituables
  (`##computer.name##`, ...), règles de déclenchement par événement
  (`NotificationEventCatalog` : inventaire, agent, job de déploiement, tâche
  réseau, équipement découvert, WoL), file d'attente consultable
  (`/config/notifications/queue`) et envoi SMTP (`SmtpMailSender`).
- Webhooks sortants (`/config/webhooks`) : mêmes couples type/événement que les
  notifications, avec URL, verbe HTTP, corps JSON par défaut ou personnalisé
  (balises `##cle##`), en-têtes supplémentaires, secret partagé et file de
  livraison consultable (`/config/webhooks/queue`) avec reprise et purge. Les
  appels signés portent `X-GLPI-signature` (HMAC-SHA256 hexadécimal du corps
  concaténé à l'horodatage) et `X-GLPI-timestamp`, comme GLPI. Un bouton
  « Tester » envoie un appel réel — valeurs d'exemple préfixées `TEST`, pour
  qu'un destinataire qui agit sur ce qu'il reçoit puisse le distinguer d'un vrai
  événement.
- Liens externes (`/config/external-links`) : une URL portant des balises
  (`[NAME]`, `[IP]`, `[SERIAL]`, ...) remplacées par les valeurs de la fiche,
  associée à un ou plusieurs types d'actifs, et affichée dans l'onglet « Liens
  externes » de leur fiche — pour pointer une supervision, une prise en main ou
  un wiki sur le bon élément. L'écran de configuration montre les balises que
  chaque type fournit réellement, signale celles qu'il ne fournit pas, et donne
  un aperçu de l'URL rendue.
- Statut par défaut d'un poste créé par un inventaire, choisi dans les intitulés
  (`Administration > Inventaire`) : appliqué à la création seulement, les
  inventaires suivants ne touchant plus au statut.
- Cron applicatif (`GlpiNg.Modules.Cron`) piloté depuis
  `/config/automatic-actions` : purge d'historique, envoi de la file de
  notifications, envoi et purge de la file des webhooks, nettoyage des agents,
  déclenchement des tâches de déploiement, réseau et Wake-on-LAN.

### CLI d'administration (`GlpiNg.Console`)

`db:install`, `db:check --fix`, `user:create`, `user:resetpassword`,
`user:enable`, `user:disable` — utile quand l'UI n'est pas accessible (base à
initialiser, mot de passe admin perdu).

### Protocole GLPI-Agent (`/inventory`)

Endpoint POST unique dispatché sur un champ `action` :

- `contact` : enregistre/rafraîchit l'agent et lui signale les jobs de
  déploiement en attente
- `inventory` : importe le payload d'inventaire envoyé par l'agent — matériel,
  BIOS, système, processeurs, mémoire, disques, volumes, cartes réseau,
  logiciels, écrans, batteries, périphériques USB, périphériques d'entrée,
  antivirus, cartes graphiques, contrôleurs, cartes son, modems, cartes SIM,
  prise en main à distance et TAG d'entité (`accountinfo`)
- `getJobs` : renvoie le prochain job de déploiement au format JSON GLPI-Agent
  (`jobs.checks/associatedFiles/actions`, fichiers indexés par hash SHA512)
- `setStatus` : rapport d'avancement/résultat d'un job par l'agent
- `getCollectJobs` : renvoie les collectes actives à exécuter (`getFromRegistry`,
  `getFromWMI`, `findFile`), chacune portant un `_sid` qui identifie l'entrée
- `setCollectAnswer` : enregistre les valeurs rapportées, visibles dans l'onglet
  « Informations de collecte » de la fiche du poste
- `GET /inventory/deploy/file/{sha512}` : téléchargement d'un fichier de
  package par son hash

Le PROLOG XML historique (probe FusionInventory/OCS) est accepté en entrée et
répondu en JSON pour faire basculer l'agent sur le protocole natif. Les corps
compressés zlib/gzip (Content-Type ou Content-Encoding) sont décompressés
automatiquement. Non couvert : brotli, chiffrement (`GLPI-CryptoKey-ID`), proxy
agent (`GLPI-Proxy-ID`).

Le routage (un seul POST `/inventory` + champ `action`, plutôt que les
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

**Couvert (base de connaissances)** — `GlpiKnowledgeBaseImportService`, onglet
« Base de connaissances » de la page d'import : catégories
(`glpi_knowbaseitemcategories`, arborescence reconstruite en deux passes comme
les entités), articles (`glpi_knowbaseitems` : sujet, contenu, FAQ, compteur de
consultations, auteur, dates), cibles de visibilité (`glpi_knowbaseitems_users`
/`_groups`/`_profiles`/`_entities`) et révisions
(`glpi_knowbaseitems_revisions`, GLPI ≥ 9.2). Idempotent par `SourceGlpiId` sur
la catégorie et sur l'article.

Deux points à connaître avant de le lancer :

- **Le contenu change de format.** GLPI stocke la réponse d'un article en HTML,
  GlpiNg stocke du Markdown et n'interprète jamais de HTML : la conversion est
  faite à l'import (ReverseMarkdown, balises inconnues supprimées). Une mise en
  forme complexe peut s'en trouver simplifiée.
- **L'ordre compte.** L'import de la base de connaissances passe après celui de
  l'administration, dont il dépend : sans les entités, groupes, profils et
  comptes, l'auteur d'un article n'est pas retrouvé et ses cibles de visibilité
  sont ignorées — l'article arrive alors visible de tous, et le nombre de cibles
  ignorées est affiché en fin d'import pour que ça ne passe pas inaperçu.
  Le rattachement à la catégorie est lu depuis la colonne
  `knowbaseitemcategories_id` ou, sur GLPI 10, depuis la table de liaison
  `glpi_knowbaseitems_knowbaseitemcategories`.

L'import affiche une barre de progression : le plan est bâti au lancement depuis
les volumes annoncés par l'analyse, si bien qu'une phase de dix mille ordinateurs
et une de trois profils n'avancent pas d'autant. Les phases du parc et du plugin
rapportent élément par élément, celles de l'administration à chaque entrée de
phase (leur volume ne le justifie pas). Le téléchargement du contenu d'un fichier
de paquet, qui peut durer des minutes sans qu'aucun élément ne s'achève, affiche
en plus le nom du fichier en cours. Le rafraîchissement est étranglé à quelques
images par seconde pour ne pas engorger le circuit Blazor — sauf un changement de
phase ou de fichier, toujours affiché immédiatement.

**Détection du plugin d'inventaire** : l'analyse signale la présence de GLPI
Inventory (`glpiinventory`) ou de son ancêtre FusionInventory, avec sa version,
son état et le volume de ce qu'il contient (paquets de déploiement, tâches,
agents, plages IP, identifiants SNMP, actifs non gérés). Croisement de
`glpi_plugins` — qui donne nom et version mais survit à une désinstallation — et
de la présence réelle des tables, découvertes par préfixe plutôt que listées en
dur, leurs noms ayant changé d'une version à l'autre.

La sélection est répartie en trois onglets — Parc, Administration, Plugin
d'inventaire, soit les trois services d'import — dont chaque titre porte un
interrupteur qui active ou désactive tout son contenu, et un compteur
« sélectionnés / disponibles ». Une catégorie absente de la base source reste hors
du compte comme hors de la bascule.

L'administrateur choisit ensuite ce qu'il souhaite reprendre parmi les plages IP,
les identifiants SNMP, les paquets de déploiement et les actifs non gérés. Rien
n'est coché par défaut : ces données doublonnent un domaine que GlpiNg gère en
propre, donc la reprise se demande explicitement. Idempotent par `SourceGlpiId`,
sauf les actifs non gérés, corrélés par adresse MAC puis IP comme le fait la
découverte réseau. Non repris : les tâches du plugin, et ses agents — déjà
couverts par la catégorie « Agents » (`glpi_agents`, table du cœur de GLPI).
Le contenu d'un paquet est repris : la colonne `json` du plugin, qui porte ses
vérifications, ses actions (`cmd`, `move`, `copy`, `mkdir`, `delete`) et ses
interactions utilisateur, est traduite vers les entrées correspondantes de GlpiNg
(`GlpiDeployPackageJsonMapper`). Ce document est lu tel quel, puis
« désassaini » : GLPI n'écrit pas ses champs texte bruts en base —
`Toolbox\Sanitizer` y échappe les caractères réservés de SQL et encode `&`, `<`
et `>` en entités — si bien qu'un document lu directement dans la colonne n'est
pas du JSON valide. La lecture est permissive — le format a bougé
entre FusionInventory et GLPI Inventory — et ce qui n'a pas d'équivalent est
listé dans les avertissements plutôt que perdu en silence.

Les **fichiers** d'un paquet sont créés avec leur nom, leur empreinte et leur
taille, complétés au besoin depuis la table `deployfiles` du plugin. Leur contenu,
lui, n'est pas en base : il vit sur le disque du serveur GLPI, déjà découpé en
fragments compressés avec un manifeste par fichier. Deux sources permettent de le
rapatrier, renseignées dans le formulaire d'import quand les paquets sont
sélectionnés :

D'après le code du plugin (`inc/deployfile.class.php`,
`inc/deployfilepart.class.php`, `public/b/deploy/index.php`), un fichier est
découpé en fragments compressés en gzip, rangés dans
`files/repository/{1er caractère}/{2 premiers}/{empreinte du fragment}`, et la
liste de ses fragments vit dans `files/manifests/{empreinte du fichier}`, à plat.
Cette liste n'est **nulle part en base** : `deployfiles` ne porte que le nom, la
taille, le type et l'empreinte. Trois routes en découlent :

- le **répertoire des fichiers** du plugin, vu depuis la machine GlpiNg (chemin
  local ou partage réseau) : manifeste puis fragments, aux emplacements ci-dessus.
  La seule qui fonctionne sans rien d'autre. Les chemins connus sont essayés
  directement, l'index de toute l'arborescence ne servant que de filet de sécurité
  — un dépôt de plusieurs milliers de fichiers est long à parcourir sur un partage.
  Un identifiant et un mot de passe optionnels permettent d'atteindre un partage
  auquel le compte du service n'a pas accès : la session est ouverte le temps de
  l'import (`WNetAddConnection2`, sans réserver de lettre de lecteur) et refermée
  ensuite. Windows uniquement ; ailleurs, le partage doit être monté par l'hôte au
  préalable, ce que l'import dit explicitement plutôt que d'échouer ;
- les **serveurs de miroir** déclarés par le plugin (`deploymirrors`, lus à
  l'analyse) : un miroir est une copie statique de `files/` servie par un serveur
  web ordinaire, donc les mêmes chemins en HTTP ;
- une **session GLPI**, avec la racine HTTP que GLPI se connaît
  (`glpi_configs.url_base`, qui pré-remplit un champ modifiable) et un compte :
  `front/deployfile_download.php?deployfile_id=` rend le fichier entier déjà
  réassemblé et décompressé, mais vérifie le droit
  `plugin_glpiinventory_package` — d'où la connexion préalable au formulaire, dont
  le jeton anti-rejeu est repris (son absence est signalée pour elle-même plutôt
  que laissée devenir un 400 inexplicable).

Une **clé d'API ne convient pas** ici, et ce n'est pas un oubli de GLPI :
`apirest.php` n'expose aucun point d'accès rendant les octets d'un fichier de
paquet, le plugin n'en expose pas non plus, et la session ouverte par
`initSession` ne peut pas servir de session web — elle retire `valid_id` de
`$_SESSION` juste après avoir forgé son jeton, précisément pour empêcher cet
usage, alors que les pages `front/` l'exigent.

Le point d'accès des agents (`b/deploy/?action=getFilePart`) n'est **pas** une
route utilisable : il ne sert qu'un fragment à la fois, et l'agent n'apprend la
liste des fragments que dans le JSON de son job, construit à partir du manifeste.

Chaque contenu récupéré est réécrit par le stockage GlpiNg, qui en recalcule le
SHA-512 : s'il ne correspond pas à celui que le paquet attend, rien n'est
enregistré. Un contenu déjà présent n'est jamais retéléchargé ni écrasé.

Sans l'une de ces deux sources, les fichiers restent déclarés **sans leur
contenu**. La fiche du paquet les affiche
« À téléverser », et le lancement d'une tâche comme l'assignation à un poste
refusent un paquet dans cet état : sans ce garde-fou le job partirait avec un
`multiparts` vide, et l'échec ne se verrait qu'au déploiement, poste par poste. Un
second import ne touche jamais aux fragments déjà téléversés. Autre limite
signalée à l'exécution : les protocoles SNMPv3 restent à « Aucun » plutôt que
d'être devinés.

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
- **Outils** : réservations (et, absents même de la sidebar : projets, rappels,
  flux RSS). Les rapports et la base de connaissances existent (voir leurs sections
  ci-dessus) ; côté rapports, ceux de GLPI qui portent sur des modules absents —
  contrats, licences, financier — n'ont pas d'équivalent, et côté base de
  connaissances, le contenu est du Markdown (pas de WYSIWYG), sans commentaires ni pièces
  jointes.
- **Administration** : formulaires.
- **Configuration** : actifs personnalisés, niveaux de services (SLA/OLA),
  unicité des champs, collecteurs, plugins.

À noter : les sections `Assistance`, `Helpdesk` et `Analyse d'impact` de
`/config` configurent des fonctionnalités qui n'existent pas encore.

### Manques transverses

- **Pas d'API REST générique** — rien d'équivalent à `apirest.php` (CRUD et
  recherche par itemtype). Les seuls endpoints exposés sont `/inventory`,
  `/oauth2/token`, `/admin/import/glpi` et l'upload de fichiers de paquet.
- **Pas de gestion documentaire** — aucune entité `Document`, donc pas de
  pièces jointes sur les fiches.
- **Pas d'internationalisation** — aucun `.resx` ni `IStringLocalizer`, l'UI est
  en français en dur.
- **Actions massives limitées aux Ordinateurs** : statut, lieu et utilisateur
  assigné y sont modifiables sur une sélection ; les autres listes n'ont que la
  suppression. L'export reste lui aussi limité à cette page
  (`ComputerExportWriter`) au lieu d'un export générique sur toutes les listes.
- **2FA inerte** — `GlpiEntity.TwoFactorAuthRequired` et
  `GlpiUser.TwoFactorAuthDisabled` sont stockés et éditables, mais il n'y a ni
  TOTP, ni enrôlement, ni vérification au login.
- **Pas de système de plugins** — l'architecture `GlpiNg.Modules.*` est interne
  et compilée, sans chargement à chaud ni marketplace.
- **Moteur de règles partiel** — absents : règles d'habilitations LDAP, règles
  métier tickets, règles d'affectation d'entité, règles de localisation.
- **Collectes sans ciblage** — les collectes (clés de registre, requêtes WMI,
  recherches de fichiers) sont transmises aux agents et leurs résultats stockés,
  mais *toutes* les collectes actives s'appliquent à *tous* les agents : le
  plugin d'origine les cible par tâche, GlpiNg n'a pas de tâche de collecte.
  Seule la dernière valeur de chaque entrée est conservée, sans historique.
- **Liens externes sans `[FIELD:colonne]`** — les balises forment une liste
  fermée (voir `ExternalLinkTags`), là où GLPI accepte n'importe quelle colonne
  de la table de l'objet ; et un lien ne sait pas générer de fichier à partir
  d'un gabarit, seulement pointer une URL. Les schémas `javascript:`, `data:` et
  `vbscript:` ne sont jamais rendus — un lien est configuré par un
  administrateur mais s'affiche pour tous ceux qui ouvrent la fiche.
- **Webhooks sans Twig ni validation CRA** — le corps personnalisé substitue des
  balises `##cle##` comme les gabarits de notification, là où GLPI interprète du
  Twig ; et l'URL n'est pas validée par le défi `crc_token` de GLPI à
  l'enregistrement, le bouton « Tester » répondant au même besoin. Les
  événements disponibles sont ceux que GlpiNg déclenche réellement
  (`NotificationEventCatalog`), donc pas les événements ITIL de GLPI.
- **Recherche** — le moteur multi-critères est en place sur toutes les listes du
  parc, mais l'enregistrement/rappel d'une recherche n'est branché que sur
  Ordinateurs et Moniteurs : `SavedSearchItemTypes` annonce les autres types,
  sans que leur liste sache encore appliquer une recherche sauvegardée.

## À faire

- [ ] Champ « Entité » sur les fiches hors parc (déploiement, notifications,
      groupes) : seules les fiches d'actifs l'exposent
- [ ] Sections d'inventaire encore non reprises, faute d'équivalent dans le
      modèle : machines virtuelles, processus, variables d'environnement, règles
      de pare-feu, comptes et groupes locaux, licences logicielles
- [ ] Page `/self-service` coté utilisateur (le déploiement à la demande est déjà
      opérant depuis la fiche d'un poste : seuls les paquets dont le libre-service
      est activé pour un groupe dont ce poste est membre y sont proposés)
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

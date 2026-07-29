# GlpiNg

Réimplémentation d'un serveur de gestion de parc informatique compatible
avec le protocole **GLPI-Agent**, en .NET 8 / Blazor Server.

## Objectifs

- Inventaire de parc (postes, composants, statuts, affectations)
- Compatibilité protocole GLPI-Agent : contact, inventory, deploy
- Déploiement de paquets logiciels vers les postes via l'agent
- Suivi des tâches de déploiement par machine

## Stack

- .NET 8 / Blazor Server (Interactive Server Components)
- EF Core (SQLite en dev, SQL Server envisagé en prod)
- API REST dédiée (`/glpi-agent`) pour la communication avec GLPI-Agent

## Structure

```
GlpiNg.sln
src/
  GlpiNg.Web/
    Components/       # UI Blazor (pages, layout)
    Controllers/       # Endpoints API protocole agent
    Data/               # DbContext EF Core
    Models/             # Entités (Computer, Agent, DeploymentJob...)
```

## État actuel

- Modèle de données de base (Computer, GlpiAgent, DeploymentJob/Package)
- Endpoint `/glpi-agent` gérant l'annonce ("contact") d'un agent
- Tâche de déploiement de package :
  - `action: "getJobs"` renvoie le job en attente au format JSON GLPI-Agent
    (`jobs.checks/associatedFiles/actions` + carte `associatedFiles` par hash SHA512)
  - `action: "setStatus"` permet à l'agent de rapporter l'avancement / le résultat
  - `GET /glpi-agent/deploy/file/{sha512}` télécharge un fichier de package par son hash
- Import de données depuis une base GLPI MySQL existante (voir section dédiée ci-dessous)
- Tableau de bord minimal

### Import depuis une base GLPI MySQL

`POST /admin/import/glpi` déclenche un import (lecture seule) depuis une base GLPI
MySQL existante, configurée via `GlpiImport:ConnectionString` (appsettings ou
user-secrets — ne pas committer d'identifiants réels) :

```json
{ "GlpiImport": { "ConnectionString": "Server=host;Database=glpi;User=ro_user;Password=***" } }
```

L'import est idempotent : chaque poste est rattaché à son `glpi_computers.id`
d'origine (`Computer.SourceGlpiId`), donc relancer l'import met à jour les postes
existants au lieu de les dupliquer.

**Couvert** : postes (`glpi_computers`), fabricant/modèle/type/état/emplacement,
système d'exploitation, agents (`glpi_agents`, rattachement par `itemtype='Computer'`),
composants matériels CPU/RAM/disques/cartes réseau.

**Non couvert / limites connues** :
- Un seul compte utilisateur MySQL en lecture seule est supposé ; aucune écriture n'est
  faite sur la base GLPI source.
- Les composants matériels (nom exact des colonnes fréquence/capacité) varient selon la
  version de GLPI installée : l'import détecte les colonnes présentes via
  `information_schema` et se dégrade proprement (valeur vide) plutôt que d'échouer,
  mais le résultat peut donc être incomplet selon la version source.
- La correspondance état GLPI → `ComputerStatus` est une heuristique par mots-clés
  (les noms d'état sont libres dans GLPI) : à vérifier sur un premier import.
- L'emplacement GLPI (`locations_id`) est mappé sur un seul champ `Site` ; `Building`/
  `Room` ne sont pas déduits de la hiérarchie d'emplacement.
- Endpoint sans authentification dans ce squelette — à protéger avant tout usage
  hors poste de développement.

### Note sur le protocole de déploiement

Le format du JSON de job (`jobs`, `checks`, `associatedFiles`, `actions`) reprend la
structure observée dans des échanges réels agent/serveur GLPI. Le routage choisi ici
(un seul endpoint POST `/glpi-agent` avec un champ `action`, plutôt que les endpoints
`?action=getJobs/setStatus` à base de query-string du plugin GlpiInventory) est une
adaptation propre à ce projet, pas une reproduction certifiée du protocole d'origine —
à valider face à un agent réel avant mise en production.

## À faire

- [ ] Parsing exhaustif du payload `inventory` (hardware/software/réseau)
- [ ] Stockage réel des fichiers de package (upload, découpage en fragments)
- [ ] CRUD complet + recherche avancée sur le parc
- [ ] Gestion des rôles / authentification (dont protection de /admin/import/glpi)
- [ ] Pages de gestion des agents et des déploiements (nav actuelle : liens non encore implémentés)

## Démarrage

```bash
cd src/GlpiNg.Web
dotnet restore
dotnet ef database update   # si migrations générées
dotnet run
```

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
- Tableau de bord minimal

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
- [ ] Gestion des rôles / authentification
- [ ] Pages de gestion des agents et des déploiements (nav actuelle : liens non encore implémentés)

## Démarrage

```bash
cd src/GlpiNg.Web
dotnet restore
dotnet ef database update   # si migrations générées
dotnet run
```

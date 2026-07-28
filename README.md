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

Squelette initial :
- Modèle de données de base (Computer, GlpiAgent, DeploymentJob/Package)
- Endpoint `/glpi-agent` gérant l'annonce ("contact") d'un agent
- Tableau de bord minimal

## À faire

- [ ] Parsing exhaustif du payload `inventory` (hardware/software/réseau)
- [ ] Génération du JSON de job `deploy` (fichiers + hash SHA512 + actions)
- [ ] Endpoint de téléchargement des fragments de fichiers pour le deploy
- [ ] CRUD complet + recherche avancée sur le parc
- [ ] Gestion des rôles / authentification
- [ ] Pages de gestion des agents et des déploiements

## Démarrage

```bash
cd src/GlpiNg.Web
dotnet restore
dotnet ef database update   # si migrations générées
dotnet run
```

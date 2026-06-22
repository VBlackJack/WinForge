# WinForge / Win11Forge v2026062101

**Configurez un PC Windows 10/11 avec des profils d'applications reproductibles.**

[![Version](https://img.shields.io/badge/version-2026062101-blue.svg)](CHANGELOG.md)
[![Windows](https://img.shields.io/badge/Windows-10%20%2F%2011-0078D4.svg)](https://www.microsoft.com/windows)
[![License](https://img.shields.io/badge/license-Apache%202.0-blue.svg)](LICENSE)

Win11Forge automatise l'installation et la mise a jour d'applications Windows a partir de profils JSON. L'interface WPF permet de choisir un profil, ajuster la selection, scanner les installations existantes, lancer des installations par lot et maintenir le catalogue d'applications.

## Demarrage rapide

1. Telechargez la derniere archive depuis les [releases](https://github.com/VBlackJack/WinForge/releases/latest).
2. Decompressez l'archive dans un dossier local.
3. Lancez `Win11Forge.cmd` ou `Win11Forge.GUI.exe`.
4. Choisissez un profil, ajustez les applications si besoin, puis lancez l'installation.

## Profils inclus

| Profil | Usage | Contenu |
| --- | --- | --- |
| `Base` | socle general | navigateurs, multimedia, utilitaires systeme, diagnostic et securite |
| `Office` | productivite | `Base` + suite bureautique, PDF, collaboration |
| `Gaming` | jeu | `Office` + plateformes de jeu et communication |
| `Personnel` | poste avance | `Gaming` + outils de dev, cloud, VPN et productivite personnelle |
| `Enterprise` | poste professionnel | `Base` + outils IT, securite, collaboration et configuration durcie |

Les profils peuvent heriter les uns des autres. Une application heritee d'un profil parent doit etre retiree dans ce parent, pas dans l'enfant.

## Fonctionnalites

- Interface WPF moderne, themes clairs/sombres, francais et anglais.
- Catalogue de 195 applications avec sources Winget, Chocolatey, Microsoft Store ou telechargement direct selon les entrees.
- Detection des applications deja installees et des mises a jour disponibles.
- Installation, mise a jour et desinstallation par lot avec progression, logs et annulation cooperative.
- Edition du catalogue d'applications depuis l'interface.
- Creation de nouveaux profils et mise a jour directe d'un profil existant depuis la selection de la grille Applications.
- Deploiements planifies depuis les parametres.
- API REST locale PowerShell pour l'automatisation avancee.

## Modifier un profil

1. Ouvrez la page **Applications**.
2. Selectionnez un profil dans la carte **Profil**.
3. Cochez ou decochez les applications dans la grille.
4. Cliquez sur **Mettre a jour le profil** pour sauvegarder la selection dans ce profil.

Le bouton **Sauvegarder le profil** sert a creer un nouveau profil ou a enregistrer une selection sous un autre nom.

## Configuration requise

- Windows 10 21H2 ou plus recent, ou Windows 11.
- Connexion internet pour les sources de paquets.
- Droits administrateur pour les operations systeme et certaines installations.
- PowerShell est utilise par les modules d'installation fournis avec le projet.

## Documentation

- [Guide utilisateur](Docs/USER_GUIDE.md)
- [Index documentation](Docs/README.md)
- [Architecture publique](Docs/ARCHITECTURE.md)
- [Documentation API](Docs/API_DOCUMENTATION.md)
- [Guide de contribution](CONTRIBUTING.md)
- [Changelog](CHANGELOG.md)

## Support

Signalez les problemes ou demandes d'evolution via les [issues GitHub](https://github.com/VBlackJack/WinForge/issues).

**Licence :** Apache 2.0

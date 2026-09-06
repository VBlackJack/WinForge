# WinForge v2026090601

[English](README.md)

**Configurez un PC Windows 10/11 avec des profils d'applications reproductibles.**

[![Version](https://img.shields.io/badge/version-2026090601-blue.svg)](CHANGELOG.md)
[![Windows](https://img.shields.io/badge/Windows-10%20%2F%2011-0078D4.svg)](https://www.microsoft.com/windows)
[![License](https://img.shields.io/badge/license-Apache%202.0-blue.svg)](LICENSE)

WinForge automatise l'installation et la mise à jour d'applications Windows à partir de profils JSON. L'interface WPF permet de choisir un profil, ajuster la sélection, scanner les installations existantes, lancer des installations par lot et maintenir le catalogue d'applications.

## Démarrage rapide

1. Téléchargez la dernière archive depuis les [releases](https://github.com/VBlackJack/WinForge/releases/latest), ainsi que le fichier `.zip.sha256` associé.
2. Vérifiez l'intégrité de l'archive avant de l'extraire. Remplacez `XXXXXXXXXX` par la version téléchargée :

   ```powershell
   $expected = ((Get-Content .\WinForge_vXXXXXXXXXX.zip.sha256 -Raw).Trim() -split '\s+')[0]
   $actual = (Get-FileHash .\WinForge_vXXXXXXXXXX.zip -Algorithm SHA256).Hash
   if ($expected -eq $actual) { 'OK' } else { 'CHECKSUM MISMATCH' }
   ```

3. Décompressez l'archive dans un dossier local uniquement si la somme de contrôle correspond.
4. Lancez `WinForge.cmd` ou `WinForge.GUI.exe`.
5. Choisissez un profil, ajustez les applications si besoin, puis lancez l'installation.

## Profils inclus

| Profil | Usage | Contenu |
| --- | --- | --- |
| `Base` | socle général | navigateurs, multimédia, utilitaires système, diagnostic et sécurité |
| `Office` | productivité | `Base` + suite bureautique, PDF, collaboration |
| `Gaming` | jeu | `Office` + plateformes de jeu et communication |
| `Personnel` | poste avancé | `Gaming` + outils de dev, cloud, VPN et productivité personnelle |
| `Enterprise` | poste professionnel | `Base` + outils IT, sécurité, collaboration et configuration durcie |

Les profils peuvent hériter les uns des autres. Une application héritée d'un profil parent doit être retirée dans ce parent, pas dans l'enfant.

## Fonctionnalités

- Interface WPF moderne, thèmes clairs/sombres, français et anglais.
- Catalogue de 195 applications avec sources Winget, Chocolatey, Microsoft Store ou téléchargement direct selon les entrées.
- Détection des applications déjà installées et des mises à jour disponibles.
- Installation, mise à jour et désinstallation par lot avec progression, logs et annulation coopérative.
- Édition du catalogue d'applications depuis l'interface.
- Création de nouveaux profils et mise à jour directe d'un profil existant depuis la sélection de la grille Applications.
- Déploiements planifiés depuis les paramètres.
- API REST locale PowerShell pour l'automatisation avancée.

## Modifier un profil

1. Ouvrez la page **Applications**.
2. Sélectionnez un profil dans la carte **Profil**.
3. Cochez ou décochez les applications dans la grille.
4. Cliquez sur **Mettre à jour le profil** pour sauvegarder la sélection dans ce profil.

Le bouton **Sauvegarder le profil** sert à créer un nouveau profil ou à enregistrer une sélection sous un autre nom.

## Configuration requise

- Windows 10 21H2 ou plus récent, ou Windows 11.
- Connexion internet pour les sources de paquets.
- Droits administrateur pour les opérations système et certaines installations.
- PowerShell est utilisé par les modules d'installation fournis avec le projet.

## Documentation

- [Guide utilisateur](Docs/USER_GUIDE.fr.md)
- [Index documentation](Docs/README.fr.md)
- [Architecture publique (anglais)](Docs/ARCHITECTURE.md)
- [Documentation API (anglais)](Docs/API_DOCUMENTATION.md)
- [Guide de contribution (anglais)](CONTRIBUTING.md)
- [Changelog (anglais)](CHANGELOG.md)

## Support

Signalez les problèmes ou demandes d'évolution via les [issues GitHub](https://github.com/VBlackJack/WinForge/issues).

**Licence :** Apache 2.0

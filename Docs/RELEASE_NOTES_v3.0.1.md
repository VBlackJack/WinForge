# Win11Forge v3.0.1

**Date de sortie :** 7 janvier 2026
**Auteur :** Julien Bombled
**Licence :** Apache 2.0

---

## Corrections de bugs

### Detection des applications

Plusieurs applications n'etaient pas correctement detectees comme installees :

| Application | Probleme | Correction |
|-------------|----------|------------|
| **TV Rename** | Mauvais repertoire (Program Files au lieu de x86) | Chemin corrige vers `Program Files (x86)` |
| **Creality Slicer** | Ancien nom de dossier/exe | Chemin avec wildcard `Creality Slicer*\CrealitySlicer.exe` |
| **PDF-XChange Editor** | Cle de registre incorrecte | Detection par fichier `Tracker Software\PDF Editor\PDFXEdit.exe` |
| **WSL2** | Commande `wsl --status` non fiable | Detection via WindowsFeature `Microsoft-Windows-Subsystem-Linux` |

### Structure du build

- Les fichiers GUI sont maintenant dans un sous-dossier `GUI/` au lieu de la racine
- Lanceur simplifie `Win11Forge.cmd`
- Ajout du dossier `Docs/` dans le package de distribution

---

## Mise a jour depuis v3.0.0

Telechargez simplement le nouveau ZIP et remplacez l'ancien dossier.

---

## Fichiers modifies

- `Apps/Database/applications.json` - Corrections de detection
- `Config/version.json` - Version 3.0.1
- `Profiles/*.json` - Version 3.0.1
- `Deploy-Win11Forge.bat` - Version 3.0.1
- `Build-Release.ps1` - Nouvelle structure de build

---

**Merci d'utiliser Win11Forge !**

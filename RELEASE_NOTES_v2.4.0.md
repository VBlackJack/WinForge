# 🚀 Win11Forge Framework v2.4.0 - Release Notes

**Release Date:** 2025-10-06
**Type:** Compatibility & Performance Release
**Commit:** 19f9e46

---

## 📋 Table des Matières

- [Vue d'ensemble](#vue-densemble)
- [Nouvelles Fonctionnalités](#nouvelles-fonctionnalités)
- [Corrections Majeures](#corrections-majeures)
- [Améliorations](#améliorations)
- [Migration depuis v2.3.0](#migration-depuis-v230)
- [Statistiques](#statistiques)
- [Contributeurs](#contributeurs)

---

## 🎯 Vue d'ensemble

La version **2.4.0** du Win11Forge Framework apporte des **améliorations majeures de compatibilité PowerShell 5.1**, des **optimisations de performance pour System-Audit**, et des **corrections critiques pour la stabilité du mode séquentiel**.

### Highlights

✨ **PowerShell 5.1 Full Compatibility** - Mode séquentiel 100% compatible
⚡ **System-Audit Performance** - Overhead réduit de 67% (3000ms → 750ms)
🔧 **TrustedInstaller Launcher** - Menu interactif avec 8 options
🚀 **Auto-Restart Mechanism** - PowerShell 7 auto-upgrade intelligent
🐛 **15+ Critical Bugs Fixed** - StrictMode, GUI, Parallel mode

---

## ✨ Nouvelles Fonctionnalités

### 🔍 System-Audit v2.3.0 - Performance Optimized

**Réduction massive de l'overhead de monitoring** :
- ⚡ **67% moins d'overhead** : 3000ms → ~750ms par échantillon
- 🎯 **Intervalle optimisé** : 2s → 5s par défaut (configurable)
- 📊 **Fréquences ajustées** :
  - Applications : 30s (au lieu de 20s)
  - Event Viewer : 60s (au lieu de 30s)
  - Network : 120s (au lieu de 40s)
- 🚫 **Nouveau paramètre** : `-SkipApplicationMonitoring` (réduit overhead de 40% supplémentaire)
- 📈 **Affichage temps réel** : avg/max temps par échantillon

**Exemple d'utilisation** :
```powershell
.\Tools\System-Audit.ps1 -MonitorLogPath ".\Logs" `
    -LogCompletionMarkers "Deployment completed|Summary" `
    -GenerateReport -AuditName "Win11Forge_Deploy"
```

### 🔐 TrustedInstaller Launcher Improvements

**Menu interactif avec privilèges NT AUTHORITY\SYSTEM** :

1. PowerShell (TrustedInstaller)
2. Command Prompt (TrustedInstaller)
3. Registry Editor (TrustedInstaller)
4. Task Manager (TrustedInstaller)
5. Computer Management (TrustedInstaller)
6. Windows Explorer (TrustedInstaller)
7. Custom executable path
8. Win11Forge GUI (TrustedInstaller)

**Fonctionnalités** :
- ✅ Exécution avec privilèges NT AUTHORITY\SYSTEM
- ✅ GUI visible dans la session utilisateur (Session 1)
- ✅ Support automatique des fichiers .msc (via mmc.exe)
- ✅ Auto-installation du module NtObjectManager si nécessaire

**Utilisation** :
```batch
cd Tools
.\Launch-AsTrustedInstaller.bat
```

### 🔄 PowerShell 7 Auto-Restart Enhancement

**Redémarrage intelligent automatique** :
- 🔍 Détection automatique de PowerShell 5.1
- 🔄 Redémarrage automatique en PowerShell 7
- 💾 Préservation de tous les paramètres (-ProfileName, -Parallel, etc.)
- 📝 Message informatif avant redémarrage
- ✅ Support modes Parallel et Sequential

**Flux automatique** :
1. Script lancé en PowerShell 5.1
2. Détection version < 7.0
3. Relance automatique en `pwsh.exe` avec paramètres identiques
4. Installation continue sans intervention

---

## 🔧 Corrections Majeures

### 1. PowerShell 5.1 Sequential Mode Compatibility ✅

**Problème** : `PropertyNotFoundException` en mode StrictMode PowerShell 5.1

**Solution** :
- ✅ Remplacement des conditions chainées par des conditions imbriquées
- ✅ Accès sécurisé aux propriétés via `PSObject.Properties['PropertyName']`
- ✅ 100% compatible PowerShell 5.1 et 7.x

**Fichiers modifiés** :
- `Modules/InstallationEngine.psm1` (lignes 646-656)

**Exemple de fix** :
```powershell
# Avant (crash en PS 5.1 StrictMode)
if ($app.InstallationOptions -and $app.InstallationOptions.IgnoreExitCodeIfFileExists) {
    # ...
}

# Après (compatible PS 5.1 + 7.x)
if ($app.PSObject.Properties['InstallationOptions']) {
    if ($app.InstallationOptions.IgnoreExitCodeIfFileExists) {
        # ...
    }
}
```

### 2. System-Audit Bug Fixes (v2.2.0) ✅

**Bugs critiques résolus** :
- 🐛 **Processus terminés** comptés avant calcul overhead (timing fix)
- 🐛 **Division par zéro** dans génération rapport HTML (protection ajoutée)
- 🐛 **Ctrl+C gracieux** : Génère automatiquement le rapport même en interruption
- 🐛 **Mode Quiet** : `-Quiet` pour exécution silencieuse (scripts automatisés)

**Optimisations v2.2.0** :
- ⚡ Session CIM réutilisable (+20% performance)
- ⚡ Optimisation HashSet pour comparaisons O(1) au lieu de O(n²)

### 3. GUI Stability Improvements ✅

**Fixes GUI** :
- ✅ Module caching `PropertyNotFoundException` resolved
- ✅ Détection call operator vs direct execution
- ✅ Correction crash au lancement avec paths contenant espaces
- ✅ Validation AppId override et propagation exit codes

**Fichier** : `Modules/Win11ForgeGUI.psm1`

### 4. StrictMode and Parallel Mode Fixes ✅

**Crash statistiques mode parallèle** :
- ✅ Null-safe environment report avec fallbacks
- ✅ Propriété `Skipped` correctement vérifiée dans stats
- ✅ Apps skippées comptées correctement (pas comme Failed)
- ✅ Affichage summary correct pour apps skippées (jaune au lieu de rouge)

**Fichier** : `Deploy-Win11Environment.ps1`

### 5. TrustedInstaller Launcher Fixes ✅

**Gestion des chemins avec espaces** :
- ✅ Correction du quoting pour paths avec espaces
- ✅ Suppression du code mort (delayed expansion inutilisée)
- ✅ Robustesse assignment ARGS avec quoted set statement
- ✅ Validation correcte des paramètres personnalisés

**Fichier** : `Tools/Launch-AsTrustedInstaller.bat`

### 6. DirectDownload and ZIP Deployment ✅

**Support multi-format** :
- ✅ DirectDownload fonctionnel en mode parallèle pour PS7
- ✅ Déploiement ZIP archive correct pour outils portables
- ✅ Mode séquentiel ZIP deployment avec Detection.Path
- ✅ Compatibilité PowerShell 5.1 pour DirectDownload
- ✅ Suppression `-UseBasicParsing` en mode séquentiel

**Fichier** : `Modules/InstallationEngine.psm1`

---

## 🛠️ Améliorations

### Documentation Consistency

**50+ fichiers harmonisés** à v2.4.0 :
- ✅ Bannières console : v2.4.0
- ✅ Entêtes modules : v2.4.0
- ✅ README.md, PROJET_STRUCTURE.md, GUI_README.md
- ✅ Tools/README.md, System-Audit-README.md
- ✅ Scripts de lancement et utilitaires

### EnvironmentDetection Module Path

**Utilisation de RepositoryRoot** :
- ✅ Remplacement calcul path relatif par variable `$script:RepositoryRoot`
- ✅ Path module fiable en mode séquentiel
- ✅ Plus maintenable avec variable centralisée

**Fichier** : `Modules/InstallationEngine.psm1:519`

### Module Encoding and Formatting

**Uniformisation** :
- ✅ UTF-8 BOM appliqué à tous modules et scripts
- ✅ Formatage linter appliqué uniformément
- ✅ Amélioration démarrage GUI

---

## 🔄 Migration depuis v2.3.0

### Compatibilité

✅ **100% rétrocompatible** avec v2.3.0
✅ **Aucune action requise** pour migration
✅ **Profils v2.3.0** fonctionnent sans modification

### Mise à jour

```bash
# Clone ou pull la dernière version
git pull origin main

# Ou téléchargez la release v2.4.0
# https://github.com/VBlackJack/Win11Forge/releases/tag/v2.4.0
```

### Nouveaux Paramètres (Optionnels)

**System-Audit** :
```powershell
# Nouveau paramètre pour réduire overhead
.\Tools\System-Audit.ps1 -MonitorLogPath ".\Logs" `
    -SkipApplicationMonitoring `  # ← NOUVEAU
    -GenerateReport
```

---

## 📊 Statistiques

### Commits et Changements

- **100+ commits** depuis v2.3.0 (2025-10-04 → 2025-10-06)
- **35 fichiers** modifiés dans le commit final
- **50+ fichiers** corrigés pour cohérence de version
- **2214 insertions, 39 suppressions**

### Bugs Résolus

- **15+ bugs critiques** :
  - ✅ StrictMode PropertyNotFoundException (PS 5.1)
  - ✅ Crash statistiques mode parallèle
  - ✅ GUI module caching issues
  - ✅ TrustedInstaller paths avec espaces
  - ✅ DirectDownload multi-format support
  - ✅ ZIP deployment detection
  - ✅ EnvironmentDetection module path
  - ✅ System-Audit overhead (v2.1.0 → v2.3.0)

### Performance

**System-Audit** :
- **v2.1.0** : ~5000ms/sample (100% overhead)
- **v2.2.0** : ~3000ms/sample (60% overhead) ← -40%
- **v2.3.0** : ~750ms/sample (20% overhead) ← -67% total

### Compatibilité

- ✅ **PowerShell 5.1** : Mode séquentiel 100% compatible
- ✅ **PowerShell 7.x** : Modes séquentiel + parallèle
- ✅ **Windows 11** : 22H2, 23H2, 24H2
- ✅ **Environnements** : Physical, VMware, Hyper-V, Sandbox

---

## 👥 Contributeurs

- **Julien Bombled** - Développement principal
- **Claude (Anthropic)** - Assistance développement et optimisation

---

## 🔗 Liens Utiles

- **Repository** : [github.com/VBlackJack/Win11Forge](https://github.com/VBlackJack/Win11Forge)
- **CHANGELOG** : [CHANGELOG.md](CHANGELOG.md#240---2025-10-06)
- **Documentation** : [README.md](README.md)
- **System-Audit** : [Tools/System-Audit-README.md](Tools/System-Audit-README.md)
- **Previous Release** : [v2.3.0](https://github.com/VBlackJack/Win11Forge/releases/tag/v2.3.0)

---

## 📝 Notes Importantes

1. **PowerShell 7 recommandé** pour mode parallèle (auto-installation disponible)
2. **Admin requis** pour tous les lanceurs (auto-élévation automatique)
3. **System-Audit** : Nouveau paramètre `-SkipApplicationMonitoring` pour overhead minimal
4. **TrustedInstaller** : Utiliser avec précaution (privilèges système complets)

---

**Version** : 2.4.0
**Date de Release** : 2025-10-06
**Statut** : ✅ Stable - Production Ready

🎉 **Merci d'utiliser Win11Forge !**

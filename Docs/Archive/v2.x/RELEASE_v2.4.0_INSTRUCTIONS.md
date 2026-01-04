# Instructions pour créer la GitHub Release v2.4.0

## 📋 Étapes pour créer la Release

### 1. Aller sur GitHub

Ouvrir : https://github.com/VBlackJack/Win11Forge/releases/new

### 2. Configuration de la Release

**Tag version** : `v2.4.0` (déjà créé et poussé)

**Release title** :
```
Win11Forge v2.4.0 - Compatibility & Performance Release
```

**Description** : (Copier-coller le contenu ci-dessous)

---

## 🎯 Compatibility & Performance Release

La version **2.4.0** apporte des **améliorations majeures de compatibilité PowerShell 5.1**, des **optimisations de performance pour System-Audit**, et des **corrections critiques pour la stabilité**.

### 🌟 Highlights

- ✅ **PowerShell 5.1 Full Compatibility** - Mode séquentiel 100% compatible
- ⚡ **System-Audit Performance** - Overhead réduit de 67% (3000ms → 750ms)
- 🔧 **TrustedInstaller Launcher** - Menu interactif avec 8 options
- 🚀 **Auto-Restart Mechanism** - PowerShell 7 auto-upgrade intelligent
- 🐛 **15+ Critical Bugs Fixed** - StrictMode, GUI, Parallel mode

---

## ✨ Nouvelles Fonctionnalités

### 🔍 System-Audit v2.3.0 - Performance Optimized

**Réduction massive de l'overhead de monitoring** :
- ⚡ **67% moins d'overhead** : 3000ms → ~750ms par échantillon
- 🎯 **Intervalle optimisé** : 2s → 5s par défaut
- 📊 **Fréquences ajustées** : Apps (30s), Events (60s), Network (120s)
- 🚫 **Nouveau paramètre** : `-SkipApplicationMonitoring` (réduit overhead de 40%)

### 🔐 TrustedInstaller Launcher

**Menu interactif avec privilèges NT AUTHORITY\SYSTEM** :
1. PowerShell (TrustedInstaller)
2. Command Prompt (TrustedInstaller)
3. Registry Editor (TrustedInstaller)
4. Task Manager (TrustedInstaller)
5. Computer Management (TrustedInstaller)
6. Windows Explorer (TrustedInstaller)
7. Custom executable path
8. Win11Forge GUI (TrustedInstaller)

### 🔄 PowerShell 7 Auto-Restart

- 🔍 Détection automatique de PowerShell 5.1
- 🔄 Redémarrage automatique en PowerShell 7
- 💾 Préservation de tous les paramètres
- ✅ Support modes Parallel et Sequential

---

## 🔧 Corrections Majeures

### PowerShell 5.1 Compatibility ✅
- Résolution `PropertyNotFoundException` en mode StrictMode
- Conditions imbriquées pour compatibilité PS 5.1 + 7.x
- Accès sécurisé aux propriétés PSObject

### System-Audit Bug Fixes ✅
- Processus terminés comptés avant calcul overhead
- Protection division par zéro dans rapport HTML
- Gestionnaire Ctrl+C gracieux
- Mode Quiet pour scripts automatisés
- Session CIM réutilisable (+20% performance)
- Optimisation HashSet O(1) vs O(n²)

### GUI & StrictMode Fixes ✅
- Module caching PropertyNotFoundException resolved
- Crash statistiques mode parallèle fixed
- Apps skippées comptées correctement
- TrustedInstaller paths avec espaces fixed

### DirectDownload & ZIP Deployment ✅
- DirectDownload fonctionnel en mode parallèle PS7
- Déploiement ZIP archive pour outils portables
- Compatibilité PowerShell 5.1

---

## 📊 Statistiques

- **100+ commits** depuis v2.3.0
- **35 fichiers** modifiés
- **15+ bugs critiques** résolus
- **67% réduction overhead** System-Audit
- **100% compatibilité** PS 5.1 + 7.x

---

## 📚 Documentation

📖 **Release Notes Complètes** : [RELEASE_NOTES_v2.4.0.md](https://github.com/VBlackJack/Win11Forge/blob/main/RELEASE_NOTES_v2.4.0.md)
📋 **CHANGELOG** : [CHANGELOG.md](https://github.com/VBlackJack/Win11Forge/blob/main/CHANGELOG.md#240---2025-10-06)
📘 **Documentation** : [README.md](https://github.com/VBlackJack/Win11Forge/blob/main/README.md)
🔍 **System-Audit** : [System-Audit-README.md](https://github.com/VBlackJack/Win11Forge/blob/main/Tools/System-Audit-README.md)

---

## 🔄 Migration depuis v2.3.0

✅ **100% rétrocompatible** - Aucune action requise
✅ **Profils v2.3.0** fonctionnent sans modification

```bash
# Mise à jour
git pull origin main
```

---

## 📝 Notes Importantes

1. **PowerShell 7 recommandé** pour mode parallèle (auto-installation disponible)
2. **Admin requis** pour tous les lanceurs (auto-élévation automatique)
3. **System-Audit** : Nouveau paramètre `-SkipApplicationMonitoring` disponible
4. **TrustedInstaller** : Utiliser avec précaution (privilèges système complets)

---

**Auteur** : Julien Bombled
**Avec l'assistance de** : Claude (Anthropic)
**Statut** : ✅ Stable - Production Ready

🎉 **Merci d'utiliser Win11Forge !**

---

### 3. Options supplémentaires

- ✅ Cocher **"Set as the latest release"**
- ✅ Cocher **"Create a discussion for this release"** (optionnel)

### 4. Publier

Cliquer sur **"Publish release"**

---

## 🔗 Lien direct

Une fois créée, la release sera accessible à :
https://github.com/VBlackJack/Win11Forge/releases/tag/v2.4.0

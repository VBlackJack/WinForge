# 🔢 Gestion des Versions - Win11Forge

## 📋 Philosophie

Win11Forge utilise un système de **versionnage centralisé** pour éviter les incohérences et simplifier les mises à jour.

## 🎯 Source Unique de Vérité

### `Config/version.json`
```json
{
  "DisplayName": "Win11Forge Framework",
  "Version": "2.5.0",
  "ReleaseDate": "2025-10-06"
}
```

**C'est le SEUL endroit où la version du framework doit être modifiée.**

## 🛠️ Utilisation dans les Scripts

### Méthode Recommandée

```powershell
# Charger la version dynamiquement
$versionInfo = & "$PSScriptRoot\Tools\Get-Win11ForgeVersion.ps1"
$version = $versionInfo.Version

Write-Host "Win11Forge v$version"
```

### Exemples d'Implémentation

#### Scripts Principaux
- ✅ `Setup-Framework.ps1` - Charge la version dynamiquement
- ✅ `Cleanup-Framework.ps1` - Charge la version dynamiquement

#### Modules
- ✅ `ProfileManager.psm1` - Utilise version.json comme fallback si profil n'a pas de version

## 📦 Versionnage des Profils

### Stratégie Hybride

Les profils JSON (`Profiles/*.json`) peuvent avoir leur propre version pour tracker l'évolution de leur contenu :

```json
{
  "Name": "Base",
  "Version": "2.5.0",
  "Description": "...",
  "Applications": [...]
}
```

**Règles** :
1. Si le profil spécifie une `Version`, elle est utilisée
2. Si `Version` est absente, le ProfileManager utilise automatiquement la version du framework
3. Mettre à jour la version du profil uniquement si son contenu change

### Quand Mettre à Jour la Version d'un Profil?

- ✅ Ajout/suppression d'applications
- ✅ Changement de configuration système
- ✅ Modification de la description
- ❌ Mise à jour du framework sans modification du profil

## 🔄 Processus de Release

### 1. Mettre à Jour la Version

Éditer **UN SEUL FICHIER** :
```bash
# Config/version.json
{
  "Version": "2.6.0",
  "ReleaseDate": "2025-XX-XX"
}
```

### 2. Vérifier la Cohérence

```powershell
.\Tools\Verify-VersionConsistency.ps1
```

### 3. Tester

```powershell
.\Run-All-Checks.ps1
# ou
.\Run-All-Checks.bat
```

## 📝 Outils de Gestion

### `Tools/Get-Win11ForgeVersion.ps1`

Utilitaire pour charger la version depuis `Config/version.json` :

```powershell
$version = & .\Tools\Get-Win11ForgeVersion.ps1
# Retourne: PSCustomObject avec Version, DisplayName, ReleaseDate
```

### `Tools/Verify-VersionConsistency.ps1`

Vérifie que tous les scripts principaux utilisent la bonne version :
- Scripts principaux (`.ps1`)
- Modules (`.psm1`)
- Fichiers batch (`.bat`)

## ⚠️ À NE PAS FAIRE

❌ **Coder en dur une version dans un script**
```powershell
# MAUVAIS
Write-Host "Win11Forge v2.5.0"
```

✅ **Charger la version dynamiquement**
```powershell
# BON
$version = (& "$PSScriptRoot\Tools\Get-Win11ForgeVersion.ps1").Version
Write-Host "Win11Forge v$version"
```

## 🏷️ Versions Indépendantes Autorisées

Certains outils peuvent avoir leur propre versionnage :

- `Tools/System-Audit.ps1` (v2.4.0) - Outil universel avec son propre cycle
- Documentation historique (RELEASE_NOTES_v*.md)

## 📊 Résumé du Système

| Composant | Version | Source |
|-----------|---------|--------|
| **Framework** | 2.5.0 | `Config/version.json` |
| **Profils** | 2.5.0 ou indépendante | JSON ou fallback framework |
| **Scripts** | Dynamique | `Get-Win11ForgeVersion.ps1` |
| **Modules** | 2.5.0 | Constante dans en-tête |

## ✅ Avantages du Système

1. **Un seul point de modification** - `Config/version.json`
2. **Cohérence automatique** - Les scripts chargent dynamiquement
3. **Traçabilité** - Les profils peuvent tracker leurs propres évolutions
4. **Simplicité** - Plus besoin de modifier 50 fichiers pour une release

## 🔍 Vérification Rapide

```powershell
# Afficher la version actuelle
& .\Tools\Get-Win11ForgeVersion.ps1

# Vérifier la cohérence
.\Tools\Verify-VersionConsistency.ps1

# Tester tout
.\Run-All-Checks.ps1
```

---

**Dernière mise à jour** : 2025-10-08  
**Version du document** : 1.0

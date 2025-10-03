# Win11Forge - Guide de Déploiement Rapide

**Version** : 2.1.3
**Mise à jour** : 2025-10-03

---

## 🚀 Démarrage Rapide (5 minutes)

### Étape 1 : Télécharger Win11Forge
```powershell
# Si vous avez git
git clone https://github.com/your-repo/Win11Forge.git
cd Win11Forge

# Ou téléchargez le ZIP et extrayez-le
```

### Étape 2 : Lancer le déploiement

**Option A : Lanceur Interactif (Recommandé pour débutants)**
```cmd
# Clic droit → "Exécuter en tant qu'administrateur"
Deploy-Win11Forge.bat
```

**Option B : PowerShell Direct (Recommandé pour avancés)**
```powershell
# PowerShell 7 en tant qu'Administrateur
cd C:\Path\To\Win11Forge
.\Deploy-Win11Environment.ps1 -ProfileName "Base" -Parallel
```

---

## 📦 Quel Profil Choisir ?

| Profil | Applications | Cas d'usage | Durée |
|--------|--------------|-------------|-------|
| **Base** | 31 apps | PC de travail, diagnostics | ~5-8 min |
| **Office** | 36 apps | Base + productivité bureautique | ~6-10 min |
| **Gaming** | 40 apps | Office + plateformes gaming | ~7-12 min |
| **Personnel** | 66 apps | Gaming + dev tools complets | ~10-15 min |

### Recommandations

**Installation fraîche Windows 11** → `Base` puis ajouter selon besoins

**PC de bureau professionnel** → `Office`

**PC gaming** → `Gaming`

**Workstation développeur** → `Personnel`

---

## ⚡ Commandes Essentielles

### Déploiement Standard
```powershell
# Profil Base (le plus rapide)
.\Deploy-Win11Environment.ps1 -ProfileName "Base" -Parallel

# Profil Gaming
.\Deploy-Win11Environment.ps1 -ProfileName "Gaming" -Parallel

# Profil Personnel (complet)
.\Deploy-Win11Environment.ps1 -ProfileName "Personnel" -Parallel
```

### Test sans Installation
```powershell
# Dry-run pour voir ce qui serait installé
.\Deploy-Win11Environment.ps1 -ProfileName "Personnel" -TestMode -Verbose
```

### Mode Verbose (Debug)
```powershell
# Affichage détaillé de toutes les opérations
.\Deploy-Win11Environment.ps1 -ProfileName "Gaming" -Parallel -Verbose
```

---

## 📊 Pendant l'Installation

### Ce qui se passe automatiquement :

1. **Vérification administrateur** (obligatoire)
2. **Détection environnement** (VMware, Hyper-V, physique...)
3. **Installation prérequis**
   - PowerShell 7 (si manquant)
   - Chocolatey
   - .NET 6, .NET 8, .NET Framework 4.8.1
   - Visual C++ Redistributables
   - Java runtime
4. **Chargement profil** avec héritage
5. **Installation applications** (parallèle si `-Parallel`)
6. **Configuration système**
   - Explorer (fichiers cachés, extensions)
   - Taskbar (widgets, alignement)
   - Network (DNS optimisés)
   - Privacy (télémétrie désactivée)
   - Performance (services, power plan)
   - Security (Defender, Firewall)

### Durées Approximatives

**Mode Séquentiel** (sans `-Parallel`) :
- Base : 15-20 min
- Gaming : 25-35 min
- Personnel : 45-60 min

**Mode Parallèle** (avec `-Parallel`) ⭐ :
- Base : 5-8 min
- Gaming : 7-12 min
- Personnel : 10-15 min

---

## ✅ Après l'Installation

### Vérifier les Résultats
```powershell
# Voir le dernier log
Get-Content .\Logs\deployment_*.log | Select-Object -Last 50

# Statistiques finales
Get-Content .\Logs\deployment_*.log | Select-String "Deployment Summary" -Context 0,10
```

### Applications Installées ?
```powershell
# Vérifier une app spécifique
winget list --name "Chrome"

# Vérifier Battle.net
Test-Path "C:\Program Files (x86)\Battle.net\Battle.net.exe"
```

### DNS Configurés ?
```powershell
Get-DnsClientServerAddress -InterfaceAlias "Ethernet*" |
  Select-Object InterfaceAlias, ServerAddresses
```

---

## 🐛 Résolution de Problèmes

### "Script not found"
```powershell
# Vérifiez que vous êtes dans le bon répertoire
cd C:\Path\To\Win11Forge
Get-ChildItem Deploy-Win11Environment.ps1
```

### "Execution Policy"
```powershell
# Autorisez l'exécution temporairement
Set-ExecutionPolicy -ExecutionPolicy Bypass -Scope Process
.\Deploy-Win11Environment.ps1 -ProfileName "Base"
```

### Application échoue
```powershell
# Voir les logs détaillés
Get-Content .\Logs\deployment_*.log | Select-String "Failed"

# Relancer avec verbose
.\Deploy-Win11Environment.ps1 -ProfileName "Gaming" -Verbose
```

### Erreurs DNS
```powershell
# Vérifier la configuration réseau
Get-NetAdapter | Where-Object Status -eq "Up"

# Le framework configure automatiquement 9.9.9.9 et 1.1.1.1
```

---

## 🔧 Personnalisation Rapide

### Modifier un Profil Existant
```powershell
# Éditer le profil Gaming
notepad .\Profiles\Gaming.json

# Ajouter/retirer des applications
# Modifier la configuration système
```

### Créer un Profil Personnalisé
```powershell
# Copier un profil existant
Copy-Item .\Profiles\Gaming.json .\Profiles\MonProfil.json

# Éditer
notepad .\Profiles\MonProfil.json

# Utiliser
.\Deploy-Win11Environment.ps1 -ProfileName "MonProfil" -Parallel
```

---

## 📋 Checklist Post-Installation

- [ ] Vérifier le log final (0-1 échec maximum)
- [ ] Tester les applications critiques
- [ ] Vérifier DNS (9.9.9.9, 1.1.1.1)
- [ ] Redémarrer Windows (recommandé)
- [ ] Configurer Battle.net, Steam, Discord (si Gaming/Personnel)
- [ ] Vérifier mises à jour Windows
- [ ] Configurer OneDrive/Google Drive (si installés)

---

## 🎯 Cas d'Usage Courants

### PC Gaming Frais
```powershell
# 1. Installation Windows 11
# 2. Lancer Win11Forge
.\Deploy-Win11Environment.ps1 -ProfileName "Gaming" -Parallel

# 3. Attendre ~10 minutes
# 4. Redémarrer
# 5. Configurer Steam, Battle.net, Discord
```

### Workstation Développeur
```powershell
# 1. Profile Personnel (le plus complet)
.\Deploy-Win11Environment.ps1 -ProfileName "Personnel" -Parallel

# Inclut:
# - Navigateurs (Chrome, Firefox, Brave)
# - Dev tools (VS Code, Git, Python, Node.js, .NET SDK)
# - Outils réseau (Wireshark, PuTTY, WinSCP)
# - Cloud (Google Drive, pCloud, Proton suite)
# - Virtualisation (si physique)
```

### PC Bureau Entreprise
```powershell
# Profile Office (productivité)
.\Deploy-Win11Environment.ps1 -ProfileName "Office" -Parallel

# Inclut:
# - Base (diagnostics, sécurité)
# - Office 365
# - PDF-XChange Editor
# - Signal, WhatsApp
# - OBS Studio
```

---

## 📞 Support & Documentation

**Documentation Complète** :
- 📖 [README.md](../README.md) - Vue d'ensemble
- 📋 [CHANGELOG.md](../CHANGELOG.md) - Historique des versions
- ✅ [VALIDATION_REPORT.md](../VALIDATION_REPORT.md) - Tests et validations
- 🐛 [DEBUG_REPORT.md](../DEBUG_REPORT.md) - Corrections appliquées
- 🏗️ [framework_structure.md](framework_structure.md) - Architecture détaillée

**Scripts de Validation** :
```powershell
# Valider les IDs d'applications
.\Debug-FailedApps.ps1

# Tester Battle.net silent install
.\Test-BattleNet-Silent.ps1
```

---

**Bon déploiement !** 🚀

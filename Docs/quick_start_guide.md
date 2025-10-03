# Win11Forge - Quick Start Guide

🚀 **Guide d'installation rapide pour démarrer en 5 minutes**

## 📋 Prérequis

- ✅ Windows 11 24H2 (installation fraîche recommandée)
- ✅ Compte administrateur
- ✅ Connexion Internet
- ✅ ~2 GB d'espace disque libre

## 🎯 Installation en 3 étapes

### Étape 1 : Créer la structure

Ouvrez PowerShell en tant qu'**Administrateur** et exécutez :

```powershell
# Créer le répertoire principal
New-Item -ItemType Directory -Path "C:\Win11Forge" -Force

# Créer les sous-répertoires
@('Core', 'Modules', 'Profiles', 'Logs') | ForEach-Object {
    New-Item -ItemType Directory -Path "C:\Win11Forge\$_" -Force
}

Write-Host "✓ Structure créée avec succès!" -ForegroundColor Green
```

### Étape 2 : Copier les fichiers

Copiez tous les fichiers du framework dans leur emplacement :

```
C:\Win11Forge\
├── Deploy-Win11Forge.bat
├── Deploy-Win11Environment.ps1
├── README.md
├── Validate-Framework.ps1
│
├── Core\
│   └── Core.psm1
│
├── Modules\
│   ├── Prerequisites.psm1
│   ├── EnvironmentDetection.psm1
│   ├── ProfileManager.psm1
│   ├── InstallationEngine.psm1
│   └── SystemConfig.psm1
│
└── Profiles\
    ├── Base.json
    ├── Office.json
    ├── Gaming.json
    └── Personnel.json
```

**Méthode rapide via PowerShell :**

```powershell
# Si vous avez les fichiers dans un dossier temporaire
$sourceDir = "D:\Win11Forge_Files"  # Ajustez le chemin
$destDir = "C:\Win11Forge"

Copy-Item -Path "$sourceDir\*" -Destination $destDir -Recurse -Force
```

### Étape 3 : Valider l'installation

```powershell
cd C:\Win11Forge
.\Validate-Framework.ps1 -Detailed
```

Si tous les tests passent, vous êtes prêt ! ✅

## 🎮 Premier déploiement

### Test en Sandbox (recommandé pour le 1er essai)

1. Activez Windows Sandbox si pas déjà fait :
```powershell
Enable-WindowsOptionalFeature -FeatureName "Containers-DisposableClientVM" -Online -NoRestart
```

2. Copiez le dossier Win11Forge dans le Sandbox

3. Dans le Sandbox, ouvrez CMD en Admin et lancez :
```cmd
cd C:\Users\WDAGUtilityAccount\Desktop\Win11Forge
Deploy-Win11Forge.bat
```

4. Sélectionnez **6. Test Mode** pour un test sans installation

### Déploiement réel

#### Option A : Menu interactif (recommandé pour débutants)

```cmd
# Clic droit sur Deploy-Win11Forge.bat → Exécuter en tant qu'administrateur
# Ou en ligne de commande :
C:\Win11Forge\Deploy-Win11Forge.bat
```

Choisissez votre profil :
- **1. Base** : Outils essentiels (31 apps)
- **2. Office** : Base + Office (36 apps)
- **3. Gaming** : Office + Gaming (40 apps)
- **4. Personnel** : Gaming + Dev tools (73 apps)

#### Option B : PowerShell direct (pour utilisateurs avancés)

```powershell
cd C:\Win11Forge

# Profil Base
.\Deploy-Win11Environment.ps1 -ProfileName "Base"

# Profil Gaming avec logs détaillés
.\Deploy-Win11Environment.ps1 -ProfileName "Gaming" -Verbose

# Mode test (dry-run)
.\Deploy-Win11Environment.ps1 -ProfileName "Office" -TestMode

# Forcer la réinstallation
.\Deploy-Win11Environment.ps1 -ProfileName "Personnel" -Force
```

## 📊 Que se passe-t-il pendant le déploiement ?

### Phase 1 : Détection (30 sec)
- Détection de l'environnement (Sandbox/VM/PC)
- Vérification des prérequis

### Phase 2 : Prérequis (5-10 min)
- Installation de Chocolatey
- Installation de PowerShell 7
- Installation des runtimes (.NET, VC++, Java)
- Rafraîchissement de l'environnement

### Phase 3 : Applications (variable)
- Installation des applications selon le profil
- Tentative Winget → Chocolatey → Store → Direct Download
- Exclusion automatique selon environnement

### Phase 4 : Configuration (2 min)
- Configuration Windows Explorer
- Configuration Taskbar
- Configuration DNS
- Paramètres de confidentialité
- Optimisations de performance

## 📝 Exemples d'utilisation

### Déploiement standard pour poste bureautique

```powershell
.\Deploy-Win11Environment.ps1 -ProfileName "Office"
```

**Résultat attendu :**
- 36 applications installées
- DNS configuré (9.9.9.9, 1.1.1.1)
- Fichiers cachés visibles
- Extensions affichées
- Télémétrie désactivée

**Durée :** ~25 minutes

### Déploiement gaming complet

```powershell
.\Deploy-Win11Environment.ps1 -ProfileName "Gaming" -Verbose
```

**Résultat attendu :**
- 40 applications installées
- Steam, Discord, Epic Games Store
- Mode Game activé
- Plan d'alimentation High Performance
- Optimisations réseau gaming

**Durée :** ~35 minutes

### Déploiement développeur full-stack

```powershell
.\Deploy-Win11Environment.ps1 -ProfileName "Personnel" -Force
```

**Résultat attendu :**
- 73 applications installées
- VS Code, Git, Python, Node.js
- Docker, WSL
- Outils réseau (Wireshark, PuTTY)
- Suite Proton (VPN, Drive, Mail)
- Mode développeur activé

**Durée :** ~60 minutes

## 🔧 Personnalisation rapide

### Créer un profil personnalisé

1. Copiez un profil existant :
```powershell
Copy-Item "C:\Win11Forge\Profiles\Base.json" "C:\Win11Forge\Profiles\MonProfil.json"
```

2. Éditez `MonProfil.json` avec Notepad++ :
```json
{
    "Name": "MonProfil",
    "Description": "Mon profil personnalisé",
    "Version": "1.0.0",
    "Inherits": ["Base"],
    "Applications": [
        {
            "Name": "Notion",
            "Priority": 100,
            "Required": false,
            "Category": "Productivity",
            "Sources": {
                "Winget": "Notion.Notion",
                "Chocolatey": null,
                "Store": null,
                "DirectUrl": null
            },
            "EnvironmentRestrictions": []
        }
    ]
}
```

3. Déployez votre profil :
```powershell
.\Deploy-Win11Environment.ps1 -ProfileName "MonProfil"
```

## 🐛 Dépannage rapide

### Erreur : "Module not found"

```powershell
# Vérifier la structure
ls C:\Win11Forge\Core
ls C:\Win11Forge\Modules

# Si manquant, recopiez les fichiers
```

### Erreur : "Administrator privileges required"

```powershell
# Relancer en admin
Start-Process powershell -Verb RunAs -ArgumentList "-File C:\Win11Forge\Deploy-Win11Environment.ps1"
```

### Erreur : "Execution policy"

```powershell
# Débloquer temporairement
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
.\Deploy-Win11Environment.ps1 -ProfileName "Base"
```

### Application non installée

```powershell
# Vérifier les logs
notepad C:\Win11Forge\Logs\deployment_*.log

# Chercher l'application spécifique
Select-String -Path "C:\Win11Forge\Logs\deployment_*.log" -Pattern "NomApplication"
```

### Internet lent ou timeout

```powershell
# Utiliser seulement Chocolatey (parfois plus rapide)
# Modifier temporairement dans Base.json :
# Supprimer les IDs Winget, garder seulement Chocolatey
```

## 📈 Monitoring en temps réel

Pendant le déploiement, ouvrez un second PowerShell :

```powershell
# Suivre le log en direct
Get-Content "C:\Win11Forge\Logs\deployment_*.log" -Wait -Tail 50
```

## ✅ Checklist post-déploiement

- [ ] Toutes les applications installées avec succès
- [ ] Vérifier le log final pour erreurs
- [ ] Tester les applications critiques
- [ ] Vérifier la configuration réseau (DNS)
- [ ] Redémarrer le PC
- [ ] Vérifier Windows Update
- [ ] Activer Windows Defender (si désactivé)

## 🎓 Commandes utiles

```powershell
# Voir les applications installées via Winget
winget list

# Voir les packages Chocolatey
choco list --local-only

# Vérifier la version PowerShell
$PSVersionTable

# Vérifier les runtimes .NET
dotnet --list-runtimes

# Tester Java
java -version

# Voir les logs récents
ls C:\Win11Forge\Logs | Sort-Object LastWriteTime -Descending | Select-Object -First 5
```

## 🚀 Aller plus loin

- Lire le **README.md** complet pour toutes les fonctionnalités
- Consulter **STRUCTURE.md** pour l'architecture détaillée
- Contribuer : créer vos propres profils et partager !

## 💡 Astuces Pro

### Déploiement silencieux complet

```powershell
# Créer un script de déploiement automatique
$script = @'
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass -Force
C:\Win11Forge\Deploy-Win11Environment.ps1 -ProfileName "Gaming" -Verbose *> C:\Win11Forge\auto_deploy.log
'@

$script | Out-File "C:\Win11Forge\AutoDeploy.ps1"

# Programmer au démarrage (Task Scheduler)
```

### Backup avant déploiement

```powershell
# Créer un point de restauration système
Checkpoint-Computer -Description "Before Win11Forge deployment" -RestorePointType "MODIFY_SETTINGS"
```

### Déploiement sur plusieurs machines

```powershell
# Créer un package
Compress-Archive -Path "C:\Win11Forge" -DestinationPath "C:\Win11Forge_Package.zip"

# Copier sur d'autres machines via réseau
Copy-Item "C:\Win11Forge_Package.zip" "\\MACHINE2\C$\Temp\"
```

---

**Besoin d'aide ?** Consultez les logs détaillés dans `C:\Win11Forge\Logs\`

**Version :** 2.0.0  
**Dernière mise à jour :** 2025-01-15  

**🎉 Bon déploiement !**

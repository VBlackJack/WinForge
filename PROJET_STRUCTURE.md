# 📁 Win11Forge v2.2.0 - Structure du Projet

## 🎯 Scripts Principaux (Racine)

### **Déploiement**
- **`Deploy-Win11Forge.bat`** ⭐
  - Déploiement rapide avec interface console
  - Auto-élévation admin
  - Installation auto PowerShell 7 si manquant
  - Choix du profil (Base/Office/Gaming/Personnel)
  - Mode parallèle disponible

- **`Start-Win11ForgeGUI.ps1`** ⭐
  - Interface GUI PowerShell complète
  - Navigation par menus interactifs
  - Gestion des profils, applications, base de données

- **`Start-Win11ForgeGUI-Admin.bat`** ⭐
  - Lanceur avec auto-élévation admin pour le GUI
  - Double-clic et c'est parti !

- **`Deploy-Win11Environment.ps1`**
  - Script principal de déploiement
  - Appelé par les lanceurs ci-dessus
  - Ne pas exécuter directement

### **Maintenance**
- **`Setup-Framework.ps1`**
  - Installation initiale du framework
  - Vérification des dépendances

- **`Cleanup-Framework.ps1`**
  - Nettoyage des logs et fichiers temporaires

- **`Cleanup-ObsoleteFiles.ps1`**
  - Archive les fichiers obsolètes après migration
  - Mode `-DryRun` disponible
  - Nettoie automatiquement les fichiers de test/migration

## 📂 Structure des Dossiers

```
Win11Forge/
│
├── 🚀 Scripts de Lancement (voir ci-dessus)
│
├── 📦 Apps/
│   └── Database/
│       └── applications.json          # Base de données centralisée (67 apps)
│
├── 📋 Profiles/                       # Profils de déploiement v2.2.0
│   ├── Base.json                      # 31 apps essentielles
│   ├── Office.json                    # Base + 5 apps bureautique
│   ├── Gaming.json                    # Office + 4 apps gaming
│   └── Personnel.json                 # Gaming + développement (total 66 apps)
│
├── 🧩 Modules/                        # Modules PowerShell
│   ├── ApplicationDatabase.psm1       # Gestion de la base de données
│   ├── EnvironmentDetection.psm1     # Détection environnement (VM, Sandbox, etc.)
│   ├── InstallationEngine.psm1       # Moteur d'installation (Winget, Choco, Store)
│   ├── Prerequisites.psm1            # Vérification prérequis
│   ├── ProfileManager.psm1           # Gestion des profils avec héritage
│   ├── SystemConfig.psm1             # Configuration système (Explorer, Taskbar, etc.)
│   └── Win11ForgeGUI.psm1            # Interface graphique PowerShell
│
├── 🛠️ Tools/                          # Outils utilitaires
│   ├── ProfileCreator.html           # Créateur de profils web (67 apps, file://)
│   ├── applications-data.js          # Base de données pour ProfileCreator
│   ├── Search-ApplicationSources.ps1 # Recherche app dans stores
│   ├── Debug-FailedApps.ps1          # Debug des échecs d'installation
│   ├── Test-BattleNet-Silent.ps1     # Test installation Battle.net
│   └── Validate-AppDatabase.ps1      # Validation de la base de données
│
├── 📚 Docs/                           # Documentation
│   └── profile_template.json         # Template de profil v2.2.0
│
├── 🗂️ Archive/                        # Fichiers archivés
│   ├── Profiles-v2.0-*/              # Anciens profils v2.0 (avant migration)
│   └── Test-*.ps1                    # Scripts de test archivés
│
├── 📊 Logs/                           # Logs de déploiement
│   └── deployment_*.log              # Logs horodatés
│
└── ⚙️ Core/
    └── Core.psm1                     # Fonctions core du framework
```

## 🎮 Utilisation Rapide

### **Déploiement avec GUI (Recommandé)**
```batch
# Double-clic sur :
Start-Win11ForgeGUI-Admin.bat
```

### **Déploiement Console Rapide**
```batch
# Double-clic sur :
Deploy-Win11Forge.bat

# Ou en ligne de commande :
.\Deploy-Win11Forge.bat
# Puis choisir :
# 1 = Base (31 apps)
# 2 = Office (36 apps)
# 3 = Gaming (40 apps)
# 4 = Personnel (66 apps)
```

### **Créer un Profil Custom**
```batch
# Ouvrir dans un navigateur :
Tools\ProfileCreator.html
# Ou utiliser le GUI (option 4)
```

### **Ajouter une Application**
```powershell
# Via GUI :
Start-Win11ForgeGUI-Admin.bat → Option 8

# Via script :
.\Tools\Search-ApplicationSources.ps1 -AppName "Discord"
```

## 📊 Base de Données

**Location:** `Apps/Database/applications.json`

**Format v2.2.0:**
- **67 applications** référencées
- Sources multiples : Winget, Chocolatey, Microsoft Store, DirectUrl
- Métadonnées complètes : détection, priorité, tags, vérification
- Compatible avec ProfileCreator.html

## 🔄 Profils et Héritage

Les profils utilisent l'héritage pour éviter la duplication :

```
Base (31 apps)
  ↓ hérite
Office (+ 5 apps = 36 total)
  ↓ hérite
Gaming (+ 4 apps = 40 total)
  ↓ hérite
Personnel (+ 26 apps = 66 total)
```

**Format des profils v2.2.0:**
```json
{
  "Name": "Gaming",
  "Inherits": ["Office"],
  "Applications": [
    "Steam",
    "Discord",
    "EpicGamesLauncher",
    "BattleNet"
  ]
}
```

## 🧹 Nettoyage

```powershell
# Voir ce qui serait nettoyé :
.\Cleanup-ObsoleteFiles.ps1 -DryRun

# Nettoyer effectivement :
.\Cleanup-ObsoleteFiles.ps1

# Nettoyer les logs :
.\Cleanup-Framework.ps1
```

## 🆕 Nouveautés v2.2.0

✅ Base de données centralisée (67 applications)
✅ Interface GUI PowerShell interactive
✅ ProfileCreator.html avec 67 apps dynamiques
✅ Recherche automatique dans stores (Search-ApplicationSources.ps1)
✅ Ajout d'applications via GUI
✅ Édition de profils via HTML
✅ Auto-élévation admin sur tous les lanceurs
✅ Mode parallèle (PowerShell 7+)
✅ Système d'héritage de profils optimisé

## 📝 Notes

- **PowerShell 7+** : Recommandé pour le mode parallèle (installation auto)
- **Admin requis** : Tous les lanceurs s'auto-élèvent
- **Logs** : Consultables dans `Logs/deployment_*.log`
- **Profils** : Stockés dans `Profiles/` au format JSON v2.2.0
- **Compatible file://** : ProfileCreator.html fonctionne sans serveur web

## 🔗 Liens Utiles

- GUI README : `GUI_README.md`
- Template de profil : `Docs/profile_template.json`
- Validation base de données : `.\Tools\Validate-AppDatabase.ps1`

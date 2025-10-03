# ProfileCreator.html - Guide d'Utilisation

## 📋 Vue d'ensemble

ProfileCreator.html est un outil standalone (utilisable en `file://`) pour créer des profils JSON personnalisés pour Win11Forge.

## ✨ Fonctionnalités

### 🎯 6 Étapes de Création

#### 1️⃣ Informations du Profil
- Nom du profil
- Description
- Version

#### 2️⃣ Héritage
- Hériter d'un profil existant (Base, Office, Gaming, Personnel)
- Compteur automatique des applications héritées

#### 3️⃣ Applications Prédéfinies
- Sélection d'applications depuis la base de données intégrée
- Catégories : Navigateurs, Développement, Gaming, Média, Utilitaires
- 18+ applications préconfigurées

#### 4️⃣ Applications Personnalisées ⭐ (NOUVEAU)
**Fonctionnalités** :
- Ajouter vos propres applications avec sources personnalisées
- Champs disponibles :
  - ✅ **Nom de l'application** (requis)
  - ✅ **Winget ID** (optionnel)
  - ✅ **Chocolatey Package** (optionnel)
  - ✅ **Microsoft Store ID** (optionnel)
  - ✅ **URL de téléchargement direct** (optionnel)
  - ✅ **Arguments d'installation** (optionnel)
  - ✅ **Catégorie** (sélectionnable)

**Validation** :
- Au moins une source (Winget/Choco/Store/URL) requise
- Liste des applications ajoutées avec aperçu des sources
- Bouton de suppression pour chaque app personnalisée

#### 5️⃣ Configuration Système
- **Explorer** : Fichiers cachés, extensions, navigation
- **Taskbar** : Widgets, alignement, recherche
- **Réseau** : DNS personnalisés (9.9.9.9, 1.1.1.1)
- **Confidentialité** : Télémétrie, collecte de données
- **Performance** : Services, plan d'alimentation
- **Sécurité** : Defender, Firewall

#### 6️⃣ Aperçu & Téléchargement
- Aperçu JSON complet
- Statistiques (nombre d'apps, sections config)
- Téléchargement du fichier JSON

## 🚀 Utilisation

### Méthode 1 : Double-clic
```
Double-clic sur ProfileCreator.html
```

### Méthode 2 : Navigateur
```
file:///C:/Users/User/Desktop/Win11Forge/Tools/ProfileCreator.html
```

## 📝 Exemple : Ajouter une Application Personnalisée

### Cas 1 : Application Winget + Chocolatey
```
Nom             : Postman
Winget ID       : Postman.Postman
Chocolatey      : postman
Store ID        : (vide)
URL             : (vide)
Arguments       : (vide)
Catégorie       : Development
```

### Cas 2 : Application avec URL directe et arguments
```
Nom             : MonAppli
Winget ID       : (vide)
Chocolatey      : (vide)
Store ID        : (vide)
URL             : https://example.com/setup.exe
Arguments       : /S /quiet /norestart
Catégorie       : Custom
```

### Cas 3 : Microsoft Store uniquement
```
Nom             : Windows Terminal
Winget ID       : (vide)
Chocolatey      : (vide)
Store ID        : 9N0DX20HK701
URL             : (vide)
Arguments       : (vide)
Catégorie       : Utilities
```

## 📊 Structure JSON Générée

```json
{
  "Name": "MonProfil",
  "Description": "Profil personnalisé",
  "Version": "1.0.0",
  "Inherits": ["Base"],
  "Applications": [
    {
      "Name": "MonAppli",
      "Priority": 100,
      "Required": false,
      "Category": "Custom",
      "Sources": {
        "Winget": "Publisher.App",
        "Chocolatey": "packagename",
        "Store": null,
        "DirectUrl": "https://example.com/setup.exe"
      },
      "InstallArguments": "/S /quiet",
      "Detection": {
        "Method": "Command",
        "Command": "echo Installed"
      },
      "EnvironmentRestrictions": []
    }
  ],
  "SystemConfig": { ... }
}
```

## ✅ Validation Automatique

- ✅ Nom d'application obligatoire
- ✅ Au moins une source requise
- ✅ Compteur temps réel des applications
- ✅ Aperçu avant téléchargement
- ✅ Format JSON valide garanti

## 🎨 Interface

- **Design moderne** : Gradient violet, style Fluent
- **Navigation intuitive** : Barre latérale + boutons Précédent/Suivant
- **Responsive** : Adaptatif mobile/desktop
- **Statistiques en temps réel** : Compteurs d'applications
- **Aperçu coloré** : Code JSON avec syntaxe highlight

## 📥 Déploiement du Profil Créé

1. **Télécharger le JSON** depuis l'étape 6
2. **Placer le fichier** dans `Win11Forge/Profiles/`
3. **Lancer le déploiement** :
   ```powershell
   .\Deploy-Win11Environment.ps1 -ProfileName "MonProfil"
   ```

## 🔧 Cas d'Usage

### Profil Développeur Web
```
Base + VSCode, Node.js, Git, Chrome, Firefox
+ Apps personnalisées : Postman, MongoDB Compass, Docker Desktop
```

### Profil Designer
```
Base + Adobe Creative Cloud, Figma, Sketch
+ Apps personnalisées : Fontbase, ColorSlurp, Blender
```

### Profil Gaming Avancé
```
Gaming + Discord, Steam, Battle.net
+ Apps personnalisées : MSI Afterburner, GeForce Experience, Playnite
```

## 📌 Notes Importantes

- ✅ Aucune connexion Internet requise (standalone)
- ✅ Fonctionne en mode `file://` (pas de serveur web)
- ✅ Données stockées localement (pas de cloud)
- ✅ Compatible avec tous les navigateurs modernes
- ✅ Applications personnalisées persistent dans la session

## 🆕 Nouveautés v2.1.3

- ✅ **Étape 4 ajoutée** : Applications personnalisées
- ✅ **Sources multiples** : Winget, Choco, Store, DirectUrl
- ✅ **Arguments personnalisés** : Support InstallArguments
- ✅ **Catégories** : 8 catégories prédéfinies
- ✅ **Gestion dynamique** : Ajout/Suppression d'apps custom
- ✅ **Validation robuste** : Vérification des champs obligatoires
- ✅ **Compteur amélioré** : Inclut les apps custom dans le total

---

**Version** : 2.1.3
**Date** : 2025-10-03
**Auteur** : Win11Forge Team

# Guide de contribution - Win11Forge Framework

Merci de votre intérêt pour améliorer Win11Forge ! Ce guide vous aidera à contribuer efficacement.

## 📋 Table des matières

- [Comment contribuer](#comment-contribuer)
- [Standards de code](#standards-de-code)
- [Création de profils](#création-de-profils)
- [Tests](#tests)
- [Soumission de modifications](#soumission-de-modifications)

## 🤝 Comment contribuer

### Types de contributions acceptées

1. **Nouveaux profils JSON** - Profils spécialisés pour différents cas d'usage
2. **Corrections de bugs** - Corrections dans le code PowerShell
3. **Nouvelles fonctionnalités** - Améliorations du framework
4. **Documentation** - Amélioration des guides et exemples
5. **Tests** - Rapports de tests sur différents environnements

### Avant de commencer

- [ ] Consultez les [issues existantes](https://github.com/your-repo/issues)
- [ ] Lisez la documentation complète (README.md, STRUCTURE.md)
- [ ] Testez le framework dans votre environnement
- [ ] Familiarisez-vous avec la structure du projet

## 💻 Standards de code

### PowerShell

#### Style général

```powershell
# ✅ BON - Nommage descriptif, commentaires clairs
function Install-ApplicationSafely {
    <#
    .SYNOPSIS
        Installs an application with error handling
    
    .DESCRIPTION
        Attempts installation using multiple sources with fallback
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$ApplicationName
    )
    
    # Code avec indentation cohérente (4 espaces)
    if (Test-Path $path) {
        Write-Status -Message "Found: $ApplicationName" -Level 'Success'
    }
}

# ❌ MAUVAIS - Nom vague, pas de documentation
function doStuff {
    param($app)
    # Code sans structure
    if(Test-Path $p){Write-Host "ok"}
}
```

#### Conventions de nommage

| Type | Convention | Exemple |
|------|-----------|---------|
| Fonctions | Verb-Noun (PascalCase) | `Get-DeploymentProfile` |
| Paramètres | PascalCase | `$ProfileName` |
| Variables locales | camelCase | `$totalFiles` |
| Constantes | UPPERCASE | `$MAX_RETRIES` |

#### Gestion d'erreurs

```powershell
# ✅ BON - Try-catch avec logging approprié
try {
    $result = Invoke-SomeOperation
    Write-Status -Message "Operation successful" -Level 'Success'
    return $true
} catch {
    Write-Status -Message "Operation failed: $($_.Exception.Message)" -Level 'Error'
    return $false
}

# ❌ MAUVAIS - Pas de gestion d'erreur
$result = Invoke-SomeOperation
return $true
```

#### Logging

```powershell
# ✅ BON - Messages informatifs avec niveaux appropriés
Write-Status -Message "Starting deployment phase" -Level 'Info'
Write-Status -Message "Application installed successfully" -Level 'Success'
Write-Status -Message "Source unavailable, trying fallback" -Level 'Warning'
Write-Status -Message "Installation failed: No sources available" -Level 'Error'

# ❌ MAUVAIS - Messages vagues sans contexte
Write-Host "ok"
Write-Host "error"
```

### JSON (Profils)

#### Structure

```json
{
    "Name": "ProfileName",
    "Description": "Clear, concise description",
    "Version": "1.0.0",
    "Inherits": ["Base"],
    
    "Applications": [
        {
            "Name": "Full Application Name",
            "Priority": 100,
            "Required": false,
            "Category": "Appropriate Category",
            
            "Sources": {
                "Winget": "Publisher.AppName",
                "Chocolatey": "packagename",
                "Store": "9NXXXXXXXXX",
                "DirectUrl": "https://official-site.com/download.exe"
            },
            
            "Detection": {
                "Method": "Registry",
                "Path": "HKLM:\\SOFTWARE\\AppName"
            },
            
            "EnvironmentRestrictions": []
        }
    ],
    
    "SystemConfig": {
        "Explorer": {},
        "Taskbar": {},
        "Network": {},
        "Privacy": {},
        "Performance": {},
        "Security": {}
    }
}
```

#### Bonnes pratiques JSON

✅ **À FAIRE :**
- Utiliser des noms d'applications officiels complets
- Fournir plusieurs sources d'installation quand possible
- Tester tous les IDs Winget/Chocolatey avant soumission
- Ajouter des détections appropriées
- Documenter les restrictions d'environnement
- Maintenir l'ordre alphabétique dans les catégories

❌ **À ÉVITER :**
- Noms abrégés ou ambigus ("VS" au lieu de "Visual Studio")
- Sources non vérifiées ou obsolètes
- Priorités en doublon
- DirectUrl vers sites non officiels

## 📦 Création de profils

### Process de création

1. **Copier le template**
```powershell
Copy-Item "Profiles\Template_Profile.json" "Profiles\MonNouveauProfil.json"
```

2. **Définir le profil**
```json
{
    "Name": "DataScience",
    "Description": "Data Science workstation with Python, R, Jupyter",
    "Version": "1.0.0",
    "Inherits": ["Base", "Office"]
}
```

3. **Ajouter les applications**
- Rechercher les IDs Winget : `winget search "Application Name"`
- Vérifier Chocolatey : `choco search appname`
- Tester l'installation manuellement d'abord

4. **Tester le profil**
```powershell
# Test sans installation
.\Deploy-Win11Environment.ps1 -ProfileName "DataScience" -TestMode

# Test réel en Sandbox
# (Copiez le framework dans Sandbox et testez)

# Test en VM
# (Créez un snapshot, testez, restaurez)
```

5. **Valider**
```powershell
.\Validate-Framework.ps1 -Detailed
```

### Exemples de profils contributifs

#### Profil "WebDev" (Développement web)

```json
{
    "Name": "WebDev",
    "Description": "Web development environment - Node.js, VS Code, Chrome DevTools",
    "Version": "1.0.0",
    "Inherits": ["Office"],
    
    "Applications": [
        {
            "Name": "Node.js LTS",
            "Priority": 100,
            "Required": true,
            "Category": "Development",
            "Sources": {
                "Winget": "OpenJS.NodeJS.LTS",
                "Chocolatey": "nodejs-lts"
            }
        },
        {
            "Name": "Visual Studio Code",
            "Priority": 101,
            "Required": true,
            "Category": "Development",
            "Sources": {
                "Winget": "Microsoft.VisualStudioCode",
                "Chocolatey": "vscode"
            }
        },
        {
            "Name": "Postman",
            "Priority": 102,
            "Required": false,
            "Category": "Development",
            "Sources": {
                "Winget": "Postman.Postman",
                "Chocolatey": "postman"
            }
        }
    ]
}
```

#### Profil "MediaCreator" (Création multimédia)

```json
{
    "Name": "MediaCreator",
    "Description": "Content creation suite - Adobe alternatives, audio/video editing",
    "Version": "1.0.0",
    "Inherits": ["Base"],
    
    "Applications": [
        {
            "Name": "GIMP",
            "Priority": 100,
            "Required": true,
            "Category": "Graphics",
            "Sources": {
                "Winget": "GIMP.GIMP",
                "Chocolatey": "gimp"
            }
        },
        {
            "Name": "Inkscape",
            "Priority": 101,
            "Required": false,
            "Category": "Graphics",
            "Sources": {
                "Winget": "Inkscape.Inkscape",
                "Chocolatey": "inkscape"
            }
        },
        {
            "Name": "Audacity",
            "Priority": 102,
            "Required": true,
            "Category": "Audio",
            "Sources": {
                "Winget": "Audacity.Audacity",
                "Chocolatey": "audacity"
            }
        },
        {
            "Name": "OBS Studio",
            "Priority": 103,
            "Required": true,
            "Category": "Video",
            "Sources": {
                "Winget": "OBSProject.OBSStudio",
                "Chocolatey": "obs-studio"
            }
        }
    ],
    
    "SystemConfig": {
        "Performance": {
            "PowerPlan": "High Performance",
            "DisableVisualEffects": false
        }
    }
}
```

## 🧪 Tests

### Tests requis avant soumission

1. **Validation syntaxe**
```powershell
.\Validate-Framework.ps1 -Detailed
```

2. **Test Mode**
```powershell
.\Deploy-Win11Environment.ps1 -ProfileName "VotreProfil" -TestMode
```

3. **Test en Sandbox**
- Copier le framework dans Windows Sandbox
- Exécuter un déploiement complet
- Vérifier le log pour erreurs

4. **Test en VM** (recommandé)
- Créer un snapshot avant test
- Déployer le profil
- Vérifier toutes les applications installées
- Tester les fonctionnalités de base
- Restaurer le snapshot

### Rapport de test

Incluez ces informations dans votre pull request :

```markdown
## Tests effectués

**Environnement :**
- OS : Windows 11 24H2 (Build 22631.xxxx)
- PowerShell : 7.4.x
- Type : Physical / VM / Sandbox

**Profil testé :** NomDuProfil

**Résultats :**
- ✅ Validation framework réussie
- ✅ Test Mode réussi
- ✅ Déploiement Sandbox réussi
- ✅ Déploiement VM réussi
- ⚠️ 2 applications ont échoué (voir détails)

**Applications installées :** 25/27
**Applications échouées :**
- Application X : Source Winget indisponible (Chocolatey OK)
- Application Y : Restriction VM appliquée correctement

**Durée totale :** 35 minutes

**Logs :** [Joindre le fichier log]
```

## 📝 Soumission de modifications

### Workflow Git

1. **Fork le repository**

2. **Créer une branche**
```bash
git checkout -b feature/nouveau-profil-datascience
# ou
git checkout -b fix/correction-detection-chrome
```

3. **Faire vos modifications**
- Suivre les standards de code
- Tester exhaustivement
- Documenter les changements

4. **Commit avec message descriptif**
```bash
git add .
git commit -m "feat: Add DataScience profile with Python, R, Jupyter

- Added 15 data science applications
- Configured Anaconda environment
- Tested on Windows 11 24H2
- All applications install successfully"
```

### Format des messages de commit

Utilisez [Conventional Commits](https://www.conventionalcommits.org/):

- `feat:` Nouvelle fonctionnalité
- `fix:` Correction de bug
- `docs:` Documentation uniquement
- `style:` Formatage, pas de changement de code
- `refactor:` Refactorisation sans changement fonctionnel
- `test:` Ajout/modification de tests
- `chore:` Maintenance, tâches build

### Pull Request

Créez une PR avec :

1. **Titre descriptif**
```
feat: Add MediaCreator profile for content creators
```

2. **Description détaillée**
```markdown
## Description
Ajout d'un profil MediaCreator pour créateurs de contenu avec :
- GIMP, Inkscape (graphisme)
- Audacity (audio)
- OBS Studio (streaming/recording)
- Configuration performance optimisée

## Type de changement
- [x] Nouveau profil
- [ ] Correction de bug
- [ ] Amélioration fonctionnalité
- [ ] Documentation

## Tests
- [x] Validation framework
- [x] Test Mode
- [x] Windows Sandbox
- [x] VM Windows 11
- [x] PC physique

## Checklist
- [x] Code suit les standards
- [x] Tests passent
- [x] Documentation mise à jour
- [x] JSON validé
- [x] Logs de test joints
```

3. **Joindre les logs de test**

## 🔍 Review Process

### Critères de review

Votre contribution sera évaluée sur :

1. **Qualité du code**
   - Respect des conventions
   - Gestion d'erreurs appropriée
   - Commentaires pertinents

2. **Fonctionnalité**
   - Teste dans tous les environnements
   - Pas de régression
   - Bonne intégration

3. **Documentation**
   - README mis à jour si nécessaire
   - Commentaires de code clairs
   - Rapport de tests complet

4. **Tests**
   - Tests Sandbox réussis
   - Tests VM réussis
   - Pas d'erreurs critiques

## 📚 Ressources

- [PowerShell Best Practices](https://poshcode.gitbooks.io/powershell-practice-and-style/)
- [JSON Schema](https://json-schema.org/)
- [Winget Package Search](https://winget.run/)
- [Chocolatey Packages](https://community.chocolatey.org/packages)

## 💡 Idées de contributions

### Profils recherchés

- **Cybersecurity** : Outils pentesting, forensics
- **CAD/Engineering** : AutoCAD, SolidWorks alternatives
- **Scientific** : MATLAB alternatives, LaTeX
- **Gaming Streaming** : StreamElements, Streamlabs
- **Education** : Outils enseignement à distance
- **Business** : CRM, ERP, comptabilité

### Améliorations techniques

- Support macOS/Linux
- Interface graphique (GUI)
- API REST
- Intégration CI/CD
- Tests automatisés
- Performance optimizations

## ❓ Questions ?

- Ouvrez une [issue](https://github.com/your-repo/issues)
- Consultez les [discussions](https://github.com/your-repo/discussions)
- Lisez la [documentation complète](README.md)

---

**Merci de contribuer à Win11Forge ! 🚀**

Ensemble, créons le meilleur framework de déploiement Windows !

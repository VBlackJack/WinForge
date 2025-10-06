# Win11Forge Tests - v2.5.0

## 📋 Vue d'ensemble

Suite de tests Pester pour Win11Forge Framework v2.5.0, visant une couverture minimale de 50%.

**Version** : 2.5.0
**Framework de test** : Pester v5+
**Couverture cible** : 50% minimum

---

## 🚀 Installation rapide

### 1. Installer Pester v5+

```powershell
.\Install-Pester.ps1
```

Ou manuellement :

```powershell
Install-Module -Name Pester -MinimumVersion 5.0.0 -Force -SkipPublisherCheck
```

### 2. Exécuter les tests

```powershell
# Tests simples
.\Invoke-Tests.ps1

# Tests avec couverture de code
.\Invoke-Tests.ps1 -Coverage

# Tests avec rapport NUnit
.\Invoke-Tests.ps1 -OutputFormat NUnitXml
```

---

## 📂 Structure

```
Tests/
├── README.md                            # Cette documentation
├── Install-Pester.ps1                   # Installation Pester v5+
├── Invoke-Tests.ps1                     # Lanceur de tests principal
├── InstallationEngine.Tests.ps1         # Tests InstallationEngine (PRIORITÉ 1)
├── ApplicationDatabase.Tests.ps1        # Tests ApplicationDatabase (PRIORITÉ 2)
├── ProfileManager.Tests.ps1             # Tests ProfileManager (À venir)
├── EnvironmentDetection.Tests.ps1       # Tests EnvironmentDetection (À venir)
└── Results/                             # Résultats de tests (auto-généré)
    ├── TestResults_*.xml
    └── Coverage_*.xml
```

---

## 🧪 Modules testés

### ✅ **InstallationEngine.psm1** (PRIORITÉ 1 - CRITIQUE)

**Couverture** : ~40-50% (estimation)
**Fonctions testées** :
- `Test-ValidDownloadUrl` - Validation sécurité URLs
- `Start-ProcessWithTimeout` - Timeout protection
- `Test-RegistryKey` - Détection Registry
- `Test-ApplicationInstalled` - Détection installation
- `Test-ApplicationByName` - Détection par nom
- `Install-Application` - Installation (tests d'intégration)
- `Install-WindowsFeature` - Windows Features
- `Install-WindowsCapability` - Windows Capabilities

**Tests de sécurité** :
- Validation URLs (whitelist, HTTPS only)
- Protection command injection
- Timeout enforcement

**Tests de performance** :
- Timeout protection (<8 secondes pour 3 sec timeout)

### ✅ **ApplicationDatabase.psm1** (PRIORITÉ 2 - HAUTE)

**Couverture** : ~60-70% (estimation)
**Fonctions testées** :
- `Get-ApplicationDatabase` - Chargement database
- `Get-ApplicationById` - Récupération par ID
- `Get-AllApplications` - Liste complète + filtres
- `Search-Applications` - Recherche
- `ConvertTo-ProfileApplication` - Conversion format
- `Get-ApplicationCategories` - Liste catégories
- `Get-ApplicationTags` - Liste tags
- `Get-DatabaseStatistics` - Statistiques
- `Reset-DatabaseCache` - Gestion cache

**Tests d'intégrité** :
- Structure JSON valide
- Propriétés requises présentes
- Cohérence TotalApplications
- Au moins une source par app

**Tests de performance** :
- Chargement <2 secondes
- Cache 50% plus rapide
- Recherche <1 seconde

---

## 📊 Exécution des tests

### Commandes disponibles

```powershell
# 1. Tests basiques
.\Invoke-Tests.ps1

# 2. Tests avec couverture
.\Invoke-Tests.ps1 -Coverage

# 3. Tests avec rapport XML (NUnit/JUnit)
.\Invoke-Tests.ps1 -OutputFormat NUnitXml
.\Invoke-Tests.ps1 -OutputFormat JUnitXml

# 4. Tests complets (couverture + rapport)
.\Invoke-Tests.ps1 -Coverage -OutputFormat NUnitXml
```

### Sortie attendue

```
═══════════════════════════════════════════════
  Win11Forge v2.5.0 - Test Runner
═══════════════════════════════════════════════

✅ Pester v5.6.1 found

══════════════════════════════════════════════════
  Running Tests
══════════════════════════════════════════════════

Running tests from 'C:\Win11Forge\Tests'
Describing InstallationEngine Module
  Context Module Loading
    [+] Should load without errors 42ms (25ms|17ms)
    [+] Should export Install-Application function 8ms (7ms|1ms)
    ...

══════════════════════════════════════════════════
  Test Results Summary
══════════════════════════════════════════════════

Total Tests      : 85
Passed           : 85
Failed           : 0
Skipped          : 0
Duration         : 12.34 seconds

══════════════════════════════════════════════════
  Code Coverage Summary
══════════════════════════════════════════════════

Coverage         : 52.3%
Commands Covered : 234 / 447

✅ Coverage target (50%) achieved!

✅ All tests PASSED
```

---

## 🎯 Objectifs de couverture v2.5.0

| Module | Couverture cible | Status |
|--------|------------------|--------|
| InstallationEngine.psm1 | 45% | ✅ Implémenté |
| ApplicationDatabase.psm1 | 65% | ✅ Implémenté |
| ProfileManager.psm1 | 40% | 🔄 À venir |
| EnvironmentDetection.psm1 | 50% | 🔄 À venir |
| **TOTAL** | **50%** | 🔄 En cours |

---

## 🔧 Développement de tests

### Ajouter de nouveaux tests

1. Créer un fichier `Module.Tests.ps1` dans `Tests/`
2. Utiliser la structure standard :

```powershell
BeforeAll {
    $ModulePath = Join-Path $PSScriptRoot '..\Modules\Module.psm1'
    Import-Module $ModulePath -Force
}

Describe 'Module Name' {
    Context 'Feature Group' {
        It 'Should do something' {
            # Arrange
            $input = 'test'

            # Act
            $result = Test-Function -Input $input

            # Assert
            $result | Should -Be 'expected'
        }
    }
}
```

### Best practices

1. **Tests unitaires** : Tester une fonction à la fois
2. **Tests d'intégration** : Tester interactions entre fonctions
3. **AAA pattern** : Arrange, Act, Assert
4. **Isolation** : Tests indépendants (pas d'état partagé)
5. **Mocking** : Simuler dépendances externes si nécessaire

---

## 🐛 Dépannage

### Pester v3 installé au lieu de v5

```powershell
# Désinstaller Pester v3
Uninstall-Module -Name Pester -AllVersions -Force

# Installer Pester v5
Install-Module -Name Pester -MinimumVersion 5.0.0 -Force -SkipPublisherCheck
```

### Tests échouent avec "Module not found"

```powershell
# Vérifier chemins relatifs dans tests
$ModulePath = Join-Path $PSScriptRoot '..\Modules\Module.psm1'
Test-Path $ModulePath  # Doit retourner True
```

### Coverage ne fonctionne pas

Pester v5+ requis pour code coverage. Vérifier version :

```powershell
(Get-Module Pester -ListAvailable).Version  # Doit être >= 5.0.0
```

---

## 📈 CI/CD Integration (v2.6.0)

Structure prête pour GitHub Actions :

```yaml
# .github/workflows/tests.yml
name: Tests

on: [push, pull_request]

jobs:
  test:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v3
      - name: Install Pester
        run: .\Tests\Install-Pester.ps1
      - name: Run Tests
        run: .\Tests\Invoke-Tests.ps1 -Coverage -OutputFormat NUnitXml
      - name: Upload Results
        uses: actions/upload-artifact@v3
        with:
          name: test-results
          path: Tests/Results/
```

---

## 📝 Changelog

### v2.5.0 (2025-10-06)
- ✅ Infrastructure Pester v5+ créée
- ✅ Tests InstallationEngine.psm1 (85 tests)
- ✅ Tests ApplicationDatabase.psm1 (60+ tests)
- ✅ Lanceur de tests avec couverture
- ✅ Documentation complète

### v2.6.0 (Prévu)
- 🔄 Tests ProfileManager.psm1
- 🔄 Tests EnvironmentDetection.psm1
- 🔄 CI/CD GitHub Actions
- 🔄 Coverage >60%

---

**Version** : 2.5.0
**Date** : 2025-10-06
**Statut** : 🔄 En cours (Infrastructure complète, tests en implémentation)

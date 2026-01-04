# Win11Forge v3.0.0 - Cahier des Charges & Roadmap

## Executive Summary

Win11Forge v3.0.0 introduit une interface graphique moderne pour remplacer/compléter l'interface console actuelle. L'objectif est de rendre le framework accessible aux utilisateurs non-techniques tout en conservant la puissance du moteur d'installation existant.

---

## 1. Analyse de l'Existant (v2.6.0)

### 1.1 Architecture Actuelle

```
Win11Forge v2.6.0
├── Core Engine (stable, 479 tests)
│   ├── InstallationEngine.psm1 (2352 lignes, 18 fonctions)
│   ├── ProfileManager.psm1 (761 lignes, 11 fonctions)
│   ├── ApplicationDatabase.psm1 (10 fonctions)
│   └── 8 autres modules
├── Console GUI (Win11ForgeGUI.psm1)
│   └── 30 fonctions (menus textuels)
├── Database
│   └── 66 applications définies
└── Profiles
    └── 4 profils (Base → Office → Gaming → Personnel)
```

### 1.2 Points Forts à Conserver
- Moteur d'installation parallèle performant
- Système de retry avec backoff exponentiel
- Validation SHA256 des téléchargements
- Héritage de profils flexible
- Détection d'environnement (VM/Physical)
- Système de rollback et reprise après crash

### 1.3 Limitations Actuelles
- Interface console uniquement
- Pas de visualisation de progression en temps réel
- Configuration manuelle des profils (JSON)
- Pas de prévisualisation des changements
- Pas d'historique des déploiements

---

## 2. Objectifs v3.0.0

### 2.1 Objectifs Principaux
1. **Accessibilité** - Interface utilisable sans connaissances PowerShell
2. **Visualisation** - Progression en temps réel des installations
3. **Personnalisation** - Éditeur de profils intégré
4. **Fiabilité** - Prévisualisation et confirmation avant action
5. **Traçabilité** - Historique complet des déploiements

### 2.2 Objectifs Secondaires
- Mode sombre/clair
- Support multi-langue (FR/EN)
- Export de rapports (HTML/PDF)
- Intégration Windows Task Scheduler
- Notifications système

---

## 3. Choix Technologiques

### 3.1 Options Évaluées

| Technologie | Avantages | Inconvénients | Score |
|-------------|-----------|---------------|-------|
| **WPF + XAML** | Natif Windows, riche, mature | Courbe apprentissage, verbeux | 7/10 |
| **WinForms** | Simple, rapide | Vieillissant, moins flexible | 5/10 |
| **Avalonia** | Cross-platform, moderne | Dépendance externe, moins mature | 6/10 |
| **Terminal.Gui** | Console améliorée, léger | Limité graphiquement | 4/10 |
| **Electron + HTML** | Moderne, flexible | Lourd (Chromium), complexe | 5/10 |
| **MAUI** | Cross-platform, moderne | Nouveau, moins stable | 6/10 |

### 3.2 Recommandation: WPF + XAML

**Justification:**
- Natif Windows (pas de dépendances externes)
- Intégration PowerShell native
- Data binding puissant pour la progression
- Styles modernes disponibles (MaterialDesign, MahApps)
- Large communauté et documentation
- Performances optimales sur Windows 11

### 3.3 Architecture Proposée

```
Win11Forge v3.0.0
├── Core/ (existant, inchangé)
├── Modules/ (existant, inchangé)
├── GUI/
│   ├── Win11Forge.GUI/           # Projet WPF principal
│   │   ├── Views/                # XAML views
│   │   ├── ViewModels/           # MVVM ViewModels
│   │   ├── Models/               # Data models
│   │   ├── Services/             # Services (PowerShell bridge)
│   │   ├── Converters/           # Value converters
│   │   └── Resources/            # Styles, thèmes, i18n
│   └── Win11Forge.GUI.Tests/     # Tests unitaires GUI
├── Bridge/
│   └── PowerShellBridge.psm1     # Interface PS ↔ GUI
└── Launcher/
    └── Win11Forge.exe            # Point d'entrée unifié
```

---

## 4. Spécifications Fonctionnelles

### 4.1 Écran d'Accueil (Dashboard)

```
┌─────────────────────────────────────────────────────────────┐
│  Win11Forge v3.0.0                              [─][□][×]   │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│   ┌─────────────┐  ┌─────────────┐  ┌─────────────┐        │
│   │   DEPLOY    │  │   MANAGE    │  │  SETTINGS   │        │
│   │   Profile   │  │    Apps     │  │             │        │
│   └─────────────┘  └─────────────┘  └─────────────┘        │
│                                                             │
│   ╔═══════════════════════════════════════════════════╗    │
│   ║  System Status                                     ║    │
│   ╠═══════════════════════════════════════════════════╣    │
│   ║  Environment: Physical / Windows 11 23H2          ║    │
│   ║  Apps Installed: 42 / 66                          ║    │
│   ║  Last Deployment: 2026-01-04 (Gaming profile)     ║    │
│   ║  Pending Updates: 3 applications                  ║    │
│   ╚═══════════════════════════════════════════════════╝    │
│                                                             │
│   Recent Activity                                           │
│   ├─ [OK] Discord installed via Winget (2h ago)            │
│   ├─ [OK] VS Code updated (yesterday)                      │
│   └─ [!] Steam install failed - retry available            │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### 4.2 Écran de Déploiement

```
┌─────────────────────────────────────────────────────────────┐
│  Deploy Profile                                 [─][□][×]   │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  Select Profile:  [Gaming ▼]                                │
│                                                             │
│  ┌─ Profile Summary ────────────────────────────────────┐  │
│  │ Gaming (inherits: Office → Base)                     │  │
│  │ Total Applications: 39                               │  │
│  │ New to install: 12  |  Already installed: 27         │  │
│  └──────────────────────────────────────────────────────┘  │
│                                                             │
│  ┌─ Applications ───────────────────────────────────────┐  │
│  │ [✓] Google Chrome          [Installed]               │  │
│  │ [✓] Visual Studio Code     [Installed]               │  │
│  │ [✓] Discord                [To Install]              │  │
│  │ [✓] Steam                  [To Install]              │  │
│  │ [ ] Battle.net             [To Install] (uncheck)    │  │
│  │ ...                                                  │  │
│  └──────────────────────────────────────────────────────┘  │
│                                                             │
│  System Configuration:                                      │
│  [✓] Apply Explorer settings                               │
│  [✓] Apply Privacy settings                                │
│  [ ] Apply Power settings (skip)                           │
│                                                             │
│  ┌────────────┐  ┌────────────┐  ┌────────────────────┐    │
│  │  Preview   │  │   Deploy   │  │  Save as Profile   │    │
│  └────────────┘  └────────────┘  └────────────────────┘    │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### 4.3 Écran de Progression

```
┌─────────────────────────────────────────────────────────────┐
│  Deployment in Progress                         [─][□][×]   │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  Profile: Gaming                                            │
│  Overall Progress: ████████████░░░░░░░░ 60% (24/40)        │
│                                                             │
│  ┌─ Parallel Installation (5 threads) ─────────────────┐   │
│  │                                                      │   │
│  │  [████████████████] Discord         100% ✓          │   │
│  │  [██████████░░░░░░] Steam            65% ↓ 12MB/s   │   │
│  │  [████░░░░░░░░░░░░] Epic Games       25% ↓ 8MB/s    │   │
│  │  [██░░░░░░░░░░░░░░] Battle.net       10% Starting   │   │
│  │  [░░░░░░░░░░░░░░░░] GOG Galaxy       0%  Queued     │   │
│  │                                                      │   │
│  └──────────────────────────────────────────────────────┘   │
│                                                             │
│  ┌─ Log ────────────────────────────────────────────────┐  │
│  │ [13:45:02] Installing Discord via Winget...          │  │
│  │ [13:45:15] Discord installed successfully            │  │
│  │ [13:45:16] Installing Steam via Winget...            │  │
│  │ [13:45:18] Downloading Steam (256 MB)...             │  │
│  └──────────────────────────────────────────────────────┘  │
│                                                             │
│  Estimated time remaining: ~8 minutes                       │
│                                                             │
│  ┌────────────┐  ┌────────────┐                            │
│  │   Pause    │  │   Cancel   │                            │
│  └────────────┘  └────────────┘                            │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### 4.4 Gestionnaire d'Applications

```
┌─────────────────────────────────────────────────────────────┐
│  Application Manager                            [─][□][×]   │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  Search: [________________________] [Category ▼] [Status ▼] │
│                                                             │
│  ┌──────────────────────────────────────────────────────┐  │
│  │ NAME              CATEGORY     STATUS      ACTIONS   │  │
│  ├──────────────────────────────────────────────────────┤  │
│  │ Google Chrome     Browser      Installed   [Update]  │  │
│  │ Firefox           Browser      Available   [Install] │  │
│  │ VS Code           Development  Installed   [Remove]  │  │
│  │ Discord           Gaming       Installed   [  -  ]   │  │
│  │ Steam             Gaming       Failed      [Retry]   │  │
│  │ ...                                                  │  │
│  └──────────────────────────────────────────────────────┘  │
│                                                             │
│  ┌─ Application Details ────────────────────────────────┐  │
│  │ Steam                                                │  │
│  │ Category: Gaming                                     │  │
│  │ Sources: Winget (Valve.Steam), Choco, Direct        │  │
│  │ Detection: Registry HKLM:\...\Steam                 │  │
│  │ Last attempt: Failed (network timeout)               │  │
│  │ [View Logs] [Retry Installation]                     │  │
│  └──────────────────────────────────────────────────────┘  │
│                                                             │
│  ┌────────────────┐  ┌─────────────────┐                   │
│  │  Add Custom    │  │  Import/Export  │                   │
│  └────────────────┘  └─────────────────┘                   │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### 4.5 Éditeur de Profils

```
┌─────────────────────────────────────────────────────────────┐
│  Profile Editor: MyCustomProfile                [─][□][×]   │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  Profile Name: [MyCustomProfile    ]                        │
│  Inherits From: [Gaming ▼]                                  │
│  Description: [My personalized gaming setup           ]     │
│                                                             │
│  ┌─ Applications ───────────────────────────────────────┐  │
│  │ Inherited (39)                    Added (5)          │  │
│  │ ├─ [✓] Chrome                    ├─ [✓] Notion      │  │
│  │ ├─ [✓] VS Code                   ├─ [✓] Figma       │  │
│  │ ├─ [ ] Discord (disabled)        ├─ [✓] Slack       │  │
│  │ └─ ...                           └─ ...             │  │
│  │                                                      │  │
│  │ [+ Add Application]  [Browse Database]               │  │
│  └──────────────────────────────────────────────────────┘  │
│                                                             │
│  ┌─ System Configuration ───────────────────────────────┐  │
│  │ [✓] Explorer     [Edit]                              │  │
│  │ [✓] Privacy      [Edit]                              │  │
│  │ [✓] Power        [Edit]                              │  │
│  │ [ ] Network      [Edit]                              │  │
│  └──────────────────────────────────────────────────────┘  │
│                                                             │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐   │
│  │  Save    │  │ Save As  │  │ Validate │  │  Cancel  │   │
│  └──────────┘  └──────────┘  └──────────┘  └──────────┘   │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

---

## 5. Spécifications Techniques

### 5.1 Communication PowerShell ↔ WPF

```csharp
// PowerShellBridge.cs
public class PowerShellBridge
{
    private PowerShell _ps;

    public async Task<InstallResult> InstallApplicationAsync(
        Application app,
        IProgress<InstallProgress> progress,
        CancellationToken ct)
    {
        // Invoke Install-Application with progress callback
    }

    public event EventHandler<LogEventArgs> LogReceived;
}
```

```powershell
# PowerShellBridge.psm1
function Invoke-GUIInstallation {
    param(
        [PSCustomObject[]]$Applications,
        [scriptblock]$ProgressCallback,
        [scriptblock]$LogCallback
    )

    # Bridge between GUI and InstallationEngine
}
```

### 5.2 Modèle de Données

```csharp
// Models
public class ApplicationViewModel
{
    public string AppId { get; set; }
    public string Name { get; set; }
    public string Category { get; set; }
    public InstallStatus Status { get; set; }
    public int Progress { get; set; }
    public string CurrentAction { get; set; }
}

public class DeploymentSession
{
    public Guid SessionId { get; set; }
    public string ProfileName { get; set; }
    public DateTime StartTime { get; set; }
    public List<ApplicationViewModel> Applications { get; set; }
    public DeploymentState State { get; set; }
}
```

### 5.3 Persistance

```
%LOCALAPPDATA%\Win11Forge\
├── settings.json           # User preferences
├── history.db              # SQLite deployment history
├── cache/
│   └── app-icons/          # Cached application icons
└── logs/
    └── gui-*.log           # GUI-specific logs
```

---

## 6. Roadmap Détaillée

### Phase 1: Fondations (2 semaines)

| Tâche | Description | Effort |
|-------|-------------|--------|
| 1.1 | Setup projet WPF + structure MVVM | 2j |
| 1.2 | Intégration MaterialDesign/MahApps | 1j |
| 1.3 | PowerShell Bridge basique | 3j |
| 1.4 | Écran Dashboard (statique) | 2j |
| 1.5 | Tests unitaires infrastructure | 2j |

**Livrable:** Application WPF fonctionnelle avec dashboard statique

### Phase 2: Déploiement Core (3 semaines)

| Tâche | Description | Effort |
|-------|-------------|--------|
| 2.1 | Sélection de profil | 2j |
| 2.2 | Liste applications avec checkboxes | 2j |
| 2.3 | Écran de progression temps réel | 4j |
| 2.4 | Intégration Install-ApplicationsParallel | 3j |
| 2.5 | Gestion pause/cancel/resume | 2j |
| 2.6 | Notifications fin de déploiement | 1j |
| 2.7 | Tests intégration | 3j |

**Livrable:** Déploiement complet fonctionnel via GUI

### Phase 3: Gestion Applications (2 semaines)

| Tâche | Description | Effort |
|-------|-------------|--------|
| 3.1 | Liste applications avec filtres | 2j |
| 3.2 | Détails application | 1j |
| 3.3 | Actions individuelles (install/remove) | 2j |
| 3.4 | Détection statut en temps réel | 2j |
| 3.5 | Ajout application custom | 2j |
| 3.6 | Tests | 1j |

**Livrable:** Gestionnaire d'applications complet

### Phase 4: Éditeur de Profils (2 semaines)

| Tâche | Description | Effort |
|-------|-------------|--------|
| 4.1 | Création/édition profil | 2j |
| 4.2 | Gestion héritage visuel | 2j |
| 4.3 | Configuration système intégrée | 2j |
| 4.4 | Validation profil | 1j |
| 4.5 | Import/Export profils | 1j |
| 4.6 | Tests | 2j |

**Livrable:** Éditeur de profils fonctionnel

### Phase 5: Polish & Features (2 semaines)

| Tâche | Description | Effort |
|-------|-------------|--------|
| 5.1 | Thème sombre/clair | 1j |
| 5.2 | Internationalisation (FR/EN) | 2j |
| 5.3 | Historique déploiements | 2j |
| 5.4 | Export rapports | 1j |
| 5.5 | Paramètres utilisateur | 1j |
| 5.6 | Optimisation performances | 2j |
| 5.7 | Documentation utilisateur | 1j |

**Livrable:** v3.0.0 Release Candidate

### Phase 6: Finalisation (1 semaine)

| Tâche | Description | Effort |
|-------|-------------|--------|
| 6.1 | Tests end-to-end | 2j |
| 6.2 | Bug fixes | 2j |
| 6.3 | Packaging (MSIX/installer) | 1j |
| 6.4 | Documentation release | 1j |

**Livrable:** Win11Forge v3.0.0 GA

---

## 7. Critères d'Acceptation

### 7.1 Fonctionnels
- [ ] Déploiement profil complet via GUI
- [ ] Progression temps réel (< 1s latence)
- [ ] Pause/Resume fonctionnel
- [ ] Rollback accessible depuis GUI
- [ ] Création profil custom sans édition JSON
- [ ] Historique 30 derniers déploiements

### 7.2 Non-Fonctionnels
- [ ] Démarrage < 3 secondes
- [ ] Mémoire < 200 MB en idle
- [ ] Compatible Windows 10 21H2+ / Windows 11
- [ ] Accessible (lecteur d'écran, navigation clavier)
- [ ] Pas de dépendances runtime externes

### 7.3 Qualité
- [ ] 80%+ couverture tests GUI
- [ ] 0 warning PSScriptAnalyzer
- [ ] Documentation utilisateur complète
- [ ] Changelog détaillé

---

## 8. Risques et Mitigations

| Risque | Probabilité | Impact | Mitigation |
|--------|-------------|--------|------------|
| Complexité intégration PS/WPF | Moyenne | Élevé | POC en Phase 1 |
| Performance progression temps réel | Faible | Moyen | Throttling updates |
| Compatibilité Windows 10 | Faible | Moyen | Tests CI multi-version |
| Scope creep features | Élevée | Moyen | Backlog strict, phases claires |

---

## 9. Décisions Techniques Ouvertes

### À Trancher Avant Phase 1

1. **Framework UI**
   - [ ] WPF natif + MaterialDesign
   - [ ] WPF + MahApps.Metro
   - [ ] Autre (Avalonia, MAUI)

2. **Packaging**
   - [ ] MSIX (Windows Store compatible)
   - [ ] Inno Setup / NSIS
   - [ ] Self-contained executable

3. **Base de données historique**
   - [ ] SQLite
   - [ ] LiteDB
   - [ ] JSON files

4. **Langue par défaut**
   - [ ] Français (avec EN disponible)
   - [ ] Anglais (avec FR disponible)

---

## 10. Ressources Nécessaires

### Compétences
- C# / WPF / XAML
- PowerShell avancé
- MVVM pattern
- Tests unitaires (xUnit/NUnit)

### Outils
- Visual Studio 2022
- Blend for Visual Studio (design)
- PowerShell 7+
- Git

### Documentation de Référence
- [WPF Documentation](https://docs.microsoft.com/wpf)
- [MaterialDesignInXaml](https://github.com/MaterialDesignInXAML/MaterialDesignInXamlToolkit)
- [PowerShell SDK](https://docs.microsoft.com/powershell/scripting/developer/hosting/adding-and-invoking-commands)

---

## Appendice A: Wireframes Détaillés

*À compléter avec Figma/Adobe XD*

## Appendice B: API PowerShell Bridge

*À définir en Phase 1*

## Appendice C: Schéma Base de Données

*À définir en Phase 5*

---

**Document Version:** 1.0
**Date:** 2026-01-04
**Auteur:** Win11Forge Team
**Statut:** Draft - En attente validation

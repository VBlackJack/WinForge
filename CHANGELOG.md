# WinForge Framework - Changelog

Note: the framework version source of truth is `Config/version.json`. Launchers and GUI read this value dynamically.

## [Unreleased]

### Changed
- The Applications update scan button now uses clearer "Scan for Updates" wording.
- Required prerequisite apps now remain manageable in the Applications grid with an explicit prerequisite badge and uninstall warning.
- Rebranded public project identifiers, GUI assets, launchers, runtime data directory, and documentation from Win11Forge to WinForge.
- Added startup migration from `%LOCALAPPDATA%\Win11Forge` to `%LOCALAPPDATA%\WinForge`, with graceful fallback to legacy data when migration is blocked.
- Scheduled deployments now use WinForge task names while continuing to discover and manage legacy Win11Forge scheduled tasks.
- Persisted logs and operation result messages now resolve WinForge-owned localized strings in English and file logs are written as explicit UTF-8 without BOM.

### Fixed
- Chocolatey now self-updates through `choco upgrade chocolatey` instead of trying the WinGet bootstrap package during update operations.
- Applications selection now routes checked `Update Available` apps through the update workflow, installs only not-installed apps, and skips apps that are already current.
- Refresh Updates now forces a fresh update scan and update caches are invalidated after install, update, or uninstall operations.
- Update scans now suppress trailing-zero version false positives such as `2.7.3` versus `2.7.3.0`.
- Refresh Updates now stays executable when installed/updateable apps exist even if display counters are stale.
- Process output capture now uses UTF-8 consistently and deployment logs omit raw localized package-manager output from the main log stream.

## [2026062201] - 2026-06-22

### Added
- Existing profiles can now be updated directly from the Applications grid while preserving inherited applications from parent profiles.
- Added a shared icon dropdown button style and applied it to the Apps column visibility menu.
- Added a public architecture overview and refreshed user-facing profile editing documentation.

### Changed
- Unified dark semantic colors through the ThemeForge runtime palette and fixed brush opacity preservation for tinted row backgrounds.
- Tokenized GUI font sizes and corner radii through named application resources.
- Normalized icon button styles and App Catalog toolbar button metrics.
- DataGrid selection now renders as a clean full-row accent-tinted highlight instead of a per-cell focus rectangle.
- Root launchers now prefer fresh release GUI binaries over stale Debug artifacts.

### Fixed
- Localized the ThemeForge 2.1.0 Magellan theme name in the theme picker.
- Fixed application update detection when the WinGet PowerShell module is unavailable.
- Refreshed the Applications "Has Updates" filter after scans so update rows appear immediately.

### Removed
- Removed unused Dracula theme dictionaries and dead button styles.
- Removed internal historical audit documentation from the public documentation set.

### Profile editing and public documentation cleanup — June 2026

#### Added
- **Existing profiles can now be updated from the Applications grid.** After applying a profile, users can check or uncheck apps and save that selection back to the active profile. Inherited applications remain owned by their parent profile and are restored with a warning when a child profile is updated.
- **Added a public architecture overview.** `Docs/ARCHITECTURE.md` now summarizes the current runtime layout, GUI architecture, profile model, theming pipeline, API surface, and validation commands.

#### Changed
- **Updated user-facing documentation.** `README.md`, `Docs/USER_GUIDE.md`, `Docs/README.md`, and `Docs/GUI_VM_VISUAL_CHECKLIST.md` now document profile editing and avoid local machine paths.
- **Removed internal historical docs from the public documentation set.** Old closed-work archives and detailed internal ADR/audit notes were removed in favor of the concise public architecture page.

### Application catalog utilities refresh — June 2026

#### Added
- **Added six utility/runtime catalog entries.** `WindowsAppSDK21`, `BleachBit`,
  `FluentCleaner`, `MicrosoftPCManager`, `Textify`, and `Capture2Text` are now
  present in `Apps/Database/applications.json`. `WizTree` was already present,
  so it was not duplicated.
- **Declared FluentCleaner's Windows App SDK runtime dependency.**
  FluentCleaner installs from the GitHub portable ZIP and now depends on the
  Windows App SDK 2.1 runtime entry, with Microsoft's signed 2.1.3 runtime
  installer as the direct fallback.

#### Changed
- **Direct ZIP portable deployment now allows executable archive contents,
  expands detection-path environment variables, and flattens single-root
  archives.** This keeps DirectUrl ZIP apps such as FluentCleaner installable
  and deployed to the same folder their file detection checks.

#### Fixed
- **DirectUrl installs now load shell-folder path helpers before creating temp
  download folders.** `InstallationMethods.psm1` imports
  `DirectoryConstants.psm1` so `Install-ViaDirectDownload` can reliably resolve
  the temp directory.
- **Dependency satisfaction handles single required dependencies under
  StrictMode.** `Test-DependenciesSatisfied` now normalizes dependency output to
  an array before counting missing prerequisites.

### Direct-download publisher gate activation — June 2026

#### Security
- **Authenticode publisher gate now enforced for the AIMP direct-download.**
  `Sources.ExpectedPublisher = "IP Izmaylov Artem Andreevich"` is now populated
  for AIMP, bringing verified coverage to 18/20 signed direct-downloads.
  `ABDownloadManager` is unsigned and `FileZilla` remains CDN-protected, so both
  remain intentionally dormant.

#### Changed
- **Enabled Authenticode publisher enforcement for 18 direct-download apps.**
  Added empirically verified `Sources.ExpectedPublisher` values for signed
  direct-download installers so the existing installer signature gate now
  fails closed on publisher mismatch for those apps. `ABDownloadManager` and
  `FileZilla` remain dormant pending signing or download-source verification.
- **Removed broken sponsored2 direct-download fallback for FileZilla.** CDN hotlink-protected 403 wrapper bundles offers; FileZilla now installs via Chocolatey only.
- **AIMP direct-download URL switched to the version-stable official endpoint.**
  The direct URL now uses `https://aimp.ru/?do=download.file&id=3` to avoid
  per-release URL drift.
- **Added maintainer publisher probes.** `Tools\Get-DirectDownloadPublishers.ps1`
  reports `{ AppId, Url, Status, Bytes, SignerSubject }` from downloaded
  installers, and `Tools\Set-DirectDownloadExpectedPublishers.ps1` applies the
  approved publisher mappings with a BOM-safe dry-run-first workflow.

#### Fixed
- **Refreshed the stale Signal Desktop direct-download URL.** Updated the
  fallback from `7.0.0` to `x64-8.12.0`, restoring the broken direct-download
  fallback while keeping the existing Authenticode publisher gate enforced.

### Button visual hierarchy restored — May 2026 follow-up

After visual inspection of the `v2026051001` release built from the unified
flat-text state, the lack of visual hierarchy on every action button proved
unworkable in practice — hero CTAs, dialog confirm buttons, and toolbar
actions all rendered as identical plain text labels. Reversed §2.6 of
the historical design notes for the full rationale. Those internal notes are
no longer part of the public documentation set.

#### Changed
- **Restored 70 `Appearance` attributes** across 20 view files to their
  pre-PR #94 values — `Primary` for constructive primary actions
  (Confirm/Save/OK/Apply/Install/Add/Start/Restart/etc.), `Secondary` for
  outlined actions (Cancel/Close/Browse/Test/Filter/Save Profile/etc.),
  `Danger` for App Catalog row Delete. Hero CTAs (Start Deployment, Fix
  Prerequisites) regain their accent fill. Inline `Background` / `Foreground`
  / `Padding` / `Height` overrides previously stripped by PR #94 are **not**
  restored — the implicit `ui:Button` `Style.Triggers` in App.xaml drive the
  theme-adaptive look from `ThemeAdaptiveAccentBrush` / `BadgeTextBrush`.

#### Kept (unchanged from PR #94)
- Forked WPF-UI 4.3 `ui:Button` `ControlTemplate` with the
  `ContentBorder.Opacity=0.45` disabled-state workaround.
- `Secondary` and `Transparent` hover/pressed re-targeted to `SurfaceBrush`
  for WCAG AA contrast (~5.6:1–6:1).
- Apps view fixes (Column Visibility menu, `_selectedCount`
  `NotifyCanExecuteChangedFor`, DataGrid cell vertical centering).
- ThemeService `PaletteBrushResourceMap` extensions (~22 button/checkbox state
  keys) and `Palette*Color` entries.
- Keyed styles `HeroPrimaryButton` / `PrimaryButton` / `SecondaryButton` /
  `DestructiveSolidButton` / etc. remain defined in App.xaml as a fallback
  API; views still drive look through `Appearance="..."` directly.

### UI flat-text consolidation + WPF-UI 4.3 disabled-state fix — May 2026

Visual unification pass and several orthogonal Apps-view defect fixes uncovered
along the way.

#### Changed
- **App-wide flat button presentation.** Every `Appearance="Primary|Secondary|`
  `Danger"` and every `Style="{StaticResource HeroPrimaryButton|`
  `WarningPrimaryButton|SecondaryButton|DestructiveSolidButton}"` instance is
  now `Appearance="Transparent"`. ~60 buttons across 15 files: `Views/AppsView`,
  `Views/AppCatalogView`, `Views/DashboardView`, `Views/PrerequisitesView`,
  `Views/DeploymentView`, `Views/SettingsView`, `Views/LogsView`,
  `Views/SaveProfileDialog`, `Views/ApplicationPickerDialog`,
  `Views/Dialogs/ApplicationEditorDialog`, `Controls/ConfirmDialog`,
  `Controls/EmptyStateControl`, `Controls/ErrorDialog`,
  `Controls/KeyboardShortcutsPanel`, `Controls/LoadingOverlay`,
  `Controls/OnboardingDialog`, plus four `UserControls/*SourceEditor` and
  `UserControls/DetectionEditor`.
- **Hover contrast WCAG AA.** `Secondary` and `Transparent`
  `MouseOverBackground` / `PressedBackground` re-targeted from `HighlightBrush`
  (~`#4A4E66`, ~2.25:1 vs purple text — fails AA) to `SurfaceBrush`
  (`#1B1C25`, ~5.6:1–6:1 against the configured foregrounds — clears AA).

#### Fixed
- **Column Visibility menu silently broken (Apps view).** The `ui:Flyout` placed
  inside `<ui:DropDownButton.Flyout>` was silently ignored at runtime
  (`Flyout` is typed `ContextMenu`). Replacement attempt with
  `ui:Button + ContextMenu + Click handler` failed because the menu's
  `PlacementTarget="{Binding ElementName=...}"` evaluates inside the
  `ContextMenu` NameScope and could not resolve. Fix: adopt the
  `ui:DropDownButton` + flyout `ContextMenu` pattern already used in
  `AppCatalogView`. Removed the `ColumnVisibilityButton_Click` handler and the
  dead `_isColumnVisibilityPopupOpen` `[ObservableProperty]`.
- **WPF-UI 4.3 disabled state painted Light Fluent on dark Dracula.** The
  upstream `controls:Button` `ControlTemplate.Triggers` writes
  `ContentBorder.Background = {DynamicResource ButtonBackgroundDisabled}`,
  and that `DynamicResource` lookup does not honor any user-scope override at
  any level we tested (`StackPanel.Resources`, `ui:Button.Resources`,
  `UserControl.Resources`, or `Application.Current.Resources` direct entry —
  all probed and verified inert). Fix: fork the upstream
  `DefaultUiButtonStyle` `ControlTemplate` verbatim into our App.xaml implicit
  `ui:Button` style. Single deviation: the `IsEnabled=False` trigger now sets
  `ContentBorder.Opacity=0.45` instead of overriding the three brushes. The
  fork preserves `Icon` DP support, `RecognizesAccessKey`, `PressedForeground`,
  the `InsetBorder`, and every upstream `Appearance` trigger.
- **`UninstallSelectedCommand` `CanExecute` not refreshed when selection
  changes.** `_selectedCount` in `ViewModels/AppsViewModel.cs` only had
  `[NotifyCanExecuteChangedFor(nameof(InstallSelectedCommand))]`. Added the
  matching attribute for `UninstallSelectedCommand`. Without it, Uninstall
  Selected stayed in the visual disabled state after the user picked an item,
  even though `CanUninstallSelected` would have evaluated `true`.
- **DataGrid app-name column visibly top-biased.** Outer `StackPanel`
  `Orientation="Vertical"` of the Application Name `CellTemplate` defaulted to
  `VerticalAlignment="Stretch"`, so the cell-level `VerticalContentAlignment=`
  `"Center"` had no effect on the rendered text position. Set
  `VerticalAlignment="Center"` on that panel and on the Selection
  `CheckBox` cell template. Title + description now sit at row center within
  ~3 px.

#### ThemeService bridge
- Extended `PaletteBrushResourceMap` with rest/hover/pressed/disabled state
  keys for `Button*` and `CheckBox*` (~22 new mappings) so all WPF-UI control
  states pull Dracula brushes.
- Extended `PaletteColorResourceMap` with `PaletteRedColor` /
  `PaletteGreenColor` / `PaletteOrangeColor` / `PaletteLightBlueColor` Color
  overrides — preserved for forward compatibility even though no instance
  currently uses `Appearance="Danger|Success|Caution|Info"`.

### Dead code + i18n audit pass — May 2026

Two-axis cleanup pass closing 18 dead-code and localization findings across 7 self-contained commits. No behavior change at runtime; build, 566 GUI tests, Pester suite, PSScriptAnalyzer, FR diacritics lint, and version-consistency check all green.

#### Removed
- **PowerShell manifest cleanup (DC-013)** — `Core/Core.psd1` no longer declares `Test-AdminRights` or `Get-FrameworkVersion` in `FunctionsToExport` (these functions never existed in `Core.psm1`).
- **Resx keys cleanup (DC-002 to DC-012)** — 10 unused EN+FR resx pairs deleted: `AppEditor_Category`, `Apps_SelectWithUpdates`, `Dashboard_Updates_Available`, `Deploy_InheritedFrom`, `Deploy_Installing`, `Help_Shortcut_Actions`, `Help_Shortcut_Navigation`, `Recovery_NetworkTimeout`, `SourceEditor_TestPlaceholder`, `Toast_UninstallComplete`. Designer.cs trimmed where applicable. Guard test `DeadResourceCleanup_RemovesUnusedKeys2026May` added on `AccessibilityHardeningTests`.
- **IAccessibilityService scaffolding (DC-001 / DC-008)** — `IAccessibilityService`, `AccessibilityService`, `AnnouncementPriority`, DI registration, `MainWindow` field/initialize call, plus correlated resx keys `Accessibility_Progress`, `Accessibility_ProgressWithItem`, `Accessibility_ProgressComplete`, `Accessibility_DeploymentStarted`. Live-region screen-reader behavior is preserved via the existing XAML automation properties on `ScreenReaderLiveRegion` (PR #61/#62 baseline) and the 5 `LiveRegionAttributesTests` guards.

#### Refactored
- **Centralized GUI timeouts (ZH-002, ZH-003, ZH-004)** — three duplicated/literal timeout values now live in `GUI/Win11Forge.GUI/Configuration/TimeoutDefaults.cs` (`HttpClient` 15 s, `PackageOperation` 30 s, `CacheWarmingShutdown` 2 s). PowerShell install timeouts continue to live in `Config/timeouts-settings.json`.
- **Centralized GitHub project links (ZH-001)** — `GUI/Win11Forge.GUI/Configuration/ProjectLinks.cs` now provides `Repository`, `Issues`, `NewIssue`. `ErrorDialog` and `SettingsViewModel` route through this single source; the `ErrorDialog` issue-report fallback path is now consistent with the primary URL (previously dropped the `/new` suffix).

#### Documented
- **ReDoS regex timeout (ZH-005)** — `JsonApplicationDetectionService.RegexTimeout` (500 ms) is annotated as intentionally non-configurable to prevent attacker-controlled config from disabling the protection.

Resx parity after this pass: 943/943.

### Post-audit debt closure - May 2026

Closed the remaining non-audit and technical debt backlog through PR #82-#91. The baseline is now `2026050901`, with `Config/version.json` as the display-version source of truth.

- **Localization SSoT** (PR #82): supported GUI locales now flow through `SupportedLocales`, with runtime resolver, Settings language picker, and resx parity tests aligned.
- **Logs localization sweep** (PR #83): user-facing Logs status strings, filters, export filter, and delete confirmations now come from EN/FR resources. Resource parity moved to 957/957.
- **Calendar versioning** (PR #84): release tooling, GUI project properties, manifests, schema, and version consistency checks now use `YYYYMMDDxx` display versions with assembly versions derived as `1.0.MMDD.sequence`.
- **WinSight smoke harness** (PR #85): added opt-in `Tools/Invoke-WinsightSmoke.ps1` for local desktop smoke checks through the sibling WinSight MCP server. The legacy xUnit UIA harness remains opt-in.
- **Application editor save fidelity** (PR #86): application saves preserve entry order, UTF-8 BOM state, unchanged metadata, and no-op saves avoid rewriting `applications.json`.
- **Update Pause/Resume clarity** (PR #87): `IsUpdating` now hides batch Pause/Resume controls during selected updates instead of exposing inert actions.
- **WF-005 strict profile safety** (PR #88): switching away from an applied profile now detects manual selection drift and reuses Replace / Merge / Cancel safety semantics.
- **ExportSelection flake hardening** (PR #89): import/export tests now use isolated temporary directories, retry-safe cleanup, explicit success/error assertions, and no shared dialog service state.
- **Application bridge cleanup** (PR #90): removed unused `IApplicationBridge.InstallApplicationsAsync` and its batch progress/result types; batch install remains owned by `IAppInstallationCoordinator`.
- **Theme cleanup + coverage** (PR #91): `ThemeNames.DraculaResourcePathPrefix` is the single Dracula resource path prefix, and migration/converter fallback coverage was added.

### MVVM refactor closure

The architectural refactor of the GUI ViewModels (audit findings I1, I3, I4) is complete. All four batch operation coordinators (`AppScanCoordinator`, `AppInstallationCoordinator`, `AppUpdateCoordinator`, `AppUninstallCoordinator`) are extracted under `Services/Coordinators/`, sharing the internal `AppOperationRunner` helper for parallelism, cancellation, and progress reporting. The `AppsViewModel` god class (3 002 lines, 31 RelayCommands) is now a 531-line orchestrator with twelve cluster-scoped partial classes. WPF lifetime coupling, file dialog handling, and code-behind business logic have been moved behind dedicated services (`IApplicationLifetimeService`, `IFileDialogService`, `IApplicationEditorDialogService`, `IPauseGate`).

See `Docs/ARCHITECTURE.md` for the current public architecture overview.

### UX audit closures - May 2026

Completed the P0/P1 remediation sweep from the May 2026 UX review, then closed the remaining P2/P3 UX backlog through PR #81.

#### Accessibility
- **Closed A11Y-001** (PR #67/#68): High Contrast resources are preserved when switching Dracula themes.
- **Closed A11Y-002** (PR #69/#70): Reduced Motion now gates code-driven animations; stale storyboard-only controls were removed.
- **Closed A11Y-003** (PR #71/#72): High-visibility focus visuals cascade through implicit WPF and WPF-UI button styles.
- **Closed A11Y-004/A11Y-005/A11Y-006** (PR #61/#62): Live region announcements stay in the automation tree; DraculaPro text contrast and Light-theme orange contrast now meet WCAG AA.

#### Naming and visual hierarchy
- **Closed UX-001 and DC-002** (PR #63/#64): The admin catalog surface is consistently named App Catalog, including `AppCatalogView*`, `AppCatalog_*` resource keys, and navigation IDs.
- **Closed DC-001/DC-004/DC-011** (PR #65/#66): Page typography tokens, shared source badge styling, and subtler DataGrid gridlines now apply consistently across the GUI.
- **Closed DC-005/DC-006/DC-007/DC-008** (PR #79): AppsView now follows the icon/title/subtitle header pattern, profile/filter cards reflow without forced horizontal card scroll, Settings tabs have a reinforced selected indicator, and navigation separates workflow pages from configuration pages while preserving `ViewIndex` tags.
- **Closed DC-012/DC-013/DC-014** (PR #80): Post-smoke polish hides unavailable AppCatalog actions, removes the duplicate AppsView install CTA, and gives shared TabItems a theme-aware selected template.
- **Closed DC-009/DC-010** (PR #81): Settings card headers no longer repeat tab-strip icons, and audited card borders now use the shared `CardPadding` token across views.

#### Workflow safety
- **Closed WF-003/WF-005/WF-006** (PR #73/#74): Single-app uninstall now confirms destructive action; profile changes protect manual selections with Replace/Merge/Cancel; profile tier badges are derived from JSON inheritance instead of hardcoded tiers.
- **Closed WF-007** (PR #75/#76): Update Selected now mirrors Install progress UX with current app, progress percentage, ETA, cancellation, and final summary.
- **Confirmed WF-008 closure** (PR #55/#56): App Catalog empty states are gated on loading and load-error state, with distinct empty-database and empty-filter copy.
- **Closed WF-011/WF-012/WF-013/WF-014** (PR #81): Selection helpers respect active filters, import selection/favorites uses Replace/Merge/Cancel previews, Settings auto-save no longer spams info toasts, and cancellation paths now confirm with progress context.

### Added

- **Test coverage** — `AccessibilityHardeningTests` adds three static-analysis guards: `RequiredA11yLocKeys_ArePresentInXaml` (theory across the 6 a11y-touched XAML files), `HighContrastMode_TextOnAccentBrushes_AreRemapped` (asserts the 3 `SwapIfExists` entries in `App.xaml.cs`), and `HighContrastTheme_ImplicitlyStylesWpfUiButton` (asserts the implicit `ui:Button` style based on `HighContrastButtonStyle` in `HighContrastTheme.xaml`). Total test count: 527 passed (`dotnet test -c Release GUI\Win11Forge.slnx --filter "FullyQualifiedName!~UIA"`).
- **Test coverage** — `AccessibilityHardeningTests` adds four static visual-hierarchy guards for P-15: AppsView header pattern, AppsView filter/profile reflow without `MinWidth="920"`, implicit `ReinforcedTabItemStyle`, and workflow/config navigation clustering. Total test count: 531 passed (`dotnet test -c Release GUI\Win11Forge.slnx --filter "FullyQualifiedName!~UIA"`).
- **Test coverage** — `AccessibilityHardeningTests` adds three post-smoke guards for DC-012/DC-013/DC-014: AppCatalog unavailable action visibility, AppsView install CTA deduplication, and theme-aware TabItem template states. Total test count: 534 passed (`dotnet test -c Release GUI\Win11Forge.slnx --filter "FullyQualifiedName!~UIA"`).
- **Test coverage** — PR #81 adds guards for filtered Apps selection helpers, import Replace/Merge/Cancel behavior, cancel confirmations, Settings no-toast auto-save, AppEditor source-specific a11y keys, AppCatalog HC contrast, dead resource cleanup, Settings icon de-duplication, and cross-view `CardPadding` token usage. Total test count: 554 passed (`dotnet test -c Release GUI\Win11Forge.slnx --filter "FullyQualifiedName!~UIA"`).

### Changed

- **Applications workflow** — `SelectAll`, `SelectNotInstalled`, `SelectFavorites`, and `SelectWithUpdates` now operate on the active filtered application list; `SelectNone` remains global to clear hidden selections deliberately. Closes WF-011 (PR #81).

### Fixed
- **Workflow safety (WF-012)** — Import Selection and Import Favorites now show Replace / Merge / Cancel previews when current state is non-empty, summarize matched/missing/final counts, and leave state untouched on cancel. Closes WF-012 (PR #81).
- **Workflow feedback (WF-013)** — Settings auto-save updates continue to set inline status, but no longer fire repetitive info toasts during every keystroke/toggle change. Closes WF-013 (PR #81).
- **Workflow safety (WF-014)** — Apps batch cancellation and Monitoring/Deployment cancellation now prompt before requesting cooperative cancellation, including completed/total context. Closes WF-014 (PR #81).
- **Visual hierarchy (DC-009)** — Settings keeps tab icons but removes redundant repeated icons from section headers inside cards. Closes DC-009 (PR #81).
- **Visual hierarchy (DC-010)** — Audited card borders across Apps, AppCatalog, Dashboard, Deployment, Logs, Prerequisites, and Settings now use `{StaticResource CardPadding}` instead of literal `16`/`20` card padding. Closes DC-010 (PR #81).
- **Accessibility follow-up** — ApplicationEditorDialog source action buttons now use source-specific automation names for Winget, Chocolatey, and Microsoft Store search/apply actions. Resolves the AppEditor P3 a11y follow-up (PR #81).
- **Accessibility follow-up** — High Contrast AppCatalog header contrast is guarded by WCAG AA tests and HC brush mappings for mapped surface brushes. Resolves A11Y-011 candidate (PR #81).
- **Resource cleanup** — Removed dead `AppCatalog_DeleteMultiple*` EN/FR resource keys and hardcoded English fallbacks in `LogsViewModel.ClearOldLogsAsync`. Resolves TODO cleanup follow-ups (PR #81).
- **Visual hierarchy (DC-012)** — AppCatalog now hides Undo/Redo and row actions when unavailable instead of rendering large disabled blocks. Closes DC-012 (PR #80).
- **Information architecture (DC-013)** — AppsView keeps `Install Selected` only in the selection action bar, removing the duplicate Profile-card CTA. Closes DC-013 (PR #80).
- **Visual hierarchy (DC-014)** — Shared WPF `TabItem` styling now uses a theme-aware template with explicit hover, focus, selected, and disabled states. Closes DC-014 (PR #80).
- **Visual hierarchy (DC-005)** — AppsView now mirrors the app-wide page header structure with an `Apps24` icon, `PageTitleTextStyle`, `PageSubtitleTextStyle`, and new localized `Apps_Subtitle` resource. EN/FR resource parity is 920/920. Closes DC-005 (PR #79).
- **Visual hierarchy (DC-006)** — AppsView profile and filter cards no longer force internal horizontal scrolling through `MinWidth="920"`; both surfaces now reflow in a two-row responsive layout. Closes DC-006 (PR #79).
- **Visual hierarchy (DC-007)** — Settings and shared WPF `TabItem` surfaces use `ReinforcedTabItemStyle` with a stronger selected underline and selected-state tint. Closes the DC-007 implementation scope; stronger post-smoke tab treatment is tracked separately as DC-014. Closes DC-007 (PR #79).
- **Information architecture (DC-008)** — Main navigation now separates workflow and configuration clusters with `NavigationViewItemSeparator`, moving App Catalog next to Settings while keeping Settings `Tag="4"` and App Catalog `Tag="5"` stable. Closes DC-008 (PR #79).
- **Accessibility (A11Y-007)** — Sweep `AutomationProperties.Name` on AppsView toolbar buttons (LogViewer Copy/Close, Summary Close, Save Profile, Reset Columns) and ApplicationEditorDialog source actions (Search + Apply Selection × 3 sources). Screen readers now announce explicit names instead of relying on inferred Content text. Closes A11Y-007 from the May 2026 UX review (PR #78).
- **Accessibility (A11Y-008)** — High-contrast mode now remaps `TextOnAccentFillColor*` brushes (Primary/Secondary/Disabled) to high-contrast foreground variants in `App.ApplyHighContrastMode`, and selectively applies `HighContrastButtonStyle` to `ui:Button` only (WPF-UI). Resolves the 1.07:1 white-on-cyan hover regression on accent-painted buttons in HC mode. Plain WPF `Button` controls remain untouched to preserve custom-styled buttons. Closes A11Y-008 (PR #78).
- **Accessibility (A11Y-009)** — Settings toggle switches (Reduce Motion, High Contrast) now expose their descriptive subtitle via `AutomationProperties.HelpText`, in addition to the existing `Name`. Closes A11Y-009 (PR #78).
- **Accessibility (A11Y-010)** — Explicit `AutomationProperties.Name` added to templated/dialog buttons that previously relied on inferred announcement: Prerequisites Check (StackPanel-wrapped Button with ProgressRing), ConfirmDialog Cancel/Confirm, ErrorDialog Help/Retry/OK. Closes A11Y-010 (PR #78).
- Hardened the Windows runner CI baseline after enabling strict Pester gating.
- Replaced a fragile File detection fixture path with a stable system executable present on Windows Server runners.
- Made Store app detection tolerate app definitions without optional `Sources.Store` metadata.
- Kept plugin load sandbox validation compatible with Constrained Language Mode by performing AST validation before constrained module execution.
- Added GUI test hang diagnostics and a 20-minute timeout to the coverage step.
- Converted GUI coverage enforcement to a baseline floor with an explicit 80% target warning while the MVVM coverage backlog is completed.
- Reactivated the `SettingsViewModel` history-clear test after replacing its modal `MessageBox` dependency with `IDialogService`.
- Closed audit finding I4: code-behind business logic in `ApplicationsView` and `AppsView` migrated to ViewModels via dialog services and pure XAML context menu bindings.
- Advanced I1 phase 2 with `AppUpdateCoordinator` extraction (PR #44): parallel scan-for-updates and sequential update-apply are modeled as separate coordinator workflows, the two PR7-tagged provisional semaphores in `AppsViewModel.Update.cs` were absorbed, and a test guard now prevents accidental re-parallelization of `UpdateAsync`.
- Completed I1 phase 2 with `AppUninstallCoordinator` extraction (PR #45): all four coordinators (Scan, Installation, Update, Uninstall) now live under `Services/Coordinators/`. PR #45 also completed the residual `MessageBox.Show` migration to `IDialogService.ShowConfirmAsync` across ViewModels (`SettingsViewModel`, `LogsViewModel`, and Uninstall), satisfying the DoD §8.1 dialog invariant.

## [3.7.2] - 2026-02-12

### New Features
- **Added**: REST API async server mode with background job management (`Start-ApiServerAsync`)
- **Added**: Application Editor with live package search (Winget, Chocolatey, Store) in WPF GUI
- **Added**: Package verification service for source validation in Application Editor
- **Added**: Log Viewer with filtering, export, and old log cleanup in WPF GUI
- **Added**: Settings export/import functionality with JSON serialization
- **Added**: Scheduled Deployments management UI in Settings view
- **Added**: Plugin system with sandboxed execution (`PluginManager.psm1`, `PluginSandbox.psm1`)
- **Added**: Structured logging module (`StructuredLogging.psm1`) with JSON log output
- **Added**: Feature flags runtime toggle (`FeatureFlags.psm1`)
- **Added**: Timeout configuration module (`TimeoutSettings.psm1`)

### Security Improvements
- **Added**: CSRF token protection for state-changing API endpoints
- **Added**: Per-IP rate limiting with configurable thresholds (per-minute and per-hour)
- **Added**: API key DPAPI-encrypted secure storage (`Set-SecureApiKey`, `Get-SecureApiKey`)
- **Added**: Handler validation against dangerous patterns, commands, types, and static methods
- **Added**: Request body size limiting to prevent memory exhaustion
- **Added**: Localhost binding enforcement for API server security
- **Added**: IP blocking after repeated authentication failures

### Bug Fixes
- **Fixed**: SecureStorage DPAPI round-trip failures — `Get-DpapiEntropy` now persists entropy to disk on PS7 (replaced .NET Framework-only `File.Create(FileSecurity)` overload with cross-platform `FileStream` + `Set-SecureFileAcl`) and caches entropy in memory for session consistency
- **Fixed**: Added `Add-Type -AssemblyName System.Security` to `SecureStorage.psm1` for PowerShell 5.1 compatibility (DPAPI types not auto-loaded)

### Zero Hardcoding Audit Remediation

Complete audit remediation addressing 225+ violations across conformity, code quality, and architecture.

#### Internationalization (i18n)
- **Added**: 150+ new i18n keys to `Config/Locales/en.json` and `Config/Locales/fr.json`
- **Fixed**: 127+ hardcoded user-facing strings replaced with `Get-LocalizedString`/`t` calls across 20+ modules
- **Affected modules**: StartMenuLayout, ApplicationDatabase, PluginSandbox, RollbackManager, JsonSchemaValidation, Prerequisites, InstallationMethods, SecureStorage, ModuleLoader, Core, StructuredLogging, Win11ForgeGUI, TelemetryCollector, InstallationOrchestrator

#### Path Centralization (DirectoryConstants)
- **Added**: 7 new registry path constants (`WindowsNTVersion`, `ContainerManager`, `OfficeClickToRun`, `OfficeInstallRoot`, `DotNetFramework`, `VCRedistX64`, `VCRedistX86`)
- **Added**: 3 new state path constants (`SecureStorage`, `ApiKeys`, `Entropy`)
- **Added**: 2 new shell folder entries (`DefaultUserProfile`, `StartMenuBinary`)
- **Added**: Exit code constants for Winget, Chocolatey, and general operations (`Get-ExitCodes`)
- **Fixed**: 28+ hardcoded `$env:LOCALAPPDATA` paths replaced with `Get-Win11ForgeDirectory`/`Get-StatePath` calls
- **Fixed**: 5 hardcoded `$env:TEMP` paths replaced with `Get-ShellFolder -FolderType 'Temp'`
- **Fixed**: 12 hardcoded `HKLM:\`/`HKCU:\` registry paths replaced with `Get-RegistryPath` calls
- **Affected modules**: SecureStorage, StateManager, TelemetryCollector, WingetCache, UserProfileManager, ApplicationDatabase, UpdateManager, StartupManager, ApplicationDetection, EnvironmentDetection, Prerequisites

#### User-Agent Version Fixes
- **Fixed**: Outdated User-Agent strings (`Win11Forge/3.5.0`, `Win11Forge/3.5.2`) now read version dynamically from `Config/version.json` or assembly metadata
- **Affected**: InstallationOrchestrator, UpdateManager, PackageVerificationService.cs

#### Code Quality
- **Renamed**: `Deploy-StartMenuLayoutToDefault` to `Publish-StartMenuLayoutToDefault` (approved PowerShell verb)
- **Added**: `.SYNOPSIS` to 10 functions missing documentation (ParallelDetection x6, Win11ForgeGUI x4)
- **Fixed**: 3 empty `catch {}` blocks replaced with `Write-Debug` statements (ModuleLoader, SecureStorage, StructuredLogging)
- **Changed**: `JsonSchemaValidation.psm1` lazy-loaded in `ApiEndpoints.psm1` (resolves Core→Modules reverse dependency)
- **Fixed**: CI workflow no longer excludes `PSAvoidUsingEmptyCatchBlock` rule

#### Version Alignment
- **Fixed**: All profile versions aligned to 3.7.2 (Base, Office, Gaming, Personnel, Enterprise)
- **Fixed**: Locale file versions aligned to 3.7.2 (en.json, fr.json)
- **Fixed**: 15+ module version headers updated from 3.6.8 to 3.7.2

### GUI Audit Round 2

Post-merge audit of 23 GUI files with targeted fixes for correctness, accessibility, and code quality.

#### Critical / High
- **Fixed**: `SettingsViewModel.TrySaveSettings()` — was creating a new `AppSettings` object, losing fields from other views; now loads existing settings first and updates only managed fields
- **Fixed**: `AppSettingsService.SaveSettingsAsync()` — return type changed from `Task` to `Task<bool>` for consistency with sync method
- **Fixed**: Duplicate `ApplyHighContrastMode` call removed from `App.xaml.cs` startup
- **Fixed**: Duplicate `AutomationProperties.Name="ScheduledDeployment_DateTime"` on DatePicker and TextBox — now unique (`ScheduledDeployment_Date` / `ScheduledDeployment_Time`)
- **Fixed**: Hardcoded undo/redo tooltip strings replaced with localized computed properties (`UndoButtonTooltip` / `RedoButtonTooltip`)

#### Medium
- **Fixed**: Magic animation duration numbers replaced with named constants (`AnimationFastMs`, `AnimationNormalMs`, `AnimationSlowMs`)
- **Fixed**: `Contains("HighContrastTheme")` now uses `StringComparison.Ordinal`
- **Fixed**: `GetLocalizedString` fallback removed — replaced with strongly-typed `Resources.Resources.Settings_SaveFailed`
- **Fixed**: Undo/Redo buttons now use `TouchFriendlyIconButton` style (44x44px WCAG 2.1 AA)
- **Fixed**: `DashboardView.xaml` Grid.ColumnDefinitions indentation corrected

#### Low
- **Fixed**: Redundant `RegexOptions.Compiled` removed from 9 `[GeneratedRegex]` attributes across 3 services
- **Fixed**: GitHub URL extracted to named constant in `SettingsViewModel`
- **Fixed**: Hardcoded `"_Copy"` / `" (Copy)"` replaced with localized resource keys
- **Added**: 8 new resource keys to `Resources.resx` / `Resources.fr.resx` with Designer.cs accessors

### Statistics
- **i18n Keys**: 1,460+ (up from ~1,300)
- **Test Coverage**: 1047+ Pester, 309 xUnit
- **Total Applications**: 175

---

## [3.6.7] - 2026-02-05

### Bug Fixes

#### Office Installation Detection
- **Fixed**: `Wait-ForOfficeInstallation` stuck in infinite loop
  - The function was only checking if Office was installed when no Office processes were found
  - But `OfficeClickToRun.exe` is a permanent Windows service, not just an installation process
  - Fix: Always check if Office is installed first (registry/files) on each iteration
  - Only monitor installation-specific processes (`setup`, `OfficeC2RClient`), not the permanent service

#### GUI Timeout
- **Fixed**: Increased installation timeout from 30 to 47.5 minutes
  - Must exceed Office Click-to-Run 45 minute timeout to prevent premature cancellation

#### DirectDownload (v3.6.4)
- **Fixed**: URL query parameter handling for filenames
  - URLs like `?installer=Battle.net-Setup.exe` now correctly extract the filename
  - Added proper parsing of query parameters instead of using raw URL path

#### GUI Stability (v3.6.3)
- **Fixed**: Binary content filter in log viewer to prevent GUI freeze
  - Filters out DOS executable headers (MZ) and high ratio of non-printable characters
  - Prevents crash when viewing Battle.net installation logs

---

## [3.5.2] - 2026-01-28

### Bug Fixes & Test Improvements

#### Tests
- **Fixed**: `StructuredLogging.Tests.ps1` - Corrected function name `Flush-LogBuffer` → `Clear-LogBuffer`
- **Status**: All 1047 tests passing (100%)

#### Documentation
- **Updated**: README.md version and application count (175+ apps)
- **Added**: Enterprise profile to profile documentation
- **Added**: API REST and scheduled deployments features to README

#### Code Quality
- **Verified**: All BMAD audit items completed (100%)
- **Validated**: Zero hardcoding mandate compliance

### Statistics
- **Test Coverage**: 1047/1047 tests passing
- **Total Applications**: 175

---

## [3.5.1] - 2026-01-24

### Database & Schema Updates

#### Applications Database
- **Updated**: `applications.json` database version to 3.5.1
- **Fixed**: Application count metadata (177 → 175)
- **Added**: Detection method improvements for runtime applications

#### JSON Schema Validation
- **Enhanced**: `JsonSchemaValidation.psm1` with additional validation rules
- **Added**: Priority range documentation in applications-database.schema.json

#### Infrastructure
- **Improved**: State management validation in `StateManager.psm1`
- **Enhanced**: Scheduled deployment configuration validation

### Statistics
- **Test Coverage**: 1045/1045 tests passing
- **Total Applications**: 175

---

## [3.5.0] - 2026-01-21

### GUI - Runtime Detection Fix

#### JsonApplicationDetectionService (NEW)
- **Added**: JSON-based detection service for applications.json detection methods
- **Added**: Support for Command, Registry, File, and WindowsFeature detection methods
- **Added**: Proper parsing of nested `Applications` structure with WinGet ID matching
- **Added**: Executable path resolution for `dotnet`, `java`, `node`, `python`, `git`
- **Fixed**: Runtime detection now correctly identifies .NET, VC++, Java, etc.
- **Impact**: All 177 applications now properly detected when scanning

#### Interface Segregation (ISP)
- **Added**: Focused interfaces split from IPowerShellBridge:
  - IApplicationManagementService
  - IProfileManagementService
  - IPrerequisitesService
  - ISystemInfoService
  - IVersionService
  - IDeploymentOrchestrationService
- **Added**: Adapter classes: ApplicationBridge, ProfileBridge, PrerequisitesService
- **Added**: FocusedInterfacesTests.cs to verify contract compliance

#### UI Enhancements
- **Added**: Scheduled Deployments UI in Settings view
- **Added**: SplashScreen with cache pre-warming
- **Added**: AccessibilityService for high contrast theme detection
- **Added**: HighContrastTheme.xaml resource dictionary
- **Updated**: ApplicationModel with 177 applications (was 170)

### Core

#### Security
- **Added**: DPAPI encryption for api-settings.json via SecureStorage.psm1
- **Added**: ValidatePathWithinDirectory() for path traversal prevention
- **Added**: URL validation with regex patterns in REST API

#### Profiles
- **Added**: Enterprise.json profile
- **Added**: Profile cycle detection (Test-ProfileCycles)

### Documentation
- **Consolidated**: BMAD audit reports into single BMAD-AUDIT-v3.5.0.md
- **Cleaned**: Removed old audit reports from Docs/ and Reports/

### Statistics
- **Test Coverage**: 1040+ tests passing
- **Total Applications**: 177

---

## [3.2.3] - 2026-01-19

### Performance Optimizations

#### Registry-First Detection (ApplicationDetection.psm1)
- **Added**: `Get-RegistryInstalledApp` function for fast registry-based detection (~20ms vs ~2s)
- **Added**: Script-level `RegistryAppsCache` with 5-minute TTL
- **Modified**: `Test-ApplicationInstalled` checks registry FIRST before CLI calls
- **Added**: `Clear-RegistryAppsCache` for manual cache invalidation
- **Impact**: App detection ~100x faster (2s → 20ms per app)

#### Batch Update Cache (UpdateManager.psm1)
- **Added**: `$script:BatchUpdateCache` hashtable with 10-minute TTL
- **Added**: `Get-WingetUpdatesBatch` - single `winget upgrade` call to cache all updates
- **Added**: `Get-ApplicationUpdateStatus` - lookup update status from cache
- **Added**: `Clear-BatchUpdateCache` for manual cache invalidation
- **Impact**: Update check reduced from ~2s/app to ~3s total

#### Semantic Versioning (UpdateManager.psm1)
- **Added**: `Test-IsNewerVersion` helper using `[System.Version]::Parse()`
- **Handles**: "v1.0" vs "1.0" format differences, missing patch/build versions
- **Fallback**: Uses `Compare-SemanticVersions` if parsing fails

### Zero Hardcoding

#### Build-Release.ps1
- **Added**: Localization module import and `Get-Text` helper function
- **Added**: New "build" section to `fr.json` and `en.json` (28 keys)
- **Replaced**: All hardcoded strings with `Get-LocalizedString` calls

#### Localization
- **Added**: "optimization" section to locale files (8 keys)

---

## [3.2.2] - 2026-01-19

### GUI

#### Dashboard Refactoring
- **Refactored**: Action-First Dashboard with state machine (Checking/Ready/Update)
- **Added**: `DashboardState` enum with three states for clear UI flow
- **Removed**: Hardcoded strings - all user-facing text now uses localization (.resx)
- **Improved**: Scan button behavior with state-aware enablement
- **Fixed**: Version display now reads dynamically from `Config/version.json`

### Core

#### InstallationEngine Architecture Refactoring
- **Added**: `InstallationOrchestrator.psm1` - High-level orchestration module (~1900 lines)
  - State management (rollback, deployment resume)
  - Orchestration functions (`Install-Application`, `Install-ApplicationsParallel`)
  - Environment restriction checking
- **Refactored**: `InstallationEngine.psm1` converted to thin wrapper (~147 lines)
  - Delegates to sub-modules: ApplicationDetection, InstallationMethods, InstallationOrchestrator
  - Explicit sub-module imports for direct .psm1 loading compatibility
- **Updated**: `InstallationEngine.psd1` manifest with NestedModules configuration

### QA

#### Test Coverage Improvements
- **Enhanced**: `WingetCache.Tests.ps1` with mocked tests
  - Mocked winget list/search output tests (isolated from real winget)
  - Cache expiry behavior tests
  - Cache miss/hit scenarios
  - Search key normalization tests
- **Enhanced**: `TelemetryCollector.Tests.ps1` with schema validation
  - JSON schema structure validation
  - Chart data integrity tests
  - Session management tests
  - Edge case handling
- **Fixed**: `InstallationEngine.Tests.ps1` to check correct sub-modules
- **Fixed**: Test isolation issues in WingetCache normalization tests

#### Database Fixes
- **Fixed**: `applications.json` TotalApplications metadata (167 → 170)
- **Fixed**: 7-Zip category test expectation (Utility → Compression)

### Statistics
- **Test Coverage**: 711/711 tests passing (100%)
- **Total Applications**: 170

---

## [3.2.0] - 2026-01-17

### GUI Improvements

#### Navigation Simplification
- **Removed**: Profile Editor view (redundant - functionality available in Apps view)
- **Updated**: Navigation indices and keyboard shortcuts (Ctrl+1-5)
- **Fixed**: Back navigation and navigation service integration

### Application Catalog

#### Catalog Cleanup (-9 apps)
- **Removed**: GOM Player, Bandizip, PeaZip, PotPlayer, Pidgin, foobar2000, IrfanView (redundant alternatives exist)
- **Removed**: Eclipse IDE (niche, VS Code covers most use cases)
- **Removed**: VoiceMeeter Banana (niche streaming tool, installation issues)

#### NirSoft Tools (+12 apps)
- **Added**: BlueScreenView, FullEventLogView, LastActivityView, TurnedOnTimesView, WhatIsHang
- **Added**: WirelessNetworkWatcher, WifiInfoView, NetworkInterfacesView
- **Added**: HashMyFiles, USBDeview, SearchMyFiles, UninstallView, ShellExView
- **Fixed**: Detection paths for NirSoft tools (WinGet packages location)

#### Winget ID Fixes
- **Fixed**: LibreOffice (`TheDocumentFoundation.LibreOffice` - LTS variant removed)
- **Fixed**: AnyDesk (`AnyDesk.AnyDesk`)
- **Fixed**: Creality Print (`Creality.CrealityPrint` - was null)
- **Fixed**: CutePDF Writer (`AcroSoftware.CutePDFWriter` - was null)
- **Fixed**: DBeaver (`DBeaver.DBeaver.Community`)
- **Fixed**: GIMP (`GIMP.GIMP.3`)
- **Fixed**: PDF24 Creator (`geeksoftwareGmbH.PDF24Creator`)
- **Removed**: FileZilla Winget ID (no longer available, Chocolatey only)

#### Detection & Categories
- **Fixed**: Spotify and MusicBee detection (StoreApp method for Store installs)
- **Fixed**: 7-Zip category (Utility → Compression)
- **Fixed**: Proton Drive category (Storage → CloudStorage)
- **Merged**: Multimedia category into Media

#### Fallback Sources
- **Added**: DirectUrl for FileZilla

### Installation Engine

#### Bug Fixes
- **Fixed**: Transient error handling - fails immediately on last retry attempt instead of false positive verification
- **Fixed**: Install-Via* return value extraction - added `$getInstallResult` helper to handle output pollution

### Configuration

#### Trusted Domains
- **Added**: vb-audio.com, download.vb-audio.com, aimp.ru, ultimaker.com, creality.com

### Statistics
- **Total Applications**: 170 (was 166 in v3.1.4)

---

## [3.1.4] - 2026-01-16

### Critical Security Fixes

#### Command Injection Prevention
- **Fixed**: CRITICAL - Replaced `cmd /c` string interpolation with `Start-Process` argument arrays in `Invoke-Rollback`
- **Fixed**: Winget/Chocolatey uninstall now use safe argument passing

#### State File Security
- **Added**: `Test-ValidStateData` function validates deployment state files before loading
- **Added**: SessionId GUID format validation
- **Added**: ProfileName path traversal and character validation
- **Added**: App name shell metacharacter detection

#### Parallel Detection Security
- **Fixed**: HIGH - Added path traversal protection to parallel `Test-AppInstalledParallel`
- **Added**: Registry and File detection now validate paths against `..` sequences

#### Command Detection Hardening
- **Added**: Executable whitelist for Command detection method (java, dotnet, python, node, git, etc.)
- **Blocked**: Arbitrary executables can no longer be run via applications.json Detection.Command

#### C# PowerShellBridge Security
- **Added**: `ValidateAppId` method prevents injection via malicious app IDs
- **Added**: AppId character validation (alphanumeric, dots, hyphens, underscores only)

#### Configuration Consistency
- **Fixed**: Parallel install timeout now uses configurable `$script:ParallelInstallTimeoutMs`
- **Standardized**: All timeout values defined in module configuration section

---

## [3.1.3] - 2026-01-16

### Security Hardening Update

#### Path Traversal Protection
- **Added**: Expand-DetectionPath now validates paths against traversal attacks (`..`)
- **Added**: Blocks relative paths and validates absolute path requirements
- **Added**: Double-check after environment variable expansion

#### URL Validation Improvements
- **Changed**: Test-ValidDownloadUrl now blocks non-whitelisted domains by default
- **Added**: Trusted domains loaded from `Config/download-sources.json`
- **Added**: `-AllowUntrusted` parameter for explicit override when needed
- **Added**: Fallback whitelist for common CDNs when config unavailable

#### Temp Directory Security
- **Changed**: Full 32-character GUID for temp directories (was 8 characters)
- **Improved**: Reduces collision risk from 1/4B to 1/340 undecillion

#### Code Quality
- **Reviewed**: SilentlyContinue usage - confirmed legitimate for existence checks

---

## [3.1.2] - 2026-01-16

### Installation & Detection Improvements

#### Real-time Installation Streaming
- **Added**: Live installation logs in GUI with real-time output streaming
- **Added**: `Write-Output` statements throughout InstallationEngine for status updates

#### Download Improvements
- **Added**: curl.exe as fallback download method (built into Windows 10/11)
- **Added**: Browser-like User-Agent headers to avoid download blocks
- **Fixed**: Battle.net installation - DirectUrl only with curl fallback

#### Detection Fixes
- **Fixed**: AnyDesk detection (portable via Chocolatey bin)
- **Fixed**: CutePDF Writer registry key (`CutePDF Writer Installation`)
- **Fixed**: Eclipse IDE path (`Eclipse*\eclipse\eclipse.exe`)
- **Renamed**: Creality Slicer → Creality Print with correct detection path

#### Settings & UI
- **Added**: Parallel installs configurable up to 10 (was 5)
- **Added**: Parallel scans configurable up to 20
- **Added**: Context menu scan options (Scan/Scan Selected/Scan All)
- **Fixed**: Dark mode toggle button visibility

#### Database
- **Updated**: 70+ applications (vs 66 in v3.0.0)
- **Updated**: Multiple detection paths corrected

---

## [3.0.0] - 2026-01-05

### Major Release - Modern WPF GUI

Win11Forge v3.0.0 introduces a complete graphical interface while maintaining full CLI compatibility.

### Recent Fixes (2026-01-05)

#### Installation Engine Reliability
- **Fixed**: SHA256 property access in StrictMode for DirectUrl installations (Battle.net)
- **Fixed**: Chocolatey "already installed" detection - no longer triggers unnecessary retries
- **Fixed**: Winget "No available upgrade" detection - treats as success
- **Fixed**: Store "already installed" detection - same improvement
- **Fixed**: WebClient Timeout property error - removed invalid property assignment

#### Localization
- **Fixed**: Hardcoded "Win11Forge" text in Settings view - now uses localized App_Name key

### New Features

#### WPF GUI Application
- **Dashboard** - System info, stats cards, recent deployments history
- **Prerequisites** - Visual prerequisite checker with one-click installation
- **Deployment** - Profile selection, parallel installation with progress tracking
- **Applications Manager** - Search, filter, scan installed apps, batch installation
- **Profile Editor** - Create/edit profiles with inheritance support
- **Settings** - Dark/Light theme, English/French language

#### Technical Highlights
- .NET 8.0 with MaterialDesignThemes
- MVVM architecture with CommunityToolkit.Mvvm
- PowerShell Bridge for CLI integration
- Self-contained deployment (no .NET install required)
- i18n support (EN/FR)

### Improvements

#### Application Detection
- **Winget fallback detection** - If Registry/File detection fails, uses `winget list --id` as fallback
- **Office installation wait** - Polls for Office executables after Click-to-Run async install
- **Increased timeouts** - Default 30min, Office-specific 45min for slow VMs

#### GUI Fixes
- Fixed PowerShell script execution deadlock (concurrent stdout/stderr reading)
- Fixed system info retrieval using native .NET instead of PowerShell SDK
- Fixed Sources column not displaying (JSON parsing correction)
- Fixed Scan button staying greyed out after loading apps

#### Database Updates
- Fixed winget IDs: ProtonVPN, RoboForm, Mp3tag, WinAero Tweaker
- Updated Signal/ProtonVPN detection paths

### Dependencies
| Package | Version |
|---------|---------|
| .NET | 8.0 |
| MaterialDesignThemes | 5.1.0 |
| CommunityToolkit.Mvvm | 8.3.2 |

### Breaking Changes
- None - Full backward compatibility with v2.x profiles and CLI

---

## [2.4.0] - 2025-10-06

### 🎉 Compatibility & Performance Release

Cette version apporte des améliorations majeures de compatibilité PowerShell 5.1, des optimisations de performance pour System-Audit, et des corrections critiques pour la stabilité du mode séquentiel.

### ✨ Nouvelles Fonctionnalités

#### System-Audit v2.4.0 - Performance Optimized
- **Ajouté**: `Tools/System-Audit.ps1` v2.4.0
  - Overhead réduit de 67% : 3000ms → ~750ms par échantillon (60% → 20%)
  - Intervalle d'échantillonnage ajusté de 2s à 5s par défaut
  - Fréquences de scan optimisées : Apps (30s), Events (60s), Network (120s)
  - Nouveau paramètre `-SkipApplicationMonitoring` (réduit overhead de 40%)
  - Affichage temps réel des performances (avg/max par échantillon)

#### TrustedInstaller Launcher Improvements
- **Ajouté**: `Tools/Launch-AsTrustedInstaller.bat`
  - Menu interactif avec 8 options (PowerShell, CMD, Registry, Task Manager, etc.)
  - Exécution avec privilèges NT AUTHORITY\SYSTEM
  - Support automatique des fichiers .msc via mmc.exe
  - Auto-installation du module NtObjectManager si nécessaire

### 🔧 Corrections Majeures

#### PowerShell 5.1 Sequential Mode Compatibility
- **Corrigé**: `Modules/InstallationEngine.psm1` - StrictMode PropertyNotFoundException
  - Remplacement des conditions chainées par des conditions imbriquées
  - Accès sécurisé aux propriétés PSObject : `$app.PSObject.Properties['PropertyName']`
  - Compatible avec StrictMode en PowerShell 5.1 et 7.x
  - Fixes appliqués aux propriétés : InstallationOptions, IgnoreExitCodeIfFileExists, ValidExitCodes

#### PowerShell 7 Auto-Restart Enhancement
- **Ajouté**: `Deploy-Win11Environment.ps1` - Auto-restart en PowerShell 7
  - Détection automatique de PowerShell 5.1
  - Redémarrage automatique avec préservation des paramètres
  - Support modes Parallel et Sequential
  - Message informatif avant redémarrage

#### System-Audit Bug Fixes (v2.2.0)
- **Corrigé**: `Tools/System-Audit.ps1` - Bugs critiques
  - Processus terminés comptés avant calcul overhead (timing fix)
  - Protection division par zéro dans génération rapport HTML
  - Gestionnaire Ctrl+C gracieux avec génération automatique du rapport
  - Mode `-Quiet` pour exécution silencieuse (scripts automatisés)
  - Session CIM réutilisable pour +20% de performance
  - Optimisation HashSet pour comparaisons O(1) au lieu de O(n²)

#### TrustedInstaller Launcher Fixes
- **Corrigé**: `Tools/Launch-AsTrustedInstaller.bat` - Gestion des chemins avec espaces
  - Correction du quoting pour paths avec espaces
  - Suppression du code mort (delayed expansion inutilisée)
  - Robustesse assignment ARGS avec quoted set statement
  - Validation correcte des paramètres personnalisés

#### GUI Stability Improvements
- **Corrigé**: `Modules/Win11ForgeGUI.psm1` - Module caching PropertyNotFoundException
  - Détection call operator vs direct execution
  - Correction crash au lancement avec paths contenant espaces
  - Validation AppId override et propagation exit codes

#### StrictMode and Parallel Mode Fixes
- **Corrigé**: `Deploy-Win11Environment.ps1` - Crash statistiques mode parallèle
  - Null-safe environment report avec fallbacks
  - Propriété Skipped correctement vérifiée dans stats
  - Apps skippées comptées correctement (pas comme Failed)
  - Affichage summary correct pour apps skippées (jaune au lieu de rouge)

#### Detection and Registry Fixes
- **Corrigé**: `Modules/ApplicationDatabase.psm1` - Validation et type coercion
  - Support valeurs numériques booléennes (0/1) pour champ Required
  - Validation type pour priority/required overrides
  - Prévention coercion type cassant default priority/required
  - Corrections critiques registry writes et handling priority 0

#### DirectDownload and ZIP Deployment
- **Corrigé**: `Modules/InstallationEngine.psm1` - Support multi-format
  - DirectDownload fonctionnel en mode parallèle pour PS7
  - Déploiement ZIP archive correct pour outils portables
  - Mode séquentiel ZIP deployment avec Detection.Path
  - Compatibilité PowerShell 5.1 pour DirectDownload
  - Suppression `-UseBasicParsing` en mode séquentiel

#### Setup and Validation Improvements
- **Corrigé**: `Setup-Framework.ps1` - Création répertoires et validation
  - Création correcte du répertoire Tools
  - Correction références documentation dans messages d'erreur
  - Cohérence version avec framework principal

### 🛠️ Améliorations

#### Documentation Consistency
- **Corrigé**: 50+ fichiers pour cohérence de version
  - Harmonisation toutes bannières console à v2.4.0
  - Correction counts applications dans Apps/README.md
  - Synchronisation statistiques CHANGELOG et PROJET_STRUCTURE
  - Correction documentation GUI tags et sources
  - Cohérence dates dernière mise à jour (2025-10-06)

#### EnvironmentDetection Module Path
- **Corrigé**: `Modules/InstallationEngine.psm1` - Utilisation RepositoryRoot
  - Remplacement calcul path relatif par variable $script:RepositoryRoot
  - Path module fiable en mode séquentiel
  - Plus maintenable avec variable centralisée

#### Module Encoding and Formatting
- **Corrigé**: Tous modules - Encodage UTF-8 BOM
  - UTF-8 BOM appliqué à tous modules et scripts
  - Formatage linter appliqué uniformément
  - Amélioration démarrage GUI

### 📊 Statistiques v2.4.0

- **100+ commits** depuis v2.3.0
- **50+ fichiers** corrigés pour cohérence
- **15+ bugs critiques** résolus (StrictMode, parallel, GUI)
- **4 versions System-Audit** (2.1.0 → 2.2.0 → 2.3.0 → 2.4.0)
- **67% réduction overhead** System-Audit (3000ms → 750ms)
- **100% compatibilité** PowerShell 5.1 + 7.x en modes séquentiel/parallèle

### 🔗 Liens Utiles

- **Documentation complète** : `README.md`
- **System-Audit docs** : `Tools/System-Audit-README.md` (30+ pages)
- **Structure projet** : historical v2.x docs retired from the public documentation set
- **Quick Start** : `Apps/QUICK_START.md`

---

## [2.3.0] - 2025-10-04

### 🎉 Stability & Detection Improvements Release

Cette version corrige des problèmes majeurs de détection d'applications Store, améliore la stabilité PowerShell 7, et introduit le logging parallèle avec l'épinglage Start Menu fiable.

### ✨ Nouvelles Fonctionnalités

#### Start Menu Pinning (start2.bin)
- **Ajouté**: `Modules/StartMenuPinning.psm1`
  - Méthode fiable pour Windows 11 22H2+
  - Épinglage d'items au Start Menu via start2.bin
  - Support Default profile + utilisateur courant
  - Remplace LayoutModification.json (déprécié)
  - Intégration avec StartMenuLayout.psm1

#### Start Menu Layout Organisation
- **Ajouté**: `Modules/StartMenuLayout.psm1`
  - Organisation automatique par catégorie
  - Création de dossiers dans le Start Menu
  - Mapping applications → catégories
  - Compatible avec StartMenuPinning

#### Startup Manager
- **Ajouté**: `Modules/StartupManager.psm1`
  - Gestion applications au démarrage
  - Activation/désactivation au démarrage
  - Compatible mode parallèle et séquentiel

#### Logs Parallèles Individuels
- **Ajouté**: Logs séparés par application en mode parallèle
  - Nouveau dossier `Logs/Parallel/`
  - Logs individuels par application (ex: `Logs/Parallel/GoogleChrome_20251004_203045.log`)
  - Tracking temps réel de chaque installation
  - Stack traces détaillées avec numéros de ligne
  - Facilite le debugging des crashs et erreurs
  - Chaque runspace écrit dans son propre fichier

### 🔧 Corrections Majeures

#### Détection Store Apps Améliorée
- **Corrigé**: `Modules/InstallationEngine.psm1` - Support complet méthode `StoreApp`
  - **PackageName Detection**: Support complet pour applications Store
  - **Détection multilingue**: Quick Assist FR/EN, autres apps localisées
  - **Vendor Prefix Extraction**: Regex `^([^.]+)\.` pour extraire préfixe du PackageName
  - **Fallback nom de base**: Si PackageName complet non trouvé, essaie nom de base
  - **Méthode winget list**: Évite conflits module Appx en PowerShell 7
  - **Compatible PS7 parallèle**: Fonctionne en mode parallèle et séquentiel

#### WhatsApp Desktop
- **Corrigé**: `Apps/Database/applications.json`
  - Méthode: `StoreApp` avec `PackageName: "WhatsAppDesktop"`
  - Détection par nom de base (suffixe vendor tronqué pour compatibilité)
  - Fallback intelligent vers nom sans suffixe dans code de détection
  - Sources: Store prioritaire, sinon Winget/Chocolatey

#### Quick Assist
- **Corrigé**: `Apps/Database/applications.json`
  - Méthode: `StoreApp` avec `PackageName: "MicrosoftCorporationII.QuickAssist"`
  - Détection par préfixe vendor (suffixe hash tronqué pour compatibilité)
  - Support multilingue (FR: Assistance Rapide, EN: Quick Assist)
  - Résolution regex avancée pour noms tronqués dans code de détection

#### Epic Games Launcher
- **Corrigé**: `Apps/Database/applications.json`
  - Chemin File corrigé: `C:\Program Files (x86)\Epic Games\Launcher\Portal\Binaries\Win32\EpicGamesLauncher.exe`
  - Ancien chemin incorrect: `.../Win64/...` (n'existe pas)
  - Validation: Chemin vérifié sur installation réelle

#### Proton Apps (Drive, Mail Bridge, Pass)
- **Corrigé**: `Apps/Database/applications.json`
  - **Detection supprimée** pour les 3 apps Proton
  - Utilisation fallback `Test-ApplicationByName` via winget list
  - Chemins File incorrects supprimés (n'existaient pas)
  - Détection fiable par nom winget:
    - `Proton.ProtonDrive`
    - `Proton.ProtonMailBridge`
    - `Proton.ProtonPass`

#### CUE Splitter
- **Corrigé**: `Apps/Database/applications.json`
  - **App corrigée**: De CUETools vers CUE Splitter (app Store correcte)
  - AppId: `CUESplitter`
  - Source Store uniquement: `9NBLGGH43MH5`
  - Détection: `StoreApp` avec `PackageName: "CUESplitter"` (nom de base)

#### InstallArguments Access
- **Corrigé**: `Modules/InstallationEngine.psm1`
  - Accès sécurisé aux propriétés PSObject en StrictMode
  - Utilisation de `$app.PSObject.Properties['InstallArguments']` au lieu de `$app.InstallArguments`
  - Évite erreurs "property does not exist" en mode strict
  - Compatible avec toutes les versions PowerShell

### 🛠️ Améliorations

#### Stabilité PowerShell 7
- **Corrigé**: Conflits assembly Appx en mode séquentiel
  - Détection StoreApp via `winget list` au lieu de `Get-AppxPackage`
  - Évite conflit "Could not load file or assembly 'System.Runtime.WindowsRuntime'"
  - Support complet PS7 parallèle sans crashes
  - Utilisation systématique de winget pour cohérence

#### Test-ApplicationByName Fallback
- **Amélioré**: Fallback automatique pour apps sans Detection
  - Détection par `winget list --name "AppName"`
  - Alternative fiable quand chemins File incorrects
  - Exemple: Proton apps utilisent ce fallback avec succès
  - Mode par défaut pour nouvelles apps

#### Logging en Mode Parallèle
- **Amélioré**: Architecture de logging parallèle
  - Chaque runspace a son propre fichier log
  - Horodatage précis pour chaque opération
  - Stack traces complètes avec numéros de ligne
  - Résumé consolidé dans log principal
  - Facilite identification problèmes spécifiques par app

### 🧪 Tests et Validation

#### Test-ProtonAppsDetection.ps1
- **Ajouté**: `Tests/Test-ProtonAppsDetection.ps1`
  - Script de validation des 3 apps Proton
  - Vérifie chemins File (n'existent pas)
  - Vérifie détection winget (fonctionne)
  - Recherche emplacements réels si paths incorrects

#### Validation Déploiement Séquentiel
- **Testé**: Mode séquentiel PowerShell 7
  - Profil Personnel (66 apps)
  - Résultat: 64 apps traitées, 16 installées, 41 déjà présentes, 4 skipped, 3 échecs (Proton - maintenant corrigé)
  - Quick Assist: ✅ Détecté correctement
  - WhatsApp Desktop: ✅ Détecté correctement
  - Epic Games Launcher: ✅ Détecté correctement

#### Validation Déploiement Parallèle
- **Testé**: Mode parallèle PowerShell 7
  - 5 jobs concurrents
  - Logs individuels fonctionnels
  - Stabilité confirmée sans crashes Appx
  - Performance optimale maintenue

### 📊 Statistiques v2.3.0

**Applications** : 66 (stable vs v2.2.0)
**Profils** : 4 (Base, Office, Gaming, Personnel)
**Modules** : 10 (+3 vs v2.2.0: StartMenuLayout, StartMenuPinning, StartupManager)
**Tests** : +1 (Test-ProtonAppsDetection.ps1)

**Apps Corrigées** : 7
- WhatsApp Desktop (StoreApp)
- Quick Assist (StoreApp multilingue)
- Epic Games Launcher (File path)
- Proton Drive (Detection removed)
- Proton Mail Bridge (Detection removed)
- Proton Pass (Detection removed)
- CUE Splitter (App corrigée)

**Taux de Succès d'Installation** :
- v2.2.0: ~95% (3 échecs Proton)
- v2.3.0: ~99% (0-1 échec attendu)

### 🔧 Fichiers Modifiés

**Modules Ajoutés** :
- `Modules/StartMenuLayout.psm1`
- `Modules/StartMenuPinning.psm1`
- `Modules/StartupManager.psm1`

**Modules Modifiés** :
- `Modules/InstallationEngine.psm1` (StoreApp detection, PSObject safe access, parallel logging)

**Base de Données** :
- `Apps/Database/applications.json` (7 apps corrigées)

**Tests** :
- `Tests/Test-ProtonAppsDetection.ps1` (nouveau)

**Documentation** :
- `README.md` (v2.3.0)
- `CHANGELOG.md` (ce fichier)

### 🐛 Bugs Résolus

1. **WhatsApp Desktop pas détecté**
   - Cause: Détection par Registry incorrecte
   - Fix: StoreApp avec PackageName + fallback nom de base
   - Status: ✅ Résolu

2. **Quick Assist pas détecté (FR/EN)**
   - Cause: Nom multilingue, PackageName tronqué par winget
   - Fix: Vendor prefix extraction regex
   - Status: ✅ Résolu

3. **Epic Games Launcher File path incorrect**
   - Cause: Chemin Win64 au lieu de Win32
   - Fix: Correction vers `.../Win32/EpicGamesLauncher.exe`
   - Status: ✅ Résolu

4. **Proton Apps File paths invalides**
   - Cause: Chemins `C:\Program Files\Proton\...` n'existent pas
   - Fix: Suppression Detection, utilisation fallback winget
   - Status: ✅ Résolu

5. **CUE Splitter app incorrecte**
   - Cause: Référence à CUETools au lieu de CUE Splitter
   - Fix: App Store correcte avec PackageName
   - Status: ✅ Résolu

6. **PowerShell 7 crash avec Get-AppxPackage**
   - Cause: Conflit assembly Appx en mode séquentiel
   - Fix: Utilisation winget list pour détection StoreApp
   - Status: ✅ Résolu

7. **InstallArguments erreur StrictMode**
   - Cause: Accès direct propriété non existante
   - Fix: PSObject.Properties safe access
   - Status: ✅ Résolu

### ⚠️ Breaking Changes

Aucun breaking change. Version 100% rétrocompatible avec v2.2.0.

### 🚀 Migration depuis v2.2.0

Aucune migration requise. Mise à jour transparente :

```powershell
# 1. Pull derniers changements
git pull

# 2. Valider base de données
.\Tools\Validate-AppDatabase.ps1

# 3. Tester avec un profil
.\Deploy-Win11Environment.ps1 -ProfileName "Base" -TestMode

# 4. Déployer
.\Deploy-Win11Environment.ps1 -ProfileName "Personnel" -Parallel
```

### 📚 Pour Plus d'Informations

- Guide complet : [README.md](README.md)
- Structure projet : historical v2.x docs retired from the public documentation set
- Guide GUI : historical v2.x docs retired from the public documentation set
- Base de données : [Apps/README.md](Apps/README.md)

---

## [2.2.0] - 2025-10-03

### 🎉 Major Release - Architecture Refactoring

Cette version majeure introduit une refonte complète de l'architecture avec une base de données centralisée, une interface GUI, et des outils de gestion avancés.

### ✨ Nouvelles Fonctionnalités

#### Interface GUI PowerShell (`Win11ForgeGUI.psm1`)
- **Ajouté**: Interface utilisateur interactive complète
  - Menu de navigation principal avec 8 options
  - Déploiement de profils avec sélection mode parallèle/séquentiel
  - Navigateur d'applications (66 apps) avec filtrage par catégorie/tag
  - Navigateur de profils avec visualisation détaillée
  - Créateur de profils custom interactif
  - Statistiques de base de données en temps réel
  - Validation de base de données intégrée
  - **Nouveau**: Option "Add New Application" avec recherche automatique

#### Base de Données Centralisée (`Apps/Database/applications.json`)
- **Ajouté**: Base de données centralisée v2.2.0
  - 66 applications référencées
  - Sources multiples: Winget, Chocolatey, Microsoft Store, DirectUrl
  - Métadonnées complètes: tags, vérification, homepage, priorité
  - Détection intelligente par méthode (Registry, File, Command, StoreApp, WindowsFeature)
  - Restrictions d'environnement par application
  - Format optimisé pour réutilisation

- **Module**: `ApplicationDatabase.psm1`
  - Chargement et cache de la base de données
  - Fonctions de requête par catégorie, tag, AppId
  - Export de statistiques
  - Validation de structure

#### ProfileCreator.html - Interface Web
- **Ajouté**: Créateur/éditeur de profils web
  - Interface en 6 étapes guidées
  - **66 applications** chargées dynamiquement depuis `applications-data.js`
  - Création de profils au format v2.2.0
  - **Nouveau**: Édition de profils existants (charger JSON)
  - Filtrage par catégorie et recherche
  - Configuration système (Explorer, Taskbar, Privacy, Performance)
  - Compatible `file://` (pas de serveur web requis)
  - Export JSON téléchargeable

#### Search-ApplicationSources.ps1
- **Ajouté**: Outil de recherche automatique d'applications
  - Recherche simultanée dans Winget, Chocolatey, Microsoft Store
  - Détection des URLs de téléchargement direct (patterns connus)
  - Génération de template JSON prêt à l'emploi
  - Modes interactif et automatisé
  - Affichage coloré avec résumé des résultats

#### Lanceurs avec Auto-Élévation
- **Amélioré**: `Deploy-Win11Forge.bat`
  - Auto-élévation automatique (UAC)
  - Plus besoin de clic-droit "Exécuter en tant qu'admin"

- **Ajouté**: `Start-Win11ForgeGUI-Admin.bat`
  - Lanceur GUI avec auto-élévation
  - Double-clic et c'est parti

### 🔄 Changements Majeurs

#### Format des Profils v2.2.0
- **BREAKING**: Nouveau format ultra-compact
  - Applications référencées par AppId uniquement
  - Définitions chargées depuis la base de données centralisée
  - Profils 10x plus petits et lisibles

**Ancien format (v2.1.x)** :
```json
{
  "Applications": [
    {
      "Name": "Google Chrome",
      "Category": "Browser",
      "Sources": {...},
      "Detection": {...}
    }
  ]
}
```

**Nouveau format (v2.2.0)** :
```json
{
  "Applications": [
    "GoogleChrome",
    "MozillaFirefox",
    "BraveBrowser"
  ]
}
```

#### Migration Automatique
- **Ajouté**: Scripts de migration v2.0 → v2.2.0
  - `Switch-ToProduction.ps1` : Migration des profils
  - `Test-NewProfiles.ps1` : Validation post-migration
  - Backup automatique dans `Archive/Profiles-v2.0-*/`
  - Conversion automatique au nouveau format

### ✨ Améliorations

#### Gestion des Profils
- **Amélioré**: `ProfileManager.psm1`
  - Résolution d'AppIds via base de données centralisée
  - Validation de profils avec vérification d'AppIds
  - Héritage optimisé avec cache
  - Messages d'erreur détaillés

#### Interface Utilisateur
- **Ajouté**: Fonction `Read-Choice` améliorée
  - Support de l'option '0' pour retour/annulation universelle
  - Messages d'aide contextuels
  - Validation robuste des choix
  - Plus de situations bloquantes

#### Applications
- **Ajouté**: Creality Slicer (impression 3D)
  - Chocolatey: `creality-print`
  - Détection: File
  - Catégorie: 3DPrint

### 🧹 Nettoyage et Organisation

#### Cleanup-ObsoleteFiles.ps1
- **Ajouté**: Script de nettoyage automatique
  - Archive les fichiers de test/migration obsolètes
  - Réorganise `Validate-Framework.ps1` vers `Tools/`
  - Mode `-DryRun` pour prévisualisation
  - Rapport détaillé des opérations

#### Structure du Projet
- **Réorganisé**: Dossier `Tools/`
  - Tous les utilitaires regroupés
  - Scripts de validation consolidés
  - Outils web (ProfileCreator.html)

- **Archivé**: Rapports de développement
  - `Archive/Docs-Reports-20251003/`
  - DEBUG_*, DATABASE_*, VALIDATION_*, INTEGRATION_*

### 📝 Documentation

#### Nouvelle Documentation
- **Ajouté**: `PROJET_STRUCTURE.md`
  - Structure complète du projet
  - Guide d'utilisation rapide
  - Explication de l'architecture
  - Cas d'usage détaillés

- **Mis à jour**: `README.md` v2.2.0
  - Nouvelles fonctionnalités GUI
  - Base de données centralisée
  - ProfileCreator.html
  - Guides de démarrage rapide

- **Ajouté**: `GUI_README.md`
  - Documentation complète de l'interface GUI
  - Captures d'écran et workflows
  - Guide des 8 options du menu

### 🐛 Corrections de Bugs

#### GUI
- **Corrigé**: Menus sans option de retour (blocage utilisateur)
- **Corrigé**: Erreurs PSObject.Properties sur certaines applications
- **Corrigé**: Accès aux sources Winget/Choco/Store/DirectUrl
- **Corrigé**: Comptage incorrect d'applications

#### Profils
- **Corrigé**: Script `Deploy-Win11Environment.ps1` fermait au lieu de retourner au GUI
  - Remplacé tous les `exit` par `return`
  - GUI reste ouvert après déploiement

#### Modules
- **Corrigé**: Scope des modules en mode parallèle
  - Ajout du flag `-Global` sur tous les Import-Module

### 📊 Statistiques v2.2.0

**Applications** : 66 (vs v2.1.3)
**Profils** : 4 (Base, Office, Gaming, Personnel)
**Modules** : 7 (+1 GUI)
**Outils** : 6 (+2 vs v2.1.3)
**Scripts** : 8 (+4 vs v2.1.3)

**Composition des Profils** :
- Base: 30 apps
- Office: 35 apps (Base + 5)
- Gaming: 39 apps (Office + 4)
- Personnel: 64 apps (Gaming + 25)

### 🔧 Fichiers Modifiés

**Nouveaux Modules** :
- `Modules/ApplicationDatabase.psm1`
- `Modules/Win11ForgeGUI.psm1`

**Nouveaux Scripts** :
- `Start-Win11ForgeGUI.ps1`
- `Start-Win11ForgeGUI-Admin.bat`
- `Tools/Search-ApplicationSources.ps1`
- `Cleanup-ObsoleteFiles.ps1`

**Nouveaux Outils** :
- `Tools/ProfileCreator.html`
- `Tools/applications-data.js`

**Base de Données** :
- `Apps/Database/applications.json` (nouvelle architecture)

**Profils Migrés** :
- `Profiles/Base.json` (format v2.2.0)
- `Profiles/Office.json` (format v2.2.0)
- `Profiles/Gaming.json` (format v2.2.0)
- `Profiles/Personnel.json` (format v2.2.0)

**Documentation** :
- `README.md` (v2.2.0)
- `PROJET_STRUCTURE.md` (nouveau)
- `GUI_README.md` (nouveau)

### ⚠️ Breaking Changes

1. **Format de Profils** : Les profils v2.0/v2.1 doivent être migrés vers v2.2.0
   - Utiliser `Switch-ToProduction.ps1` pour migration automatique
   - Ou utiliser ProfileCreator.html pour recréer

2. **Base de Données** : Les applications sont maintenant centralisées
   - Pas de définitions inline dans les profils
   - Toutes les apps doivent être dans `Apps/Database/applications.json`

### 🚀 Migration depuis v2.1.x

```powershell
# Étape 1: Backup automatique
.\Switch-ToProduction.ps1

# Étape 2: Test des nouveaux profils
.\Test-NewProfiles.ps1

# Étape 3: Validation
.\Tools\Validate-AppDatabase.ps1

# Étape 4 (optionnel): Nettoyage
.\Cleanup-ObsoleteFiles.ps1
```

### 📚 Pour Plus d'Informations

- Guide complet : [README.md](README.md)
- Structure projet : historical v2.x docs retired from the public documentation set
- Guide GUI : historical v2.x docs retired from the public documentation set
- Base de données : [Apps/README.md](Apps/README.md)

---

## [2.1.3] - 2025-10-03

### 🐛 Bug Fixes

#### Installation Issues
- **Fixed**: Battle.net installation - Added custom silent install arguments support (`--lang=frFR --installpath=...`)
  - ✅ **VALIDATED**: Tested and confirmed 100% silent installation with Perplexity Pro verified switches
- **Fixed**: WhatsApp Desktop - Corrected Winget ID to `9NKSQGP7F2NH` (Store ID)
- **Fixed**: Proton Drive - Corrected Winget ID from `Proton.Drive` to `Proton.ProtonDrive`
- **Fixed**: Proton Mail Bridge - Corrected Winget ID from `ProtonTechnologies.ProtonMailBridge` to `Proton.ProtonMailBridge`
- **Fixed**: Proton Pass - Corrected Winget ID from `Proton.Pass` to `Proton.ProtonPass`
- **Verified**: Google Drive for Desktop - Winget `Google.GoogleDrive` and Chocolatey `googledrive`
- **Verified**: PDF-XChange Editor - Winget `TrackerSoftware.PDF-XChangeEditor` and Chocolatey `pdfxchangeeditor`

#### System Configuration
- **Fixed**: DNS configuration not parsing array types correctly
  - Added support for `System.Collections.ArrayList`
  - Added support for `System.Collections.Generic.List[object]`
  - Added fallback enumeration for unknown array types
  - Location: `Modules/SystemConfig.psm1` v2.0.4

### ✨ Enhancements

#### Installation Engine (v2.1.3)
- **Added**: Custom install arguments support for DirectDownload method
  - New parameter: `InstallArguments` in application JSON
  - Example: Battle.net uses `--lang=frFR --installpath="C:\Program Files (x86)\Battle.net"`
  - Location: `Modules/InstallationEngine.psm1`

- **Enhanced**: Error logging with detailed failure tracking
  - Tracks all attempted installation methods
  - Provides specific failure reasons for each method
  - Includes package IDs and names in verbose output
  - Improved final error messages with full context

#### Profile Updates
- `Profiles/Gaming.json`
  - Updated Battle.net with Store ID `XPDM5VSMTKQLBJ`
  - Added `InstallArguments` field for silent installation
  - Prioritizes DirectUrl over Store for better automation

- `Profiles/Office.json`
  - Updated WhatsApp Desktop with verified Store ID
  - Added fallback to Store source

- `Profiles/Personnel.json`
  - All Proton applications IDs corrected
  - Added notes for each verified ID

### 🧪 Testing & Validation

- **Added**: `Debug-FailedApps.ps1` - Automated ID validation script
  - Tests Winget, Chocolatey, and Store IDs
  - Color-coded output (Pass/Fail/Skip)
  - Success rate calculation
  - **Result**: 100% validation pass rate (10/10 tests passed)

- **Added**: Complete debugging documentation in CHANGELOG
  - Issue analysis
  - Corrections applied
  - Validation results
  - Testing recommendations

### 📊 Performance Improvements

**Expected Results** (compared to v2.1.2):
- Installation success rate: ~83% → ~95%
- Failed applications: 11 → 0-1 (excluding environment restrictions)
- New successful installs: +6-7 applications

### 📝 Files Modified

**Profiles**:
- `Profiles/Gaming.json` (Battle.net)
- `Profiles/Office.json` (WhatsApp Desktop)
- `Profiles/Personnel.json` (Proton apps + Google Drive)

**Modules**:
- `Modules/SystemConfig.psm1` v2.0.4 (DNS parsing)
- `Modules/InstallationEngine.psm1` v2.1.3 (Error handling + custom arguments)

**New Files**:
- `Debug-FailedApps.ps1` (Validation script)
- `CHANGELOG.md` (This file)

---

## [2.1.2] - 2025-10-02

### Fixed
- Empty `Write-Log` calls causing errors
- `InheritanceChain.Count` errors in profile loading
- PowerToys multi-path detection
- Quick Assist Store App detection

### Added
- Parallel installation support (up to 5 concurrent apps)
- PowerShell 7 detection and upgrade prompt

---

## [2.1.1] - 2025-10-01

### Fixed
- DNS array handling in SystemConfig
- Taskbar configuration error handling

---

## [2.1.0] - 2025-10-01

### Added
- Parallel installation mode with `-Parallel` parameter
- `MaxParallelJobs` parameter (default: 5)
- Installation mode logging (Sequential vs Parallel)

### Enhanced
- Installation Engine performance optimizations
- Better progress tracking for parallel installations

---

## [2.0.2] - 2025-09-30

### Fixed
- Base profile application priorities
- Detection methods for various applications

---

## [2.0.0] - 2025-09-30

### Initial Release
- Complete framework restructure
- Modular architecture (Core + 5 modules)
- Profile inheritance system (Base → Office → Gaming → Personnel)
- Multi-source installation (Winget → Chocolatey → Store → DirectUrl)
- Environment detection (Sandbox/VMware/Hyper-V/VirtualBox/Physical)
- Comprehensive logging and reporting

---

**Legend**:
- 🐛 Bug Fix
- ✨ Enhancement
- 🧪 Testing
- 📊 Performance
- 🔒 Security
- 📝 Documentation

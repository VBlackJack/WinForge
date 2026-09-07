# WinForge Framework - Changelog

[Français](CHANGELOG.fr.md)

Note: the framework version source of truth is `Config/version.json`. Launchers and GUI read this value dynamically.

## [Unreleased]

## [2026090702] - 2026-09-07

### Fixed
- Complete the profile save dialog, reject invalid destinations and accidental replacement, and preserve existing profile metadata.
- Preserve corrupt deployment history, write atomically and report persistence failures.
- Propagate checkpoint failures and keep the original checkpoint until recovery succeeds.
- Cancel redirected PowerShell reads promptly and clean up failed UI test launches.

### Changed
- Constrain path factories to their roots and use an instance-owned lazy PowerShell discovery cache.
- Add regression coverage for cancellation, corrupt storage, locked destinations and profile names.

## [2026090601] - 2026-09-06

### Fixed
- Keep sandboxed plugin imports and hooks outside the main PowerShell session, execute immutable validated module snapshots, and safely unregister hooks.
- Preserve SystemConfig and custom JSON properties when saving or updating profiles, with atomic file replacement.
- Enforce process deadlines while draining standard output and standard error concurrently.
- Validate API JSON bodies through a supported in-memory parameter, fail closed when required schemas are missing, and run deployments in an owned background worker with overlap rejection and completion/failure reporting.
- Include runtime schemas and both documentation languages in release packages, and verify required archive entries before generating checksums.

### Build and documentation
- Pin Pester to 5.7.1 and use supported mock assertions.
- Make CI framework validation independent of ICMP and host write-permission probes.
- Pin ThemeForge to a recorded commit shared by CI and the local dependency resolver.
- Publish documentation in English with separate French files and reciprocal links.

## [2026081201] - 2026-08-12

### Security
- Command detection is now gated by an argument allowlist in addition to the executable allowlist, on all three paths (GUI probe, post-update verification, PowerShell detection modules). The executable list is not a boundary on its own because it permits interpreters (`python`, `node`, `pwsh`, `ruby`, `perl`, `php`) that accept code as an argument, and screening for shell metacharacters does not stop them: `pwsh -Command Start-Process calc` contains none and was executed. Detection only ever needs to ask a program for its version, and every entry in the shipped catalog uses `--version`, `-version` or `--list-runtimes`, so the permitted arguments are configured in `Config/detection-allowlist.json` and loaded fail-closed.
- The post-update verification path in the application-management service applied no argument guard at all - a third Command-detection execution site that the earlier audit missed.
- Plugin AST validation now uses a type allowlist instead of a denylist. Plugin code may only reference value types and PowerShell container types, and static member access is rejected unless the owning type is allowlisted. The previous denylist admitted `[System.Diagnostics.Process]::Start`, `[System.IO.File]::WriteAllText`/`Delete`, `[Microsoft.Win32.Registry]::SetValue`, `[System.Activator]::CreateInstance` and `[System.AppDomain]` reflection.
- Plugin handlers and plugin-load probes are now compiled inside a runspace whose `InitialSessionState` declares ConstrainedLanguage. Setting `$ExecutionContext.SessionState.LanguageMode` after `[scriptblock]::Create` had no effect, because a scriptblock carries the language mode it was compiled under, so handlers ran in FullLanguage.
- `Import-Plugin` re-fingerprints the entry point immediately before `Import-Module` and refuses the import if the file changed after sandbox validation, closing a time-of-check/time-of-use window on an import that runs in the main session at the host's privilege level.
- `trustedPublishers` in `Config/plugins-settings.json` is now enforced: when the list is non-empty, a plugin entry point must carry a valid Authenticode signature from one of the listed publishers. An empty list preserves the previous behaviour.
- The GUI detection probe now applies the same argument guard as the PowerShell detection paths before launching a Command detection. The executable allowlist alone was insufficient because it permits interpreters (`python`, `node`, `pwsh`, `ruby`, `perl`, `php`) that accept code as an argument.
- REST API endpoint handlers are validated against the same .NET type allowlist as plugin code instead of a separate denylist that admitted `[System.Diagnostics.Process]::Start`, `[System.IO.File]` and reflective activation. Handlers keep a slightly wider surface than plugins (`regex`, `StringComparison`, `System.IO.Path`), enumerated at the call site with the reason for each, so the plugin sandbox is not widened to accommodate them.
- REST API request bodies are now read under a hard cap instead of trusting the declared `Content-Length`. A chunked request reports `ContentLength64 = -1`, which passed the previous size check and allowed an unbounded read.
- Per-API-key rate limiting is now enforced in the request loop. The limiter was implemented, exported and unit-tested but never called, so only IP-based limiting ran - and on a localhost-only listener every caller shares one bucket.

### Changed
- `ApplicationManagementServiceImpl` is decomposed from 1635 to 1166 lines. Package matching, deployment log formatting, update-source routing and external-process execution move to `PackageMatcher`, `DeploymentLog`, `UpdateSourcePolicy` and `VendorCommandRunner`. The `protected virtual` seams that test doubles override are preserved and now delegate to the runner.
- Removed a dead copy of the CLIXML/binary output filter from the application-management service; the live implementation is in `PowerShellExecutionService`.
- `Build-Release.ps1` publishes a `.zip.sha256` alongside the release archive, CI verifies the digest before publishing, and the README documents the verification step.
- PSScriptAnalyzer now runs the full default rule set. The configuration previously combined an `IncludeRules` allowlist with an `ExcludeRules` list that cancelled 21 of the rules it had just enabled, and configured six rules that were themselves excluded, so the gate could not fail. Every remaining exclusion carries a specific justification.
- Application settings are validated when loaded: per-property constraints are enforced and invalid values are reset to their defaults and logged. The `Range`, `StringLength` and `RegularExpression` annotations on `AppSettings` were previously declarative only. The documented cross-field performance hint does not trigger a reset.
- Every `Config/*.json` file that declares a schema is now validated against it through `Test-AllConfigurationFiles`, wired into `Invoke-JsonSchemaValidation` and `Validate-Framework.ps1`. Twelve of the sixteen shipped schemas previously had no runtime consumer.
- `Verify-VersionConsistency.ps1` now covers the documentation files, which could previously state an older release while the check stayed green.
- GUI PowerShell timeouts are derived from `Config/timeouts-settings.json` instead of being hardcoded, with the built-in values kept only as fallbacks. The installation timeout is computed from the slowest configured install plus explicit headroom, so raising a timeout in configuration no longer leaves the GUI killing installs early and reporting a timeout the engine never saw.
- Removed the unused `PowerShellProcessWrapper` (271 lines). It had no production call site, its stated rationale (the PowerShell SDK not working in single-file deployments) no longer applies since `Build-Release.ps1` sets `PublishSingleFile=false`, and its `Invoke()` read stdout to completion before stderr and before `WaitForExit`, which deadlocks on a child that fills the stderr pipe - past the reach of the timeout below it. Its UTF-8 decoding coverage moved to the execution path that actually runs.

### Fixed
- `Test-DetectionArgumentAllowed` resolved its allowlist through the caller's scope, so in a parallel detection runspace the lookup failed closed and reported every application as not installed. The guard module now imports the allowlist module itself.
- `Test-ObjectAgainstSchema` rejected any JSON `null`: `$Object` was declared mandatory without `[AllowNull()]`, and a mandatory parameter refuses an explicit `$null` at binding time. Any configuration file containing a null value failed validation with a misleading binding error, and the function's own null-handling branch was unreachable.
- Locale tests never restored the current locale. `$originalLocale` was assigned in `BeforeEach`/`BeforeAll` and read in `AfterEach`/`AfterAll`, which is out of scope under Pester v5, so the tests leaked locale state.
- Removed 57 assignments to PowerShell automatic variables (`$profile`, `$Error`, `$host`, `$input`, `$args`) and 18 dead assignments across modules and tests, plus a reversed `$null` comparison and two unapproved cmdlet verbs.
- Nine controls that cannot derive an automation name from their content now declare a localized accessible name.
- `LoadSettingsAsync` no longer races: the cache check could not be held across the file read, so two concurrent first-callers each loaded the file and walked away with a different `AppSettings` instance, making a mutation through one invisible to the other.
- `Test-DirectDownloadChecksumGate` hashes the downloaded file once instead of twice, which is measurable on installer-sized payloads.
- PowerShell files containing non-ASCII characters now carry a UTF-8 BOM. Windows PowerShell 5.1 reads a BOM-less file as ANSI, so those characters rendered as mojibake there - and 5.1 is a real execution path: every module manifest declares `PowerShellVersion = '5.1'`, the GUI falls back to `powershell.exe`, and the analyzer targets `desktop-5.1` compatibility. `.editorconfig` now specifies `utf-8-bom` for PowerShell files and `PSUseBOMForUnicodeEncodedFile` is enforced so this cannot regress.

### Tests
- Added regression coverage for the plugin type allowlist, effective constrained-language enforcement, plugin content fingerprinting, GUI/PowerShell detection-guard parity, bounded request-body reads, per-key rate-limit wiring, settings normalization, configuration schema validation, JSON null handling, accessible names across all XAML, endpoint-handler type validation including every built-in handler, GUI timeouts tracking their configured source, and profile-migration resumption after an interrupted run.
- Coverage floors are ratcheted to just below measured coverage so a regression fails the build while normal fluctuation does not: .NET from 35% to 38% (measured 40.46%) and Pester from 40% to 48% (measured 51.76%).

## [2026062701] - 2026-06-27

### Security
- Command detection on the post-update verification path now enforces the shared executable allowlist, so an imported application catalog can no longer cause an arbitrary executable to be launched. The GUI detection probe and the application-management service now load the same fail-closed allowlist from a single source.
- Plugin execution and plugin-load validation now share the same sandbox AST checks, reject dynamic command invocation and dangerous aliases, and enforce an explicit allowlist for plugin template/logging commands.
- Direct-download catalog entries must now declare an Authenticode publisher, a SHA-256 checksum, or a documented validation exemption before the application database is accepted.

### Changed
- Updated GitHub Actions workflow dependencies to maintained Node 24-ready major versions and aligned the GUI coverage threshold with the current tested baseline.
- CI now parses PowerShell sources, runs the shared PSScriptAnalyzer wrapper, and executes detailed framework validation as first-class workflow steps.
- Application database validation now includes a direct-download security gate and requires notes for moving "latest" endpoints that cannot be version-pinned.
- Deployment progress and result strings are now sourced from resources instead of hardcoded literals, while still resolving in English so persisted logs stay parseable across UI cultures.
- The severity indicator's accessible name is now localized instead of using a hardcoded English string.
- Deduplicated the winget/chocolatey process-execution and version-probe plumbing in the application-management service.
- Decoupled view models from WPF presentation types: log entry colouring now uses a converter, and clipboard access goes through an injectable service.
- Registry and JSON application-detection services are now resolved through the dependency-injection container.
- Added persistent localized labels to the application editor inputs and inline spacing tokens for more consistent layout.
- Extracted parallel-install retry/retention/log-path values and special-case detection identifiers into named module configuration, and tidied the analyzer rule list and ignore rules.

### Fixed
- Failed winget version probes are now logged instead of silently returning an empty version.
- Reload failures triggered by application-database changes are now logged instead of being lost.
- Full exception stack traces are no longer written into the user-facing deployment log.
- Corrected the documented prerequisite SDK to .NET 10 in the contribution guide.
- System configuration tests now mock Explorer and DNS operations, avoiding local machine side effects during CI or maintainer test runs.

### Tests
- Added regression coverage for plugin sandbox command allowlisting, direct-download metadata invariants, publisher validation, and side-effect-safe system configuration behavior.

## [2026062301] - 2026-06-23

### Changed
- The Applications update scan button now uses clearer "Scan for Updates" wording.
- Required prerequisite apps now remain manageable in the Applications grid with an explicit prerequisite badge and uninstall warning.
- Rebranded public project identifiers, GUI assets, launchers, runtime data directory, and documentation from Win11Forge to WinForge.
- Added startup migration from `%LOCALAPPDATA%\Win11Forge` to `%LOCALAPPDATA%\WinForge`, with graceful fallback to legacy data when migration is blocked.
- Scheduled deployments now use WinForge task names while continuing to discover and manage legacy Win11Forge scheduled tasks.
- Persisted logs and operation result messages now resolve WinForge-owned localized strings in English and file logs are written as explicit UTF-8 without BOM.

### Fixed
- Dashboard update scans now run directly through the update coordinator, so the Scan for Updates button cannot get stuck waiting on a disposed Applications view handler.
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

### Profile editing and public documentation cleanup - June 2026

#### Added
- **Existing profiles can now be updated from the Applications grid.** After applying a profile, users can check or uncheck apps and save that selection back to the active profile. Inherited applications remain owned by their parent profile and are restored with a warning when a child profile is updated.
- **Added a public architecture overview.** `Docs/ARCHITECTURE.md` now summarizes the current runtime layout, GUI architecture, profile model, theming pipeline, API surface, and validation commands.

#### Changed
- **Updated user-facing documentation.** `README.md`, `Docs/USER_GUIDE.md`, `Docs/README.md`, and `Docs/GUI_VM_VISUAL_CHECKLIST.md` now document profile editing and avoid local machine paths.
- **Removed internal historical docs from the public documentation set.** Old closed-work archives and detailed internal ADR/audit notes were removed in favor of the concise public architecture page.

### Application catalog utilities refresh - June 2026

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

### Direct-download publisher gate activation - June 2026

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

### Button visual hierarchy restored - May 2026 follow-up

After visual inspection of the `v2026051001` release built from the unified
flat-text state, the lack of visual hierarchy on every action button proved
unworkable in practice - hero CTAs, dialog confirm buttons, and toolbar
actions all rendered as identical plain text labels. Reversed §2.6 of
the historical design notes for the full rationale. Those internal notes are
no longer part of the public documentation set.

#### Changed
- **Restored 70 `Appearance` attributes** across 20 view files to their
  pre-PR #94 values - `Primary` for constructive primary actions
  (Confirm/Save/OK/Apply/Install/Add/Start/Restart/etc.), `Secondary` for
  outlined actions (Cancel/Close/Browse/Test/Filter/Save Profile/etc.),
  `Danger` for App Catalog row Delete. Hero CTAs (Start Deployment, Fix
  Prerequisites) regain their accent fill. Inline `Background` / `Foreground`
  / `Padding` / `Height` overrides previously stripped by PR #94 are **not**
  restored - the implicit `ui:Button` `Style.Triggers` in App.xaml drive the
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

### UI flat-text consolidation + WPF-UI 4.3 disabled-state fix - May 2026

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
  (~`#4A4E66`, ~2.25:1 vs purple text - fails AA) to `SurfaceBrush`
  (`#1B1C25`, ~5.6:1–6:1 against the configured foregrounds - clears AA).

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
  `UserControl.Resources`, or `Application.Current.Resources` direct entry -
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
  overrides - preserved for forward compatibility even though no instance
  currently uses `Appearance="Danger|Success|Caution|Info"`.

### Dead code + i18n audit pass - May 2026

Two-axis cleanup pass closing 18 dead-code and localization findings across 7 self-contained commits. No behavior change at runtime; build, 566 GUI tests, Pester suite, PSScriptAnalyzer, FR diacritics lint, and version-consistency check all green.

#### Removed
- **PowerShell manifest cleanup (DC-013)** - `Core/Core.psd1` no longer declares `Test-AdminRights` or `Get-FrameworkVersion` in `FunctionsToExport` (these functions never existed in `Core.psm1`).
- **Resx keys cleanup (DC-002 to DC-012)** - 10 unused EN+FR resx pairs deleted: `AppEditor_Category`, `Apps_SelectWithUpdates`, `Dashboard_Updates_Available`, `Deploy_InheritedFrom`, `Deploy_Installing`, `Help_Shortcut_Actions`, `Help_Shortcut_Navigation`, `Recovery_NetworkTimeout`, `SourceEditor_TestPlaceholder`, `Toast_UninstallComplete`. Designer.cs trimmed where applicable. Guard test `DeadResourceCleanup_RemovesUnusedKeys2026May` added on `AccessibilityHardeningTests`.
- **IAccessibilityService scaffolding (DC-001 / DC-008)** - `IAccessibilityService`, `AccessibilityService`, `AnnouncementPriority`, DI registration, `MainWindow` field/initialize call, plus correlated resx keys `Accessibility_Progress`, `Accessibility_ProgressWithItem`, `Accessibility_ProgressComplete`, `Accessibility_DeploymentStarted`. Live-region screen-reader behavior is preserved via the existing XAML automation properties on `ScreenReaderLiveRegion` (PR #61/#62 baseline) and the 5 `LiveRegionAttributesTests` guards.

#### Refactored
- **Centralized GUI timeouts (ZH-002, ZH-003, ZH-004)** - three duplicated/literal timeout values now live in `GUI/Win11Forge.GUI/Configuration/TimeoutDefaults.cs` (`HttpClient` 15 s, `PackageOperation` 30 s, `CacheWarmingShutdown` 2 s). PowerShell install timeouts continue to live in `Config/timeouts-settings.json`.
- **Centralized GitHub project links (ZH-001)** - `GUI/Win11Forge.GUI/Configuration/ProjectLinks.cs` now provides `Repository`, `Issues`, `NewIssue`. `ErrorDialog` and `SettingsViewModel` route through this single source; the `ErrorDialog` issue-report fallback path is now consistent with the primary URL (previously dropped the `/new` suffix).

#### Documented
- **ReDoS regex timeout (ZH-005)** - `JsonApplicationDetectionService.RegexTimeout` (500 ms) is annotated as intentionally non-configurable to prevent attacker-controlled config from disabling the protection.

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

- **Test coverage** - `AccessibilityHardeningTests` adds three static-analysis guards: `RequiredA11yLocKeys_ArePresentInXaml` (theory across the 6 a11y-touched XAML files), `HighContrastMode_TextOnAccentBrushes_AreRemapped` (asserts the 3 `SwapIfExists` entries in `App.xaml.cs`), and `HighContrastTheme_ImplicitlyStylesWpfUiButton` (asserts the implicit `ui:Button` style based on `HighContrastButtonStyle` in `HighContrastTheme.xaml`). Total test count: 527 passed (`dotnet test -c Release GUI\Win11Forge.slnx --filter "FullyQualifiedName!~UIA"`).
- **Test coverage** - `AccessibilityHardeningTests` adds four static visual-hierarchy guards for P-15: AppsView header pattern, AppsView filter/profile reflow without `MinWidth="920"`, implicit `ReinforcedTabItemStyle`, and workflow/config navigation clustering. Total test count: 531 passed (`dotnet test -c Release GUI\Win11Forge.slnx --filter "FullyQualifiedName!~UIA"`).
- **Test coverage** - `AccessibilityHardeningTests` adds three post-smoke guards for DC-012/DC-013/DC-014: AppCatalog unavailable action visibility, AppsView install CTA deduplication, and theme-aware TabItem template states. Total test count: 534 passed (`dotnet test -c Release GUI\Win11Forge.slnx --filter "FullyQualifiedName!~UIA"`).
- **Test coverage** - PR #81 adds guards for filtered Apps selection helpers, import Replace/Merge/Cancel behavior, cancel confirmations, Settings no-toast auto-save, AppEditor source-specific a11y keys, AppCatalog HC contrast, dead resource cleanup, Settings icon de-duplication, and cross-view `CardPadding` token usage. Total test count: 554 passed (`dotnet test -c Release GUI\Win11Forge.slnx --filter "FullyQualifiedName!~UIA"`).

### Changed

- **Applications workflow** - `SelectAll`, `SelectNotInstalled`, `SelectFavorites`, and `SelectWithUpdates` now operate on the active filtered application list; `SelectNone` remains global to clear hidden selections deliberately. Closes WF-011 (PR #81).

### Fixed
- **Workflow safety (WF-012)** - Import Selection and Import Favorites now show Replace / Merge / Cancel previews when current state is non-empty, summarize matched/missing/final counts, and leave state untouched on cancel. Closes WF-012 (PR #81).
- **Workflow feedback (WF-013)** - Settings auto-save updates continue to set inline status, but no longer fire repetitive info toasts during every keystroke/toggle change. Closes WF-013 (PR #81).
- **Workflow safety (WF-014)** - Apps batch cancellation and Monitoring/Deployment cancellation now prompt before requesting cooperative cancellation, including completed/total context. Closes WF-014 (PR #81).
- **Visual hierarchy (DC-009)** - Settings keeps tab icons but removes redundant repeated icons from section headers inside cards. Closes DC-009 (PR #81).
- **Visual hierarchy (DC-010)** - Audited card borders across Apps, AppCatalog, Dashboard, Deployment, Logs, Prerequisites, and Settings now use `{StaticResource CardPadding}` instead of literal `16`/`20` card padding. Closes DC-010 (PR #81).
- **Accessibility follow-up** - ApplicationEditorDialog source action buttons now use source-specific automation names for Winget, Chocolatey, and Microsoft Store search/apply actions. Resolves the AppEditor P3 a11y follow-up (PR #81).
- **Accessibility follow-up** - High Contrast AppCatalog header contrast is guarded by WCAG AA tests and HC brush mappings for mapped surface brushes. Resolves A11Y-011 candidate (PR #81).
- **Resource cleanup** - Removed dead `AppCatalog_DeleteMultiple*` EN/FR resource keys and hardcoded English fallbacks in `LogsViewModel.ClearOldLogsAsync`. Resolves TODO cleanup follow-ups (PR #81).
- **Visual hierarchy (DC-012)** - AppCatalog now hides Undo/Redo and row actions when unavailable instead of rendering large disabled blocks. Closes DC-012 (PR #80).
- **Information architecture (DC-013)** - AppsView keeps `Install Selected` only in the selection action bar, removing the duplicate Profile-card CTA. Closes DC-013 (PR #80).
- **Visual hierarchy (DC-014)** - Shared WPF `TabItem` styling now uses a theme-aware template with explicit hover, focus, selected, and disabled states. Closes DC-014 (PR #80).
- **Visual hierarchy (DC-005)** - AppsView now mirrors the app-wide page header structure with an `Apps24` icon, `PageTitleTextStyle`, `PageSubtitleTextStyle`, and new localized `Apps_Subtitle` resource. EN/FR resource parity is 920/920. Closes DC-005 (PR #79).
- **Visual hierarchy (DC-006)** - AppsView profile and filter cards no longer force internal horizontal scrolling through `MinWidth="920"`; both surfaces now reflow in a two-row responsive layout. Closes DC-006 (PR #79).
- **Visual hierarchy (DC-007)** - Settings and shared WPF `TabItem` surfaces use `ReinforcedTabItemStyle` with a stronger selected underline and selected-state tint. Closes the DC-007 implementation scope; stronger post-smoke tab treatment is tracked separately as DC-014. Closes DC-007 (PR #79).
- **Information architecture (DC-008)** - Main navigation now separates workflow and configuration clusters with `NavigationViewItemSeparator`, moving App Catalog next to Settings while keeping Settings `Tag="4"` and App Catalog `Tag="5"` stable. Closes DC-008 (PR #79).
- **Accessibility (A11Y-007)** - Sweep `AutomationProperties.Name` on AppsView toolbar buttons (LogViewer Copy/Close, Summary Close, Save Profile, Reset Columns) and ApplicationEditorDialog source actions (Search + Apply Selection × 3 sources). Screen readers now announce explicit names instead of relying on inferred Content text. Closes A11Y-007 from the May 2026 UX review (PR #78).
- **Accessibility (A11Y-008)** - High-contrast mode now remaps `TextOnAccentFillColor*` brushes (Primary/Secondary/Disabled) to high-contrast foreground variants in `App.ApplyHighContrastMode`, and selectively applies `HighContrastButtonStyle` to `ui:Button` only (WPF-UI). Resolves the 1.07:1 white-on-cyan hover regression on accent-painted buttons in HC mode. Plain WPF `Button` controls remain untouched to preserve custom-styled buttons. Closes A11Y-008 (PR #78).
- **Accessibility (A11Y-009)** - Settings toggle switches (Reduce Motion, High Contrast) now expose their descriptive subtitle via `AutomationProperties.HelpText`, in addition to the existing `Name`. Closes A11Y-009 (PR #78).
- **Accessibility (A11Y-010)** - Explicit `AutomationProperties.Name` added to templated/dialog buttons that previously relied on inferred announcement: Prerequisites Check (StackPanel-wrapped Button with ProgressRing), ConfirmDialog Cancel/Confirm, ErrorDialog Help/Retry/OK. Closes A11Y-010 (PR #78).
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
- **Fixed**: SecureStorage DPAPI round-trip failures - `Get-DpapiEntropy` now persists entropy to disk on PS7 (replaced .NET Framework-only `File.Create(FileSecurity)` overload with cross-platform `FileStream` + `Set-SecureFileAcl`) and caches entropy in memory for session consistency
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
- **Fixed**: `SettingsViewModel.TrySaveSettings()` - was creating a new `AppSettings` object, losing fields from other views; now loads existing settings first and updates only managed fields
- **Fixed**: `AppSettingsService.SaveSettingsAsync()` - return type changed from `Task` to `Task<bool>` for consistency with sync method
- **Fixed**: Duplicate `ApplyHighContrastMode` call removed from `App.xaml.cs` startup
- **Fixed**: Duplicate `AutomationProperties.Name="ScheduledDeployment_DateTime"` on DatePicker and TextBox - now unique (`ScheduledDeployment_Date` / `ScheduledDeployment_Time`)
- **Fixed**: Hardcoded undo/redo tooltip strings replaced with localized computed properties (`UndoButtonTooltip` / `RedoButtonTooltip`)

#### Medium
- **Fixed**: Magic animation duration numbers replaced with named constants (`AnimationFastMs`, `AnimationNormalMs`, `AnimationSlowMs`)
- **Fixed**: `Contains("HighContrastTheme")` now uses `StringComparison.Ordinal`
- **Fixed**: `GetLocalizedString` fallback removed - replaced with strongly-typed `Resources.Resources.Settings_SaveFailed`
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

### Compatibility and performance

This historical release improved PowerShell 5.1 compatibility, System-Audit performance, and sequential deployment stability. Measurements below are the original release measurements, not a current benchmark.

- System-Audit 2.4.0 changed the default sample interval from 2 to 5 seconds and scheduled application, event, and network scans every 30, 60, and 120 seconds. The release recorded sample overhead changing from 3000 ms to about 750 ms, added SkipApplicationMonitoring, and displayed average/maximum sample costs.
- Added an eight-option TrustedInstaller launcher for administrative tools, SYSTEM execution, .msc support through mmc.exe, and dependency installation.
- Fixed StrictMode property access in InstallationEngine with nested conditions and PSObject.Properties checks for InstallationOptions, IgnoreExitCodeIfFileExists, and ValidExitCodes.
- Added automatic PowerShell 7 restart with parameter preservation for sequential and parallel modes.
- Fixed System-Audit process accounting, division by zero in HTML reports, Ctrl+C report generation, Quiet mode, CIM-session reuse, and HashSet-based comparisons.
- Fixed launcher quoting for paths containing spaces, custom-argument validation, ARGS assignment, and removed unused delayed-expansion code.
- Fixed GUI module-cache property access, call-operator detection, paths containing spaces, AppId overrides, and exit-code propagation.
- Fixed null environment reports, Skipped-property checks, skipped-app statistics, and summary colors in parallel mode.
- Improved application-database validation for numeric boolean Required values, priority/required overrides, priority zero, and registry writes.
- Fixed DirectDownload and portable ZIP deployment in parallel PowerShell 7 and sequential PowerShell 5.1, including Detection.Path handling.
- Fixed setup directory creation, documentation references, version consistency, and EnvironmentDetection module lookup through RepositoryRoot.
- Standardized UTF-8 BOM and formatting, console versions, application counts, GUI documentation, and update dates across more than 50 files.

The release recorded more than 100 commits, more than 15 critical fixes, and four System-Audit revisions since 2.3.0. See the repository history for the original validation reports.

## [2.3.0] - 2025-10-04

### Features

- Added StartMenuPinning using start2.bin for Windows 11 22H2+, supporting the Default profile and current user.
- Added StartMenuLayout for category-based organization and folders, integrated with pinning.
- Added StartupManager for enabling and disabling startup applications.
- Added per-application parallel logs under Logs/Parallel, with timestamps, detailed errors, and a consolidated main log.

### Fixes

- Added StoreApp detection with PackageName, vendor-prefix extraction, base-name fallback, and winget list to avoid Appx assembly conflicts under PowerShell 7.
- Fixed WhatsAppDesktop and multilingual MicrosoftCorporationII.QuickAssist detection.
- Corrected the Epic Games Launcher detection path to the Win32 executable under Program Files (x86).
- Removed invalid file paths for Proton Drive, Mail Bridge, and Pass, using the package-name fallback instead.
- Corrected CUE Splitter to Store package 9NBLGGH43MH5 and base-name detection.
- Made InstallArguments access safe under StrictMode using PSObject.Properties.
- Improved Test-ApplicationByName and isolated log output per runspace.

### Historical validation and migration

The release included Test-ProtonAppsDetection.ps1. Sequential testing used the Personnel profile: 64 processed applications, 16 installed, 41 already present, 4 skipped, and 3 Proton failures subsequently addressed. Parallel testing used five workers. Recorded inventory: 66 applications, four profiles, and ten modules, including the three new modules.

Seven application definitions were corrected. The original release described an expected installation-success improvement from approximately 95% to 99%; this is a historical estimate, not a current guarantee. No breaking change or profile migration was required from 2.2.0. The update procedure was to update the checkout, validate the database, run Base in TestMode, then deploy the chosen profile.

## [2.2.0] - 2025-10-03

### Architecture

- Added a centralized application database with 66 entries, multiple installation sources, tags, verification metadata, priorities, environment restrictions, and Registry/File/Command/StoreApp/WindowsFeature detection.
- Added ApplicationDatabase.psm1 for loading, caching, queries, statistics, and structural validation.
- Added the PowerShell GUI with eight navigation options, profile deployment, parallel/sequential selection, application and profile browsing, custom-profile creation, statistics, validation, and application-source search.
- Added the six-step ProfileCreator.html wizard, its applications-data.js catalog, existing-profile loading, filtering, system configuration, local-browser support, and JSON export.
- Added Search-ApplicationSources.ps1 for Winget, Chocolatey, Store, known direct-download patterns, and JSON templates.
- Added administrative GUI launchers and automatic UAC elevation.

### Profiles and migration

Profiles moved to compact AppId references resolved through the central database. ProfileManager added ID validation, cached inheritance, and detailed errors. Historical migration scripts Switch-ToProduction.ps1 and Test-NewProfiles.ps1 converted profiles and created backups under Archive/Profiles-v2.0-*/.

This was a breaking format change from 2.0/2.1: applications had to be centralized and profiles migrated or recreated. These statements describe the historical release; use current profile documentation for today's supported formats.

### Improvements and fixes

- Improved Read-Choice with a consistent zero/back/cancel option, contextual help, and input validation.
- Added Creality Slicer through the creality-print Chocolatey package with File detection.
- Added Cleanup-ObsoleteFiles.ps1 with preview and reporting; consolidated validation and web tools under Tools.
- Archived obsolete development reports and added project-structure and GUI documentation.
- Fixed missing back-navigation choices, PSObject.Properties access, source access, and application counts.
- Kept the GUI open after deployment by returning instead of exiting.
- Corrected module scope in parallel execution.

The release recorded 66 applications, four profiles, seven modules, six tools, and eight scripts. Profile sizes were Base 30, Office 35, Gaming 39, and Personnel 64. The migration sequence was backup/conversion, new-profile tests, database validation, then optional obsolete-file cleanup. Some named migration tools and documents have since been retired; consult the historical revision before attempting an old migration.

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

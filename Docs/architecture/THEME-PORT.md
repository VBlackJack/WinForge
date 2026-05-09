# ADR — Theme Port from Heimdall.Next

**Status:** COMPLETE — theme port merged in PR #47; post-merge cleanup and coverage closed by PR #91.
**Author:** Julien Bombled (with Cowork-Claude as architect, Codex as executor).
**Pre-requisite:** `REFACTOR-MVVM.md` Status COMPLETE. Verified.
**Source project:** `G:\_dev\SnapConnect\Heimdall.Next` (sister project, same author).
**Scope:** Port Heimdall's color palette system + `ThemeService` to Win11Forge. **Strategy 1 strict** (palette + service only, keep WPF-UI 4.2).
**Type:** Feature port (not an audit-finding closure). No `§12 Audit Follow-up` section needed.

> All `[TO DECIDE]` sections from the brainstorming session have been resolved. Decisions Q1..Q9 are marked `[DECIDED]`. Sections 11-12 retain historical checklist/execution context; closed execution prompts and phase reports are archived under `Docs/Archive/2026-05-closed-work/`.

---

## 1. Context

At the time of this ADR, Win11Forge was a .NET 8 WPF GUI consuming **WPF-UI 4.2** (`Wpf.Ui.Appearance.ApplicationThemeManager`, `Wpf.Ui.Controls`, `WindowBackdropType.Mica`) for Fluent design. The codebase has since moved to .NET 10; the theme architecture described here remains current. The pre-port theme system supported **two themes** (Light, Dark) via:

- `AppSettings.IsDarkTheme: bool` persisted in `%LOCALAPPDATA%\Win11Forge\settings.json`
- `App.xaml.cs:OnStartup` calling `ApplicationThemeManager.Apply(Dark|Light, Mica)` + `ApplyThemeResources(isDark)` which programmatically constructs ~25 `SolidColorBrush` instances and assigns them to `Application.Current.Resources`
- `Resources/FluentThemeBridge.xaml` defining ~50 app-specific brushes (Status*, Error*, Favorite*, etc.) with explicit `*Light*` variants for the light theme
- `Resources/Converters.cs:ThemeAdaptiveBrushConverter` which reads `ApplicationThemeManager.GetAppTheme()` at convert time and adapts return value

Heimdall.Next (sister project) has built a richer theme infrastructure based on the **Dracula palette family**: 7 thematic ResourceDictionaries (1 light + 6 dark) and a centralized `ThemeService` that owns the single code path replacing the active theme dictionary on `Application.Current`. Heimdall does **not** use WPF-UI; its theme RDs reference a custom `CommonControls.xaml` providing native WPF `ControlTemplate` overrides.

This ADR scopes a **Strategy 1** port: extract only the **palettes + service**, adapt the service to feed WPF-UI's `ApplicationAccentColorManager`, and preserve the WPF-UI 4.2 control library intact. The port is a feature, not a debt closure; no audit finding is associated.

---

## 2. Decision Q1 — Light theme handling [DECIDED]

**Decision:** Adopt **8 themes total — WPF-UI Light + 7 Dracula variants**.

The catalogue:

| Name | IsDark | Source | Notes |
|---|---|---|---|
| Light | false | WPF-UI native (`ApplicationTheme.Light`) | Existing, no Dracula RD merged. Triggers `ApplyThemeResources(false)`. |
| DraculaPro | true | `Themes/Dracula/DraculaProTheme.xaml` | Default for migration `IsDarkTheme=true`. |
| Alucard | false | `Themes/Dracula/AlucardTheme.xaml` | **Light Dracula variant** (`PrimaryColor=#FFFBEB`). Source-level discovery; not anticipated by `TODO.md`. |
| Blade | true | `Themes/Dracula/BladeTheme.xaml` | Pink/red dark accent. |
| Buffy | true | `Themes/Dracula/BuffyTheme.xaml` | Purple dark accent. |
| Lincoln | true | `Themes/Dracula/LincolnTheme.xaml` | Brown/sepia dark accent. |
| Morbius | true | `Themes/Dracula/MorbiusTheme.xaml` | Blue dark accent. |
| VanHelsing | true | `Themes/Dracula/VanHelsingTheme.xaml` | Green dark accent. |

**Rationale:**

- The current `ApplyLightThemeEnhancements` path is non-trivial (13 dedicated `*LightBrush` resources, careful WCAG AA contrast tuning). Dropping it would be a UX regression for current Light-theme users. **Behavior-preserving by default.**
- Alucard is a fully-formed light theme in Heimdall (confirmed via `<Color x:Key="PrimaryColor">#FFFBEB</Color>` and the comment `WCAG-safe text variants for semantic colors on light backgrounds`). It coexists with WPF-UI Light as a Dracula-flavored alternative, not a replacement.
- `Light` and `Alucard` are both `IsDark=false` but diverge in accent identity: `Light` uses the Microsoft Fluent system blue, `Alucard` uses Dracula purple `#815CD6`. The `SettingsView` ComboBox surfaces both; users pick.

**Migration rule** (one-time, persisted during first read post-port):

- `IsDarkTheme=false`, no `ThemeName` persisted → `ThemeName = "Light"`
- `IsDarkTheme=true`, no `ThemeName` persisted → `ThemeName = "DraculaPro"`
- After migration, `IsDarkTheme` becomes a **derived getter**: `IsDarkTheme => !(ThemeName is "Light" or "Alucard")`.

---

## 3. Decision Q2 — Scope of refactor [DECIDED]

**Decision:** **Strategy 1 strict**. Do **not** refactor `App.xaml.cs:RestoreDarkThemeDefaults` / `ApplyLightThemeEnhancements`. The ~25 programmatic `SolidColorBrush` swaps remain.

**Routing:**

- When the active theme is `Light` → existing path: `ApplicationThemeManager.Apply(Light, Mica)` + `ApplyThemeResources(false)`. No Dracula RD merged.
- When the active theme is one of the 7 Dracula variants → new path via `IThemeService.ApplyTheme`:
  1. Merge the corresponding Dracula RD into `Application.Current.Resources.MergedDictionaries` (replacing any existing Dracula RD).
  2. Call `ApplicationThemeManager.Apply(theme.IsDark ? Dark : Light, Mica)` to align WPF-UI Mica + DWM title bar with the palette luminance.
  3. Read `AccentBrush.Color` from the merged RD and call `Wpf.Ui.Appearance.ApplicationAccentColorManager.Apply(accentColor)` so WPF-UI's accent-aware controls follow the Dracula accent.
  4. Skip the legacy `ApplyThemeResources(_)` brush swap (would clash with Dracula palette keys).
  5. Bump `ThemeRevision++` and raise `ThemeChanged`.

**Trade-off accepted:** Two coexisting routing paths in `App.xaml.cs:OnStartup` (Light path vs Dracula path). Refactoring both into a single RD-driven model would be cleaner architecturally but expands scope to a separate PR (logged in §13 Risks → out of scope).

---

## 4. Decision Q3 — PR slicing [DECIDED]

**Decision:** **Single big-bang PR.** All 7 Dracula palettes + `IThemeService`/`ThemeService` + `SettingsView` ComboBox + `ThemeAdaptiveBrushConverter` migration are delivered together.

**Rationale (acknowledged risk):** Diff will be substantial (~900 lines of new XAML for palettes alone, ~400 lines of service code + tests, ~50 lines of XAML for the selector). Smoke test will be global (cycle through all 8 themes manually). This is the user's explicit choice; the alternative iterative slicing (DraculaPro-only PR1 then 6 others) was offered and rejected.

**Mitigation:** PR review focuses on the service contract first (mechanical palette files reviewed in bulk). Smoke test checklist provided in §11 DoD.

---

## 5. Decision Q4 — ThemeRevision adoption [DECIDED]

**Decision:** **Adopt in PR1.** Migrate `ThemeAdaptiveBrushConverter` to consume `IThemeService` state and return Dracula palette brushes when a Dracula theme is active.

**Rationale:** Win11Forge already has one converter (`Resources/Converters.cs:ThemeAdaptiveBrushConverter:540`) that reads `ApplicationThemeManager.GetAppTheme()` at convert time. Currently this works only because WPF-UI's `ApplicationThemeManager.Apply` triggers a re-render via `DynamicResource` lookups in the brushes the converter returns. Once the active theme is one of the 7 Dracula variants, the converter's logic (`isDark ? SecondaryHueMidBrush : PrimaryHueMidBrush`) is **incorrect** because both branches resolve to brushes defined in `FluentThemeBridge.xaml`, not in the active Dracula RD. The converter needs to either:

- Be migrated to consume Dracula keys when active (e.g. read `AccentBrush` from the merged RD)
- Or be deleted (questionable — used by `ThemeAdaptiveOutlinedButton` style in `App.xaml`)

**Adopted approach:** Convert `ThemeAdaptiveBrushConverter` to a stateless converter that:
1. Calls `App.GetService<IThemeService>().CurrentTheme` to know active theme name.
2. Reads `AccentBrush` from `Application.Current.Resources` if active theme is Dracula, else falls back to the existing `SecondaryHueMidBrush` / `PrimaryHueMidBrush` logic.
3. `ThemeService` updates the global `ThemeAdaptiveAccentBrush` resource when a Dracula palette is active, so existing `DynamicResource` consumers re-render without a bespoke subscription path in the converter.

YAGNI rejected: the converter exists, has a consumer (`App.xaml:ThemeAdaptiveOutlinedButton`), and would silently break across the Dracula variants if the active-palette branch is deferred.

---

## 6. Decision Q5 — Theme metadata model [DECIDED]

**Decision:** Introduce a `ThemeDescriptor` record + a static catalogue inside `ThemeService`.

```csharp
public sealed record ThemeDescriptor(
    string Name,
    bool IsDark,
    Uri? ResourceUri,    // null for "Light" (WPF-UI native, no RD merge)
    string DisplayKey);  // .resx key for localized display name
```

The catalogue is **hardcoded** as a `static readonly IReadOnlyList<ThemeDescriptor>` (8 entries). No JSON config — consistent with `dev-standards` "Zero hardcoding" applies to user-facing strings (`DisplayKey` resolves via `Resources.resx`) and paths (`ResourceUri` is a relative URI within the assembly). Theme names are constants, not magic strings — exposed via `public static class ThemeNames { public const string Light = "Light"; ... }`.

**Why a record vs class:** Immutable, value-based equality, terse syntax — matches Heimdall's `Dictionary<string, Uri>` pattern but adds the IsDark + DisplayKey metadata.

---

## 7. Decision Q6 — Resource key strategy [DECIDED]

**Decision:** Merge Heimdall keys **as-is, no prefix**. Audit confirmed no collision with current Win11Forge brush keys.

**Audit done:**

- Heimdall generic keys (`BackgroundBrush`, `SurfaceBrush`, `AccentBrush`, `TextPrimaryBrush`, `BorderBrush`, `SuccessBrush`, `WarningBrush`, `ErrorBrush`, `InfoBrush`, `OverlayBackground`, `BadgeTextBrush`, `FocusIndicatorBrush`, `TextOnAccentBrush`, etc.): **no current Win11Forge consumer with the same key**. Verified via `grep -rn` over `GUI/Win11Forge.GUI/**/*.xaml`.
- Heimdall-specific keys NOT ported (out of scope, §10): `ProtocolRdpBrush`, `ProtocolSshBrush`, `RdpBadgeBrush`, `JwtHeaderBrush`, `HackerSim*Brush`, `Tool*Brush` — these belong to Heimdall's domain (RDP/SSH connection manager).
- Win11Forge-specific keys in `FluentThemeBridge.xaml` (`StatusInstalledBrush`, `FavoriteActiveBrush`, `RequiredBrush`, `ManualInstallBadgeBrush`, etc.): **untouched**. They remain part of the global ResourceDictionary and are independent of the Dracula palette swap. Their light/dark variants continue to be swapped by the existing `App.xaml.cs:ApplyThemeResources` path **only when the active theme is `Light`**. When a Dracula theme is active, these Win11Forge brushes keep their dark-theme defaults regardless of which Dracula variant is selected (acceptable: `StatusInstalledBrush=#4CAF50` reads fine on every Dracula background; verified by spot-checking each palette's `BackgroundBrush` luminance).

**Bridge to WPF-UI:** `ThemeService.ApplyTheme` ends with `ApplicationAccentColorManager.Apply(accentColor)` so WPF-UI's `SystemAccentColorPrimaryBrush` family realigns. This propagates the Dracula accent to all WPF-UI controls without touching Win11Forge's custom brushes.

---

## 8. Decision Q7 — IThemeService contract [DECIDED]

**Decision:** Introduce an interface (`IThemeService`) and a sealed implementation (`ThemeService`). Heimdall exposes only the sealed class; Win11Forge follows its own DI conventions (interface-first, consistent with `IAppSettingsService`, `IDialogService`, `INavigationService`).

**Contract:**

```csharp
public interface IThemeService
{
    /// <summary>Canonical name of the currently applied theme (e.g. "DraculaPro").</summary>
    string CurrentTheme { get; }

    /// <summary>Monotonically increasing counter bumped on each successful theme swap.
    /// Theme-aware bindings/converters use this as a re-evaluation trigger.</summary>
    int ThemeRevision { get; }

    /// <summary>Catalogue of all themes the service can apply (8 entries).</summary>
    IReadOnlyList<ThemeDescriptor> AvailableThemes { get; }

    /// <summary>Raised on the UI thread after a successful theme swap.
    /// Carries the canonical name of the newly active theme.</summary>
    event Action<string>? ThemeChanged;

    /// <summary>Applies the named theme. Unknown names fall back to the default theme.
    /// Idempotent: a call for the already-active theme is a no-op.
    /// Legacy "Dark"/"Light" string values from older Heimdall builds (non-applicable here)
    /// would be migrated; Win11Forge's bool-based legacy migration is handled in
    /// AppSettingsService.LoadSettings, not in this method.</summary>
    void ApplyTheme(string? themeName);
}
```

**Notes vs Heimdall:**

- Heimdall's legacy `"Dark"/"Light"` string migration is in `ThemeService.Resolve`. Win11Forge's legacy is `IsDarkTheme: bool`, migrated in `AppSettingsService.LoadSettings` (one-shot, persisted). Cleaner separation: `ThemeService` only knows theme names, not bool-vs-string history.
- Heimdall's ctor depends on `IConfigManager.MergeSettingAsync`. Win11Forge's `ThemeService` depends on `IAppSettingsService` only to persist canonical fallback values when an invalid theme name is normalized to the default. Bool-based legacy migration remains owned by `AppSettingsService`.
- Heimdall calls `WindowThemeHelper.ApplyCurrentTheme(window)` per Window for DWM title bar. **Not needed** in Win11Forge: WPF-UI's `ApplicationThemeManager.Apply(theme, WindowBackdropType.Mica)` handles `DWMWA_USE_IMMERSIVE_DARK_MODE` and `DWMWA_WINDOW_CORNER_PREFERENCE` internally.

**DI registration:** `services.AddSingleton<IThemeService, ThemeService>()` in `ServiceCollectionExtensions.AddWin11ForgeServices()`.

---

## 9. Decision Q8 — AppSettings schema migration [DECIDED]

**Decision:** Add `string ThemeName` property; keep `IsDarkTheme` as a **derived getter only** (eliminate setter surface to avoid drift).

**New `AppSettings` shape:**

```csharp
public class AppSettings
{
    public string ThemeName { get; set; } = ThemeNames.Light;

    [Obsolete("Use ThemeName. Kept for one-shot migration compatibility.", error: false)]
    public bool IsDarkTheme  // derived, no setter
    {
        get => !(ThemeName is ThemeNames.Light or ThemeNames.Alucard);
    }
    // ... other properties unchanged
}
```

**One-shot migration** (in `AppSettingsService.LoadSettings`, only on first read post-port):

```csharp
if (string.IsNullOrEmpty(settings.ThemeName))
{
    // Legacy bool-based settings detected
    var legacyIsDark = ReadLegacyIsDarkTheme(rawJson);
    settings.ThemeName = legacyIsDark ? ThemeNames.DraculaPro : ThemeNames.Light;
    PersistMigratedSettings(settings);
}
```

**Backward-compat impact:**

- All current `IsDarkTheme` setters in code become **compile errors** post-port. Files needing migration:
  - `ViewModels/SettingsViewModel.cs:394` (read), `:410` (consumer), `:461` (`OnIsDarkThemeChanged` partial), `:604` (write back to settings)
  - `App.xaml.cs:186, 190-193, 215-219` (theme application based on `settings.IsDarkTheme`)
  - `Services/AppSettingsService.cs:236-238` (theme application on settings reload)
- Replacement: VMs and callers go through `IThemeService.ApplyTheme(settings.ThemeName)` and read/write `settings.ThemeName` directly.
- `SettingsView.xaml:61` `<ui:ToggleSwitch IsChecked="{Binding IsDarkTheme}"/>` is replaced by a `<ComboBox>` bound to `IThemeService.AvailableThemes` with `SelectedItem` ↔ `SettingsViewModel.SelectedTheme`.

---

## 10. Decision Q9 — Out of scope (explicitly) [DECIDED]

The following are **not** part of this PR and remain residual debt or future scope:

| Item | Why excluded | Tracked where |
|---|---|---|
| `Themes/CommonControls.xaml` (Heimdall, 2 173 lines) | Frontal collision with WPF-UI 4.2 ControlTemplates. | Strategy 2 in `TODO.md` — not retained. |
| `Themes/IconGeometries.xaml` (Heimdall, 290 lines) | WPF-UI provides Fluent symbols (`SymbolRegular24`/`SymbolFilled24`). | Future scope only if specific icon needed. |
| `Themes/DialogCommonStyles.xaml` (Heimdall, 88 lines) | No equivalent need in Win11Forge — dialogs use WPF-UI controls. | — |
| `Themes/DraculaSyntaxPalette.cs` (Heimdall, ~150 lines) | No syntax-highlighting in Win11Forge (no log/code viewer requiring tokenization). | Future scope if `LogsView` adds JSON/script highlighting. |
| `Services/WindowThemeHelper.cs` (Heimdall, DWM dark mode) | WPF-UI's `ApplicationThemeManager.Apply(theme, Mica)` handles DWM. | — |
| Heimdall-specific brushes (Protocol*, Jwt*, HackerSim*, RdpBadge, Tool*) | Heimdall app domain (RDP/SSH manager), not Win11Forge (app installer). | Strip from each ported palette during PR1. |
| Refactor of `App.xaml.cs:RestoreDarkThemeDefaults` / `ApplyLightThemeEnhancements` to RD-driven | Strategy 1 strict (Decision Q2). | Future ADR if/when the legacy programmatic swap becomes a maintenance burden. |
| GUI line-coverage gate raise (still ~18 % per `REFACTOR-MVVM.md §8.6`) | Not theme-related. | Tracked in MVVM ADR §8.6. |

---

## 11. Definition of Done

### 11.1 Structural criteria

- [ ] `Services/IThemeService.cs` and `Services/ThemeService.cs` created (Apache 2.0 header, English-only).
- [ ] `Models/ThemeDescriptor.cs` record created.
- [ ] `Resources/ThemeNames.cs` const class created (no string literals for theme names elsewhere).
- [ ] 7 RDs in `Themes/Dracula/` (DraculaProTheme.xaml, AlucardTheme.xaml, BladeTheme.xaml, BuffyTheme.xaml, LincolnTheme.xaml, MorbiusTheme.xaml, VanHelsingTheme.xaml).
  - Each RD strips Heimdall-specific keys (Protocol*, Jwt*, HackerSim*, RdpBadge, Tool*, FileXxx, Scrollbar) — keep only generic Dracula keys (Background, Surface, Card, Accent*, Text*, Border, Highlight, Success/Warning/Error/Info, Badge, FocusIndicator, Overlay).
  - Each RD removes the `<ResourceDictionary.MergedDictionaries><ResourceDictionary Source="CommonControls.xaml"/></ResourceDictionary.MergedDictionaries>` reference.

### 11.2 Service criteria

- [ ] `ThemeService.ApplyTheme("DraculaPro")` followed by `ApplyTheme("DraculaPro")` is a no-op (idempotent).
- [ ] After every successful swap, `ThemeRevision` strictly increases by 1 and `ThemeChanged` event fires once with the canonical name.
- [ ] `ApplyTheme(null)` and `ApplyTheme("UnknownTheme")` fall back to default (`ThemeNames.Light` for first-launch, otherwise persisted current).
- [ ] Bridge to WPF-UI: `ApplicationAccentColorManager.Apply(accentColor)` called after every successful swap.
- [ ] DI: `services.AddSingleton<IThemeService, ThemeService>()` in `ServiceCollectionExtensions`.

### 11.3 Migration criteria

- [ ] `AppSettings.ThemeName` defaults to `"Light"` for fresh installs.
- [ ] `AppSettings.IsDarkTheme` is a derived getter only (compile error if anyone tries to assign).
- [ ] On first load post-port from a settings.json containing only `"isDarkTheme": true`, `ThemeName` is set to `"DraculaPro"` and persisted immediately. Equivalent for `false → "Light"`.
- [ ] xUnit test `AppSettingsService_LegacyMigration_IsDarkThemeTrue_MapsToDraculaPro` passes.
- [ ] xUnit test `AppSettingsService_LegacyMigration_IsDarkThemeFalse_MapsToLight` passes.

### 11.4 Selector UI criteria

- [ ] `SettingsView.xaml` ToggleSwitch `IsChecked="{Binding IsDarkTheme}"` (line 61) replaced by `<ComboBox ItemsSource="{Binding AvailableThemes}" SelectedItem="{Binding SelectedTheme, Mode=TwoWay}"/>`.
- [ ] `SettingsViewModel` exposes `IReadOnlyList<ThemeDescriptor> AvailableThemes` and `[ObservableProperty] ThemeDescriptor? _selectedTheme`.
- [ ] Selecting a theme in the ComboBox triggers `IThemeService.ApplyTheme(selected.Name)` immediately and persists `ThemeName` via `IAppSettingsService.SaveSettingsAsync`.
- [ ] Each ComboBox item displays the localized `DisplayKey` from `Resources.resx` (8 new keys: `Settings_ThemeName_Light`, `..._DraculaPro`, `..._Alucard`, `..._Blade`, `..._Buffy`, `..._Lincoln`, `..._Morbius`, `..._VanHelsing`).
- [ ] The 8 new resx keys exist in `Resources.resx` AND in `Resources.fr.resx` (existing locales kept in sync).

### 11.5 Converter migration criteria

- [ ] `Resources/Converters.cs:ThemeAdaptiveBrushConverter` migrated: reads `IThemeService.CurrentTheme` defensively and relies on `ThemeAdaptiveAccentBrush` dynamic-resource updates for existing styled buttons.
- [ ] When active theme is one of the 7 Dracula variants, the converter returns `AccentBrush` from the active palette instead of `SecondaryHueMidBrush`/`PrimaryHueMidBrush` of `FluentThemeBridge.xaml`.
- [ ] xUnit test `ThemeAdaptiveBrushConverter_DraculaActive_ReturnsAccentBrush` passes.

### 11.6 Quality criteria (CI/tests)

- [ ] `dotnet restore GUI\Win11Forge.slnx --locked-mode` succeeds.
- [ ] `dotnet build GUI\Win11Forge.slnx --configuration Release --no-restore` succeeds with **0 warnings** (`TreatWarningsAsErrors`).
- [ ] `dotnet test GUI\Win11Forge.GUI.Tests\Win11Forge.GUI.Tests.csproj --no-build` reports **≥ 391 passing** (baseline 384 + at least 7 new tests: ThemeService idempotence, ThemeRevision monotonicity, ApplyUnknown fallback, 2 migration tests, ThemeChanged event firing, converter migration).
- [ ] **0 failed**, **0 skipped without justification**.
- [ ] `Nullable` enabled, no `#nullable disable` introduced.

### 11.7 Smoke test (runtime, manual — required before merge)

- [ ] Launch app from clean settings (delete `%LOCALAPPDATA%\Win11Forge\settings.json`). App opens with `Light` theme by default. No exception in `Win11Forge_startup.log`.
- [ ] Open `Settings`, switch to each of the 8 themes in turn. For each:
  - DataGrid rows render with appropriate background (`StatusInstalledBrush`, `RowFailedBackground`, etc.).
  - Buttons, TextBoxes, ToggleSwitches keep WPF-UI Fluent styling.
  - DWM title bar updates: dark for IsDark themes, light for Light/Alucard.
  - Fluent accent color (e.g. selected DataGrid row) follows the Dracula `AccentBrush`.
  - No black-on-black or white-on-white text.
  - `ThemeAdaptiveOutlinedButton` (used for theme-adaptive outlined buttons) renders with the active accent.
- [ ] Restart app, verify the last-selected Dracula theme is restored (persistence works).
- [ ] Migration scenario: edit `settings.json` to contain only `{"isDarkTheme": true, "languageCode": "en"}`. Launch app. Verify `DraculaPro` is applied AND that `settings.json` is rewritten to include `"themeName": "DraculaPro"` shortly after launch.
- [ ] Migration scenario inverse: edit `settings.json` to `{"isDarkTheme": false, ...}`. Launch app. Verify `Light` applied + `themeName: "Light"` persisted.
- [ ] High-contrast mode toggled while a Dracula theme is active: high-contrast brushes still override correctly (existing `App.xaml.cs:ApplyHighContrastMode` still functional).

#### Partial automation — opt-in UIA harness (informational, does not replace §11.7 manual smoke test)

A separate test project `GUI/Win11Forge.GUI.UITests/` ships with this PR (delivered in a dedicated commit, scope-isolated from the theme port) and provides partial smoke-test coverage via UI Automation. It is **opt-in** and must be activated explicitly:

- Set `WIN11FORGE_RUN_UIA=1` before running `dotnet test`. Without the flag, both UIA tests are skipped and do not affect normal CI runs.
- The harness launches `Win11Forge.GUI.dll` via `dotnet` (not the `.exe`) to bypass the `requireAdministrator` UAC prompt on the production binary. The product manifest is unchanged.
- Stable `AutomationId` values added across the navigation tree and key surfaces: `NavDashboard`, `NavPrerequisites`, `NavApplications`, `NavDeployment`, `NavSettings`, `NavAppCatalog`, `PageDashboard`, `PageApplications`, `PageAppCatalog`, `PageSettings`, `ThemePicker`. (`RootNavigation` was added on the WPF-UI `NavigationView` but is not used by the harness — WPF-UI does not expose it reliably through UIA; the harness traverses by leaf `AutomationId` instead.)
- Test 1 — `CanNavigateCoreScreensAndCaptureScreenshots`: navigates Dashboard → Applications → AppCatalog → Settings via `AutomationId`, waits for a stable per-page marker, and writes `01-dashboard.png` / `02-applications.png` / `03-app-catalog.png` / `04-settings.png` to `TestResults/ui-screenshots/` (gitignored). Asserts each PNG is non-empty.
- Test 2 — `SettingsThemePicker_IsDiscoverable`: navigates to Settings, locates the `ThemePicker` ComboBox via UIA, and writes `settings-theme-picker.png`. Asserts discoverability only — does not exercise the 8-theme cycling.

**What this covers from §11.7:** clean-state startup (scenario 1), basic navigation surfaces (informational, not in the original §11.7 list), Settings ThemePicker presence (subset of scenario 2). The screenshots provide a visual artefact suitable to attach to the PR description as evidence of build sanity.

**What this does NOT cover (must still be exercised manually):** the 8-theme cycling and visual-coherence checks of scenario 2 (DataGrid contrast, DWM title bar luminance, accent propagation, no black-on-black/white-on-white), persistence across restart (scenario 3), the two legacy-bool migration scenarios (4 and 5), and the HighContrast × Dracula coexistence (scenario 6, which is the highest residual risk per §13 row 6). These remain **mandatory manual gates before merge**.

The opt-in UIA harness is also distinct from the xUnit coverage gaps that were later closed by PR #91. UIA does not exercise the migration parser or the converter fallback paths; those are covered by focused xUnit tests.

### 11.8 Behavior change explicitly accepted

- [x] Users on `IsDarkTheme=true` (likely the majority) who had been seeing **WPF-UI Dark** now see **DraculaPro** after migration. This is intentional (decision Q1) and documented in `CHANGELOG.md`.
- [x] No automatic bool-revert path: once `ThemeName` is written, `IsDarkTheme` is derived. Users who want strict WPF-UI Dark have no theme to select (closest = DraculaPro). Acknowledged risk; the 7 Dracula dark variants offer richer choice in exchange.

### 11.9 Delivery criteria

- [x] Apache 2.0 header on every new file (author: Julien Bombled, year 2026).
- [x] English-only code, comments, identifiers, XML doc.
- [x] Zero hardcoding: theme names → `ThemeNames` consts, brush keys referenced in code-behind → const strings, display labels → `Resources.resx`.
- [x] No new `using System.Windows.Media.Brushes.Black` or hex-coded brushes in code-behind for any theme decision (must come from `Application.Current.Resources`).
- [x] PR description references this ADR; `TODO.md` "UI/Theme port from Heimdall.Next" entry is closed.

---

## 12. Implementation Plan — Historical Execution Context

Single-PR scope. Constraints inherited from `REFACTOR-MVVM.md §9` apply identically (Apache 2.0, English-only, zero hardcoding, `Nullable` + `WaE`, immutable audit reports, no out-of-scope file modifications).

### Step 1 — `feature/theme-port-heimdall` (single PR, closes Theme Port)

**Branch:** `feature/theme-port-heimdall`
**Depends on:** `main` (post-MVVM-refactor, currently `79dadd4`).

**Goal:** Land the entire Strategy 1 port — 7 Dracula palettes + IThemeService/ThemeService + AppSettings schema migration + SettingsView ComboBox + ThemeAdaptiveBrushConverter migration — in one PR. Smoke test runtime cycle through all 8 themes before merge.

**Files affected:**

*New:*

- `GUI/Win11Forge.GUI/Themes/Dracula/DraculaProTheme.xaml` — palette port (only generic Dracula keys, no CommonControls reference).
- `GUI/Win11Forge.GUI/Themes/Dracula/AlucardTheme.xaml` — same shape, light variant.
- `GUI/Win11Forge.GUI/Themes/Dracula/BladeTheme.xaml`
- `GUI/Win11Forge.GUI/Themes/Dracula/BuffyTheme.xaml`
- `GUI/Win11Forge.GUI/Themes/Dracula/LincolnTheme.xaml`
- `GUI/Win11Forge.GUI/Themes/Dracula/MorbiusTheme.xaml`
- `GUI/Win11Forge.GUI/Themes/Dracula/VanHelsingTheme.xaml`
- `GUI/Win11Forge.GUI/Models/ThemeDescriptor.cs` — record `(string Name, bool IsDark, Uri? ResourceUri, string DisplayKey)`.
- `GUI/Win11Forge.GUI/Resources/ThemeNames.cs` — `public static class ThemeNames { public const string Light = "Light"; ... }`.
- `GUI/Win11Forge.GUI/Services/IThemeService.cs` — interface per §8 contract.
- `GUI/Win11Forge.GUI/Services/ThemeService.cs` — sealed implementation.
- `GUI/Win11Forge.GUI.Tests/ThemeServiceTests.cs` — ≥ 5 tests (idempotence, monotonic revision, unknown fallback, ThemeChanged event, ApplicationAccentColorManager bridge invocation).
- `GUI/Win11Forge.GUI.Tests/AppSettingsServiceMigrationTests.cs` — ≥ 2 tests (true→DraculaPro, false→Light).
- `GUI/Win11Forge.GUI.Tests/ThemeAdaptiveBrushConverterTests.cs` — ≥ 1 test (DraculaActive → AccentBrush).

*Modified:*

- `GUI/Win11Forge.GUI/Services/AppSettingsService.cs` — add `string ThemeName`, derive `IsDarkTheme`, add migration on `LoadSettings`/`LoadSettingsAsync`.
- `GUI/Win11Forge.GUI/Services/ServiceCollectionExtensions.cs` — register `IThemeService`.
- `GUI/Win11Forge.GUI/App.xaml.cs` — replace direct `ApplicationThemeManager.Apply(...)` calls in `OnStartup` with `IThemeService.ApplyTheme(settings.ThemeName)`. Keep `ApplyThemeResources` only on `Light` path (when descriptor.ResourceUri is null).
- `GUI/Win11Forge.GUI/Resources/Converters.cs` — migrate `ThemeAdaptiveBrushConverter` to `IThemeService` consumer.
- `GUI/Win11Forge.GUI/ViewModels/SettingsViewModel.cs` — replace `IsDarkTheme` ObservableProperty with `SelectedTheme: ThemeDescriptor?`, expose `AvailableThemes`, react to `OnSelectedThemeChanged` by calling `IThemeService.ApplyTheme` + `IAppSettingsService.SaveSettingsAsync`.
- `GUI/Win11Forge.GUI/Views/SettingsView.xaml` — replace ToggleSwitch (line 61) with ComboBox bound to `AvailableThemes`/`SelectedTheme`.
- `GUI/Win11Forge.GUI/Resources/Resources.resx` — add 8 new keys `Settings_ThemeName_*`.
- `GUI/Win11Forge.GUI/Resources/Resources.fr.resx` — same 8 keys, French translations.
- `TODO.md` — closed by PR #47; post-merge cleanup/coverage closed by PR #91.

**Constraints:**

- Apache 2.0 header on every new `.cs` and `.xaml` file (Julien Bombled, 2026).
- English-only code, comments, identifiers (French only in `Resources.fr.resx` strings).
- All 7 ported palettes strip the following Heimdall-specific keys: `ProtocolRdpBrush`, `ProtocolSshBrush`, `ProtocolSftpBrush`, `ProtocolVncBrush`, `ProtocolTelnetBrush`, `ProtocolFtpBrush`, `ProtocolCitrixBrush`, `ProtocolLocalBrush`, `RdpBadgeBrush`, `SshBadgeBrush`, `SftpBadgeBrush`, `VncBadgeBrush`, `FtpBadgeBrush`, `CitrixBadgeBrush`, `TelnetBadgeBrush`, `LocalBadgeBrush`, `ToolBadgeColor`, `ToolBadgeBrush`, `ToolNetworkBrush`, `ToolSecurityBrush`, `ToolEncodingBrush`, `ToolSystemBrush`, `ToolExternalBrush`, `JwtHeaderColor`, `JwtHeaderBrush`, `JwtPayloadColor`, `JwtPayloadBrush`, `JwtSignatureColor`, `JwtSignatureBrush`, `FileScriptBrush`, `FileConfigBrush`, `FileDocumentBrush`, `FileArchiveBrush`, `FileExecutableBrush`, `FileImageBrush`, `ScrollBarThumbBrush`, `ScrollBarTrackBrush`, `HackerSimBackgroundBrush`, `HackerSimSurfaceBrush`, `HackerSimToolbarBrush`, `HackerSimBorderBrush`, `HackerSimInputBorderBrush`, `HackerSimTextPrimaryBrush`, `HackerSimTextSecondaryBrush`, `HackerSimTextMutedBrush`, `HackerSimButtonForegroundBrush`, `HackerSimButtonBackgroundBrush`, `HackerSimAccentBrush`, `HackerSimHighlightBrush`, `HackerSimGlowBrush`, `HackerSimGlowStrongBrush`, `HackerSimOverlayBrush`, `BroadcastActiveBrush`, `DragDropOverlayBackground`. Keep only generic palette keys.
- All 7 ported palettes remove the `<ResourceDictionary.MergedDictionaries><ResourceDictionary Source="CommonControls.xaml"/></ResourceDictionary.MergedDictionaries>` block from the Heimdall source (CommonControls is out of scope per §10).
- `IsDarkTheme` setter MUST be removed from `AppSettings` (read-only derived getter only). Any compile error from existing code referring to `IsDarkTheme = ...` is the migration boundary — fix at call site.
- `ThemeService.ApplyTheme` is the **only** entry point that mutates `Application.Current.Resources.MergedDictionaries` for a Dracula RD. App.xaml.cs delegates to it.
- Do not modify files listed in §10 (CommonControls.xaml stays absent, IconGeometries.xaml stays absent, etc.).
- Do not modify `Win11Forge-Audit-Report.md` or `REFACTOR-MVVM.md`.

**Acceptance criteria:** § 11.1 through 11.9 above.

**Notes for executor (Codex):**

- Start by porting `DraculaProTheme.xaml` first (it's the migration target). Test that one palette merges cleanly into `Application.Current.Resources` before bulk-porting the other 6 — they share the exact same key shape.
- The `ApplicationAccentColorManager.Apply(accentColor)` bridge takes a `System.Windows.Media.Color`. Read it from the merged dictionary via `Application.Current.Resources["AccentColor"]` (a `Color`, not a `SolidColorBrush`).
- For `ThemeChanged` event firing on UI thread: the service runs on UI thread (called from `App.xaml.cs:OnStartup` and `SettingsViewModel`), so direct `ThemeChanged?.Invoke(canonical)` is safe — no `Dispatcher.Invoke` needed. Document this constraint in the interface XML doc.
- `_themeRevision` increment must be atomic with the dictionary swap. The whole `ApplyTheme` method runs on UI thread, so single-threaded access is guaranteed; `Interlocked.Increment` is **not** needed (and would add noise). Note in code comment.
- SettingsView ComboBox `ItemTemplate`: bind `DisplayKey` to a `LocConverter` (existing `loc:Loc` markup pattern visible at `SettingsView.xaml:62`). Keep the localization story consistent.

### Execution amendments captured 2026-05-06

The implementation kept the ADR's behavioral decisions but tightened a few mechanics during execution:

- Legacy settings migration is persisted immediately during `LoadSettings` (and awaited during `LoadSettingsAsync`) rather than fire-and-forget. This removes the post-load race while keeping migration idempotent.
- `ThemeService.PersistCanonicalThemeIfNeeded` persists normalized fallback values when an invalid theme name is resolved to `ThemeNames.Default`. Bool-based legacy migration remains in `AppSettingsService`.
- `ThemeService` updates `ThemeAdaptiveAccentBrush` when a Dracula palette is active, preserving existing `ThemeAdaptiveOutlinedButton` dynamic-resource styling.
- `_hasAppliedTheme` distinguishes the first theme application from later idempotent calls, so the initial `Light` apply still runs even though `CurrentTheme` defaults to `Light`.
- `ResxKeyConverter` resolves `ThemeDescriptor.DisplayKey` in `SettingsView.xaml` while keeping `ThemeDescriptor` immutable.

---

## 13. Risks and Mitigations

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Brush key collision discovered at runtime (audit missed something) | Low | Medium | Smoke test § 11.7 cycles all 8 themes; visual regressions caught manually. Fast revert via theme switch back to Light. |
| `ApplicationAccentColorManager.Apply` API surface differs in WPF-UI 4.2 from documented (the version Heimdall targets is older) | Low | Low | Verified package version: `Wpf.Ui` 4.2.x in `Win11Forge.GUI.csproj`. API stable since 3.x. If divergence: fall back to setting `SystemAccentColorPrimaryBrush` directly. |
| `ApplyThemeResources` dual-path complexity | Medium | Low | Trade-off accepted (Decision Q2 strict). Future ADR can collapse. |
| Migration race during legacy bool conversion | Low | Negligible | Migration is now persisted immediately during load and remains idempotent if interrupted. |
| User unhappy with auto-migration `IsDarkTheme=true → DraculaPro` | Medium | Low (UX) | Documented in PR description + CHANGELOG. The 7 Dracula dark variants cover most aesthetic preferences. No "WPF-UI Dark" option preserved. **Acknowledged behavior change** (§ 11.8). |
| HighContrast + Dracula interaction breaks (HC overlay swap targets WPF-UI keys, not Dracula keys) | Medium | Medium | Smoke test § 11.7 covers HC + Dracula combo. If broken: scope reduction — HC only available with Light theme (one-line guard in `ApplyHighContrastMode`). Track as follow-up if discovered. |
| Big-bang PR diff size makes review harder | High | Low | Mitigated by clear ADR + Codex prompt structure. Reviewer can take palettes en bloc (mechanical), service + tests separately (logic). |
| `ThemeAdaptiveBrushConverter` migration introduces null-ref if `IThemeService` not yet initialized at first XAML load | Low | Medium | App.xaml.cs:OnStartup configures DI before any window shows; converter accesses `IThemeService` only post-startup. Fallback to `Light` defaults in converter if service not available (defensive). |
| `ApplyTheme(null)` called from a non-startup path persists `"Light"` silently via `PersistCanonicalThemeIfNeeded` | Very low | Low | Currently dead code: all production callsites pass a non-null name (`App.xaml.cs:OnStartup` reads `settings.ThemeName` which has a non-null default; `SettingsViewModel.OnSelectedThemeChanged` early-outs on `value is null`). Documented as known theoretical race per archived Phase C audit (`Docs/Archive/2026-05-closed-work/architecture/THEME-PORT-PHASE-C-REPORT.md` Section 4 row 1). If a future callsite emerges, guard at the call boundary or short-circuit `ApplyTheme(null)` to no-op. |

---

## 14. References

- ADR sibling: `Docs/architecture/REFACTOR-MVVM.md` (status COMPLETE — pre-requisite met).
- `TODO.md` entry "UI/Theme port from Heimdall.Next" — closed by PR #47, with follow-up coverage/cleanup closed by PR #91.
- Heimdall.Next source (read-only reference): `G:\_dev\SnapConnect\Heimdall.Next\src\Heimdall.App\`
  - `Services/ThemeService.cs` (180 lines) — pattern reference for `ApplyTheme`, `ThemeRevision`, migration.
  - `Themes/DraculaProTheme.xaml` and 6 siblings — palette source.
- WPF-UI 4.2 documentation: <https://wpfui.lepo.co/documentation/themes.html>
- CommunityToolkit.Mvvm: <https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/>
- Dracula palette family canonical reference: <https://draculatheme.com>

---

*ADR drafted from brainstorming session 2026-05-06. Ready for execution as a single PR (`feature/theme-port-heimdall`).*

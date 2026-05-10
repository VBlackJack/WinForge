# Win11Forge — UI flat-text consolidation + WPF-UI 4.3 disabled-state fix

**Status:** §2.6 (flat-text consolidation) **REVERSED** on 2026-05-10 after visual review of
the v2026051001 release — see §7. All other sections (Apps view fixes, hover contrast,
WPF-UI 4.3 disabled-state fork, ThemeService bridge) remain in effect.
**Author:** Julien Bombled.
**Date:** 2026-05-10 (initial), 2026-05-10 (§7 follow-up).
**Scope:** Initially app-wide visual unification of `ui:Button` to `Appearance="Transparent"`,
fork of WPF-UI 4.3 `DefaultUiButtonStyle` template to fix disabled-state quirk,
ThemeService bridge extension, plus orthogonal Apps-view bug fixes uncovered along
the way. Visual unification reversed in §7; everything else kept.
**Type:** Feature polish + framework defect workaround. No audit finding driving this.

---

## 1. Why this exists

The Win11Forge GUI inherited a Fluent-style hierarchy where buttons advertised
themselves with three appearance levels: `Primary` (filled accent), `Secondary`
(subtle filled with accent border + accent text), and `Transparent` (flat text).
Two problems showed up in practice on the Dracula theme:

1. **The visual hierarchy was inconsistent across pages.** Same kind of action
   would be `Primary` on one page and `Secondary` on another. Toolbar groups
   mixed `Primary`/`Secondary`/`Transparent` so the eye couldn't pick the
   "first action". The user explicitly preferred a **single flat-text style
   everywhere**, with destructive intent communicated through icons +
   confirmation dialogs rather than colored chrome.

2. **The `Secondary` and disabled rendering carried Light Fluent residues.**
   On dark Dracula, disabled buttons rendered as bright Light Fluent
   `#F4F4F4` blocks regardless of any user-scope override we attempted.
   Hover backgrounds defaulted to a high-luminance fill that failed WCAG AA
   contrast against the foreground text.

This document records the issues, the debugging path, and the final solutions.
The companion ADR `THEME-PORT.md` describes the Dracula palette + `ThemeService`
infrastructure that this work builds on.

---

## 2. Issues and resolutions

### 2.1 Column Visibility menu silently broken (Apps view)

**Symptom.** Clicking the Table-icon toggle above the Apps `DataGrid` did nothing
visible. Four prior fix attempts had stacked workarounds without solving the
root cause.

**Root cause analysis.** Two compounding bugs:

- WPF-UI 4.3's `DropDownButton.Flyout` property is typed `ContextMenu`, not an
  arbitrary content host. The previous code on `main` placed a `ui:Flyout`
  inside `<ui:DropDownButton.Flyout>` — XAML accepts the markup at parse time
  but the runtime silently ignores it, so the button never opened anything.
- A subsequent attempt switched to `ui:Button` + `ContextMenu` + `Click`
  handler. The `ContextMenu` lives in its own NameScope, so the
  `PlacementTarget="{Binding ElementName=ColumnVisibilityButton}"` binding
  could not resolve, and the chained `DataContext="{Binding
  PlacementTarget.DataContext, RelativeSource={RelativeSource Self}}"` then
  evaluated against a null `PlacementTarget`. Setting `PlacementTarget` from
  the click handler before opening did not consistently re-resolve the
  `DataContext` binding chain.

**Resolution.** Adopt the proven pattern from `Views/AppCatalogView.xaml`:
`ui:DropDownButton` whose `Flyout` is a `ContextMenu` populated with
`MenuItem IsCheckable="True" StaysOpenOnClick="True"` bound `TwoWay` to the
`Show*Column` `[ObservableProperty]` family on `AppsViewModel.Filters.cs`. WPF-UI
auto-wires `PlacementTarget` and `DataContext` for that pattern, so no
NameScope binding gymnastics are needed. The custom `MenuItem` template in
`App.xaml` already renders the `CheckGlyph` via a `MultiTrigger` on
`IsCheckable=True ∧ IsChecked=True`, so the checkmarks show without
additional work.

**Files.**
- `Views/AppsView.xaml` (lines around 466 — `ui:Button` + `ContextMenu` removed,
  replaced with `ui:DropDownButton` + flyout `ContextMenu`)
- `Views/AppsView.xaml.cs` (`ColumnVisibilityButton_Click` handler removed)
- `ViewModels/Apps/AppsViewModel.Filters.cs`
  (`_isColumnVisibilityPopupOpen` `[ObservableProperty]` removed — never read
  anywhere)

### 2.2 Hover background failed WCAG AA contrast on Secondary / Transparent

**Symptom.** Hovering `Select All`, `Clear Selection`, `Save Profile`, or any
other Secondary/Transparent button produced a hover background close to the
luminance of the foreground text (purple-on-mid-gray). Estimated contrast ratio
~2.25:1, well below the 4.5:1 WCAG AA threshold for normal text.

**Root cause.** Our App.xaml `Style.Triggers` mapped `MouseOverBackground` to
`HighlightBrush` for both `Secondary` and `Transparent`. Dracula's `HighlightBrush`
is `#4A4E66` — a medium gray-purple. Combined with the foreground brushes
`ThemeAdaptiveAccentBrush` (`#C4A5FF`, Secondary text) or
`TextFillColorPrimaryBrush` (`#F7F7F3`, Transparent text), the text/background
luminance gap was insufficient.

**Resolution.** Re-target both `Secondary` and `Transparent`
`MouseOverBackground` and `PressedBackground` setters to `SurfaceBrush`
(Dracula `#1B1C25` — very dark, near-black). Resulting contrast ratios:
~5.6:1 against the purple Secondary text and ~6:1 against the light Transparent
text, both clearing AA.

**Files.**
- `App.xaml` (Appearance triggers in the implicit `ui:Button` style)

### 2.3 WPF-UI disabled state paints Light Fluent regardless of user override

**Symptom.** Any `ui:Button` whose `Command.CanExecute` returned `false` rendered
a stark `#F4F4F4` block on the dark Dracula card, ignoring every override we
attempted.

**Root cause.** The WPF-UI 4.3 `DefaultUiButtonStyle.ControlTemplate.Triggers`
contains:

```xml
<Trigger Property="IsEnabled" Value="False">
    <Setter TargetName="ContentBorder" Property="Background"
            Value="{DynamicResource ButtonBackgroundDisabled}" />
    <Setter TargetName="ContentBorder" Property="BorderBrush"
            Value="{DynamicResource ButtonBorderBrushDisabled}" />
    <Setter Property="Foreground"
            Value="{DynamicResource ButtonForegroundDisabled}" />
</Trigger>
```

Empirically, the `{DynamicResource ButtonBackgroundDisabled}` lookup in this
trigger does **not** resolve any of the user-scope overrides we placed:

- `<StackPanel.Resources>` on the parent stack
- `<ui:Button.Resources>` on the button itself
- `<UserControl.Resources>` on the view root
- `Application.Current.Resources` direct entry (set programmatically by
  `ThemeService.ApplyPaletteBridgeResources`)

Diagnostic instrumentation confirmed that
`Application.Current.Resources["ButtonBackgroundDisabled"] = Dracula SurfaceBrush
(#FF1B1C25)` was correctly seeded after every theme apply step (initial,
post-`base.OnStartup`, post-second-apply, post-high-contrast). Yet a pixel-sample
of any disabled `ui:Button` returned the Light Fluent value, and a probe
brush set to `#FF0000` at `<UserControl.Resources>` did not turn the button
red. The conclusion is that this specific `DynamicResource` lookup, originating
from a `TargetName=`-targeted setter inside a `ControlTemplate.Triggers`, takes
a path that bypasses the visual/logical tree — at least for our combination of
WPF-UI 4.3 + .NET 10 + the theme dictionaries our `ApplicationThemeManager.Apply`
chain produces.

**Resolution.** Fork the upstream `DefaultUiButtonStyle` `ControlTemplate`
verbatim into our App.xaml implicit `ui:Button` style. The fork preserves:

- `Icon` DP support via the `ControlIcon` `ContentPresenter`
- `RecognizesAccessKey="True"` on the content `ContentPresenter` (preserves
  underscore access-key rendering)
- The `MultiTrigger`-driven hover/pressed mechanics that bind
  `ContentBorder.Background` to the templated parent's `MouseOverBackground` /
  `PressedBackground`
- All upstream `Appearance` triggers (`Primary` / `Secondary` / `Dark` / `Light`
  / `Info` / `Danger` / `Success` / `Caution`)
- The `InsetBorder` for visual fidelity

The single deliberate deviation: the `IsEnabled=False` trigger sets
`ContentBorder.Opacity=0.45` instead of overwriting the three brushes.
Background then stays at the Button's `TemplateBinding Background` (which our
`Appearance` setters drive correctly), and the visible button greys out cleanly
without the Light Fluent block.

The Codex review pass caught two issues with our first draft of the fork (a
minimal template that broke `Icon`-only buttons in `MainWindow.xaml`'s
`NavigationView` and dropped `RecognizesAccessKey`) — the verbatim fork
corrected both.

**Files.**
- `App.xaml` (implicit `ui:Button` style — full template fork; the only
  semantic change is the `IsEnabled=False` trigger)

### 2.4 `_selectedCount` did not invalidate `UninstallSelectedCommand`

**Symptom.** Selecting an item in the Apps `DataGrid` correctly enabled the
Install Selected button (its background flipped to themed purple), but the
Uninstall Selected button kept rendering in its disabled state — even though
`CanUninstallSelected => SelectedCount > 0 && !IsInstalling && !IsUninstalling`
should have returned `true`.

**Root cause.** `ViewModels/AppsViewModel.cs:131-133` declared:

```csharp
[ObservableProperty]
[NotifyCanExecuteChangedFor(nameof(InstallSelectedCommand))]
private int _selectedCount;
```

Only `InstallSelectedCommand` was wired to refresh on `SelectedCount` change.
`UninstallSelectedCommand`'s `CanExecute` was never re-queried, so the
underlying `ButtonBase.IsEnabledCore` (which AND's the local `IsEnabled` with
`ICommand.CanExecute`) stayed at `false`. The visual was correct given the
state, the state was simply stale.

**Resolution.** Add the matching attribute:

```csharp
[ObservableProperty]
[NotifyCanExecuteChangedFor(nameof(InstallSelectedCommand))]
[NotifyCanExecuteChangedFor(nameof(UninstallSelectedCommand))]
private int _selectedCount;
```

This is the kind of bug only reproducible by clicking through state, easy to
miss in a code review. Codex's diagnostic ("source `ButtonBase` shows
`IsEnabledCore = base.IsEnabledCore && CanExecute`, so `IsEnabled="True"`
inline cannot win against a stale `CanExecute=false`") was the lead that
sent us looking at the VM rather than the XAML.

**Files.**
- `ViewModels/AppsViewModel.cs`

### 2.5 DataGrid app-name column visibly top-biased

**Symptom.** In the Apps `DataGrid`, application name + description rendered
visibly closer to the top of the row (~`MinHeight=56`) than expected for a
"vertically centered" cell.

**Root cause.** The implicit `EnhancedDataGridCellStyle` set
`VerticalContentAlignment="Center"`, but the `DataTemplate` content was a
`StackPanel Orientation="Vertical"` whose default `VerticalAlignment` is
`Stretch`. The cell centered the stretched panel (no-op), not the panel's
content.

**Resolution.** Set `VerticalAlignment="Center"` on the outer
`StackPanel` of the Application Name `CellTemplate`, and the same on the
Selection `CheckBox` cell template. Pixel sampling confirms title/description
now sit at row center within ~3 px.

**Files.**
- `Views/AppsView.xaml` (Application Name + Selection `CellTemplate`s)

### 2.6 Visual hierarchy fragmentation

**Symptom.** Pages mixed `Primary` (filled accent), `Secondary` (subtle filled),
and `Transparent` (flat text) appearances inconsistently. The user wanted a
single flat-text presentation everywhere — destructive intent communicated by
icons + confirm dialogs rather than colored chrome.

**Resolution.** Convert every `Appearance="Primary|Secondary|Danger"` and
every `Style="{StaticResource HeroPrimaryButton|WarningPrimaryButton|`
`SecondaryButton|DestructiveSolidButton}"` instance to
`Appearance="Transparent"`. ~60 buttons across 15 files were touched. Hover
and disabled behavior are uniform across the app via the App.xaml implicit
style.

**Files.** All views and dialogs under `GUI/Win11Forge.GUI/Views/`,
`Controls/`, and `UserControls/`. Keyed styles `HeroPrimaryButton`,
`WarningPrimaryButton`, `SecondaryButton`, and `DestructiveSolidButton`
remain defined for backward compatibility but are no longer referenced from
instance `Style="..."` bindings.

---

## 3. ThemeService bridge extensions

`Services/ThemeService.cs:PaletteBrushResourceMap` was extended with the
following keys to ensure every WPF-UI button/checkbox state pulls Dracula
brushes:

- `ButtonBackground`, `ButtonForeground`, `ButtonBorderBrush` (rest)
- `ButtonBackgroundPointerOver` / `Pressed` / `Disabled`
- `ButtonForegroundPointerOver` / `Pressed` / `Disabled`
- `ButtonBorderBrushPressed` / `Disabled`
- `CheckBoxBackground`, `CheckBoxForeground`, `CheckBoxBorderBrush`,
  `CheckBoxCheckBorderBrush`, `CheckBoxCheckGlyphForeground`
- `CheckBoxCheckBackgroundFillChecked` (+ PointerOver/Pressed)
- `CheckBoxCheckBackgroundFillUnchecked` PointerOver/Pressed/Disabled
- `CheckBoxCheckBackgroundStrokeUncheckedDisabled`
- `CheckBoxForegroundUncheckedDisabled`

`PaletteColorResourceMap` gained `Palette*Color` entries
(`PaletteRedColor`, `PaletteGreenColor`, `PaletteOrangeColor`,
`PaletteLightBlueColor`) so the upstream `Appearance="Danger|Success|Caution|`
`Info"` triggers — which wrap these `Color`s inside a `SolidColorBrush`
constructor at template eval time — pick up Dracula values rather than
WPF-UI's Fluent palette. This is dead code in current usage (no instance still
uses those appearances after §2.6), but the mappings remain for forward
compatibility if the appearances are ever reintroduced.

---

## 4. What this is **not**

- It is not a port of the Heimdall `CommonControls.xaml` (we still target
  WPF-UI 4.3, this is the deliberate Strategy 1 decision recorded in
  `THEME-PORT.md`).
- It is not a re-theming of Dracula colors (the seven Dracula palettes are
  unchanged — only mappings to WPF-UI consumed brushes are extended).
- It does not change the public XAML namespace or break any external
  consumer of the `PrimaryButton` / `SecondaryButton` /
  `DestructiveSolidButton` keyed styles — they still resolve, they're just
  no longer referenced from our own views.

---

## 5. Verification

Manual test matrix exercised on `claude/zealous-bardeen-d31c33` with the
DraculaPro theme active:

| Case | Expected | Observed |
|---|---|---|
| Apps view, 0 selected | Install/Uninstall Selected greyed text, no white blocks | ✓ |
| Apps view, 1 installable selected | Install Selected fully visible, Uninstall greyed | ✓ |
| Apps view, 1 installed app selected | Both visible | ✓ |
| Apps view, hover Select All | Dark surface hover, light text readable | ✓ |
| Apps view, Column Visibility menu | Menu opens below button, 6 toggle items + reset | ✓ |
| Apps view, DataGrid row | Title + description vertically centered | ✓ |
| Dashboard, ready state | Start Deployment flat text | ✓ |
| Prerequisites, all green | Install/Reinstall flat text | ✓ |
| App Catalog header | Add / Import / Export / Verify all flat text | ✓ |
| App Catalog row, app selected | Edit / Duplicate / Delete flat text | ✓ |
| Settings → Appearance | Theme combobox + Apply flat text | ✓ |
| Save Profile dialog | Cancel / Save flat text | ✓ |
| Application Picker dialog | Cancel / Add flat text | ✓ |
| Application Editor dialog | Cancel / Save flat text, all 3 source-search buttons flat | ✓ |
| Confirm dialog | Cancel / Confirm flat text | ✓ |
| Error dialog | Copy Details / Help / Retry / OK flat text | ✓ |

Build: `dotnet build GUI\Win11Forge.GUI\Win11Forge.GUI.csproj -c Debug` →
0 warnings, 0 errors.

Pixel sampling on disabled action buttons returns Card-bg or near-Card values
(`#3B3D51` or `#4A4B5D`), no `#F4F4F4` Light Fluent.

---

## 6. References

- WPF-UI 4.3 source for the upstream Button template:
  https://github.com/lepoco/wpfui/blob/4.3.0/src/Wpf.Ui/Controls/Button/Button.xaml
- WPF DependencyProperty value precedence:
  https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/dependency-property-value-precedence
- `THEME-PORT.md` — palette + service ADR this work extends.
- `Win11Forge-UX-Audit-2026-05.md` — orthogonal UX audit (none of its
  findings drove this work).

---

## 7. 2026-05-10 follow-up: §2.6 reversed — visual hierarchy restored

**Status:** done in commit `27b898d` on branch `claude/restore-button-appearances`.

### 7.1 Why the reversal

§2.6 was based on a stated user preference for a single flat-text presentation
everywhere. After §2.6 landed via PR #94 and a release `v2026051001` was built
and visually inspected on the actual Dracula dark theme, the result did not
match the user's actual aesthetic preference. The reported feedback after seeing
the result in-app was simply "all buttons became ugly again" — the lack of
hierarchy made every action toolbar look like a row of plain text labels with
no clear primary CTA, no visual cue distinguishing constructive from destructive,
and the hero CTAs on Dashboard and Prerequisites lost their dominance over
surrounding navigation.

The reversal restores the pre-PR #94 `Appearance` values verbatim. It does
**not** restore the pre-PR #94 inline `Background` / `Foreground` /
`Padding` / `Height` / `FontSize` overrides on the Dashboard hero CTAs (those
hardcoded an orange `AccentOrangeTextBrush` over white at WCAG 1.73:1 —
documented in the consolidated audit as a separate finding C2-bis). The Hero
CTAs in the restored state use `Appearance="Primary"` only, picking up the
theme accent via the App.xaml implicit `ui:Button` `Style.Triggers`.

### 7.2 What was restored

70 `Appearance` attributes restored across 20 files (every change reversed
exactly as it was before PR #94):

| Role | Style | Examples |
|---|---|---|
| Constructive primary | `Appearance="Primary"` (accent fill) | Confirm, Save, OK, Apply, Install Selected, Add (App Catalog), Start Deployment, Restart Application, Create Scheduled Deployment, Install Prerequisites, Apply Search Result |
| Secondary outlined | `Appearance="Secondary"` (subtle bg + accent border + accent text) | Cancel, Close, Save Profile, Browse, Test (source editors), Refresh, Open Log Folder, Export Logs, Filter actions (Select All / Clear / etc.), Search (Winget/Choco/Store), Uninstall Selected, Cancel Scan, Cancel Verification |
| Destructive | `Appearance="Danger"` (red) | Delete (App Catalog row action) |

Full file list: `Controls/ConfirmDialog`, `Controls/EmptyStateControl`,
`Controls/ErrorDialog`, `Controls/KeyboardShortcutsPanel`, `Controls/LoadingOverlay`,
`Controls/OnboardingDialog`, `UserControls/ChocolateySourceEditor`,
`UserControls/DetectionEditor`, `UserControls/DirectDownloadSourceEditor`,
`UserControls/WingetSourceEditor`, `Views/AppCatalogView`,
`Views/ApplicationPickerDialog`, `Views/AppsView`, `Views/DashboardView`,
`Views/DeploymentView`, `Views/Dialogs/ApplicationEditorDialog`,
`Views/LogsView`, `Views/PrerequisitesView`, `Views/SaveProfileDialog`,
`Views/SettingsView`.

### 7.3 What was kept from PR #94

All architecture-side improvements remain in place — the reversal is strictly
a view-side `Appearance` attribute restoration:

- §2.1 Apps view Column Visibility menu (`ui:DropDownButton` + `ContextMenu` flyout pattern).
- §2.2 Hover/pressed contrast fix (`Secondary` and `Transparent` `MouseOverBackground`
  / `PressedBackground` re-targeted to `SurfaceBrush`, ~5.6:1–6:1).
- §2.3 WPF-UI 4.3 disabled-state fork in App.xaml (`ContentBorder.Opacity=0.45`
  instead of the upstream `ButtonBackgroundDisabled` overwrite that did not
  honor user-scope overrides).
- §2.4 `_selectedCount` `[NotifyCanExecuteChangedFor(nameof(UninstallSelectedCommand))]`.
- §2.5 DataGrid app-name column vertical centering (`StackPanel VerticalAlignment="Center"`).
- §3 ThemeService `PaletteBrushResourceMap` extensions (~22 button/checkbox state keys)
  and `Palette*Color` entries — `Danger` / `Success` / `Caution` / `Info` triggers
  again pick up Dracula values now that those appearances are in active use again.

The keyed styles in App.xaml (`HeroPrimaryButton` / `WarningPrimaryButton` /
`PrimaryButton` / `SecondaryButton` / `OutlinedButton` / `DestructiveButton` /
`DestructiveSolidButton` / `IconButton` / `StatsCardButton` / `QuickActionButton` /
`FavoriteIconButton`) remain defined but are still **not referenced** from
instance `Style="..."` bindings — the views use `Appearance="..."` directly,
which lets the implicit `ui:Button` `Style.Triggers` in App.xaml drive the
theme-adaptive Background / BorderBrush / Foreground from
`ThemeAdaptiveAccentBrush` / `BadgeTextBrush` / etc. These keyed styles are
useful as a fallback API but are intentionally unused for now.

### 7.4 Verification (replaces §5)

Build: `dotnet build GUI\Win11Forge.GUI\Win11Forge.GUI.csproj -c Debug` →
0 warnings, 0 errors.

Runtime test on `v2026051002` build with the active Dracula theme:

| Case | Expected | Observed |
|---|---|---|
| Apps view, 0 selected | Install Selected disabled (opacity 0.45), Uninstall outlined | ✓ |
| Apps view, 1 installable selected | Install Selected accent fill, Uninstall outlined | ✓ |
| Apps view, Save Profile | Outlined accent (border + text) — matches user reference screenshot | ✓ |
| Apps view, hover Select All | Dark surface hover, light text readable | ✓ (kept from §2.2) |
| Apps view, Column Visibility menu | Menu opens below button, 6 toggle items + reset | ✓ (kept from §2.1) |
| Apps view, DataGrid row | Title + description vertically centered | ✓ (kept from §2.5) |
| Dashboard, ready state | Start Deployment accent fill, View Updates outlined | ✓ |
| Dashboard, prereq missing state | Fix Prerequisites accent fill | ✓ |
| Prerequisites, all green | Install/Reinstall accent fill | ✓ |
| App Catalog header | Add accent fill, Import/Verify/Cancel outlined | ✓ |
| App Catalog row, app selected | Edit/Duplicate outlined, Delete red (Danger) | ✓ |
| Settings → Appearance | Apply accent fill | ✓ |
| Save Profile dialog | Cancel outlined, Save accent fill | ✓ |
| Application Picker dialog | Cancel outlined, Confirm accent fill | ✓ |
| Application Editor dialog | Cancel outlined, Save accent fill, source-search buttons outlined | ✓ |
| Confirm dialog | Cancel outlined, Confirm accent fill | ✓ |
| Error dialog | Help/Retry outlined, OK accent fill | ✓ |
| Disabled state, any theme | `ContentBorder.Opacity=0.45` (no white Fluent block) | ✓ (kept from §2.3) |

Pixel sampling on disabled action buttons returns the active button background
at 0.45 opacity over the card — no `#F4F4F4` Light Fluent leakage.

### 7.5 Lessons learned

The original §2.6 proceeded from a stated preference without a cheap visual
verification step. The reversal cycle ate roughly two extra sessions
(audit-then-revert) that a 30-second runtime sanity check on a release build
before merging PR #94 would have prevented. For future visual changes that
touch the entire app surface, run `Build-Release.ps1` and hand-test a release
ZIP before merging the PR — even when the code change is mechanical and the
intent is documented in an ADR.

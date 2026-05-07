# ADR — .NET 8 → .NET 10 Migration

**Status:** In review via PR #52; migration executed in commit `38dce7e355e115c2bcfa0490b5184673bf1350fc`, merge pending.
**Last updated:** 2026-05-07.
**Sister ADRs:** [`REFACTOR-MVVM.md`](REFACTOR-MVVM.md), [`THEME-PORT.md`](THEME-PORT.md), [`COWORK-RESUME-PROMPT.md`](COWORK-RESUME-PROMPT.md).
**Source:** TODO.md "Migration .NET 8 → .NET 10 — aligner sur Heimdall.Next" entry, post-resume request.

## 1. Goal

Migrate Win11Forge GUI + Tests + UITests from `net8.0-windows` (.NET 8 LTS, support ends November 2026) to `net10.0-windows` (.NET 10 LTS, support ends November 2028). Align with Heimdall.Next, which targets the same `net10.0-windows` generic moniker.

## 2. Decisions (Q1..Q9 from study)

| Q | Decision | Rationale |
|---|---|---|
| Q1 — PR slicing | Single PR all-in-one | Atomic rollback, mirrors PR #47 / #49 / #50 / #51 pattern. |
| Q2 — xunit | Stay on 2.9.x | xunit v3 is a separate breaking refactor; deferred. |
| Q3 — C# 14 | No new features in this PR | Migration = TargetFramework bump only; C# 14 adoption deferred. |
| Q4 — UI tolerance | Zero | Smoke test required before merge, mirroring theme port + a11y hardening protocol. |
| Q5 — Timing | Immediately after PR #51 | All P0 audit findings are closed; strategic upgrade window opened per COWORK-RESUME-PROMPT priority rules. |
| Q6 — Heimdall align | Match exact `net10.0-windows` | Verified by reading Heimdall csproj: generic moniker, no Windows version pin. |
| Q7 — TODO.md | Untouched in this PR | TODO closure flip belongs to a separate `chore(todo):` PR. |
| Q8 — Approval | Direct draft after Q1..Q7 confirmation | Standard Cowork → Codex flow. |
| Q9 — Final GO | GO | Compat path verified for all coupled dependencies. |

## 3. Compat Matrix

| Component | Before | After | Reason |
|---|---|---|---|
| TargetFramework | `net8.0-windows` | `net10.0-windows` | LTS alignment (.NET 10 LTS to November 2028 vs .NET 8 to November 2026). |
| WPF-UI | 4.2.0 | 4.3.0 | 4.3.0 was selected because it provides explicit `net10.0-windows` support. |
| CommunityToolkit.Mvvm | 8.4.0 | 8.4.2 | 8.4.0 has a known default .NET 10 compile issue; 8.4.2 is the mitigation. |
| System.Management.Automation | 7.4.6 | 7.6.0 | SMA 7.5+ requires .NET 9 minimum, so the bump is coupled to the TargetFramework bump. |
| Microsoft.Extensions.DependencyInjection | 8.0.1 | 10.0.7 | Version alignment with .NET 10 package family; API surface used by Win11Forge remains stable. |
| CI `dotnet-version` | 8.0.x | 10.0.x | Updated in security, test-gui, and deploy jobs. |

The generated lock files target `net10.0-windows7.0`, which is NuGet's resolved Windows platform version for the generic `net10.0-windows` TFM.

## 4. Execution Notes

Commit `38dce7e355e115c2bcfa0490b5184673bf1350fc` contains:

- 3 csproj TFM bumps to `net10.0-windows`.
- The 4 coupled production package bumps listed above.
- CI setup-dotnet updates from `8.0.x` to `10.0.x`.
- 3 regenerated `packages.lock.json` files from `dotnet restore --force-evaluate`.
- One analyzer-driven source fix in `PowerShellExecutionService.ReadStreamWithLimitAsync`: replacing `while (!reader.EndOfStream)` with a `ReadAsync` loop that exits on `charsRead == 0`. This preserves behavior and removes the new .NET 10 `CA2024` warning.
- Two leftover `net8.0-windows` literals corrected post-audit (Drift C, audit GO-with-nits): `Start-Win11ForgeGUI.ps1` dev launcher binary path and `GUI/Win11Forge.GUI.UITests/Win11ForgeAppSession.cs` UIA harness fallback path. The harness primary path (`localCopy`) was already independent of TFM, so CI was not affected.
- Transitive dependency note: `Microsoft.PowerShell.Native@700.0.0-rc.1` is pulled by SMA 7.6.0. RC tag is upstream's choice for the .NET 10-targeted PowerShell native interop; not a Win11Forge instability signal. Tracked for future SMA 7.6.x stable bump.
- Manual smoke follow-up fixed two regressions before merge readiness: prerequisite install logs now force UTF-8 and use the configured app language for PowerShell localization, and native WPF `ComboBox` / `ContextMenu` / `DataGrid` selection styling is explicitly theme-aware for Application Manager and Settings.

## 5. Verification

Local verification on 2026-05-07:

| Check | Result |
|---|---|
| `dotnet --list-sdks` | `10.0.103` installed. |
| `dotnet restore --force-evaluate GUI/Win11Forge.GUI/Win11Forge.GUI.csproj` | Passed. |
| `dotnet restore --force-evaluate GUI/Win11Forge.GUI.Tests/Win11Forge.GUI.Tests.csproj` | Passed. |
| `dotnet restore --force-evaluate GUI/Win11Forge.GUI.UITests/Win11Forge.GUI.UITests.csproj` | Passed. |
| `dotnet build GUI/Win11Forge.slnx --configuration Release` | Passed: 0 warnings, 0 errors. |
| `dotnet test GUI/Win11Forge.GUI.Tests/Win11Forge.GUI.Tests.csproj --configuration Release --no-build` | Passed: 411 passed, 0 failed. |
| `dotnet test GUI/Win11Forge.GUI.UITests/Win11Forge.GUI.UITests.csproj --configuration Release --no-build` | Passed with `WIN11FORGE_RUN_UIA=1`: 2 passed, 0 failed. |
| `dotnet test GUI/Win11Forge.slnx -c Release` after manual smoke fixes | Passed: 422 GUI tests passed, 2 UIA tests skipped by default. |

Evidence path for UIA screenshots:

`TestResults/manual-smoke-dotnet10-20260507-142032/screenshots/`

Captured files:

- `01-dashboard.png`
- `02-applications.png`
- `03-app-catalog.png`
- `04-settings.png`
- `05-deployment.png`
- `06-prerequisites.png`
- `settings-theme-picker.png`

An initial isolated run of `SettingsThemePicker_IsDiscoverable` timed out once. Direct UIA inspection confirmed `PageSettings` and `ThemePicker` were present after clicking `NavSettings`, and the immediate full UIA rerun passed 2/2. Treat this as an observed UIA harness flake, not a confirmed runtime regression.

## 6. Manual Smoke Scope

The following smoke items are intentionally left for PR review / local operator confirmation because they change machine state or require user-visible desktop settings:

- Install / uninstall workflow on a real package such as 7-Zip.
- DPI 100% / 125% / 150% switching.
- Full visual theme cycle across all 8 themes beyond the automated core-screen captures. Application Manager and Settings native dropdown/menu regressions found during manual smoke were fixed before merge readiness.
- High Contrast and Reduced Motion toggles beyond the existing regression coverage from PR #49 and PR #51.

Do not merge PR #52 until these are either executed manually or explicitly waived by the reviewer.

## 7. Behavior Changes

- **WPF-UI 4.2 → 4.3:** no code-level API migration was required. Automated UIA screenshots for core screens are non-empty and the Settings theme picker remains discoverable after rerun.
- **SMA 7.4.6 → 7.6.0:** restore/build/test succeeded. Real install/uninstall workflow validation is pending manual smoke because it modifies the local machine.
- **.NET 10 analyzers:** `CA2024` surfaced a pre-existing async stream-read pattern; fixed in commit 1.
- **C# language version:** implicit C# version advances with `net10.0-windows`, but this PR intentionally adopts no C# 14 syntax.
- **PowerShell sub-process encoding & locale (smoke fix, commit 4):** `PowerShellExecutionService` now sets `StandardOutputEncoding = StandardErrorEncoding = UTF8` on every spawned PowerShell sub-process and propagates the configured `settings.LanguageCode` to the `Initialize-Localization` script. Scope is centralized at the execution-service layer, so the encoding fix benefits all consumers (Prerequisites install/uninstall, ApplicationManagementServiceImpl, PowerShellBridgeFacade), not only the prerequisites flow that surfaced the regression. Behavior is non-regressive: UTF-8 is a superset of ASCII for the parse contracts (`INSTALLED` / `NOT_INSTALLED` / JSON payloads), and the locale propagation falls back cleanly when the configured language is unrecognized.
- **Native WPF control theming (smoke fix, commit 4):** explicit theme-aware templates added for native `ComboBox`, `ContextMenu`, `MenuItem`, `ComboBoxItem`, and `DataGrid` row selection in the Application Manager and Settings views. Templates bind to `DynamicResource` keys already defined in the WPF-UI palette, so HC-mode overrides (A11Y-001 closure post-PR #49) and `HighVisibilityFocusVisual` (A11Y-003 closure post-PR #49) continue to win across all 8 themes — verified during the commit 4 re-audit.

## 8. Rollback Note

If a regression surfaces post-merge:

```powershell
git revert -m 1 <merge-commit-of-PR-52>
```

The migration is scope-isolated to framework/dependency/build changes plus one analyzer fix, so rollback should be clean. Reopen this ADR with status `REVERTED` and record the blocker before retrying.

## 9. Follow-ups

- After PR #52 merges, open a separate `chore(todo):` PR to mark the .NET 10 TODO entry closed with the merge SHA.
- Consider C# 14 feature adoption in a dedicated `refactor:` PR.
- Consider xunit 2.9.x → v3 in a dedicated `chore(test):` PR.
- Consider aligning `Directory.Build.props` with Heimdall's stricter warning policy only after a warning audit.

## 10. References

- WPF-UI NuGet: https://www.nuget.org/packages/wpf-ui/
- WPF-UI target frameworks: https://github.com/lepoco/wpfui/blob/main/src/Wpf.Ui/Wpf.Ui.csproj
- CommunityToolkit.Mvvm .NET 10 issue: https://github.com/CommunityToolkit/dotnet/issues/1139
- CommunityToolkit.Mvvm NuGet: https://www.nuget.org/packages/CommunityToolkit.Mvvm
- System.Management.Automation NuGet: https://www.nuget.org/packages/system.management.automation/
- SMA 7.5+ .NET 9 requirement: https://github.com/PowerShell/PowerShell/issues/24891
- Microsoft.Extensions.DependencyInjection NuGet: https://www.nuget.org/packages/microsoft.extensions.dependencyinjection
- COWORK-RESUME-PROMPT.md priority sequencing rules.

## 11. Status History

| Date | Status | By | Note |
|---|---|---|---|
| 2026-05-07 | Phase Étude / CONDITIONAL GO | Cowork-Claude | Initial study; Q1..Q8 awaiting answers. |
| 2026-05-07 | GO | Cowork-Claude + user | Q1..Q9 locked all-recommended; Heimdall config verified. |
| 2026-05-07 | In review via PR #52 | Codex executor | Migration commit executed, automated build/tests/UIA smoke green; manual state-changing smoke pending. |
| 2026-05-07 | Manual smoke fixes | Cowork-Claude + user | Prerequisite log locale/encoding and native WPF dropdown/context-menu theming regressions fixed; Release suite green after patch. |

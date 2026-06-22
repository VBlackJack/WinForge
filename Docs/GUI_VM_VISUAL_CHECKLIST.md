# GUI VM Visual Checklist

## Scope
Manual visual/accessibility validation for the current Win11Forge GUI after the May 2026 backlog closure:
- theme persistence and consistency
- light/dark/high-contrast readability
- reduced-motion behavior
- responsiveness at lower resolutions and high DPI scaling
- App Catalog and Apps workflow safety states

## Test Matrix
Run the checklist on these combinations:

1. Resolution `1366x768` at `100%` scaling
2. Resolution `1366x768` at `125%` scaling
3. Resolution `1366x768` at `150%` scaling
4. Resolution `1920x1080` at `100%` scaling
5. Resolution `1920x1080` at `125%` scaling

For each combo, validate:
- Theme `Light`
- Theme `Dark`
- High contrast `On` (from Settings)
- Reduced motion `On/Off` (from Settings)

## Startup Checks
1. Launch app.
2. Confirm startup theme matches saved setting.
3. Navigate to `Settings`, confirm theme does not auto-switch unexpectedly.
4. Close/reopen app, confirm same theme remains.

Expected:
- No spontaneous switch when opening `Settings`.
- Theme, high contrast, and reduced motion persist after restart.

## Navigation + Layout Checks
Validate each page:
- `Dashboard`
- `Prerequisites`
- `Apps`
- `Deployment`
- `Settings`
- `Logs`
- `App Catalog`

For each page:
1. Resize window to minimum width.
2. Resize back to normal.
3. Verify no clipped titles/buttons/icons.
4. Verify no overlapping controls.
5. Verify actionable controls remain reachable.

Expected:
- No truncated critical actions.
- No control overlap.
- Any horizontal scroll only where intentionally enabled (filter bars in narrow widths).

## Page-Specific Checks

### Apps
1. Open filter bar at narrow width.
2. Confirm horizontal scroll appears when needed.
3. Confirm scan/update actions remain accessible.
4. Start an install batch and check progress row controls (pause/resume/cancel).
5. Start a selected update batch and confirm Pause/Resume are hidden while Update runs.
6. Select a profile, change app checkboxes, and confirm `Update profile` remains visible and aligned in the profile card.

Expected:
- Filter area remains usable at small width.
- Touch-size icon buttons are still properly aligned.
- Pause/Resume are visible only for pause-aware batches.
- Profile update and save actions remain distinct and readable.

### Logs
1. Check filter row at narrow width.
2. Confirm horizontal scroll appears when needed.
3. Verify filter-clear/delete icons remain clickable and visible.

Expected:
- No clipped filter controls at `1366x768` + `150%`.

### Settings
1. Change the theme from the theme picker.
2. Toggle `Reduced motion`.
3. Toggle `High contrast`.
4. Restart app and confirm all 3 settings persisted.

Expected:
- Toggles take effect immediately.
- No mismatch between toggle state and visual result.
- Theme selection persists across restart.

### Deployment
1. Open log viewer dialog.
2. Verify dialog fits at narrow widths and buttons remain visible.

Expected:
- Dialog remains usable at minimum window size.

## Accessibility Checks
1. Navigate core actions with keyboard (`Tab`, `Shift+Tab`, `Enter`, `Space`).
2. Confirm focus indicator is visible on interactive controls.
3. Validate screen-reader labels for controls in:
   - Detection editor
   - Direct download source editor
   - Store source editor
4. In light theme, inspect source badges and status chips for text readability.

Expected:
- Focus ring always visible.
- No unlabeled interactive control in the edited screens.
- Badge text remains readable in light theme.

## Regression Checklist
1. Open app directly to last visited page.
2. Perform Undo/Redo from header buttons.
3. Open App Picker dialog and verify category/search layout at narrow width.
4. Open Logs and Apps pages and confirm no functional regression in filtering.
5. Apply a profile, manually change the selection, then switch profiles and confirm Replace/Merge/Cancel appears.
6. Apply a profile, change the selected applications, click `Update profile`, reselect the same profile, and confirm the saved selection is restored.
7. Save an unchanged App Catalog entry and confirm no visible metadata reset.

## WinSight Smoke

For an opt-in agent-side smoke pass with screenshots, run from the repository root:

```powershell
pwsh -NoProfile -File Tools\Invoke-WinsightSmoke.ps1 -WinsightRoot <path-to-winsight>
```

Expected screenshots:

- `TestResults\winsight\01-dashboard.png`
- `TestResults\winsight\02-settings.png`
- `TestResults\winsight\03-app-catalog.png`

## Defect Template
Use this format for each finding:

- ID: `GUI-VM-###`
- Environment: `Resolution`, `Scaling`, `Theme`, `HighContrast`, `ReducedMotion`
- Screen: `Dashboard/Apps/...`
- Steps:
  1. ...
  2. ...
- Actual result:
- Expected result:
- Severity: `Critical/Major/Minor`
- Screenshot file:

## Exit Criteria
Validation is complete when:
1. No critical issues remain.
2. All startup/theme persistence checks pass.
3. No clipped or overlapping critical controls in target matrix.
4. Accessibility labels and focus behavior pass on edited screens.

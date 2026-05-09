# Win11Forge GUI UIA Tests

These tests launch the real WPF application, drive it through UI Automation, and write PNG screenshots.

They are skipped by default because they require an interactive Windows desktop session.
The harness starts `Win11Forge.GUI.dll` through `dotnet` so the UI smoke tests can run without triggering the product executable's `requireAdministrator` UAC manifest.

Run manually from the repository root:

```powershell
$env:WIN11FORGE_RUN_UIA = '1'
$env:WIN11FORGE_UIA_ARTIFACTS = 'G:\_Projects\Win11Forge\TestResults\ui-screenshots'
dotnet test GUI\Win11Forge.GUI.UITests\Win11Forge.GUI.UITests.csproj --configuration Release
```

Screenshots are written to `WIN11FORGE_UIA_ARTIFACTS` when set, otherwise to a timestamped folder under `%TEMP%\Win11Forge\UIA`.

## Winsight smoke

WinSight is the preferred opt-in agent smoke harness for exploratory desktop checks.
It runs as a sibling repository and is not required for normal CI.

From the Win11Forge repository root:

```powershell
.\Tools\Invoke-WinsightSmoke.ps1 -WinsightRoot G:\_Projects\winsight
```

The script builds the WinSight MCP server and Win11Forge GUI, launches `Win11Forge.GUI.dll` through `dotnet`, drives the app via MCP tools (`list_windows`, `inspect_ui_tree`, `click_element`, `capture_screenshot`), and writes screenshots to `TestResults\winsight`.

You can also set `WINSIGHT_ROOT` and omit the parameter:

```powershell
$env:WINSIGHT_ROOT = 'G:\_Projects\winsight'
.\Tools\Invoke-WinsightSmoke.ps1
```

Use this in addition to the xUnit UIA tests when a change needs richer agent-side inspection or screenshot capture.

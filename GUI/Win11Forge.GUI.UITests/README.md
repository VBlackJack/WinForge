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

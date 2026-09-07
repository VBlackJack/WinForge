# Release validation: 2026090703

[Français](RELEASE_VALIDATION.fr.md)

The Windows 11 Pro disposable VMware acceptance passed installation of 7-Zip 24.09,
upgrade to independently observed 26.02.00.0, partial rollback, and a SYSTEM scheduled
task with exit code 0 and a protected profile snapshot. A separate real deployment
worker interruption during winget.exe execution resumed with one attempt for the
completed first item and two for the interrupted second item.

The WinGet Configuration Test prototype checked DeveloperMode=false through the
Microsoft.Windows.Settings resource and returned 0. Configuration features were
explicitly enabled in the disposable guest; no Configure apply was invoked.
An example is available in Config/Examples/developer-mode-disabled.winget.

Local gates: 2005 Pester passes, 6 skips, 53.94% coverage against a 48% floor;
787 .NET tests; 5 desktop UIA tests. Final targeted regression tests cover native
PowerShell 5.1 argument quoting/timeouts, rollback ownership and interruption.
The release CI runs the full suite against the committed revision.

## Boundaries

- Catalog source evidence is package-specific. The VM result does not certify all
  195 applications. LDPlayer remains unavailable in the checked sources.
- Rollback uninstalls only newly installed supported packages. It does not restore
  prior versions or Windows configuration. Unsupported entries remain visible.
- GUI batch receipts and CLI deployment plans are distinct formats.
- UIA validates navigation, labels, focus and the deployment report. Full spoken
  screen-reader output and every physical monitor/DPI combination are not certified.
- Interrupting a deployment worker cannot make arbitrary vendor installers
  transactional; their own repair procedures may still be required.
- SYSTEM scheduling requires a machine-wide PowerShell 7 installation. User MSIX
  registration alone is insufficient for the scheduled execution contract.

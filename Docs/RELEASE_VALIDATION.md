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

## Post-release verification, 2026-09-08

Release [v2026090703](https://github.com/VBlackJack/WinForge/releases/tag/v2026090703)
is published. PR #32 is merged; main and the tag resolve to
`84cbc5d1d78698a750132499afd3c1033d49940d`. Main CI 34156567128 and tag CI
34156586036 succeeded. The final CI result is 2011 Pester passes and 1 skip;
the local counts above describe the earlier local run.

The local 2026-09-08 acceptance closes the 40-case visual matrix, qualifies the
recorded NVDA speech workflows and closes a first catalog lot of 21 trials.
The corrections belong to an unpublished candidate. Coverage of all 195
applications remains partial; the boundaries below do not promise new features.

The [2026-09-08 acceptance evidence](Validation/20260908/README.md) records the
subsequent real catalog lot, spoken NVDA output, local corrections and their
validation. It distinguishes the published package from the unpublished candidate.

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

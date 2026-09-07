# Deployment preparation and recovery

[Français](DEPLOYMENT_WORKBENCH.fr.md)

The Applications page offers **Preview plan** and **Execution history**. A preview
detects current installations and records the selected catalog definitions without
installing packages. It lists the proposed action, detected version, source order,
available evidence, possible elevation or reboot, and supported rollback behavior.
Windows feature detection may require elevation. Detection failures stop the preview
instead of being interpreted as missing software.

Execution history opens a GUI batch receipt from `%LocalAppData%\WinForge\state`.
It shows outcomes and recorded version observations, then offers to retry only
failed, skipped or unattempted items. New installation batches preserve catalog
definitions so recovery survives subsequent catalog changes. Old receipts without
definitions require the application to remain in the catalog. Incomplete and
unreadable receipts are retained for recovery; completed receipts remain subject
to the existing retention period.

## PowerShell workbench

From the repository root, use a new destination for every plan:

```powershell
.\Tools\Deployment-Workbench.ps1 -Action Plan -ProfileName Base -Path .\base-plan.json
.\Tools\Deployment-Workbench.ps1 -Action Compare -ReferencePath .\base-plan.json -Path .\new-plan.json
.\Tools\Deployment-Workbench.ps1 -Action Apply -Path .\base-plan.json -WhatIf
.\Tools\Deployment-Workbench.ps1 -Action Apply -Path .\base-plan.json
.\Tools\Deployment-Workbench.ps1 -Action History -Path .\base-plan.json
.\Tools\Deployment-Workbench.ps1 -Action Retry -Path .\base-plan.json
.\Tools\Deployment-Workbench.ps1 -Action Rollback -Path .\base-plan.json
.\Tools\Deployment-Workbench.ps1 -Action Compliance -Path .\base-plan.json
```

Apply and rollback change installed packages and request confirmation. The plan
must be executed on its original machine and account. An exclusive lease prevents
two processes from applying the same receipt concurrently. Each attempt is saved
atomically before installation and after its outcome. A killed worker leaves an
interrupted entry; retry reconciles the current installation before proceeding.
An installation discovered after interruption is not claimed as rollback-owned.

Rollback removes only new Winget or Chocolatey packages identified in that plan's
successful receipts. Existing software is preserved. Unsupported installation
methods require manual recovery. A failed uninstall retains its receipt for retry.
Rollback does not restore old versions or reverse Windows configuration changes.
GUI batch receipts and workbench plans have different schemas: open each through
its corresponding interface.

## Reproducibility and evidence

Plans capture inherited application origins, resolved definitions, profile version
and system configuration. Comparison reports definition, observed version and
configuration changes. The workbench applies packages only; captured system
configuration is for comparison. Compliance compares current detection with the
observed baseline and reports unknown versions explicitly.

Remote package versions remain floating unless an application definition includes:

```json
"SourceLock": {
  "Method": "Winget",
  "Identifier": "7zip.7zip",
  "Version": "24.09"
}
```

Winget and Chocolatey locks require an exact matching source identifier and disable
fallback. They cannot be combined with custom installation methods. An already
installed, different or unknown version requires reconciliation; it is not silently
upgraded or downgraded. A lock cannot guarantee that a publisher keeps an old package
available or that external installers remain byte-identical.

`SourceEvidence` is specific to a source identifier. Package discovery, download
verification and installation testing are separate dated observations. Missing
observations display **Not measured**. The historical `Verified` flag is not
installation evidence. Changing a source identifier invalidates its old evidence.
An explicitly unavailable source remains distinguishable from an untested source.

## Disposable guest acceptance

`Tools/Test-GuestAcceptance.ps1` runs only elevated inside a VMware guest whose name
exactly matches `ExpectedComputerName`. Start from a clean checkpoint without 7-Zip
and with Telnet Client disabled. The harness installs a specified older 7-Zip
version, exercises update and partial rollback, and verifies a real scheduled task
running as SYSTEM. It leaves Telnet enabled as the unsupported rollback case.

```powershell
.\Tools\Test-GuestAcceptance.ps1 -ExpectedComputerName WINFORGE-LAB `
    -ReportPath C:\WinForgeReports\run-01\acceptance.json
```

Run this only in the disposable guest. The script does not create or restore the
VM checkpoint. The supplied Windows installation media determines the baseline;
results from modified media do not establish behavior on clean Microsoft media.
`Tests/DeploymentExecution.Tests.ps1` separately kills and restarts a real worker
against inert installers, proving receipt recovery without modifying host software.

## Limited WinGet configuration prototype

`Tools/Test-WinGetConfiguration.ps1` executes `winget configure test` against a
reviewed configuration and records the configuration hash, native exit code and
output. It never applies configuration. WinGet may download resource modules and
execute their Test implementations. Review resource publishers first. This is an
opt-in prototype, not a replacement for the deployment engine.

## Release validation and runtime requirements

Scheduled SYSTEM deployments require the machine-wide PowerShell 7 MSI installation.
A user MSIX installation is not a substitute for this prerequisite. Scheduled tasks
use the absolute executable path and fail explicitly when it is unavailable. Native
commands with timeouts also support Windows PowerShell 5.1, including quoted arguments.

The disposable Windows 11 Pro acceptance run on 2026-09-07 passed installation of
7-Zip 24.09, upgrade to independently observed 26.02.00.0, partial rollback, and
execution of a protected profile snapshot under SYSTEM. The scheduled task returned
exit code 0. The test removes 7-Zip; the unsupported Telnet feature is retained by
rollback and can then be disabled during laboratory cleanup. This is evidence for
these scenarios and this package, not certification of the entire catalog.

Use `Get-ApplicationsInstallationStatus -Refresh` when comparing state immediately
after a package change. A cached version must not be used as update evidence.
`Tools/Test-WinGetConfiguration.ps1` requires WinGet configuration features to be
enabled explicitly with `winget configure --enable`; it only runs resource tests.

GUI history and CLI plans remain separate formats. Restore of old application
versions, system configuration rollback and exhaustive screen-reader/DPI acceptance
are outside the verified package rollback contract. Process interruption/retry is
covered with disposable installer fixtures; arbitrary third-party installers may
leave partial system changes that require their own repair procedures.

Release completion validation: the actual deployment worker was interrupted while its child winget.exe was running. Resume completed the second item with two attempts while the first successful Windows feature retained one attempt. Evidence: real-interruption.json in the private acceptance results. This does not imply transactional rollback of arbitrary third-party installer internals.

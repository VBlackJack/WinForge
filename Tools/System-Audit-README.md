# System Audit

[Français](System-Audit-README.fr.md)

`Tools/System-Audit.ps1` observes system activity during a deployment, maintenance operation, or timed measurement. It collects performance samples, process changes, application changes, events, and network information. It does not start the operation being observed.

## Start and stop conditions

```powershell
.\Tools\System-Audit.ps1 -MonitorProcessId 1234 -AuditName 'ProcessAudit' -GenerateReport
.\Tools\System-Audit.ps1 -MonitorProcessName 'msiexec' -GenerateReport
.\Tools\System-Audit.ps1 -MonitorLogFile 'C:\Logs\install.log' -LogCompletionMarkers 'SUCCESS|COMPLETED' -GenerateReport
.\Tools\System-Audit.ps1 -MonitorLogPath '.\Logs' -GenerateReport
.\Tools\System-Audit.ps1 -Duration 30 -SampleInterval 5 -GenerateReport
```

Replace the example PID and paths with the observed operation. Process-name monitoring waits for a matching process and follows the selected instance; it is not a transaction spanning every process with that name. Log monitoring stops on a completion marker or the configured inactivity period. A marker or inactivity is an observation, not proof that an installation succeeded.

`-Duration 0` removes the duration limit. Stop a manual session with Ctrl+C. `Launch-SystemAudit.bat` provides an interactive launcher.

## Parameters

| Parameter | Default | Meaning |
|---|---|---|
| `Duration` | 60 | Maximum minutes; 0 means unlimited |
| `OutputPath` | `./AuditReports` | Report directory |
| `SampleInterval` | 5 | Seconds between samples |
| `GenerateReport` | Off | Write HTML in addition to JSON |
| `RealTimeDisplay` | On unless Quiet | Console status |
| `Quiet` | Off | Suppress live display |
| `SkipApplicationMonitoring` | Off | Omit application-inventory comparisons |
| `AuditName` | `SystemAudit` | Report name prefix |
| `MonitorProcessName`, `MonitorProcessId` | Unset | Process to observe |
| `MonitorLogFile`, `MonitorLogPath` | Unset | Log file or directory to observe |
| `LogCompletionMarkers` | `completed\|finished\|Summary` | Completion regular expression |
| `LogInactivityMinutes` | 2 | Inactivity stop threshold |
| `MonitorRegistry` | Off | Observe selected registry locations |

`MonitorFileSystem` is accepted for compatibility but has no filesystem-change collector in the current script. Do not treat its presence as evidence of file auditing.

## Data and reports

JSON reports contain timestamps, system information, performance samples and alerts, process changes, application changes, events, and anomalies. With `-GenerateReport`, an HTML summary is written beside the JSON as `{AuditName}_{timestamp}.html`.

Registry monitoring covers selected startup and uninstall locations, not every registry key. Application inventory and event collection are sampled; short-lived changes can be missed. Some event logs require administrator access. CPU, memory, and disk observations describe system activity, which may include unrelated processes.

## Operational use

Choose a distinctive audit name, start monitoring before the operation, and keep the operation's own result and logs alongside the report. Increase `SampleInterval` for long observations to reduce collection overhead. Use a specific completion marker to avoid matching an unrelated log line.

For performance comparisons, keep duration, sample interval, host configuration, and workload comparable. For multiple independent processes, start separate audit sessions with distinct names.

## Troubleshooting

- No process found: verify the process name or PID and whether it has started.
- No automatic stop: test the regular expression against the actual log and review the inactivity threshold.
- Missing events: verify access to the requested Windows event logs.
- Missing HTML: include `-GenerateReport` and inspect `OutputPath`.
- Script blocked by policy: inspect the active execution policy and use your organization's approved script-signing or execution procedure.

```powershell
Get-Help .\Tools\System-Audit.ps1 -Full
Get-Help .\Tools\System-Audit.ps1 -Examples
```

See the [tools index](README.md).

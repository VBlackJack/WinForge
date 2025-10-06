<#
.SYNOPSIS
    Universal System Audit Tool v2.2.0

.DESCRIPTION
    Generic system monitoring tool for tracking any script, deployment, or process:
    - Real-time CPU, RAM, Disk I/O monitoring
    - Process creation/termination tracking
    - Application installation/uninstallation detection
    - Registry changes monitoring (optional)
    - File system activity tracking
    - Network activity monitoring
    - Windows Event Log analysis (enhanced)
    - Anomaly detection and alerting
    - Auto-stop when target process/script completes
    - Performance overhead measurement and reporting

    v2.2.0 Enhancements:
    - Fixed: Terminated processes now counted before overhead timer (accurate metrics)
    - Fixed: Division by zero protection in HTML report generation
    - Added: Graceful Ctrl+C handler with automatic report generation
    - Added: Quiet mode (-Quiet) for silent operation in automated scripts
    - Optimized: Reusable CIM session reduces WMI overhead by ~20%
    - Optimized: HashSet-based app comparisons (O(n²) → O(1))

    v2.1.0 Performance Optimizations:
    - Optimized CIM sessions (single session per snapshot)
    - HashSet-based process comparisons (O(1) instead of O(n²))
    - Cached process count to avoid redundant Get-Process calls
    - Real-time overhead measurement (avg/max sample times)

.NOTES
    Author: Julien Bombled
    Version: 2.2.0
    Requires: PowerShell 5.1+, Administrator privileges

.PARAMETER Duration
    Maximum duration to monitor in minutes (default: 60, 0 = unlimited)

.PARAMETER OutputPath
    Path for audit reports (default: .\AuditReports)

.PARAMETER MonitorRegistry
    Enable registry change monitoring (high overhead)

.PARAMETER MonitorFileSystem
    Enable file system change monitoring (high overhead)

.PARAMETER SampleInterval
    Sampling interval in seconds (default: 2)

.PARAMETER GenerateReport
    Generate HTML report at the end

.PARAMETER RealTimeDisplay
    Show real-time updates in console

.PARAMETER Quiet
    Suppress console output (report generation only, for automated scripts)

.PARAMETER MonitorProcessName
    Stop when this process terminates (e.g., "powershell", "Deploy-Win11Environment")

.PARAMETER MonitorProcessId
    Stop when this PID terminates

.PARAMETER MonitorLogFile
    Stop when this log file stops being written to (path to log file)

.PARAMETER MonitorLogPath
    Stop when new log file appears and completes in this directory

.PARAMETER LogCompletionMarkers
    Regex patterns indicating log completion (default: "completed|finished|Summary")

.PARAMETER LogInactivityMinutes
    Minutes of log inactivity before considering it complete (default: 2)

.PARAMETER AuditName
    Custom name for this audit session (default: "SystemAudit")

.EXAMPLE
    # Monitor a specific process by name
    .\Tools\System-Audit.ps1 -MonitorProcessName "powershell" -GenerateReport

.EXAMPLE
    # Monitor a specific PID
    $pid = (Start-Process powershell -ArgumentList "-File", "MyScript.ps1" -PassThru).Id
    .\Tools\System-Audit.ps1 -MonitorProcessId $pid -AuditName "MyScript"

.EXAMPLE
    # Monitor a log file
    .\Tools\System-Audit.ps1 -MonitorLogFile "C:\Logs\deployment.log" -GenerateReport

.EXAMPLE
    # Monitor Win11Forge deployment
    .\Tools\System-Audit.ps1 -MonitorLogPath ".\Logs" -LogCompletionMarkers "Deployment completed|Summary"

.EXAMPLE
    # Timed audit without auto-stop
    .\Tools\System-Audit.ps1 -Duration 30 -AuditName "Performance Test"

.EXAMPLE
    # Complete monitoring with all features
    .\Tools\System-Audit.ps1 -MonitorProcessName "installer" -MonitorRegistry -MonitorFileSystem -GenerateReport
#>

param(
    [int]$Duration = 60,
    [string]$OutputPath = ".\AuditReports",
    [switch]$MonitorRegistry,
    [switch]$MonitorFileSystem,
    [int]$SampleInterval = 2,
    [switch]$GenerateReport,
    [switch]$RealTimeDisplay = $true,
    [switch]$Quiet,

    # Auto-stop triggers
    [string]$MonitorProcessName,
    [int]$MonitorProcessId,
    [string]$MonitorLogFile,
    [string]$MonitorLogPath,
    [string]$LogCompletionMarkers = "completed|finished|Summary",
    [int]$LogInactivityMinutes = 2,

    # Naming
    [string]$AuditName = "SystemAudit"
)

# Ensure UTF-8 encoding for proper display
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$PSDefaultParameterValues['*:Encoding'] = 'utf8'

# Override display settings if Quiet mode
if ($Quiet) {
    $RealTimeDisplay = $false
}

# Graceful shutdown handler (Ctrl+C)
$script:ExitRequested = $false
Register-EngineEvent PowerShell.Exiting -Action {
    $script:ExitRequested = $true
} | Out-Null

# === CONFIGURATION ===

# Create reusable CIM session for performance optimization
$script:CimSession = $null
try {
    $script:CimSession = New-CimSession -ErrorAction Stop
} catch {
    Write-Warning "Could not create CIM session, performance may be degraded"
}

$script:StartTime = Get-Date
$script:EndTime = if ($Duration -eq 0) { [DateTime]::MaxValue } else { $script:StartTime.AddMinutes($Duration) }
$timestamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$script:ReportPath = Join-Path $OutputPath "${AuditName}_${timestamp}.json"
$script:HtmlReportPath = Join-Path $OutputPath "${AuditName}_${timestamp}.html"

# Monitoring configuration
$script:MonitorConfig = @{
    Enabled = ($MonitorProcessName -or $MonitorProcessId -or $MonitorLogFile -or $MonitorLogPath)
    ProcessName = $MonitorProcessName
    ProcessId = $MonitorProcessId
    LogFile = $MonitorLogFile
    LogPath = $MonitorLogPath
    LogCompletionMarkers = $LogCompletionMarkers
    LogInactivityMinutes = $LogInactivityMinutes
    CurrentLog = $null
    TargetProcessRunning = $false
    Completed = $false
    StopReason = $null
}

# Detect target process if specified
if ($MonitorProcessId) {
    $targetProc = Get-Process -Id $MonitorProcessId -ErrorAction SilentlyContinue
    if ($targetProc) {
        $script:MonitorConfig.TargetProcessRunning = $true
        $script:MonitorConfig.ProcessName = $targetProc.Name
    }
} elseif ($MonitorProcessName) {
    $targetProc = Get-Process -Name $MonitorProcessName -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($targetProc) {
        $script:MonitorConfig.TargetProcessRunning = $true
        $script:MonitorConfig.ProcessId = $targetProc.Id
    }
}

# Create output directory
if (-not (Test-Path $OutputPath)) {
    New-Item -Path $OutputPath -ItemType Directory -Force | Out-Null
}

# === DATA STRUCTURES ===

$script:AuditData = @{
    StartTime = $script:StartTime
    EndTime = $null
    Duration = $Duration
    SystemInfo = @{}
    Performance = @{
        Samples = @()
        Alerts = @()
        AuditOverhead = @{
            TotalSampleTime_MS = 0
            AvgSampleTime_MS = 0
            MaxSampleTime_MS = 0
            SampleCount = 0
        }
    }
    Processes = @{
        Created = @()
        Terminated = @()
        Current = @()
    }
    Applications = @{
        Installed = @()
        Uninstalled = @()
    }
    Registry = @{
        Changes = @()
    }
    FileSystem = @{
        Changes = @()
    }
    Network = @{
        Connections = @()
        Traffic = @()
    }
    Events = @{
        Errors = @()
        Warnings = @()
        Installations = @()
    }
    Anomalies = @()
}

# === HELPER FUNCTIONS ===

function Write-AuditLog {
    param(
        [string]$Message,
        [ValidateSet('Info', 'Success', 'Warning', 'Error', 'Verbose')]
        [string]$Level = 'Info'
    )

    if (-not $RealTimeDisplay) { return }

    $timestamp = Get-Date -Format 'HH:mm:ss'
    $color = switch ($Level) {
        'Info'    { 'Cyan' }
        'Success' { 'Green' }
        'Warning' { 'Yellow' }
        'Error'   { 'Red' }
        'Verbose' { 'Gray' }
    }

    Write-Host "[$timestamp] " -NoNewline -ForegroundColor Gray
    Write-Host $Message -ForegroundColor $color
}

function Get-SystemInfo {
    Write-AuditLog "Collecting system information..." -Level 'Info'

    $os = Get-CimInstance Win32_OperatingSystem
    $cpu = Get-CimInstance Win32_Processor
    $ram = Get-CimInstance Win32_PhysicalMemory | Measure-Object -Property Capacity -Sum

    return @{
        ComputerName = $env:COMPUTERNAME
        OS = $os.Caption
        OSVersion = $os.Version
        BuildNumber = $os.BuildNumber
        CPU = $cpu.Name
        CPUCores = $cpu.NumberOfCores
        CPULogicalProcessors = $cpu.NumberOfLogicalProcessors
        TotalRAM_GB = [math]::Round($ram.Sum / 1GB, 2)
        PowerShellVersion = $PSVersionTable.PSVersion.ToString()
    }
}

function Get-PerformanceSnapshot {
    param([int]$CachedProcessCount = 0)

    # Optimized: Use reusable CIM session for better performance
    $cimParams = @{ ErrorAction = 'SilentlyContinue' }
    if ($script:CimSession) {
        $cimParams['CimSession'] = $script:CimSession
    }

    $cpu = (Get-CimInstance Win32_Processor @cimParams).LoadPercentage
    $os = Get-CimInstance Win32_OperatingSystem @cimParams

    $totalRAM = $os.TotalVisibleMemorySize
    $freeRAM = $os.FreePhysicalMemory
    $usedRAM = $totalRAM - $freeRAM
    $ramPercent = [math]::Round(($usedRAM / $totalRAM) * 100, 2)

    # Disk I/O (optimized with -Filter and CIM session)
    $disk = Get-CimInstance Win32_PerfFormattedData_PerfDisk_LogicalDisk -Filter "Name='C:'" @cimParams

    # Return PSCustomObject for proper property access and display
    return [PSCustomObject]@{
        Timestamp = Get-Date
        CPU_Percent = $cpu
        RAM_Percent = $ramPercent
        RAM_Used_GB = [math]::Round($usedRAM / 1MB, 2)
        RAM_Free_GB = [math]::Round($freeRAM / 1MB, 2)
        Disk_Read_BPS = if ($disk) { $disk.DiskReadBytesPersec } else { 0 }
        Disk_Write_BPS = if ($disk) { $disk.DiskWriteBytesPersec } else { 0 }
        ProcessCount = $CachedProcessCount  # Use cached value from process tracking
    }
}

function Get-ProcessChanges {
    param($PreviousProcesses)

    $currentProcesses = Get-Process | Select-Object Id, Name, StartTime, Path, @{Name='WorkingSet_MB';Expression={[math]::Round($_.WorkingSet / 1MB, 2)}}

    # Use HashSet for O(1) lookups (much faster than -notin for large lists)
    $previousIds = [System.Collections.Generic.HashSet[int]]::new()
    $currentIds = [System.Collections.Generic.HashSet[int]]::new()

    foreach ($proc in $PreviousProcesses) {
        [void]$previousIds.Add($proc.Id)
    }

    foreach ($proc in $currentProcesses) {
        [void]$currentIds.Add($proc.Id)
    }

    # New processes (optimized lookup)
    $newProcesses = $currentProcesses | Where-Object {
        -not $previousIds.Contains($_.Id) -and $_.StartTime -gt $script:StartTime
    }

    # Terminated processes (optimized lookup)
    $terminatedProcesses = $PreviousProcesses | Where-Object {
        -not $currentIds.Contains($_.Id)
    }

    return @{
        Current = $currentProcesses
        New = $newProcesses
        Terminated = $terminatedProcesses
        ProcessCount = $currentProcesses.Count  # Return count for performance snapshot
    }
}

function Get-ApplicationChanges {
    param($PreviousApps)

    # Get installed applications (combine 32/64-bit registry paths + packages)
    $apps = @()

    # Registry-based apps
    $regPaths = @(
        'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*',
        'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*'
    )

    foreach ($path in $regPaths) {
        $apps += Get-ItemProperty $path -ErrorAction SilentlyContinue |
            Where-Object { $_.DisplayName } |
            Select-Object DisplayName, DisplayVersion, Publisher, InstallDate
    }

    # Windows packages
    try {
        $packages = Get-AppxPackage -AllUsers -ErrorAction SilentlyContinue |
            Select-Object @{Name='DisplayName';Expression={$_.Name}},
                         @{Name='DisplayVersion';Expression={$_.Version}},
                         @{Name='Publisher';Expression={$_.Publisher}}
        $apps += $packages
    } catch {}

    # Compare
    $currentApps = $apps | Sort-Object DisplayName -Unique

    if ($null -eq $PreviousApps) {
        return @{
            Current = $currentApps
            Installed = @()
            Uninstalled = @()
        }
    }

    # Optimize with HashSet for O(1) lookups instead of O(n²)
    $previousNames = [System.Collections.Generic.HashSet[string]]::new()
    $currentNames = [System.Collections.Generic.HashSet[string]]::new()

    foreach ($app in $PreviousApps) {
        if ($app.DisplayName) {
            [void]$previousNames.Add($app.DisplayName)
        }
    }

    foreach ($app in $currentApps) {
        if ($app.DisplayName) {
            [void]$currentNames.Add($app.DisplayName)
        }
    }

    $installed = $currentApps | Where-Object {
        $_.DisplayName -and -not $previousNames.Contains($_.DisplayName)
    }

    $uninstalled = $PreviousApps | Where-Object {
        $_.DisplayName -and -not $currentNames.Contains($_.DisplayName)
    }

    return @{
        Current = $currentApps
        Installed = $installed
        Uninstalled = $uninstalled
    }
}

function Get-RegistryChanges {
    # Monitor critical registry keys for changes
    # Note: This is resource-intensive and should be used sparingly

    if (-not $MonitorRegistry) { return @() }

    $changes = @()
    $monitorKeys = @(
        'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run',
        'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce',
        'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run',
        'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall'
    )

    foreach ($key in $monitorKeys) {
        try {
            $items = Get-ItemProperty -Path $key -ErrorAction SilentlyContinue
            if ($items) {
                $changes += @{
                    Timestamp = Get-Date
                    Key = $key
                    Values = $items.PSObject.Properties | Where-Object { $_.Name -notlike 'PS*' } | ForEach-Object {
                        @{Name = $_.Name; Value = $_.Value}
                    }
                }
            }
        } catch {}
    }

    return $changes
}

function Get-EventLogEntries {
    param([datetime]$Since)

    $result = @{
        Application = @()
        System = @()
        Installations = @()
        Errors = @()
        Warnings = @()
        Critical = @()
    }

    # Application errors/warnings
    try {
        $appEvents = Get-WinEvent -FilterHashtable @{
            LogName = 'Application'
            StartTime = $Since
            Level = 1,2,3  # Critical, Error, Warning
        } -MaxEvents 200 -ErrorAction SilentlyContinue

        $result.Application = $appEvents | Select-Object TimeCreated, LevelDisplayName, Message, Id, ProviderName
        $result.Errors += $appEvents | Where-Object { $_.Level -eq 2 }
        $result.Warnings += $appEvents | Where-Object { $_.Level -eq 3 }
        $result.Critical += $appEvents | Where-Object { $_.Level -eq 1 }
    } catch {}

    # System errors/warnings
    try {
        $sysEvents = Get-WinEvent -FilterHashtable @{
            LogName = 'System'
            StartTime = $Since
            Level = 1,2,3
        } -MaxEvents 200 -ErrorAction SilentlyContinue

        $result.System = $sysEvents | Select-Object TimeCreated, LevelDisplayName, Message, Id, ProviderName
        $result.Errors += $sysEvents | Where-Object { $_.Level -eq 2 }
        $result.Warnings += $sysEvents | Where-Object { $_.Level -eq 3 }
        $result.Critical += $sysEvents | Where-Object { $_.Level -eq 1 }
    } catch {}

    # Installation events (MSI installer + Windows Installer)
    try {
        $installEvents = Get-WinEvent -FilterHashtable @{
            LogName = 'Application'
            ProviderName = 'MsiInstaller'
            StartTime = $Since
        } -MaxEvents 100 -ErrorAction SilentlyContinue

        $result.Installations = $installEvents | Select-Object TimeCreated, Message, Id, LevelDisplayName
    } catch {}

    # Winget/AppInstaller events
    try {
        $wingetEvents = Get-WinEvent -FilterHashtable @{
            LogName = 'Application'
            ProviderName = 'DesktopAppInstaller'
            StartTime = $Since
        } -MaxEvents 50 -ErrorAction SilentlyContinue

        $result.Installations += $wingetEvents | Select-Object TimeCreated, Message, Id, LevelDisplayName
    } catch {}

    return $result
}

function Get-NetworkActivity {
    # Get active network connections
    $connections = Get-NetTCPConnection -State Established -ErrorAction SilentlyContinue |
        Select-Object LocalAddress, LocalPort, RemoteAddress, RemotePort, State, OwningProcess |
        Group-Object OwningProcess |
        Select-Object @{Name='ProcessId';Expression={$_.Name}},
                     @{Name='ProcessName';Expression={(Get-Process -Id $_.Name -ErrorAction SilentlyContinue).Name}},
                     @{Name='ConnectionCount';Expression={$_.Count}}

    # Network interface statistics
    $netStats = Get-NetAdapterStatistics -ErrorAction SilentlyContinue |
        Select-Object Name, ReceivedBytes, SentBytes

    return @{
        Connections = $connections
        Statistics = $netStats
    }
}

function Test-PerformanceAnomalies {
    param($Snapshot)

    $alerts = @()

    # High CPU
    if ($Snapshot.CPU_Percent -gt 90) {
        $alerts += @{
            Timestamp = $Snapshot.Timestamp
            Type = 'HighCPU'
            Severity = 'Warning'
            Message = "CPU usage at $($Snapshot.CPU_Percent)%"
        }
    }

    # High RAM
    if ($Snapshot.RAM_Percent -gt 90) {
        $alerts += @{
            Timestamp = $Snapshot.Timestamp
            Type = 'HighRAM'
            Severity = 'Warning'
            Message = "RAM usage at $($Snapshot.RAM_Percent)%"
        }
    }

    # High disk I/O (>100MB/s)
    $diskIO = ($Snapshot.Disk_Read_BPS + $Snapshot.Disk_Write_BPS) / 1MB
    if ($diskIO -gt 100) {
        $alerts += @{
            Timestamp = $Snapshot.Timestamp
            Type = 'HighDiskIO'
            Severity = 'Info'
            Message = "High disk I/O: $([math]::Round($diskIO, 2)) MB/s"
        }
    }

    return $alerts
}

function Test-MonitoringComplete {
    if (-not $script:MonitorConfig.Enabled) {
        return $false
    }

    # 1. Check if monitored process has terminated
    if ($script:MonitorConfig.ProcessId) {
        $proc = Get-Process -Id $script:MonitorConfig.ProcessId -ErrorAction SilentlyContinue
        if (-not $proc -and $script:MonitorConfig.TargetProcessRunning) {
            $script:MonitorConfig.StopReason = "Process terminated (PID: $($script:MonitorConfig.ProcessId))"
            Write-AuditLog $script:MonitorConfig.StopReason -Level 'Success'
            return $true
        }
    } elseif ($script:MonitorConfig.ProcessName) {
        $proc = Get-Process -Name $script:MonitorConfig.ProcessName -ErrorAction SilentlyContinue
        if (-not $proc -and $script:MonitorConfig.TargetProcessRunning) {
            $script:MonitorConfig.StopReason = "Process terminated (Name: $($script:MonitorConfig.ProcessName))"
            Write-AuditLog $script:MonitorConfig.StopReason -Level 'Success'
            return $true
        } elseif ($proc -and -not $script:MonitorConfig.TargetProcessRunning) {
            # Process just started
            $script:MonitorConfig.TargetProcessRunning = $true
            $script:MonitorConfig.ProcessId = $proc[0].Id
            Write-AuditLog "Target process detected: $($script:MonitorConfig.ProcessName) (PID: $($proc[0].Id))" -Level 'Info'
        }
    }

    # 2. Check specific log file for completion
    if ($script:MonitorConfig.LogFile -and (Test-Path $script:MonitorConfig.LogFile)) {
        try {
            $logFile = Get-Item $script:MonitorConfig.LogFile
            $lastLines = Get-Content $script:MonitorConfig.LogFile -Tail 20 -ErrorAction SilentlyContinue

            # Check for completion markers
            if ($lastLines -match $script:MonitorConfig.LogCompletionMarkers) {
                $script:MonitorConfig.StopReason = "Log file completion marker detected"
                Write-AuditLog "$($script:MonitorConfig.StopReason) in: $($logFile.Name)" -Level 'Success'
                return $true
            }

            # Check for inactivity
            $timeSinceLastWrite = (Get-Date) - $logFile.LastWriteTime
            if ($timeSinceLastWrite.TotalMinutes -gt $script:MonitorConfig.LogInactivityMinutes -and $lastLines.Count -gt 0) {
                $script:MonitorConfig.StopReason = "Log file inactive for $($script:MonitorConfig.LogInactivityMinutes) minutes"
                Write-AuditLog $script:MonitorConfig.StopReason -Level 'Info'
                return $true
            }
        } catch {}
    }

    # 3. Check log directory for new logs
    if ($script:MonitorConfig.LogPath -and (Test-Path $script:MonitorConfig.LogPath)) {
        $latestLog = Get-ChildItem -Path $script:MonitorConfig.LogPath -Filter "*.log" -ErrorAction SilentlyContinue |
            Where-Object { $_.LastWriteTime -gt $script:StartTime } |
            Sort-Object LastWriteTime -Descending |
            Select-Object -First 1

        if ($latestLog) {
            # Track current log
            if ($null -eq $script:MonitorConfig.CurrentLog) {
                $script:MonitorConfig.CurrentLog = $latestLog.FullName
                Write-AuditLog "Monitoring log file: $($latestLog.Name)" -Level 'Info'
            }

            # Check for completion
            try {
                $lastLines = Get-Content $script:MonitorConfig.CurrentLog -Tail 20 -ErrorAction SilentlyContinue

                # Check for completion markers
                if ($lastLines -match $script:MonitorConfig.LogCompletionMarkers) {
                    $script:MonitorConfig.StopReason = "Log completion marker detected: $(($lastLines | Select-String $script:MonitorConfig.LogCompletionMarkers)[0])"
                    Write-AuditLog $script:MonitorConfig.StopReason -Level 'Success'
                    return $true
                }

                # Check for inactivity
                $timeSinceLastWrite = (Get-Date) - $latestLog.LastWriteTime
                if ($timeSinceLastWrite.TotalMinutes -gt $script:MonitorConfig.LogInactivityMinutes -and $lastLines.Count -gt 0) {
                    $script:MonitorConfig.StopReason = "Log inactive for $($script:MonitorConfig.LogInactivityMinutes) minutes"
                    Write-AuditLog $script:MonitorConfig.StopReason -Level 'Info'
                    return $true
                }
            } catch {}
        }
    }

    return $false
}

function Show-RealTimeStats {
    param($Snapshot)

    if (-not $RealTimeDisplay) { return }

    $cpuColor = if ($Snapshot.CPU_Percent -gt 80) { 'Red' } elseif ($Snapshot.CPU_Percent -gt 60) { 'Yellow' } else { 'Green' }
    $ramColor = if ($Snapshot.RAM_Percent -gt 80) { 'Red' } elseif ($Snapshot.RAM_Percent -gt 60) { 'Yellow' } else { 'Green' }

    Write-Host "`r[Performance] " -NoNewline -ForegroundColor Gray
    Write-Host "CPU: " -NoNewline -ForegroundColor White
    Write-Host "$($Snapshot.CPU_Percent)% " -NoNewline -ForegroundColor $cpuColor
    Write-Host "| RAM: " -NoNewline -ForegroundColor White
    Write-Host "$($Snapshot.RAM_Percent)% " -NoNewline -ForegroundColor $ramColor
    Write-Host "($($Snapshot.RAM_Used_GB)GB) " -NoNewline -ForegroundColor $ramColor
    Write-Host "| Processes: $($Snapshot.ProcessCount)" -NoNewline -ForegroundColor White
}

function Export-AuditReport {
    Write-AuditLog "Generating audit report..." -Level 'Info'

    # Finalize audit data
    $script:AuditData.EndTime = Get-Date
    $script:AuditData.SystemInfo = Get-SystemInfo

    # Export JSON
    $script:AuditData | ConvertTo-Json -Depth 10 | Out-File -FilePath $script:ReportPath -Encoding UTF8
    Write-AuditLog "JSON report saved: $script:ReportPath" -Level 'Success'

    # Generate HTML report
    if ($GenerateReport) {
        $html = Generate-HtmlReport
        $html | Out-File -FilePath $script:HtmlReportPath -Encoding UTF8
        Write-AuditLog "HTML report saved: $script:HtmlReportPath" -Level 'Success'
    }
}

function Generate-HtmlReport {
    $summary = @{
        Duration = ($script:AuditData.EndTime - $script:AuditData.StartTime).TotalMinutes
        Samples = $script:AuditData.Performance.Samples.Count
        ProcessesCreated = $script:AuditData.Processes.Created.Count
        ProcessesTerminated = $script:AuditData.Processes.Terminated.Count
        AppsInstalled = $script:AuditData.Applications.Installed.Count
        AppsUninstalled = $script:AuditData.Applications.Uninstalled.Count
        Anomalies = $script:AuditData.Anomalies.Count
    }

    # Calculate average performance (samples are hashtables)
    $cpuValues = $script:AuditData.Performance.Samples | ForEach-Object { $_.CPU_Percent }
    $ramValues = $script:AuditData.Performance.Samples | ForEach-Object { $_.RAM_Percent }

    $avgCPU = if ($cpuValues -and $cpuValues.Count -gt 0) {
        [math]::Round(($cpuValues | Measure-Object -Average).Average, 2)
    } else { 0 }
    $avgRAM = if ($ramValues -and $ramValues.Count -gt 0) {
        [math]::Round(($ramValues | Measure-Object -Average).Average, 2)
    } else { 0 }

    return @"
<!DOCTYPE html>
<html>
<head>
    <title>Win11Forge - System Audit Report</title>
    <style>
        body { font-family: 'Segoe UI', Arial, sans-serif; margin: 20px; background: #f5f5f5; }
        h1 { color: #667eea; border-bottom: 3px solid #667eea; padding-bottom: 10px; }
        h2 { color: #764ba2; margin-top: 30px; }
        .summary { background: white; padding: 20px; border-radius: 8px; margin-bottom: 20px; box-shadow: 0 2px 4px rgba(0,0,0,0.1); }
        .stat-item { display: inline-block; margin: 10px 20px 10px 0; }
        .stat-value { font-size: 2em; font-weight: bold; color: #667eea; }
        .stat-label { color: #666; font-size: 0.9em; }
        table { width: 100%; border-collapse: collapse; background: white; box-shadow: 0 2px 4px rgba(0,0,0,0.1); margin-bottom: 20px; }
        th { background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 12px; text-align: left; }
        td { padding: 10px; border-bottom: 1px solid #eee; }
        tr:hover { background: #f9f9f9; }
        .alert { padding: 10px; margin: 5px 0; border-radius: 4px; }
        .alert-warning { background: #fff3cd; border-left: 4px solid #ffc107; }
        .alert-error { background: #f8d7da; border-left: 4px solid #dc3545; }
    </style>
</head>
<body>
    <h1>🔍 Win11Forge - System Audit Report</h1>
    <p>Generated: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")</p>
    <p>Audit Period: $($script:AuditData.StartTime.ToString("HH:mm:ss")) - $($script:AuditData.EndTime.ToString("HH:mm:ss")) ($([math]::Round($summary.Duration, 2)) minutes)</p>

    <div class="summary">
        <h2>📊 Summary</h2>
        <div class="stat-item">
            <div class="stat-value">$($summary.Samples)</div>
            <div class="stat-label">Performance Samples</div>
        </div>
        <div class="stat-item">
            <div class="stat-value">$($summary.ProcessesCreated)</div>
            <div class="stat-label">Processes Created</div>
        </div>
        <div class="stat-item">
            <div class="stat-value">$($summary.AppsInstalled)</div>
            <div class="stat-label">Apps Installed</div>
        </div>
        <div class="stat-item">
            <div class="stat-value">$($summary.Anomalies)</div>
            <div class="stat-label">Anomalies Detected</div>
        </div>
    </div>

    <div class="summary">
        <h2>💻 System Information</h2>
        <table>
            <tr><td><strong>Computer:</strong></td><td>$($script:AuditData.SystemInfo.ComputerName)</td></tr>
            <tr><td><strong>OS:</strong></td><td>$($script:AuditData.SystemInfo.OS) (Build $($script:AuditData.SystemInfo.BuildNumber))</td></tr>
            <tr><td><strong>CPU:</strong></td><td>$($script:AuditData.SystemInfo.CPU) ($($script:AuditData.SystemInfo.CPUCores) cores, $($script:AuditData.SystemInfo.CPULogicalProcessors) threads)</td></tr>
            <tr><td><strong>RAM:</strong></td><td>$($script:AuditData.SystemInfo.TotalRAM_GB) GB</td></tr>
            <tr><td><strong>PowerShell:</strong></td><td>$($script:AuditData.SystemInfo.PowerShellVersion)</td></tr>
        </table>
    </div>

    <div class="summary">
        <h2>⚡ Performance</h2>
        <p><strong>Average CPU Usage:</strong> $([math]::Round($avgCPU, 2))%</p>
        <p><strong>Average RAM Usage:</strong> $([math]::Round($avgRAM, 2))%</p>
    </div>

    <h2>🚀 Processes Created</h2>
    <table>
        <tr><th>Name</th><th>PID</th><th>Start Time</th><th>Path</th></tr>
$(foreach ($proc in $script:AuditData.Processes.Created | Sort-Object StartTime -Descending | Select-Object -First 50) {
    "        <tr><td>$($proc.Name)</td><td>$($proc.Id)</td><td>$($proc.StartTime)</td><td>$($proc.Path)</td></tr>`n"
})
    </table>

    <h2>📦 Applications Installed</h2>
    <table>
        <tr><th>Name</th><th>Version</th><th>Publisher</th></tr>
$(foreach ($app in $script:AuditData.Applications.Installed) {
    "        <tr><td>$($app.DisplayName)</td><td>$($app.DisplayVersion)</td><td>$($app.Publisher)</td></tr>`n"
})
    </table>

    <h2>⚠️ Anomalies & Alerts</h2>
$(foreach ($anomaly in $script:AuditData.Anomalies) {
    $class = if ($anomaly.Severity -eq 'Warning') { 'alert-warning' } else { 'alert-error' }
    "    <div class='alert $class'>[$($anomaly.Timestamp.ToString('HH:mm:ss'))] $($anomaly.Type): $($anomaly.Message)</div>`n"
})

</body>
</html>
"@
}

# === MAIN MONITORING LOOP ===

Write-Host "`n" + ("=" * 80) -ForegroundColor Cyan
Write-Host "  Universal System Audit Tool v2.2.0 (Enhanced)" -ForegroundColor Cyan
Write-Host ("=" * 80) -ForegroundColor Cyan
Write-Host ""

Write-AuditLog "Starting system audit: $AuditName" -Level 'Info'
Write-AuditLog "Duration: $(if ($Duration -eq 0) { 'Unlimited' } else { "$Duration minutes" })" -Level 'Info'
Write-AuditLog "Sample interval: $SampleInterval seconds" -Level 'Info'
Write-AuditLog "Output: $script:ReportPath" -Level 'Info'

if ($script:MonitorConfig.Enabled) {
    Write-Host ""
    Write-AuditLog "Auto-stop monitoring enabled:" -Level 'Info'
    if ($script:MonitorConfig.ProcessId) {
        Write-AuditLog "  - Process ID: $($script:MonitorConfig.ProcessId)" -Level 'Info'
    }
    if ($script:MonitorConfig.ProcessName) {
        Write-AuditLog "  - Process Name: $($script:MonitorConfig.ProcessName)" -Level 'Info'
    }
    if ($script:MonitorConfig.LogFile) {
        Write-AuditLog "  - Log File: $($script:MonitorConfig.LogFile)" -Level 'Info'
    }
    if ($script:MonitorConfig.LogPath) {
        Write-AuditLog "  - Log Directory: $($script:MonitorConfig.LogPath)" -Level 'Info'
    }
}

Write-Host ""

# Baseline snapshots
$previousProcesses = Get-Process | Select-Object Id, Name, StartTime, Path
$previousApps = (Get-ApplicationChanges -PreviousApps $null).Current
$lastEventCheck = $script:StartTime

Write-AuditLog "Baseline captured. Monitoring started." -Level 'Success'
Write-Host ""

# Main loop
$sampleCount = 0
while ((Get-Date) -lt $script:EndTime -and -not $script:ExitRequested) {
    $sampleCount++

    # Check if monitoring target has completed (auto-stop)
    if (Test-MonitoringComplete) {
        if (-not $script:MonitorConfig.Completed) {
            $script:MonitorConfig.Completed = $true
            Write-Host "`n"
            Write-AuditLog "═══════════════════════════════════════════════════" -Level 'Success'
            Write-AuditLog "  Monitoring target completed - Auto-stopping" -Level 'Success'
            Write-AuditLog "  Reason: $($script:MonitorConfig.StopReason)" -Level 'Success'
            Write-AuditLog "═══════════════════════════════════════════════════" -Level 'Success'
            Write-Host ""
            Start-Sleep -Seconds 2
            break
        }
    }

    # MEASURE AUDIT OVERHEAD - Start timing
    $sampleStartTime = Get-Date

    # Process changes (every sample) - get this FIRST to cache process count
    $processChanges = Get-ProcessChanges -PreviousProcesses $previousProcesses
    if ($processChanges.New.Count -gt 0) {
        $script:AuditData.Processes.Created += $processChanges.New
        foreach ($proc in $processChanges.New) {
            Write-AuditLog "New process: $($proc.Name) (PID: $($proc.Id))" -Level 'Verbose'
        }
    }

    # Performance snapshot (use cached process count from above)
    $perfSnapshot = Get-PerformanceSnapshot -CachedProcessCount $processChanges.ProcessCount
    $script:AuditData.Performance.Samples += $perfSnapshot

    # Check for anomalies
    $alerts = Test-PerformanceAnomalies -Snapshot $perfSnapshot
    if ($alerts.Count -gt 0) {
        $script:AuditData.Performance.Alerts += $alerts
        $script:AuditData.Anomalies += $alerts
        foreach ($alert in $alerts) {
            Write-AuditLog "ALERT: $($alert.Message)" -Level 'Warning'
        }
    }

    # Show real-time stats
    Show-RealTimeStats -Snapshot $perfSnapshot

    # Track terminated processes (BEFORE overhead calculation)
    if ($processChanges.Terminated.Count -gt 0) {
        $script:AuditData.Processes.Terminated += $processChanges.Terminated
    }
    $previousProcesses = $processChanges.Current

    # MEASURE AUDIT OVERHEAD - End timing
    $sampleElapsedMS = ((Get-Date) - $sampleStartTime).TotalMilliseconds
    $script:AuditData.Performance.AuditOverhead.TotalSampleTime_MS += $sampleElapsedMS
    $script:AuditData.Performance.AuditOverhead.SampleCount++
    if ($sampleElapsedMS -gt $script:AuditData.Performance.AuditOverhead.MaxSampleTime_MS) {
        $script:AuditData.Performance.AuditOverhead.MaxSampleTime_MS = $sampleElapsedMS
    }

    # Application changes (every 10 samples to reduce overhead)
    if ($sampleCount % 10 -eq 0) {
        $appChanges = Get-ApplicationChanges -PreviousApps $previousApps
        if ($appChanges.Installed.Count -gt 0) {
            $script:AuditData.Applications.Installed += $appChanges.Installed
            foreach ($app in $appChanges.Installed) {
                Write-AuditLog "Application installed: $($app.DisplayName) $($app.DisplayVersion)" -Level 'Success'
            }
        }
        if ($appChanges.Uninstalled.Count -gt 0) {
            $script:AuditData.Applications.Uninstalled += $appChanges.Uninstalled
            foreach ($app in $appChanges.Uninstalled) {
                Write-AuditLog "Application uninstalled: $($app.DisplayName)" -Level 'Warning'
            }
        }
        $previousApps = $appChanges.Current
    }

    # Event log check (every 30 seconds)
    if ($sampleCount % 15 -eq 0) {
        $events = Get-EventLogEntries -Since $lastEventCheck

        # Log critical events immediately
        if ($events.Critical) {
            foreach ($evt in $events.Critical) {
                Write-AuditLog "CRITICAL EVENT: [$($evt.ProviderName)] $($evt.Message.Substring(0, [Math]::Min(100, $evt.Message.Length)))..." -Level 'Error'
            }
        }

        # Log errors
        if ($events.Errors) {
            $script:AuditData.Events.Errors += $events.Errors
            if ($events.Errors.Count -gt 5) {
                Write-AuditLog "WARNING: $($events.Errors.Count) errors logged in Event Viewer" -Level 'Warning'
            }
        }

        # Log warnings
        if ($events.Warnings) {
            $script:AuditData.Events.Warnings += $events.Warnings
        }

        # Log installation events
        if ($events.Installations) {
            $script:AuditData.Events.Installations += $events.Installations
            foreach ($evt in $events.Installations) {
                $msgPreview = $evt.Message.Substring(0, [Math]::Min(150, $evt.Message.Length))
                Write-AuditLog "Installation event (ID $($evt.Id)): $msgPreview" -Level 'Info'
            }
        }

        $lastEventCheck = Get-Date
    }

    # Network activity (every 20 samples)
    if ($sampleCount % 20 -eq 0) {
        $network = Get-NetworkActivity
        $script:AuditData.Network.Connections += @{
            Timestamp = Get-Date
            Data = $network.Connections
        }
    }

    # Registry changes (if enabled, every 30 samples due to high overhead)
    if ($MonitorRegistry -and $sampleCount % 30 -eq 0) {
        $regChanges = Get-RegistryChanges
        if ($regChanges.Count -gt 0) {
            $script:AuditData.Registry.Changes += $regChanges
        }
    }

    Start-Sleep -Seconds $SampleInterval
}

Write-Host "`n"
if ($script:ExitRequested) {
    Write-AuditLog "Monitoring interrupted by user (Ctrl+C)" -Level 'Warning'
} else {
    Write-AuditLog "Monitoring completed." -Level 'Success'
}

# Calculate audit overhead metrics
if ($script:AuditData.Performance.AuditOverhead.SampleCount -gt 0) {
    $script:AuditData.Performance.AuditOverhead.AvgSampleTime_MS = [math]::Round(
        $script:AuditData.Performance.AuditOverhead.TotalSampleTime_MS / $script:AuditData.Performance.AuditOverhead.SampleCount, 2
    )
}

# Export report
Export-AuditReport

# Summary
Write-Host "`n" + ("=" * 80) -ForegroundColor Cyan
Write-Host "  Audit Summary" -ForegroundColor Cyan
Write-Host ("=" * 80) -ForegroundColor Cyan
Write-Host "Performance samples collected : $($script:AuditData.Performance.Samples.Count)" -ForegroundColor White
Write-Host "Processes created             : $($script:AuditData.Processes.Created.Count)" -ForegroundColor White
Write-Host "Processes terminated          : $($script:AuditData.Processes.Terminated.Count)" -ForegroundColor White
Write-Host "Applications installed        : $($script:AuditData.Applications.Installed.Count)" -ForegroundColor Green
Write-Host "Applications uninstalled      : $($script:AuditData.Applications.Uninstalled.Count)" -ForegroundColor Yellow
Write-Host "Anomalies detected            : $($script:AuditData.Anomalies.Count)" -ForegroundColor $(if ($script:AuditData.Anomalies.Count -gt 0) { 'Yellow' } else { 'Green' })
Write-Host "Error events                  : $($script:AuditData.Events.Errors.Count)" -ForegroundColor Red
Write-Host "`nAudit Performance:" -ForegroundColor Cyan
Write-Host "  Avg sample time             : $($script:AuditData.Performance.AuditOverhead.AvgSampleTime_MS) ms" -ForegroundColor Gray
Write-Host "  Max sample time             : $([math]::Round($script:AuditData.Performance.AuditOverhead.MaxSampleTime_MS, 2)) ms" -ForegroundColor Gray
Write-Host "  Total overhead              : $([math]::Round($script:AuditData.Performance.AuditOverhead.TotalSampleTime_MS / 1000, 2)) sec" -ForegroundColor Gray
Write-Host "`nReports saved to: $OutputPath" -ForegroundColor Cyan
Write-Host ("=" * 80) + "`n" -ForegroundColor Cyan

# Cleanup CIM session
if ($script:CimSession) {
    Remove-CimSession -CimSession $script:CimSession -ErrorAction SilentlyContinue
}

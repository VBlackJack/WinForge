<#
.SYNOPSIS
    Win11Forge - Installation Engine Module v2.1.1 (PowerToys & Quick Assist Fixed)

.DESCRIPTION
    Core installation engine with multi-source support and parallel execution:
    - Winget (primary)
    - Chocolatey (fallback)
    - Microsoft Store (UWP apps)
    - Direct download + silent install (last resort)
    - Application detection with special cases (PowerToys, Quick Assist)
    - Windows Features/Capabilities
    - Parallel installation (up to 5 apps simultaneously)

.NOTES
    Version: 2.1.1 FIXED
    Fixed: PowerToys multi-path detection
    Fixed: Quick Assist Store App detection
    Automatic fallback on installation failure
#>

Set-StrictMode -Version Latest

# === MODULE INITIALIZATION ===
$script:ModuleRoot = Split-Path -Parent $PSCommandPath
$script:RepositoryRoot = Split-Path $script:ModuleRoot -Parent
$script:CoreModulePath = Join-Path $script:RepositoryRoot 'Core\Core.psm1'

if (-not (Get-Command -Name Write-Status -ErrorAction SilentlyContinue)) {
    if (Test-Path -Path $script:CoreModulePath) {
        Import-Module -Name $script:CoreModulePath -Force
    }
}

# === CONFIGURATION ===
$script:MaxParallelJobs = 5
$script:JobCheckInterval = 2

# === DETECTION FUNCTIONS ===

function Test-ApplicationInstalled {
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [PSCustomObject]$Application
    )

    $appName = $Application.Name

    # Special case: PowerToys - Check multiple paths
    if ($appName -eq 'Microsoft PowerToys') {
        $powerToysPaths = @(
            "${env:ProgramFiles}\PowerToys\PowerToys.exe",
            "${env:LOCALAPPDATA}\PowerToys\PowerToys.exe",
            "${env:ProgramFiles(x86)}\PowerToys\PowerToys.exe"
        )
        
        foreach ($path in $powerToysPaths) {
            if (Test-Path $path -ErrorAction SilentlyContinue) {
                return $true
            }
        }
        
        if (Get-Process -Name "PowerToys" -ErrorAction SilentlyContinue) {
            return $true
        }
    }

    # Special case: Quick Assist - Store App
    if ($appName -eq 'Microsoft Quick Assist') {
        $quickAssist = Get-AppxPackage -Name "MicrosoftCorporationII.QuickAssist" -ErrorAction SilentlyContinue
        if ($quickAssist) {
            return $true
        }
    }

    if (-not $Application.Detection) {
        return (Test-ApplicationByName -Name $appName)
    }

    $method = $Application.Detection.Method

    switch ($method) {
        'Registry' {
            return Test-RegistryKey -Path $Application.Detection.Path
        }
        'File' {
            return Test-Path -Path $Application.Detection.Path -PathType Leaf
        }
        'Command' {
            try {
                $output = Invoke-Expression $Application.Detection.Command 2>&1
                return $LASTEXITCODE -eq 0
            } catch {
                return $false
            }
        }
        'WindowsFeature' {
            $feature = Get-WindowsOptionalFeature -Online -FeatureName $Application.Detection.Feature -ErrorAction SilentlyContinue
            return $feature -and $feature.State -eq 'Enabled'
        }
        'WindowsCapability' {
            $capability = Get-WindowsCapability -Online -Name "*$($Application.Detection.Capability)*" -ErrorAction SilentlyContinue
            return $capability -and $capability.State -eq 'Installed'
        }
        'StoreApp' {
            $package = Get-AppxPackage -Name "*$($Application.Detection.PackageName)*" -ErrorAction SilentlyContinue
            return $null -ne $package
        }
        default {
            return (Test-ApplicationByName -Name $appName)
        }
    }
}

function Test-ApplicationByName {
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [string]$Name
    )

    try {
        if (Test-CommandExists -Name 'winget') {
            $wingetList = & winget list --name $Name --accept-source-agreements 2>&1 | Out-String
            if ($wingetList -match [regex]::Escape($Name)) {
                return $true
            }
        }
    } catch {
        Write-Status -Message "Winget detection failed: $($_.Exception.Message)" -Level 'Verbose'
    }

    try {
        if (Test-CommandExists -Name 'choco') {
            $chocoList = & choco list --local-only --exact $Name 2>&1 | Out-String
            if ($chocoList -match $Name -and $chocoList -notmatch '0 packages installed') {
                return $true
            }
        }
    } catch {
        Write-Status -Message "Chocolatey detection failed: $($_.Exception.Message)" -Level 'Verbose'
    }

    $programFiles = @(
        $env:ProgramFiles,
        ${env:ProgramFiles(x86)},
        "$env:LOCALAPPDATA\Programs"
    )

    foreach ($baseDir in $programFiles) {
        if (Test-Path -Path "$baseDir\$Name") {
            return $true
        }
    }

    return $false
}

function Test-RegistryKey {
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    return Test-Path -Path $Path -ErrorAction SilentlyContinue
}

# === INSTALLATION METHODS ===

function Install-ViaWinget {
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [string]$PackageId,

        [Parameter()]
        [switch]$Silent = $true
    )

    if (-not (Test-CommandExists -Name 'winget')) {
        Write-Status -Message "Winget not available" -Level 'Verbose'
        return $false
    }

    try {
        Write-Status -Message "Installing via Winget: $PackageId" -Level 'Info'

        $arguments = @(
            'install',
            '--id', $PackageId,
            '--accept-package-agreements',
            '--accept-source-agreements'
        )

        if ($Silent) {
            $arguments += '--silent'
        }

        $process = Start-Process -FilePath 'winget' -ArgumentList $arguments -Wait -NoNewWindow -PassThru

        if ($process.ExitCode -eq 0) {
            Write-Status -Message "Installed successfully via Winget" -Level 'Success'
            return $true
        }

        Write-Status -Message "Winget installation failed (exit code: $($process.ExitCode))" -Level 'Verbose'
        return $false

    } catch {
        Write-Status -Message "Winget installation error: $($_.Exception.Message)" -Level 'Verbose'
        return $false
    }
}

function Install-ViaChocolatey {
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [string]$PackageName
    )

    if (-not (Test-CommandExists -Name 'choco')) {
        Write-Status -Message "Chocolatey not available" -Level 'Verbose'
        return $false
    }

    try {
        Write-Status -Message "Installing via Chocolatey: $PackageName" -Level 'Info'

        $arguments = @(
            'install', $PackageName,
            '-y',
            '--no-progress',
            '--ignore-checksums'
        )

        $process = Start-Process -FilePath 'choco' -ArgumentList $arguments -Wait -NoNewWindow -PassThru

        if ($process.ExitCode -eq 0) {
            Write-Status -Message "Installed successfully via Chocolatey" -Level 'Success'
            return $true
        }

        Write-Status -Message "Chocolatey installation failed (exit code: $($process.ExitCode))" -Level 'Verbose'
        return $false

    } catch {
        Write-Status -Message "Chocolatey installation error: $($_.Exception.Message)" -Level 'Verbose'
        return $false
    }
}

function Install-ViaStore {
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [string]$ProductId
    )

    try {
        Write-Status -Message "Installing via Microsoft Store: $ProductId" -Level 'Info'

        if (Test-CommandExists -Name 'winget') {
            $arguments = @(
                'install',
                '--id', $ProductId,
                '--source', 'msstore',
                '--accept-package-agreements',
                '--accept-source-agreements',
                '--silent'
            )

            $process = Start-Process -FilePath 'winget' -ArgumentList $arguments -Wait -NoNewWindow -PassThru

            if ($process.ExitCode -eq 0) {
                Write-Status -Message "Installed successfully via Microsoft Store" -Level 'Success'
                return $true
            }
        }

        Start-Process "ms-windows-store://pdp/?ProductId=$ProductId"
        Write-Status -Message "Store opened - please complete installation manually" -Level 'Warning'
        return $false

    } catch {
        Write-Status -Message "Store installation error: $($_.Exception.Message)" -Level 'Verbose'
        return $false
    }
}

function Install-ViaDirectDownload {
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [string]$Url,

        [Parameter()]
        [ValidateSet('exe', 'msi', 'zip', 'auto')]
        [string]$InstallerType = 'auto',

        [Parameter()]
        [string]$CustomArguments = $null
    )

    try {
        Write-Status -Message "Downloading from: $Url" -Level 'Info'

        $tempDir = Join-Path -Path $env:TEMP -ChildPath "Win11Forge_$(Get-Random)"
        New-Item -Path $tempDir -ItemType Directory -Force | Out-Null

        $filename = [System.IO.Path]::GetFileName($Url)
        if ([string]::IsNullOrWhiteSpace($filename) -or $filename -notmatch '\.[a-z]{3,4}$') {
            $filename = "installer_$(Get-Random).exe"
        }

        $installerPath = Join-Path -Path $tempDir -ChildPath $filename

        Invoke-WebRequest -Uri $Url -OutFile $installerPath -UseBasicParsing -ErrorAction Stop

        if (-not (Test-Path -Path $installerPath)) {
            Write-Status -Message "Download failed: File not found" -Level 'Error'
            return $false
        }

        Write-Status -Message "Downloaded: $(Format-FileSize -Bytes (Get-Item $installerPath).Length)" -Level 'Info'

        if ($InstallerType -eq 'auto') {
            $InstallerType = switch -Regex ($filename) {
                '\.msi$' { 'msi' }
                '\.zip$' { 'zip' }
                default  { 'exe' }
            }
        }

        $installed = $false

        switch ($InstallerType) {
            'msi' {
                $arguments = @('/i', "`"$installerPath`"", '/qn', '/norestart')
                $process = Start-Process -FilePath 'msiexec.exe' -ArgumentList $arguments -Wait -NoNewWindow -PassThru
                $installed = ($process.ExitCode -eq 0)
            }
            'exe' {
                # Use custom arguments if provided (e.g., Battle.net --lang=frFR --installpath=...)
                if ($CustomArguments) {
                    Write-Status -Message "Using custom install arguments: $CustomArguments" -Level 'Verbose'
                    $process = Start-Process -FilePath $installerPath -ArgumentList $CustomArguments -Wait -NoNewWindow -PassThru -ErrorAction SilentlyContinue
                    if ($process.ExitCode -eq 0) {
                        $installed = $true
                    }
                } else {
                    # Try common silent switches
                    $silentSwitches = @('/S', '/SILENT', '/VERYSILENT', '/quiet', '/qn')

                    foreach ($switch in $silentSwitches) {
                        $process = Start-Process -FilePath $installerPath -ArgumentList $switch -Wait -NoNewWindow -PassThru -ErrorAction SilentlyContinue
                        if ($process.ExitCode -eq 0) {
                            $installed = $true
                            break
                        }
                    }
                }
            }
            'zip' {
                Write-Status -Message "ZIP extraction not yet implemented" -Level 'Warning'
                $installed = $false
            }
        }

        Remove-Item -Path $tempDir -Recurse -Force -ErrorAction SilentlyContinue

        if ($installed) {
            Write-Status -Message "Installed successfully via direct download" -Level 'Success'
        } else {
            Write-Status -Message "Direct installation failed" -Level 'Verbose'
        }

        return $installed

    } catch {
        Write-Status -Message "Direct download error: $($_.Exception.Message)" -Level 'Verbose'
        return $false
    }
}

function Install-WindowsFeature {
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [string]$FeatureName
    )

    try {
        Write-Status -Message "Enabling Windows feature: $FeatureName" -Level 'Info'

        $feature = Get-WindowsOptionalFeature -Online -FeatureName $FeatureName -ErrorAction Stop

        if ($feature.State -eq 'Enabled') {
            Write-Status -Message "Feature already enabled" -Level 'Success'
            return $true
        }

        Enable-WindowsOptionalFeature -Online -FeatureName $FeatureName -NoRestart -ErrorAction Stop | Out-Null

        Write-Status -Message "Feature enabled successfully" -Level 'Success'
        return $true

    } catch {
        Write-Status -Message "Failed to enable feature: $($_.Exception.Message)" -Level 'Error'
        return $false
    }
}

function Install-WindowsCapability {
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [string]$CapabilityName
    )

    try {
        Write-Status -Message "Installing Windows capability: $CapabilityName" -Level 'Info'

        $capabilities = Get-WindowsCapability -Online | Where-Object { $_.Name -like "*$CapabilityName*" }

        if ($null -eq $capabilities -or $capabilities.Count -eq 0) {
            Write-Status -Message "Capability not found: $CapabilityName" -Level 'Error'
            return $false
        }

        $capability = if ($capabilities -is [array]) { $capabilities[0] } else { $capabilities }

        if ($capability.State -eq 'Installed') {
            Write-Status -Message "Capability already installed" -Level 'Success'
            return $true
        }

        Write-Status -Message "Installing capability: $($capability.Name)" -Level 'Verbose'
        Add-WindowsCapability -Online -Name $capability.Name -ErrorAction Stop | Out-Null

        Write-Status -Message "Capability installed successfully" -Level 'Success'
        return $true

    } catch {
        Write-Status -Message "Failed to install capability: $($_.Exception.Message)" -Level 'Error'
        return $false
    }
}

# === SEQUENTIAL INSTALLATION ===

function Install-Application {
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [Parameter(Mandatory)]
        [PSCustomObject]$Application,

        [Parameter()]
        [switch]$Force
    )

    $result = @{
        ApplicationName = $Application.Name
        Success = $false
        AlreadyInstalled = $false
        Method = $null
        Message = ''
    }

    # Check environment restrictions
    if ($Application.EnvironmentRestrictions -and $Application.EnvironmentRestrictions.Count -gt 0) {
        if (-not (Get-Command -Name 'Get-SystemEnvironmentType' -ErrorAction SilentlyContinue)) {
            $envModule = Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) 'Modules\EnvironmentDetection.psm1'
            if (Test-Path $envModule) {
                Import-Module $envModule -Force -WarningAction SilentlyContinue
            }
        }

        try {
            $currentEnv = Get-SystemEnvironmentType
            if ($Application.EnvironmentRestrictions -contains $currentEnv) {
                Write-Status -Message "$($Application.Name) is restricted in $currentEnv environment" -Level 'Warning'
                $result.Message = "Not compatible with $currentEnv environment"
                return $result
            }
        } catch {
            Write-Status -Message "Could not verify environment restrictions: $($_.Exception.Message)" -Level 'Verbose'
        }
    }

    if (-not $Force) {
        if (Test-ApplicationInstalled -Application $Application) {
            Write-Status -Message "Already installed: $($Application.Name)" -Level 'Success'
            $result.AlreadyInstalled = $true
            $result.Success = $true
            $result.Message = 'Already installed'
            return $result
        }
    }

    Write-Status -Message "Installing: $($Application.Name)" -Level 'Info'

    # Check for custom InstallMethod (WindowsFeature, WindowsCapability, etc.)
    $installMethod = if ($Application.PSObject.Properties['InstallMethod']) { $Application.InstallMethod } else { $null }

    if ($installMethod) {
        $installed = $false

        switch ($installMethod) {
            'WindowsFeature' {
                $installed = Install-WindowsFeature -FeatureName $Application.Detection.Feature
                $result.Method = 'WindowsFeature'
            }
            'WindowsCapability' {
                $installed = Install-WindowsCapability -CapabilityName $Application.Detection.Capability
                $result.Method = 'WindowsCapability'
            }
        }

        if ($installed) {
            $result.Success = $true
            $result.Message = "Installed via $($result.Method)"
            return $result
        } else {
            $result.Message = "Failed to install via $($result.Method)"
            return $result
        }
    }

    $sources = $Application.Sources

    if (-not $sources) {
        $result.Message = 'No installation sources available'
        return $result
    }

    # Track attempted methods
    $attemptedMethods = @()
    $failureReasons = @()

    if ($sources.Winget) {
        $attemptedMethods += 'Winget'
        Write-Status -Message "Attempting Winget: $($sources.Winget)" -Level 'Verbose'
        if (Install-ViaWinget -PackageId $sources.Winget) {
            $result.Success = $true
            $result.Method = 'Winget'
            $result.Message = 'Installed via Winget'
            return $result
        } else {
            $failureReasons += "Winget failed (ID: $($sources.Winget))"
        }
    }

    if ($sources.Chocolatey) {
        $attemptedMethods += 'Chocolatey'
        Write-Status -Message "Attempting Chocolatey: $($sources.Chocolatey)" -Level 'Verbose'
        if (Install-ViaChocolatey -PackageName $sources.Chocolatey) {
            $result.Success = $true
            $result.Method = 'Chocolatey'
            $result.Message = 'Installed via Chocolatey'
            return $result
        } else {
            $failureReasons += "Chocolatey failed (Package: $($sources.Chocolatey))"
        }
    }

    if ($sources.Store) {
        $attemptedMethods += 'Store'
        Write-Status -Message "Attempting Microsoft Store: $($sources.Store)" -Level 'Verbose'
        if (Install-ViaStore -ProductId $sources.Store) {
            $result.Success = $true
            $result.Method = 'Store'
            $result.Message = 'Installed via Microsoft Store'
            return $result
        } else {
            $failureReasons += "Store failed (ID: $($sources.Store))"
        }
    }

    if ($sources.DirectUrl) {
        $attemptedMethods += 'DirectDownload'
        Write-Status -Message "Attempting direct download: $($sources.DirectUrl)" -Level 'Verbose'

        # Check if custom install arguments are provided
        $installParams = @{ Url = $sources.DirectUrl }
        if ($Application.InstallArguments) {
            $installParams['CustomArguments'] = $Application.InstallArguments
            Write-Status -Message "Custom arguments detected: $($Application.InstallArguments)" -Level 'Verbose'
        }

        if (Install-ViaDirectDownload @installParams) {
            $result.Success = $true
            $result.Method = 'DirectDownload'
            $result.Message = 'Installed via direct download'
            return $result
        } else {
            $failureReasons += "DirectDownload failed"
        }
    }

    # Build detailed failure message
    $result.Message = if ($attemptedMethods.Count -gt 0) {
        "All methods failed: $($failureReasons -join '; ')"
    } else {
        'No valid installation sources configured'
    }

    Write-Status -Message "Installation failed: $($result.Message)" -Level 'Warning'
    return $result
}

# === PARALLEL INSTALLATION (PowerShell 7+) ===

function Install-ApplicationsParallel {
    [CmdletBinding()]
    [OutputType([hashtable[]])]
    param(
        [Parameter(Mandatory)]
        [PSCustomObject[]]$Applications,

        [Parameter()]
        [switch]$Force,

        [Parameter()]
        [ValidateRange(1, 10)]
        [int]$MaxParallel = 5
    )

    if ($PSVersionTable.PSVersion.Major -lt 7) {
        Write-Host "WARNING: Parallel installation requires PowerShell 7+" -ForegroundColor Yellow
        Write-Host "Falling back to sequential installation..." -ForegroundColor Yellow
        
        $results = @()
        foreach ($app in $Applications) {
            $results += Install-Application -Application $app -Force:$Force
        }
        return $results
    }

    Write-Host ""
    Write-Host "=== Parallel Installation Mode ===" -ForegroundColor Cyan
    Write-Host "Max parallel threads: $MaxParallel" -ForegroundColor Cyan
    Write-Host "Total applications: $($Applications.Count)" -ForegroundColor Cyan
    Write-Host ""

    $startTime = Get-Date
    $sortedApps = $Applications | Sort-Object -Property Priority
    
    $moduleRoot = $script:ModuleRoot
    $repoRoot = $script:RepositoryRoot
    $forceInstall = $Force.IsPresent
    
    $currentEnvironment = Get-SystemEnvironmentType
    
    $appsToInstall = @()
    $skippedApps = @()
    
    foreach ($app in $sortedApps) {
        if ($app.EnvironmentRestrictions -and $app.EnvironmentRestrictions.Count -gt 0) {
            if ($app.EnvironmentRestrictions -contains $currentEnvironment) {
                Write-Host "[SKIP] $($app.Name) - Not compatible with $currentEnvironment" -ForegroundColor Yellow
                $skippedApps += [PSCustomObject]@{
                    ApplicationName = $app.Name
                    Success = $false
                    AlreadyInstalled = $false
                    Method = $null
                    Message = "Not compatible with $currentEnvironment environment"
                }
                continue
            }
        }
        $appsToInstall += $app
    }
    
    Write-Host "Applications to install: $($appsToInstall.Count)" -ForegroundColor Cyan
    Write-Host "Skipped due to environment: $($skippedApps.Count)" -ForegroundColor Yellow
    Write-Host ""
    
    # Create parallel logs directory
    $parallelLogsDir = Join-Path $repoRoot 'Logs\Parallel'
    if (-not (Test-Path $parallelLogsDir)) {
        New-Item -Path $parallelLogsDir -ItemType Directory -Force | Out-Null
    }
    $timestamp = Get-Date -Format 'yyyyMMdd_HHmmss'

    $installResults = $appsToInstall | ForEach-Object -ThrottleLimit $MaxParallel -Parallel {
        $app = $_
        $force = $using:forceInstall
        $modRoot = $using:moduleRoot
        $repRoot = $using:repoRoot
        $parallelLogDir = $using:parallelLogsDir
        $ts = $using:timestamp

        # Create app-specific log file
        $appLogFile = Join-Path $parallelLogDir "parallel_${ts}_$($app.Name -replace '[^\w\-]', '_').log"

        # Helper function to log to file
        function Write-ParallelLog {
            param([string]$Message, [string]$Level = 'Info')
            $logTimestamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
            $logMessage = "[$logTimestamp] [$Level] $Message"
            $logMessage | Out-File -FilePath $appLogFile -Append -Encoding UTF8
        }

        Write-ParallelLog "Starting installation of $($app.Name)" 'Info'

        $coreModulePath = Join-Path $repRoot 'Core\Core.psm1'
        if (Test-Path $coreModulePath) {
            Import-Module $coreModulePath -Force -WarningAction SilentlyContinue
        }

        $result = @{
            ApplicationName = $app.Name
            Success = $false
            AlreadyInstalled = $false
            Method = $null
            Message = ''
        }
        
        try {
            if (-not $force) {
                $installed = $false
                
                # === SPECIAL CASE: PowerToys - Multi-path detection ===
                if ($app.Name -eq 'Microsoft PowerToys') {
                    $powerToysPaths = @(
                        "${env:ProgramFiles}\PowerToys\PowerToys.exe",
                        "${env:LOCALAPPDATA}\PowerToys\PowerToys.exe",
                        "${env:ProgramFiles(x86)}\PowerToys\PowerToys.exe"
                    )
                    
                    foreach ($path in $powerToysPaths) {
                        if (Test-Path $path -ErrorAction SilentlyContinue) {
                            try {
                                $version = (Get-ItemProperty $path).VersionInfo.ProductVersion
                                $result.AlreadyInstalled = $true
                                $result.Success = $true
                                $result.Message = "Already installed at $path (v$version)"
                                return $result
                            } catch {
                                $result.AlreadyInstalled = $true
                                $result.Success = $true
                                $result.Message = "Already installed at $path"
                                return $result
                            }
                        }
                    }
                    
                    if (Get-Process -Name "PowerToys" -ErrorAction SilentlyContinue) {
                        $result.AlreadyInstalled = $true
                        $result.Success = $true
                        $result.Message = "Already installed and running"
                        return $result
                    }
                }
                
                # === SPECIAL CASE: Quick Assist - Store App detection ===
                if ($app.Name -eq 'Microsoft Quick Assist') {
                    try {
                        $quickAssist = Get-AppxPackage -Name "MicrosoftCorporationII.QuickAssist" -ErrorAction SilentlyContinue
                        if ($quickAssist) {
                            $result.AlreadyInstalled = $true
                            $result.Success = $true
                            $result.Message = "Already installed via Store (v$($quickAssist.Version))"
                            return $result
                        }
                    } catch {
                        # Continue to normal installation
                    }
                }
                
                # === GENERIC DETECTION ===
                if ($app.Detection) {
                    switch ($app.Detection.Method) {
                        'Registry' {
                            $installed = Test-Path -Path $app.Detection.Path -ErrorAction SilentlyContinue
                        }
                        'File' {
                            if ($app.Detection.Path -match '\*') {
                                $installed = (Get-ChildItem -Path $app.Detection.Path -ErrorAction SilentlyContinue).Count -gt 0
                            } else {
                                $installed = Test-Path -Path $app.Detection.Path -PathType Leaf -ErrorAction SilentlyContinue
                            }
                        }
                        'Command' {
                            try {
                                $null = Invoke-Expression $app.Detection.Command 2>&1
                                $installed = ($LASTEXITCODE -eq 0)
                            } catch {
                                $installed = $false
                            }
                        }
                        'WindowsFeature' {
                            $feature = Get-WindowsOptionalFeature -Online -FeatureName $app.Detection.Feature -ErrorAction SilentlyContinue
                            $installed = $feature -and $feature.State -eq 'Enabled'
                        }
                        'WindowsCapability' {
                            $capability = Get-WindowsCapability -Online | Where-Object { $_.Name -like "*$($app.Detection.Capability)*" } -ErrorAction SilentlyContinue
                            $installed = $capability -and $capability.State -eq 'Installed'
                        }
                    }
                }
                
                if (-not $installed -and (Get-Command -Name 'winget' -ErrorAction SilentlyContinue)) {
                    $wingetList = & winget list --name $app.Name --accept-source-agreements 2>&1 | Out-String
                    if ($wingetList -match [regex]::Escape($app.Name)) {
                        $installed = $true
                    }
                }
                
                if ($installed) {
                    Write-ParallelLog "Already installed - skipping" 'Success'
                    $result.AlreadyInstalled = $true
                    $result.Success = $true
                    $result.Message = 'Already installed'
                    return $result
                }
            }

            Write-ParallelLog "Not installed - proceeding with installation" 'Info'

            # === INSTALLATION ===
            $appInstallMethod = if ($app.PSObject.Properties['InstallMethod']) { $app.InstallMethod } else { $null }
            if ($appInstallMethod) {
                Write-ParallelLog "Using custom install method: $appInstallMethod" 'Info'
                switch ($appInstallMethod) {
                    'WindowsFeature' {
                        Write-ParallelLog "Installing as Windows Feature: $($app.Detection.Feature)" 'Info'
                        $feature = Get-WindowsOptionalFeature -Online -FeatureName $app.Detection.Feature -ErrorAction Stop
                        if ($feature.State -ne 'Enabled') {
                            Enable-WindowsOptionalFeature -Online -FeatureName $app.Detection.Feature -NoRestart -ErrorAction Stop | Out-Null
                        }
                        Write-ParallelLog "Windows Feature installed successfully" 'Success'
                        $result.Success = $true
                        $result.Method = 'WindowsFeature'
                        $result.Message = 'Installed via WindowsFeature'
                        return $result
                    }
                    'WindowsCapability' {
                        Write-ParallelLog "Installing as Windows Capability: $($app.Detection.Capability)" 'Info'
                        $capabilities = Get-WindowsCapability -Online | Where-Object { $_.Name -like "*$($app.Detection.Capability)*" }
                        if ($capabilities) {
                            $capability = if ($capabilities -is [array]) { $capabilities[0] } else { $capabilities }
                            if ($capability.State -ne 'Installed') {
                                Add-WindowsCapability -Online -Name $capability.Name -ErrorAction Stop | Out-Null
                            }
                            Write-ParallelLog "Windows Capability installed successfully" 'Success'
                            $result.Success = $true
                            $result.Method = 'WindowsCapability'
                            $result.Message = 'Installed via WindowsCapability'
                            return $result
                        }
                    }
                }
            }

            $sources = $app.Sources

            # 1. Winget
            if ($sources.Winget -and (Get-Command -Name 'winget' -ErrorAction SilentlyContinue)) {
                Write-ParallelLog "Attempting installation via Winget: $($sources.Winget)" 'Info'
                $arguments = @(
                    'install',
                    '--id', $sources.Winget,
                    '--accept-package-agreements',
                    '--accept-source-agreements',
                    '--silent'
                )

                $process = Start-Process -FilePath 'winget' -ArgumentList $arguments -Wait -NoNewWindow -PassThru

                if ($process.ExitCode -eq 0) {
                    Write-ParallelLog "Installed successfully via Winget" 'Success'
                    $result.Success = $true
                    $result.Method = 'Winget'
                    $result.Message = 'Installed via Winget'
                    return $result
                } else {
                    Write-ParallelLog "Winget installation failed (exit code: $($process.ExitCode))" 'Warning'
                }
            }

            # 2. Chocolatey
            if ($sources.Chocolatey -and (Get-Command -Name 'choco' -ErrorAction SilentlyContinue)) {
                Write-ParallelLog "Attempting installation via Chocolatey: $($sources.Chocolatey)" 'Info'
                $arguments = @(
                    'install', $sources.Chocolatey,
                    '-y',
                    '--no-progress',
                    '--ignore-checksums'
                )

                $process = Start-Process -FilePath 'choco' -ArgumentList $arguments -Wait -NoNewWindow -PassThru

                if ($process.ExitCode -eq 0) {
                    Write-ParallelLog "Installed successfully via Chocolatey" 'Success'
                    $result.Success = $true
                    $result.Method = 'Chocolatey'
                    $result.Message = 'Installed via Chocolatey'
                    return $result
                } else {
                    Write-ParallelLog "Chocolatey installation failed (exit code: $($process.ExitCode))" 'Warning'
                }
            }

            # 3. Microsoft Store
            if ($sources.Store -and (Get-Command -Name 'winget' -ErrorAction SilentlyContinue)) {
                Write-ParallelLog "Attempting installation via Microsoft Store: $($sources.Store)" 'Info'
                $arguments = @(
                    'install',
                    '--id', $sources.Store,
                    '--source', 'msstore',
                    '--accept-package-agreements',
                    '--accept-source-agreements',
                    '--silent'
                )

                $process = Start-Process -FilePath 'winget' -ArgumentList $arguments -Wait -NoNewWindow -PassThru

                if ($process.ExitCode -eq 0) {
                    Write-ParallelLog "Installed successfully via Microsoft Store" 'Success'
                    $result.Success = $true
                    $result.Method = 'Store'
                    $result.Message = 'Installed via Microsoft Store'
                    return $result
                } else {
                    Write-ParallelLog "Microsoft Store installation failed (exit code: $($process.ExitCode))" 'Warning'
                }
            }

            Write-ParallelLog "All installation methods failed" 'Error'
            $result.Message = 'All installation methods failed'

        } catch {
            $errorMsg = $_.Exception.Message
            Write-ParallelLog "EXCEPTION: $errorMsg" 'Error'
            Write-ParallelLog "Stack trace: $($_.ScriptStackTrace)" 'Error'
            $result.Message = "Error: $errorMsg"
        }

        # Final result logging
        if ($result.Success -or $result.AlreadyInstalled) {
            $status = if ($result.AlreadyInstalled) { "Already Installed" } else { "Success" }
            Write-ParallelLog "RESULT: $status - $($result.Message)" 'Success'
        } else {
            Write-ParallelLog "RESULT: Failed - $($result.Message)" 'Error'
        }

        return $result
    }
    
    $allResults = @($installResults) + @($skippedApps)
    
    $endTime = Get-Date
    $totalTime = $endTime - $startTime
    
    Write-Host ""
    Write-Host "=== Parallel Installation Summary ===" -ForegroundColor Green
    Write-Host "Total time: $($totalTime.ToString('mm\:ss'))" -ForegroundColor Cyan
    Write-Host "Applications processed: $($Applications.Count)" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Detailed logs saved to: $parallelLogsDir" -ForegroundColor Yellow
    Write-Host "Individual app logs: parallel_${timestamp}_<AppName>.log" -ForegroundColor Gray
    Write-Host ""
    
    Write-Host "Installation Results:" -ForegroundColor Cyan
    foreach ($result in $allResults) {
        if ($result.Success -or $result.AlreadyInstalled) {
            $status = if ($result.AlreadyInstalled) { "Already Installed" } else { "Success" }
            Write-Host "  OK $($result.ApplicationName) - $status" -ForegroundColor Green
            if ($result.Method) {
                Write-Host "    Method: $($result.Method)" -ForegroundColor Gray
            }
        } else {
            Write-Host "  FAILED $($result.ApplicationName) - Failed" -ForegroundColor Red
            Write-Host "    Reason: $($result.Message)" -ForegroundColor Gray
        }
    }
    
    Write-Host ""
    
    return $allResults
}

# === MODULE EXPORTS ===

Export-ModuleMember -Function @(
    'Test-ApplicationInstalled',
    'Test-ApplicationByName',
    'Install-ViaWinget',
    'Install-ViaChocolatey',
    'Install-ViaStore',
    'Install-ViaDirectDownload',
    'Install-WindowsFeature',
    'Install-WindowsCapability',
    'Install-Application',
    'Install-ApplicationsParallel'
)
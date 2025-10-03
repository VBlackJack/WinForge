<#
.SYNOPSIS
    Win11Forge - Prerequisites installation module (v2.1.2 FIXED)

.DESCRIPTION
    Handles installation of system prerequisites with environment refresh:
    - PowerShell 7
    - Chocolatey
    - Winget
    - .NET runtimes (.NET 6, 8, Framework 4.8.1)
    - Visual C++ redistributables (2015-2022)
    - Java runtime
    - Environment variable refresh

.NOTES
    Version: 2.1.2 (FIXED: Write-Status empty strings + Chocolatey args)
    All bugs from deployment test have been corrected
#>

Set-StrictMode -Version Latest

# === MODULE INITIALIZATION ===
$script:ModuleRoot = Split-Path -Parent $PSCommandPath
$script:RepositoryRoot = Split-Path $script:ModuleRoot -Parent
$script:CoreModulePath = Join-Path $script:RepositoryRoot 'Core\Core.psm1'

if (-not (Get-Command -Name Write-Status -ErrorAction SilentlyContinue)) {
    if (Test-Path -Path $script:CoreModulePath) {
        Import-Module -Name $script:CoreModulePath -Force
    } else {
        throw 'Core module is required before loading Prerequisites.psm1'
    }
}

# === ENVIRONMENT REFRESH ===

function Update-EnvironmentPath {
    <#
    .SYNOPSIS
        Refreshes environment variables from registry without restarting session.
    
    .DESCRIPTION
        Reloads PATH and other environment variables from:
        - Machine-level (HKLM)
        - User-level (HKCU)
        - Session-level (Process)
    #>
    [CmdletBinding()]
    param()

    try {
        Write-Verbose "Refreshing environment variables..."

        # Get Machine PATH
        $machinePath = [System.Environment]::GetEnvironmentVariable('PATH', 'Machine')
        
        # Get User PATH
        $userPath = [System.Environment]::GetEnvironmentVariable('PATH', 'User')
        
        # Combine and set Process PATH
        $combinedPath = @($machinePath, $userPath) | Where-Object { $_ } | Join-String -Separator ';'
        [System.Environment]::SetEnvironmentVariable('PATH', $combinedPath, 'Process')
        
        # Update $env:PATH
        $env:PATH = $combinedPath

        # Refresh other common environment variables
        $envVars = @(
            'ChocolateyInstall',
            'JAVA_HOME',
            'DOTNET_ROOT',
            'ProgramFiles',
            'ProgramFiles(x86)',
            'CommonProgramFiles',
            'PSModulePath'
        )

        foreach ($varName in $envVars) {
            $machineValue = [System.Environment]::GetEnvironmentVariable($varName, 'Machine')
            $userValue = [System.Environment]::GetEnvironmentVariable($varName, 'User')
            
            $value = if ($userValue) { $userValue } elseif ($machineValue) { $machineValue }
            
            if ($value) {
                [System.Environment]::SetEnvironmentVariable($varName, $value, 'Process')
                Set-Item -Path "env:$varName" -Value $value -Force -ErrorAction SilentlyContinue
            }
        }

        Write-Verbose "Environment variables refreshed successfully"
        return $true
    } catch {
        Write-Verbose "Error refreshing environment: $($_.Exception.Message)"
        return $false
    }
}

function Invoke-EnvironmentRefresh {
    <#
    .SYNOPSIS
        Forces a complete environment refresh including PATH discovery.
    #>
    [CmdletBinding()]
    param()

    Update-EnvironmentPath | Out-Null

    # Refresh PowerShell's command cache
    $env:Path = [System.Environment]::GetEnvironmentVariable('Path', 'Machine') + ';' + 
                [System.Environment]::GetEnvironmentVariable('Path', 'User')
    
    # Force Get-Command cache refresh
    Get-Command -All | Out-Null
    
    Write-Status -Message "Environment refreshed" -Level 'Success'
}

# === HELPER FUNCTIONS ===

function Test-CommandAvailable {
    <#
    .SYNOPSIS
        Tests if a command is available in the current session.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Name
    )

    return $null -ne (Get-Command -Name $Name -ErrorAction SilentlyContinue)
}

function Invoke-ExternalProcess {
    <#
    .SYNOPSIS
        Executes an external process with error handling.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$FilePath,
        
        [string[]]$ArgumentList,

        [switch]$RefreshEnvironment
    )

    try {
        $process = Start-Process -FilePath $FilePath -ArgumentList $ArgumentList -Wait -NoNewWindow -PassThru
        
        if ($RefreshEnvironment) {
            Invoke-EnvironmentRefresh
        }

        if ($process.ExitCode -eq 0) {
            return $true
        }
        
        Write-Verbose "Process $FilePath exited with code $($process.ExitCode)"
        return $false
    } catch {
        Write-Verbose "Failed to start process $FilePath : $($_.Exception.Message)"
        return $false
    }
}

# === CHOCOLATEY INSTALLATION ===

function Install-Chocolatey {
    <#
    .SYNOPSIS
        Installs Chocolatey package manager.
    #>
    [CmdletBinding()]
    param([switch]$Force)

    $chocolateyAvailable = Test-CommandAvailable -Name 'choco'
    
    if ($chocolateyAvailable -and -not $Force) {
        Write-Status -Message 'Chocolatey is already installed' -Level 'Info'
        return $true
    }

    try {
        Write-Status -Message 'Installing Chocolatey package manager...' -Level 'Info'
        
        Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass -Force
        [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.ServicePointManager]::SecurityProtocol -bor 3072
        
        Invoke-Expression ((New-Object System.Net.WebClient).DownloadString('https://community.chocolatey.org/install.ps1'))
        
        # Refresh environment after installation
        Invoke-EnvironmentRefresh
        
    } catch {
        Write-Status -Message "Chocolatey installation failed: $($_.Exception.Message)" -Level 'Error'
        throw "Chocolatey installation failed: $($_.Exception.Message)"
    }

    if (Test-CommandAvailable -Name 'choco') {
        Write-Status -Message 'Chocolatey installed successfully' -Level 'Success'
        return $true
    }

    Write-Status -Message 'Chocolatey installation failed: Unknown error' -Level 'Error'
    return $false
}

# === POWERSHELL 7 INSTALLATION ===

function Install-PowerShell7 {
    <#
    .SYNOPSIS
        Installs or upgrades to PowerShell 7.
    #>
    [CmdletBinding()]
    param([switch]$Force)

    $currentVersion = $PSVersionTable.PSVersion
    
    if ($currentVersion.Major -ge 7 -and -not $Force) {
        Write-Status -Message "PowerShell $currentVersion is already installed" -Level 'Info'
        return $true
    }

    Write-Status -Message 'Installing PowerShell 7...' -Level 'Info'

    # Try Winget first
    if (Test-CommandAvailable -Name 'winget') {
        try {
            $arguments = @('install', '--id', 'Microsoft.PowerShell', '--silent', '--accept-package-agreements', '--accept-source-agreements')
            if ($Force) { $arguments += '--force' }
            
            if (Invoke-ExternalProcess -FilePath 'winget' -ArgumentList $arguments -RefreshEnvironment) {
                Write-Status -Message 'PowerShell 7 installation completed successfully' -Level 'Success'
                Write-Status -Message 'IMPORTANT: Please restart this script in PowerShell 7 for full functionality' -Level 'Warning'
                return $true
            }
        } catch {
            Write-Verbose "Winget installation failed: $($_.Exception.Message)"
        }
    }

    # Try Chocolatey as fallback
    if (Test-CommandAvailable -Name 'choco') {
        try {
            $arguments = @('install', 'powershell-core', '-y', '--no-progress')
            if ($Force) { $arguments += '--force' }
            
            if (Invoke-ExternalProcess -FilePath 'choco' -ArgumentList $arguments -RefreshEnvironment) {
                Write-Status -Message 'PowerShell 7 installation completed successfully' -Level 'Success'
                Write-Status -Message 'IMPORTANT: Please restart this script in PowerShell 7 for full functionality' -Level 'Warning'
                return $true
            }
        } catch {
            Write-Verbose "Chocolatey installation failed: $($_.Exception.Message)"
        }
    }

    # Direct download as last resort
    try {
        Write-Status -Message 'Attempting direct download...' -Level 'Info'
        
        $downloadUrl = 'https://github.com/PowerShell/PowerShell/releases/download/v7.4.6/PowerShell-7.4.6-win-x64.msi'
        $tempPath = Join-Path -Path $env:TEMP -ChildPath 'PowerShell-7.4.6-win-x64.msi'
        
        Invoke-WebRequest -Uri $downloadUrl -OutFile $tempPath -UseBasicParsing
        
        $arguments = @('/i', "`"$tempPath`"", '/qn', '/norestart', 'ADD_PATH=1', 'ENABLE_MU=1')
        if (Invoke-ExternalProcess -FilePath 'msiexec.exe' -ArgumentList $arguments -RefreshEnvironment) {
            Remove-Item -Path $tempPath -Force -ErrorAction SilentlyContinue
            Write-Status -Message 'PowerShell 7 installation completed successfully' -Level 'Success'
            Write-Status -Message 'IMPORTANT: Please restart this script in PowerShell 7 for full functionality' -Level 'Warning'
            return $true
        }
        
        Remove-Item -Path $tempPath -Force -ErrorAction SilentlyContinue
    } catch {
        Write-Verbose "Direct download failed: $($_.Exception.Message)"
    }

    Write-Status -Message 'PowerShell 7 installation failed: Unknown error' -Level 'Error'
    return $false
}

# === .NET RUNTIME INSTALLATION ===

function Install-DotNetRuntime {
    <#
    .SYNOPSIS
        Installs .NET runtimes (Framework 4.8.1, .NET 6, .NET 8).
    #>
    [CmdletBinding()]
    param([switch]$Force)

    $runtimes = @(
        @{ 
            Name = '.NET Framework 4.8.1'
            WingetId = 'Microsoft.DotNet.Framework.DeveloperPack_4'
            ChocolateyId = 'dotnetfx'
            Type = 'Framework'
        },
        @{ 
            Name = '.NET 6 Runtime'
            WingetId = 'Microsoft.DotNet.Runtime.6'
            ChocolateyId = 'dotnet-6.0-runtime'
            Type = 'Core'
        },
        @{ 
            Name = '.NET 6 Desktop Runtime'
            WingetId = 'Microsoft.DotNet.DesktopRuntime.6'
            ChocolateyId = 'dotnet-6.0-desktopruntime'
            Type = 'Core'
        },
        @{ 
            Name = '.NET 8 Runtime'
            WingetId = 'Microsoft.DotNet.Runtime.8'
            ChocolateyId = 'dotnet-8.0-runtime'
            Type = 'Core'
        },
        @{ 
            Name = '.NET 8 Desktop Runtime'
            WingetId = 'Microsoft.DotNet.DesktopRuntime.8'
            ChocolateyId = 'dotnet-8.0-desktopruntime'
            Type = 'Core'
        }
    )

    $allSucceeded = $true

    foreach ($runtime in $runtimes) {
        Write-Status -Message "Installing $($runtime.Name)..." -Level 'Info'
        $installed = $false

        # Try Winget first
        if (Test-CommandAvailable -Name 'winget') {
            $arguments = @('install', '--id', $runtime.WingetId, '--silent', '--accept-package-agreements', '--accept-source-agreements')
            if ($Force) { $arguments += '--force' }
            
            $installed = Invoke-ExternalProcess -FilePath 'winget' -ArgumentList $arguments -RefreshEnvironment
        }

        # Try Chocolatey as fallback
        if ((-not $installed) -and (Test-CommandAvailable -Name 'choco')) {
            $arguments = @('install', $runtime.ChocolateyId, '-y', '--no-progress')
            if ($Force) { $arguments += '--force' }
            
            $installed = Invoke-ExternalProcess -FilePath 'choco' -ArgumentList $arguments -RefreshEnvironment
        }

        if ($installed) {
            Write-Status -Message "$($runtime.Name) installed successfully" -Level 'Success'
        } else {
            Write-Status -Message "Installation failed for $($runtime.Name)" -Level 'Warning'
            $allSucceeded = $false
        }
    }

    # Final environment refresh after all runtimes
    Invoke-EnvironmentRefresh
    
    return $allSucceeded
}

# === VISUAL C++ REDISTRIBUTABLES ===

function Install-VCRedist {
    <#
    .SYNOPSIS
        Installs Visual C++ redistributables (2015-2022, all architectures).
    #>
    [CmdletBinding()]
    param([switch]$Force)

    $vcRedists = @(
        @{ 
            Name = 'Visual C++ 2015-2022 x64'
            ChocolateyId = 'vcredist2015'
            WingetId = 'Microsoft.VCRedist.2015+.x64'
        },
        @{ 
            Name = 'Visual C++ 2015-2022 x86'
            ChocolateyId = 'vcredist2015'
            WingetId = 'Microsoft.VCRedist.2015+.x86'
        }
    )

    $allSucceeded = $true

    foreach ($vcRedist in $vcRedists) {
        Write-Status -Message "Installing $($vcRedist.Name)..." -Level 'Info'
        $installed = $false

        # Try Chocolatey first (more reliable for VCRedist)
        if (Test-CommandAvailable -Name 'choco') {
            # FIXED: Removed malformed --params argument
            $arguments = @('install', $vcRedist.ChocolateyId, '-y', '--no-progress')
            if ($Force) { $arguments += '--force' }
            
            $installed = Invoke-ExternalProcess -FilePath 'choco' -ArgumentList $arguments
        }

        # Try Winget as fallback
        if ((-not $installed) -and (Test-CommandAvailable -Name 'winget')) {
            $arguments = @('install', '--id', $vcRedist.WingetId, '--silent', '--accept-package-agreements', '--accept-source-agreements')
            if ($Force) { $arguments += '--force' }
            
            $installed = Invoke-ExternalProcess -FilePath 'winget' -ArgumentList $arguments
        }

        if ($installed) {
            Write-Status -Message "$($vcRedist.Name) installed successfully" -Level 'Success'
        } else {
            Write-Status -Message "Installation failed for $($vcRedist.Name)" -Level 'Warning'
            $allSucceeded = $false
        }
    }

    return $allSucceeded
}

# === JAVA RUNTIME INSTALLATION ===

function Install-JavaRuntime {
    <#
    .SYNOPSIS
        Installs Java runtime environment (Temurin JRE 21).
    #>
    [CmdletBinding()]
    param([switch]$Force)

    # Check if Java is already installed
    try {
        & java -version 2>&1 | Out-Null
        if ($LASTEXITCODE -eq 0 -and -not $Force) {
            Write-Status -Message 'Java runtime is already installed' -Level 'Info'
            $javaVersion = & java -version 2>&1 | Select-Object -First 1
            Write-Verbose "Detected: $javaVersion"
            return $true
        }
    } catch {
        # Java not found, continue with installation
    }

    Write-Status -Message 'Installing Java runtime (Temurin JRE 21)...' -Level 'Info'
    $installed = $false

    # Try Winget first
    if (Test-CommandAvailable -Name 'winget') {
        $arguments = @('install', '--id', 'EclipseAdoptium.Temurin.21.JRE', '--silent', '--accept-package-agreements', '--accept-source-agreements')
        if ($Force) { $arguments += '--force' }
        
        $installed = Invoke-ExternalProcess -FilePath 'winget' -ArgumentList $arguments -RefreshEnvironment
    }

    # Try Chocolatey as fallback
    if ((-not $installed) -and (Test-CommandAvailable -Name 'choco')) {
        $arguments = @('install', 'temurin21jre', '-y', '--no-progress')
        if ($Force) { $arguments += '--force' }
        
        $installed = Invoke-ExternalProcess -FilePath 'choco' -ArgumentList $arguments -RefreshEnvironment
    }

    if ($installed) {
        # Verify installation
        Invoke-EnvironmentRefresh
        try {
            & java -version 2>&1 | Out-Null
            if ($LASTEXITCODE -eq 0) {
                Write-Status -Message 'Java runtime installed and verified successfully' -Level 'Success'
                return $true
            }
        } catch {
            Write-Verbose "Java verification failed"
        }
    }

    Write-Status -Message 'Java runtime installation failed or could not be verified' -Level 'Warning'
    return $false
}

# === PREREQUISITES TEST ===

function Test-Prerequisites {
    <#
    .SYNOPSIS
        Tests if prerequisites are installed and returns detailed status.
    
    .OUTPUTS
        [System.Collections.Specialized.OrderedDictionary] Dictionary of prerequisites status
    #>
    [CmdletBinding()]
    [OutputType([System.Collections.Specialized.OrderedDictionary])]
    param()

    # Refresh environment before testing
    Invoke-EnvironmentRefresh

    $results = [ordered]@{
        Chocolatey = @{
            Installed = Test-CommandAvailable -Name 'choco'
            Version = if (Test-CommandAvailable -Name 'choco') { 
                (& choco --version 2>&1 | Select-Object -First 1) 
            } else { 
                'Not installed' 
            }
        }
        PowerShell7 = @{
            Installed = ($PSVersionTable.PSVersion.Major -ge 7)
            Version = $PSVersionTable.PSVersion.ToString()
        }
        Winget = @{
            Installed = Test-CommandAvailable -Name 'winget'
            Version = if (Test-CommandAvailable -Name 'winget') { 
                try { (& winget --version 2>&1) } catch { 'Unknown' }
            } else { 
                'Not installed' 
            }
        }
        DotNet = @{
            Installed = $false
            Version = 'Not found'
        }
        DotNetFramework = @{
            Installed = $false
            Version = 'Not found'
        }
        Java = @{
            Installed = $false
            Version = 'Not found'
        }
        VCRedist = @{
            Installed = $false
            Version = 'Unknown'
        }
    }

    # Check .NET Core
    try {
        $dotnetInfo = & dotnet --list-runtimes 2>&1 | Out-String
        $results['DotNet'].Installed = $true
        $results['DotNet'].Version = $dotnetInfo.Trim()
    } catch {
        Write-Verbose ".NET Core not detected"
    }

    # Check .NET Framework
    try {
        $fxVersion = Get-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full' -ErrorAction Stop
        if ($fxVersion.Release) {
            $results['DotNetFramework'].Installed = $true
            $results['DotNetFramework'].Version = "4.8.1 (Release: $($fxVersion.Release))"
        }
    } catch {
        Write-Verbose ".NET Framework version not detected"
    }

    # Check Java
    try {
        $javaVersion = & java -version 2>&1 | Select-Object -First 1
        if ($LASTEXITCODE -eq 0) {
            $results['Java'].Installed = $true
            $results['Java'].Version = $javaVersion
        }
    } catch {
        Write-Verbose "Java not detected"
    }

    # Check VC++ Redist
    $vcRedistKeys = @(
        'HKLM:\SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64',
        'HKLM:\SOFTWARE\WOW6432Node\Microsoft\VisualStudio\14.0\VC\Runtimes\x64'
    )
    
    foreach ($key in $vcRedistKeys) {
        if (Test-Path $key) {
            $vcInfo = Get-ItemProperty -Path $key -ErrorAction SilentlyContinue
            if ($vcInfo) {
                $results['VCRedist'].Installed = $true
                $results['VCRedist'].Version = "2015-2022 ($($vcInfo.Version))"
                break
            }
        }
    }

    return $results
}

# === MAIN INSTALLATION WORKFLOW ===

function Start-PrerequisitesInstallation {
    <#
    .SYNOPSIS
        Installs all system prerequisites with automatic environment refresh.
    #>
    [CmdletBinding()]
    param([switch]$Force)

    Write-Status -Message 'Starting prerequisite installation workflow...' -Level 'Info'

    try {
        # Install package managers first
        Install-Chocolatey -Force:$Force | Out-Null
        Invoke-EnvironmentRefresh

        # Install PowerShell 7
        Install-PowerShell7 -Force:$Force | Out-Null
        
        # Install runtimes
        Write-Status -Message 'Installing runtime environments...' -Level 'Info'
        Install-DotNetRuntime -Force:$Force | Out-Null
        Install-VCRedist -Force:$Force | Out-Null
        Install-JavaRuntime -Force:$Force | Out-Null

        # Final environment refresh
        Write-Status -Message 'Performing final environment refresh...' -Level 'Info'
        Invoke-EnvironmentRefresh

        # Test and report results
        $results = Test-Prerequisites

        # FIXED: Use Write-Host for empty lines instead of Write-Status
        Write-Host ""
        Write-Status -Message '=== Prerequisite Installation Summary ===' -Level 'Info'
        
        foreach ($key in $results.Keys) {
            $status = if ($results[$key].Installed) { '[OK]' } else { '[MISSING]' }
            $statusLevel = if ($results[$key].Installed) { 'Success' } else { 'Warning' }
            Write-Status -Message "$status $key : $($results[$key].Version)" -Level $statusLevel
        }

        Write-Status -Message 'Prerequisite installation workflow completed' -Level 'Success'

        return $results
    } catch {
        Write-Status -Message "Prerequisite installation failed: $($_.Exception.Message)" -Level 'Error'
        throw
    }
}

# === MODULE EXPORTS ===

Export-ModuleMember -Function @(
    'Install-Chocolatey',
    'Install-PowerShell7',
    'Install-DotNetRuntime',
    'Install-VCRedist',
    'Install-JavaRuntime',
    'Test-Prerequisites',
    'Start-PrerequisitesInstallation',
    'Update-EnvironmentPath',
    'Invoke-EnvironmentRefresh'
)
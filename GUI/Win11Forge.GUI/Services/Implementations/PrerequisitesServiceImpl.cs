/*
 * Copyright 2026 Julien Bombled
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

#nullable enable

using System.Security.Principal;
using System.Text.Json;
using Win11Forge.GUI.Helpers;
using Win11Forge.GUI.Models;
using Win11Forge.GUI.Services.PowerShell;
using Loc = Win11Forge.GUI.Resources.Resources;

namespace Win11Forge.GUI.Services.Implementations;

/// <summary>
/// Implementation of IPrerequisitesService for managing system prerequisites.
/// </summary>
public class PrerequisitesServiceImpl : IPrerequisitesService
{
    private readonly IRepositoryPathService _pathService;
    private readonly IPowerShellExecutionService _executionService;
    private readonly IVersionService _versionService;
    private readonly ISystemInfoService _systemInfoService;

    /// <summary>
    /// Initializes a new instance of the PrerequisitesServiceImpl.
    /// </summary>
    public PrerequisitesServiceImpl(
        IRepositoryPathService pathService,
        IPowerShellExecutionService executionService,
        IVersionService versionService,
        ISystemInfoService systemInfoService)
    {
        _pathService = pathService ?? throw new ArgumentNullException(nameof(pathService));
        _executionService = executionService ?? throw new ArgumentNullException(nameof(executionService));
        _versionService = versionService ?? throw new ArgumentNullException(nameof(versionService));
        _systemInfoService = systemInfoService ?? throw new ArgumentNullException(nameof(systemInfoService));
    }

    /// <inheritdoc/>
    public async Task<PrerequisitesStatus> CheckPrerequisitesAsync()
    {
        // Refresh environment variables to pick up any changes from installations
        RefreshEnvironmentVariables();

        var script = @"
$result = @{}

# Check PowerShell 7
try {
    $ver = pwsh --version 2>$null
    $result.PowerShell7Installed = $null -ne $ver
    $result.PowerShellVersion = if ($ver) { $ver.Trim() } else { 'Not installed' }
} catch {
    Write-Warning ""PowerShell 7 check failed: $($_.Exception.Message)""
    $result.PowerShell7Installed = $false
    $result.PowerShellVersion = 'Not installed'
}

# Check Winget
try {
    $ver = winget --version 2>$null
    $result.WingetInstalled = $null -ne $ver
    $result.WingetVersion = if ($ver) { $ver.Trim() } else { 'Not installed' }
} catch {
    Write-Warning ""Winget check failed: $($_.Exception.Message)""
    $result.WingetInstalled = $false
    $result.WingetVersion = 'Not installed'
}

# Check Chocolatey
try {
    $ver = choco --version 2>$null
    $result.ChocolateyInstalled = $null -ne $ver
    $result.ChocolateyVersion = if ($ver) { $ver.Trim() } else { 'Not installed' }
} catch {
    Write-Warning ""Chocolatey check failed: $($_.Exception.Message)""
    $result.ChocolateyInstalled = $false
    $result.ChocolateyVersion = 'Not installed'
}

# Check .NET Core
try {
    $runtimes = dotnet --list-runtimes 2>$null
    $result.DotNetInstalled = $null -ne $runtimes -and $runtimes.Count -gt 0
    if ($result.DotNetInstalled) {
        $versions = @($runtimes | ForEach-Object { if ($_ -match 'Microsoft\.NETCore\.App (\d+\.\d+)') { $matches[1] } }) | Select-Object -Unique
        $result.DotNetVersion = ($versions -join ', ')
    } else {
        $result.DotNetVersion = 'Not installed'
    }
} catch {
    Write-Warning "".NET Core check failed: $($_.Exception.Message)""
    $result.DotNetInstalled = $false
    $result.DotNetVersion = 'Not installed'
}

# Check .NET Framework
try {
    $fxKey = Get-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full' -ErrorAction SilentlyContinue
    if ($fxKey -and $fxKey.Release) {
        $result.DotNetFrameworkInstalled = $true
        $releaseNum = $fxKey.Release
        $fxVersion = switch ($true) {
            ($releaseNum -ge 533320) { '4.8.1' }
            ($releaseNum -ge 528040) { '4.8' }
            ($releaseNum -ge 461808) { '4.7.2' }
            default { '4.x' }
        }
        $result.DotNetFrameworkVersion = $fxVersion
    } else {
        $result.DotNetFrameworkInstalled = $false
        $result.DotNetFrameworkVersion = 'Not installed'
    }
} catch {
    Write-Warning "".NET Framework check failed: $($_.Exception.Message)""
    $result.DotNetFrameworkInstalled = $false
    $result.DotNetFrameworkVersion = 'Not installed'
}

# Check Visual C++ Redistributable
try {
    $vcKeys = @(
        'HKLM:\SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64',
        'HKLM:\SOFTWARE\WOW6432Node\Microsoft\VisualStudio\14.0\VC\Runtimes\x64'
    )
    $vcInfo = $null
    foreach ($key in $vcKeys) {
        if (Test-Path $key) {
            $vcInfo = Get-ItemProperty -Path $key -ErrorAction SilentlyContinue
            if ($vcInfo) { break }
        }
    }
    if ($vcInfo -and $vcInfo.Version) {
        $result.VCRedistInstalled = $true
        $result.VCRedistVersion = ""2015-2022 ($($vcInfo.Version))""
    } else {
        $result.VCRedistInstalled = $false
        $result.VCRedistVersion = 'Not installed'
    }
} catch {
    Write-Warning ""Visual C++ Redistributable check failed: $($_.Exception.Message)""
    $result.VCRedistInstalled = $false
    $result.VCRedistVersion = 'Not installed'
}

# Check Java
try {
    $javaVer = java -version 2>&1 | Select-Object -First 1
    if ($LASTEXITCODE -eq 0 -and $javaVer) {
        $result.JavaInstalled = $true
        $result.JavaVersion = $javaVer.ToString().Trim()
    } else {
        $result.JavaInstalled = $false
        $result.JavaVersion = 'Not installed'
    }
} catch {
    Write-Warning ""Java check failed: $($_.Exception.Message)""
    $result.JavaInstalled = $false
    $result.JavaVersion = 'Not installed'
}

$result | ConvertTo-Json -Compress
";

        try
        {
            var output = await _executionService.ExecutePowerShellScriptAsync(script);
            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines.Reverse())
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("{") && trimmed.EndsWith("}"))
                {
                    using var doc = JsonDocument.Parse(trimmed);
                    var root = doc.RootElement;

                    return new PrerequisitesStatus
                    {
                        PowerShell7Installed = JsonHelper.GetJsonBool(root, "PowerShell7Installed"),
                        PowerShellVersion = JsonHelper.GetJsonString(root, "PowerShellVersion") ?? string.Empty,
                        WingetInstalled = JsonHelper.GetJsonBool(root, "WingetInstalled"),
                        WingetVersion = JsonHelper.GetJsonString(root, "WingetVersion") ?? string.Empty,
                        ChocolateyInstalled = JsonHelper.GetJsonBool(root, "ChocolateyInstalled"),
                        ChocolateyVersion = JsonHelper.GetJsonString(root, "ChocolateyVersion") ?? string.Empty,
                        DotNetInstalled = JsonHelper.GetJsonBool(root, "DotNetInstalled"),
                        DotNetVersion = JsonHelper.GetJsonString(root, "DotNetVersion") ?? string.Empty,
                        DotNetFrameworkInstalled = JsonHelper.GetJsonBool(root, "DotNetFrameworkInstalled"),
                        DotNetFrameworkVersion = JsonHelper.GetJsonString(root, "DotNetFrameworkVersion") ?? string.Empty,
                        VCRedistInstalled = JsonHelper.GetJsonBool(root, "VCRedistInstalled"),
                        VCRedistVersion = JsonHelper.GetJsonString(root, "VCRedistVersion") ?? string.Empty,
                        JavaInstalled = JsonHelper.GetJsonBool(root, "JavaInstalled"),
                        JavaVersion = JsonHelper.GetJsonString(root, "JavaVersion") ?? string.Empty
                    };
                }
            }
        }
        catch
        {
            // Return default status on error
        }

        return new PrerequisitesStatus();
    }

    /// <inheritdoc/>
    public async Task<bool> InstallPrerequisitesAsync(
        Action<string>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var prerequisitesModule = _pathService.GetPathForPowerShell("Modules", "Prerequisites.psm1");
        var corePath = _pathService.GetPathForPowerShell("Core", "Core.psm1");
        var localizationPath = _pathService.GetPathForPowerShell("Core", "Localization.psm1");
        var moduleLoaderPath = _pathService.GetPathForPowerShell("Core", "ModuleLoader.psm1");

        progressCallback?.Invoke(Loc.Prerequisites_Starting);

        try
        {
            progressCallback?.Invoke(Loc.Prerequisites_LoadingModules);

            var script = $@"
Set-ExecutionPolicy -Scope Process -ExecutionPolicy RemoteSigned -Force
$ErrorActionPreference = 'Continue'

try {{
    # Load core modules explicitly to ensure all dependencies are available
    Import-Module '{moduleLoaderPath}' -Force -ErrorAction SilentlyContinue
    Import-Module '{corePath}' -Force -ErrorAction SilentlyContinue
    Import-Module '{localizationPath}' -Force -ErrorAction SilentlyContinue
    Import-Module '{prerequisitesModule}' -Force -ErrorAction Stop

    # Run prerequisites installation
    $result = Start-PrerequisitesInstallation

    # Output success marker
    Write-Output '___SUCCESS___'
}} catch {{
    Write-Output ""___ERROR___: $($_.Exception.Message)""
}}
";

            progressCallback?.Invoke(Loc.Prerequisites_Installing);

            var result = await _executionService.ExecutePowerShellWithStreamingAsync(script, progressCallback, cancellationToken);

            if (result.Success)
            {
                // Refresh environment variables in current process
                RefreshEnvironmentVariables();
                progressCallback?.Invoke(Loc.Prerequisites_Complete);
                return true;
            }
            else
            {
                progressCallback?.Invoke($"Error: {result.ErrorMessage}");
                return false;
            }
        }
        catch (Exception ex)
        {
            progressCallback?.Invoke($"Exception: {ex.Message}");
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> InstallPrerequisiteAsync(
        string prerequisiteName,
        Action<string>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        // Validate prerequisite name
        var validPrerequisites = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "PowerShell7",
            "Chocolatey",
            "Winget",
            "DotNet",
            "DotNetFramework",
            "VCRedist",
            "Java"
        };

        if (!validPrerequisites.Contains(prerequisiteName))
        {
            throw new ArgumentException($"Unknown prerequisite: {prerequisiteName}", nameof(prerequisiteName));
        }

        progressCallback?.Invoke($"Installing {prerequisiteName}...");

        // For now, delegate to full prerequisites installation
        var result = await InstallPrerequisitesAsync(progressCallback, cancellationToken);
        return result;
    }

    /// <inheritdoc/>
    public Task<SystemInfoModel> GetSystemInfoAsync()
    {
        return _systemInfoService.GetSystemInfoAsync();
    }

    /// <inheritdoc/>
    public Task<string> GetWin11ForgeVersionAsync()
    {
        return _versionService.GetWin11ForgeVersionAsync();
    }

    /// <inheritdoc/>
    public async Task<bool> IsPowerShell7AvailableAsync()
    {
        var prereqs = await CheckPrerequisitesAsync();
        return prereqs.PowerShell7Installed;
    }

    /// <inheritdoc/>
    public bool IsRunningAsAdministrator()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    /// <summary>
    /// Refreshes environment variables in the current process from the registry.
    /// This is needed after installing software that modifies PATH.
    /// </summary>
    private static void RefreshEnvironmentVariables()
    {
        try
        {
            using var machineKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Control\Session Manager\Environment");
            using var userKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Environment");

            var machinePath = machineKey?.GetValue("Path", "", Microsoft.Win32.RegistryValueOptions.DoNotExpandEnvironmentNames) as string ?? "";
            var userPath = userKey?.GetValue("Path", "", Microsoft.Win32.RegistryValueOptions.DoNotExpandEnvironmentNames) as string ?? "";

            var combinedPath = $"{machinePath};{userPath}";
            combinedPath = Environment.ExpandEnvironmentVariables(combinedPath);
            while (combinedPath.Contains(";;"))
            {
                combinedPath = combinedPath.Replace(";;", ";");
            }

            Environment.SetEnvironmentVariable("Path", combinedPath, EnvironmentVariableTarget.Process);

            var commonVars = new[] { "JAVA_HOME", "ChocolateyInstall", "DOTNET_ROOT" };
            foreach (var varName in commonVars)
            {
                var machineValue = machineKey?.GetValue(varName) as string;
                var userValue = userKey?.GetValue(varName) as string;
                var value = userValue ?? machineValue;
                if (!string.IsNullOrEmpty(value))
                {
                    Environment.SetEnvironmentVariable(varName, value, EnvironmentVariableTarget.Process);
                }
            }
        }
        catch
        {
            // Environment refresh is non-critical
        }
    }
}

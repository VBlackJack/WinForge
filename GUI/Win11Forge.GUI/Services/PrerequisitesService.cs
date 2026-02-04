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
using Win11Forge.GUI.Models;

namespace Win11Forge.GUI.Services;

/// <summary>
/// Service for managing system prerequisites.
/// Wraps IPowerShellBridge for prerequisites operations to follow SRP.
/// </summary>
public class PrerequisitesService : IPrerequisitesService
{
    private readonly IPowerShellBridge _powerShellBridge;

    /// <summary>
    /// Initializes a new instance of the PrerequisitesService.
    /// </summary>
    /// <param name="powerShellBridge">The PowerShell bridge for executing commands</param>
    public PrerequisitesService(IPowerShellBridge powerShellBridge)
    {
        _powerShellBridge = powerShellBridge ?? throw new ArgumentNullException(nameof(powerShellBridge));
    }

    /// <inheritdoc/>
    public Task<PrerequisitesStatus> CheckPrerequisitesAsync()
    {
        return _powerShellBridge.CheckPrerequisitesAsync();
    }

    /// <inheritdoc/>
    public Task<bool> InstallPrerequisitesAsync(
        Action<string>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        return _powerShellBridge.InstallPrerequisitesAsync(progressCallback, cancellationToken);
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
        // In future, this could be expanded to install individual prerequisites
        var result = await _powerShellBridge.InstallPrerequisitesAsync(progressCallback, cancellationToken);
        return result;
    }

    /// <inheritdoc/>
    public Task<SystemInfoModel> GetSystemInfoAsync()
    {
        return _powerShellBridge.GetSystemInfoAsync();
    }

    /// <inheritdoc/>
    public Task<string> GetWin11ForgeVersionAsync()
    {
        return _powerShellBridge.GetWin11ForgeVersionAsync();
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
}

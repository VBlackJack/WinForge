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

using Win11Forge.GUI.Models;

namespace Win11Forge.GUI.Services;

/// <summary>
/// Null-object implementation of IPowerShellBridge for XAML design-time support.
/// Returns safe default values without executing any PowerShell operations.
/// </summary>
internal sealed class DesignTimePowerShellBridge : IPowerShellBridge
{
    /// <inheritdoc/>
    public string RepositoryRoot => Environment.CurrentDirectory;

    #region IVersionService

    /// <inheritdoc/>
    public Task<string> GetWin11ForgeVersionAsync() => Task.FromResult("3.6.0 (Design)");

    #endregion

    #region ISystemInfoService

    /// <inheritdoc/>
    public Task<SystemInfoModel> GetSystemInfoAsync() => Task.FromResult(new SystemInfoModel
    {
        Hostname = "DESIGN-PC",
        Username = "DesignUser",
        WindowsVersion = "Windows 11 (Design)",
        WindowsBuild = "00000.0000",
        IsAdministrator = false,
        TotalMemoryGB = 16,
        ProcessorCount = 8
    });

    #endregion

    #region IProfileManagementService

    /// <inheritdoc/>
    public Task<List<string>> GetAvailableProfilesAsync() => Task.FromResult(new List<string> { "Base", "Office", "Gaming" });

    /// <inheritdoc/>
    public Task<DeploymentProfileModel> LoadProfileAsync(string profileName) => Task.FromResult(CreateDesignTimeProfile(profileName));

    /// <inheritdoc/>
    public Task<DeploymentProfileModel> GetRawProfileAsync(string profileName) => Task.FromResult(CreateDesignTimeProfile(profileName));

    /// <inheritdoc/>
    public Task<DeploymentProfileModel> GetResolvedProfileAsync(string profileName) => Task.FromResult(CreateDesignTimeProfile(profileName));

    /// <inheritdoc/>
    public Task SaveProfileAsync(string profileName, string description, string? parentProfile, List<string> addedAppIds) => Task.CompletedTask;

    private static DeploymentProfileModel CreateDesignTimeProfile(string name) => new()
    {
        Name = name,
        Description = $"Design-time {name} profile",
        Version = "3.6.0",
        Applications = []
    };

    #endregion

    #region IApplicationManagementService

    /// <inheritdoc/>
    public Task<List<ApplicationModel>> GetAllApplicationsAsync() => Task.FromResult(new List<ApplicationModel>());

    /// <inheritdoc/>
    public Task<ApplicationStatus> GetApplicationStatusAsync(string appId) => Task.FromResult(ApplicationStatus.Pending);

    /// <inheritdoc/>
    public Task<Dictionary<string, BatchAppStatus>?> GetBatchApplicationStatusAsync(IReadOnlyList<ApplicationModel> apps) => Task.FromResult<Dictionary<string, BatchAppStatus>?>(null);

    /// <inheritdoc/>
    public Task<InstallResult> InstallApplicationAsync(ApplicationModel app, bool isDryRun, bool forceUpdate = false, Action<string>? progressCallback = null)
        => Task.FromResult(new InstallResult { Success = false, Message = "Design-time mode" });

    /// <inheritdoc/>
    public Task<InstallResult> UninstallApplicationAsync(ApplicationModel app, Action<string>? progressCallback = null)
        => Task.FromResult(new InstallResult { Success = false, Message = "Design-time mode" });

    /// <inheritdoc/>
    public Task<UpdateCheckResult> CheckApplicationUpdateAsync(ApplicationModel app)
        => Task.FromResult(new UpdateCheckResult { HasUpdate = false, CurrentVersion = "0.0.0", AvailableVersion = "0.0.0" });

    /// <inheritdoc/>
    public Task<InstallResult> UpdateApplicationAsync(ApplicationModel app, Action<string>? progressCallback = null)
        => Task.FromResult(new InstallResult { Success = false, Message = "Design-time mode" });

    /// <inheritdoc/>
    public Task<bool> LaunchApplicationAsync(ApplicationModel app) => Task.FromResult(false);

    #endregion

    #region Prerequisites

    /// <inheritdoc/>
    public Task<PrerequisitesStatus> CheckPrerequisitesAsync() => Task.FromResult(new PrerequisitesStatus
    {
        PowerShell7Installed = true,
        WingetInstalled = true,
        ChocolateyInstalled = true,
        DotNetInstalled = true
    });

    /// <inheritdoc/>
    public Task<bool> InstallPrerequisitesAsync(Action<string>? progressCallback = null, CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    #endregion

    #region Script Execution

    /// <inheritdoc/>
    public Task<string> ExecuteScriptAsync(string relativePath, CancellationToken cancellationToken = default)
        => Task.FromResult("Design-time mode - no script execution");

    /// <inheritdoc/>
    public Task<string> ExecuteCommandAsync(string command, CancellationToken cancellationToken = default)
        => Task.FromResult("Design-time mode - no command execution");

    #endregion
}

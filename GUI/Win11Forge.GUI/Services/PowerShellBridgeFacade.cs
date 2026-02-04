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
using Win11Forge.GUI.Services.PowerShell;

namespace Win11Forge.GUI.Services;

/// <summary>
/// Facade implementation of IPowerShellBridge that delegates to specialized services.
/// Follows the Interface Segregation Principle by composing focused services.
/// </summary>
public class PowerShellBridgeFacade : IPowerShellBridge, IDisposable
{
    private readonly IRepositoryPathService _pathService;
    private readonly IPowerShellExecutionService _executionService;
    private readonly IVersionService _versionService;
    private readonly IProfileManagementService _profileService;
    private readonly IApplicationManagementService _appService;
    private readonly ISystemInfoService _systemInfoService;
    private readonly IPrerequisitesService _prerequisitesService;
    private readonly IApplicationDetectionService _detectionService;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the PowerShellBridgeFacade.
    /// </summary>
    public PowerShellBridgeFacade(
        IRepositoryPathService pathService,
        IPowerShellExecutionService executionService,
        IVersionService versionService,
        IProfileManagementService profileService,
        IApplicationManagementService appService,
        ISystemInfoService systemInfoService,
        IPrerequisitesService prerequisitesService,
        IApplicationDetectionService detectionService)
    {
        _pathService = pathService ?? throw new ArgumentNullException(nameof(pathService));
        _executionService = executionService ?? throw new ArgumentNullException(nameof(executionService));
        _versionService = versionService ?? throw new ArgumentNullException(nameof(versionService));
        _profileService = profileService ?? throw new ArgumentNullException(nameof(profileService));
        _appService = appService ?? throw new ArgumentNullException(nameof(appService));
        _systemInfoService = systemInfoService ?? throw new ArgumentNullException(nameof(systemInfoService));
        _prerequisitesService = prerequisitesService ?? throw new ArgumentNullException(nameof(prerequisitesService));
        _detectionService = detectionService ?? throw new ArgumentNullException(nameof(detectionService));
    }

    #region ISystemInfoService

    /// <inheritdoc/>
    public string RepositoryRoot => _pathService.RepositoryRoot;

    /// <inheritdoc/>
    public Task<SystemInfoModel> GetSystemInfoAsync() => _systemInfoService.GetSystemInfoAsync();

    #endregion

    #region IVersionService

    /// <inheritdoc/>
    public Task<string> GetWin11ForgeVersionAsync() => _versionService.GetWin11ForgeVersionAsync();

    #endregion

    #region IProfileManagementService

    /// <inheritdoc/>
    public Task<List<string>> GetAvailableProfilesAsync() => _profileService.GetAvailableProfilesAsync();

    /// <inheritdoc/>
    public Task<DeploymentProfileModel> LoadProfileAsync(string profileName) => _profileService.LoadProfileAsync(profileName);

    /// <inheritdoc/>
    public Task<DeploymentProfileModel> GetRawProfileAsync(string profileName) => _profileService.GetRawProfileAsync(profileName);

    /// <inheritdoc/>
    public Task<DeploymentProfileModel> GetResolvedProfileAsync(string profileName) => _profileService.GetResolvedProfileAsync(profileName);

    /// <inheritdoc/>
    public Task SaveProfileAsync(string profileName, string description, string? parentProfile, List<string> addedAppIds)
        => _profileService.SaveProfileAsync(profileName, description, parentProfile, addedAppIds);

    #endregion

    #region IApplicationManagementService

    /// <inheritdoc/>
    public Task<List<ApplicationModel>> GetAllApplicationsAsync() => _appService.GetAllApplicationsAsync();

    /// <inheritdoc/>
    public Task<ApplicationStatus> GetApplicationStatusAsync(string appId) => _appService.GetApplicationStatusAsync(appId);

    /// <inheritdoc/>
    public Task<Dictionary<string, BatchAppStatus>?> GetBatchApplicationStatusAsync(IReadOnlyList<ApplicationModel> apps)
        => _appService.GetBatchApplicationStatusAsync(apps);

    /// <inheritdoc/>
    public Task<InstallResult> InstallApplicationAsync(ApplicationModel app, bool isDryRun, bool forceUpdate = false, Action<string>? progressCallback = null)
        => _appService.InstallApplicationAsync(app, isDryRun, forceUpdate, progressCallback);

    /// <inheritdoc/>
    public Task<InstallResult> UninstallApplicationAsync(ApplicationModel app, Action<string>? progressCallback = null)
        => _appService.UninstallApplicationAsync(app, progressCallback);

    /// <inheritdoc/>
    public Task<UpdateCheckResult> CheckApplicationUpdateAsync(ApplicationModel app)
        => _appService.CheckApplicationUpdateAsync(app);

    /// <inheritdoc/>
    public Task<InstallResult> UpdateApplicationAsync(ApplicationModel app, Action<string>? progressCallback = null)
        => _appService.UpdateApplicationAsync(app, progressCallback);

    /// <inheritdoc/>
    public Task<bool> LaunchApplicationAsync(ApplicationModel app) => _appService.LaunchApplicationAsync(app);

    #endregion

    #region Prerequisites

    /// <inheritdoc/>
    public Task<PrerequisitesStatus> CheckPrerequisitesAsync() => _prerequisitesService.CheckPrerequisitesAsync();

    /// <inheritdoc/>
    public Task<bool> InstallPrerequisitesAsync(Action<string>? progressCallback = null, CancellationToken cancellationToken = default)
        => _prerequisitesService.InstallPrerequisitesAsync(progressCallback, cancellationToken);

    #endregion

    #region Script Execution

    /// <inheritdoc/>
    public async Task<string> ExecuteScriptAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var scriptPath = _pathService.GetPath(relativePath);

        if (!System.IO.File.Exists(scriptPath))
        {
            throw new System.IO.FileNotFoundException($"Script not found: {scriptPath}");
        }

        var script = $@"
Set-ExecutionPolicy -Scope Process -ExecutionPolicy RemoteSigned -Force
& '{scriptPath.Replace("\\", "/")}'
";

        return await _executionService.ExecutePowerShellScriptAsync(script, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<string> ExecuteCommandAsync(string command, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var repoRoot = _pathService.GetSafeRepositoryRoot();

        var script = $@"
Set-Location '{repoRoot.Replace("\\", "/")}'
{command}
";

        return await _executionService.ExecutePowerShellScriptAsync(script, cancellationToken);
    }

    #endregion

    #region Detection Cache Management

    /// <summary>
    /// Pre-warms the application detection cache for faster subsequent operations.
    /// </summary>
    public async Task WarmDetectionCacheAsync()
    {
        await _detectionService.WarmCacheAsync();
    }

    /// <summary>
    /// Gets cache statistics for diagnostics.
    /// </summary>
    public CacheStatistics GetDetectionCacheStatistics()
    {
        return _detectionService.GetCacheStatistics();
    }

    /// <summary>
    /// Clears the detection cache.
    /// </summary>
    public void ClearDetectionCache()
    {
        _detectionService.ClearCache();
    }

    /// <summary>
    /// Gets available updates using optimized batch detection.
    /// </summary>
    public async Task<IReadOnlyList<UpdateInfo>> GetAvailableUpdatesAsync()
    {
        return await _detectionService.GetAvailableUpdatesAsync();
    }

    #endregion

    #region IDisposable

    /// <summary>
    /// Releases all resources used by the PowerShellBridgeFacade.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases the unmanaged resources and optionally releases managed resources.
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            // Dispose of any disposable services if they implement IDisposable
            if (_pathService is IDisposable disposablePath)
                disposablePath.Dispose();
        }

        _disposed = true;
    }

    #endregion
}

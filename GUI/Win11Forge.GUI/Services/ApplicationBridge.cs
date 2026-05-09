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

using System.IO;
using Win11Forge.GUI.Models;

namespace Win11Forge.GUI.Services;

/// <summary>
/// Implementation of application management operations.
/// Wraps IPowerShellBridge for core operations and adds application-specific logic.
/// </summary>
public class ApplicationBridge : IApplicationBridge
{
    private readonly IPowerShellBridge _powerShellBridge;
    private readonly string _databasePath;

    /// <summary>
    /// Initializes a new instance of ApplicationBridge.
    /// </summary>
    /// <param name="powerShellBridge">PowerShell bridge for script execution</param>
    public ApplicationBridge(IPowerShellBridge powerShellBridge)
    {
        _powerShellBridge = powerShellBridge ?? throw new ArgumentNullException(nameof(powerShellBridge));
        _databasePath = Path.Combine(powerShellBridge.RepositoryRoot, "Apps", "Database", "applications.json");
    }

    /// <inheritdoc/>
    public string DatabasePath => _databasePath;

    /// <inheritdoc/>
    public async Task<List<ApplicationModel>> GetAllApplicationsAsync()
    {
        return await _powerShellBridge.GetAllApplicationsAsync();
    }

    /// <inheritdoc/>
    public async Task<List<ApplicationModel>> GetApplicationsByCategoryAsync(string category)
    {
        var allApps = await GetAllApplicationsAsync();
        return allApps
            .Where(a => string.Equals(a.Category, category, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <inheritdoc/>
    public async Task<List<string>> GetCategoriesAsync()
    {
        var allApps = await GetAllApplicationsAsync();
        return allApps
            .Select(a => a.Category)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c)
            .ToList()!;
    }

    /// <inheritdoc/>
    public async Task<ApplicationStatus> GetApplicationStatusAsync(string appId)
    {
        if (string.IsNullOrWhiteSpace(appId))
        {
            throw new ArgumentException(
                Win11Forge.GUI.Resources.Resources.Validation_Required,
                nameof(appId));
        }

        return await _powerShellBridge.GetApplicationStatusAsync(appId);
    }

    /// <inheritdoc/>
    public async Task<Dictionary<string, BatchAppStatus>?> GetBatchApplicationStatusAsync(IReadOnlyList<ApplicationModel> apps)
    {
        if (apps == null || apps.Count == 0)
        {
            return new Dictionary<string, BatchAppStatus>();
        }

        return await _powerShellBridge.GetBatchApplicationStatusAsync(apps);
    }

    /// <inheritdoc/>
    public async Task<InstallResult> InstallApplicationAsync(
        ApplicationModel app,
        bool isDryRun,
        bool forceUpdate = false,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ValidateApplication(app);

        Action<string>? progressCallback = progress != null
            ? msg => progress.Report(msg)
            : null;

        return await _powerShellBridge.InstallApplicationAsync(app, isDryRun, forceUpdate, progressCallback);
    }

    /// <inheritdoc/>
    public async Task<InstallResult> UninstallApplicationAsync(
        ApplicationModel app,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ValidateApplication(app);

        Action<string>? progressCallback = progress != null
            ? msg => progress.Report(msg)
            : null;

        return await _powerShellBridge.UninstallApplicationAsync(app, progressCallback);
    }

    /// <inheritdoc/>
    public async Task<InstallResult> UpdateApplicationAsync(
        ApplicationModel app,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ValidateApplication(app);

        Action<string>? progressCallback = progress != null
            ? msg => progress.Report(msg)
            : null;

        return await _powerShellBridge.UpdateApplicationAsync(app, progressCallback);
    }

    /// <inheritdoc/>
    public async Task<UpdateCheckResult> CheckApplicationUpdateAsync(ApplicationModel app)
    {
        ValidateApplication(app);
        return await _powerShellBridge.CheckApplicationUpdateAsync(app);
    }

    /// <inheritdoc/>
    public async Task<bool> LaunchApplicationAsync(ApplicationModel app)
    {
        ValidateApplication(app);
        return await _powerShellBridge.LaunchApplicationAsync(app);
    }

    /// <summary>
    /// Validates an application model.
    /// </summary>
    private static void ValidateApplication(ApplicationModel app)
    {
        if (app == null)
        {
            throw new ArgumentNullException(nameof(app));
        }

        if (string.IsNullOrWhiteSpace(app.AppId))
        {
            throw new ArgumentException(
                Win11Forge.GUI.Resources.Resources.Validation_Required,
                nameof(app.AppId));
        }
    }
}

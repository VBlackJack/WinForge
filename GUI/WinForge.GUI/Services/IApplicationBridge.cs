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

using WinForge.GUI.Models;

namespace WinForge.GUI.Services;

/// <summary>
/// Interface for application management operations.
/// Separates application concerns from general PowerShell operations for better SRP and testability.
/// </summary>
public interface IApplicationBridge
{
    /// <summary>
    /// Gets all applications from the database.
    /// </summary>
    /// <returns>List of all applications</returns>
    Task<List<ApplicationModel>> GetAllApplicationsAsync();

    /// <summary>
    /// Gets applications filtered by category.
    /// </summary>
    /// <param name="category">Category name to filter by</param>
    /// <returns>List of applications in the specified category</returns>
    Task<List<ApplicationModel>> GetApplicationsByCategoryAsync(string category);

    /// <summary>
    /// Gets all available categories.
    /// </summary>
    /// <returns>List of category names</returns>
    Task<List<string>> GetCategoriesAsync();

    /// <summary>
    /// Checks if an application is installed on the system.
    /// </summary>
    /// <param name="appId">Application ID to check</param>
    /// <returns>ApplicationStatus indicating installed state</returns>
    Task<ApplicationStatus> GetApplicationStatusAsync(string appId);

    /// <summary>
    /// Checks installation status for multiple applications in a single batch operation.
    /// Uses optimized caching of Registry, Winget, and AppX data for faster detection.
    /// </summary>
    /// <param name="apps">List of applications to check</param>
    /// <returns>Dictionary mapping AppId to BatchAppStatus (status + version). Returns null if batch detection fails.</returns>
    Task<Dictionary<string, BatchAppStatus>?> GetBatchApplicationStatusAsync(IReadOnlyList<ApplicationModel> apps);

    /// <summary>
    /// Installs a single application.
    /// </summary>
    /// <param name="app">Application model to install</param>
    /// <param name="isDryRun">If true, simulates installation without making changes</param>
    /// <param name="forceUpdate">If true, attempts to upgrade already installed apps</param>
    /// <param name="progress">Optional progress reporting</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Installation result</returns>
    Task<InstallResult> InstallApplicationAsync(
        ApplicationModel app,
        bool isDryRun,
        bool forceUpdate = false,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Uninstalls a single application.
    /// </summary>
    /// <param name="app">Application model to uninstall</param>
    /// <param name="progress">Optional progress reporting</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Uninstallation result</returns>
    Task<InstallResult> UninstallApplicationAsync(
        ApplicationModel app,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a single application to the latest version.
    /// </summary>
    /// <param name="app">Application model to update</param>
    /// <param name="progress">Optional progress reporting</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Update result</returns>
    Task<InstallResult> UpdateApplicationAsync(
        ApplicationModel app,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if an application has an update available.
    /// </summary>
    /// <param name="app">Application model to check</param>
    /// <returns>Update check result with version info</returns>
    Task<UpdateCheckResult> CheckApplicationUpdateAsync(ApplicationModel app);

    /// <summary>
    /// Launches an installed application.
    /// </summary>
    /// <param name="app">The application to launch</param>
    /// <returns>True if launched successfully, false otherwise</returns>
    Task<bool> LaunchApplicationAsync(ApplicationModel app);

    /// <summary>
    /// Gets the database path.
    /// </summary>
    string DatabasePath { get; }
}

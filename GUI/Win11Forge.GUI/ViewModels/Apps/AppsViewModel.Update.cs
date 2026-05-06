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

using CommunityToolkit.Mvvm.Input;
using Win11Forge.GUI.Models;

namespace Win11Forge.GUI.ViewModels;

public partial class AppsViewModel
{
    /// <summary>
    /// Whether the ScanUpdates command can execute.
    /// Requires installed apps and no active scanning.
    /// </summary>
    private bool CanScanUpdates => !IsScanningUpdates && !IsScanning && InstalledCount > 0;

    /// <summary>
    /// Re-scans installed applications for available updates only.
    /// Faster than full scan as it skips installation detection.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanScanUpdates))]
    private async Task ScanUpdatesAsync()
    {
        IsScanningUpdates = true;
        var localUpdateCount = 0;

        try
        {
            // Get installed apps only (includes UpdateAvailable which are also installed)
            var installedApps = _allApplications
                .Where(a => a.Status == ApplicationStatus.Installed ||
                           a.Status == ApplicationStatus.AlreadyInstalled ||
                           a.Status == ApplicationStatus.UpdateAvailable)
                .ToList();

            if (installedApps.Count == 0) return;

            // Check updates in parallel with semaphore
            var tasks = installedApps.Select(async app =>
            {
                await _scanSemaphore.WaitAsync();
                try
                {
                    app.StatusMessage = Resources.Resources.Common_Loading;
                    var result = await _powerShellBridge.CheckApplicationUpdateAsync(app);

                    if (result.HasUpdate)
                    {
                        app.Status = ApplicationStatus.UpdateAvailable;
                        app.CurrentVersion = result.CurrentVersion;
                        app.AvailableVersion = result.AvailableVersion;
                        app.StatusMessage = Resources.Resources.Status_UpdateAvailable;
                        Interlocked.Increment(ref localUpdateCount);
                    }
                    else
                    {
                        // No update - mark as installed
                        app.Status = ApplicationStatus.Installed;
                        app.CurrentVersion = result.CurrentVersion;
                        app.AvailableVersion = string.Empty;
                        app.StatusMessage = Resources.Resources.Status_Installed;
                    }
                }
                finally
                {
                    _scanSemaphore.Release();
                }
            });

            await Task.WhenAll(tasks);

            // Update count on UI thread
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                UpdatesAvailableCount = localUpdateCount;
            });

            // Apply filter to refresh view
            ApplyFilter();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsScanningUpdates = false;
        }
    }

    /// <summary>
    /// Updates a single application.
    /// </summary>
    [RelayCommand]
    private async Task UpdateAppAsync(ApplicationModel? app)
    {
        if (app == null) return;

        await _installSemaphore.WaitAsync();

        try
        {
            app.Status = ApplicationStatus.Updating;
            app.StatusMessage = Resources.Resources.Status_Updating;

            var result = await _powerShellBridge.UpdateApplicationAsync(
                app,
                progress => app.StatusMessage = progress);

            app.LogOutput = result.Logs;

            if (result.Success)
            {
                // Refresh version info after successful update
                var updateCheck = await _powerShellBridge.CheckApplicationUpdateAsync(app);

                // Only update version if we got a valid one, otherwise keep the previous AvailableVersion as current
                if (!string.IsNullOrEmpty(updateCheck.CurrentVersion))
                {
                    app.CurrentVersion = updateCheck.CurrentVersion;
                }
                else if (!string.IsNullOrEmpty(app.AvailableVersion))
                {
                    // Use the AvailableVersion we were updating to as the new CurrentVersion
                    app.CurrentVersion = app.AvailableVersion;
                }

                app.AvailableVersion = string.Empty;
                app.Status = ApplicationStatus.Installed;
                app.StatusMessage = Resources.Resources.Status_Installed;

                if (UpdatesAvailableCount > 0)
                {
                    UpdatesAvailableCount--;
                }
            }
            else
            {
                app.Status = ApplicationStatus.Failed;
                app.StatusMessage = Resources.Resources.Status_Failed;
                app.ErrorMessage = result.Message;
            }
        }
        catch (Exception ex)
        {
            app.Status = ApplicationStatus.Failed;
            app.StatusMessage = Resources.Resources.Status_Failed;
            app.ErrorMessage = ex.Message;
        }
        finally
        {
            _installSemaphore.Release();
        }
    }

    /// <summary>
    /// Whether the UpdateSelected command can execute.
    /// </summary>
    private bool CanUpdateSelected => UpdatesAvailableCount > 0 && !IsInstalling && !IsUninstalling;

    /// <summary>
    /// Updates all applications with available updates.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanUpdateSelected))]
    private async Task UpdateSelectedAsync()
    {
        var appsWithUpdates = _allApplications
            .Where(a => a.Status == ApplicationStatus.UpdateAvailable && a.IsSelected)
            .ToList();

        if (appsWithUpdates.Count == 0)
        {
            // If none selected, update all with updates
            appsWithUpdates = _allApplications
                .Where(a => a.Status == ApplicationStatus.UpdateAvailable)
                .ToList();
        }

        if (appsWithUpdates.Count == 0) return;

        IsInstalling = true;

        try
        {
            foreach (var app in appsWithUpdates)
            {
                await UpdateAppAsync(app);
            }
        }
        finally
        {
            IsInstalling = false;
            ApplyFilter();
        }
    }
}

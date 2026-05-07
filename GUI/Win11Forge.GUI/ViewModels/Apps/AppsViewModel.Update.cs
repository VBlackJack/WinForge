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
using Win11Forge.GUI.Services.Coordinators;

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
        _lastOperationType = "scanupdates";
        IsScanningUpdates = true;

        try
        {
            // Get installed apps only (includes UpdateAvailable which are also installed)
            var installedApps = _allApplications
                .Where(a => a.Status == ApplicationStatus.Installed ||
                           a.Status == ApplicationStatus.AlreadyInstalled ||
                           a.Status == ApplicationStatus.UpdateAvailable)
                .ToList();

            if (installedApps.Count == 0)
            {
                _lastOperationType = string.Empty;
                return;
            }

            var result = await _updateCoordinator.ScanForUpdatesAsync(installedApps);
            UpdatesAvailableCount = result.UpdatesAvailableCount;

            // Apply filter to refresh view
            ApplyFilter();
            _lastOperationType = string.Empty;
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

        try
        {
            var result = await _updateCoordinator.UpdateAsync([app]);
            if (result.UpdatedCount > 0 && UpdatesAvailableCount > 0)
            {
                UpdatesAvailableCount--;
            }
        }
        catch (Exception ex)
        {
            app.Status = ApplicationStatus.Failed;
            app.StatusMessage = Resources.Resources.Status_Failed;
            app.ErrorMessage = ex.Message;
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
            var result = await _updateCoordinator.UpdateAsync(
                appsWithUpdates,
                new Progress<AppOperationProgress>(_ => { }));
            if (result.UpdatedCount > 0)
            {
                UpdatesAvailableCount = Math.Max(0, UpdatesAvailableCount - result.UpdatedCount);
            }
        }
        finally
        {
            IsInstalling = false;
            ApplyFilter();
        }
    }
}

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

using System.Diagnostics;
using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using Win11Forge.GUI.Models;

namespace Win11Forge.GUI.ViewModels;

public partial class AppsViewModel
{
    /// <summary>
    /// Opens the log viewer for a specific application.
    /// </summary>
    [RelayCommand]
    private void ViewLogs(ApplicationModel? app)
    {
        if (app == null || string.IsNullOrEmpty(app.LogOutput)) return;
        LogViewerApplication = app;
        IsLogViewerOpen = true;
    }

    /// <summary>
    /// Closes the log viewer dialog.
    /// </summary>
    [RelayCommand]
    private void CloseLogViewer()
    {
        IsLogViewerOpen = false;
        LogViewerApplication = null;
    }

    /// <summary>
    /// Opens the official website for manual installation.
    /// </summary>
    [RelayCommand]
    private void OpenWebsite(ApplicationModel? app)
    {
        if (app == null || string.IsNullOrEmpty(app.OfficialUrl)) return;

        try
        {
            using Process? process = Process.Start(new ProcessStartInfo
            {
                FileName = app.OfficialUrl,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _toastService?.ShowWarning(string.Format(
                CultureInfo.CurrentCulture,
                Resources.Resources.Apps_Toast_OpenWebsiteFailed,
                app.Name,
                ex.Message));
            Debug.WriteLine($"Failed to open website: {ex.Message}");
        }
    }

    /// <summary>
    /// Launches an installed application.
    /// </summary>
    [RelayCommand]
    private async Task LaunchAppAsync(ApplicationModel? app)
    {
        if (app == null) return;

        // Only launch installed apps
        if (app.Status != ApplicationStatus.Installed &&
            app.Status != ApplicationStatus.AlreadyInstalled &&
            app.Status != ApplicationStatus.UpdateAvailable)
        {
            return;
        }

        try
        {
            // Try to find and launch the application
            bool launched = await _powerShellBridge.LaunchApplicationAsync(app);

            if (!launched)
            {
                // Fallback: try to open by name using shell
                ErrorMessage = string.Format(
                    Resources.Resources.Error_LaunchFailed,
                    app.Name);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    /// <summary>
    /// Copies the application ID to clipboard.
    /// </summary>
    [RelayCommand]
    private void CopyAppId(ApplicationModel? app)
    {
        if (app == null || string.IsNullOrEmpty(app.AppId)) return;

        try
        {
            System.Windows.Clipboard.SetText(app.AppId);
        }
        catch (Exception ex)
        {
            _toastService?.ShowWarning(string.Format(
                CultureInfo.CurrentCulture,
                Resources.Resources.Apps_Toast_CopyAppIdFailed,
                ex.Message));
        }
    }

    /// <summary>
    /// Toggles the favorite status of an application.
    /// </summary>
    [RelayCommand]
    private void ToggleFavorite(ApplicationModel? app)
    {
        if (app == null) return;
        app.IsFavorite = !app.IsFavorite;
        FavoritesCount = _allApplications.Count(a => a.IsFavorite);

        // Refresh filter if currently viewing favorites
        if (SelectedStatusFilter == StatusFilterOption.Favorites)
        {
            ApplyFilter();
        }
    }
}

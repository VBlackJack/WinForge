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
    /// Selects all applications with updates available.
    /// </summary>
    [RelayCommand]
    private void SelectWithUpdates()
    {
        foreach (var app in _allApplications)
        {
            app.IsSelected = app.Status == ApplicationStatus.UpdateAvailable;
        }

        SelectedCount = _allApplications.Count(a => a.IsSelected);
    }

    /// <summary>
    /// Selects all visible (filtered) applications.
    /// </summary>
    [RelayCommand]
    private void SelectAll()
    {
        foreach (var app in FilteredApplications.Cast<ApplicationModel>())
        {
            app.IsSelected = true;
        }
        UpdateSelectedCount();
    }

    /// <summary>
    /// Deselects all applications.
    /// </summary>
    [RelayCommand]
    private void SelectNone()
    {
        foreach (var app in _allApplications)
        {
            app.IsSelected = false;
        }
        UpdateSelectedCount();
    }

    /// <summary>
    /// Selects all applications that are not installed.
    /// </summary>
    [RelayCommand]
    private void SelectNotInstalled()
    {
        foreach (var app in _allApplications)
        {
            app.IsSelected = app.Status != ApplicationStatus.Installed &&
                             app.Status != ApplicationStatus.AlreadyInstalled;
        }
        UpdateSelectedCount();
    }

    /// <summary>
    /// Toggles the selection state of a specific application.
    /// </summary>
    [RelayCommand]
    private void ToggleSelection(ApplicationModel? app)
    {
        if (app == null) return;
        app.IsSelected = !app.IsSelected;
        UpdateSelectedCount();
    }

    /// <summary>
    /// Selects all favorite applications.
    /// </summary>
    [RelayCommand]
    private void SelectFavorites()
    {
        foreach (var app in _allApplications)
        {
            if (app.IsFavorite)
            {
                app.IsSelected = true;
            }
        }
        UpdateSelectedCount();
    }
}

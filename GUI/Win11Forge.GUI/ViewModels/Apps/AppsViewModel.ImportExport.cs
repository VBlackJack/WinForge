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
using System.IO;
using System.Text.Json;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace Win11Forge.GUI.ViewModels;

public partial class AppsViewModel
{
    /// <summary>
    /// Exports the current selection to a JSON file.
    /// </summary>
    [RelayCommand]
    private async Task ExportSelectionAsync()
    {
        var selectedIds = _allApplications
            .Where(a => a.IsSelected)
            .Select(a => a.AppId)
            .ToList();

        if (selectedIds.Count == 0) return;

        var dialog = new SaveFileDialog
        {
            Filter = "JSON files (*.json)|*.json",
            DefaultExt = ".json",
            FileName = "win11forge-selection"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var json = JsonSerializer.Serialize(selectedIds, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                await File.WriteAllTextAsync(dialog.FileName, json);
            }
            catch (Exception ex)
            {
                ErrorMessage = FormatLocalized(
                    "Apps_Error_SelectionExportFailed",
                    "Failed to export selection: {0}",
                    ex.Message);
                Debug.WriteLine($"Failed to export selection: {ex}");
            }
        }
    }

    /// <summary>
    /// Imports a selection from a JSON file.
    /// </summary>
    [RelayCommand]
    private async Task ImportSelectionAsync()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "JSON files (*.json)|*.json",
            DefaultExt = ".json"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var json = await File.ReadAllTextAsync(dialog.FileName);
                var selectedIds = JsonSerializer.Deserialize<List<string>>(json);

                if (selectedIds != null)
                {
                    // Clear current selection
                    foreach (var app in _allApplications)
                    {
                        app.IsSelected = false;
                    }

                    // Apply imported selection
                    foreach (var appId in selectedIds)
                    {
                        var app = _allApplications.FirstOrDefault(a =>
                            a.AppId.Equals(appId, StringComparison.OrdinalIgnoreCase));
                        if (app != null)
                        {
                            app.IsSelected = true;
                        }
                    }

                    UpdateSelectedCount();
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = FormatLocalized(
                    "Apps_Error_SelectionImportFailed",
                    "Failed to import selection: {0}",
                    ex.Message);
                Debug.WriteLine($"Failed to import selection: {ex}");
            }
        }
    }

    /// <summary>
    /// Exports favorites to a JSON file.
    /// </summary>
    [RelayCommand]
    private async Task ExportFavoritesAsync()
    {
        var favoriteIds = _allApplications
            .Where(a => a.IsFavorite)
            .Select(a => a.AppId)
            .ToList();

        if (favoriteIds.Count == 0) return;

        var dialog = new SaveFileDialog
        {
            Filter = "JSON files (*.json)|*.json",
            DefaultExt = ".json",
            FileName = "win11forge-favorites"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var json = JsonSerializer.Serialize(favoriteIds, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                await File.WriteAllTextAsync(dialog.FileName, json);
            }
            catch (Exception ex)
            {
                ErrorMessage = FormatLocalized(
                    "Apps_Error_FavoritesExportFailed",
                    "Failed to export favorites: {0}",
                    ex.Message);
                Debug.WriteLine($"Failed to export favorites: {ex}");
            }
        }
    }

    /// <summary>
    /// Imports favorites from a JSON file.
    /// </summary>
    [RelayCommand]
    private async Task ImportFavoritesAsync()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "JSON files (*.json)|*.json",
            DefaultExt = ".json"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var json = await File.ReadAllTextAsync(dialog.FileName);
                var favoriteIds = JsonSerializer.Deserialize<List<string>>(json);

                if (favoriteIds != null)
                {
                    // Clear current favorites
                    foreach (var app in _allApplications)
                    {
                        app.IsFavorite = false;
                    }

                    // Apply imported favorites
                    foreach (var appId in favoriteIds)
                    {
                        var app = _allApplications.FirstOrDefault(a =>
                            a.AppId.Equals(appId, StringComparison.OrdinalIgnoreCase));
                        if (app != null)
                        {
                            app.IsFavorite = true;
                        }
                    }

                    FavoritesCount = _allApplications.Count(a => a.IsFavorite);

                    // Refresh filter if viewing favorites
                    if (SelectedStatusFilter == StatusFilterOption.Favorites)
                    {
                        ApplyFilter();
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = FormatLocalized(
                    "Apps_Error_FavoritesImportFailed",
                    "Failed to import favorites: {0}",
                    ex.Message);
                Debug.WriteLine($"Failed to import favorites: {ex}");
            }
        }
    }
}

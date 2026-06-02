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
using System.IO;
using System.Text.Json;
using CommunityToolkit.Mvvm.Input;
using Win11Forge.GUI.Models;
using Win11Forge.GUI.Services;

namespace Win11Forge.GUI.ViewModels;

public partial class AppsViewModel
{
    private enum ImportStateTarget
    {
        Selection,
        Favorites
    }

    /// <summary>
    /// Exports the current selection to a JSON file.
    /// </summary>
    [RelayCommand]
    private async Task ExportSelectionAsync()
    {
        List<string> selectedIds = _allApplications
            .Where(a => a.IsSelected)
            .Select(a => a.AppId)
            .ToList();

        if (selectedIds.Count == 0) return;

        string? filePath = await _fileDialogService.ShowSaveAsync(new FileDialogOptions(
            string.Empty,
            FileDialogFilters.JsonOnly,
            DefaultFileName: "win11forge-selection",
            DefaultExtension: FileDialogFilters.JsonDefaultExtension));

        if (filePath != null)
        {
            try
            {
                string json = JsonSerializer.Serialize(selectedIds, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                await File.WriteAllTextAsync(filePath, json);
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
        string? filePath = await _fileDialogService.ShowOpenAsync(new FileDialogOptions(
            string.Empty,
            FileDialogFilters.JsonOnly,
            DefaultExtension: FileDialogFilters.JsonDefaultExtension));

        if (filePath != null)
        {
            try
            {
                string json = await File.ReadAllTextAsync(filePath);
                HashSet<string> selectedIds = NormalizeImportedIds(JsonSerializer.Deserialize<List<string>>(json));
                await ImportApplicationStateAsync(selectedIds, ImportStateTarget.Selection);
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
        List<string> favoriteIds = _allApplications
            .Where(a => a.IsFavorite)
            .Select(a => a.AppId)
            .ToList();

        if (favoriteIds.Count == 0) return;

        string? filePath = await _fileDialogService.ShowSaveAsync(new FileDialogOptions(
            string.Empty,
            FileDialogFilters.JsonOnly,
            DefaultFileName: "win11forge-favorites",
            DefaultExtension: FileDialogFilters.JsonDefaultExtension));

        if (filePath != null)
        {
            try
            {
                string json = JsonSerializer.Serialize(favoriteIds, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                await File.WriteAllTextAsync(filePath, json);
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
        string? filePath = await _fileDialogService.ShowOpenAsync(new FileDialogOptions(
            string.Empty,
            FileDialogFilters.JsonOnly,
            DefaultExtension: FileDialogFilters.JsonDefaultExtension));

        if (filePath != null)
        {
            try
            {
                string json = await File.ReadAllTextAsync(filePath);
                HashSet<string> favoriteIds = NormalizeImportedIds(JsonSerializer.Deserialize<List<string>>(json));
                await ImportApplicationStateAsync(favoriteIds, ImportStateTarget.Favorites);
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

    private async Task ImportApplicationStateAsync(HashSet<string> importedIds, ImportStateTarget target)
    {
        List<ApplicationModel> matchedApps = _allApplications
            .Where(app => importedIds.Contains(app.AppId))
            .ToList();
        HashSet<string> matchedIds = matchedApps
            .Select(app => app.AppId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        int missingCount = importedIds.Count(id => !matchedIds.Contains(id));
        int currentCount = CountCurrentState(target);
        bool replace = true;

        if (currentCount > 0)
        {
            bool? mode = await _dialogService.ShowYesNoCancelAsync(
                GetImportTitle(target),
                string.Format(
                    CultureInfo.CurrentCulture,
                    GetImportMessage(target),
                    matchedApps.Count,
                    missingCount,
                    currentCount),
                GetLocalizedString("Apps_Import_Replace", "Replace"),
                GetLocalizedString("Apps_Import_Merge", "Merge"),
                Resources.Resources.Common_Cancel);

            if (mode is null)
            {
                return;
            }

            replace = mode.Value;
        }

        if (replace)
        {
            foreach (ApplicationModel app in _allApplications)
            {
                SetImportedState(app, target, false);
            }
        }

        foreach (ApplicationModel? app in matchedApps)
        {
            SetImportedState(app, target, true);
        }

        UpdateImportedState(target);

        StatusMessage = string.Format(
            CultureInfo.CurrentCulture,
            GetImportStatusMessage(target),
            matchedApps.Count,
            missingCount,
            CountCurrentState(target));
    }

    private static HashSet<string> NormalizeImportedIds(IEnumerable<string>? importedIds)
    {
        HashSet<string> normalizedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (importedIds is null)
        {
            return normalizedIds;
        }

        foreach (string appId in importedIds)
        {
            if (!string.IsNullOrWhiteSpace(appId))
            {
                normalizedIds.Add(appId.Trim());
            }
        }

        return normalizedIds;
    }

    private int CountCurrentState(ImportStateTarget target)
    {
        return target == ImportStateTarget.Selection
            ? _allApplications.Count(app => app.IsSelected)
            : _allApplications.Count(app => app.IsFavorite);
    }

    private static void SetImportedState(ApplicationModel app, ImportStateTarget target, bool value)
    {
        if (target == ImportStateTarget.Selection)
        {
            app.IsSelected = value;
        }
        else
        {
            app.IsFavorite = value;
        }
    }

    private void UpdateImportedState(ImportStateTarget target)
    {
        if (target == ImportStateTarget.Selection)
        {
            UpdateSelectedCount();
            if (SelectedStatusFilter == StatusFilterOption.Selected)
            {
                ApplyFilter();
            }
            return;
        }

        FavoritesCount = _allApplications.Count(app => app.IsFavorite);
        if (SelectedStatusFilter == StatusFilterOption.Favorites)
        {
            ApplyFilter();
        }
    }

    private static string GetImportTitle(ImportStateTarget target)
    {
        return target == ImportStateTarget.Selection
            ? GetLocalizedString("Apps_ImportSelection_Title", "Import selection")
            : GetLocalizedString("Apps_ImportFavorites_Title", "Import favorites");
    }

    private static string GetImportMessage(ImportStateTarget target)
    {
        return target == ImportStateTarget.Selection
            ? GetLocalizedString(
                "Apps_ImportSelection_Message",
                "The imported selection matches {0} application(s), with {1} missing ID(s). Current selection has {2} application(s). Replace it or merge?")
            : GetLocalizedString(
                "Apps_ImportFavorites_Message",
                "The imported favorites match {0} application(s), with {1} missing ID(s). Current favorites contain {2} application(s). Replace them or merge?");
    }

    private static string GetImportStatusMessage(ImportStateTarget target)
    {
        return target == ImportStateTarget.Selection
            ? GetLocalizedString(
                "Apps_ImportSelection_Status",
                "Selection import complete: {0} matched, {1} missing, {2} selected")
            : GetLocalizedString(
                "Apps_ImportFavorites_Status",
                "Favorites import complete: {0} matched, {1} missing, {2} favorites");
    }
}

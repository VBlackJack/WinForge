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

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Win11Forge.GUI.Models;
using Win11Forge.GUI.Services;

namespace Win11Forge.GUI.ViewModels;

public partial class AppsViewModel
{
    /// <summary>
    /// Source collection for all applications (used by CollectionViewSource).
    /// </summary>
    private readonly ObservableCollection<ApplicationModel> _applicationsSource = [];

    /// <summary>
    /// CollectionView for efficient filtering without recreating collections.
    /// </summary>
    private ICollectionView? _applicationsView;

    /// <summary>
    /// Filtered applications displayed in the view via CollectionView.
    /// Using ICollectionView for efficient filtering instead of recreating ObservableCollection.
    /// </summary>
    public ICollectionView FilteredApplications
    {
        get
        {
            if (_applicationsView == null)
            {
                _applicationsView = CollectionViewSource.GetDefaultView(_applicationsSource);
                _applicationsView.Filter = FilterPredicate;
            }
            return _applicationsView;
        }
    }

    /// <summary>
    /// Available categories for filtering.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<string> _categories = [];

    /// <summary>
    /// Currently selected category filter.
    /// </summary>
    [ObservableProperty]
    private string _selectedCategory = string.Empty;

    /// <summary>
    /// Search text for filtering by name.
    /// </summary>
    [ObservableProperty]
    private string _searchText = string.Empty;

    /// <summary>
    /// Available status filter options.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<StatusFilterOption> _statusFilters = [];

    /// <summary>
    /// Currently selected status filter.
    /// </summary>
    [ObservableProperty]
    private StatusFilterOption _selectedStatusFilter = StatusFilterOption.All;

    /// <summary>
    /// Indicates whether a search filter is currently active.
    /// </summary>
    public bool HasSearchFilter => !string.IsNullOrWhiteSpace(SearchText);

    /// <summary>
    /// Indicates whether a category filter is currently active.
    /// </summary>
    public bool HasCategoryFilter => !string.IsNullOrEmpty(SelectedCategory) &&
                                      SelectedCategory != Resources.Resources.Apps_CategoryAll;

    /// <summary>
    /// Indicates whether a status filter is currently active.
    /// </summary>
    public bool HasStatusFilter => SelectedStatusFilter != StatusFilterOption.All;

    /// <summary>
    /// Indicates whether any filter is currently active.
    /// </summary>
    public bool HasFiltersApplied => HasSearchFilter || HasCategoryFilter || HasStatusFilter;

    /// <summary>
    /// Indicates whether filters result in zero applications (empty state should be shown).
    /// </summary>
    public bool IsEmptyFilterResults => HasFiltersApplied && FilteredCount == 0 && !IsLoading;

    /// <summary>
    /// Available deployment profiles.
    /// </summary>

    #region Column Visibility

    /// <summary>
    /// Whether the Favorites column is visible.
    /// </summary>
    [ObservableProperty]
    private bool _showFavoritesColumn = true;

    /// <summary>
    /// Whether the Version column is visible.
    /// </summary>
    [ObservableProperty]
    private bool _showVersionColumn = true;

    /// <summary>
    /// Whether the Status column is visible.
    /// </summary>
    [ObservableProperty]
    private bool _showStatusColumn = true;

    /// <summary>
    /// Whether the Category column is visible.
    /// </summary>
    [ObservableProperty]
    private bool _showCategoryColumn = true;

    /// <summary>
    /// Whether the Sources column is visible.
    /// </summary>
    [ObservableProperty]
    private bool _showSourcesColumn = true;

    /// <summary>
    /// Whether the Logs column is visible.
    /// </summary>
    [ObservableProperty]
    private bool _showLogsColumn = true;

    /// <summary>
    /// Resets column visibility to defaults (shows essential columns only).
    /// </summary>
    [RelayCommand]
    private void ResetColumnsToDefaults()
    {
        ShowFavoritesColumn = true;
        ShowVersionColumn = false;
        ShowStatusColumn = true;
        ShowCategoryColumn = true;
        ShowSourcesColumn = false;
        ShowLogsColumn = false;
        SaveFilterState();
    }

    #endregion

    /// <summary>
    /// Called when FilteredCount changes.
    /// </summary>
    partial void OnFilteredCountChanged(int value)
    {
        OnPropertyChanged(nameof(IsEmptyFilterResults));
    }

    /// <summary>
    /// Called when SearchText changes.
    /// </summary>
    partial void OnSearchTextChanged(string value)
    {
        OnPropertyChanged(nameof(HasSearchFilter));
        OnPropertyChanged(nameof(HasFiltersApplied));
        OnPropertyChanged(nameof(IsEmptyFilterResults));
        ApplyFilter();
        SaveFilterState();
    }

    /// <summary>
    /// Called when SelectedCategory changes.
    /// </summary>
    partial void OnSelectedCategoryChanged(string value)
    {
        OnPropertyChanged(nameof(HasCategoryFilter));
        OnPropertyChanged(nameof(HasFiltersApplied));
        OnPropertyChanged(nameof(IsEmptyFilterResults));
        ApplyFilter();
        SaveFilterState();
    }

    /// <summary>
    /// Called when SelectedStatusFilter changes.
    /// </summary>
    partial void OnSelectedStatusFilterChanged(StatusFilterOption value)
    {
        OnPropertyChanged(nameof(HasStatusFilter));
        OnPropertyChanged(nameof(HasFiltersApplied));
        OnPropertyChanged(nameof(IsEmptyFilterResults));
        ApplyFilter();
        SaveFilterState();
    }

    /// <summary>
    /// Called when column visibility changes.
    /// </summary>
    partial void OnShowFavoritesColumnChanged(bool value) => SaveFilterState();
    partial void OnShowVersionColumnChanged(bool value) => SaveFilterState();
    partial void OnShowStatusColumnChanged(bool value) => SaveFilterState();
    partial void OnShowCategoryColumnChanged(bool value) => SaveFilterState();
    partial void OnShowSourcesColumnChanged(bool value) => SaveFilterState();
    partial void OnShowLogsColumnChanged(bool value) => SaveFilterState();

    /// <summary>
    /// Saves the current filter state to settings for persistence across navigation.
    /// </summary>
    private void SaveFilterState()
    {
        try
        {
            AppSettings settings = _settingsService.LoadSettings();
            settings.AppsLastSearchText = SearchText ?? string.Empty;
            settings.AppsLastSelectedCategory = SelectedCategory ?? string.Empty;
            settings.AppsLastStatusFilter = (int)SelectedStatusFilter;
            settings.AppsShowFavoritesColumn = ShowFavoritesColumn;
            settings.AppsShowVersionColumn = ShowVersionColumn;
            settings.AppsShowStatusColumn = ShowStatusColumn;
            settings.AppsShowCategoryColumn = ShowCategoryColumn;
            settings.AppsShowSourcesColumn = ShowSourcesColumn;
            settings.AppsShowLogsColumn = ShowLogsColumn;
            if (!_settingsService.SaveSettings(settings))
            {
                Debug.WriteLine("Failed to save filter state: settings persistence returned false");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to save filter state: {ex.Message}");
        }
    }

    /// <summary>
    /// Filter predicate for ICollectionView filtering.
    /// </summary>
    private bool FilterPredicate(object obj)
    {
        if (obj is not ApplicationModel app)
            return false;

        // Filter by search text (case-insensitive)
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            string searchLower = SearchText.ToLowerInvariant();
            bool matchesSearch =
                app.Name.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ||
                app.AppId.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ||
                app.Description.Contains(searchLower, StringComparison.OrdinalIgnoreCase);
            if (!matchesSearch)
                return false;
        }

        // Filter by category (if not "All Categories")
        if (!string.IsNullOrEmpty(SelectedCategory) &&
            SelectedCategory != Resources.Resources.Apps_CategoryAll)
        {
            if (!app.Category.Equals(SelectedCategory, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        // Filter by installation status
        return SelectedStatusFilter switch
        {
            StatusFilterOption.Installed =>
                app.Status == ApplicationStatus.Installed ||
                app.Status == ApplicationStatus.AlreadyInstalled,
            StatusFilterOption.NotInstalled =>
                app.Status != ApplicationStatus.Installed &&
                app.Status != ApplicationStatus.AlreadyInstalled,
            StatusFilterOption.Selected => app.IsSelected,
            StatusFilterOption.Favorites => app.IsFavorite,
            StatusFilterOption.HasUpdates =>
                app.Status == ApplicationStatus.UpdateAvailable,
            _ => true // All
        };
    }

    /// <summary>
    /// Applies search, category, and status filters to the application list.
    /// Uses ICollectionView.Refresh() for efficient filtering without recreating collections.
    /// </summary>
    private void ApplyFilter()
    {
        // Refresh the view filter - much more efficient than recreating collection
        FilteredApplications.Refresh();
        FilteredCount = FilteredApplications.Cast<ApplicationModel>().Count();

        // Update selected count
        UpdateSelectedCount();
    }

    /// <summary>
    /// Refreshes the application list from the database.
    /// </summary>
    [RelayCommand]
    private async Task RefreshAsync()
    {
        // Clear cache to force reload
        await InitializeAsync();
    }

    /// <summary>
    /// Clears all filters.
    /// </summary>
    [RelayCommand]
    private void ClearFilters()
    {
        SearchText = string.Empty;
        SelectedStatusFilter = StatusFilterOption.All;
        if (Categories.Count > 0)
        {
            SelectedCategory = Categories[0];
        }
    }
}

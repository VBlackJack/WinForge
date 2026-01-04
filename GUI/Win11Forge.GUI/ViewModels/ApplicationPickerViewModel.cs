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
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Win11Forge.GUI.Models;

namespace Win11Forge.GUI.ViewModels;

/// <summary>
/// ViewModel for the Application Picker dialog.
/// Provides filtering of available applications.
/// </summary>
public partial class ApplicationPickerViewModel : ObservableObject
{
    private readonly List<ApplicationModel> _allApplications;

    /// <summary>
    /// Search text for filtering applications.
    /// </summary>
    [ObservableProperty]
    private string _searchText = string.Empty;

    /// <summary>
    /// Available categories for filtering.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<string> _categories = [];

    /// <summary>
    /// Selected category filter.
    /// </summary>
    [ObservableProperty]
    private string _selectedCategory = string.Empty;

    /// <summary>
    /// Filtered applications to display.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<ApplicationModel> _filteredApplications = [];

    /// <summary>
    /// Currently selected application.
    /// </summary>
    [ObservableProperty]
    private ApplicationModel? _selectedApplication;

    /// <summary>
    /// Whether an application is selected.
    /// </summary>
    public bool HasSelection => SelectedApplication != null;

    /// <summary>
    /// Whether the filtered list is empty.
    /// </summary>
    public bool IsEmpty => FilteredApplications.Count == 0;

    /// <summary>
    /// Initializes the picker with available applications.
    /// </summary>
    /// <param name="availableApplications">Applications not already in the profile.</param>
    public ApplicationPickerViewModel(IEnumerable<ApplicationModel> availableApplications)
    {
        _allApplications = availableApplications.OrderBy(a => a.Name).ToList();

        // Build category list
        var categories = _allApplications
            .Select(a => a.Category)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct()
            .OrderBy(c => c)
            .ToList();

        Categories = new ObservableCollection<string>(
            new[] { Resources.Resources.Apps_CategoryAll }.Concat(categories));

        SelectedCategory = Resources.Resources.Apps_CategoryAll;

        ApplyFilter();
    }

    /// <summary>
    /// Called when SearchText changes.
    /// </summary>
    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }

    /// <summary>
    /// Called when SelectedCategory changes.
    /// </summary>
    partial void OnSelectedCategoryChanged(string value)
    {
        ApplyFilter();
    }

    /// <summary>
    /// Called when SelectedApplication changes.
    /// </summary>
    partial void OnSelectedApplicationChanged(ApplicationModel? value)
    {
        OnPropertyChanged(nameof(HasSelection));
    }

    /// <summary>
    /// Applies search and category filters.
    /// </summary>
    [RelayCommand]
    private void Search()
    {
        ApplyFilter();
    }

    /// <summary>
    /// Applies the current filters to the application list.
    /// </summary>
    private void ApplyFilter()
    {
        var filtered = _allApplications.AsEnumerable();

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var searchLower = SearchText.Trim();
            filtered = filtered.Where(a =>
                a.Name.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ||
                a.AppId.Contains(searchLower, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrEmpty(a.Description) &&
                 a.Description.Contains(searchLower, StringComparison.OrdinalIgnoreCase)));
        }

        // Apply category filter
        if (!string.IsNullOrEmpty(SelectedCategory) &&
            SelectedCategory != Resources.Resources.Apps_CategoryAll)
        {
            filtered = filtered.Where(a =>
                a.Category.Equals(SelectedCategory, StringComparison.OrdinalIgnoreCase));
        }

        FilteredApplications = new ObservableCollection<ApplicationModel>(filtered.ToList());
        OnPropertyChanged(nameof(IsEmpty));
    }
}

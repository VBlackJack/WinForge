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

using WinForge.GUI.Models;
using WinForge.GUI.ViewModels;

namespace WinForge.GUI.Tests;

/// <summary>
/// Tests for ApplicationPickerViewModel - application selection dialog.
/// </summary>
public class ApplicationPickerViewModelTests
{
    private static List<ApplicationModel> CreateTestApplications()
    {
        return new List<ApplicationModel>
        {
            new() { Name = "Visual Studio Code", AppId = "Microsoft.VisualStudioCode", Category = "Development" },
            new() { Name = "Git", AppId = "Git.Git", Category = "Development" },
            new() { Name = "7-Zip", AppId = "7zip.7zip", Category = "Utilities" },
            new() { Name = "Firefox", AppId = "Mozilla.Firefox", Category = "Browsers" },
            new() { Name = "Chrome", AppId = "Google.Chrome", Category = "Browsers" }
        };
    }

    /// <summary>
    /// Verifies that the ViewModel initializes with provided applications.
    /// </summary>
    [Fact]
    public void Constructor_ShouldInitializeWithApplications()
    {
        // Arrange
        List<ApplicationModel> apps = CreateTestApplications();

        // Act
        ApplicationPickerViewModel viewModel = new ApplicationPickerViewModel(apps);

        // Assert
        Assert.Equal(5, viewModel.FilteredApplications.Count);
        Assert.Empty(viewModel.SearchText);
    }

    /// <summary>
    /// Verifies that categories are extracted from applications.
    /// </summary>
    [Fact]
    public void Categories_ShouldContainAllUniqueCategories()
    {
        // Arrange
        List<ApplicationModel> apps = CreateTestApplications();

        // Act
        ApplicationPickerViewModel viewModel = new ApplicationPickerViewModel(apps);

        // Assert - Should have "All" + 3 unique categories
        Assert.Equal(4, viewModel.Categories.Count);
        Assert.Contains("Development", viewModel.Categories);
        Assert.Contains("Utilities", viewModel.Categories);
        Assert.Contains("Browsers", viewModel.Categories);
    }

    /// <summary>
    /// Verifies that HasSelection is false when no application is selected.
    /// </summary>
    [Fact]
    public void HasSelection_ShouldBeFalseWhenNoSelection()
    {
        // Arrange
        List<ApplicationModel> apps = CreateTestApplications();
        ApplicationPickerViewModel viewModel = new ApplicationPickerViewModel(apps);

        // Assert
        Assert.False(viewModel.HasSelection);
        Assert.Null(viewModel.SelectedApplication);
    }

    /// <summary>
    /// Verifies that HasSelection is true when an application is selected.
    /// </summary>
    [Fact]
    public void HasSelection_ShouldBeTrueWhenSelected()
    {
        // Arrange
        List<ApplicationModel> apps = CreateTestApplications();
        ApplicationPickerViewModel viewModel = new ApplicationPickerViewModel(apps);

        // Act
        viewModel.SelectedApplication = apps[0];

        // Assert
        Assert.True(viewModel.HasSelection);
    }

    /// <summary>
    /// Verifies that IsEmpty is false when applications are loaded.
    /// </summary>
    [Fact]
    public void IsEmpty_ShouldBeFalseWithApplications()
    {
        // Arrange
        List<ApplicationModel> apps = CreateTestApplications();

        // Act
        ApplicationPickerViewModel viewModel = new ApplicationPickerViewModel(apps);

        // Assert
        Assert.False(viewModel.IsEmpty);
    }

    /// <summary>
    /// Verifies that IsEmpty is true when no applications match filter.
    /// </summary>
    [Fact]
    public void IsEmpty_ShouldBeTrueWithNoMatches()
    {
        // Arrange
        List<ApplicationModel> apps = CreateTestApplications();
        ApplicationPickerViewModel viewModel = new ApplicationPickerViewModel(apps);

        // Act - Search for something that doesn't exist
        viewModel.SearchText = "NonExistentApplication12345";

        // Assert
        Assert.True(viewModel.IsEmpty);
    }

    /// <summary>
    /// Verifies that SearchText filters applications.
    /// </summary>
    [Fact]
    public void SearchText_ShouldFilterApplications()
    {
        // Arrange
        List<ApplicationModel> apps = CreateTestApplications();
        ApplicationPickerViewModel viewModel = new ApplicationPickerViewModel(apps);

        // Act
        viewModel.SearchText = "Visual";

        // Assert
        Assert.Single(viewModel.FilteredApplications);
        Assert.Equal("Visual Studio Code", viewModel.FilteredApplications[0].Name);
    }

    /// <summary>
    /// Verifies that search is case-insensitive.
    /// </summary>
    [Fact]
    public void SearchText_ShouldBeCaseInsensitive()
    {
        // Arrange
        List<ApplicationModel> apps = CreateTestApplications();
        ApplicationPickerViewModel viewModel = new ApplicationPickerViewModel(apps);

        // Act
        viewModel.SearchText = "VISUAL";

        // Assert
        Assert.Single(viewModel.FilteredApplications);
    }

    /// <summary>
    /// Verifies that applications are sorted by name.
    /// </summary>
    [Fact]
    public void FilteredApplications_ShouldBeSortedByName()
    {
        // Arrange
        List<ApplicationModel> apps = CreateTestApplications();

        // Act
        ApplicationPickerViewModel viewModel = new ApplicationPickerViewModel(apps);

        // Assert - 7-Zip should be first (sorts before letters)
        Assert.Equal("7-Zip", viewModel.FilteredApplications[0].Name);
    }
}

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

using Win11Forge.GUI.ViewModels;

namespace Win11Forge.GUI.Tests;

/// <summary>
/// Tests for SaveProfileDialogViewModel - profile save dialog logic.
/// </summary>
public class SaveProfileDialogViewModelTests
{
    private static SaveProfileDialogViewModel CreateViewModel(string? currentProfile = null)
    {
        return new SaveProfileDialogViewModel(currentProfile, new List<string> { "Base", "Developer" }, 5);
    }

    /// <summary>
    /// Verifies that the ViewModel initializes with default values when no current profile.
    /// </summary>
    [Fact]
    public void Constructor_ShouldInitializeWithDefaults()
    {
        // Act
        SaveProfileDialogViewModel viewModel = CreateViewModel();

        // Assert
        Assert.False(viewModel.HasExistingProfile);
        Assert.False(viewModel.OverwriteExisting);
        Assert.Equal(string.Empty, viewModel.ExistingProfileName);
        Assert.Equal(string.Empty, viewModel.NewProfileName);
        Assert.Equal(string.Empty, viewModel.Description);
        Assert.NotNull(viewModel.SelectedParent); // Default parent is set (e.g., "(None)")
        Assert.NotNull(viewModel.AvailableParents);
    }

    /// <summary>
    /// Verifies that HasExistingProfile is true when current profile is provided.
    /// </summary>
    [Fact]
    public void HasExistingProfile_ShouldBeTrueWithCurrentProfile()
    {
        // Arrange & Act
        SaveProfileDialogViewModel viewModel = CreateViewModel("TestProfile");

        // Assert
        Assert.True(viewModel.HasExistingProfile);
    }

    /// <summary>
    /// Verifies that OverwriteExisting can be toggled.
    /// </summary>
    [Fact]
    public void OverwriteExisting_ShouldBeToggleable()
    {
        // Arrange
        SaveProfileDialogViewModel viewModel = CreateViewModel();

        // Act
        viewModel.OverwriteExisting = true;

        // Assert
        Assert.True(viewModel.OverwriteExisting);

        // Act
        viewModel.OverwriteExisting = false;

        // Assert
        Assert.False(viewModel.OverwriteExisting);
    }

    /// <summary>
    /// Verifies that ExistingProfileName triggers PropertyChanged.
    /// </summary>
    [Fact]
    public void ExistingProfileName_ShouldTriggerPropertyChanged()
    {
        // Arrange
        SaveProfileDialogViewModel viewModel = CreateViewModel();
        bool propertyChanged = false;
        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(viewModel.ExistingProfileName))
                propertyChanged = true;
        };

        // Act
        viewModel.ExistingProfileName = "TestProfile";

        // Assert
        Assert.True(propertyChanged);
        Assert.Equal("TestProfile", viewModel.ExistingProfileName);
    }

    /// <summary>
    /// Verifies that NewProfileName can be set.
    /// </summary>
    [Fact]
    public void NewProfileName_ShouldBeSettable()
    {
        // Arrange
        SaveProfileDialogViewModel viewModel = CreateViewModel();

        // Act
        viewModel.NewProfileName = "MyNewProfile";

        // Assert
        Assert.Equal("MyNewProfile", viewModel.NewProfileName);
    }

    /// <summary>
    /// Verifies that SelectedParent can be set.
    /// </summary>
    [Fact]
    public void SelectedParent_ShouldBeSettable()
    {
        // Arrange
        SaveProfileDialogViewModel viewModel = CreateViewModel();
        viewModel.AvailableParents.Add("Base");
        viewModel.AvailableParents.Add("Developer");

        // Act
        viewModel.SelectedParent = "Base";

        // Assert
        Assert.Equal("Base", viewModel.SelectedParent);
    }

    /// <summary>
    /// Verifies that Description can be set.
    /// </summary>
    [Fact]
    public void Description_ShouldBeSettable()
    {
        // Arrange
        SaveProfileDialogViewModel viewModel = CreateViewModel();

        // Act
        viewModel.Description = "My custom profile for development";

        // Assert
        Assert.Equal("My custom profile for development", viewModel.Description);
    }

    /// <summary>
    /// Verifies that SelectedAppsCount can be set.
    /// </summary>
    [Fact]
    public void SelectedAppsCount_ShouldBeSettable()
    {
        // Arrange
        SaveProfileDialogViewModel viewModel = CreateViewModel();

        // Act
        viewModel.SelectedAppsCount = 15;

        // Assert
        Assert.Equal(15, viewModel.SelectedAppsCount);
    }

    /// <summary>
    /// Verifies that AvailableParents can be populated.
    /// </summary>
    [Fact]
    public void AvailableParents_ShouldBePopulatable()
    {
        // Arrange
        SaveProfileDialogViewModel viewModel = CreateViewModel();
        int initialCount = viewModel.AvailableParents.Count;

        // Act
        viewModel.AvailableParents.Add("Gaming");

        // Assert
        Assert.Equal(initialCount + 1, viewModel.AvailableParents.Count);
        Assert.Contains("Base", viewModel.AvailableParents);
        Assert.Contains("Developer", viewModel.AvailableParents);
        Assert.Contains("Gaming", viewModel.AvailableParents);
    }
}

/// <summary>
/// Tests for SaveProfileResult model.
/// </summary>
public class SaveProfileResultTests
{
    /// <summary>
    /// Verifies that SaveProfileResult initializes with default values.
    /// </summary>
    [Fact]
    public void Constructor_ShouldInitializeWithDefaults()
    {
        // Act
        SaveProfileResult result = new SaveProfileResult();

        // Assert
        Assert.False(result.OverwriteExisting);
        Assert.Equal(string.Empty, result.ProfileName);
        Assert.Null(result.ParentProfile);
        Assert.Equal(string.Empty, result.Description);
    }

    /// <summary>
    /// Verifies that all properties can be set.
    /// </summary>
    [Fact]
    public void Properties_ShouldBeSettable()
    {
        // Act
        SaveProfileResult result = new SaveProfileResult
        {
            OverwriteExisting = true,
            ProfileName = "TestProfile",
            ParentProfile = "Base",
            Description = "Test description"
        };

        // Assert
        Assert.True(result.OverwriteExisting);
        Assert.Equal("TestProfile", result.ProfileName);
        Assert.Equal("Base", result.ParentProfile);
        Assert.Equal("Test description", result.Description);
    }
}

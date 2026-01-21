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

using Win11Forge.GUI.Models;
using Win11Forge.GUI.Services;
using Win11Forge.GUI.ViewModels;

namespace Win11Forge.GUI.Tests;

/// <summary>
/// Tests for PrerequisitesViewModel - prerequisites checking and installation.
/// </summary>
public class PrerequisitesViewModelTests
{
    /// <summary>
    /// Verifies that the ViewModel initializes with default values.
    /// </summary>
    [Fact]
    public void Constructor_ShouldInitializeWithDefaults()
    {
        // Arrange
        var powerShellBridge = new MockPowerShellBridge();

        // Act
        var viewModel = new PrerequisitesViewModel(powerShellBridge);

        // Assert
        Assert.Null(viewModel.Status);
        Assert.False(viewModel.IsChecking);
        Assert.False(viewModel.IsInstalling);
        Assert.Null(viewModel.ProgressMessage);
        Assert.Equal(string.Empty, viewModel.LogOutput);
    }

    /// <summary>
    /// Verifies that IsChecking can be set.
    /// </summary>
    [Fact]
    public void IsChecking_ShouldBeSettable()
    {
        // Arrange
        var powerShellBridge = new MockPowerShellBridge();
        var viewModel = new PrerequisitesViewModel(powerShellBridge);

        // Act
        viewModel.IsChecking = true;

        // Assert
        Assert.True(viewModel.IsChecking);
    }

    /// <summary>
    /// Verifies that IsInstalling can be set.
    /// </summary>
    [Fact]
    public void IsInstalling_ShouldBeSettable()
    {
        // Arrange
        var powerShellBridge = new MockPowerShellBridge();
        var viewModel = new PrerequisitesViewModel(powerShellBridge);

        // Act
        viewModel.IsInstalling = true;

        // Assert
        Assert.True(viewModel.IsInstalling);
    }

    /// <summary>
    /// Verifies that Status property triggers PropertyChanged.
    /// </summary>
    [Fact]
    public void Status_ShouldTriggerPropertyChanged()
    {
        // Arrange
        var powerShellBridge = new MockPowerShellBridge();
        var viewModel = new PrerequisitesViewModel(powerShellBridge);
        var propertyChanged = false;
        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(viewModel.Status))
                propertyChanged = true;
        };

        // Act
        viewModel.Status = new PrerequisitesStatus
        {
            PowerShell7Installed = true,
            ChocolateyInstalled = true,
            WingetInstalled = true
        };

        // Assert
        Assert.True(propertyChanged);
        Assert.NotNull(viewModel.Status);
        Assert.True(viewModel.Status.AllPrerequisitesMet);
    }

    /// <summary>
    /// Verifies that ProgressMessage can be set.
    /// </summary>
    [Fact]
    public void ProgressMessage_ShouldBeSettable()
    {
        // Arrange
        var powerShellBridge = new MockPowerShellBridge();
        var viewModel = new PrerequisitesViewModel(powerShellBridge);

        // Act
        viewModel.ProgressMessage = "Installing PowerShell 7...";

        // Assert
        Assert.Equal("Installing PowerShell 7...", viewModel.ProgressMessage);
    }

    /// <summary>
    /// Verifies that LogOutput can be appended to.
    /// </summary>
    [Fact]
    public void LogOutput_ShouldAccumulateMessages()
    {
        // Arrange
        var powerShellBridge = new MockPowerShellBridge();
        var viewModel = new PrerequisitesViewModel(powerShellBridge);

        // Act
        viewModel.LogOutput = "Line 1\n";
        viewModel.LogOutput += "Line 2\n";

        // Assert
        Assert.Contains("Line 1", viewModel.LogOutput);
        Assert.Contains("Line 2", viewModel.LogOutput);
    }
}

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
/// Tests for SettingsViewModel - theme, language, and history management.
/// </summary>
public class SettingsViewModelTests
{
    /// <summary>
    /// Verifies that toggling theme updates the settings configuration.
    /// </summary>
    [Fact]
    public void ToggleTheme_ShouldUpdateConfiguration()
    {
        // Arrange
        var settingsService = new MockAppSettingsService();
        var historyService = new MockDeploymentHistoryService();
        var powerShellBridge = new MockPowerShellBridge();
        var viewModel = new SettingsViewModel(settingsService, historyService, powerShellBridge);

        var initialTheme = viewModel.IsDarkTheme;

        // Act - Toggle theme
        viewModel.IsDarkTheme = !initialTheme;

        // Assert - Settings should be saved
        Assert.NotNull(settingsService.LastSavedSettings);
        Assert.Equal(!initialTheme, settingsService.LastSavedSettings.IsDarkTheme);
    }

    /// <summary>
    /// Verifies that changing language sets the restart required flag.
    /// </summary>
    [Fact]
    public void ChangeLanguage_ShouldRequireRestart()
    {
        // Arrange
        var settingsService = new MockAppSettingsService();
        settingsService.SettingsToReturn = new AppSettings { LanguageCode = "en", IsDarkTheme = true };
        var historyService = new MockDeploymentHistoryService();
        var powerShellBridge = new MockPowerShellBridge();
        var viewModel = new SettingsViewModel(settingsService, historyService, powerShellBridge);

        // Act - Change language to French
        var frenchOption = viewModel.AvailableLanguages.First(l => l.Code == "fr");
        viewModel.SelectedLanguage = frenchOption;

        // Assert
        Assert.True(viewModel.RestartRequired,
            "RestartRequired should be true after changing language");
    }

    /// <summary>
    /// Verifies that applying language saves settings and requires restart.
    /// </summary>
    [Fact]
    public void ApplyLanguage_ShouldSaveSettingsAndRequireRestart()
    {
        // Arrange
        var settingsService = new MockAppSettingsService();
        settingsService.SettingsToReturn = new AppSettings { LanguageCode = "en", IsDarkTheme = true };
        var historyService = new MockDeploymentHistoryService();
        var powerShellBridge = new MockPowerShellBridge();
        var viewModel = new SettingsViewModel(settingsService, historyService, powerShellBridge);

        // Change language
        var frenchOption = viewModel.AvailableLanguages.First(l => l.Code == "fr");
        viewModel.SelectedLanguage = frenchOption;

        // Act
        viewModel.ApplyLanguageCommand.Execute(null);

        // Assert
        Assert.True(viewModel.RestartRequired);
        Assert.NotNull(settingsService.LastSavedSettings);
        Assert.Equal("fr", settingsService.LastSavedSettings.LanguageCode);
    }

    /// <summary>
    /// Verifies that clearing history invokes the history service.
    /// </summary>
    [Fact]
    public async Task ClearHistory_ShouldInvokeService()
    {
        // Arrange
        var settingsService = new MockAppSettingsService();
        var historyService = new MockDeploymentHistoryService();
        var powerShellBridge = new MockPowerShellBridge();
        var viewModel = new SettingsViewModel(settingsService, historyService, powerShellBridge);

        // Act
        await viewModel.ClearHistoryCommand.ExecuteAsync(null);

        // Assert
        Assert.True(historyService.WasCleared,
            "History service ClearHistoryAsync should be called");
        Assert.NotNull(viewModel.StatusMessage);
    }

    /// <summary>
    /// Verifies that settings are loaded on initialization.
    /// </summary>
    [Fact]
    public void Initialize_ShouldLoadSettings()
    {
        // Arrange
        var settingsService = new MockAppSettingsService();
        settingsService.SettingsToReturn = new AppSettings
        {
            IsDarkTheme = false,
            LanguageCode = "fr"
        };
        var historyService = new MockDeploymentHistoryService();
        var powerShellBridge = new MockPowerShellBridge();

        // Act
        var viewModel = new SettingsViewModel(settingsService, historyService, powerShellBridge);

        // Assert
        Assert.False(viewModel.IsDarkTheme);
        Assert.Equal("fr", viewModel.SelectedLanguage?.Code);
    }

    /// <summary>
    /// Verifies that theme changes are persisted immediately.
    /// </summary>
    [Fact]
    public void ThemeChange_ShouldPersistImmediately()
    {
        // Arrange
        var settingsService = new MockAppSettingsService();
        var historyService = new MockDeploymentHistoryService();
        var powerShellBridge = new MockPowerShellBridge();
        var viewModel = new SettingsViewModel(settingsService, historyService, powerShellBridge);

        // Act - Change theme multiple times
        viewModel.IsDarkTheme = false;
        Assert.NotNull(settingsService.LastSavedSettings);
        Assert.False(settingsService.LastSavedSettings.IsDarkTheme);

        viewModel.IsDarkTheme = true;
        Assert.True(settingsService.LastSavedSettings.IsDarkTheme);
    }

    /// <summary>
    /// Verifies that same language selection does not require restart.
    /// </summary>
    [Fact]
    public void SameLanguage_ShouldNotRequireRestart()
    {
        // Arrange
        var settingsService = new MockAppSettingsService();
        settingsService.SettingsToReturn = new AppSettings { LanguageCode = "en", IsDarkTheme = true };
        var historyService = new MockDeploymentHistoryService();
        var powerShellBridge = new MockPowerShellBridge();
        var viewModel = new SettingsViewModel(settingsService, historyService, powerShellBridge);

        // Act - Select same language
        var englishOption = viewModel.AvailableLanguages.First(l => l.Code == "en");
        viewModel.SelectedLanguage = englishOption;

        // Assert
        Assert.False(viewModel.RestartRequired,
            "RestartRequired should be false when selecting the same language");
    }

    /// <summary>
    /// Verifies available languages are populated.
    /// </summary>
    [Fact]
    public void AvailableLanguages_ShouldContainEnglishAndFrench()
    {
        // Arrange
        var settingsService = new MockAppSettingsService();
        var historyService = new MockDeploymentHistoryService();
        var powerShellBridge = new MockPowerShellBridge();

        // Act
        var viewModel = new SettingsViewModel(settingsService, historyService, powerShellBridge);

        // Assert
        Assert.Equal(2, viewModel.AvailableLanguages.Count);
        Assert.Contains(viewModel.AvailableLanguages, l => l.Code == "en");
        Assert.Contains(viewModel.AvailableLanguages, l => l.Code == "fr");
    }
}

/// <summary>
/// Mock implementation of IAppSettingsService for testing.
/// </summary>
internal class MockAppSettingsService : IAppSettingsService
{
    public AppSettings? LastSavedSettings { get; private set; }
    public AppSettings SettingsToReturn { get; set; } = new AppSettings();

    public AppSettings LoadSettings() => SettingsToReturn;

    public Task<AppSettings> LoadSettingsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(SettingsToReturn);

    public void SaveSettings(AppSettings settings)
    {
        LastSavedSettings = settings;
    }

    public Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        LastSavedSettings = settings;
        return Task.CompletedTask;
    }

    public void ApplySettings(AppSettings settings)
    {
        // No-op in tests
    }
}

/// <summary>
/// Mock implementation of IDeploymentHistoryService for testing.
/// </summary>
internal class MockDeploymentHistoryService : IDeploymentHistoryService
{
    public bool WasCleared { get; private set; }

    public Task AddEntryAsync(DeploymentHistoryEntry entry) => Task.CompletedTask;

    public Task<List<DeploymentHistoryEntry>> GetHistoryAsync(int limit = 50) =>
        Task.FromResult(new List<DeploymentHistoryEntry>());

    public Task<List<DeploymentHistoryEntry>> GetRecentHistoryAsync(int count = 5) =>
        Task.FromResult(new List<DeploymentHistoryEntry>());

    public Task ClearHistoryAsync()
    {
        WasCleared = true;
        return Task.CompletedTask;
    }
}

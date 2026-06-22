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

using System.IO;
using WinForge.GUI.Models;
using WinForge.GUI.Resources;
using WinForge.GUI.Services;
using WinForge.GUI.ViewModels;

namespace WinForge.GUI.Tests;

/// <summary>
/// Tests for SettingsViewModel - theme, language, and history management.
/// </summary>
[Collection("WpfApplication")]
public class SettingsViewModelTests
{
    /// <summary>
    /// Verifies that changing theme updates the settings configuration.
    /// </summary>
    [Fact]
    public void ChangeTheme_ShouldUpdateConfiguration()
    {
        // Arrange
        MockAppSettingsService settingsService = new MockAppSettingsService();
        MockThemeService themeService = new MockThemeService();
        MockDeploymentHistoryService historyService = new MockDeploymentHistoryService();
        MockPowerShellBridge powerShellBridge = new MockPowerShellBridge();
        SettingsViewModel viewModel = new SettingsViewModel(settingsService, historyService, powerShellBridge, themeService);

        ThemeDescriptor targetTheme = viewModel.AvailableThemes.First(theme => theme.Name == ThemeNames.Folio);

        // Act
        viewModel.SelectedTheme = targetTheme;

        // Assert - Settings should be saved
        Assert.NotNull(settingsService.LastSavedSettings);
        Assert.Equal(ThemeNames.Folio, settingsService.LastSavedSettings.ThemeName);
        Assert.Equal(ThemeNames.Folio, themeService.CurrentTheme);
    }

    /// <summary>
    /// Verifies that changing language sets the restart required flag.
    /// </summary>
    [Fact]
    public void ChangeLanguage_ShouldRequireRestart()
    {
        // Arrange
        MockAppSettingsService settingsService = new MockAppSettingsService();
        settingsService.SettingsToReturn = new AppSettings { LanguageCode = "en", ThemeName = ThemeNames.Drakul };
        MockDeploymentHistoryService historyService = new MockDeploymentHistoryService();
        MockPowerShellBridge powerShellBridge = new MockPowerShellBridge();
        SettingsViewModel viewModel = new SettingsViewModel(settingsService, historyService, powerShellBridge);

        // Act - Change language to French
        LanguageOption frenchOption = viewModel.AvailableLanguages.First(l => l.Code == "fr");
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
        MockAppSettingsService settingsService = new MockAppSettingsService();
        settingsService.SettingsToReturn = new AppSettings { LanguageCode = "en", ThemeName = ThemeNames.Drakul };
        MockDeploymentHistoryService historyService = new MockDeploymentHistoryService();
        MockPowerShellBridge powerShellBridge = new MockPowerShellBridge();
        SettingsViewModel viewModel = new SettingsViewModel(settingsService, historyService, powerShellBridge);

        // Change language
        LanguageOption frenchOption = viewModel.AvailableLanguages.First(l => l.Code == "fr");
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
        MockAppSettingsService settingsService = new MockAppSettingsService();
        MockDeploymentHistoryService historyService = new MockDeploymentHistoryService();
        MockPowerShellBridge powerShellBridge = new MockPowerShellBridge();
        TestDialogService dialogService = new TestDialogService();
        dialogService.QueueConfirmResult(true);
        SettingsViewModel viewModel = new SettingsViewModel(
            settingsService,
            historyService,
            powerShellBridge,
            dialogService: dialogService);

        // Act
        await viewModel.ClearHistoryCommand.ExecuteAsync(null);

        // Assert
        Assert.Single(dialogService.ConfirmRequests);
        Assert.True(historyService.WasCleared,
            "History service ClearHistoryAsync should be called");
        Assert.NotNull(viewModel.StatusMessage);
    }

    /// <summary>
    /// Verifies that clearing history stops when confirmation is declined.
    /// </summary>
    [Fact]
    public async Task ClearHistory_WhenCancelled_ShouldNotInvokeService()
    {
        // Arrange
        MockDeploymentHistoryService historyService = new MockDeploymentHistoryService();
        TestDialogService dialogService = new TestDialogService();
        dialogService.QueueConfirmResult(false);
        SettingsViewModel viewModel = new SettingsViewModel(
            new MockAppSettingsService(),
            historyService,
            new MockPowerShellBridge(),
            dialogService: dialogService);

        // Act
        await viewModel.ClearHistoryCommand.ExecuteAsync(null);

        // Assert
        Assert.Single(dialogService.ConfirmRequests);
        Assert.False(historyService.WasCleared);
    }

    /// <summary>
    /// Verifies that settings are loaded on initialization.
    /// </summary>
    [Fact]
    public void Initialize_ShouldLoadSettings()
    {
        // Arrange
        MockAppSettingsService settingsService = new MockAppSettingsService();
        settingsService.SettingsToReturn = new AppSettings
        {
            ThemeName = ThemeNames.Folio,
            LanguageCode = "fr"
        };
        MockDeploymentHistoryService historyService = new MockDeploymentHistoryService();
        MockPowerShellBridge powerShellBridge = new MockPowerShellBridge();

        // Act
        SettingsViewModel viewModel = new SettingsViewModel(settingsService, historyService, powerShellBridge);

        // Assert
        Assert.Equal(ThemeNames.Folio, viewModel.SelectedTheme?.Name);
        Assert.Equal("fr", viewModel.SelectedLanguage?.Code);
    }

    /// <summary>
    /// Verifies that theme changes are persisted immediately.
    /// </summary>
    [Fact]
    public void ThemeChange_ShouldPersistImmediately()
    {
        // Arrange
        MockAppSettingsService settingsService = new MockAppSettingsService();
        MockDeploymentHistoryService historyService = new MockDeploymentHistoryService();
        MockPowerShellBridge powerShellBridge = new MockPowerShellBridge();
        SettingsViewModel viewModel = new SettingsViewModel(settingsService, historyService, powerShellBridge);

        // Act - Change theme multiple times
        viewModel.SelectedTheme = viewModel.AvailableThemes.First(theme => theme.Name == ThemeNames.Folio);
        Assert.NotNull(settingsService.LastSavedSettings);
        Assert.Equal(ThemeNames.Folio, settingsService.LastSavedSettings.ThemeName);

        viewModel.SelectedTheme = viewModel.AvailableThemes.First(theme => theme.Name == ThemeNames.Sconce);
        Assert.Equal(ThemeNames.Sconce, settingsService.LastSavedSettings.ThemeName);
    }

    [Fact]
    public void AccentTintChange_ShouldPersistImmediately()
    {
        MockAppSettingsService settingsService = new MockAppSettingsService();
        MockDeploymentHistoryService historyService = new MockDeploymentHistoryService();
        MockPowerShellBridge powerShellBridge = new MockPowerShellBridge();
        MockThemeService themeService = new MockThemeService();
        SettingsViewModel viewModel = new SettingsViewModel(settingsService, historyService, powerShellBridge, themeService);

        viewModel.SelectedAccentTint = viewModel.AvailableAccentTints.First(tint => tint.Name == "Green");

        Assert.NotNull(settingsService.LastSavedSettings);
        Assert.Equal("Green", settingsService.LastSavedSettings.AccentTintName);
        Assert.Equal("Green", themeService.CurrentAccentTint);
    }

    [Fact]
    public void AutoSaveChanges_ShouldUpdateStatusWithoutInfoToastSpam()
    {
        // Arrange
        MockAppSettingsService settingsService = new MockAppSettingsService();
        MockDeploymentHistoryService historyService = new MockDeploymentHistoryService();
        MockPowerShellBridge powerShellBridge = new MockPowerShellBridge();
        TestToastService toastService = new TestToastService();
        SettingsViewModel viewModel = new SettingsViewModel(
            settingsService,
            historyService,
            powerShellBridge,
            toastService: toastService);

        // Act
        viewModel.SelectedTheme = viewModel.AvailableThemes.First(theme => theme.Name == ThemeNames.Folio);
        viewModel.ReducedMotion = !viewModel.ReducedMotion;
        viewModel.IsHighContrastEnabled = !viewModel.IsHighContrastEnabled;
        viewModel.MaxParallelInstalls = 3;
        viewModel.MaxParallelScans = 4;
        viewModel.UpdateScanTimeoutMinutes = 10;

        // Assert
        Assert.NotNull(settingsService.LastSavedSettings);
        Assert.NotNull(viewModel.StatusMessage);
        Assert.DoesNotContain(toastService.Toasts, toast => toast.Level == ToastLevel.Info);
    }

    /// <summary>
    /// Verifies that save failures surface an explicit status message.
    /// </summary>
    [Fact]
    public void ThemeChange_WhenSaveFails_ShouldShowSaveError()
    {
        // Arrange
        MockAppSettingsService settingsService = new MockAppSettingsService
        {
            SaveShouldSucceed = false
        };
        MockDeploymentHistoryService historyService = new MockDeploymentHistoryService();
        MockPowerShellBridge powerShellBridge = new MockPowerShellBridge();
        SettingsViewModel viewModel = new SettingsViewModel(settingsService, historyService, powerShellBridge);
        string expected = WinForge.GUI.Resources.Resources.ResourceManager.GetString(
            "Settings_SaveFailed",
            WinForge.GUI.Resources.Resources.Culture) ?? "Failed to save settings";

        // Act
        viewModel.SelectedTheme = viewModel.AvailableThemes.First(theme => theme.Name == ThemeNames.Folio);

        // Assert
        Assert.Equal(expected, viewModel.StatusMessage);
    }

    /// <summary>
    /// Verifies that InitializeAsync does not reload settings when already loaded in constructor.
    /// </summary>
    [Fact]
    public async Task InitializeAsync_ShouldNotReloadSettingsWhenAlreadyLoaded()
    {
        // Arrange
        MockAppSettingsService settingsService = new MockAppSettingsService();
        MockDeploymentHistoryService historyService = new MockDeploymentHistoryService();
        MockPowerShellBridge powerShellBridge = new MockPowerShellBridge();
        SettingsViewModel viewModel = new SettingsViewModel(settingsService, historyService, powerShellBridge);

        // Assert precondition
        Assert.Equal(1, settingsService.LoadSettingsCallCount);

        // Act
        await viewModel.InitializeAsync();

        // Assert - no second load
        Assert.Equal(1, settingsService.LoadSettingsCallCount);
    }

    /// <summary>
    /// Verifies that loading persisted settings does not trigger an unintended save.
    /// </summary>
    [Fact]
    public void Initialize_ShouldNotSaveDuringInitialLoad()
    {
        // Arrange
        MockAppSettingsService settingsService = new MockAppSettingsService
        {
            SettingsToReturn = new AppSettings
            {
                ThemeName = ThemeNames.Folio,
                IsHighContrastEnabled = true,
                ReducedMotionOverride = true,
                LanguageCode = "fr"
            }
        };
        MockDeploymentHistoryService historyService = new MockDeploymentHistoryService();
        MockPowerShellBridge powerShellBridge = new MockPowerShellBridge();

        // Act
        SettingsViewModel viewModel = new SettingsViewModel(settingsService, historyService, powerShellBridge);

        // Assert
        Assert.Null(settingsService.LastSavedSettings);
        Assert.Equal(ThemeNames.Folio, viewModel.SelectedTheme?.Name);
        Assert.True(viewModel.IsHighContrastEnabled);
        Assert.True(viewModel.ReducedMotion);
    }

    /// <summary>
    /// Verifies that high contrast preference is persisted immediately when toggled.
    /// </summary>
    [Fact]
    public void HighContrastToggle_ShouldPersistImmediately()
    {
        // Arrange
        MockAppSettingsService settingsService = new MockAppSettingsService();
        MockDeploymentHistoryService historyService = new MockDeploymentHistoryService();
        MockPowerShellBridge powerShellBridge = new MockPowerShellBridge();
        SettingsViewModel viewModel = new SettingsViewModel(settingsService, historyService, powerShellBridge);

        // Act
        viewModel.IsHighContrastEnabled = true;

        // Assert
        Assert.NotNull(settingsService.LastSavedSettings);
        Assert.True(settingsService.LastSavedSettings.IsHighContrastEnabled);
    }

    /// <summary>
    /// Verifies that reduced motion preference is persisted as an explicit override.
    /// </summary>
    [Fact]
    public void ReducedMotionToggle_ShouldPersistOverride()
    {
        // Arrange
        MockAppSettingsService settingsService = new MockAppSettingsService();
        MockDeploymentHistoryService historyService = new MockDeploymentHistoryService();
        MockPowerShellBridge powerShellBridge = new MockPowerShellBridge();
        SettingsViewModel viewModel = new SettingsViewModel(settingsService, historyService, powerShellBridge);
        bool toggledValue = !viewModel.ReducedMotion;

        // Act
        viewModel.ReducedMotion = toggledValue;

        // Assert
        Assert.NotNull(settingsService.LastSavedSettings);
        Assert.Equal(toggledValue, settingsService.LastSavedSettings.ReducedMotionOverride);
    }

    /// <summary>
    /// Verifies that same language selection does not require restart.
    /// </summary>
    [Fact]
    public void SameLanguage_ShouldNotRequireRestart()
    {
        // Arrange
        MockAppSettingsService settingsService = new MockAppSettingsService();
        settingsService.SettingsToReturn = new AppSettings { LanguageCode = "en", ThemeName = ThemeNames.Drakul };
        MockDeploymentHistoryService historyService = new MockDeploymentHistoryService();
        MockPowerShellBridge powerShellBridge = new MockPowerShellBridge();
        SettingsViewModel viewModel = new SettingsViewModel(settingsService, historyService, powerShellBridge);

        // Act - Select same language
        LanguageOption englishOption = viewModel.AvailableLanguages.First(l => l.Code == "en");
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
        MockAppSettingsService settingsService = new MockAppSettingsService();
        MockDeploymentHistoryService historyService = new MockDeploymentHistoryService();
        MockPowerShellBridge powerShellBridge = new MockPowerShellBridge();

        // Act
        SettingsViewModel viewModel = new SettingsViewModel(settingsService, historyService, powerShellBridge);

        // Assert
        Assert.Equal(2, viewModel.AvailableLanguages.Count);
        Assert.Contains(viewModel.AvailableLanguages, l => l.Code == "en");
        Assert.Contains(viewModel.AvailableLanguages, l => l.Code == "fr");
    }

    /// <summary>
    /// Verifies restart launches a new instance and requests shutdown through the lifetime service.
    /// </summary>
    [Fact]
    public void RestartApplicationCommand_ShouldLaunchProcessAndRequestShutdown()
    {
        // Arrange
        MockAppSettingsService settingsService = new MockAppSettingsService();
        MockDeploymentHistoryService historyService = new MockDeploymentHistoryService();
        MockPowerShellBridge powerShellBridge = new MockPowerShellBridge();
        MockApplicationLifetimeService lifetimeService = new MockApplicationLifetimeService();
        MockProcessLauncher processLauncher = new MockProcessLauncher();
        SettingsViewModel viewModel = new SettingsViewModel(
            settingsService,
            historyService,
            powerShellBridge,
            applicationLifetimeService: lifetimeService,
            processLauncher: processLauncher);

        // Act
        viewModel.RestartApplicationCommand.Execute(null);

        // Assert
        Assert.Equal(1, processLauncher.StartCallCount);
        Assert.NotNull(processLauncher.LastStartInfo);
        Assert.True(processLauncher.LastStartInfo.UseShellExecute);
        Assert.Equal(1, lifetimeService.RequestShutdownCallCount);
        Assert.Equal(0, lifetimeService.LastExitCode);
    }

    [Fact]
    public async Task ExportSettingsCommand_ShouldWriteSettingsToChosenFile()
    {
        // Arrange
        MockAppSettingsService settingsService = new MockAppSettingsService
        {
            SettingsToReturn = new AppSettings { ThemeName = ThemeNames.Folio, LanguageCode = "fr" }
        };
        TestFileDialogService fileDialogService = new TestFileDialogService();
        string filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");
        fileDialogService.QueueSaveResult(filePath);
        SettingsViewModel viewModel = new SettingsViewModel(
            settingsService,
            new MockDeploymentHistoryService(),
            new MockPowerShellBridge(),
            fileDialogService: fileDialogService);

        try
        {
            // Act
            await viewModel.ExportSettingsCommand.ExecuteAsync(null);

            // Assert
            Assert.Single(fileDialogService.SaveOptions);
            Assert.Equal("JSON files (*.json)|*.json|All files (*.*)|*.*", fileDialogService.SaveOptions[0].Filter);
            Assert.Equal(".json", fileDialogService.SaveOptions[0].DefaultExtension);
            Assert.StartsWith("WinForge_Settings_", fileDialogService.SaveOptions[0].DefaultFileName);
            Assert.True(File.Exists(filePath));
            Assert.Contains("\"LanguageCode\": \"fr\"", await File.ReadAllTextAsync(filePath));
            Assert.Equal(Resources.Resources.Settings_ExportSuccess, viewModel.StatusMessage);
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    [Fact]
    public async Task ImportSettingsCommand_ShouldLoadSettingsFromChosenFile()
    {
        // Arrange
        MockAppSettingsService settingsService = new MockAppSettingsService();
        TestFileDialogService fileDialogService = new TestFileDialogService();
        string filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");
        AppSettings importedSettings = new AppSettings { ThemeName = ThemeNames.Folio, AccentTintName = "Purple", LanguageCode = "fr" };
        await File.WriteAllTextAsync(filePath, System.Text.Json.JsonSerializer.Serialize(importedSettings));
        fileDialogService.QueueOpenResult(filePath);
        SettingsViewModel viewModel = new SettingsViewModel(
            settingsService,
            new MockDeploymentHistoryService(),
            new MockPowerShellBridge(),
            fileDialogService: fileDialogService);

        try
        {
            // Act
            await viewModel.ImportSettingsCommand.ExecuteAsync(null);

            // Assert
            Assert.Single(fileDialogService.OpenOptions);
            Assert.Equal("JSON files (*.json)|*.json|All files (*.*)|*.*", fileDialogService.OpenOptions[0].Filter);
            Assert.Equal(".json", fileDialogService.OpenOptions[0].DefaultExtension);
            Assert.NotNull(settingsService.LastSavedSettings);
            Assert.Equal(ThemeNames.Folio, settingsService.LastSavedSettings.ThemeName);
            Assert.Equal("Purple", settingsService.LastSavedSettings.AccentTintName);
            Assert.Equal("fr", settingsService.LastSavedSettings.LanguageCode);
            Assert.True(viewModel.RestartRequired);
            Assert.Equal(Resources.Resources.Settings_ImportSuccess, viewModel.StatusMessage);
        }
        finally
        {
            File.Delete(filePath);
        }
    }
}

/// <summary>
/// Mock implementation of IAppSettingsService for testing.
/// </summary>
internal class MockAppSettingsService : IAppSettingsService
{
    public AppSettings? LastSavedSettings { get; private set; }
    public AppSettings SettingsToReturn { get; set; } = new AppSettings();
    public bool SaveShouldSucceed { get; set; } = true;
    public int LoadSettingsCallCount { get; private set; }

    public AppSettings LoadSettings()
    {
        LoadSettingsCallCount++;
        return SettingsToReturn;
    }

    public Task<AppSettings> LoadSettingsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(SettingsToReturn);

    public bool SaveSettings(AppSettings settings)
    {
        LastSavedSettings = settings;
        return SaveShouldSucceed;
    }

    public Task<bool> SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        LastSavedSettings = settings;
        return Task.FromResult(SaveShouldSucceed);
    }

    public void ApplySettings(AppSettings settings)
    {
        // No-op in tests
    }
}

/// <summary>
/// Mock implementation of IThemeService for testing.
/// </summary>
internal sealed class MockThemeService : IThemeService
{
    private static readonly IReadOnlyList<ThemeDescriptor> ThemeCatalogue =
        ThemeForge.Theme.ThemeNames.All
            .Select(CreateTheme)
            .ToArray();

    private static readonly IReadOnlyList<AccentTintDescriptor> AccentTintCatalogue =
        ThemeForge.Theme.AccentTints.All
            .Select(tint => new AccentTintDescriptor(tint.ToString(), $"Settings_AccentTintName_{tint}"))
            .ToArray();

    public string CurrentTheme { get; private set; } = ThemeNames.Default;

    public int ThemeRevision { get; private set; }

    public IReadOnlyList<ThemeDescriptor> AvailableThemes => ThemeCatalogue;

    public string CurrentAccentTint { get; private set; } = ThemeNames.DefaultAccentTint;

    public IReadOnlyList<AccentTintDescriptor> AvailableAccentTints => AccentTintCatalogue;

    public event Action<string>? ThemeChanged;

    public void ApplyTheme(string? themeName)
    {
        CurrentTheme = AvailableThemes.FirstOrDefault(theme =>
                string.Equals(theme.Name, themeName, StringComparison.OrdinalIgnoreCase))
            ?.Name ?? ThemeNames.Default;
        ThemeRevision++;
        ThemeChanged?.Invoke(CurrentTheme);
    }

    public void ApplyAccentTint(string? accentTintName)
    {
        CurrentAccentTint = AvailableAccentTints.FirstOrDefault(tint =>
                string.Equals(tint.Name, accentTintName, StringComparison.OrdinalIgnoreCase))
            ?.Name ?? ThemeNames.DefaultAccentTint;
        ThemeRevision++;
        ThemeChanged?.Invoke(CurrentTheme);
    }

    private static ThemeDescriptor CreateTheme(string name)
    {
        return new ThemeDescriptor(name, !ThemeNames.IsLightTheme(name), null, $"Settings_ThemeName_{name}");
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

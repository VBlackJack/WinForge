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
using System.Globalization;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Win11Forge.GUI.Configuration;
using Win11Forge.GUI.Localization;
using Win11Forge.GUI.Models;
using Win11Forge.GUI.Resources;
using Win11Forge.GUI.Services;
using Win11Forge.GUI.Services.PowerShell;

namespace Win11Forge.GUI.ViewModels;

/// <summary>
/// ViewModel for the Settings view.
/// Handles theme and language configuration with persistence.
/// </summary>
public partial class SettingsViewModel : ViewModelBase, IDisposable
{
    private readonly IAppSettingsService _settingsService;
    private readonly IThemeService _themeService;
    private readonly IDeploymentHistoryService _historyService;
    private readonly IPowerShellBridge _powerShellBridge;
    private readonly IErrorHistoryService? _errorHistoryService;
    private readonly IApplicationDetectionService? _detectionService;
    private readonly IToastService? _toastService;
    private readonly IApplicationLifetimeService _applicationLifetimeService;
    private readonly IProcessLauncher _processLauncher;
    private readonly IFileDialogService _fileDialogService;
    private readonly IDialogService _dialogService;
    private readonly IRepositoryPathService _pathService;
    private readonly IScheduledDeploymentService _scheduledDeploymentService;
    private string _initialLanguageCode = string.Empty;
    private bool _isLoadingSettings;
    private bool _settingsLoaded;
    private bool? _reducedMotionOverride;

    /// <summary>
    /// Available application themes.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<ThemeDescriptor> _availableThemes = [];

    /// <summary>
    /// Selected application theme.
    /// </summary>
    [ObservableProperty]
    private ThemeDescriptor? _selectedTheme;

    /// <summary>
    /// Available application accent tints.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<AccentTintDescriptor> _availableAccentTints = [];

    /// <summary>
    /// Selected application accent tint.
    /// </summary>
    [ObservableProperty]
    private AccentTintDescriptor? _selectedAccentTint;

    /// <summary>
    /// Available languages.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<LanguageOption> _availableLanguages = [];

    /// <summary>
    /// Selected language.
    /// </summary>
    [ObservableProperty]
    private LanguageOption? _selectedLanguage;

    /// <summary>
    /// Whether restart is required for language change.
    /// </summary>
    [ObservableProperty]
    private bool _restartRequired;

    /// <summary>
    /// Status message for settings changes.
    /// </summary>
    [ObservableProperty]
    private string? _statusMessage;

    /// <summary>
    /// Win11Forge version string.
    /// </summary>
    [ObservableProperty]
    private string _appVersion = string.Empty;

    /// <summary>
    /// Maximum number of parallel installations (1-10).
    /// </summary>
    [ObservableProperty]
    private int _maxParallelInstalls = 5;

    /// <summary>
    /// Maximum number of parallel scans (1-20).
    /// </summary>
    [ObservableProperty]
    private int _maxParallelScans = 8;

    /// <summary>
    /// Update scan timeout in minutes (1-30).
    /// </summary>
    [ObservableProperty]
    private int _updateScanTimeoutMinutes = 5;

    /// <summary>
    /// Whether reduced motion is enabled for accessibility.
    /// </summary>
    [ObservableProperty]
    private bool _reducedMotion;

    /// <summary>
    /// Whether high contrast mode is enabled for accessibility.
    /// </summary>
    [ObservableProperty]
    private bool _isHighContrastEnabled;

    /// <summary>
    /// Available parallel install options.
    /// </summary>
    public int[] ParallelInstallOptions { get; } = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];

    /// <summary>
    /// Available parallel scan options.
    /// </summary>
    public int[] ParallelScanOptions { get; } = [1, 2, 4, 6, 8, 10, 12, 16, 20];

    /// <summary>
    /// Available update scan timeout options in minutes.
    /// </summary>
    public int[] UpdateScanTimeoutOptions { get; } = [1, 2, 3, 5, 10, 15, 20, 30];

    /// <summary>
    /// Collection of scheduled deployments.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<ScheduledDeploymentModel> _scheduledDeployments = [];

    /// <summary>
    /// Currently selected scheduled deployment.
    /// </summary>
    [ObservableProperty]
    private ScheduledDeploymentModel? _selectedScheduledDeployment;

    /// <summary>
    /// Whether scheduled deployments are loading.
    /// </summary>
    [ObservableProperty]
    private bool _isLoadingScheduledDeployments;

    /// <summary>
    /// Whether the scheduled deployment feature is available (requires admin).
    /// </summary>
    [ObservableProperty]
    private bool _isScheduledDeploymentsAvailable;

    /// <summary>
    /// Available profiles for scheduling.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<string> _availableProfiles = [];

    /// <summary>
    /// Selected profile for new scheduled deployment.
    /// </summary>
    [ObservableProperty]
    private string? _newScheduledProfile;

    /// <summary>
    /// Scheduled time for new deployment.
    /// </summary>
    [ObservableProperty]
    private DateTime _newScheduledTime = DateTime.Now.AddHours(1);

    /// <summary>
    /// Trigger type for new scheduled deployment.
    /// </summary>
    [ObservableProperty]
    private ScheduledTriggerType _newScheduledTriggerType = ScheduledTriggerType.OneTime;

    /// <summary>
    /// Available trigger types for the dropdown.
    /// </summary>
    public ScheduledTriggerType[] AvailableTriggerTypes { get; } =
    [
        ScheduledTriggerType.OneTime,
        ScheduledTriggerType.Daily,
        ScheduledTriggerType.Weekly,
        ScheduledTriggerType.AtStartup,
        ScheduledTriggerType.AtLogon
    ];

    #region Error History

    /// <summary>
    /// Recent errors from the error history service.
    /// </summary>
    public ObservableCollection<ErrorHistoryEntry>? RecentErrors => _errorHistoryService?.Errors;

    /// <summary>
    /// Whether there are any errors in history.
    /// </summary>
    [ObservableProperty]
    private bool _hasErrors;

    /// <summary>
    /// Number of errors in history.
    /// </summary>
    [ObservableProperty]
    private int _errorCount;

    /// <summary>
    /// Clears the error history.
    /// </summary>
    [RelayCommand]
    private void ClearErrorHistory()
    {
        _errorHistoryService?.ClearErrors();
        _toastService?.ShowSuccess(Resources.Resources.Settings_ErrorsCleared);
    }

    #endregion

    #region Cache Statistics

    /// <summary>
    /// Number of cached packages.
    /// </summary>
    [ObservableProperty]
    private int _cachePackageCount;

    /// <summary>
    /// Number of cache hits.
    /// </summary>
    [ObservableProperty]
    private int _cacheHits;

    /// <summary>
    /// Number of cache misses.
    /// </summary>
    [ObservableProperty]
    private int _cacheMisses;

    /// <summary>
    /// Cache hit rate as percentage string.
    /// </summary>
    [ObservableProperty]
    private string _cacheHitRate = "0%";

    /// <summary>
    /// Last cache refresh time.
    /// </summary>
    [ObservableProperty]
    private string _lastCacheRefresh = Resources.Resources.Common_NotAvailable;

    /// <summary>
    /// Average detection time.
    /// </summary>
    [ObservableProperty]
    private string _averageDetectionTime = "0 ms";

    /// <summary>
    /// Refreshes cache statistics.
    /// </summary>
    [RelayCommand]
    private void RefreshCacheStatistics()
    {
        if (_detectionService == null) return;

        var stats = _detectionService.GetCacheStatistics();
        CachePackageCount = stats.PackageCount;
        CacheHits = stats.Hits;
        CacheMisses = stats.Misses;

        var total = stats.Hits + stats.Misses;
        CacheHitRate = total > 0
            ? $"{(stats.Hits * 100.0 / total):F1}%"
            : "0%";

        LastCacheRefresh = stats.LastRefresh.HasValue
            ? stats.LastRefresh.Value.ToLocalTime().ToString("g")
            : Resources.Resources.Common_NotAvailable;

        AverageDetectionTime = stats.AverageDetectionTime.TotalMilliseconds > 0
            ? $"{stats.AverageDetectionTime.TotalMilliseconds:F0} ms"
            : "0 ms";
    }

    /// <summary>
    /// Clears the detection cache.
    /// </summary>
    [RelayCommand]
    private void ClearCache()
    {
        _detectionService?.ClearCache();
        RefreshCacheStatistics();
        _toastService?.ShowSuccess(Resources.Resources.Settings_CacheCleared);
    }

    #endregion

    /// <summary>
    /// Initializes a new instance of SettingsViewModel with default services.
    /// Used for XAML design-time support only.
    /// </summary>
    public SettingsViewModel()
        : this(
            CreateDesignTimeService<IAppSettingsService>(() => new AppSettingsService()),
            CreateDesignTimeService<IDeploymentHistoryService>(() => new DeploymentHistoryService()),
            CreateDesignTimeService<IPowerShellBridge>(() => new DesignTimePowerShellBridge()),
            null,
            null,
            null,
            null,
            null,
            null)
    {
    }

    private static T CreateDesignTimeService<T>(Func<T> factory)
    {
        if (!DesignerProperties.GetIsInDesignMode(new DependencyObject()))
        {
            throw new InvalidOperationException("The parameterless SettingsViewModel constructor is design-time only.");
        }

        return factory();
    }

    /// <summary>
    /// Initializes a new instance of SettingsViewModel with injected services.
    /// </summary>
    public SettingsViewModel(
        IAppSettingsService settingsService,
        IDeploymentHistoryService historyService,
        IPowerShellBridge powerShellBridge,
        IThemeService? themeService = null,
        IToastService? toastService = null,
        IErrorHistoryService? errorHistoryService = null,
        IApplicationDetectionService? detectionService = null,
        IApplicationLifetimeService? applicationLifetimeService = null,
        IProcessLauncher? processLauncher = null,
        IFileDialogService? fileDialogService = null,
        IDialogService? dialogService = null,
        IRepositoryPathService? pathService = null,
        IScheduledDeploymentService? scheduledDeploymentService = null)
    {
        _settingsService = settingsService;
        _themeService = themeService ?? new ThemeService(settingsService);
        _historyService = historyService;
        _powerShellBridge = powerShellBridge;
        _toastService = toastService;
        _errorHistoryService = errorHistoryService;
        _detectionService = detectionService;
        _applicationLifetimeService = applicationLifetimeService ?? new ApplicationLifetimeService();
        _processLauncher = processLauncher ?? new ProcessLauncher();
        _fileDialogService = fileDialogService ?? new FileDialogService();
        _dialogService = dialogService ?? new DialogService();
        _pathService = pathService ?? new RepositoryPathService();
        _scheduledDeploymentService = scheduledDeploymentService ?? new ScheduledDeploymentService(powerShellBridge);

        // Subscribe to error history changes
        if (_errorHistoryService != null)
        {
            _errorHistoryService.ErrorCountChanged += OnErrorCountChanged;
            HasErrors = _errorHistoryService.HasErrors;
            ErrorCount = _errorHistoryService.ErrorCount;
        }

        // Initialize available languages
        AvailableLanguages = new ObservableCollection<LanguageOption>(
            SupportedLocales.Codes.Select(code =>
                new LanguageOption(code, SupportedLocales.DisplayNames[code])));

        // Version will be loaded in InitializeAsync
        AppVersion = "...";

        // Load current settings
        LoadCurrentSettings();
    }

    private void OnErrorCountChanged(object? sender, EventArgs e)
    {
        if (_errorHistoryService == null) return;
        HasErrors = _errorHistoryService.HasErrors;
        ErrorCount = _errorHistoryService.ErrorCount;
    }

    /// <inheritdoc/>
    public override async Task InitializeAsync()
    {
        if (!_settingsLoaded)
        {
            LoadCurrentSettings();
        }

        // Load version and scheduled deployments in parallel
        var versionTask = _powerShellBridge.GetWin11ForgeVersionAsync();
        var deploymentsTask = LoadScheduledDeploymentsAsync();

        await Task.WhenAll(versionTask, deploymentsTask);
        AppVersion = await versionTask;
    }

    /// <summary>
    /// Loads the current theme and language settings from persisted storage.
    /// </summary>
    private void LoadCurrentSettings()
    {
        _isLoadingSettings = true;
        try
        {
            var settings = _settingsService.LoadSettings();

            AvailableThemes = new ObservableCollection<ThemeDescriptor>(_themeService.AvailableThemes);
            _themeService.ApplyTheme(settings.ThemeName);
            AvailableAccentTints = new ObservableCollection<AccentTintDescriptor>(_themeService.AvailableAccentTints);
            _themeService.ApplyAccentTint(settings.AccentTintName);
            SelectedTheme = AvailableThemes.FirstOrDefault(theme =>
                    string.Equals(theme.Name, _themeService.CurrentTheme, StringComparison.Ordinal))
                ?? AvailableThemes.First(theme => theme.Name == ThemeNames.Default);
            SelectedAccentTint = AvailableAccentTints.FirstOrDefault(tint =>
                    string.Equals(tint.Name, _themeService.CurrentAccentTint, StringComparison.Ordinal))
                ?? AvailableAccentTints.First(tint => tint.Name == ThemeNames.DefaultAccentTint);

            // Get language from settings
            _initialLanguageCode = settings.LanguageCode;
            SelectedLanguage = AvailableLanguages.FirstOrDefault(l => l.Code == settings.LanguageCode)
                              ?? AvailableLanguages.First();

            // Get parallel installs setting
            MaxParallelInstalls = Math.Clamp(settings.MaxParallelInstalls, 1, 10);
            MaxParallelScans = Math.Clamp(settings.MaxParallelScans, 1, 20);
            UpdateScanTimeoutMinutes = Math.Clamp(settings.UpdateScanTimeoutMinutes, 1, 30);
            _reducedMotionOverride = settings.ReducedMotionOverride;
            ReducedMotion = _reducedMotionOverride ?? App.ReducedMotion;
            IsHighContrastEnabled = settings.IsHighContrastEnabled;

            // Apply accessibility immediately
            App.ApplyHighContrastMode(IsHighContrastEnabled);
            App.SetReducedMotionOverride(_reducedMotionOverride);
            _settingsLoaded = true;
        }
        finally
        {
            _isLoadingSettings = false;
        }
    }

    /// <summary>
    /// Called when MaxParallelInstalls changes.
    /// </summary>
    partial void OnMaxParallelInstallsChanged(int value)
    {
        if (_isLoadingSettings) return;
        if (TrySaveSettings())
        {
            StatusMessage = Resources.Resources.Settings_ParallelInstallsApplied;
        }
    }

    /// <summary>
    /// Called when MaxParallelScans changes.
    /// </summary>
    partial void OnMaxParallelScansChanged(int value)
    {
        if (_isLoadingSettings) return;
        if (TrySaveSettings())
        {
            StatusMessage = Resources.Resources.Settings_ParallelScansApplied;
        }
    }

    /// <summary>
    /// Called when UpdateScanTimeoutMinutes changes.
    /// </summary>
    partial void OnUpdateScanTimeoutMinutesChanged(int value)
    {
        if (_isLoadingSettings) return;
        if (TrySaveSettings())
        {
            StatusMessage = Resources.Resources.Settings_ScanTimeoutApplied;
        }
    }

    /// <summary>
    /// Called when the selected application theme changes.
    /// </summary>
    partial void OnSelectedThemeChanged(ThemeDescriptor? value)
    {
        if (_isLoadingSettings || value is null) return;

        _themeService.ApplyTheme(value.Name);
        if (TrySaveSettings())
        {
            StatusMessage = Resources.Resources.Settings_ThemeApplied;
        }
    }

    /// <summary>
    /// Called when the selected application accent tint changes.
    /// </summary>
    partial void OnSelectedAccentTintChanged(AccentTintDescriptor? value)
    {
        if (_isLoadingSettings || value is null) return;

        _themeService.ApplyAccentTint(value.Name);
        if (TrySaveSettings())
        {
            StatusMessage = Resources.Resources.Settings_ThemeApplied;
        }
    }

    /// <summary>
    /// Called when reduced motion preference changes.
    /// </summary>
    partial void OnReducedMotionChanged(bool value)
    {
        if (_isLoadingSettings) return;
        _reducedMotionOverride = value;
        App.SetReducedMotionOverride(value);
        if (TrySaveSettings())
        {
            StatusMessage = Resources.Resources.Settings_AutoSaved;
        }
    }

    /// <summary>
    /// Called when high contrast mode changes.
    /// </summary>
    partial void OnIsHighContrastEnabledChanged(bool value)
    {
        if (_isLoadingSettings) return;
        App.ApplyHighContrastMode(value);
        if (TrySaveSettings())
        {
            StatusMessage = Resources.Resources.Settings_AutoSaved;
        }
    }

    /// <summary>
    /// Called when SelectedLanguage changes.
    /// </summary>
    partial void OnSelectedLanguageChanged(LanguageOption? value)
    {
        if (value == null) return;

        // Check if language actually changed from initial
        if (value.Code != _initialLanguageCode)
        {
            RestartRequired = true;
        }
    }

    /// <summary>
    /// Applies the selected language and saves settings.
    /// </summary>
    [RelayCommand]
    private void ApplyLanguage()
    {
        if (SelectedLanguage == null) return;

        // Save the language setting (will take effect on restart)
        if (!TrySaveSettings()) return;

        // Set the culture for immediate partial effect
        try
        {
            var culture = new CultureInfo(SelectedLanguage.Code);
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
            Resources.Resources.Culture = culture;
        }
        catch
        {
            // Language application is non-critical
        }

        RestartRequired = true;
        StatusMessage = Resources.Resources.Settings_RestartRequired;
    }

    /// <summary>
    /// Restarts the application to apply language changes.
    /// </summary>
    [RelayCommand]
    private void RestartApplication()
    {
        try
        {
            var currentExecutablePath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(currentExecutablePath))
            {
                _processLauncher.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = currentExecutablePath,
                    UseShellExecute = true
                });
                _applicationLifetimeService.RequestShutdown();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to restart application: {ex.Message}");
        }
    }

    /// <summary>
    /// Saves current settings to disk.
    /// </summary>
    private bool TrySaveSettings()
    {
        // Load existing settings to preserve fields not managed by this view
        var settings = _settingsService.LoadSettings();
        settings.ThemeName = SelectedTheme?.Name ?? ThemeNames.Default;
        settings.AccentTintName = SelectedAccentTint?.Name ?? ThemeNames.DefaultAccentTint;
        settings.IsHighContrastEnabled = IsHighContrastEnabled;
        settings.ReducedMotionOverride = _reducedMotionOverride;
        settings.LanguageCode = SelectedLanguage?.Code ?? SupportedLocales.Default;
        settings.MaxParallelInstalls = MaxParallelInstalls;
        settings.MaxParallelScans = MaxParallelScans;
        settings.UpdateScanTimeoutMinutes = UpdateScanTimeoutMinutes;

        if (_settingsService.SaveSettings(settings))
        {
            return true;
        }

        var saveFailedMessage = Resources.Resources.Settings_SaveFailed;
        StatusMessage = saveFailedMessage;
        _toastService?.ShowError(saveFailedMessage);
        return false;
    }

    /// <summary>
    /// Clears the deployment history with confirmation.
    /// </summary>
    [RelayCommand]
    private async Task ClearHistoryAsync()
    {
        var confirmed = await _dialogService.ShowConfirmAsync(
            Resources.Resources.Confirm_ClearHistory_Title,
            Resources.Resources.Confirm_ClearHistory_Message,
            Resources.Resources.Confirm_ClearHistory_Btn,
            Resources.Resources.Common_Cancel);

        if (!confirmed)
        {
            return;
        }

        await _historyService.ClearHistoryAsync();
        StatusMessage = Resources.Resources.Settings_HistoryCleared;
    }

    /// <summary>
    /// Opens the log folder in Windows Explorer.
    /// </summary>
    [RelayCommand]
    private void OpenLogFolder()
    {
        try
        {
            var logFolderPath = _pathService.LogsDirectory;

            // Create the directory if it doesn't exist
            if (!Directory.Exists(logFolderPath))
            {
                Directory.CreateDirectory(logFolderPath);
            }

            // Open in Windows Explorer
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = logFolderPath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    /// <summary>
    /// Opens the GitHub repository in the default browser.
    /// </summary>
    [RelayCommand]
    private void OpenGitHub()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = ProjectLinks.Repository,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to open GitHub: {ex.Message}");
        }
    }

    /// <summary>
    /// Exports settings to a JSON file.
    /// </summary>
    [RelayCommand]
    private async Task ExportSettingsAsync()
    {
        try
        {
            var filePath = await _fileDialogService.ShowSaveAsync(new FileDialogOptions(
                string.Empty,
                FileDialogFilters.Json,
                DefaultFileName: $"Win11Forge_Settings_{DateTime.Now:yyyyMMdd}",
                DefaultExtension: FileDialogFilters.JsonDefaultExtension));

            if (filePath != null)
            {
                var settings = _settingsService.LoadSettings();
                var json = System.Text.Json.JsonSerializer.Serialize(settings, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                });
                await File.WriteAllTextAsync(filePath, json);
                StatusMessage = Resources.Resources.Settings_ExportSuccess;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"{Resources.Resources.Settings_ExportFailed}: {ex.Message}";
        }
    }

    /// <summary>
    /// Imports settings from a JSON file.
    /// </summary>
    [RelayCommand]
    private async Task ImportSettingsAsync()
    {
        try
        {
            var filePath = await _fileDialogService.ShowOpenAsync(new FileDialogOptions(
                string.Empty,
                FileDialogFilters.Json,
                DefaultExtension: FileDialogFilters.JsonDefaultExtension));

            if (filePath != null)
            {
                var json = await File.ReadAllTextAsync(filePath);
                var importedSettings = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json);

                if (importedSettings != null)
                {
                    importedSettings.ThemeName = ThemeService.NormalizeThemeName(importedSettings.ThemeName);
                    importedSettings.AccentTintName = ThemeService.NormalizeAccentTintName(importedSettings.AccentTintName);
                    if (!_settingsService.SaveSettings(importedSettings))
                    {
                        StatusMessage = Resources.Resources.Settings_SaveFailed;
                        return;
                    }

                    // Reload settings into UI
                    LoadCurrentSettings();
                    StatusMessage = Resources.Resources.Settings_ImportSuccess;
                    RestartRequired = true;
                }
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"{Resources.Resources.Settings_ImportFailed}: {ex.Message}";
        }
    }

    /// <summary>
    /// Resets all settings to default values.
    /// </summary>
    [RelayCommand]
    private async Task ResetToDefaultsAsync()
    {
        var confirmed = await _dialogService.ShowConfirmAsync(
            Resources.Resources.Confirm_ResetSettings_Title,
            Resources.Resources.Confirm_ResetSettings_Message,
            Resources.Resources.Confirm_Reset_Btn,
            Resources.Resources.Common_Cancel);

        if (!confirmed)
        {
            return;
        }

        try
        {
            var defaultSettings = new AppSettings();
            if (!_settingsService.SaveSettings(defaultSettings))
            {
                StatusMessage = Resources.Resources.Settings_SaveFailed;
                return;
            }
            LoadCurrentSettings();
            StatusMessage = Resources.Resources.Settings_ResetSuccess;
            RestartRequired = true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"{Resources.Resources.Settings_ResetFailed}: {ex.Message}";
        }
    }

    /// <summary>
    /// Loads scheduled deployments from the system.
    /// </summary>
    [RelayCommand]
    private async Task LoadScheduledDeploymentsAsync()
    {
        try
        {
            IsLoadingScheduledDeployments = true;

            var availability = await _scheduledDeploymentService.GetAvailabilityAsync();
            IsScheduledDeploymentsAvailable = availability.IsAvailable;

            if (!IsScheduledDeploymentsAvailable)
            {
                return;
            }

            // Load available profiles
            var profiles = await _powerShellBridge.GetAvailableProfilesAsync();
            AvailableProfiles.Clear();
            foreach (var profile in profiles)
            {
                AvailableProfiles.Add(profile);
            }

            if (AvailableProfiles.Count > 0 && NewScheduledProfile == null)
            {
                NewScheduledProfile = AvailableProfiles[0];
            }

            ScheduledDeployments.Clear();
            var deployments = await _scheduledDeploymentService.GetScheduledDeploymentsAsync();
            foreach (var deployment in deployments)
            {
                ScheduledDeployments.Add(deployment);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"{Resources.Resources.ScheduledDeployment_LoadError}: {ex.Message}";
        }
        finally
        {
            IsLoadingScheduledDeployments = false;
        }
    }

    /// <summary>
    /// Creates a new scheduled deployment.
    /// </summary>
    [RelayCommand]
    private async Task CreateScheduledDeploymentAsync()
    {
        if (string.IsNullOrEmpty(NewScheduledProfile))
        {
            StatusMessage = Resources.Resources.ScheduledDeployment_SelectProfile;
            return;
        }

        try
        {
            var deploymentId = await _scheduledDeploymentService.CreateScheduledDeploymentAsync(
                NewScheduledProfile,
                NewScheduledTime,
                NewScheduledTriggerType);

            if (!string.IsNullOrEmpty(deploymentId))
            {
                StatusMessage = Resources.Resources.ScheduledDeployment_Created;
                await LoadScheduledDeploymentsAsync();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"{Resources.Resources.ScheduledDeployment_CreateError}: {ex.Message}";
        }
    }

    /// <summary>
    /// Removes the selected scheduled deployment.
    /// </summary>
    [RelayCommand]
    private async Task RemoveScheduledDeploymentAsync()
    {
        if (SelectedScheduledDeployment == null)
        {
            return;
        }

        var confirmed = await _dialogService.ShowConfirmAsync(
            Resources.Resources.ScheduledDeployment_ConfirmRemoveTitle,
            Resources.Resources.ScheduledDeployment_ConfirmRemove,
            Resources.Resources.Confirm_Delete_Btn,
            Resources.Resources.Common_Cancel);

        if (!confirmed)
        {
            return;
        }

        try
        {
            await _scheduledDeploymentService.RemoveScheduledDeploymentAsync(SelectedScheduledDeployment.Id);
            StatusMessage = Resources.Resources.ScheduledDeployment_Removed;
            await LoadScheduledDeploymentsAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"{Resources.Resources.ScheduledDeployment_RemoveError}: {ex.Message}";
        }
    }

    /// <summary>
    /// Runs the selected scheduled deployment immediately.
    /// </summary>
    [RelayCommand]
    private async Task RunScheduledDeploymentNowAsync()
    {
        if (SelectedScheduledDeployment == null)
        {
            return;
        }

        try
        {
            await _scheduledDeploymentService.StartScheduledDeploymentAsync(SelectedScheduledDeployment.Id);
            StatusMessage = Resources.Resources.ScheduledDeployment_Started;
            await LoadScheduledDeploymentsAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"{Resources.Resources.ScheduledDeployment_StartError}: {ex.Message}";
        }
    }

    #region IDisposable

    private bool _disposed;

    /// <summary>
    /// Releases all resources used by the SettingsViewModel.
    /// </summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases managed and unmanaged resources.
    /// </summary>
    /// <param name="disposing">True if disposing managed resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            // Unsubscribe from error history events
            if (_errorHistoryService != null)
            {
                _errorHistoryService.ErrorCountChanged -= OnErrorCountChanged;
            }
        }

        _disposed = true;
    }

    #endregion
}

/// <summary>
/// Represents a language option.
/// </summary>
public class LanguageOption
{
    /// <summary>
    /// ISO language code (e.g., "en", "fr").
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// Display name (e.g., "English", "Français").
    /// </summary>
    public string DisplayName { get; }

    public LanguageOption(string code, string displayName)
    {
        Code = code;
        DisplayName = displayName;
    }

    public override string ToString() => DisplayName;
}

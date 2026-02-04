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
using System.Diagnostics;
using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using Win11Forge.GUI.Models;
using Win11Forge.GUI.Services;

namespace Win11Forge.GUI.ViewModels;

/// <summary>
/// ViewModel for the Settings view.
/// Handles theme and language configuration with persistence.
/// </summary>
public partial class SettingsViewModel : ViewModelBase, IDisposable
{
    private readonly PaletteHelper _paletteHelper = new();
    private readonly IAppSettingsService _settingsService;
    private readonly IDeploymentHistoryService _historyService;
    private readonly IPowerShellBridge _powerShellBridge;
    private readonly IErrorHistoryService? _errorHistoryService;
    private readonly IApplicationDetectionService? _detectionService;
    private readonly ToastService? _toastService;
    private string _initialLanguageCode = string.Empty;

    /// <summary>
    /// Whether dark theme is enabled.
    /// </summary>
    [ObservableProperty]
    private bool _isDarkTheme = true;

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
        : this(new AppSettingsService(), new DeploymentHistoryService(), new DesignTimePowerShellBridge(), null, null, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of SettingsViewModel with injected services.
    /// </summary>
    public SettingsViewModel(IAppSettingsService settingsService, IDeploymentHistoryService historyService, IPowerShellBridge powerShellBridge, ToastService? toastService = null, IErrorHistoryService? errorHistoryService = null, IApplicationDetectionService? detectionService = null)
    {
        _settingsService = settingsService;
        _historyService = historyService;
        _powerShellBridge = powerShellBridge;
        _toastService = toastService;
        _errorHistoryService = errorHistoryService;
        _detectionService = detectionService;

        // Subscribe to error history changes
        if (_errorHistoryService != null)
        {
            _errorHistoryService.ErrorCountChanged += OnErrorCountChanged;
            HasErrors = _errorHistoryService.HasErrors;
            ErrorCount = _errorHistoryService.ErrorCount;
        }

        // Initialize available languages
        AvailableLanguages =
        [
            new LanguageOption("en", "English"),
            new LanguageOption("fr", "Francais")
        ];

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
        LoadCurrentSettings();

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
        var settings = _settingsService.LoadSettings();

        // Get current theme from settings
        IsDarkTheme = settings.IsDarkTheme;

        // Get language from settings
        _initialLanguageCode = settings.LanguageCode;
        SelectedLanguage = AvailableLanguages.FirstOrDefault(l => l.Code == settings.LanguageCode)
                          ?? AvailableLanguages.First();

        // Get parallel installs setting
        MaxParallelInstalls = Math.Clamp(settings.MaxParallelInstalls, 1, 10);
        MaxParallelScans = Math.Clamp(settings.MaxParallelScans, 1, 20);
        UpdateScanTimeoutMinutes = Math.Clamp(settings.UpdateScanTimeoutMinutes, 1, 30);

        // Apply theme immediately
        ApplyThemeInternal(IsDarkTheme);
    }

    /// <summary>
    /// Called when MaxParallelInstalls changes.
    /// </summary>
    partial void OnMaxParallelInstallsChanged(int value)
    {
        SaveSettings();
        StatusMessage = Resources.Resources.Settings_ParallelInstallsApplied;
        _toastService?.ShowInfo(Resources.Resources.Settings_AutoSaved);
    }

    /// <summary>
    /// Called when MaxParallelScans changes.
    /// </summary>
    partial void OnMaxParallelScansChanged(int value)
    {
        SaveSettings();
        StatusMessage = Resources.Resources.Settings_ParallelScansApplied;
        _toastService?.ShowInfo(Resources.Resources.Settings_AutoSaved);
    }

    /// <summary>
    /// Called when UpdateScanTimeoutMinutes changes.
    /// </summary>
    partial void OnUpdateScanTimeoutMinutesChanged(int value)
    {
        SaveSettings();
        StatusMessage = Resources.Resources.Settings_ScanTimeoutApplied;
        _toastService?.ShowInfo(Resources.Resources.Settings_AutoSaved);
    }

    /// <summary>
    /// Called when IsDarkTheme changes.
    /// </summary>
    partial void OnIsDarkThemeChanged(bool value)
    {
        ApplyThemeInternal(value);
        SaveSettings();
        StatusMessage = Resources.Resources.Settings_ThemeApplied;
        _toastService?.ShowInfo(Resources.Resources.Settings_AutoSaved);
    }

    /// <summary>
    /// Applies the theme without saving (internal use).
    /// </summary>
    private void ApplyThemeInternal(bool isDark)
    {
        try
        {
            var theme = _paletteHelper.GetTheme();
            theme.SetBaseTheme(isDark ? BaseTheme.Dark : BaseTheme.Light);
            _paletteHelper.SetTheme(theme);

            // Update theme-adaptive accent brush
            UpdateThemeAdaptiveResources(isDark);
        }
        catch
        {
            // Theme application is non-critical
        }
    }

    /// <summary>
    /// Updates theme-adaptive resources based on current theme.
    /// Dark theme uses Secondary (lime), Light theme uses Primary (purple).
    /// </summary>
    private static void UpdateThemeAdaptiveResources(bool isDark)
    {
        var app = System.Windows.Application.Current;
        if (app?.Resources == null) return;

        // Use direct colors to ensure correct contrast in each theme
        // Dark theme: lime (#CDDC39) - visible on dark background
        // Light theme: purple (#673AB7) - visible on light background
        var accentBrush = isDark
            ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(205, 220, 57))   // Lime
            : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(103, 58, 183)); // DeepPurple

        app.Resources["ThemeAdaptiveAccentBrush"] = accentBrush;
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
        SaveSettings();

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
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = currentExecutablePath,
                    UseShellExecute = true
                });
                System.Windows.Application.Current.Shutdown();
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
    private void SaveSettings()
    {
        var settings = new AppSettings
        {
            IsDarkTheme = IsDarkTheme,
            LanguageCode = SelectedLanguage?.Code ?? "en",
            MaxParallelInstalls = MaxParallelInstalls,
            MaxParallelScans = MaxParallelScans,
            UpdateScanTimeoutMinutes = UpdateScanTimeoutMinutes
        };

        _settingsService.SaveSettings(settings);
    }

    /// <summary>
    /// Clears the deployment history with confirmation.
    /// </summary>
    [RelayCommand]
    private async Task ClearHistoryAsync()
    {
        // Show confirmation dialog
        var result = System.Windows.MessageBox.Show(
            Resources.Resources.Confirm_ClearHistory_Message,
            Resources.Resources.Confirm_ClearHistory_Title,
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (result != System.Windows.MessageBoxResult.Yes)
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
            // Get the Win11Forge Logs folder path (LocalAppData)
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var logFolderPath = Path.Combine(localAppData, "Win11Forge", "Logs");

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
                FileName = "https://github.com/VBlackJack/Win11Forge",
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
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                DefaultExt = ".json",
                FileName = $"Win11Forge_Settings_{DateTime.Now:yyyyMMdd}"
            };

            if (dialog.ShowDialog() == true)
            {
                var settings = _settingsService.LoadSettings();
                var json = System.Text.Json.JsonSerializer.Serialize(settings, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                });
                await File.WriteAllTextAsync(dialog.FileName, json);
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
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                DefaultExt = ".json"
            };

            if (dialog.ShowDialog() == true)
            {
                var json = await File.ReadAllTextAsync(dialog.FileName);
                var importedSettings = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json);

                if (importedSettings != null)
                {
                    _settingsService.SaveSettings(importedSettings);

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
    private void ResetToDefaults()
    {
        var result = System.Windows.MessageBox.Show(
            Resources.Resources.Confirm_ResetSettings_Message,
            Resources.Resources.Confirm_ResetSettings_Title,
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (result != System.Windows.MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            var defaultSettings = new AppSettings();
            _settingsService.SaveSettings(defaultSettings);
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

            // Check if admin and Task Scheduler available
            var repoRoot = _powerShellBridge.RepositoryRoot.Replace("'", "''");
            var checkScript = $@"
                Import-Module (Join-Path '{repoRoot}' 'Modules\ScheduledDeployment.psm1') -Force -ErrorAction Stop
                @{{
                    TaskSchedulerAvailable = Test-ScheduledTasksAvailable
                    IsAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
                }} | ConvertTo-Json
            ";

            var checkResult = await _powerShellBridge.ExecuteCommandAsync(checkScript);

            if (!string.IsNullOrEmpty(checkResult))
            {
                var checkJson = System.Text.Json.JsonDocument.Parse(checkResult);
                var taskSchedulerAvailable = checkJson.RootElement.GetProperty("TaskSchedulerAvailable").GetBoolean();
                var isAdmin = checkJson.RootElement.GetProperty("IsAdmin").GetBoolean();

                IsScheduledDeploymentsAvailable = taskSchedulerAvailable && isAdmin;
            }

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

            // Load scheduled deployments
            var script = $@"
                Import-Module (Join-Path '{repoRoot}' 'Modules\ScheduledDeployment.psm1') -Force -ErrorAction Stop
                Get-ScheduledDeployment | ForEach-Object {{
                    @{{
                        Id = $_.Id
                        ProfileName = $_.ProfileName
                        ScheduledTime = $_.ScheduledTime.ToString('o')
                        TriggerType = $_.TriggerType
                        Status = $_.Status
                        CreatedBy = $_.CreatedBy
                        CreatedAt = $_.CreatedAt.ToString('o')
                        LastRunTime = if ($_.LastRunTime -and $_.LastRunTime -ne [datetime]::MinValue) {{ $_.LastRunTime.ToString('o') }} else {{ $null }}
                        LastRunResult = $_.LastRunResult
                    }}
                }} | ConvertTo-Json -Compress
            ";

            var result = await _powerShellBridge.ExecuteCommandAsync(script);

            ScheduledDeployments.Clear();

            if (string.IsNullOrWhiteSpace(result) || result == "null")
            {
                return;
            }

            var jsonOptions = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            // Handle both single object and array
            if (result.TrimStart().StartsWith('['))
            {
                var deployments = System.Text.Json.JsonSerializer.Deserialize<List<ScheduledDeploymentJson>>(result, jsonOptions);
                if (deployments != null)
                {
                    foreach (var d in deployments)
                    {
                        ScheduledDeployments.Add(MapToModel(d));
                    }
                }
            }
            else
            {
                var deployment = System.Text.Json.JsonSerializer.Deserialize<ScheduledDeploymentJson>(result, jsonOptions);
                if (deployment != null)
                {
                    ScheduledDeployments.Add(MapToModel(deployment));
                }
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
            var triggerTypeStr = NewScheduledTriggerType.ToString();
            var timeStr = NewScheduledTime.ToString("o");
            var repoRoot = _powerShellBridge.RepositoryRoot.Replace("'", "''");

            var script = $@"
                Import-Module (Join-Path '{repoRoot}' 'Modules\ScheduledDeployment.psm1') -Force -ErrorAction Stop
                $deployment = New-ScheduledDeployment -ProfileName '{NewScheduledProfile}' -ScheduledTime ([datetime]'{timeStr}') -TriggerType '{triggerTypeStr}'
                @{{
                    Success = $true
                    Id = $deployment.Id
                }} | ConvertTo-Json
            ";

            var result = await _powerShellBridge.ExecuteCommandAsync(script);

            if (!string.IsNullOrEmpty(result))
            {
                var json = System.Text.Json.JsonDocument.Parse(result);
                if (json.RootElement.TryGetProperty("Success", out var successProp) && successProp.GetBoolean())
                {
                    StatusMessage = Resources.Resources.ScheduledDeployment_Created;
                    await LoadScheduledDeploymentsAsync();
                }
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

        var result = System.Windows.MessageBox.Show(
            Resources.Resources.ScheduledDeployment_ConfirmRemove,
            Resources.Resources.ScheduledDeployment_ConfirmRemoveTitle,
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);

        if (result != System.Windows.MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            var repoRoot = _powerShellBridge.RepositoryRoot.Replace("'", "''");
            var script = $@"
                Import-Module (Join-Path '{repoRoot}' 'Modules\ScheduledDeployment.psm1') -Force -ErrorAction Stop
                Remove-ScheduledDeployment -Id '{SelectedScheduledDeployment.Id}' -Force
            ";

            await _powerShellBridge.ExecuteCommandAsync(script);
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
            var repoRoot = _powerShellBridge.RepositoryRoot.Replace("'", "''");
            var script = $@"
                Import-Module (Join-Path '{repoRoot}' 'Modules\ScheduledDeployment.psm1') -Force -ErrorAction Stop
                Start-ScheduledDeployment -Id '{SelectedScheduledDeployment.Id}'
            ";

            await _powerShellBridge.ExecuteCommandAsync(script);
            StatusMessage = Resources.Resources.ScheduledDeployment_Started;
            await LoadScheduledDeploymentsAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"{Resources.Resources.ScheduledDeployment_StartError}: {ex.Message}";
        }
    }

    /// <summary>
    /// Maps JSON data to a ScheduledDeploymentModel.
    /// </summary>
    private static ScheduledDeploymentModel MapToModel(ScheduledDeploymentJson json)
    {
        var status = json.Status?.ToLowerInvariant() switch
        {
            "pending" => ScheduledDeploymentStatus.Pending,
            "running" => ScheduledDeploymentStatus.Running,
            "completed" => ScheduledDeploymentStatus.Completed,
            "failed" => ScheduledDeploymentStatus.Failed,
            "cancelled" => ScheduledDeploymentStatus.Cancelled,
            _ => ScheduledDeploymentStatus.Unknown
        };

        var triggerType = json.TriggerType?.ToLowerInvariant() switch
        {
            "onetime" => ScheduledTriggerType.OneTime,
            "daily" => ScheduledTriggerType.Daily,
            "weekly" => ScheduledTriggerType.Weekly,
            "atstartup" => ScheduledTriggerType.AtStartup,
            "atlogon" => ScheduledTriggerType.AtLogon,
            _ => ScheduledTriggerType.OneTime
        };

        return new ScheduledDeploymentModel
        {
            Id = json.Id ?? string.Empty,
            ProfileName = json.ProfileName ?? string.Empty,
            ScheduledTime = DateTime.TryParse(json.ScheduledTime, out var st) ? st : DateTime.Now,
            TriggerType = triggerType,
            Status = status,
            CreatedBy = json.CreatedBy ?? Environment.UserName,
            CreatedAt = DateTime.TryParse(json.CreatedAt, out var ca) ? ca : DateTime.Now,
            LastRunTime = DateTime.TryParse(json.LastRunTime, out var lrt) ? lrt : null,
            LastRunResult = json.LastRunResult
        };
    }

    /// <summary>
    /// JSON DTO for scheduled deployment data.
    /// </summary>
    private class ScheduledDeploymentJson
    {
        public string? Id { get; set; }
        public string? ProfileName { get; set; }
        public string? ScheduledTime { get; set; }
        public string? TriggerType { get; set; }
        public string? Status { get; set; }
        public string? CreatedBy { get; set; }
        public string? CreatedAt { get; set; }
        public string? LastRunTime { get; set; }
        public string? LastRunResult { get; set; }
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

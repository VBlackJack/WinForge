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
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Win11Forge.GUI.Exceptions;
using Win11Forge.GUI.Helpers;
using Win11Forge.GUI.Messages;
using Win11Forge.GUI.Models;
using Win11Forge.GUI.Services;

namespace Win11Forge.GUI.ViewModels;

/// <summary>
/// ViewModel for the Dashboard view.
/// Displays a hero section with system status, stats, and quick navigation.
/// </summary>
public partial class DashboardViewModel : ViewModelBase
{
    private const int MaxRecentHistory = 5;

    private readonly IPowerShellBridge _powerShellBridge;
    private readonly IDeploymentHistoryService _historyService;
    private readonly IAppSettingsService _settingsService;

    #region Hero Section Properties

    /// <summary>
    /// Current state of the dashboard hero section.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HeroTitle))]
    [NotifyPropertyChangedFor(nameof(HeroSubtitle))]
    [NotifyPropertyChangedFor(nameof(IsChecking))]
    [NotifyPropertyChangedFor(nameof(IsReady))]
    [NotifyPropertyChangedFor(nameof(NeedsPrerequisites))]
    [NotifyPropertyChangedFor(nameof(HasUpdatesAvailable))]
    [NotifyPropertyChangedFor(nameof(CanStartDeployment))]
    private DashboardState _currentState = DashboardState.Checking;

    /// <summary>
    /// Number of missing prerequisites.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HeroTitle))]
    private int _missingPrerequisitesCount;

    /// <summary>
    /// Current phase text during checking state.
    /// </summary>
    [ObservableProperty]
    private string _checkingPhaseText = string.Empty;

    /// <summary>
    /// Title for the hero section based on current state.
    /// </summary>
    public string HeroTitle => CurrentState switch
    {
        DashboardState.Checking => CheckingPhaseText,
        DashboardState.NeedPrereqs => string.Format(Resources.Resources.Dashboard_Hero_NeedPrereqs, MissingPrerequisitesCount),
        DashboardState.Ready => Resources.Resources.Dashboard_Hero_Ready,
        DashboardState.HasUpdates => string.Format(Resources.Resources.Dashboard_Hero_HasUpdates, UpdateCount),
        _ => Resources.Resources.Dashboard_Hero_Checking
    };

    /// <summary>
    /// Subtitle for the hero section based on current state.
    /// </summary>
    public string HeroSubtitle => CurrentState switch
    {
        DashboardState.Checking => Resources.Resources.Dashboard_Phase_LoadingInfo,
        DashboardState.NeedPrereqs => Resources.Resources.Dashboard_Hero_Subtitle_Prereqs,
        DashboardState.Ready => Resources.Resources.Dashboard_Hero_Subtitle_Ready,
        DashboardState.HasUpdates => Resources.Resources.Dashboard_Hero_Subtitle_Updates,
        _ => string.Empty
    };

    /// <summary>
    /// Whether the dashboard is in checking state.
    /// </summary>
    public bool IsChecking => CurrentState == DashboardState.Checking;

    /// <summary>
    /// Whether the system is ready for deployment.
    /// </summary>
    public bool IsReady => CurrentState == DashboardState.Ready || CurrentState == DashboardState.HasUpdates;

    /// <summary>
    /// Whether prerequisites are missing.
    /// </summary>
    public bool NeedsPrerequisites => CurrentState == DashboardState.NeedPrereqs;

    /// <summary>
    /// Whether updates are available.
    /// </summary>
    public bool HasUpdatesAvailable => CurrentState == DashboardState.HasUpdates;

    /// <summary>
    /// Whether deployment can be started.
    /// </summary>
    public bool CanStartDeployment => CurrentState == DashboardState.Ready || CurrentState == DashboardState.HasUpdates;

    #endregion

    #region System Info Properties

    /// <summary>
    /// Win11Forge version.
    /// </summary>
    [ObservableProperty]
    private string _appVersion = string.Empty;

    /// <summary>
    /// Computer hostname.
    /// </summary>
    [ObservableProperty]
    private string _hostname = string.Empty;

    /// <summary>
    /// Operating system name.
    /// </summary>
    [ObservableProperty]
    private string _oSName = string.Empty;

    /// <summary>
    /// Operating system build number.
    /// </summary>
    [ObservableProperty]
    private string _oSBuild = string.Empty;

    /// <summary>
    /// Whether running as administrator.
    /// </summary>
    [ObservableProperty]
    private bool _isAdmin;

    /// <summary>
    /// Admin status display text (localized).
    /// </summary>
    [ObservableProperty]
    private string _adminStatus = string.Empty;

    /// <summary>
    /// Whether Winget is installed.
    /// </summary>
    [ObservableProperty]
    private bool _wingetInstalled;

    /// <summary>
    /// Winget version or localized "Not installed".
    /// </summary>
    [ObservableProperty]
    private string _wingetVersion = string.Empty;

    /// <summary>
    /// Whether Chocolatey is installed.
    /// </summary>
    [ObservableProperty]
    private bool _chocolateyInstalled;

    /// <summary>
    /// Chocolatey version or localized "Not installed".
    /// </summary>
    [ObservableProperty]
    private string _chocolateyVersion = string.Empty;

    /// <summary>
    /// Whether PowerShell 7+ is installed.
    /// </summary>
    [ObservableProperty]
    private bool _powerShellInstalled;

    /// <summary>
    /// PowerShell version or localized "Not installed".
    /// </summary>
    [ObservableProperty]
    private string _powerShellVersion = string.Empty;

    #endregion

    #region Stats Properties

    /// <summary>
    /// Number of available profiles.
    /// </summary>
    [ObservableProperty]
    private int _profileCount;

    /// <summary>
    /// Number of available applications.
    /// </summary>
    [ObservableProperty]
    private int _appCount;

    /// <summary>
    /// Number of available updates.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HeroTitle))]
    private int _updateCount;

    #endregion

    #region Scan Properties

    /// <summary>
    /// Whether currently scanning for updates.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotScanning))]
    [NotifyPropertyChangedFor(nameof(ScanButtonText))]
    private bool _isScanning;

    /// <summary>
    /// Current scan progress (number of apps scanned).
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ScanButtonText))]
    private int _scanProgress;

    /// <summary>
    /// Total number of apps to scan.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ScanButtonText))]
    private int _scanTotal;

    /// <summary>
    /// Inverse of IsScanning for button enabling.
    /// </summary>
    public bool IsNotScanning => !IsScanning;

    /// <summary>
    /// Text for the scan button (localized).
    /// </summary>
    public string ScanButtonText => IsScanning
        ? string.Format(Resources.Resources.Dashboard_Scan_Progress, ScanProgress, ScanTotal)
        : Resources.Resources.Dashboard_Scan_Button;

    /// <summary>
    /// Timestamp of the last scan.
    /// </summary>
    [ObservableProperty]
    private DateTime? _lastScanTime;

    /// <summary>
    /// Formatted string for last scan time (localized).
    /// </summary>
    public string LastScanDisplay
    {
        get
        {
            if (!LastScanTime.HasValue) return string.Empty;
            TimeSpan elapsed = DateTime.Now - LastScanTime.Value;
            if (elapsed.TotalSeconds < 60)
                return Resources.Resources.Dashboard_LastCheck_JustNow;
            if (elapsed.TotalMinutes < 60)
                return string.Format(Resources.Resources.Dashboard_LastCheck_MinutesAgo, (int)elapsed.TotalMinutes);
            return string.Format(Resources.Resources.Dashboard_LastCheck_HoursAgo, (int)elapsed.TotalHours);
        }
    }

    #endregion

    #region History Properties

    /// <summary>
    /// Recent deployment history entries.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<DeploymentHistoryEntry> _recentDeployments = [];

    /// <summary>
    /// Whether there are any recent deployments.
    /// </summary>
    public bool HasRecentDeployments => RecentDeployments.Count > 0;

    #endregion

    /// <summary>
    /// Initializes a new instance of DashboardViewModel.
    /// </summary>
    public DashboardViewModel(
        IPowerShellBridge powerShellBridge,
        IDeploymentHistoryService historyService,
        IAppSettingsService settingsService)
    {
        _powerShellBridge = powerShellBridge;
        _historyService = historyService;
        _settingsService = settingsService;
    }

    /// <summary>
    /// Initializes a new instance with just the PowerShell bridge.
    /// </summary>
    public DashboardViewModel(IPowerShellBridge powerShellBridge)
        : this(
            powerShellBridge,
            CreateDesignTimeService<IDeploymentHistoryService>(() => new DeploymentHistoryService()),
            CreateDesignTimeService<IAppSettingsService>(() => new AppSettingsService()))
    {
    }

    private static T CreateDesignTimeService<T>(Func<T> factory)
    {
        if (!DesignerProperties.GetIsInDesignMode(new DependencyObject()))
        {
            throw new InvalidOperationException("This DashboardViewModel constructor is design-time only.");
        }

        return factory();
    }

    /// <inheritdoc/>
    public override async Task InitializeAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        CurrentState = DashboardState.Checking;

        try
        {
            // Phase 1: Load basic information
            CheckingPhaseText = Resources.Resources.Dashboard_Phase_LoadingInfo;
            OnPropertyChanged(nameof(HeroTitle));

            AppVersion = await _powerShellBridge.GetWin11ForgeVersionAsync();
            await LoadSystemInfoAsync();

            // Load stats
            List<string> profiles = await _powerShellBridge.GetAvailableProfilesAsync();
            ProfileCount = profiles.Count;

            List<ApplicationModel> apps = await _powerShellBridge.GetAllApplicationsAsync();
            AppCount = apps.Count;

            // Load recent deployments
            List<DeploymentHistoryEntry> history = await _historyService.GetRecentHistoryAsync(MaxRecentHistory);
            RecentDeployments = new ObservableCollection<DeploymentHistoryEntry>(history);
            OnPropertyChanged(nameof(HasRecentDeployments));

            // Phase 2: Check prerequisites
            CheckingPhaseText = Resources.Resources.Dashboard_Phase_CheckingPrereqs;
            OnPropertyChanged(nameof(HeroTitle));

            await LoadPrerequisitesStatusAsync();

            // Determine state based on prerequisites
            int missingCount = 0;
            if (!WingetInstalled) missingCount++;
            if (!PowerShellInstalled) missingCount++;

            MissingPrerequisitesCount = missingCount;

            if (missingCount > 0)
            {
                CurrentState = DashboardState.NeedPrereqs;
            }
            else
            {
                // Show Ready state immediately, then scan in background
                CurrentState = DashboardState.Ready;

                // Phase 3: Scan for updates in background (non-blocking)
                ScanForUpdatesInBackgroundAsync().SafeFireAndForget();
            }
        }
        catch (PowerShellBridgeException ex)
        {
            ErrorMessage = $"PowerShell error: {ex.Message}";
            CurrentState = DashboardState.Ready;
            Debug.WriteLine($"PowerShellBridgeException in InitializeAsync: {ex}");
        }
        catch (DetectionException ex)
        {
            ErrorMessage = $"Detection error: {ex.Message}";
            CurrentState = DashboardState.Ready;
            Debug.WriteLine($"DetectionException in InitializeAsync: {ex}");
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            CurrentState = DashboardState.Ready;
            Debug.WriteLine($"Unexpected exception in InitializeAsync: {ex}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Loads system information.
    /// </summary>
    private async Task LoadSystemInfoAsync()
    {
        try
        {
            SystemInfoModel systemInfo = await _powerShellBridge.GetSystemInfoAsync();
            if (systemInfo != null)
            {
                Hostname = !string.IsNullOrEmpty(systemInfo.Hostname) ? systemInfo.Hostname : Environment.MachineName;
                OSName = !string.IsNullOrEmpty(systemInfo.WindowsVersion) ? systemInfo.WindowsVersion : "Windows";
                OSBuild = !string.IsNullOrEmpty(systemInfo.WindowsBuild) ? systemInfo.WindowsBuild : Resources.Resources.Common_Unknown;
                IsAdmin = systemInfo.IsAdministrator;
                AdminStatus = IsAdmin ? Resources.Resources.Common_Yes : Resources.Resources.Common_No;
            }
            else
            {
                SetSystemInfoDefaults();
            }
        }
        catch
        {
            SetSystemInfoDefaults();
        }
    }

    private void SetSystemInfoDefaults()
    {
        Hostname = Environment.MachineName;
        OSName = "Windows";
        OSBuild = Environment.OSVersion.Version.Build.ToString();
        IsAdmin = false;
        AdminStatus = Resources.Resources.Common_No;
    }

    /// <summary>
    /// Loads prerequisites status.
    /// </summary>
    private async Task LoadPrerequisitesStatusAsync()
    {
        try
        {
            PrerequisitesStatus prereqStatus = await _powerShellBridge.CheckPrerequisitesAsync();
            if (prereqStatus != null)
            {
                PowerShellInstalled = prereqStatus.PowerShell7Installed;
                PowerShellVersion = prereqStatus.PowerShell7Installed
                    ? (!string.IsNullOrEmpty(prereqStatus.PowerShellVersion) ? prereqStatus.PowerShellVersion : "7+")
                    : Resources.Resources.Status_Missing;

                WingetInstalled = prereqStatus.WingetInstalled;
                WingetVersion = prereqStatus.WingetInstalled
                    ? (!string.IsNullOrEmpty(prereqStatus.WingetVersion) ? prereqStatus.WingetVersion : Resources.Resources.Status_Installed)
                    : Resources.Resources.Status_Missing;

                ChocolateyInstalled = prereqStatus.ChocolateyInstalled;
                ChocolateyVersion = prereqStatus.ChocolateyInstalled
                    ? (!string.IsNullOrEmpty(prereqStatus.ChocolateyVersion) ? prereqStatus.ChocolateyVersion : Resources.Resources.Status_Installed)
                    : Resources.Resources.Status_Missing;
            }
            else
            {
                SetPrerequisitesUnknown();
            }
        }
        catch
        {
            SetPrerequisitesUnknown();
        }
    }

    private void SetPrerequisitesUnknown()
    {
        PowerShellInstalled = false;
        PowerShellVersion = Resources.Resources.Common_Unknown;
        WingetInstalled = false;
        WingetVersion = Resources.Resources.Common_Unknown;
        ChocolateyInstalled = false;
        ChocolateyVersion = Resources.Resources.Common_Unknown;
    }

    /// <summary>
    /// Performs the update scan in the background without blocking initialization.
    /// Updates dashboard state when complete.
    /// </summary>
    private async Task ScanForUpdatesInBackgroundAsync()
    {
        IsScanning = true;
        UpdateCount = 0;
        ScanProgress = 0;
        ScanTotal = 0;

        TaskCompletionSource<int> tcs = new TaskCompletionSource<int>();

        WeakReferenceMessenger.Default.Send(new TriggerScanMessage(
            progressCallback: (current, total) =>
            {
                ScanProgress = current;
                ScanTotal = total;
            },
            completionCallback: (updateCount) =>
            {
                tcs.TrySetResult(updateCount);
            }
        ));

        try
        {
            int timeoutMinutes = _settingsService.LoadSettings()?.UpdateScanTimeoutMinutes ?? 5;
            int resultCount = await tcs.Task.WaitAsync(TimeSpan.FromMinutes(timeoutMinutes));
            UpdateCount = resultCount;
            LastScanTime = DateTime.Now;
            OnPropertyChanged(nameof(LastScanDisplay));

            CurrentState = resultCount > 0 ? DashboardState.HasUpdates : DashboardState.Ready;
        }
        catch (TimeoutException)
        {
            Debug.WriteLine("Background scan timed out");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Background scan failed: {ex.Message}");
        }
        finally
        {
            IsScanning = false;
        }
    }

    /// <summary>
    /// Scans for available updates by triggering the scan in AppsViewModel.
    /// </summary>
    [RelayCommand]
    private void ScanUpdates()
    {
        if (IsScanning) return;

        IsScanning = true;
        UpdateCount = 0;
        ScanProgress = 0;
        ScanTotal = 0;

        WeakReferenceMessenger.Default.Send(new TriggerScanMessage(
            progressCallback: (current, total) =>
            {
                ScanProgress = current;
                ScanTotal = total;
            },
            completionCallback: (updateCount) =>
            {
                UpdateCount = updateCount;
                LastScanTime = DateTime.Now;
                OnPropertyChanged(nameof(LastScanDisplay));
                IsScanning = false;

                // Update state based on scan results
                if (CurrentState == DashboardState.Ready || CurrentState == DashboardState.HasUpdates)
                {
                    CurrentState = updateCount > 0 ? DashboardState.HasUpdates : DashboardState.Ready;
                }
            }
        ));
    }

    /// <summary>
    /// Navigates to the Prerequisites view.
    /// </summary>
    [RelayCommand]
    private void NavigateToPrerequisites()
    {
        WeakReferenceMessenger.Default.Send(new NavigateMessage(ViewIndex.Prerequisites));
    }

    /// <summary>
    /// Navigates to the Apps view.
    /// </summary>
    [RelayCommand]
    private void NavigateToApps()
    {
        WeakReferenceMessenger.Default.Send(new NavigateMessage(ViewIndex.Apps));
    }

    /// <summary>
    /// Navigates to the Deployment view for profile selection.
    /// </summary>
    [RelayCommand]
    private void NavigateToProfiles()
    {
        WeakReferenceMessenger.Default.Send(new NavigateMessage(ViewIndex.Deployment));
    }

    /// <summary>
    /// Navigates to the Apps view with updates filter.
    /// </summary>
    [RelayCommand]
    private void ViewUpdates()
    {
        WeakReferenceMessenger.Default.Send(new NavigateMessage(ViewIndex.Apps));
        WeakReferenceMessenger.Default.Send(new ApplyFilterMessage(StatusFilterOption.HasUpdates, triggerScan: true));
    }

    /// <summary>
    /// Starts the deployment process by navigating to the deployment view.
    /// </summary>
    [RelayCommand]
    private void StartDeployment()
    {
        WeakReferenceMessenger.Default.Send(new NavigateMessage(ViewIndex.Apps));
    }
}

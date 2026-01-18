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
using CommunityToolkit.Mvvm.Messaging;
using Win11Forge.GUI.Messages;
using Win11Forge.GUI.Models;
using Win11Forge.GUI.Services;

namespace Win11Forge.GUI.ViewModels;

/// <summary>
/// ViewModel for the Dashboard view.
/// Displays system information, update scanner, and quick navigation.
/// </summary>
public partial class DashboardViewModel : ViewModelBase
{
    private const int MaxRecentHistory = 5;

    private readonly IPowerShellBridge _powerShellBridge;
    private readonly IDeploymentHistoryService _historyService;

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
    /// Admin status display text.
    /// </summary>
    [ObservableProperty]
    private string _adminStatus = string.Empty;

    /// <summary>
    /// Whether Winget is installed.
    /// </summary>
    [ObservableProperty]
    private bool _wingetInstalled;

    /// <summary>
    /// Winget version or "Not installed".
    /// </summary>
    [ObservableProperty]
    private string _wingetVersion = string.Empty;

    /// <summary>
    /// Whether Chocolatey is installed.
    /// </summary>
    [ObservableProperty]
    private bool _chocolateyInstalled;

    /// <summary>
    /// Chocolatey version or "Not installed".
    /// </summary>
    [ObservableProperty]
    private string _chocolateyVersion = string.Empty;

    /// <summary>
    /// Whether PowerShell 7+ is installed.
    /// </summary>
    [ObservableProperty]
    private bool _powerShellInstalled;

    /// <summary>
    /// PowerShell version or "Not installed".
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
    /// Text for the scan button.
    /// </summary>
    public string ScanButtonText => IsScanning
        ? $"Scanning... ({ScanProgress}/{ScanTotal})"
        : "Scan for Updates";

    /// <summary>
    /// Timestamp of the last scan.
    /// </summary>
    [ObservableProperty]
    private DateTime? _lastScanTime;

    /// <summary>
    /// Formatted string for last scan time.
    /// </summary>
    public string LastScanDisplay
    {
        get
        {
            if (!LastScanTime.HasValue) return string.Empty;
            var elapsed = DateTime.Now - LastScanTime.Value;
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
    public DashboardViewModel(IPowerShellBridge powerShellBridge, IDeploymentHistoryService historyService)
    {
        _powerShellBridge = powerShellBridge;
        _historyService = historyService;
    }

    /// <summary>
    /// Initializes a new instance with just the PowerShell bridge.
    /// </summary>
    public DashboardViewModel(IPowerShellBridge powerShellBridge)
        : this(powerShellBridge, new DeploymentHistoryService())
    {
    }

    /// <inheritdoc/>
    public override async Task InitializeAsync()
    {
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            // Load version
            AppVersion = await _powerShellBridge.GetWin11ForgeVersionAsync();

            // Load system info
            await LoadSystemInfoAsync();

            // Load prerequisites status
            await LoadPrerequisitesStatusAsync();

            // Load stats
            var profiles = await _powerShellBridge.GetAvailableProfilesAsync();
            ProfileCount = profiles.Count;

            var apps = await _powerShellBridge.GetAllApplicationsAsync();
            AppCount = apps.Count;

            // Load recent deployments
            var history = await _historyService.GetRecentHistoryAsync(MaxRecentHistory);
            RecentDeployments = new ObservableCollection<DeploymentHistoryEntry>(history);
            OnPropertyChanged(nameof(HasRecentDeployments));
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
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
            var systemInfo = await _powerShellBridge.GetSystemInfoAsync();
            if (systemInfo != null)
            {
                Hostname = !string.IsNullOrEmpty(systemInfo.Hostname) ? systemInfo.Hostname : Environment.MachineName;
                OSName = !string.IsNullOrEmpty(systemInfo.WindowsVersion) ? systemInfo.WindowsVersion : "Windows";
                OSBuild = !string.IsNullOrEmpty(systemInfo.WindowsBuild) ? systemInfo.WindowsBuild : "Unknown";
                IsAdmin = systemInfo.IsAdministrator;
                AdminStatus = IsAdmin ? "Yes" : "No";
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
        AdminStatus = "No";
    }

    /// <summary>
    /// Loads prerequisites status.
    /// </summary>
    private async Task LoadPrerequisitesStatusAsync()
    {
        try
        {
            var prereqStatus = await _powerShellBridge.CheckPrerequisitesAsync();
            if (prereqStatus != null)
            {
                PowerShellInstalled = prereqStatus.PowerShell7Installed;
                PowerShellVersion = prereqStatus.PowerShell7Installed
                    ? (!string.IsNullOrEmpty(prereqStatus.PowerShellVersion) ? prereqStatus.PowerShellVersion : "7+")
                    : "Not installed";

                WingetInstalled = prereqStatus.WingetInstalled;
                WingetVersion = prereqStatus.WingetInstalled
                    ? (!string.IsNullOrEmpty(prereqStatus.WingetVersion) ? prereqStatus.WingetVersion : "Installed")
                    : "Not installed";

                ChocolateyInstalled = prereqStatus.ChocolateyInstalled;
                ChocolateyVersion = prereqStatus.ChocolateyInstalled
                    ? (!string.IsNullOrEmpty(prereqStatus.ChocolateyVersion) ? prereqStatus.ChocolateyVersion : "Installed")
                    : "Not installed";
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
        PowerShellVersion = "Unknown";
        WingetInstalled = false;
        WingetVersion = "Unknown";
        ChocolateyInstalled = false;
        ChocolateyVersion = "Unknown";
    }

    /// <summary>
    /// Scans for available updates by triggering the scan in AppsViewModel.
    /// This ensures the Applications view has the scan results available.
    /// </summary>
    [RelayCommand]
    private void ScanUpdates()
    {
        if (IsScanning) return;

        IsScanning = true;
        UpdateCount = 0;
        ScanProgress = 0;
        ScanTotal = 0;

        // Send message to AppsViewModel to trigger its scan
        // with callbacks for progress and completion
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
            }
        ));
    }

    /// <summary>
    /// Navigates to the Prerequisites view.
    /// </summary>
    [RelayCommand]
    private void NavigateToPrerequisites()
    {
        WeakReferenceMessenger.Default.Send(new NavigateMessage(NavigateMessage.ViewIndex.Prerequisites));
    }

    /// <summary>
    /// Navigates to the Apps view.
    /// </summary>
    [RelayCommand]
    private void NavigateToApps()
    {
        WeakReferenceMessenger.Default.Send(new NavigateMessage(NavigateMessage.ViewIndex.Apps));
    }

    /// <summary>
    /// Navigates to the Apps view with updates filter.
    /// </summary>
    [RelayCommand]
    private void ViewUpdates()
    {
        WeakReferenceMessenger.Default.Send(new NavigateMessage(NavigateMessage.ViewIndex.Apps));
        WeakReferenceMessenger.Default.Send(new ApplyFilterMessage(StatusFilterOption.HasUpdates, triggerScan: true));
    }
}

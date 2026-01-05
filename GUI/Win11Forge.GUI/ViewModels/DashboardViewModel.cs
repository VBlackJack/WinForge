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
using Win11Forge.GUI.Services;

namespace Win11Forge.GUI.ViewModels;

/// <summary>
/// ViewModel for the Dashboard view.
/// Displays Win11Forge version, system status, and recent activity.
/// </summary>
public partial class DashboardViewModel : ViewModelBase
{
    /// <summary>
    /// Maximum number of recent deployment entries to display.
    /// </summary>
    private const int MaxRecentHistory = 5;

    /// <summary>
    /// Maximum number of history entries to load for total count.
    /// </summary>
    private const int MaxTotalHistory = 100;

    private readonly IPowerShellBridge _powerShellBridge;
    private readonly IDeploymentHistoryService _historyService;

    /// <summary>
    /// Win11Forge version.
    /// </summary>
    [ObservableProperty]
    private string _appVersion = string.Empty;

    /// <summary>
    /// Repository path.
    /// </summary>
    [ObservableProperty]
    private string _repositoryPath = string.Empty;

    /// <summary>
    /// System information.
    /// </summary>
    [ObservableProperty]
    private SystemInfoModel? _systemInfo;

    /// <summary>
    /// Recent deployment history entries.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<DeploymentHistoryEntry> _recentDeployments = [];

    /// <summary>
    /// Total number of deployments.
    /// </summary>
    [ObservableProperty]
    private int _totalDeployments;

    /// <summary>
    /// Number of available profiles.
    /// </summary>
    [ObservableProperty]
    private int _profileCount;

    /// <summary>
    /// Number of available applications.
    /// </summary>
    [ObservableProperty]
    private int _applicationCount;

    /// <summary>
    /// Whether system info is being loaded.
    /// </summary>
    [ObservableProperty]
    private bool _isLoadingSystemInfo;

    /// <summary>
    /// Prerequisites status.
    /// </summary>
    [ObservableProperty]
    private PrerequisitesStatus? _prerequisitesStatus;

    /// <summary>
    /// Whether prerequisites are being checked.
    /// </summary>
    [ObservableProperty]
    private bool _isCheckingPrerequisites;

    /// <summary>
    /// Whether prerequisites are being installed.
    /// </summary>
    [ObservableProperty]
    private bool _isInstallingPrerequisites;

    /// <summary>
    /// Prerequisites installation progress message.
    /// </summary>
    [ObservableProperty]
    private string? _prerequisitesProgressMessage;

    /// <summary>
    /// Initializes a new instance of DashboardViewModel.
    /// </summary>
    public DashboardViewModel(IPowerShellBridge powerShellBridge, IDeploymentHistoryService historyService)
    {
        _powerShellBridge = powerShellBridge;
        _historyService = historyService;
    }

    /// <summary>
    /// Initializes a new instance with just the PowerShell bridge (for backwards compatibility).
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
            // Load basic info first
            AppVersion = await _powerShellBridge.GetWin11ForgeVersionAsync();
            RepositoryPath = _powerShellBridge.RepositoryRoot;

            // Load profile and app counts
            var profiles = await _powerShellBridge.GetAvailableProfilesAsync();
            ProfileCount = profiles.Count;

            var apps = await _powerShellBridge.GetAllApplicationsAsync();
            ApplicationCount = apps.Count;

            // Load recent deployments
            await LoadRecentDeploymentsAsync();

            // Load system info and prerequisites in background
            _ = LoadSystemInfoAsync();
            _ = CheckPrerequisitesAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            AppVersion = Resources.Resources.Common_ErrorFallback;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Loads recent deployment history.
    /// </summary>
    private async Task LoadRecentDeploymentsAsync()
    {
        var history = await _historyService.GetRecentHistoryAsync(MaxRecentHistory);
        RecentDeployments = new ObservableCollection<DeploymentHistoryEntry>(history);

        var allHistory = await _historyService.GetHistoryAsync(MaxTotalHistory);
        TotalDeployments = allHistory.Count;
    }

    /// <summary>
    /// Loads system information asynchronously.
    /// </summary>
    private async Task LoadSystemInfoAsync()
    {
        IsLoadingSystemInfo = true;
        try
        {
            SystemInfo = await _powerShellBridge.GetSystemInfoAsync();
        }
        catch
        {
            // System info is optional, don't fail dashboard
        }
        finally
        {
            IsLoadingSystemInfo = false;
        }
    }

    /// <summary>
    /// Refreshes the dashboard data.
    /// </summary>
    [RelayCommand]
    private async Task RefreshAsync()
    {
        await InitializeAsync();
    }

    /// <summary>
    /// Checks the status of prerequisites.
    /// </summary>
    [RelayCommand]
    private async Task CheckPrerequisitesAsync()
    {
        IsCheckingPrerequisites = true;
        try
        {
            PrerequisitesStatus = await _powerShellBridge.CheckPrerequisitesAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsCheckingPrerequisites = false;
        }
    }

    /// <summary>
    /// Installs missing prerequisites.
    /// </summary>
    [RelayCommand]
    private async Task InstallPrerequisitesAsync()
    {
        IsInstallingPrerequisites = true;
        PrerequisitesProgressMessage = Resources.Resources.Prerequisites_Starting;

        try
        {
            var success = await _powerShellBridge.InstallPrerequisitesAsync(msg =>
            {
                PrerequisitesProgressMessage = msg;
            });

            if (success)
            {
                // Refresh status after installation
                await CheckPrerequisitesAsync();
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsInstallingPrerequisites = false;
            PrerequisitesProgressMessage = null;
        }
    }
}

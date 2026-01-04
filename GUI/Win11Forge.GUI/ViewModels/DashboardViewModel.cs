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

            // Load system info in background
            _ = LoadSystemInfoAsync();
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
        var history = await _historyService.GetRecentHistoryAsync(5);
        RecentDeployments = new ObservableCollection<DeploymentHistoryEntry>(history);

        var allHistory = await _historyService.GetHistoryAsync(100);
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
}

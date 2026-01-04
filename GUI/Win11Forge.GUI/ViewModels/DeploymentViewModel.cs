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
/// ViewModel for the Deployment view.
/// Handles profile selection, application listing, and deployment execution.
/// </summary>
public partial class DeploymentViewModel : ViewModelBase
{
    private readonly IPowerShellBridge _powerShellBridge;
    private readonly IDeploymentHistoryService _historyService;
    private readonly SemaphoreSlim _installSemaphore = new(5);
    private CancellationTokenSource? _cancellationTokenSource;
    private DateTime _deploymentStartTime;

    /// <summary>
    /// List of available deployment profiles.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<string> _availableProfiles = [];

    /// <summary>
    /// Currently selected profile name.
    /// </summary>
    [ObservableProperty]
    private string? _selectedProfile;

    /// <summary>
    /// Currently loaded deployment profile with applications.
    /// </summary>
    [ObservableProperty]
    private DeploymentProfileModel? _currentProfile;

    /// <summary>
    /// Whether a profile is being loaded.
    /// </summary>
    [ObservableProperty]
    private bool _isLoadingProfile;

    /// <summary>
    /// Summary text showing profile statistics.
    /// </summary>
    [ObservableProperty]
    private string _profileSummary = string.Empty;

    /// <summary>
    /// Whether deployment is currently in progress.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeployCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelDeploymentCommand))]
    private bool _isDeploying;

    /// <summary>
    /// Whether to run in simulation mode (no actual changes).
    /// Defaults to true for safety.
    /// </summary>
    [ObservableProperty]
    private bool _isDryRun = true;

    /// <summary>
    /// Current deployment progress message.
    /// </summary>
    [ObservableProperty]
    private string _deploymentStatusMessage = string.Empty;

    /// <summary>
    /// Number of completed installations in current deployment.
    /// </summary>
    [ObservableProperty]
    private int _completedCount;

    /// <summary>
    /// Total number of applications to install in current deployment.
    /// </summary>
    [ObservableProperty]
    private int _totalToInstall;

    /// <summary>
    /// Number of selected applications.
    /// </summary>
    public int SelectedApplicationsCount =>
        CurrentProfile?.Applications.Count(a => a.IsSelected) ?? 0;

    /// <summary>
    /// Whether deployment can start.
    /// </summary>
    public bool CanDeploy => !IsDeploying && SelectedApplicationsCount > 0;

    /// <summary>
    /// Initializes a new instance of DeploymentViewModel.
    /// </summary>
    public DeploymentViewModel(IPowerShellBridge powerShellBridge, IDeploymentHistoryService historyService)
    {
        _powerShellBridge = powerShellBridge;
        _historyService = historyService;
    }

    /// <summary>
    /// Initializes a new instance with just the PowerShell bridge (for backwards compatibility).
    /// </summary>
    public DeploymentViewModel(IPowerShellBridge powerShellBridge)
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
            var profiles = await _powerShellBridge.GetAvailableProfilesAsync();
            AvailableProfiles = new ObservableCollection<string>(profiles);

            // Auto-select first profile if available
            if (AvailableProfiles.Count > 0)
            {
                SelectedProfile = AvailableProfiles[0];
            }
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
    /// Called when SelectedProfile changes.
    /// Triggers profile loading.
    /// </summary>
    partial void OnSelectedProfileChanged(string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            _ = LoadSelectedProfileAsync();
        }
    }

    /// <summary>
    /// Loads the currently selected profile.
    /// </summary>
    [RelayCommand]
    private async Task LoadSelectedProfileAsync()
    {
        if (string.IsNullOrEmpty(SelectedProfile))
        {
            return;
        }

        IsLoadingProfile = true;
        ErrorMessage = null;

        try
        {
            CurrentProfile = await _powerShellBridge.LoadProfileAsync(SelectedProfile);
            UpdateProfileSummary();
            OnPropertyChanged(nameof(SelectedApplicationsCount));
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            CurrentProfile = null;
            ProfileSummary = string.Empty;
        }
        finally
        {
            IsLoadingProfile = false;
        }
    }

    /// <summary>
    /// Toggles selection for all applications.
    /// </summary>
    [RelayCommand]
    private void SelectAll()
    {
        if (CurrentProfile?.Applications == null) return;

        foreach (var app in CurrentProfile.Applications)
        {
            app.IsSelected = true;
        }
        OnPropertyChanged(nameof(SelectedApplicationsCount));
    }

    /// <summary>
    /// Deselects all non-required applications.
    /// </summary>
    [RelayCommand]
    private void SelectNone()
    {
        if (CurrentProfile?.Applications == null) return;

        foreach (var app in CurrentProfile.Applications)
        {
            // Keep required apps selected
            app.IsSelected = app.IsRequired;
        }
        OnPropertyChanged(nameof(SelectedApplicationsCount));
    }

    /// <summary>
    /// Updates the profile summary text.
    /// </summary>
    private void UpdateProfileSummary()
    {
        if (CurrentProfile == null)
        {
            ProfileSummary = string.Empty;
            return;
        }

        var totalApps = CurrentProfile.TotalApplications;
        var requiredApps = CurrentProfile.RequiredApplications;
        var inheritedFrom = CurrentProfile.InheritedFrom.Count > 0
            ? string.Join(" → ", CurrentProfile.InheritedFrom)
            : "-";

        ProfileSummary = $"{totalApps} | {requiredApps} | {inheritedFrom}";
    }

    /// <summary>
    /// Called when an application's IsSelected property changes.
    /// </summary>
    public void OnApplicationSelectionChanged()
    {
        OnPropertyChanged(nameof(SelectedApplicationsCount));
        OnPropertyChanged(nameof(CanDeploy));
        DeployCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Starts deployment of selected applications.
    /// Uses SemaphoreSlim to limit concurrent installations to 5.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanDeploy))]
    private async Task DeployAsync()
    {
        if (CurrentProfile?.Applications == null) return;

        var selectedApps = CurrentProfile.Applications
            .Where(a => a.IsSelected)
            .OrderBy(a => a.Priority)
            .ToList();

        if (selectedApps.Count == 0) return;

        IsDeploying = true;
        CompletedCount = 0;
        TotalToInstall = selectedApps.Count;
        _cancellationTokenSource = new CancellationTokenSource();
        _deploymentStartTime = DateTime.Now;

        // Reset all selected apps to pending state
        foreach (var app in selectedApps)
        {
            app.Status = ApplicationStatus.Pending;
            app.ErrorMessage = null;
            app.LogOutput = string.Empty;
            app.StatusMessage = string.Empty;
        }

        var wasCancelled = false;

        try
        {
            var tasks = selectedApps.Select(app => InstallApplicationWithSemaphoreAsync(
                app,
                _cancellationTokenSource.Token));

            await Task.WhenAll(tasks);

            wasCancelled = _cancellationTokenSource.IsCancellationRequested;
            DeploymentStatusMessage = wasCancelled
                ? Resources.Resources.Progress_Cancelled
                : Resources.Resources.Progress_Complete;
        }
        catch (OperationCanceledException)
        {
            wasCancelled = true;
            DeploymentStatusMessage = Resources.Resources.Progress_Cancelled;
        }
        finally
        {
            IsDeploying = false;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;

            // Record deployment in history
            await RecordDeploymentHistoryAsync(selectedApps, wasCancelled);
        }
    }

    /// <summary>
    /// Records the deployment results to history.
    /// </summary>
    private async Task RecordDeploymentHistoryAsync(List<ApplicationModel> apps, bool wasCancelled)
    {
        if (SelectedProfile == null) return;

        var successCount = apps.Count(a =>
            a.Status == ApplicationStatus.Installed ||
            a.Status == ApplicationStatus.AlreadyInstalled);
        var failedCount = apps.Count(a => a.Status == ApplicationStatus.Failed);
        var skippedCount = apps.Count(a => a.Status == ApplicationStatus.Skipped);

        DeploymentResult result;
        if (wasCancelled)
        {
            result = DeploymentResult.Cancelled;
        }
        else if (failedCount == 0)
        {
            result = DeploymentResult.Success;
        }
        else if (successCount > 0)
        {
            result = DeploymentResult.PartialSuccess;
        }
        else
        {
            result = DeploymentResult.Failed;
        }

        var entry = new DeploymentHistoryEntry
        {
            Date = _deploymentStartTime,
            ProfileName = SelectedProfile,
            Result = result,
            TotalApps = apps.Count,
            SuccessfulApps = successCount,
            FailedApps = failedCount,
            SkippedApps = skippedCount,
            DurationSeconds = (DateTime.Now - _deploymentStartTime).TotalSeconds,
            IsDryRun = IsDryRun
        };

        await _historyService.AddEntryAsync(entry);
    }

    /// <summary>
    /// Installs a single application with semaphore-controlled concurrency.
    /// </summary>
    private async Task InstallApplicationWithSemaphoreAsync(
        ApplicationModel app,
        CancellationToken cancellationToken)
    {
        await _installSemaphore.WaitAsync(cancellationToken);

        try
        {
            if (cancellationToken.IsCancellationRequested)
            {
                app.Status = ApplicationStatus.Skipped;
                app.StatusMessage = Resources.Resources.Status_Skipped;
                return;
            }

            app.Status = ApplicationStatus.Installing;
            app.StatusMessage = Resources.Resources.Status_Installing;

            var result = await _powerShellBridge.InstallApplicationAsync(
                app,
                IsDryRun,
                progress => app.StatusMessage = progress);

            app.LogOutput = result.Logs;

            if (result.Success)
            {
                app.Status = result.AlreadyInstalled
                    ? ApplicationStatus.AlreadyInstalled
                    : ApplicationStatus.Installed;
                app.StatusMessage = result.AlreadyInstalled
                    ? Resources.Resources.Status_AlreadyInstalled
                    : Resources.Resources.Status_Installed;
            }
            else
            {
                app.Status = ApplicationStatus.Failed;
                app.StatusMessage = Resources.Resources.Status_Failed;
                app.ErrorMessage = result.Message;
            }

            CompletedCount++;
            OnPropertyChanged(nameof(CompletedCount));
        }
        catch (OperationCanceledException)
        {
            app.Status = ApplicationStatus.Skipped;
            app.StatusMessage = Resources.Resources.Status_Skipped;
        }
        catch (Exception ex)
        {
            app.Status = ApplicationStatus.Failed;
            app.StatusMessage = Resources.Resources.Status_Failed;
            app.ErrorMessage = ex.Message;
        }
        finally
        {
            _installSemaphore.Release();
        }
    }

    /// <summary>
    /// Cancels the current deployment.
    /// </summary>
    [RelayCommand(CanExecute = nameof(IsDeploying))]
    private void CancelDeployment()
    {
        _cancellationTokenSource?.Cancel();
        DeploymentStatusMessage = Resources.Resources.Progress_Cancelled;
    }
}

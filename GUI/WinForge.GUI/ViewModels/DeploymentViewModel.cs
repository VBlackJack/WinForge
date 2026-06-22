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
using System.Globalization;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinForge.GUI.Models;
using WinForge.GUI.Services;

namespace WinForge.GUI.ViewModels;

/// <summary>
/// Represents the final result type of a deployment.
/// </summary>
public enum DeploymentResultType
{
    /// <summary>No result yet (deployment in progress or not started).</summary>
    None,
    /// <summary>All applications installed successfully.</summary>
    Success,
    /// <summary>Some applications failed but others succeeded.</summary>
    Partial,
    /// <summary>All applications failed.</summary>
    Failed,
    /// <summary>Deployment was cancelled by the user.</summary>
    Cancelled
}

/// <summary>
/// ViewModel for the Deployment Monitoring view.
/// Observes the shared deployment state service and displays progress.
/// </summary>
public partial class DeploymentViewModel : ViewModelBase, IDisposable
{
    private readonly IDeploymentStateService _deploymentStateService;
    private readonly IDialogService _dialogService;
    private bool _disposed;

    /// <summary>
    /// Whether deployment is currently in progress.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PauseDeploymentCommand))]
    [NotifyCanExecuteChangedFor(nameof(ResumeDeploymentCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelDeploymentCommand))]
    private bool _isDeploying;

    /// <summary>
    /// Whether deployment is currently paused.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PauseDeploymentCommand))]
    [NotifyCanExecuteChangedFor(nameof(ResumeDeploymentCommand))]
    private bool _isPaused;

    /// <summary>
    /// Current deployment status message.
    /// </summary>
    [ObservableProperty]
    private string _deploymentStatusMessage = string.Empty;

    /// <summary>
    /// Name of the currently installing application.
    /// </summary>
    [ObservableProperty]
    private string? _currentAppName;

    /// <summary>
    /// Number of completed installations.
    /// </summary>
    [ObservableProperty]
    private int _completedCount;

    /// <summary>
    /// Total number of applications to install.
    /// </summary>
    [ObservableProperty]
    private int _totalToInstall;

    /// <summary>
    /// Deployment progress percentage (0-100).
    /// </summary>
    [ObservableProperty]
    private double _progressPercentage;

    /// <summary>
    /// Elapsed time since deployment started.
    /// </summary>
    [ObservableProperty]
    private string? _elapsedTime;

    /// <summary>
    /// Estimated time remaining for deployment.
    /// </summary>
    [ObservableProperty]
    private string? _estimatedTimeRemaining;

    /// <summary>
    /// Applications being deployed (bound directly from service for real-time updates).
    /// </summary>
    public ObservableCollection<ApplicationModel> DeploymentApplications =>
        _deploymentStateService.Applications;

    /// <summary>
    /// Debug: Direct read from service to verify connectivity.
    /// </summary>
    public bool ServiceIsDeploying => _deploymentStateService.IsDeploying;

    /// <summary>
    /// Debug: Service instance hash code to verify singleton.
    /// </summary>
    public int ServiceHashCode => _deploymentStateService.GetHashCode();

    /// <summary>
    /// Application whose logs are being viewed.
    /// </summary>
    [ObservableProperty]
    private ApplicationModel? _logViewerApplication;

    /// <summary>
    /// Whether the log viewer dialog is open.
    /// </summary>
    [ObservableProperty]
    private bool _isLogViewerOpen;

    /// <summary>
    /// Whether deployment has completed (success, partial, or failed).
    /// </summary>
    [ObservableProperty]
    private bool _isDeploymentComplete;

    /// <summary>
    /// Deployment result type: Success, Partial, Failed, Cancelled.
    /// </summary>
    [ObservableProperty]
    private DeploymentResultType _deploymentResult;

    /// <summary>
    /// Number of successfully installed applications.
    /// </summary>
    [ObservableProperty]
    private int _successCount;

    /// <summary>
    /// Number of failed installations.
    /// </summary>
    [ObservableProperty]
    private int _failureCount;

    /// <summary>
    /// Number of skipped applications.
    /// </summary>
    [ObservableProperty]
    private int _skippedCount;

    /// <summary>
    /// Whether deployment can be paused.
    /// </summary>
    public bool CanPauseDeployment => IsDeploying && !IsPaused;

    /// <summary>
    /// Whether deployment can be resumed.
    /// </summary>
    public bool CanResumeDeployment => IsDeploying && IsPaused;

    /// <summary>
    /// Initializes a new instance of DeploymentViewModel.
    /// </summary>
    public DeploymentViewModel(IDeploymentStateService deploymentStateService, IDialogService? dialogService = null)
    {
        _deploymentStateService = deploymentStateService;
        _dialogService = dialogService ?? new DialogService();

        // Subscribe to state changes
        _deploymentStateService.StateChanged += OnDeploymentStateChanged;

        // Initialize from current state
        SyncFromService();
    }

    /// <inheritdoc/>
    public override Task InitializeAsync()
    {
        // Sync state when view becomes active
        SyncFromService();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when deployment state changes.
    /// </summary>
    private void OnDeploymentStateChanged(object? sender, EventArgs e)
    {
        // Update on UI thread
        Dispatcher? dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher == null) return;

        if (dispatcher.CheckAccess())
        {
            SyncFromService();
        }
        else
        {
            dispatcher.BeginInvoke(SyncFromService);
        }
    }

    /// <summary>
    /// Synchronizes local properties from the shared service.
    /// </summary>
    private void SyncFromService()
    {
        bool wasDeploying = IsDeploying;

        // Use property setters to trigger proper change notifications
        IsDeploying = _deploymentStateService.IsDeploying;
        IsPaused = _deploymentStateService.IsPaused;
        DeploymentStatusMessage = _deploymentStateService.StatusMessage ?? string.Empty;
        CurrentAppName = _deploymentStateService.CurrentAppName;
        CompletedCount = _deploymentStateService.CompletedCount;
        TotalToInstall = _deploymentStateService.TotalCount;
        ProgressPercentage = _deploymentStateService.ProgressPercentage;
        ElapsedTime = _deploymentStateService.ElapsedTime;
        EstimatedTimeRemaining = _deploymentStateService.EstimatedTimeRemaining;

        // Calculate deployment result when deployment completes
        if (wasDeploying && !IsDeploying && TotalToInstall > 0)
        {
            CalculateDeploymentResult();
        }
        else if (!wasDeploying && IsDeploying)
        {
            // Reset result when new deployment starts
            IsDeploymentComplete = false;
            DeploymentResult = DeploymentResultType.None;
            SuccessCount = 0;
            FailureCount = 0;
            SkippedCount = 0;
        }

        // Notify property changes for computed properties and collection
        OnPropertyChanged(nameof(DeploymentApplications));
        OnPropertyChanged(nameof(CanPauseDeployment));
        OnPropertyChanged(nameof(CanResumeDeployment));
    }

    /// <summary>
    /// Calculates the deployment result based on application statuses.
    /// </summary>
    private void CalculateDeploymentResult()
    {
        List<ApplicationModel> apps = DeploymentApplications.ToList();
        SuccessCount = apps.Count(a => a.Status == Models.ApplicationStatus.Installed);
        FailureCount = apps.Count(a => a.Status == Models.ApplicationStatus.Failed);
        SkippedCount = apps.Count(a => a.Status == Models.ApplicationStatus.Skipped ||
                                       a.Status == Models.ApplicationStatus.AlreadyInstalled);

        IsDeploymentComplete = true;

        // Determine result type
        if (_deploymentStateService.IsCancelled)
        {
            DeploymentResult = DeploymentResultType.Cancelled;
        }
        else if (FailureCount == 0 && SuccessCount > 0)
        {
            DeploymentResult = DeploymentResultType.Success;
        }
        else if (SuccessCount > 0 && FailureCount > 0)
        {
            DeploymentResult = DeploymentResultType.Partial;
        }
        else if (FailureCount > 0)
        {
            DeploymentResult = DeploymentResultType.Failed;
        }
        else
        {
            DeploymentResult = DeploymentResultType.Success; // All skipped = success
        }
    }

    /// <summary>
    /// Pauses the current deployment.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanPauseDeployment))]
    private void PauseDeployment()
    {
        _deploymentStateService.RequestPause();
    }

    /// <summary>
    /// Resumes a paused deployment.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanResumeDeployment))]
    private void ResumeDeployment()
    {
        _deploymentStateService.RequestResume();
    }

    /// <summary>
    /// Cancels the current deployment.
    /// </summary>
    [RelayCommand(CanExecute = nameof(IsDeploying))]
    private async Task CancelDeploymentAsync()
    {
        bool confirmed = await _dialogService.ShowConfirmAsync(
            GetLocalizedString("Deployment_Cancel_Title", "Cancel deployment"),
            string.Format(
                CultureInfo.CurrentCulture,
                GetLocalizedString("Deployment_Cancel_Message", "Cancel the current deployment? {0} of {1} items have completed."),
                CompletedCount,
                TotalToInstall),
            Resources.Resources.Btn_Cancel,
            Resources.Resources.Common_Cancel);

        if (confirmed)
        {
            _deploymentStateService.RequestCancel();
        }
    }

    /// <summary>
    /// Opens the log viewer for a specific application.
    /// </summary>
    [RelayCommand]
    private void ViewLogs(ApplicationModel? app)
    {
        if (app == null) return;

        LogViewerApplication = app;
        IsLogViewerOpen = true;
    }

    /// <summary>
    /// Closes the log viewer dialog.
    /// </summary>
    [RelayCommand]
    private void CloseLogViewer()
    {
        IsLogViewerOpen = false;
        LogViewerApplication = null;
    }

    /// <summary>
    /// Disposes resources and unsubscribes from events.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes managed resources.
    /// </summary>
    /// <param name="disposing">True if disposing managed resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            // Unsubscribe from service events to prevent memory leaks
            _deploymentStateService.StateChanged -= OnDeploymentStateChanged;
        }

        _disposed = true;
    }

    private static string GetLocalizedString(string resourceKey, string fallback)
    {
        return Resources.Resources.ResourceManager.GetString(resourceKey, Resources.Resources.Culture) ?? fallback;
    }
}

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
/// ViewModel for the Deployment Monitoring view.
/// Observes the shared deployment state service and displays progress.
/// </summary>
public partial class DeploymentViewModel : ViewModelBase, IDisposable
{
    private readonly IDeploymentStateService _deploymentStateService;
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
    public DeploymentViewModel(IDeploymentStateService deploymentStateService)
    {
        _deploymentStateService = deploymentStateService;

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
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
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

        // Notify property changes for computed properties and collection
        OnPropertyChanged(nameof(DeploymentApplications));
        OnPropertyChanged(nameof(CanPauseDeployment));
        OnPropertyChanged(nameof(CanResumeDeployment));
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
    private void CancelDeployment()
    {
        _deploymentStateService.RequestCancel();
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
}

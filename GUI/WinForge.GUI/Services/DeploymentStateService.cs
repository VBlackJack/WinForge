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
using WinForge.GUI.Models;

namespace WinForge.GUI.Services;

/// <summary>
/// Shared service for deployment state that can be observed by multiple ViewModels.
/// </summary>
public interface IDeploymentStateService : IDisposable
{
    /// <summary>
    /// Whether a deployment is currently in progress.
    /// </summary>
    bool IsDeploying { get; }

    /// <summary>
    /// Whether the deployment is paused.
    /// </summary>
    bool IsPaused { get; }

    /// <summary>
    /// Whether the deployment was cancelled.
    /// </summary>
    bool IsCancelled { get; }

    /// <summary>
    /// Current deployment status message.
    /// </summary>
    string? StatusMessage { get; }

    /// <summary>
    /// Name of the app currently being processed.
    /// </summary>
    string? CurrentAppName { get; }

    /// <summary>
    /// Number of completed installations.
    /// </summary>
    int CompletedCount { get; }

    /// <summary>
    /// Total number of apps to install.
    /// </summary>
    int TotalCount { get; }

    /// <summary>
    /// Progress percentage (0-100).
    /// </summary>
    double ProgressPercentage { get; }

    /// <summary>
    /// Elapsed time string.
    /// </summary>
    string? ElapsedTime { get; }

    /// <summary>
    /// Estimated time remaining string.
    /// </summary>
    string? EstimatedTimeRemaining { get; }

    /// <summary>
    /// List of applications being deployed.
    /// </summary>
    ObservableCollection<ApplicationModel> Applications { get; }

    /// <summary>
    /// Event raised when any property changes.
    /// </summary>
    event EventHandler? StateChanged;

    /// <summary>
    /// Event raised when pause is requested from monitoring view.
    /// </summary>
    event EventHandler? PauseRequested;

    /// <summary>
    /// Event raised when resume is requested from monitoring view.
    /// </summary>
    event EventHandler? ResumeRequested;

    /// <summary>
    /// Event raised when cancel is requested from monitoring view.
    /// </summary>
    event EventHandler? CancelRequested;

    /// <summary>
    /// Starts a deployment session.
    /// </summary>
    void StartDeployment(IEnumerable<ApplicationModel> apps);

    /// <summary>
    /// Updates the current deployment progress.
    /// </summary>
    void UpdateProgress(string? currentAppName, int completed, int total, string? statusMessage);

    /// <summary>
    /// Updates time information.
    /// </summary>
    void UpdateTime(string? elapsed, string? remaining);

    /// <summary>
    /// Sets the paused state.
    /// </summary>
    void SetPaused(bool isPaused);

    /// <summary>
    /// Ends the deployment session.
    /// </summary>
    void EndDeployment();

    /// <summary>
    /// Clears application logs and resets state after successful deployment.
    /// This frees memory used by log strings while keeping applications for display.
    /// </summary>
    void ClearApplicationLogs();

    /// <summary>
    /// Requests pause from the monitoring view.
    /// </summary>
    void RequestPause();

    /// <summary>
    /// Requests resume from the monitoring view.
    /// </summary>
    void RequestResume();

    /// <summary>
    /// Requests cancel from the monitoring view.
    /// </summary>
    void RequestCancel();
}

/// <summary>
/// Implementation of the deployment state service.
/// </summary>
public partial class DeploymentStateService : ObservableObject, IDeploymentStateService
{
    private readonly object _applicationsLock = new();
    private bool _isDeploying;
    private bool _isPaused;
    private bool _isCancelled;
    private string? _statusMessage;
    private string? _currentAppName;
    private int _completedCount;
    private int _totalCount;
    private double _progressPercentage;
    private string? _elapsedTime;
    private string? _estimatedTimeRemaining;

    /// <summary>
    /// Initializes a new instance with thread-safe collection support.
    /// </summary>
    public DeploymentStateService()
    {
        // Enable cross-thread access to the ObservableCollection
        System.Windows.Data.BindingOperations.EnableCollectionSynchronization(Applications, _applicationsLock);
    }

    public bool IsDeploying
    {
        get => _isDeploying;
        private set => SetProperty(ref _isDeploying, value);
    }

    public bool IsPaused
    {
        get => _isPaused;
        private set => SetProperty(ref _isPaused, value);
    }

    public bool IsCancelled
    {
        get => _isCancelled;
        private set => SetProperty(ref _isCancelled, value);
    }

    public string? StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string? CurrentAppName
    {
        get => _currentAppName;
        private set => SetProperty(ref _currentAppName, value);
    }

    public int CompletedCount
    {
        get => _completedCount;
        private set => SetProperty(ref _completedCount, value);
    }

    public int TotalCount
    {
        get => _totalCount;
        private set => SetProperty(ref _totalCount, value);
    }

    public double ProgressPercentage
    {
        get => _progressPercentage;
        private set => SetProperty(ref _progressPercentage, value);
    }

    public string? ElapsedTime
    {
        get => _elapsedTime;
        private set => SetProperty(ref _elapsedTime, value);
    }

    public string? EstimatedTimeRemaining
    {
        get => _estimatedTimeRemaining;
        private set => SetProperty(ref _estimatedTimeRemaining, value);
    }

    public ObservableCollection<ApplicationModel> Applications { get; } = [];

    public event EventHandler? StateChanged;
    public event EventHandler? PauseRequested;
    public event EventHandler? ResumeRequested;
    public event EventHandler? CancelRequested;

    public void StartDeployment(IEnumerable<ApplicationModel> apps)
    {
        lock (_applicationsLock)
        {
            // Clear logs from previous deployment to free memory
            foreach (ApplicationModel existingApp in Applications)
            {
                existingApp.LogOutput = string.Empty;
            }

            Applications.Clear();
            foreach (ApplicationModel app in apps)
            {
                // Clear any existing logs on the new apps
                app.LogOutput = string.Empty;
                Applications.Add(app);
            }

            // Thread-safety: Update all state properties inside the lock
            // to prevent race conditions with UI thread reading inconsistent state
            IsDeploying = true;
            IsPaused = false;
            IsCancelled = false;
            CompletedCount = 0;
            TotalCount = Applications.Count;
            ProgressPercentage = 0;
            StatusMessage = Resources.Resources.Progress_Deploying;
            CurrentAppName = null;
            ElapsedTime = null;
            EstimatedTimeRemaining = null;
        }

        RaiseStateChanged();
    }

    public void UpdateProgress(string? currentAppName, int completed, int total, string? statusMessage)
    {
        CurrentAppName = currentAppName;
        CompletedCount = completed;
        TotalCount = total;
        ProgressPercentage = total > 0 ? (double)completed / total * 100 : 0;
        StatusMessage = statusMessage;

        RaiseStateChanged();
    }

    public void UpdateTime(string? elapsed, string? remaining)
    {
        ElapsedTime = elapsed;
        EstimatedTimeRemaining = remaining;

        RaiseStateChanged();
    }

    public void SetPaused(bool isPaused)
    {
        IsPaused = isPaused;
        StatusMessage = isPaused
            ? Resources.Resources.Status_Paused
            : Resources.Resources.Progress_Deploying;

        RaiseStateChanged();
    }

    public void EndDeployment()
    {
        IsDeploying = false;
        IsPaused = false;
        StatusMessage = Resources.Resources.Progress_Complete;
        CurrentAppName = null;

        RaiseStateChanged();
    }

    public void ClearApplicationLogs()
    {
        lock (_applicationsLock)
        {
            foreach (ApplicationModel app in Applications)
            {
                // Clear log output to free memory (can be 256KB per app)
                app.LogOutput = string.Empty;
            }
        }

        RaiseStateChanged();
    }

    public void RequestPause()
    {
        PauseRequested?.Invoke(this, EventArgs.Empty);
    }

    public void RequestResume()
    {
        ResumeRequested?.Invoke(this, EventArgs.Empty);
    }

    public void RequestCancel()
    {
        IsCancelled = true;
        CancelRequested?.Invoke(this, EventArgs.Empty);
    }

    private void RaiseStateChanged()
    {
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private bool _disposed;

    /// <summary>
    /// Releases all resources used by the DeploymentStateService.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases the unmanaged resources and optionally releases managed resources.
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            lock (_applicationsLock)
            {
                Applications.Clear();
            }
            StateChanged = null;
            PauseRequested = null;
            ResumeRequested = null;
            CancelRequested = null;
        }

        _disposed = true;
    }
}

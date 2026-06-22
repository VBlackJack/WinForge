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
using Win11Forge.GUI.Models;
using Win11Forge.GUI.Services;
using Win11Forge.GUI.ViewModels;

namespace Win11Forge.GUI.Tests;

/// <summary>
/// Tests for DeploymentViewModel - deployment monitoring and control.
/// </summary>
public class DeploymentViewModelTests
{
    /// <summary>
    /// Verifies that the ViewModel initializes with service state.
    /// </summary>
    [Fact]
    public void Constructor_ShouldSyncWithService()
    {
        // Arrange
        MockDeploymentStateServiceForTests stateService = new MockDeploymentStateServiceForTests();

        // Act
        DeploymentViewModel viewModel = new DeploymentViewModel(stateService);

        // Assert
        Assert.False(viewModel.IsDeploying);
        Assert.False(viewModel.IsPaused);
    }

    /// <summary>
    /// Verifies that IsDeploying reflects service state.
    /// </summary>
    [Fact]
    public void IsDeploying_ShouldReflectServiceState()
    {
        // Arrange
        MockDeploymentStateServiceForTests stateService = new MockDeploymentStateServiceForTests { MockIsDeploying = true };

        // Act
        DeploymentViewModel viewModel = new DeploymentViewModel(stateService);

        // Assert
        Assert.True(viewModel.IsDeploying);
    }

    /// <summary>
    /// Verifies that PauseCommand can be executed when deploying.
    /// </summary>
    [Fact]
    public void PauseCommand_ShouldBeExecutableWhenDeploying()
    {
        // Arrange
        MockDeploymentStateServiceForTests stateService = new MockDeploymentStateServiceForTests { MockIsDeploying = true };
        DeploymentViewModel viewModel = new DeploymentViewModel(stateService);

        // Assert
        Assert.True(viewModel.CanPauseDeployment);
    }

    /// <summary>
    /// Verifies that ResumeCommand can be executed when paused.
    /// </summary>
    [Fact]
    public void ResumeCommand_ShouldBeExecutableWhenPaused()
    {
        // Arrange
        MockDeploymentStateServiceForTests stateService = new MockDeploymentStateServiceForTests
        {
            MockIsDeploying = true,
            MockIsPaused = true
        };
        DeploymentViewModel viewModel = new DeploymentViewModel(stateService);

        // Assert
        Assert.True(viewModel.CanResumeDeployment);
    }

    /// <summary>
    /// Verifies that CancelCommand invokes service cancel.
    /// </summary>
    [Fact]
    public async Task CancelCommand_WhenConfirmed_ShouldInvokeServiceCancel()
    {
        // Arrange
        MockDeploymentStateServiceForTests stateService = new MockDeploymentStateServiceForTests { MockIsDeploying = true };
        TestDialogService dialogService = new TestDialogService();
        dialogService.QueueConfirmResult(true);
        DeploymentViewModel viewModel = new DeploymentViewModel(stateService, dialogService);

        // Act
        await viewModel.CancelDeploymentCommand.ExecuteAsync(null);

        // Assert
        (string Title, string Message, string? ConfirmText, string? CancelText) request = Assert.Single(dialogService.ConfirmRequests);
        Assert.Contains("5", request.Message, StringComparison.Ordinal);
        Assert.Contains("10", request.Message, StringComparison.Ordinal);
        Assert.True(stateService.CancelRequestedFlag);
    }

    /// <summary>
    /// Verifies that declining cancel confirmation does not notify the deployment service.
    /// </summary>
    [Fact]
    public async Task CancelCommand_WhenDeclined_ShouldNotInvokeServiceCancel()
    {
        // Arrange
        MockDeploymentStateServiceForTests stateService = new MockDeploymentStateServiceForTests { MockIsDeploying = true };
        TestDialogService dialogService = new TestDialogService();
        dialogService.QueueConfirmResult(false);
        DeploymentViewModel viewModel = new DeploymentViewModel(stateService, dialogService);

        // Act
        await viewModel.CancelDeploymentCommand.ExecuteAsync(null);

        // Assert
        Assert.Single(dialogService.ConfirmRequests);
        Assert.False(stateService.CancelRequestedFlag);
    }

    /// <summary>
    /// Verifies that the ViewModel properly disposes and unsubscribes.
    /// </summary>
    [Fact]
    public void Dispose_ShouldUnsubscribeFromEvents()
    {
        // Arrange
        MockDeploymentStateServiceForTests stateService = new MockDeploymentStateServiceForTests();
        DeploymentViewModel viewModel = new DeploymentViewModel(stateService);

        // Act
        viewModel.Dispose();

        // Assert - Should not throw when triggering event after disposal
        stateService.TriggerStateChanged();
    }

    /// <summary>
    /// Verifies that progress is synced from service.
    /// </summary>
    [Fact]
    public void ProgressPercentage_ShouldSyncFromService()
    {
        // Arrange
        MockDeploymentStateServiceForTests stateService = new MockDeploymentStateServiceForTests { MockProgressPercentage = 50.0 };

        // Act
        DeploymentViewModel viewModel = new DeploymentViewModel(stateService);

        // Assert
        Assert.Equal(50.0, viewModel.ProgressPercentage);
    }
}

/// <summary>
/// Mock implementation of IDeploymentStateService for DeploymentViewModel tests.
/// </summary>
internal class MockDeploymentStateServiceForTests : IDeploymentStateService
{
    public bool MockIsDeploying { get; set; }
    public bool MockIsPaused { get; set; }
    public bool MockIsCancelled { get; set; }
    public double MockProgressPercentage { get; set; }
    public bool CancelRequestedFlag { get; private set; }

    public bool IsDeploying => MockIsDeploying;
    public bool IsPaused => MockIsPaused;
    public bool IsCancelled => MockIsCancelled;
    public string? StatusMessage => "Test Status";
    public string? CurrentAppName => "TestApp";
    public int CompletedCount => 5;
    public int TotalCount => 10;
    public double ProgressPercentage => MockProgressPercentage;
    public string? ElapsedTime => "00:05:00";
    public string? EstimatedTimeRemaining => "00:05:00";
    public ObservableCollection<ApplicationModel> Applications { get; } = [];

    public event EventHandler? StateChanged;
    public event EventHandler? PauseRequested;
    public event EventHandler? ResumeRequested;
    public event EventHandler? CancelRequested;

    public void StartDeployment(IEnumerable<ApplicationModel> apps) { }
    public void UpdateProgress(string? currentAppName, int completed, int total, string? statusMessage) { }
    public void UpdateTime(string? elapsed, string? remaining) { }
    public void SetPaused(bool isPaused) => MockIsPaused = isPaused;
    public void EndDeployment() => MockIsDeploying = false;
    public void ClearApplicationLogs() { }

    public void RequestPause() => PauseRequested?.Invoke(this, EventArgs.Empty);
    public void RequestResume() => ResumeRequested?.Invoke(this, EventArgs.Empty);
    public void RequestCancel()
    {
        CancelRequestedFlag = true;
        MockIsCancelled = true;
        CancelRequested?.Invoke(this, EventArgs.Empty);
    }

    public void TriggerStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);

    public void Dispose() { }
}

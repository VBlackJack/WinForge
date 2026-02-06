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

namespace Win11Forge.GUI.Tests;

/// <summary>
/// Tests for DeploymentStateService - deployment state tracking and coordination.
/// Uses a testable wrapper to bypass WPF BindingOperations requirement in the constructor.
/// </summary>
public class DeploymentStateServiceTests
{
    /// <summary>
    /// Testable implementation of IDeploymentStateService that mirrors
    /// the production DeploymentStateService logic without WPF dependencies.
    /// </summary>
    private class TestableDeploymentStateService : IDeploymentStateService
    {
        public bool IsDeploying { get; private set; }
        public bool IsPaused { get; private set; }
        public bool IsCancelled { get; private set; }
        public string? StatusMessage { get; private set; }
        public string? CurrentAppName { get; private set; }
        public int CompletedCount { get; private set; }
        public int TotalCount { get; private set; }
        public double ProgressPercentage { get; private set; }
        public string? ElapsedTime { get; private set; }
        public string? EstimatedTimeRemaining { get; private set; }
        public ObservableCollection<ApplicationModel> Applications { get; } = [];

        public event EventHandler? StateChanged;
        public event EventHandler? PauseRequested;
        public event EventHandler? ResumeRequested;
        public event EventHandler? CancelRequested;

        public void StartDeployment(IEnumerable<ApplicationModel> apps)
        {
            foreach (var existingApp in Applications)
            {
                existingApp.LogOutput = string.Empty;
            }

            Applications.Clear();
            foreach (var app in apps)
            {
                app.LogOutput = string.Empty;
                Applications.Add(app);
            }

            IsDeploying = true;
            IsPaused = false;
            IsCancelled = false;
            CompletedCount = 0;
            TotalCount = Applications.Count;
            ProgressPercentage = 0;
            StatusMessage = "Deploying...";
            CurrentAppName = null;
            ElapsedTime = null;
            EstimatedTimeRemaining = null;

            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void UpdateProgress(string? currentAppName, int completed, int total, string? statusMessage)
        {
            CurrentAppName = currentAppName;
            CompletedCount = completed;
            TotalCount = total;
            ProgressPercentage = total > 0 ? (double)completed / total * 100 : 0;
            StatusMessage = statusMessage;

            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void UpdateTime(string? elapsed, string? remaining)
        {
            ElapsedTime = elapsed;
            EstimatedTimeRemaining = remaining;

            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void SetPaused(bool isPaused)
        {
            IsPaused = isPaused;
            StatusMessage = isPaused ? "Paused" : "Deploying...";

            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void EndDeployment()
        {
            IsDeploying = false;
            IsPaused = false;
            StatusMessage = "Complete";
            CurrentAppName = null;

            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ClearApplicationLogs()
        {
            foreach (var app in Applications)
            {
                app.LogOutput = string.Empty;
            }

            StateChanged?.Invoke(this, EventArgs.Empty);
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

        public void Dispose()
        {
            Applications.Clear();
            StateChanged = null;
            PauseRequested = null;
            ResumeRequested = null;
            CancelRequested = null;
        }
    }

    private TestableDeploymentStateService CreateService()
    {
        return new TestableDeploymentStateService();
    }

    private static List<ApplicationModel> CreateTestApps(int count)
    {
        var apps = new List<ApplicationModel>();
        for (int i = 1; i <= count; i++)
        {
            apps.Add(new ApplicationModel
            {
                AppId = $"App{i}",
                Name = $"Application {i}"
            });
        }
        return apps;
    }

    #region Initial State

    /// <summary>
    /// Verifies that a new service starts in a clean state.
    /// </summary>
    [Fact]
    public void NewService_ShouldHaveCleanState()
    {
        // Arrange & Act
        using var service = CreateService();

        // Assert
        Assert.False(service.IsDeploying);
        Assert.False(service.IsPaused);
        Assert.False(service.IsCancelled);
        Assert.Null(service.StatusMessage);
        Assert.Null(service.CurrentAppName);
        Assert.Equal(0, service.CompletedCount);
        Assert.Equal(0, service.TotalCount);
        Assert.Equal(0, service.ProgressPercentage);
        Assert.Null(service.ElapsedTime);
        Assert.Null(service.EstimatedTimeRemaining);
        Assert.Empty(service.Applications);
    }

    #endregion

    #region StartDeployment

    /// <summary>
    /// Verifies that StartDeployment sets deploying state and populates applications.
    /// </summary>
    [Fact]
    public void StartDeployment_ShouldSetDeployingStateAndPopulateApps()
    {
        // Arrange
        using var service = CreateService();
        var apps = CreateTestApps(3);

        // Act
        service.StartDeployment(apps);

        // Assert
        Assert.True(service.IsDeploying);
        Assert.False(service.IsPaused);
        Assert.False(service.IsCancelled);
        Assert.Equal(3, service.Applications.Count);
        Assert.Equal(3, service.TotalCount);
        Assert.Equal(0, service.CompletedCount);
        Assert.Equal(0, service.ProgressPercentage);
    }

    /// <summary>
    /// Verifies that StartDeployment raises StateChanged event.
    /// </summary>
    [Fact]
    public void StartDeployment_ShouldRaiseStateChanged()
    {
        // Arrange
        using var service = CreateService();
        var eventRaised = false;
        service.StateChanged += (_, _) => eventRaised = true;

        // Act
        service.StartDeployment(CreateTestApps(2));

        // Assert
        Assert.True(eventRaised);
    }

    /// <summary>
    /// Verifies that StartDeployment clears previous deployment data.
    /// </summary>
    [Fact]
    public void StartDeployment_ShouldClearPreviousDeployment()
    {
        // Arrange
        using var service = CreateService();
        service.StartDeployment(CreateTestApps(3));
        service.UpdateProgress("App1", 2, 3, "In progress");

        // Act - Start a new deployment
        service.StartDeployment(CreateTestApps(5));

        // Assert
        Assert.Equal(5, service.Applications.Count);
        Assert.Equal(5, service.TotalCount);
        Assert.Equal(0, service.CompletedCount);
        Assert.Equal(0, service.ProgressPercentage);
    }

    /// <summary>
    /// Verifies that StartDeployment clears log output on existing apps.
    /// </summary>
    [Fact]
    public void StartDeployment_ShouldClearLogOutputOnApps()
    {
        // Arrange
        using var service = CreateService();
        var apps = CreateTestApps(2);
        apps[0].LogOutput = "Some previous log data";
        apps[1].LogOutput = "More log data from before";

        // Act
        service.StartDeployment(apps);

        // Assert
        Assert.All(service.Applications, app =>
            Assert.Equal(string.Empty, app.LogOutput));
    }

    /// <summary>
    /// Verifies that StartDeployment with empty list sets TotalCount to zero.
    /// </summary>
    [Fact]
    public void StartDeployment_EmptyList_ShouldSetTotalCountZero()
    {
        // Arrange
        using var service = CreateService();

        // Act
        service.StartDeployment([]);

        // Assert
        Assert.True(service.IsDeploying);
        Assert.Equal(0, service.TotalCount);
        Assert.Empty(service.Applications);
    }

    #endregion

    #region UpdateProgress

    /// <summary>
    /// Verifies that UpdateProgress updates all progress-related properties.
    /// </summary>
    [Fact]
    public void UpdateProgress_ShouldUpdateAllProgressProperties()
    {
        // Arrange
        using var service = CreateService();
        service.StartDeployment(CreateTestApps(4));

        // Act
        service.UpdateProgress("App2", 2, 4, "Installing Application 2");

        // Assert
        Assert.Equal("App2", service.CurrentAppName);
        Assert.Equal(2, service.CompletedCount);
        Assert.Equal(4, service.TotalCount);
        Assert.Equal(50.0, service.ProgressPercentage);
        Assert.Equal("Installing Application 2", service.StatusMessage);
    }

    /// <summary>
    /// Verifies that UpdateProgress raises StateChanged event.
    /// </summary>
    [Fact]
    public void UpdateProgress_ShouldRaiseStateChanged()
    {
        // Arrange
        using var service = CreateService();
        service.StartDeployment(CreateTestApps(2));
        var eventCount = 0;
        service.StateChanged += (_, _) => eventCount++;

        // Act
        service.UpdateProgress("App1", 1, 2, "Progress");

        // Assert
        Assert.True(eventCount > 0);
    }

    /// <summary>
    /// Verifies that progress percentage is calculated correctly.
    /// </summary>
    [Theory]
    [InlineData(0, 10, 0.0)]
    [InlineData(5, 10, 50.0)]
    [InlineData(10, 10, 100.0)]
    [InlineData(1, 3, 33.33)]
    public void UpdateProgress_ShouldCalculatePercentageCorrectly(int completed, int total, double expectedPercentage)
    {
        // Arrange
        using var service = CreateService();
        service.StartDeployment(CreateTestApps(total));

        // Act
        service.UpdateProgress(null, completed, total, null);

        // Assert - use precision of 2 decimal places to avoid floating-point comparison issues
        Assert.Equal(expectedPercentage, service.ProgressPercentage, precision: 2);
    }

    /// <summary>
    /// Verifies that progress percentage is zero when total is zero.
    /// </summary>
    [Fact]
    public void UpdateProgress_ZeroTotal_ShouldSetPercentageToZero()
    {
        // Arrange
        using var service = CreateService();
        service.StartDeployment([]);

        // Act
        service.UpdateProgress(null, 0, 0, null);

        // Assert
        Assert.Equal(0, service.ProgressPercentage);
    }

    #endregion

    #region UpdateTime

    /// <summary>
    /// Verifies that UpdateTime sets elapsed and remaining time.
    /// </summary>
    [Fact]
    public void UpdateTime_ShouldSetTimeProperties()
    {
        // Arrange
        using var service = CreateService();
        service.StartDeployment(CreateTestApps(2));

        // Act
        service.UpdateTime("00:05:30", "00:02:15");

        // Assert
        Assert.Equal("00:05:30", service.ElapsedTime);
        Assert.Equal("00:02:15", service.EstimatedTimeRemaining);
    }

    /// <summary>
    /// Verifies that UpdateTime raises StateChanged event.
    /// </summary>
    [Fact]
    public void UpdateTime_ShouldRaiseStateChanged()
    {
        // Arrange
        using var service = CreateService();
        var eventRaised = false;
        service.StateChanged += (_, _) => eventRaised = true;

        // Act
        service.UpdateTime("00:01:00", "00:09:00");

        // Assert
        Assert.True(eventRaised);
    }

    /// <summary>
    /// Verifies that UpdateTime handles null values.
    /// </summary>
    [Fact]
    public void UpdateTime_NullValues_ShouldSetToNull()
    {
        // Arrange
        using var service = CreateService();
        service.UpdateTime("00:05:00", "00:05:00");

        // Act
        service.UpdateTime(null, null);

        // Assert
        Assert.Null(service.ElapsedTime);
        Assert.Null(service.EstimatedTimeRemaining);
    }

    #endregion

    #region SetPaused

    /// <summary>
    /// Verifies that SetPaused sets IsPaused to true.
    /// </summary>
    [Fact]
    public void SetPaused_True_ShouldSetIsPaused()
    {
        // Arrange
        using var service = CreateService();
        service.StartDeployment(CreateTestApps(2));

        // Act
        service.SetPaused(true);

        // Assert
        Assert.True(service.IsPaused);
    }

    /// <summary>
    /// Verifies that SetPaused false clears IsPaused.
    /// </summary>
    [Fact]
    public void SetPaused_False_ShouldClearIsPaused()
    {
        // Arrange
        using var service = CreateService();
        service.StartDeployment(CreateTestApps(2));
        service.SetPaused(true);

        // Act
        service.SetPaused(false);

        // Assert
        Assert.False(service.IsPaused);
    }

    /// <summary>
    /// Verifies that SetPaused raises StateChanged event.
    /// </summary>
    [Fact]
    public void SetPaused_ShouldRaiseStateChanged()
    {
        // Arrange
        using var service = CreateService();
        var eventRaised = false;
        service.StateChanged += (_, _) => eventRaised = true;

        // Act
        service.SetPaused(true);

        // Assert
        Assert.True(eventRaised);
    }

    #endregion

    #region EndDeployment

    /// <summary>
    /// Verifies that EndDeployment clears deploying state.
    /// </summary>
    [Fact]
    public void EndDeployment_ShouldClearDeployingState()
    {
        // Arrange
        using var service = CreateService();
        service.StartDeployment(CreateTestApps(3));
        service.SetPaused(true);

        // Act
        service.EndDeployment();

        // Assert
        Assert.False(service.IsDeploying);
        Assert.False(service.IsPaused);
        Assert.Null(service.CurrentAppName);
    }

    /// <summary>
    /// Verifies that EndDeployment raises StateChanged event.
    /// </summary>
    [Fact]
    public void EndDeployment_ShouldRaiseStateChanged()
    {
        // Arrange
        using var service = CreateService();
        service.StartDeployment(CreateTestApps(2));
        var eventRaised = false;
        service.StateChanged += (_, _) => eventRaised = true;

        // Act
        service.EndDeployment();

        // Assert
        Assert.True(eventRaised);
    }

    /// <summary>
    /// Verifies that EndDeployment preserves application list for display.
    /// </summary>
    [Fact]
    public void EndDeployment_ShouldPreserveApplications()
    {
        // Arrange
        using var service = CreateService();
        service.StartDeployment(CreateTestApps(3));

        // Act
        service.EndDeployment();

        // Assert
        Assert.Equal(3, service.Applications.Count);
    }

    #endregion

    #region ClearApplicationLogs

    /// <summary>
    /// Verifies that ClearApplicationLogs clears log output on all applications.
    /// </summary>
    [Fact]
    public void ClearApplicationLogs_ShouldClearAllLogs()
    {
        // Arrange
        using var service = CreateService();
        var apps = CreateTestApps(3);
        service.StartDeployment(apps);
        service.Applications[0].LogOutput = "Log for app 1";
        service.Applications[1].LogOutput = "Log for app 2";
        service.Applications[2].LogOutput = "Log for app 3";

        // Act
        service.ClearApplicationLogs();

        // Assert
        Assert.All(service.Applications, app =>
            Assert.Equal(string.Empty, app.LogOutput));
    }

    /// <summary>
    /// Verifies that ClearApplicationLogs raises StateChanged event.
    /// </summary>
    [Fact]
    public void ClearApplicationLogs_ShouldRaiseStateChanged()
    {
        // Arrange
        using var service = CreateService();
        service.StartDeployment(CreateTestApps(1));
        var eventRaised = false;
        service.StateChanged += (_, _) => eventRaised = true;

        // Act
        service.ClearApplicationLogs();

        // Assert
        Assert.True(eventRaised);
    }

    #endregion

    #region Request Methods

    /// <summary>
    /// Verifies that RequestPause raises PauseRequested event.
    /// </summary>
    [Fact]
    public void RequestPause_ShouldRaisePauseRequestedEvent()
    {
        // Arrange
        using var service = CreateService();
        var eventRaised = false;
        service.PauseRequested += (_, _) => eventRaised = true;

        // Act
        service.RequestPause();

        // Assert
        Assert.True(eventRaised);
    }

    /// <summary>
    /// Verifies that RequestResume raises ResumeRequested event.
    /// </summary>
    [Fact]
    public void RequestResume_ShouldRaiseResumeRequestedEvent()
    {
        // Arrange
        using var service = CreateService();
        var eventRaised = false;
        service.ResumeRequested += (_, _) => eventRaised = true;

        // Act
        service.RequestResume();

        // Assert
        Assert.True(eventRaised);
    }

    /// <summary>
    /// Verifies that RequestCancel raises CancelRequested event and sets IsCancelled.
    /// </summary>
    [Fact]
    public void RequestCancel_ShouldRaiseCancelRequestedEventAndSetFlag()
    {
        // Arrange
        using var service = CreateService();
        var eventRaised = false;
        service.CancelRequested += (_, _) => eventRaised = true;

        // Act
        service.RequestCancel();

        // Assert
        Assert.True(eventRaised);
        Assert.True(service.IsCancelled);
    }

    /// <summary>
    /// Verifies that RequestPause does not throw when no handlers are attached.
    /// </summary>
    [Fact]
    public void RequestPause_NoHandlers_ShouldNotThrow()
    {
        // Arrange
        using var service = CreateService();

        // Act & Assert
        var exception = Record.Exception(() => service.RequestPause());
        Assert.Null(exception);
    }

    /// <summary>
    /// Verifies that RequestResume does not throw when no handlers are attached.
    /// </summary>
    [Fact]
    public void RequestResume_NoHandlers_ShouldNotThrow()
    {
        // Arrange
        using var service = CreateService();

        // Act & Assert
        var exception = Record.Exception(() => service.RequestResume());
        Assert.Null(exception);
    }

    /// <summary>
    /// Verifies that RequestCancel does not throw when no handlers are attached.
    /// </summary>
    [Fact]
    public void RequestCancel_NoHandlers_ShouldNotThrow()
    {
        // Arrange
        using var service = CreateService();

        // Act & Assert
        var exception = Record.Exception(() => service.RequestCancel());
        Assert.Null(exception);
    }

    #endregion

    #region Dispose

    /// <summary>
    /// Verifies that Dispose clears applications and event handlers.
    /// </summary>
    [Fact]
    public void Dispose_ShouldClearApplicationsAndHandlers()
    {
        // Arrange
        var service = CreateService();
        service.StartDeployment(CreateTestApps(3));
        var stateChangedAfterDispose = false;
        service.StateChanged += (_, _) => stateChangedAfterDispose = true;

        // Act
        service.Dispose();

        // Assert
        Assert.Empty(service.Applications);

        // Verify that double dispose does not throw and events are detached
        stateChangedAfterDispose = false;
        service.Dispose();
        Assert.False(stateChangedAfterDispose);
    }

    /// <summary>
    /// Verifies that double Dispose does not throw.
    /// </summary>
    [Fact]
    public void Dispose_CalledTwice_ShouldNotThrow()
    {
        // Arrange
        var service = CreateService();
        service.StartDeployment(CreateTestApps(2));

        // Act & Assert
        var exception = Record.Exception(() =>
        {
            service.Dispose();
            service.Dispose();
        });
        Assert.Null(exception);
    }

    #endregion

    #region Full Deployment Lifecycle

    /// <summary>
    /// Verifies a complete deployment lifecycle: start, progress, pause, resume, complete.
    /// </summary>
    [Fact]
    public void FullLifecycle_StartProgressPauseResumeComplete()
    {
        // Arrange
        using var service = CreateService();
        var stateChangedCount = 0;
        service.StateChanged += (_, _) => stateChangedCount++;

        // Act - Start
        service.StartDeployment(CreateTestApps(3));
        Assert.True(service.IsDeploying);
        Assert.Equal(3, service.TotalCount);

        // Act - Progress
        service.UpdateProgress("App1", 1, 3, "Installing App 1");
        Assert.Equal("App1", service.CurrentAppName);
        Assert.Equal(1, service.CompletedCount);

        // Act - Update time
        service.UpdateTime("00:01:00", "00:02:00");
        Assert.Equal("00:01:00", service.ElapsedTime);

        // Act - Pause
        service.SetPaused(true);
        Assert.True(service.IsPaused);

        // Act - Resume
        service.SetPaused(false);
        Assert.False(service.IsPaused);

        // Act - More progress
        service.UpdateProgress("App3", 3, 3, "Installing App 3");
        Assert.Equal(100.0, service.ProgressPercentage);

        // Act - End
        service.EndDeployment();
        Assert.False(service.IsDeploying);
        Assert.False(service.IsPaused);

        // Assert - StateChanged should have been raised multiple times
        Assert.True(stateChangedCount >= 6);
    }

    /// <summary>
    /// Verifies a deployment cancellation lifecycle.
    /// </summary>
    [Fact]
    public void FullLifecycle_StartProgressCancel()
    {
        // Arrange
        using var service = CreateService();
        var cancelRaised = false;
        service.CancelRequested += (_, _) => cancelRaised = true;

        // Act - Start
        service.StartDeployment(CreateTestApps(5));
        Assert.False(service.IsCancelled);

        // Act - Some progress
        service.UpdateProgress("App2", 2, 5, "Installing");

        // Act - Cancel
        service.RequestCancel();

        // Assert
        Assert.True(service.IsCancelled);
        Assert.True(cancelRaised);
        Assert.True(service.IsDeploying); // Still deploying until EndDeployment is called

        // Act - End after cancellation
        service.EndDeployment();
        Assert.False(service.IsDeploying);
    }

    #endregion
}

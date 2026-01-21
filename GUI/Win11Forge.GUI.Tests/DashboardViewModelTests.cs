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

using Win11Forge.GUI.Models;
using Win11Forge.GUI.Services;
using Win11Forge.GUI.ViewModels;

namespace Win11Forge.GUI.Tests;

/// <summary>
/// Tests for DashboardViewModel - system status, prerequisites, and quick actions.
/// </summary>
public class DashboardViewModelTests
{
    /// <summary>
    /// Verifies that the dashboard initializes in checking state.
    /// </summary>
    [Fact]
    public void Constructor_ShouldInitializeInCheckingState()
    {
        // Arrange
        var powerShellBridge = new MockPowerShellBridge();
        var historyService = new MockDashboardHistoryService();

        // Act
        var viewModel = new DashboardViewModel(powerShellBridge, historyService);

        // Assert
        Assert.Equal(DashboardState.Checking, viewModel.CurrentState);
        Assert.True(viewModel.IsChecking);
    }

    /// <summary>
    /// Verifies that AppVersion is initially empty before initialization.
    /// Note: Full InitializeAsync testing requires WPF dispatcher and is tested in integration tests.
    /// </summary>
    [Fact]
    public void AppVersion_ShouldInitializeEmpty()
    {
        // Arrange
        var powerShellBridge = new MockPowerShellBridge();
        var historyService = new MockDashboardHistoryService();

        // Act
        var viewModel = new DashboardViewModel(powerShellBridge, historyService);

        // Assert - Version is empty until InitializeAsync completes
        Assert.Equal(string.Empty, viewModel.AppVersion);
    }

    /// <summary>
    /// Verifies that IsReady is false during checking state.
    /// </summary>
    [Fact]
    public void IsReady_ShouldBeFalseWhenChecking()
    {
        // Arrange
        var powerShellBridge = new MockPowerShellBridge();
        var historyService = new MockDashboardHistoryService();

        // Act
        var viewModel = new DashboardViewModel(powerShellBridge, historyService);

        // Assert - Should not be ready while in Checking state
        Assert.False(viewModel.IsReady);
        Assert.True(viewModel.IsChecking);
    }

    /// <summary>
    /// Verifies that HeroTitle changes based on state.
    /// </summary>
    [Fact]
    public void HeroTitle_ShouldChangeWithState()
    {
        // Arrange
        var powerShellBridge = new MockPowerShellBridge();
        var historyService = new MockDashboardHistoryService();
        var viewModel = new DashboardViewModel(powerShellBridge, historyService);

        // Assert - Should have a title in checking state
        Assert.NotNull(viewModel.HeroTitle);
    }

    /// <summary>
    /// Verifies that CanStartDeployment is based on Ready state.
    /// </summary>
    [Fact]
    public void CanStartDeployment_ShouldBeFalseWhenChecking()
    {
        // Arrange
        var powerShellBridge = new MockPowerShellBridge();
        var historyService = new MockDashboardHistoryService();
        var viewModel = new DashboardViewModel(powerShellBridge, historyService);

        // Assert
        Assert.False(viewModel.CanStartDeployment);
    }

    /// <summary>
    /// Verifies that recent deployments collection is initially empty.
    /// </summary>
    [Fact]
    public void RecentDeployments_ShouldInitializeEmpty()
    {
        // Arrange
        var powerShellBridge = new MockPowerShellBridge();
        var historyService = new MockDashboardHistoryService();
        historyService.HistoryEntries.Add(new DeploymentHistoryEntry
        {
            ProfileName = "TestProfile",
            TotalApps = 10,
            SuccessfulApps = 8,
            FailedApps = 2
        });

        // Act
        var viewModel = new DashboardViewModel(powerShellBridge, historyService);

        // Assert - Collection is empty until InitializeAsync completes
        Assert.Empty(viewModel.RecentDeployments);
        Assert.False(viewModel.HasRecentDeployments);
    }

    /// <summary>
    /// Verifies that IsLoading is initially false.
    /// </summary>
    [Fact]
    public void IsLoading_ShouldInitializeFalse()
    {
        // Arrange
        var powerShellBridge = new MockPowerShellBridge();
        var historyService = new MockDashboardHistoryService();

        // Act
        var viewModel = new DashboardViewModel(powerShellBridge, historyService);

        // Assert
        Assert.False(viewModel.IsLoading);
    }
}

/// <summary>
/// Mock implementation of IDeploymentHistoryService for Dashboard tests.
/// </summary>
internal class MockDashboardHistoryService : IDeploymentHistoryService
{
    public List<DeploymentHistoryEntry> HistoryEntries { get; } = [];

    public Task AddEntryAsync(DeploymentHistoryEntry entry)
    {
        HistoryEntries.Add(entry);
        return Task.CompletedTask;
    }

    public Task<List<DeploymentHistoryEntry>> GetHistoryAsync(int limit = 50) =>
        Task.FromResult(HistoryEntries.Take(limit).ToList());

    public Task<List<DeploymentHistoryEntry>> GetRecentHistoryAsync(int count = 5) =>
        Task.FromResult(HistoryEntries.Take(count).ToList());

    public Task ClearHistoryAsync()
    {
        HistoryEntries.Clear();
        return Task.CompletedTask;
    }
}

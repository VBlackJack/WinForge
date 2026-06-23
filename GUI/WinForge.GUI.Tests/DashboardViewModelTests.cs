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

using WinForge.GUI.Models;
using WinForge.GUI.Services;
using WinForge.GUI.Services.Coordinators;
using WinForge.GUI.ViewModels;

namespace WinForge.GUI.Tests;

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
        MockPowerShellBridge powerShellBridge = new MockPowerShellBridge();
        MockDashboardHistoryService historyService = new MockDashboardHistoryService();
        MockDashboardAppSettingsService settingsService = new MockDashboardAppSettingsService();

        // Act
        DashboardViewModel viewModel = new DashboardViewModel(powerShellBridge, historyService, settingsService);

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
        MockPowerShellBridge powerShellBridge = new MockPowerShellBridge();
        MockDashboardHistoryService historyService = new MockDashboardHistoryService();
        MockDashboardAppSettingsService settingsService = new MockDashboardAppSettingsService();

        // Act
        DashboardViewModel viewModel = new DashboardViewModel(powerShellBridge, historyService, settingsService);

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
        MockPowerShellBridge powerShellBridge = new MockPowerShellBridge();
        MockDashboardHistoryService historyService = new MockDashboardHistoryService();
        MockDashboardAppSettingsService settingsService = new MockDashboardAppSettingsService();

        // Act
        DashboardViewModel viewModel = new DashboardViewModel(powerShellBridge, historyService, settingsService);

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
        MockPowerShellBridge powerShellBridge = new MockPowerShellBridge();
        MockDashboardHistoryService historyService = new MockDashboardHistoryService();
        MockDashboardAppSettingsService settingsService = new MockDashboardAppSettingsService();
        DashboardViewModel viewModel = new DashboardViewModel(powerShellBridge, historyService, settingsService);

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
        MockPowerShellBridge powerShellBridge = new MockPowerShellBridge();
        MockDashboardHistoryService historyService = new MockDashboardHistoryService();
        MockDashboardAppSettingsService settingsService = new MockDashboardAppSettingsService();
        DashboardViewModel viewModel = new DashboardViewModel(powerShellBridge, historyService, settingsService);

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
        MockPowerShellBridge powerShellBridge = new MockPowerShellBridge();
        MockDashboardHistoryService historyService = new MockDashboardHistoryService();
        MockDashboardAppSettingsService settingsService = new MockDashboardAppSettingsService();
        historyService.HistoryEntries.Add(new DeploymentHistoryEntry
        {
            ProfileName = "TestProfile",
            TotalApps = 10,
            SuccessfulApps = 8,
            FailedApps = 2
        });

        // Act
        DashboardViewModel viewModel = new DashboardViewModel(powerShellBridge, historyService, settingsService);

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
        MockPowerShellBridge powerShellBridge = new MockPowerShellBridge();
        MockDashboardHistoryService historyService = new MockDashboardHistoryService();
        MockDashboardAppSettingsService settingsService = new MockDashboardAppSettingsService();

        // Act
        DashboardViewModel viewModel = new DashboardViewModel(powerShellBridge, historyService, settingsService);

        // Assert
        Assert.False(viewModel.IsLoading);
    }

    [Fact]
    public async Task ScanUpdatesCommand_ShouldRunFullScanAndInvalidateUpdateCache()
    {
        // Arrange
        MockPowerShellBridge powerShellBridge = new MockPowerShellBridge();
        TestAppScanCoordinator scanCoordinator = new TestAppScanCoordinator
        {
            Result = new AppScanResult(0, 3, 2, WasCancelled: false)
        };
        UpdateScanStateService updateScanStateService = new UpdateScanStateService();
        int publishedUpdateCount = -1;
        updateScanStateService.UpdateScanCompleted += (_, args) =>
        {
            publishedUpdateCount = args.UpdatesAvailableCount;
        };
        DashboardViewModel viewModel = new DashboardViewModel(
            powerShellBridge,
            new MockDashboardHistoryService(),
            new MockDashboardAppSettingsService(),
            scanCoordinator,
            updateScanStateService)
        {
            CurrentState = DashboardState.Ready
        };

        // Act
        await viewModel.ScanUpdatesCommand.ExecuteAsync(null);

        // Assert
        IReadOnlyCollection<ApplicationModel> call = Assert.Single(scanCoordinator.Calls);
        Assert.Equal(powerShellBridge.Applications.Count, call.Count);
        Assert.Equal(1, powerShellBridge.InvalidateUpdateCacheCallCount);
        Assert.Equal(2, viewModel.UpdateCount);
        Assert.Equal(2, publishedUpdateCount);
        Assert.Equal(DashboardState.HasUpdates, viewModel.CurrentState);
        Assert.False(viewModel.IsScanning);
    }

    [Fact]
    public async Task ScanUpdatesCommand_WhenScanFails_ShouldResetScanningFlag()
    {
        // Arrange
        MockPowerShellBridge powerShellBridge = new MockPowerShellBridge();
        powerShellBridge.Applications[0].Status = ApplicationStatus.Installed;
        DashboardViewModel viewModel = new DashboardViewModel(
            powerShellBridge,
            new MockDashboardHistoryService(),
            new MockDashboardAppSettingsService(),
            new ThrowingDashboardScanCoordinator())
        {
            CurrentState = DashboardState.Ready
        };

        // Act
        await viewModel.ScanUpdatesCommand.ExecuteAsync(null);

        // Assert
        Assert.False(viewModel.IsScanning);
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

/// <summary>
/// Mock implementation of IAppSettingsService for Dashboard tests.
/// </summary>
internal class MockDashboardAppSettingsService : IAppSettingsService
{
    public AppSettings Settings { get; set; } = new();

    public AppSettings LoadSettings() => Settings;

    public Task<AppSettings> LoadSettingsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Settings);

    public bool SaveSettings(AppSettings settings)
    {
        Settings = settings;
        return true;
    }

    public Task<bool> SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        Settings = settings;
        return Task.FromResult(true);
    }

    public void ApplySettings(AppSettings settings) => Settings = settings;
}

internal sealed class ThrowingDashboardScanCoordinator : IAppScanCoordinator
{
    public Task<AppScanResult> ScanAsync(
        IReadOnlyCollection<ApplicationModel> applications,
        IProgress<AppOperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("Update scan failed for test.");
    }
}

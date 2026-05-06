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

using Moq;
using Win11Forge.GUI.Models;
using Win11Forge.GUI.Services;
using Win11Forge.GUI.Services.Coordinators;

namespace Win11Forge.GUI.Tests;

public class AppScanCoordinatorTests
{
    [Fact]
    public async Task ScanAsync_WithEmptyList_ShouldReturnEmptyResult()
    {
        var coordinator = CreateCoordinator();

        var result = await coordinator.ScanAsync([]);

        Assert.Equal(0, result.Total);
        Assert.Equal(0, result.InstalledCount);
        Assert.Equal(0, result.UpdatesAvailableCount);
        Assert.False(result.WasCancelled);
    }

    [Fact]
    public async Task ScanAsync_WithBatchInstalledApps_ShouldAggregateInstalledCount()
    {
        var apps = CreateApps("App1", "App2");
        var bridge = CreateBridge();
        bridge
            .Setup(x => x.GetBatchApplicationStatusAsync(It.IsAny<IReadOnlyList<ApplicationModel>>()))
            .ReturnsAsync(new Dictionary<string, BatchAppStatus>
            {
                ["App1"] = new(ApplicationStatus.Installed, "1.0"),
                ["App2"] = new(ApplicationStatus.AlreadyInstalled, "2.0")
            });
        var detection = CreateDetectionService();
        detection.Setup(x => x.GetAvailableUpdatesAsync()).ReturnsAsync([]);
        var coordinator = CreateCoordinator(bridge.Object, detection.Object);

        var result = await coordinator.ScanAsync(apps);

        Assert.Equal(2, result.Total);
        Assert.Equal(2, result.InstalledCount);
        Assert.Equal(0, result.UpdatesAvailableCount);
        Assert.All(apps, app => Assert.Equal(Resources.Resources.Status_Installed, app.StatusMessage));
    }

    [Fact]
    public async Task ScanAsync_WithMixedBatchResults_ShouldMarkMissingApps()
    {
        var apps = CreateApps("InstalledApp", "MissingApp");
        var bridge = CreateBridge();
        bridge
            .Setup(x => x.GetBatchApplicationStatusAsync(It.IsAny<IReadOnlyList<ApplicationModel>>()))
            .ReturnsAsync(new Dictionary<string, BatchAppStatus>
            {
                ["InstalledApp"] = new(ApplicationStatus.Installed, "1.0"),
                ["MissingApp"] = new(ApplicationStatus.Pending, null)
            });
        var detection = CreateDetectionService();
        detection.Setup(x => x.GetAvailableUpdatesAsync()).ReturnsAsync([]);
        var coordinator = CreateCoordinator(bridge.Object, detection.Object);

        var result = await coordinator.ScanAsync(apps);

        Assert.Equal(1, result.InstalledCount);
        Assert.Equal(ApplicationStatus.Installed, apps[0].Status);
        Assert.Equal(ApplicationStatus.Pending, apps[1].Status);
        Assert.Equal(Resources.Resources.Status_Missing, apps[1].StatusMessage);
    }

    [Fact]
    public async Task ScanAsync_WithBatchUpdates_ShouldUseDetectionService()
    {
        var apps = CreateApps("App1");
        apps[0].Name = "Application One";
        var bridge = CreateBridge();
        bridge
            .Setup(x => x.GetBatchApplicationStatusAsync(It.IsAny<IReadOnlyList<ApplicationModel>>()))
            .ReturnsAsync(new Dictionary<string, BatchAppStatus>
            {
                ["App1"] = new(ApplicationStatus.Installed, "1.0")
            });
        var detection = CreateDetectionService();
        detection
            .Setup(x => x.GetAvailableUpdatesAsync())
            .ReturnsAsync(
            [
                new UpdateInfo
                {
                    Id = "App1",
                    Name = "Application One",
                    CurrentVersion = "1.0",
                    NewVersion = "2.0"
                }
            ]);
        var coordinator = CreateCoordinator(bridge.Object, detection.Object);

        var result = await coordinator.ScanAsync(apps);

        Assert.Equal(1, result.UpdatesAvailableCount);
        Assert.Equal(ApplicationStatus.UpdateAvailable, apps[0].Status);
        Assert.Equal("2.0", apps[0].AvailableVersion);
        detection.Verify(x => x.GetAvailableUpdatesAsync(), Times.Once);
    }

    [Fact]
    public async Task ScanAsync_WhenCancelled_ShouldReturnCancelledResult()
    {
        var apps = CreateApps("App1", "App2");
        var bridge = CreateBridge();
        bridge
            .Setup(x => x.GetBatchApplicationStatusAsync(It.IsAny<IReadOnlyList<ApplicationModel>>()))
            .ReturnsAsync((Dictionary<string, BatchAppStatus>?)null);
        bridge
            .Setup(x => x.GetApplicationStatusAsync(It.IsAny<string>()))
            .Callback(() => throw new OperationCanceledException())
            .ReturnsAsync(ApplicationStatus.Pending);
        var coordinator = CreateCoordinator(bridge.Object);

        var result = await coordinator.ScanAsync(apps, cancellationToken: new CancellationToken(canceled: false));

        Assert.True(result.WasCancelled);
        Assert.Equal(2, result.Total);
    }

    private static List<ApplicationModel> CreateApps(params string[] appIds)
    {
        return appIds
            .Select(appId => new ApplicationModel { AppId = appId, Name = appId })
            .ToList();
    }

    private static Mock<IPowerShellBridge> CreateBridge()
    {
        var bridge = new Mock<IPowerShellBridge>();
        bridge
            .Setup(x => x.GetBatchApplicationStatusAsync(It.IsAny<IReadOnlyList<ApplicationModel>>()))
            .ReturnsAsync((Dictionary<string, BatchAppStatus>?)null);
        bridge
            .Setup(x => x.CheckApplicationUpdateAsync(It.IsAny<ApplicationModel>()))
            .ReturnsAsync(UpdateCheckResult.UpToDate("1.0"));
        return bridge;
    }

    private static Mock<IApplicationDetectionService> CreateDetectionService()
    {
        var detection = new Mock<IApplicationDetectionService>();
        detection.Setup(x => x.GetAvailableUpdatesAsync()).ReturnsAsync([]);
        return detection;
    }

    private static AppScanCoordinator CreateCoordinator(
        IPowerShellBridge? bridge = null,
        IApplicationDetectionService? detectionService = null)
    {
        var settings = new MockAppSettingsService
        {
            SettingsToReturn = new AppSettings { MaxParallelScans = 2 }
        };

        return new AppScanCoordinator(
            bridge ?? CreateBridge().Object,
            detectionService ?? CreateDetectionService().Object,
            settings);
    }
}

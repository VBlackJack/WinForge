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
        AppScanCoordinator coordinator = CreateCoordinator();

        AppScanResult result = await coordinator.ScanAsync([]);

        Assert.Equal(0, result.Total);
        Assert.Equal(0, result.InstalledCount);
        Assert.Equal(0, result.UpdatesAvailableCount);
        Assert.False(result.WasCancelled);
    }

    [Fact]
    public async Task ScanAsync_WithBatchInstalledApps_ShouldAggregateInstalledCount()
    {
        List<ApplicationModel> apps = CreateApps("App1", "App2");
        Mock<IPowerShellBridge> bridge = CreateBridge();
        bridge
            .Setup(x => x.GetBatchApplicationStatusAsync(It.IsAny<IReadOnlyList<ApplicationModel>>()))
            .ReturnsAsync(new Dictionary<string, BatchAppStatus>
            {
                ["App1"] = new(ApplicationStatus.Installed, "1.0"),
                ["App2"] = new(ApplicationStatus.AlreadyInstalled, "2.0")
            });
        Mock<IApplicationDetectionService> detection = CreateDetectionService();
        detection.Setup(x => x.GetAvailableUpdatesAsync()).ReturnsAsync([]);
        AppScanCoordinator coordinator = CreateCoordinator(bridge.Object, detection.Object);

        AppScanResult result = await coordinator.ScanAsync(apps);

        Assert.Equal(2, result.Total);
        Assert.Equal(2, result.InstalledCount);
        Assert.Equal(0, result.UpdatesAvailableCount);
        Assert.All(apps, app => Assert.Equal(Resources.Resources.Status_Installed, app.StatusMessage));
    }

    [Fact]
    public async Task ScanAsync_WithMixedBatchResults_ShouldMarkMissingApps()
    {
        List<ApplicationModel> apps = CreateApps("InstalledApp", "MissingApp");
        Mock<IPowerShellBridge> bridge = CreateBridge();
        bridge
            .Setup(x => x.GetBatchApplicationStatusAsync(It.IsAny<IReadOnlyList<ApplicationModel>>()))
            .ReturnsAsync(new Dictionary<string, BatchAppStatus>
            {
                ["InstalledApp"] = new(ApplicationStatus.Installed, "1.0"),
                ["MissingApp"] = new(ApplicationStatus.Pending, null)
            });
        Mock<IApplicationDetectionService> detection = CreateDetectionService();
        detection.Setup(x => x.GetAvailableUpdatesAsync()).ReturnsAsync([]);
        AppScanCoordinator coordinator = CreateCoordinator(bridge.Object, detection.Object);

        AppScanResult result = await coordinator.ScanAsync(apps);

        Assert.Equal(1, result.InstalledCount);
        Assert.Equal(ApplicationStatus.Installed, apps[0].Status);
        Assert.Equal(ApplicationStatus.Pending, apps[1].Status);
        Assert.Equal(Resources.Resources.Status_Missing, apps[1].StatusMessage);
    }

    [Fact]
    public async Task ScanAsync_WithBatchUpdates_ShouldUseDetectionService()
    {
        List<ApplicationModel> apps = CreateApps("App1");
        apps[0].Name = "Application One";
        Mock<IPowerShellBridge> bridge = CreateBridge();
        bridge
            .Setup(x => x.GetBatchApplicationStatusAsync(It.IsAny<IReadOnlyList<ApplicationModel>>()))
            .ReturnsAsync(new Dictionary<string, BatchAppStatus>
            {
                ["App1"] = new(ApplicationStatus.Installed, "1.0")
            });
        Mock<IApplicationDetectionService> detection = CreateDetectionService();
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
        AppScanCoordinator coordinator = CreateCoordinator(bridge.Object, detection.Object);

        AppScanResult result = await coordinator.ScanAsync(apps);

        Assert.Equal(1, result.UpdatesAvailableCount);
        Assert.Equal(ApplicationStatus.UpdateAvailable, apps[0].Status);
        Assert.Equal("2.0", apps[0].AvailableVersion);
        detection.Verify(x => x.GetAvailableUpdatesAsync(), Times.Once);
    }

    [Fact]
    public async Task ScanAsync_WithBatchUpdates_ShouldMatchWingetIdAndVersionedName()
    {
        List<ApplicationModel> apps = CreateApps("7Zip");
        apps[0].Name = "7-Zip";
        Mock<IPowerShellBridge> bridge = CreateBridge();
        bridge
            .Setup(x => x.GetBatchApplicationStatusAsync(It.IsAny<IReadOnlyList<ApplicationModel>>()))
            .ReturnsAsync(new Dictionary<string, BatchAppStatus>
            {
                ["7Zip"] = new(ApplicationStatus.Installed, "25.01")
            });
        Mock<IApplicationDetectionService> detection = CreateDetectionService();
        detection
            .Setup(x => x.GetAvailableUpdatesAsync())
            .ReturnsAsync(
            [
                new UpdateInfo
                {
                    Id = "7zip.7zip",
                    Name = "7-Zip 25.01 (x64)",
                    CurrentVersion = "25.01",
                    NewVersion = "26.01",
                    Source = "winget"
                }
            ]);
        AppScanCoordinator coordinator = CreateCoordinator(bridge.Object, detection.Object);

        AppScanResult result = await coordinator.ScanAsync(apps);

        Assert.Equal(1, result.UpdatesAvailableCount);
        Assert.Equal(ApplicationStatus.UpdateAvailable, apps[0].Status);
        Assert.Equal("25.01", apps[0].CurrentVersion);
        Assert.Equal("26.01", apps[0].AvailableVersion);
    }

    [Fact]
    public async Task ScanAsync_WhenCancelled_ShouldReturnCancelledResult()
    {
        List<ApplicationModel> apps = CreateApps("App1", "App2");
        Mock<IPowerShellBridge> bridge = CreateBridge();
        bridge
            .Setup(x => x.GetBatchApplicationStatusAsync(It.IsAny<IReadOnlyList<ApplicationModel>>()))
            .ReturnsAsync((Dictionary<string, BatchAppStatus>?)null);
        bridge
            .Setup(x => x.GetApplicationStatusAsync(It.IsAny<string>()))
            .Callback(() => throw new OperationCanceledException())
            .ReturnsAsync(ApplicationStatus.Pending);
        AppScanCoordinator coordinator = CreateCoordinator(bridge.Object);

        AppScanResult result = await coordinator.ScanAsync(apps, cancellationToken: new CancellationToken(canceled: false));

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
        Mock<IPowerShellBridge> bridge = new Mock<IPowerShellBridge>();
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
        Mock<IApplicationDetectionService> detection = new Mock<IApplicationDetectionService>();
        detection.Setup(x => x.GetAvailableUpdatesAsync()).ReturnsAsync([]);
        return detection;
    }

    private static AppScanCoordinator CreateCoordinator(
        IPowerShellBridge? bridge = null,
        IApplicationDetectionService? detectionService = null)
    {
        MockAppSettingsService settings = new MockAppSettingsService
        {
            SettingsToReturn = new AppSettings { MaxParallelScans = 2 }
        };

        return new AppScanCoordinator(
            bridge ?? CreateBridge().Object,
            detectionService ?? CreateDetectionService().Object,
            settings);
    }
}

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

public class AppUpdateCoordinatorTests
{
    [Fact]
    public async Task ScanForUpdatesAsync_WithEmptyList_ShouldReturnEmptyResult()
    {
        var bridge = CreateBridge();
        var coordinator = CreateCoordinator(bridge.Object);

        var result = await coordinator.ScanForUpdatesAsync([]);

        Assert.Equal(0, result.Total);
        Assert.Equal(0, result.UpdatesAvailableCount);
        Assert.False(result.WasCancelled);
        bridge.Verify(x => x.CheckApplicationUpdateAsync(It.IsAny<ApplicationModel>()), Times.Never);
    }

    [Fact]
    public async Task ScanForUpdatesAsync_WithAvailableUpdates_ShouldAggregateUpdateCount()
    {
        var apps = CreateApps("App1", "App2", "App3");
        var bridge = CreateBridge();
        bridge
            .Setup(x => x.CheckApplicationUpdateAsync(It.IsAny<ApplicationModel>()))
            .ReturnsAsync((ApplicationModel app) =>
                app.AppId == "App2"
                    ? UpdateCheckResult.UpdateAvailable("1.0", "2.0")
                    : UpdateCheckResult.UpToDate("1.0"));
        var coordinator = CreateCoordinator(bridge.Object);

        var result = await coordinator.ScanForUpdatesAsync(apps);

        Assert.Equal(3, result.Total);
        Assert.Equal(1, result.UpdatesAvailableCount);
        Assert.Equal(ApplicationStatus.UpdateAvailable, apps[1].Status);
        Assert.Equal("2.0", apps[1].AvailableVersion);
        Assert.Equal(ApplicationStatus.Installed, apps[0].Status);
        Assert.Equal(string.Empty, apps[0].AvailableVersion);
    }

    [Fact]
    public async Task ScanForUpdatesAsync_ShouldRespectMaxParallelScans()
    {
        var apps = CreateApps("App1", "App2", "App3", "App4", "App5");
        var bridge = CreateBridge();
        var activeCalls = 0;
        var maxActiveCalls = 0;
        bridge
            .Setup(x => x.CheckApplicationUpdateAsync(It.IsAny<ApplicationModel>()))
            .Returns(async () =>
            {
                var active = Interlocked.Increment(ref activeCalls);
                UpdateMax(ref maxActiveCalls, active);

                await Task.Delay(50);

                Interlocked.Decrement(ref activeCalls);
                return UpdateCheckResult.UpToDate("1.0");
            });
        var coordinator = CreateCoordinator(bridge.Object, maxParallelScans: 2);

        await coordinator.ScanForUpdatesAsync(apps);

        Assert.InRange(maxActiveCalls, 2, 2);
    }

    [Fact]
    public async Task UpdateAsync_ShouldCallBridgeInInputOrderWithoutOverlap()
    {
        var apps = CreateApps("App1", "App2", "App3");
        var bridge = CreateBridge();
        var callOrder = new List<string>();
        var activeCalls = 0;
        var maxActiveCalls = 0;
        bridge
            .Setup(x => x.UpdateApplicationAsync(
                It.IsAny<ApplicationModel>(),
                It.IsAny<Action<string>?>()))
            .Returns(async (ApplicationModel app, Action<string>? progressCallback) =>
            {
                var active = Interlocked.Increment(ref activeCalls);
                UpdateMax(ref maxActiveCalls, active);
                callOrder.Add(app.AppId);

                await Task.Delay(25);

                Interlocked.Decrement(ref activeCalls);
                return InstallResult.Successful("Updated", "log");
            });
        var coordinator = CreateCoordinator(bridge.Object);

        var result = await coordinator.UpdateAsync(apps);

        Assert.Equal(["App1", "App2", "App3"], callOrder);
        Assert.Equal(1, maxActiveCalls);
        Assert.Equal(3, result.UpdatedCount);
    }

    [Fact]
    public async Task UpdateAsync_WithMixedResults_ShouldAggregateSuccessAndFailure()
    {
        var apps = CreateApps("Updated", "Failed");
        var bridge = CreateBridge();
        bridge
            .Setup(x => x.UpdateApplicationAsync(
                It.IsAny<ApplicationModel>(),
                It.IsAny<Action<string>?>()))
            .ReturnsAsync((ApplicationModel app, Action<string>? progressCallback) =>
                app.AppId == "Failed"
                    ? InstallResult.Failed("Update failed", "failed-log")
                    : InstallResult.Successful("Updated", "updated-log"));
        var coordinator = CreateCoordinator(bridge.Object);

        var result = await coordinator.UpdateAsync(apps);

        Assert.Equal(2, result.Total);
        Assert.Equal(1, result.UpdatedCount);
        Assert.Equal(1, result.FailedCount);
        Assert.Equal(0, result.SkippedCount);
        Assert.Equal(ApplicationStatus.Installed, apps[0].Status);
        Assert.Equal(ApplicationStatus.Failed, apps[1].Status);
        Assert.Equal("Update failed", apps[1].ErrorMessage);
    }

    [Fact]
    public async Task UpdateAsync_WhenCancelledBetweenItems_ShouldReturnCancelledResult()
    {
        var apps = CreateApps("App1", "App2", "App3");
        using var cts = new CancellationTokenSource();
        var bridge = CreateBridge();
        bridge
            .Setup(x => x.UpdateApplicationAsync(
                It.IsAny<ApplicationModel>(),
                It.IsAny<Action<string>?>()))
            .ReturnsAsync((ApplicationModel app, Action<string>? progressCallback) =>
            {
                cts.Cancel();
                return InstallResult.Successful("Updated", "log");
            });
        var coordinator = CreateCoordinator(bridge.Object);

        var result = await coordinator.UpdateAsync(apps, cancellationToken: cts.Token);

        Assert.True(result.WasCancelled);
        Assert.Equal(1, result.UpdatedCount);
        Assert.Equal(2, result.SkippedCount);
        bridge.Verify(
            x => x.UpdateApplicationAsync(
                It.IsAny<ApplicationModel>(),
                It.IsAny<Action<string>?>()),
            Times.Once);
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
            .Setup(x => x.CheckApplicationUpdateAsync(It.IsAny<ApplicationModel>()))
            .ReturnsAsync(UpdateCheckResult.UpToDate("1.0"));
        bridge
            .Setup(x => x.UpdateApplicationAsync(
                It.IsAny<ApplicationModel>(),
                It.IsAny<Action<string>?>()))
            .ReturnsAsync(InstallResult.Successful("Updated", "log"));
        return bridge;
    }

    private static AppUpdateCoordinator CreateCoordinator(
        IPowerShellBridge? bridge = null,
        int maxParallelScans = 2)
    {
        var settings = new MockAppSettingsService
        {
            SettingsToReturn = new AppSettings { MaxParallelScans = maxParallelScans }
        };

        return new AppUpdateCoordinator(bridge ?? CreateBridge().Object, settings);
    }

    private static void UpdateMax(ref int target, int candidate)
    {
        int current;
        do
        {
            current = Volatile.Read(ref target);
            if (candidate <= current)
            {
                return;
            }
        }
        while (Interlocked.CompareExchange(ref target, candidate, current) != current);
    }
}

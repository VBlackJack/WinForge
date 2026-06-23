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
using WinForge.GUI.Models;
using WinForge.GUI.Services;
using WinForge.GUI.Services.Coordinators;
using WinForge.GUI.Services.Resume;

namespace WinForge.GUI.Tests;

public class AppUpdateCoordinatorTests
{
    [Fact]
    public async Task ScanForUpdatesAsync_WithEmptyList_ShouldReturnEmptyResult()
    {
        Mock<IPowerShellBridge> bridge = CreateBridge();
        AppUpdateCoordinator coordinator = CreateCoordinator(bridge.Object);

        AppUpdateScanResult result = await coordinator.ScanForUpdatesAsync([]);

        Assert.Equal(0, result.Total);
        Assert.Equal(0, result.UpdatesAvailableCount);
        Assert.False(result.WasCancelled);
        bridge.Verify(x => x.CheckApplicationUpdateAsync(It.IsAny<ApplicationModel>()), Times.Never);
    }

    [Fact]
    public async Task ScanForUpdatesAsync_WithAvailableUpdates_ShouldAggregateUpdateCount()
    {
        List<ApplicationModel> apps = CreateApps("App1", "App2", "App3");
        Mock<IPowerShellBridge> bridge = CreateBridge();
        bridge
            .Setup(x => x.CheckApplicationUpdateAsync(It.IsAny<ApplicationModel>()))
            .ReturnsAsync((ApplicationModel app) =>
                app.AppId == "App2"
                    ? UpdateCheckResult.UpdateAvailable("1.0", "2.0")
                    : UpdateCheckResult.UpToDate("1.0"));
        AppUpdateCoordinator coordinator = CreateCoordinator(bridge.Object);

        AppUpdateScanResult result = await coordinator.ScanForUpdatesAsync(apps);

        Assert.Equal(3, result.Total);
        Assert.Equal(1, result.UpdatesAvailableCount);
        Assert.Equal(ApplicationStatus.UpdateAvailable, apps[1].Status);
        Assert.Equal("2.0", apps[1].AvailableVersion);
        Assert.Equal(ApplicationStatus.Installed, apps[0].Status);
        Assert.Equal(string.Empty, apps[0].AvailableVersion);
    }

    [Fact]
    public async Task ScanForUpdatesAsync_WithForceRefresh_ShouldInvalidateUpdateCacheBeforeScan()
    {
        List<ApplicationModel> apps = CreateApps("App1", "App2");
        Mock<IPowerShellBridge> bridge = CreateBridge();
        bridge
            .Setup(x => x.CheckApplicationUpdateAsync(It.IsAny<ApplicationModel>()))
            .ReturnsAsync(UpdateCheckResult.UpToDate("1.0"));
        AppUpdateCoordinator coordinator = CreateCoordinator(bridge.Object);

        await coordinator.ScanForUpdatesAsync(apps, forceRefresh: true);

        bridge.Verify(x => x.InvalidateUpdateCacheAsync(), Times.Once);
        bridge.Verify(x => x.CheckApplicationUpdateAsync(It.IsAny<ApplicationModel>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ScanForUpdatesAsync_WithoutForceRefresh_ShouldKeepPassiveCache()
    {
        List<ApplicationModel> apps = CreateApps("App1");
        Mock<IPowerShellBridge> bridge = CreateBridge();
        AppUpdateCoordinator coordinator = CreateCoordinator(bridge.Object);

        await coordinator.ScanForUpdatesAsync(apps);

        bridge.Verify(x => x.InvalidateUpdateCacheAsync(), Times.Never);
    }

    [Fact]
    public async Task ScanForUpdatesAsync_ShouldRespectMaxParallelScans()
    {
        List<ApplicationModel> apps = CreateApps("App1", "App2", "App3", "App4", "App5");
        Mock<IPowerShellBridge> bridge = CreateBridge();
        int activeCalls = 0;
        int maxActiveCalls = 0;
        bridge
            .Setup(x => x.CheckApplicationUpdateAsync(It.IsAny<ApplicationModel>()))
            .Returns(async () =>
            {
                int active = Interlocked.Increment(ref activeCalls);
                UpdateMax(ref maxActiveCalls, active);

                await Task.Delay(50);

                Interlocked.Decrement(ref activeCalls);
                return UpdateCheckResult.UpToDate("1.0");
            });
        AppUpdateCoordinator coordinator = CreateCoordinator(bridge.Object, maxParallelScans: 2);

        await coordinator.ScanForUpdatesAsync(apps);

        Assert.InRange(maxActiveCalls, 2, 2);
    }

    [Fact]
    public async Task UpdateAsync_ShouldCallBridgeInInputOrderWithoutOverlap()
    {
        List<ApplicationModel> apps = CreateApps("App1", "App2", "App3");
        Mock<IPowerShellBridge> bridge = CreateBridge();
        List<string> callOrder = new List<string>();
        int activeCalls = 0;
        int maxActiveCalls = 0;
        bridge
            .Setup(x => x.UpdateApplicationAsync(
                It.IsAny<ApplicationModel>(),
                It.IsAny<Action<string>?>()))
            .Returns(async (ApplicationModel app, Action<string>? progressCallback) =>
            {
                int active = Interlocked.Increment(ref activeCalls);
                UpdateMax(ref maxActiveCalls, active);
                callOrder.Add(app.AppId);

                await Task.Delay(25);

                Interlocked.Decrement(ref activeCalls);
                return InstallResult.Successful("Updated", "log");
            });
        AppUpdateCoordinator coordinator = CreateCoordinator(bridge.Object);

        AppUpdateResult result = await coordinator.UpdateAsync(apps);

        Assert.Equal(["App1", "App2", "App3"], callOrder);
        Assert.Equal(1, maxActiveCalls);
        Assert.Equal(3, result.UpdatedCount);
    }

    [Fact]
    public async Task UpdateAsync_WithChocolateyUpdateCandidate_ShouldDelegateToBridgeUpdate()
    {
        ApplicationModel app = new ApplicationModel
        {
            AppId = "Chocolatey",
            Name = "Chocolatey",
            Status = ApplicationStatus.UpdateAvailable
        };
        Mock<IPowerShellBridge> bridge = CreateBridge();
        AppUpdateCoordinator coordinator = CreateCoordinator(bridge.Object);

        AppUpdateResult result = await coordinator.UpdateAsync([app]);

        Assert.Equal(1, result.UpdatedCount);
        bridge.Verify(
            x => x.UpdateApplicationAsync(
                It.Is<ApplicationModel>(candidate => candidate.AppId == "Chocolatey"),
                It.IsAny<Action<string>?>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WithMixedResults_ShouldAggregateSuccessAndFailure()
    {
        List<ApplicationModel> apps = CreateApps("Updated", "Failed");
        Mock<IPowerShellBridge> bridge = CreateBridge();
        bridge
            .Setup(x => x.UpdateApplicationAsync(
                It.IsAny<ApplicationModel>(),
                It.IsAny<Action<string>?>()))
            .ReturnsAsync((ApplicationModel app, Action<string>? progressCallback) =>
                app.AppId == "Failed"
                    ? InstallResult.Failed("Update failed", "failed-log")
                    : InstallResult.Successful("Updated", "updated-log"));
        AppUpdateCoordinator coordinator = CreateCoordinator(bridge.Object);

        AppUpdateResult result = await coordinator.UpdateAsync(apps);

        Assert.Equal(2, result.Total);
        Assert.Equal(1, result.UpdatedCount);
        Assert.Equal(1, result.FailedCount);
        Assert.Equal(0, result.SkippedCount);
        Assert.Equal(ApplicationStatus.Installed, apps[0].Status);
        Assert.Equal(ApplicationStatus.Failed, apps[1].Status);
        Assert.Equal("Update failed", apps[1].ErrorMessage);
    }

    [Fact]
    public async Task UpdateAsync_OnSuccess_ShouldForceRefreshUpdatedApplicationState()
    {
        ApplicationModel app = new ApplicationModel
        {
            AppId = "App1",
            Name = "App1",
            Status = ApplicationStatus.UpdateAvailable,
            CurrentVersion = "1.0",
            AvailableVersion = "2.0"
        };
        Mock<IPowerShellBridge> bridge = CreateBridge();
        bridge
            .Setup(x => x.UpdateApplicationAsync(
                app,
                It.IsAny<Action<string>?>()))
            .ReturnsAsync(InstallResult.Successful("Updated", "updated-log"));
        bridge
            .Setup(x => x.CheckApplicationUpdateAsync(app, true))
            .ReturnsAsync(UpdateCheckResult.UpToDate("2.0"));
        AppUpdateCoordinator coordinator = CreateCoordinator(bridge.Object);

        AppUpdateResult result = await coordinator.UpdateAsync([app]);

        Assert.Equal(1, result.UpdatedCount);
        Assert.Equal(ApplicationStatus.Installed, app.Status);
        Assert.Equal("2.0", app.CurrentVersion);
        Assert.Equal(string.Empty, app.AvailableVersion);
        bridge.Verify(x => x.CheckApplicationUpdateAsync(app, true), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WhenForcedRefreshStillFindsUpdate_ShouldKeepUpdateAvailableStatus()
    {
        ApplicationModel app = new ApplicationModel
        {
            AppId = "App1",
            Name = "App1",
            Status = ApplicationStatus.UpdateAvailable,
            CurrentVersion = "1.0",
            AvailableVersion = "2.0"
        };
        Mock<IPowerShellBridge> bridge = CreateBridge();
        bridge
            .Setup(x => x.UpdateApplicationAsync(
                app,
                It.IsAny<Action<string>?>()))
            .ReturnsAsync(InstallResult.Successful("Updated", "updated-log"));
        bridge
            .Setup(x => x.CheckApplicationUpdateAsync(app, true))
            .ReturnsAsync(UpdateCheckResult.UpdateAvailable("1.0", "2.0"));
        AppUpdateCoordinator coordinator = CreateCoordinator(bridge.Object);

        await coordinator.UpdateAsync([app]);

        Assert.Equal(ApplicationStatus.UpdateAvailable, app.Status);
        Assert.Equal("2.0", app.AvailableVersion);
    }

    [Fact]
    public async Task UpdateAsync_WhenCancelledBetweenItems_ShouldReturnCancelledResult()
    {
        List<ApplicationModel> apps = CreateApps("App1", "App2", "App3");
        using CancellationTokenSource cts = new CancellationTokenSource();
        Mock<IPowerShellBridge> bridge = CreateBridge();
        bridge
            .Setup(x => x.UpdateApplicationAsync(
                It.IsAny<ApplicationModel>(),
                It.IsAny<Action<string>?>()))
            .ReturnsAsync((ApplicationModel app, Action<string>? progressCallback) =>
            {
                cts.Cancel();
                return InstallResult.Successful("Updated", "log");
            });
        AppUpdateCoordinator coordinator = CreateCoordinator(bridge.Object);

        AppUpdateResult result = await coordinator.UpdateAsync(apps, cancellationToken: cts.Token);

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
        Mock<IPowerShellBridge> bridge = new Mock<IPowerShellBridge>();
        bridge
            .Setup(x => x.CheckApplicationUpdateAsync(It.IsAny<ApplicationModel>()))
            .ReturnsAsync(UpdateCheckResult.UpToDate("1.0"));
        bridge
            .Setup(x => x.CheckApplicationUpdateAsync(It.IsAny<ApplicationModel>(), It.IsAny<bool>()))
            .ReturnsAsync(UpdateCheckResult.UpToDate("1.0"));
        bridge
            .Setup(x => x.UpdateApplicationAsync(
                It.IsAny<ApplicationModel>(),
                It.IsAny<Action<string>?>()))
            .ReturnsAsync(InstallResult.Successful("Updated", "log"));
        bridge
            .Setup(x => x.InvalidateUpdateCacheAsync())
            .Returns(Task.CompletedTask);
        return bridge;
    }

    private static AppUpdateCoordinator CreateCoordinator(
        IPowerShellBridge? bridge = null,
        int maxParallelScans = 2,
        IBatchResumeService? resumeService = null)
    {
        MockAppSettingsService settings = new MockAppSettingsService
        {
            SettingsToReturn = new AppSettings { MaxParallelScans = maxParallelScans }
        };

        return new AppUpdateCoordinator(
            bridge ?? CreateBridge().Object,
            settings,
            resumeService ?? CreateResumeServiceMock().Object);
    }

    private static Mock<IBatchResumeService> CreateResumeServiceMock()
    {
        Mock<IBatchResumeService> mock = new Mock<IBatchResumeService>();
        mock.Setup(x => x.BeginBatchAsync(
                It.IsAny<BatchOperationKind>(),
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<BatchOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());
        mock.Setup(x => x.AppendCompletedAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<BatchItemOutcome>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mock.Setup(x => x.MarkBatchCompletedAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return mock;
    }

    [Fact]
    public async Task UpdateAsync_ShouldBeginBatchWithUpdateKindAndAppIds()
    {
        List<ApplicationModel> apps = CreateApps("Foo", "Bar");
        Mock<IBatchResumeService> resume = CreateResumeServiceMock();
        AppUpdateCoordinator coordinator = CreateCoordinator(resumeService: resume.Object);

        await coordinator.UpdateAsync(apps);

        resume.Verify(
            x => x.BeginBatchAsync(
                BatchOperationKind.Update,
                It.Is<IReadOnlyList<string>>(plan => plan.SequenceEqual(new[] { "Foo", "Bar" })),
                It.IsAny<BatchOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_ShouldAppendUpdatedOutcomeForSuccess()
    {
        List<ApplicationModel> apps = CreateApps("App1");
        Mock<IBatchResumeService> resume = CreateResumeServiceMock();
        AppUpdateCoordinator coordinator = CreateCoordinator(resumeService: resume.Object);

        await coordinator.UpdateAsync(apps);

        resume.Verify(
            x => x.AppendCompletedAsync(
                It.IsAny<Guid>(),
                "App1",
                BatchItemOutcome.Updated,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_OnSuccess_ShouldMarkBatchCompleted()
    {
        List<ApplicationModel> apps = CreateApps("App1");
        Mock<IBatchResumeService> resume = CreateResumeServiceMock();
        AppUpdateCoordinator coordinator = CreateCoordinator(resumeService: resume.Object);

        await coordinator.UpdateAsync(apps);

        resume.Verify(
            x => x.MarkBatchCompletedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Once);
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

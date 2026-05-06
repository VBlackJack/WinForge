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

public class AppInstallationCoordinatorTests
{
    [Fact]
    public async Task InstallAsync_WithEmptyList_ShouldReturnEmptyResult()
    {
        var bridge = CreateBridge();
        var coordinator = CreateCoordinator(bridge.Object);

        var result = await coordinator.InstallAsync([]);

        Assert.Equal(0, result.Total);
        Assert.Equal(0, result.InstalledCount);
        Assert.Equal(0, result.AlreadyInstalledCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Equal(0, result.SkippedCount);
        Assert.False(result.WasCancelled);
        bridge.Verify(
            x => x.InstallApplicationAsync(
                It.IsAny<ApplicationModel>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<Action<string>?>()),
            Times.Never);
    }

    [Fact]
    public async Task InstallAsync_WithSuccessfulApps_ShouldAggregateInstalledCount()
    {
        var apps = CreateApps("App1", "App2");
        var bridge = CreateBridge();
        var coordinator = CreateCoordinator(bridge.Object);

        var result = await coordinator.InstallAsync(apps);

        Assert.Equal(2, result.Total);
        Assert.Equal(2, result.InstalledCount);
        Assert.Equal(0, result.AlreadyInstalledCount);
        Assert.Equal(0, result.FailedCount);
        Assert.All(apps, app => Assert.Equal(ApplicationStatus.Installed, app.Status));
        bridge.Verify(
            x => x.InstallApplicationAsync(
                It.IsAny<ApplicationModel>(),
                false,
                true,
                It.IsAny<Action<string>?>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task InstallAsync_WithMixedResults_ShouldSplitFinalCounters()
    {
        var apps = CreateApps("Installed", "Already", "Failed");
        var bridge = CreateBridge();
        bridge
            .Setup(x => x.InstallApplicationAsync(
                It.IsAny<ApplicationModel>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<Action<string>?>()))
            .ReturnsAsync((ApplicationModel app, bool isDryRun, bool forceUpdate, Action<string>? progressCallback) =>
                app.AppId switch
                {
                    "Already" => InstallResult.Successful("Already installed", "already-log", alreadyInstalled: true),
                    "Failed" => InstallResult.Failed("Install failed", "failed-log"),
                    _ => InstallResult.Successful("Installed", "installed-log")
                });
        var coordinator = CreateCoordinator(bridge.Object);

        var result = await coordinator.InstallAsync(apps);

        Assert.Equal(1, result.InstalledCount);
        Assert.Equal(1, result.AlreadyInstalledCount);
        Assert.Equal(1, result.FailedCount);
        Assert.Equal(0, result.SkippedCount);
        Assert.Equal(ApplicationStatus.Installed, apps[0].Status);
        Assert.Equal(ApplicationStatus.AlreadyInstalled, apps[1].Status);
        Assert.Equal(ApplicationStatus.Failed, apps[2].Status);
        Assert.Equal("Install failed", apps[2].ErrorMessage);
    }

    [Fact]
    public async Task InstallAsync_WhenPaused_ShouldWaitUntilResumed()
    {
        var apps = CreateApps("App1");
        var bridge = CreateBridge();
        var installCalls = 0;
        bridge
            .Setup(x => x.InstallApplicationAsync(
                It.IsAny<ApplicationModel>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<Action<string>?>()))
            .Callback(() => Interlocked.Increment(ref installCalls))
            .ReturnsAsync(InstallResult.Successful("Installed", "log"));
        using var pauseGate = new PauseGate();
        pauseGate.Pause();
        var coordinator = CreateCoordinator(bridge.Object, pauseGate: pauseGate);

        var installTask = coordinator.InstallAsync(apps);
        await Task.Delay(75);

        Assert.Equal(0, Volatile.Read(ref installCalls));

        pauseGate.Resume();
        var result = await installTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(1, result.InstalledCount);
        Assert.Equal(1, Volatile.Read(ref installCalls));
    }

    [Fact]
    public async Task InstallAsync_WhenCancelledDuringPause_ShouldReturnCancelledResult()
    {
        var apps = CreateApps("App1");
        var bridge = CreateBridge();
        using var pauseGate = new PauseGate();
        pauseGate.Pause();
        var coordinator = CreateCoordinator(bridge.Object, pauseGate: pauseGate);
        using var cts = new CancellationTokenSource();

        var installTask = coordinator.InstallAsync(apps, cancellationToken: cts.Token);
        await Task.Delay(50);
        cts.Cancel();

        var result = await installTask;

        Assert.True(result.WasCancelled);
        Assert.Equal(1, result.SkippedCount);
        Assert.Equal(ApplicationStatus.Skipped, apps[0].Status);
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
            .Setup(x => x.InstallApplicationAsync(
                It.IsAny<ApplicationModel>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<Action<string>?>()))
            .ReturnsAsync(InstallResult.Successful("Installed", "log"));
        return bridge;
    }

    private static AppInstallationCoordinator CreateCoordinator(
        IPowerShellBridge? bridge = null,
        IAppSettingsService? settingsService = null,
        IPauseGate? pauseGate = null)
    {
        var settings = new MockAppSettingsService
        {
            SettingsToReturn = new AppSettings { MaxParallelInstalls = 2 }
        };
        var pauseGateMock = new Mock<IPauseGate>();
        pauseGateMock
            .Setup(x => x.WaitAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return new AppInstallationCoordinator(
            bridge ?? CreateBridge().Object,
            settingsService ?? settings,
            pauseGate ?? pauseGateMock.Object);
    }
}

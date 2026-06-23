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

public class AppInstallationCoordinatorTests
{
    [Fact]
    public async Task InstallAsync_WithEmptyList_ShouldReturnEmptyResult()
    {
        Mock<IPowerShellBridge> bridge = CreateBridge();
        AppInstallationCoordinator coordinator = CreateCoordinator(bridge.Object);

        AppInstallationResult result = await coordinator.InstallAsync([], new AppInstallationOptions(ForceUpdate: false));

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
        List<ApplicationModel> apps = CreateApps("App1", "App2");
        Mock<IPowerShellBridge> bridge = CreateBridge();
        AppInstallationCoordinator coordinator = CreateCoordinator(bridge.Object);

        AppInstallationResult result = await coordinator.InstallAsync(apps, new AppInstallationOptions(ForceUpdate: true));

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
    public async Task InstallAsync_WithInstalledApps_ShouldInvalidateUpdateCache()
    {
        List<ApplicationModel> apps = CreateApps("App1");
        Mock<IPowerShellBridge> bridge = CreateBridge();
        AppInstallationCoordinator coordinator = CreateCoordinator(bridge.Object);

        await coordinator.InstallAsync(apps, new AppInstallationOptions(ForceUpdate: false));

        bridge.Verify(x => x.InvalidateUpdateCacheAsync(), Times.Once);
    }

    [Fact]
    public async Task InstallAsync_WithOnlyAlreadyInstalledApps_ShouldNotInvalidateUpdateCache()
    {
        List<ApplicationModel> apps = CreateApps("Already");
        Mock<IPowerShellBridge> bridge = CreateBridge();
        bridge
            .Setup(x => x.InstallApplicationAsync(
                It.IsAny<ApplicationModel>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<Action<string>?>()))
            .ReturnsAsync(InstallResult.Successful("Already installed", "already-log", alreadyInstalled: true));
        AppInstallationCoordinator coordinator = CreateCoordinator(bridge.Object);

        await coordinator.InstallAsync(apps, new AppInstallationOptions(ForceUpdate: false));

        bridge.Verify(x => x.InvalidateUpdateCacheAsync(), Times.Never);
    }

    [Fact]
    public async Task InstallAsync_WithMixedResults_ShouldSplitFinalCounters()
    {
        List<ApplicationModel> apps = CreateApps("Installed", "Already", "Failed");
        Mock<IPowerShellBridge> bridge = CreateBridge();
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
        AppInstallationCoordinator coordinator = CreateCoordinator(bridge.Object);

        AppInstallationResult result = await coordinator.InstallAsync(apps, new AppInstallationOptions(ForceUpdate: true));

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
        List<ApplicationModel> apps = CreateApps("App1");
        Mock<IPowerShellBridge> bridge = CreateBridge();
        int installCalls = 0;
        bridge
            .Setup(x => x.InstallApplicationAsync(
                It.IsAny<ApplicationModel>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<Action<string>?>()))
            .Callback(() => Interlocked.Increment(ref installCalls))
            .ReturnsAsync(InstallResult.Successful("Installed", "log"));
        using PauseGate pauseGate = new PauseGate();
        pauseGate.Pause();
        AppInstallationCoordinator coordinator = CreateCoordinator(bridge.Object, pauseGate: pauseGate);

        Task<AppInstallationResult> installTask = coordinator.InstallAsync(apps, new AppInstallationOptions(ForceUpdate: true));
        await Task.Delay(75);

        Assert.Equal(0, Volatile.Read(ref installCalls));

        pauseGate.Resume();
        AppInstallationResult result = await installTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(1, result.InstalledCount);
        Assert.Equal(1, Volatile.Read(ref installCalls));
    }

    [Fact]
    public async Task InstallAsync_WhenCancelledDuringPause_ShouldReturnCancelledResult()
    {
        List<ApplicationModel> apps = CreateApps("App1");
        Mock<IPowerShellBridge> bridge = CreateBridge();
        using PauseGate pauseGate = new PauseGate();
        pauseGate.Pause();
        AppInstallationCoordinator coordinator = CreateCoordinator(bridge.Object, pauseGate: pauseGate);
        using CancellationTokenSource cts = new CancellationTokenSource();

        Task<AppInstallationResult> installTask = coordinator.InstallAsync(
            apps,
            new AppInstallationOptions(ForceUpdate: true),
            cancellationToken: cts.Token);
        await Task.Delay(50);
        cts.Cancel();

        AppInstallationResult result = await installTask;

        Assert.True(result.WasCancelled);
        Assert.Equal(1, result.SkippedCount);
        Assert.Equal(ApplicationStatus.Skipped, apps[0].Status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InstallAsync_ShouldPropagateForceUpdateOption(bool forceUpdate)
    {
        List<ApplicationModel> apps = CreateApps("App1");
        Mock<IPowerShellBridge> bridge = CreateBridge();
        AppInstallationCoordinator coordinator = CreateCoordinator(bridge.Object);

        await coordinator.InstallAsync(apps, new AppInstallationOptions(ForceUpdate: forceUpdate));

        bridge.Verify(
            x => x.InstallApplicationAsync(
                It.IsAny<ApplicationModel>(),
                false,
                forceUpdate,
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
            .Setup(x => x.InstallApplicationAsync(
                It.IsAny<ApplicationModel>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<Action<string>?>()))
            .ReturnsAsync(InstallResult.Successful("Installed", "log"));
        bridge
            .Setup(x => x.InvalidateUpdateCacheAsync())
            .Returns(Task.CompletedTask);
        return bridge;
    }

    private static AppInstallationCoordinator CreateCoordinator(
        IPowerShellBridge? bridge = null,
        IAppSettingsService? settingsService = null,
        IPauseGate? pauseGate = null,
        IBatchResumeService? resumeService = null)
    {
        MockAppSettingsService settings = new MockAppSettingsService
        {
            SettingsToReturn = new AppSettings { MaxParallelInstalls = 2 }
        };
        Mock<IPauseGate> pauseGateMock = new Mock<IPauseGate>();
        pauseGateMock
            .Setup(x => x.WaitAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return new AppInstallationCoordinator(
            bridge ?? CreateBridge().Object,
            settingsService ?? settings,
            pauseGate ?? pauseGateMock.Object,
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
    public async Task InstallAsync_ShouldBeginBatchWithInstallKindAndAppIds()
    {
        List<ApplicationModel> apps = CreateApps("App1", "App2");
        Mock<IBatchResumeService> resume = CreateResumeServiceMock();
        AppInstallationCoordinator coordinator = CreateCoordinator(resumeService: resume.Object);

        await coordinator.InstallAsync(apps, new AppInstallationOptions(ForceUpdate: true));

        resume.Verify(
            x => x.BeginBatchAsync(
                BatchOperationKind.Install,
                It.Is<IReadOnlyList<string>>(plan => plan.SequenceEqual(new[] { "App1", "App2" })),
                It.Is<BatchOptions>(opt => opt.ForceUpdate == true),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task InstallAsync_ShouldAppendCompletedPerAppWithMappedOutcome()
    {
        List<ApplicationModel> apps = CreateApps("Installed", "Already", "Failed");
        Mock<IPowerShellBridge> bridge = CreateBridge();
        bridge
            .Setup(x => x.InstallApplicationAsync(
                It.IsAny<ApplicationModel>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<Action<string>?>()))
            .ReturnsAsync((ApplicationModel app, bool _, bool _, Action<string>? _) =>
                app.AppId switch
                {
                    "Already" => InstallResult.Successful("ok", "log", alreadyInstalled: true),
                    "Failed" => InstallResult.Failed("nope", "log"),
                    _ => InstallResult.Successful("ok", "log")
                });
        Mock<IBatchResumeService> resume = CreateResumeServiceMock();
        AppInstallationCoordinator coordinator = CreateCoordinator(bridge.Object, resumeService: resume.Object);

        await coordinator.InstallAsync(apps, new AppInstallationOptions(ForceUpdate: false));

        resume.Verify(
            x => x.AppendCompletedAsync(It.IsAny<Guid>(), "Installed", BatchItemOutcome.Installed, It.IsAny<CancellationToken>()),
            Times.Once);
        resume.Verify(
            x => x.AppendCompletedAsync(It.IsAny<Guid>(), "Already", BatchItemOutcome.AlreadyInstalled, It.IsAny<CancellationToken>()),
            Times.Once);
        resume.Verify(
            x => x.AppendCompletedAsync(It.IsAny<Guid>(), "Failed", BatchItemOutcome.Failed, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task InstallAsync_OnSuccess_ShouldMarkBatchCompleted()
    {
        List<ApplicationModel> apps = CreateApps("App1");
        Mock<IBatchResumeService> resume = CreateResumeServiceMock();
        AppInstallationCoordinator coordinator = CreateCoordinator(resumeService: resume.Object);

        await coordinator.InstallAsync(apps, new AppInstallationOptions(ForceUpdate: false));

        resume.Verify(
            x => x.MarkBatchCompletedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task InstallAsync_OnCancellation_ShouldStillMarkBatchCompleted()
    {
        List<ApplicationModel> apps = CreateApps("App1");
        Mock<IPowerShellBridge> bridge = CreateBridge();
        using PauseGate pauseGate = new PauseGate();
        pauseGate.Pause();
        Mock<IBatchResumeService> resume = CreateResumeServiceMock();
        AppInstallationCoordinator coordinator = CreateCoordinator(bridge.Object, pauseGate: pauseGate, resumeService: resume.Object);
        using CancellationTokenSource cts = new CancellationTokenSource();

        Task<AppInstallationResult> installTask = coordinator.InstallAsync(
            apps,
            new AppInstallationOptions(ForceUpdate: false),
            cancellationToken: cts.Token);
        await Task.Delay(50);
        cts.Cancel();
        await installTask;

        resume.Verify(
            x => x.MarkBatchCompletedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }
}

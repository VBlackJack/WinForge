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
using Win11Forge.GUI.Services.Resume;

namespace Win11Forge.GUI.Tests;

public class AppUninstallCoordinatorTests
{
    [Fact]
    public async Task UninstallAsync_WithEmptyList_ShouldReturnEmptyResult()
    {
        Mock<IPowerShellBridge> bridge = CreateBridge();
        AppUninstallCoordinator coordinator = CreateCoordinator(bridge.Object);

        AppUninstallResult result = await coordinator.UninstallAsync([]);

        Assert.Equal(0, result.Total);
        Assert.Equal(0, result.UninstalledCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Equal(0, result.SkippedCount);
        Assert.False(result.WasCancelled);
        bridge.Verify(
            x => x.UninstallApplicationAsync(
                It.IsAny<ApplicationModel>(),
                It.IsAny<Action<string>?>()),
            Times.Never);
    }

    [Fact]
    public async Task UninstallAsync_WithSuccessfulApps_ShouldAggregateUninstalledCount()
    {
        List<ApplicationModel> apps = CreateApps("App1", "App2");
        Mock<IPowerShellBridge> bridge = CreateBridge();
        AppUninstallCoordinator coordinator = CreateCoordinator(bridge.Object);

        AppUninstallResult result = await coordinator.UninstallAsync(apps);

        Assert.Equal(2, result.Total);
        Assert.Equal(2, result.UninstalledCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Equal(0, result.SkippedCount);
        Assert.All(apps, app => Assert.Equal(ApplicationStatus.Uninstalled, app.Status));
        bridge.Verify(
            x => x.UninstallApplicationAsync(
                It.IsAny<ApplicationModel>(),
                It.IsAny<Action<string>?>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task UninstallAsync_WithMixedResults_ShouldSplitFinalCounters()
    {
        List<ApplicationModel> apps = CreateApps("Uninstalled", "Failed");
        Mock<IPowerShellBridge> bridge = CreateBridge();
        bridge
            .Setup(x => x.UninstallApplicationAsync(
                It.IsAny<ApplicationModel>(),
                It.IsAny<Action<string>?>()))
            .ReturnsAsync((ApplicationModel app, Action<string>? progressCallback) =>
                app.AppId == "Failed"
                    ? InstallResult.Failed("Uninstall failed", "failed-log")
                    : InstallResult.Successful("Uninstalled", "uninstalled-log"));
        AppUninstallCoordinator coordinator = CreateCoordinator(bridge.Object);

        AppUninstallResult result = await coordinator.UninstallAsync(apps);

        Assert.Equal(1, result.UninstalledCount);
        Assert.Equal(1, result.FailedCount);
        Assert.Equal(0, result.SkippedCount);
        Assert.Equal(ApplicationStatus.Uninstalled, apps[0].Status);
        Assert.Equal(ApplicationStatus.Failed, apps[1].Status);
        Assert.Equal("Uninstall failed", apps[1].ErrorMessage);
    }

    [Fact]
    public async Task UninstallAsync_WhenPaused_ShouldWaitUntilResumed()
    {
        List<ApplicationModel> apps = CreateApps("App1");
        Mock<IPowerShellBridge> bridge = CreateBridge();
        int uninstallCalls = 0;
        bridge
            .Setup(x => x.UninstallApplicationAsync(
                It.IsAny<ApplicationModel>(),
                It.IsAny<Action<string>?>()))
            .Callback(() => Interlocked.Increment(ref uninstallCalls))
            .ReturnsAsync(InstallResult.Successful("Uninstalled", "log"));
        using PauseGate pauseGate = new PauseGate();
        pauseGate.Pause();
        AppUninstallCoordinator coordinator = CreateCoordinator(bridge.Object, pauseGate: pauseGate);

        Task<AppUninstallResult> uninstallTask = coordinator.UninstallAsync(apps);
        await Task.Delay(75);

        Assert.Equal(0, Volatile.Read(ref uninstallCalls));

        pauseGate.Resume();
        AppUninstallResult result = await uninstallTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(1, result.UninstalledCount);
        Assert.Equal(1, Volatile.Read(ref uninstallCalls));
    }

    [Fact]
    public async Task UninstallAsync_WhenCancelledDuringPause_ShouldReturnCancelledResult()
    {
        List<ApplicationModel> apps = CreateApps("App1");
        Mock<IPowerShellBridge> bridge = CreateBridge();
        using PauseGate pauseGate = new PauseGate();
        pauseGate.Pause();
        AppUninstallCoordinator coordinator = CreateCoordinator(bridge.Object, pauseGate: pauseGate);
        using CancellationTokenSource cts = new CancellationTokenSource();

        Task<AppUninstallResult> uninstallTask = coordinator.UninstallAsync(apps, cancellationToken: cts.Token);
        await Task.Delay(50);
        cts.Cancel();

        AppUninstallResult result = await uninstallTask;

        Assert.True(result.WasCancelled);
        Assert.Equal(1, result.SkippedCount);
        Assert.Equal(ApplicationStatus.Skipped, apps[0].Status);
    }

    [Fact]
    public async Task UninstallAsync_ShouldReportProgressForEachApplication()
    {
        List<ApplicationModel> apps = CreateApps("App1", "App2");
        AppUninstallCoordinator coordinator = CreateCoordinator();
        TestProgress<AppOperationProgress> progress = new TestProgress<AppOperationProgress>();

        await coordinator.UninstallAsync(apps, progress);

        Assert.Equal(2, progress.Reports.Count);
        Assert.Equal(1, progress.Reports[0].Completed);
        Assert.Equal(2, progress.Reports[0].Total);
        Assert.Equal(2, progress.Reports[1].Completed);
        Assert.Equal(2, progress.Reports[1].Total);
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
            .Setup(x => x.UninstallApplicationAsync(
                It.IsAny<ApplicationModel>(),
                It.IsAny<Action<string>?>()))
            .ReturnsAsync(InstallResult.Successful("Uninstalled", "log"));
        return bridge;
    }

    private static AppUninstallCoordinator CreateCoordinator(
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

        return new AppUninstallCoordinator(
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
    public async Task UninstallAsync_ShouldBeginBatchWithUninstallKindAndAppIds()
    {
        List<ApplicationModel> apps = CreateApps("Foo", "Bar");
        Mock<IBatchResumeService> resume = CreateResumeServiceMock();
        AppUninstallCoordinator coordinator = CreateCoordinator(resumeService: resume.Object);

        await coordinator.UninstallAsync(apps);

        resume.Verify(
            x => x.BeginBatchAsync(
                BatchOperationKind.Uninstall,
                It.Is<IReadOnlyList<string>>(plan => plan.SequenceEqual(new[] { "Foo", "Bar" })),
                It.IsAny<BatchOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UninstallAsync_ShouldAppendUninstalledOutcomeForSuccess()
    {
        List<ApplicationModel> apps = CreateApps("App1");
        Mock<IBatchResumeService> resume = CreateResumeServiceMock();
        AppUninstallCoordinator coordinator = CreateCoordinator(resumeService: resume.Object);

        await coordinator.UninstallAsync(apps);

        resume.Verify(
            x => x.AppendCompletedAsync(
                It.IsAny<Guid>(),
                "App1",
                BatchItemOutcome.Uninstalled,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UninstallAsync_OnSuccess_ShouldMarkBatchCompleted()
    {
        List<ApplicationModel> apps = CreateApps("App1");
        Mock<IBatchResumeService> resume = CreateResumeServiceMock();
        AppUninstallCoordinator coordinator = CreateCoordinator(resumeService: resume.Object);

        await coordinator.UninstallAsync(apps);

        resume.Verify(
            x => x.MarkBatchCompletedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private sealed class TestProgress<T> : IProgress<T>
    {
        public List<T> Reports { get; } = [];

        public void Report(T value)
        {
            Reports.Add(value);
        }
    }
}

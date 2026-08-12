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

using System.Text;
using Moq;
using WinForge.GUI.Services;
using WinForge.GUI.Services.Implementations;
using WinForge.GUI.Services.PowerShell;

namespace WinForge.GUI.Tests;

/// <summary>
/// Command detection runs on three paths: the GUI probe, the PowerShell modules, and the
/// post-update verification in the application-management service. All three must apply
/// the same argument guard, because the executable allowlist permits interpreters that
/// accept code as an argument.
/// </summary>
public class CommandDetectionArgumentGuardTests
{
    [Theory]
    [InlineData("python -c \"import os;os.system('calc')\"")]
    [InlineData("node -e require('child_process').exec('calc')")]
    [InlineData("python --version; whoami")]
    [InlineData("python --version && whoami")]
    [InlineData("python --version | whoami")]
    // No shell metacharacter at all: only the argument allowlist stops these.
    [InlineData("pwsh -Command Start-Process calc")]
    [InlineData("python -m http.server")]
    public async Task ExecuteCommandDetection_RejectsDangerousArguments(string commandLine)
    {
        ProbeableApplicationManagementService service = CreateService();
        StringBuilder logBuilder = new StringBuilder();

        (bool Success, int ExitCode, string Output) result =
            await service.RunCommandDetectionAsync(commandLine, logBuilder);

        Assert.False(result.Success);
        Assert.Equal(-1, result.ExitCode);
        Assert.Equal(string.Empty, result.Output);
        Assert.Contains("argument allowlist", logBuilder.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("python --version")]
    [InlineData("java -version")]
    [InlineData("dotnet --list-runtimes")]
    public async Task ExecuteCommandDetection_AllowsTheVersionProbesTheCatalogUses(string commandLine)
    {
        ProbeableApplicationManagementService service = CreateService();
        StringBuilder logBuilder = new StringBuilder();

        (bool Success, int ExitCode, string Output) result =
            await service.RunCommandDetectionAsync(commandLine, logBuilder);

        // The executable may be absent on the runner, so the probe can legitimately fail -
        // what must not happen is a rejection by the guard.
        Assert.DoesNotContain("argument allowlist", logBuilder.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteCommandDetection_StillRejectsNonAllowlistedExecutables()
    {
        ProbeableApplicationManagementService service = CreateService();
        StringBuilder logBuilder = new StringBuilder();

        (bool Success, int ExitCode, string Output) result =
            await service.RunCommandDetectionAsync("notallowed.exe --version", logBuilder);

        Assert.False(result.Success);
        Assert.Contains("not in the detection allowlist", logBuilder.ToString(), StringComparison.Ordinal);
    }

    private static ProbeableApplicationManagementService CreateService()
    {
        Mock<IRepositoryPathService> pathService = new Mock<IRepositoryPathService>();
        pathService
            .Setup(service => service.GetPath(It.IsAny<string[]>()))
            .Returns((string[] parts) => System.IO.Path.Combine(
                [new RepositoryPathService().RepositoryRoot, .. parts]));
        pathService
            .SetupGet(service => service.RepositoryRoot)
            .Returns(new RepositoryPathService().RepositoryRoot);

        Mock<IPowerShellExecutionService> executionService = new Mock<IPowerShellExecutionService>();
        executionService.SetupGet(service => service.DefaultQueryTimeoutMs).Returns(5000);

        return new ProbeableApplicationManagementService(
            pathService.Object,
            executionService.Object,
            new Mock<IApplicationCacheService>().Object,
            new Mock<IApplicationDetectionService>().Object,
            new Mock<IApplicationLauncher>().Object);
    }

    /// <summary>
    /// Exposes the real (non-overridden) detection implementation so the guard itself is
    /// exercised rather than a test double.
    /// </summary>
    private sealed class ProbeableApplicationManagementService : ApplicationManagementServiceImpl
    {
        public ProbeableApplicationManagementService(
            IRepositoryPathService pathService,
            IPowerShellExecutionService executionService,
            IApplicationCacheService cacheService,
            IApplicationDetectionService detectionService,
            IApplicationLauncher launcher)
            : base(pathService, executionService, cacheService, detectionService, launcher)
        {
        }

        public Task<(bool Success, int ExitCode, string Output)> RunCommandDetectionAsync(
            string commandLine,
            StringBuilder logBuilder)
            => ExecuteCommandDetectionAsync(commandLine, logBuilder);
    }
}

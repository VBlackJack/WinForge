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
using System.Text.Json;
using Moq;
using WinForge.GUI.Models;
using WinForge.GUI.Services;
using WinForge.GUI.Services.Implementations;
using WinForge.GUI.Services.PowerShell;

namespace WinForge.GUI.Tests;

public class ProcessOutputEncodingTests
{
    [Fact]
    public void PowerShellProcessWrapper_Invoke_DecodesUtf8Output()
    {
        RepositoryPathService pathService = new RepositoryPathService();
        PowerShellExecutionService executionService = new PowerShellExecutionService(pathService);
        string expected = "UTF8 sentinel \u00e9\u00e0\u2019";

        using PowerShellProcessWrapper wrapper = new PowerShellProcessWrapper(
            executionService.GetPowerShellPath(),
            pathService.GetSafeRepositoryRoot());

        IReadOnlyCollection<PSObject> result = wrapper
            .AddScript("$utf8 = New-Object System.Text.UTF8Encoding -ArgumentList $false; [Console]::OutputEncoding = $utf8; Write-Output ('UTF8 sentinel ' + [char]0x00e9 + [char]0x00e0 + [char]0x2019)")
            .Invoke();

        string output = Assert.Single(result).ToString() ?? string.Empty;
        Assert.Equal(expected, output);
        Assert.DoesNotContain("\u00c3", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpdateApplicationAsync_OmitsRawVendorOutputFromMainLog()
    {
        string rawVendorOutput = "raw vendor output \u00e9\u00e0\u2019";
        using JsonDocument document = JsonDocument.Parse("""
        {
          "Sources": {
            "Winget": "Example.Package"
          }
        }
        """);
        TestableApplicationManagementService service = CreateService("ExampleApp", document.RootElement);
        service.RawVendorOutput = rawVendorOutput;
        ApplicationModel app = new ApplicationModel
        {
            AppId = "ExampleApp",
            Name = "Example App",
            Status = ApplicationStatus.UpdateAvailable
        };

        InstallResult result = await service.UpdateApplicationAsync(app);

        Assert.False(result.Success);
        Assert.Contains("Updating via Winget: Example.Package", result.Logs, StringComparison.Ordinal);
        Assert.Contains("Winget update failed (exit code: 1)", result.Logs, StringComparison.Ordinal);
        Assert.Contains("Raw vendor output omitted from main log", result.Logs, StringComparison.Ordinal);
        Assert.DoesNotContain(rawVendorOutput, result.Logs, StringComparison.Ordinal);
        Assert.DoesNotContain("\u00c3", result.Logs, StringComparison.Ordinal);
    }

    private static TestableApplicationManagementService CreateService(string appId, JsonElement appData)
    {
        Mock<IRepositoryPathService> pathService = new Mock<IRepositoryPathService>(MockBehavior.Strict);
        Mock<IPowerShellExecutionService> executionService = new Mock<IPowerShellExecutionService>(MockBehavior.Strict);
        Mock<IApplicationCacheService> cacheService = new Mock<IApplicationCacheService>(MockBehavior.Strict);
        Mock<IApplicationDetectionService> detectionService = new Mock<IApplicationDetectionService>(MockBehavior.Strict);
        Mock<IApplicationLauncher> launcher = new Mock<IApplicationLauncher>(MockBehavior.Strict);

        JsonElement cachedAppData = appData;
        cacheService
            .Setup(service => service.EnsureApplicationsCacheAsync())
            .Returns(Task.CompletedTask);
        cacheService
            .Setup(service => service.TryGetApplicationData(appId, out cachedAppData))
            .Returns(true);
        executionService
            .SetupGet(service => service.InstallationTimeoutMs)
            .Returns(5000);

        return new TestableApplicationManagementService(
            pathService.Object,
            executionService.Object,
            cacheService.Object,
            detectionService.Object,
            launcher.Object);
    }

    private sealed class TestableApplicationManagementService : ApplicationManagementServiceImpl
    {
        public TestableApplicationManagementService(
            IRepositoryPathService pathService,
            IPowerShellExecutionService executionService,
            IApplicationCacheService cacheService,
            IApplicationDetectionService detectionService,
            IApplicationLauncher launcher)
            : base(pathService, executionService, cacheService, detectionService, launcher)
        {
        }

        public string RawVendorOutput { get; set; } = string.Empty;

        protected override Task<(bool Success, int ExitCode, string Output)> ExecuteUpdateCommandAsync(
            string command,
            string arguments,
            StringBuilder logBuilder)
        {
            AppendVendorOutputSummary(logBuilder, RawVendorOutput, string.Empty);
            return Task.FromResult((false, 1, RawVendorOutput));
        }
    }
}

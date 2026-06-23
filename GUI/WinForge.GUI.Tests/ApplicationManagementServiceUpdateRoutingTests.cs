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

public class ApplicationManagementServiceUpdateRoutingTests
{
    private const string ChocolateyAppId = "Chocolatey";

    [Fact]
    public async Task GetAllApplicationsAsync_WithPrerequisiteTag_ShouldMarkPrerequisiteSeparatelyFromDefaultRequired()
    {
        using JsonDocument prerequisiteDocument = CreateApplicationDocument("""
        {
          "Name": "PowerShell 7",
          "Category": "System",
          "DefaultRequired": true,
          "Tags": [ "system", "runtime", "prerequisite" ],
          "Sources": {
            "Winget": "Microsoft.PowerShell"
          }
        }
        """);
        using JsonDocument requiredAppDocument = CreateApplicationDocument("""
        {
          "Name": "Google Chrome",
          "Category": "Browser",
          "DefaultRequired": true,
          "Tags": [ "browser" ],
          "Sources": {
            "Winget": "Google.Chrome"
          }
        }
        """);

        TestableApplicationManagementService service = CreateService(
            new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
            {
                ["PowerShell7"] = prerequisiteDocument.RootElement,
                ["GoogleChrome"] = requiredAppDocument.RootElement
            });

        List<ApplicationModel> applications = await service.GetAllApplicationsAsync();

        ApplicationModel powerShell = Assert.Single(applications, app => app.AppId == "PowerShell7");
        Assert.True(powerShell.IsRequired);
        Assert.True(powerShell.IsPrerequisite);
        Assert.True(powerShell.IsRequiredPrerequisite);

        ApplicationModel chrome = Assert.Single(applications, app => app.AppId == "GoogleChrome");
        Assert.True(chrome.IsRequired);
        Assert.False(chrome.IsPrerequisite);
        Assert.False(chrome.IsRequiredPrerequisite);
    }

    [Fact]
    public async Task UpdateApplicationAsync_WithPreferredChocolateySource_RoutesThroughChocoWithoutWinget()
    {
        using JsonDocument document = CreateApplicationDocument("""
        {
          "PreferredUpdateSource": "Chocolatey",
          "Sources": {
            "Winget": "Chocolatey.Chocolatey",
            "Chocolatey": "chocolatey"
          },
          "Detection": {
            "Method": "Command",
            "Command": "choco --version",
            "VersionRegex": "(\\d+\\.\\d+[\\.\\d]*)"
          }
        }
        """);
        TestableApplicationManagementService service = CreateService(ChocolateyAppId, document.RootElement);
        service.UpdateResults.Enqueue((true, 0, "Chocolatey upgraded"));
        service.CommandDetectionResults.Enqueue((true, 0, "2.4.3"));
        ApplicationModel app = CreateUpdateAvailableApp(ChocolateyAppId, "Chocolatey");

        InstallResult result = await service.UpdateApplicationAsync(app);

        Assert.True(result.Success);
        Assert.Equal("Chocolatey", result.Method);
        CommandCall updateCall = Assert.Single(service.UpdateCommandCalls);
        Assert.Equal("choco", updateCall.Command);
        Assert.Equal("upgrade chocolatey -y --no-progress", updateCall.Arguments);
        Assert.DoesNotContain(service.UpdateCommandCalls, call => call.Command == "winget");
        Assert.Equal("choco --version", Assert.Single(service.CommandDetectionCalls));
        Assert.Contains("Preferred update source: Chocolatey", result.Logs, StringComparison.Ordinal);
        Assert.Contains("Post-update command detection succeeded", result.Logs, StringComparison.Ordinal);
        Assert.DoesNotContain("Updating via Winget", result.Logs, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpdateApplicationAsync_WithoutPreferredUpdateSource_KeepsWingetFirst()
    {
        using JsonDocument document = CreateApplicationDocument("""
        {
          "Sources": {
            "Winget": "Example.Package",
            "Chocolatey": "example"
          }
        }
        """);
        TestableApplicationManagementService service = CreateService("ExampleApp", document.RootElement);
        service.UpdateResults.Enqueue((true, 0, "Winget upgraded"));
        ApplicationModel app = CreateUpdateAvailableApp("ExampleApp", "Example App");

        InstallResult result = await service.UpdateApplicationAsync(app);

        Assert.True(result.Success);
        Assert.Equal("Winget", result.Method);
        CommandCall updateCall = Assert.Single(service.UpdateCommandCalls);
        Assert.Equal("winget", updateCall.Command);
        Assert.Equal(
            "upgrade --id \"Example.Package\" --silent --accept-package-agreements --accept-source-agreements",
            updateCall.Arguments);
        Assert.Empty(service.CommandDetectionCalls);
    }

    private static JsonDocument CreateApplicationDocument(string json) => JsonDocument.Parse(json);

    private static ApplicationModel CreateUpdateAvailableApp(string appId, string name)
    {
        return new ApplicationModel
        {
            AppId = appId,
            Name = name,
            Status = ApplicationStatus.UpdateAvailable
        };
    }

    private static TestableApplicationManagementService CreateService(string appId, JsonElement appData)
    {
        TestableApplicationManagementService service = CreateService(
            new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
            {
                [appId] = appData
            });

        return service;
    }

    private static TestableApplicationManagementService CreateService(IReadOnlyDictionary<string, JsonElement> applicationsCache)
    {
        Mock<IRepositoryPathService> pathService = new Mock<IRepositoryPathService>(MockBehavior.Strict);
        Mock<IPowerShellExecutionService> executionService = new Mock<IPowerShellExecutionService>(MockBehavior.Strict);
        Mock<IApplicationCacheService> cacheService = new Mock<IApplicationCacheService>(MockBehavior.Strict);
        Mock<IApplicationDetectionService> detectionService = new Mock<IApplicationDetectionService>(MockBehavior.Strict);
        Mock<IApplicationLauncher> launcher = new Mock<IApplicationLauncher>(MockBehavior.Strict);

        cacheService
            .Setup(service => service.EnsureApplicationsCacheAsync())
            .Returns(Task.CompletedTask);
        cacheService
            .SetupGet(service => service.ApplicationsCache)
            .Returns(applicationsCache);
        foreach (KeyValuePair<string, JsonElement> entry in applicationsCache)
        {
            JsonElement cachedAppData = entry.Value;
            cacheService
                .Setup(service => service.TryGetApplicationData(entry.Key, out cachedAppData))
                .Returns(true);
        }

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

        public List<CommandCall> UpdateCommandCalls { get; } = new();

        public List<string> CommandDetectionCalls { get; } = new();

        public Queue<(bool Success, int ExitCode, string Output)> UpdateResults { get; } = new();

        public Queue<(bool Success, int ExitCode, string Output)> CommandDetectionResults { get; } = new();

        protected override Task<(bool Success, int ExitCode, string Output)> ExecuteUpdateCommandAsync(
            string command,
            string arguments,
            StringBuilder logBuilder)
        {
            UpdateCommandCalls.Add(new CommandCall(command, arguments));
            (bool Success, int ExitCode, string Output) result = UpdateResults.Count > 0
                ? UpdateResults.Dequeue()
                : (false, -1, "No test update result configured");
            return Task.FromResult(result);
        }

        protected override Task<(bool Success, int ExitCode, string Output)> ExecuteCommandDetectionAsync(
            string commandLine,
            StringBuilder logBuilder)
        {
            CommandDetectionCalls.Add(commandLine);
            (bool Success, int ExitCode, string Output) result = CommandDetectionResults.Count > 0
                ? CommandDetectionResults.Dequeue()
                : (false, -1, "No test detection result configured");
            return Task.FromResult(result);
        }
    }

    private sealed record CommandCall(string Command, string Arguments);
}

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

using System.Text.Json;
using Moq;
using Win11Forge.GUI.Models;
using Win11Forge.GUI.Services;
using Win11Forge.GUI.Services.Implementations;
using Win11Forge.GUI.Services.PowerShell;

namespace Win11Forge.GUI.Tests;

public class ApplicationManagementServicePackageIdValidationTests
{
    private const string AppId = "TestApp";
    private const string AppName = "Test Application";
    private const string MaliciousPackageId = "evil\" & calc";

    [Theory]
    [InlineData("Google.Chrome", true)]
    [InlineData("vlc", true)]
    [InlineData("7zip.7zip", true)]
    [InlineData("a/b-c_d.1", true)]
    [InlineData("evil\" & calc", false)]
    [InlineData("a;b", false)]
    [InlineData("a b", false)]
    [InlineData("$(x)", false)]
    [InlineData("a|b", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void PackageIdValidator_ValidatesSafeCharset(string? packageId, bool expected)
    {
        bool actual = PackageIdValidator.IsValidPackageId(packageId);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("uninstall", "Winget", "Winget")]
    [InlineData("uninstall", "Chocolatey", "Chocolatey")]
    [InlineData("update", "Winget", "Winget")]
    [InlineData("update", "Chocolatey", "Chocolatey")]
    public async Task PackageManagerOperation_WithMaliciousId_FailsBeforeExecution(
        string operation,
        string sourceField,
        string sourceLabel)
    {
        using JsonDocument document = CreateApplicationDocument(sourceField, MaliciousPackageId);
        ApplicationManagementServiceImpl service = CreateService(AppId, document.RootElement);
        ApplicationModel app = CreateInstalledApp();

        InstallResult result = await ExecuteOperationAsync(operation, service, app);

        Assert.False(result.Success);
        Assert.Equal(Win11Forge.GUI.Resources.Resources.AppManagement_InvalidPackageId, result.Message);
        Assert.Contains(
            $"Rejected invalid {sourceLabel} package id (failed safe-charset validation)",
            result.Logs,
            StringComparison.Ordinal);
        Assert.DoesNotContain(MaliciousPackageId, result.Logs, StringComparison.Ordinal);
        Assert.DoesNotContain("Command execution failed", result.Logs, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CheckApplicationUpdate_WithMaliciousWingetId_ReturnsCannotDetermineWithoutExecution()
    {
        using JsonDocument document = CreateApplicationDocument("Winget", MaliciousPackageId);
        ApplicationManagementServiceImpl service = CreateService(AppId, document.RootElement);
        ApplicationModel app = CreateInstalledApp();

        UpdateCheckResult result = await service.CheckApplicationUpdateAsync(app);

        Assert.False(result.HasUpdate);
        Assert.Equal(Win11Forge.GUI.Resources.Resources.AppManagement_CannotDetermineVersion, result.ErrorMessage);
        Assert.Equal(string.Empty, result.CurrentVersion);
        Assert.Equal(string.Empty, result.AvailableVersion);
    }

    private static async Task<InstallResult> ExecuteOperationAsync(
        string operation,
        ApplicationManagementServiceImpl service,
        ApplicationModel app)
    {
        if (string.Equals(operation, "uninstall", StringComparison.Ordinal))
        {
            return await service.UninstallApplicationAsync(app);
        }

        if (string.Equals(operation, "update", StringComparison.Ordinal))
        {
            return await service.UpdateApplicationAsync(app);
        }

        throw new ArgumentOutOfRangeException(nameof(operation), operation, "Unknown operation.");
    }

    private static ApplicationModel CreateInstalledApp()
    {
        return new ApplicationModel
        {
            AppId = AppId,
            Name = AppName,
            Status = ApplicationStatus.Installed
        };
    }

    private static JsonDocument CreateApplicationDocument(string sourceField, string packageId)
    {
        Dictionary<string, string> sources = new Dictionary<string, string>
        {
            [sourceField] = packageId
        };
        Dictionary<string, object> application = new Dictionary<string, object>
        {
            ["Sources"] = sources
        };
        string json = JsonSerializer.Serialize(application);
        return JsonDocument.Parse(json);
    }

    private static ApplicationManagementServiceImpl CreateService(string appId, JsonElement appData)
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

        return new ApplicationManagementServiceImpl(
            pathService.Object,
            executionService.Object,
            cacheService.Object,
            detectionService.Object,
            launcher.Object);
    }
}

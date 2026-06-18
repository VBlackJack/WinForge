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

using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using Win11Forge.GUI.Configuration;
using Win11Forge.GUI.Models;
using Win11Forge.GUI.Services;
using Win11Forge.GUI.Services.PowerShell;

namespace Win11Forge.GUI.Tests;

public class DetectionProbeTests
{
    [Fact]
    public async Task ProbeAsync_FilePathWithTraversal_ReturnsInvalidInput()
    {
        DetectionProbe probe = new DetectionProbe();
        string root = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
        DetectionConfiguration configuration = new DetectionConfiguration
        {
            Method = DetectionMethodStrings.File,
            Path = Path.Combine(root, "Windows", "..", "notepad.exe")
        };

        DetectionProbeResult result = await probe.ProbeAsync(configuration, PathValidationPolicy.AdHoc);

        Assert.Equal(DetectionOutcome.InvalidInput, result.Outcome);
    }

    [Fact]
    public async Task ProbeAsync_ExistingAdHocFile_ReturnsFound()
    {
        using TestWorkspace workspace = new TestWorkspace();
        string probePath = Path.Combine(workspace.RootPath, "probe.txt");
        File.WriteAllText(probePath, "probe");
        DetectionProbe probe = new DetectionProbe();
        DetectionConfiguration configuration = new DetectionConfiguration
        {
            Method = DetectionMethodStrings.File,
            Path = probePath
        };

        DetectionProbeResult result = await probe.ProbeAsync(configuration, PathValidationPolicy.AdHoc);

        Assert.Equal(DetectionOutcome.Found, result.Outcome);
        Assert.Equal(DetectionSource.File, result.Source);
    }

    [Fact]
    public async Task ProbeAsync_RegistryVersionKeyExists_ReturnsFoundWithVersion()
    {
        string subKey = $@"Software\Win11Forge\Tests\DetectionProbe\{Guid.NewGuid():N}";
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(subKey);
        key.SetValue("ProductName", "Windows Test", RegistryValueKind.String);

        try
        {
            DetectionProbe probe = new DetectionProbe();
            DetectionConfiguration configuration = new DetectionConfiguration
            {
                Method = DetectionMethodStrings.Registry,
                Path = $@"HKCU:\{subKey}",
                VersionKey = "ProductName"
            };

            DetectionProbeResult result = await probe.ProbeAsync(configuration, PathValidationPolicy.AdHoc);

            Assert.Equal(DetectionOutcome.Found, result.Outcome);
            Assert.Equal(DetectionSource.Registry, result.Source);
            Assert.Equal("Windows Test", result.Version);
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKeyTree(subKey, throwOnMissingSubKey: false);
        }
    }

    [Fact]
    public async Task ProbeAsync_RegistryVersionKeyMissing_ReturnsNotFound()
    {
        string subKey = $@"Software\Win11Forge\Tests\DetectionProbe\{Guid.NewGuid():N}";
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(subKey);
        key.SetValue("ProductName", "Windows Test", RegistryValueKind.String);

        try
        {
            DetectionProbe probe = new DetectionProbe();
            DetectionConfiguration configuration = new DetectionConfiguration
            {
                Method = DetectionMethodStrings.Registry,
                Path = $@"HKCU:\{subKey}",
                VersionKey = "ZZZ_DoesNotExist"
            };

            DetectionProbeResult result = await probe.ProbeAsync(configuration, PathValidationPolicy.AdHoc);

            Assert.Equal(DetectionOutcome.NotFound, result.Outcome);
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKeyTree(subKey, throwOnMissingSubKey: false);
        }
    }

    [Fact]
    public async Task ProbeAsync_StoreApp_ReturnsUnsupported()
    {
        DetectionProbe probe = new DetectionProbe();
        DetectionConfiguration configuration = new DetectionConfiguration
        {
            Method = nameof(DetectionMethod.StoreApp)
        };

        DetectionProbeResult result = await probe.ProbeAsync(configuration, PathValidationPolicy.Strict);

        Assert.Equal(DetectionOutcome.Unsupported, result.Outcome);
    }

    [Fact]
    public async Task ProbeAsync_InvalidWindowsFeatureName_ReturnsInvalidInput()
    {
        DetectionProbe probe = new DetectionProbe();
        DetectionConfiguration configuration = new DetectionConfiguration
        {
            Method = DetectionMethodStrings.WindowsFeature,
            FeatureName = "NetFx3;Start-Process calc"
        };

        DetectionProbeResult result = await probe.ProbeAsync(configuration, PathValidationPolicy.Strict);

        Assert.Equal(DetectionOutcome.InvalidInput, result.Outcome);
    }

    [Fact]
    public void ClassifyWindowsFeatureResult_ElevationFailure_ReturnsError()
    {
        DetectionProbeResult result = DetectionProbe.ClassifyWindowsFeatureResult(
            1,
            string.Empty,
            "Get-WindowsOptionalFeature : The requested operation requires elevation.");

        Assert.Equal(DetectionOutcome.Error, result.Outcome);
        Assert.NotNull(result.Detail);
        Assert.Contains("Administrator privileges", result.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void ClassifyWindowsFeatureResult_DisabledFeature_ReturnsNotFound()
    {
        DetectionProbeResult result = DetectionProbe.ClassifyWindowsFeatureResult(
            0,
            "Disabled",
            string.Empty);

        Assert.Equal(DetectionOutcome.NotFound, result.Outcome);
    }

    [Fact]
    public void ClassifyWindowsFeatureResult_EnabledFeature_ReturnsFound()
    {
        DetectionProbeResult result = DetectionProbe.ClassifyWindowsFeatureResult(
            0,
            "Enabled",
            string.Empty);

        Assert.Equal(DetectionOutcome.Found, result.Outcome);
        Assert.Equal(DetectionSource.WindowsFeature, result.Source);
        Assert.Equal("enabled", result.Version);
    }

    [Fact]
    public void AddWin11ForgeServices_ShouldRegisterDetectionProbeSingleton()
    {
        ServiceCollection services = new ServiceCollection();

        using ServiceProvider provider = services.AddWin11ForgeServices().BuildServiceProvider();
        IDetectionProbe first = provider.GetRequiredService<IDetectionProbe>();
        IDetectionProbe second = provider.GetRequiredService<IDetectionProbe>();

        Assert.IsType<DetectionProbe>(first);
        Assert.Same(first, second);
    }

    [Fact]
    public async Task DetectApplicationAsync_WhenProbeFindsPackage_MapsResultWithStrictPolicy()
    {
        using TestWorkspace workspace = new TestWorkspace();
        string databaseDirectory = Path.Combine(
            workspace.RepositoryRoot,
            Win11ForgePathNames.AppsDirectoryName,
            Win11ForgePathNames.DatabaseDirectoryName);
        Directory.CreateDirectory(databaseDirectory);
        string databasePath = Path.Combine(databaseDirectory, Win11ForgePathNames.ApplicationsDatabaseFileName);
        File.WriteAllText(databasePath, """
            {
              "Applications": {
                "TestApp": {
                  "Name": "Test App",
                  "Sources": {
                    "Winget": "Test.App"
                  },
                  "Detection": {
                    "Method": "File",
                    "Path": "%ProgramFiles%\\Test\\test.exe"
                  }
                }
              }
            }
            """);

        RepositoryPathService pathService = new RepositoryPathService(workspace.RepositoryRoot, [workspace.UserDataBasePath]);
        StaticDetectionProbe probe = new StaticDetectionProbe(
            DetectionProbeResult.Found(DetectionSource.File, "1.2.3"));
        JsonApplicationDetectionService service = new JsonApplicationDetectionService(pathService, null, probe);

        InstalledPackageInfo? packageInfo = await service.DetectApplicationAsync("Test.App");

        Assert.NotNull(packageInfo);
        Assert.Equal("Test.App", packageInfo.Id);
        Assert.Equal("Test App", packageInfo.Name);
        Assert.Equal("1.2.3", packageInfo.InstalledVersion);
        Assert.Equal(DetectionSource.File, packageInfo.Source);
        Assert.Equal(PathValidationPolicy.Strict, probe.CapturedPolicy);
        Assert.NotNull(probe.CapturedConfig);
        Assert.Equal(DetectionMethodStrings.File, probe.CapturedConfig.Method);
    }

    private sealed class StaticDetectionProbe : IDetectionProbe
    {
        private readonly DetectionProbeResult _result;

        public StaticDetectionProbe(DetectionProbeResult result)
        {
            _result = result;
        }

        public DetectionConfiguration? CapturedConfig { get; private set; }

        public PathValidationPolicy CapturedPolicy { get; private set; }

        public Task<DetectionProbeResult> ProbeAsync(
            DetectionConfiguration config,
            PathValidationPolicy pathPolicy,
            CancellationToken cancellationToken = default)
        {
            CapturedConfig = config;
            CapturedPolicy = pathPolicy;
            return Task.FromResult(_result);
        }
    }
}

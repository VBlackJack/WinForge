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
using System.IO;
using Moq;
using Win11Forge.GUI.Models;
using Win11Forge.GUI.Services;

namespace Win11Forge.GUI.Tests;

/// <summary>
/// Tests for ApplicationDatabaseService serialization/parsing behaviors.
/// </summary>
public class ApplicationDatabaseServiceTests
{
    [Fact]
    public async Task LoadApplicationsAsync_ShouldParseCamelCaseSourceProperties()
    {
        // Arrange
        var repoRoot = CreateTempRepository();
        try
        {
            WriteDatabase(repoRoot, """
            {
              "DatabaseVersion": "test",
              "Applications": {
                "TestApp": {
                  "Name": "Test App",
                  "Category": "Utility",
                  "Description": "Desc",
                  "Sources": {
                    "winget": "Google.Chrome",
                    "chocolatey": "googlechrome",
                    "store": "9NBLGGH4NNS1",
                    "directUrl": "https://example.com/installer.exe",
                    "wingetConfig": {
                      "version": "1.2.3",
                      "source": "winget",
                      "additionalArgs": "--silent"
                    }
                  },
                  "Detection": {
                    "method": "Registry",
                    "path": "HKLM:\\Software\\Test",
                    "versionKey": "Version",
                    "minVersion": "1.0"
                  },
                  "DefaultPriority": 50
                }
              }
            }
            """);

            var bridge = CreateBridge(repoRoot);
            var service = new ApplicationDatabaseService(bridge.Object);

            // Act
            var applications = (await service.LoadApplicationsAsync()).ToList();

            // Assert
            var app = Assert.Single(applications);
            Assert.Equal("TestApp", app.AppId);
            Assert.Equal("Google.Chrome", app.Sources.Winget);
            Assert.Equal("googlechrome", app.Sources.Chocolatey);
            Assert.Equal("9NBLGGH4NNS1", app.Sources.Store);
            Assert.Equal("https://example.com/installer.exe", app.Sources.DirectUrl);
            Assert.NotNull(app.Sources.WingetConfig);
            Assert.Equal("1.2.3", app.Sources.WingetConfig!.Version);
            Assert.Equal("winget", app.Sources.WingetConfig.Source);
            Assert.Equal("--silent", app.Sources.WingetConfig.AdditionalArgs);
            Assert.NotNull(app.Detection);
            Assert.Equal("Registry", app.Detection!.Method);
            Assert.Equal("HKLM:\\Software\\Test", app.Detection.Path);
            Assert.Equal("Version", app.Detection.VersionKey);
            Assert.Equal("1.0", app.Detection.MinVersion);
        }
        finally
        {
            TryDeleteDirectory(repoRoot);
        }
    }

    [Fact]
    public async Task SaveApplicationAsync_ShouldSerializePascalCaseSourceKeys()
    {
        // Arrange
        var repoRoot = CreateTempRepository();
        try
        {
            WriteDatabase(repoRoot, """
            {
              "DatabaseVersion": "test",
              "Applications": {}
            }
            """);

            string? capturedScript = null;
            var bridge = CreateBridge(repoRoot);
            bridge
                .Setup(x => x.ExecuteCommandAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback<string, CancellationToken>((script, _) => capturedScript = script)
                .ReturnsAsync("""{"Success":true}""");

            var service = new ApplicationDatabaseService(bridge.Object);

            var app = new EditableApplicationModel
            {
                AppId = "MyTestApp",
                Name = "My Test App",
                Category = "Utility",
                Description = "desc",
                Sources = new ApplicationSourcesModel
                {
                    Winget = "Google.Chrome",
                    Chocolatey = "googlechrome",
                    Store = "9NBLGGH4NNS1",
                    DirectUrl = "https://example.com/installer.exe",
                    WingetConfig = new WingetSourceConfig
                    {
                        Version = "1.2.3",
                        Source = "winget",
                        AdditionalArgs = "--silent"
                    },
                    ChocolateyConfig = new ChocolateySourceConfig
                    {
                        Version = "1.0.0",
                        AdditionalArgs = "--yes"
                    },
                    DirectDownloadConfig = new DirectDownloadSourceConfig
                    {
                        InstallerType = "exe",
                        SilentArgs = "/S",
                        Checksum = "sha256:abcdef",
                        FileName = "installer.exe"
                    }
                },
                Detection = new ApplicationDetectionModel
                {
                    Method = "Registry",
                    Path = "HKLM:\\Software\\Test",
                    VersionKey = "Version",
                    MinVersion = "1.0"
                },
                DefaultPriority = 50
            };

            // Act
            var result = await service.SaveApplicationAsync(app, isNew: true);

            // Assert
            Assert.True(result.Success);
            Assert.False(string.IsNullOrWhiteSpace(capturedScript));

            Assert.Contains("\"Winget\":", capturedScript!);
            Assert.Contains("\"Chocolatey\":", capturedScript!);
            Assert.Contains("\"Store\":", capturedScript!);
            Assert.Contains("\"DirectUrl\":", capturedScript!);
            Assert.DoesNotContain("\"winget\":", capturedScript!);
            Assert.DoesNotContain("\"chocolatey\":", capturedScript!);
            Assert.DoesNotContain("\"store\":", capturedScript!);
            Assert.DoesNotContain("\"directUrl\":", capturedScript!);
        }
        finally
        {
            TryDeleteDirectory(repoRoot);
        }
    }

    private static Mock<IPowerShellBridge> CreateBridge(string repositoryRoot)
    {
        var bridge = new Mock<IPowerShellBridge>(MockBehavior.Strict);
        bridge.SetupGet(x => x.RepositoryRoot).Returns(repositoryRoot);
        bridge
            .Setup(x => x.ExecuteCommandAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("""{"Success":true}""");

        return bridge;
    }

    private static string CreateTempRepository()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "Win11Forge.Gui.Tests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(Path.Combine(root, "Apps", "Database"));
        return root;
    }

    private static void WriteDatabase(string repoRoot, string json)
    {
        var path = Path.Combine(repoRoot, "Apps", "Database", "applications.json");
        File.WriteAllText(path, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup.
        }
    }
}

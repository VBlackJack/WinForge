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
using System.Text;
using System.Text.Json;
using Moq;
using Win11Forge.GUI.Models;
using Win11Forge.GUI.Services;
using DataValidationContext = System.ComponentModel.DataAnnotations.ValidationContext;
using DataValidationResult = System.ComponentModel.DataAnnotations.ValidationResult;
using DataValidator = System.ComponentModel.DataAnnotations.Validator;

namespace Win11Forge.GUI.Tests;

/// <summary>
/// Tests for ApplicationDatabaseService serialization/parsing behaviors.
/// </summary>
public class ApplicationDatabaseServiceTests
{
    private const string LowerSha256 = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string UpperSha256 = "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF";

    [Fact]
    public async Task LoadApplicationsAsync_ShouldParseCamelCaseSourceProperties()
    {
        // Arrange
        string repoRoot = CreateTempRepository();
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

            Mock<IPowerShellBridge> bridge = CreateBridge(repoRoot);
            ApplicationDatabaseService service = new ApplicationDatabaseService(bridge.Object);

            // Act
            List<EditableApplicationModel> applications = (await service.LoadApplicationsAsync()).ToList();

            // Assert
            EditableApplicationModel app = Assert.Single(applications);
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
        string repoRoot = CreateTempRepository();
        try
        {
            WriteDatabase(repoRoot, """
            {
              "DatabaseVersion": "test",
              "Applications": {}
            }
            """);

            string? capturedScript = null;
            Mock<IPowerShellBridge> bridge = CreateBridge(repoRoot);
            bridge
                .Setup(x => x.ExecuteCommandAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback<string, CancellationToken>((script, _) => capturedScript = script)
                .ReturnsAsync("""{"Success":true}""");

            ApplicationDatabaseService service = new ApplicationDatabaseService(bridge.Object);

            EditableApplicationModel app = new EditableApplicationModel
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
                        Checksum = "sha256:" + LowerSha256,
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
                DefaultPriority = 50,
                LastVerified = "2026-02-12",
                Verified = true
            };

            // Act
            ApplicationSaveResult result = await service.SaveApplicationAsync(app, isNew: true);

            // Assert
            Assert.True(result.Success);
            Assert.False(string.IsNullOrWhiteSpace(capturedScript));

            Assert.Contains("\"Winget\":", capturedScript!);
            Assert.Contains("\"Chocolatey\":", capturedScript!);
            Assert.Contains("\"Store\":", capturedScript!);
            Assert.Contains("\"DirectUrl\":", capturedScript!);
            Assert.Contains("\"LastVerified\":", capturedScript!);
            Assert.Contains("\"Verified\":", capturedScript!);
            Assert.Contains("2026-02-12", capturedScript!);
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

    [Fact]
    public async Task Save_DirectDownloadApp_EmitsCanonicalSha256AndInstallArguments()
    {
        // Arrange
        string repoRoot = CreateTempRepository();
        try
        {
            WriteDatabase(repoRoot, """
            {
              "DatabaseVersion": "test",
              "Applications": {}
            }
            """);

            string? capturedScript = null;
            Mock<IPowerShellBridge> bridge = CreateBridge(repoRoot);
            bridge
                .Setup(x => x.ExecuteCommandAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback<string, CancellationToken>((script, _) => capturedScript = script)
                .ReturnsAsync("""{"Success":true}""");

            ApplicationDatabaseService service = new ApplicationDatabaseService(bridge.Object);
            EditableApplicationModel app = new EditableApplicationModel
            {
                AppId = "DirectApp",
                Name = "Direct App",
                Category = "Utility",
                Sources = new ApplicationSourcesModel
                {
                    DirectUrl = "https://example.com/installer.exe",
                    DirectDownloadConfig = new DirectDownloadSourceConfig
                    {
                        InstallerType = "exe",
                        SilentArgs = "/S",
                        Checksum = "sha256:" + LowerSha256,
                        FileName = "installer.exe"
                    }
                }
            };

            // Act
            ApplicationSaveResult result = await service.SaveApplicationAsync(app, isNew: true);

            // Assert
            Assert.True(result.Success);
            Assert.False(string.IsNullOrWhiteSpace(capturedScript));

            string appJson = ExtractEmbeddedApplicationJson(capturedScript!);
            using JsonDocument document = JsonDocument.Parse(appJson);
            JsonElement root = document.RootElement;
            JsonElement sources = root.GetProperty("Sources");

            Assert.Equal("/S", root.GetProperty("InstallArguments").GetString());
            Assert.Equal(UpperSha256, sources.GetProperty("SHA256").GetString());
        }
        finally
        {
            TryDeleteDirectory(repoRoot);
        }
    }

    [Fact]
    public async Task Load_CanonicalDirectDownload_HydratesEditorFields()
    {
        // Arrange
        string repoRoot = CreateTempRepository();
        try
        {
            WriteDatabase(repoRoot, $$"""
            {
              "DatabaseVersion": "test",
              "Applications": {
                "CanonicalDirect": {
                  "Name": "Canonical Direct",
                  "Category": "Utility",
                  "InstallArguments": "/quiet",
                  "Sources": {
                    "DirectUrl": "https://example.com/installer.exe",
                    "SHA256": "{{UpperSha256}}"
                  },
                  "DefaultPriority": 50
                }
              }
            }
            """);

            Mock<IPowerShellBridge> bridge = CreateBridge(repoRoot);
            ApplicationDatabaseService service = new ApplicationDatabaseService(bridge.Object);

            // Act
            List<EditableApplicationModel> applications = (await service.LoadApplicationsAsync()).ToList();

            // Assert
            EditableApplicationModel app = Assert.Single(applications);
            Assert.Equal("/quiet", app.InstallArguments);
            Assert.NotNull(app.Sources.DirectDownloadConfig);
            Assert.Equal("sha256:" + LowerSha256, app.Sources.DirectDownloadConfig!.Checksum);
            Assert.Equal("/quiet", app.Sources.DirectDownloadConfig.SilentArgs);
        }
        finally
        {
            TryDeleteDirectory(repoRoot);
        }
    }

    [Fact]
    public async Task Save_AppWithTopLevelInstallArguments_NotDropped()
    {
        // Arrange
        string repoRoot = CreateTempRepository();
        try
        {
            WriteDatabase(repoRoot, """
            {
              "DatabaseVersion": "test",
              "Applications": {}
            }
            """);

            string? capturedScript = null;
            Mock<IPowerShellBridge> bridge = CreateBridge(repoRoot);
            bridge
                .Setup(x => x.ExecuteCommandAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback<string, CancellationToken>((script, _) => capturedScript = script)
                .ReturnsAsync("""{"Success":true}""");

            ApplicationDatabaseService service = new ApplicationDatabaseService(bridge.Object);
            EditableApplicationModel app = new EditableApplicationModel
            {
                AppId = "WingetOnly",
                Name = "Winget Only",
                Category = "Utility",
                InstallArguments = "/verysilent",
                Sources = new ApplicationSourcesModel
                {
                    Winget = "Example.Package"
                }
            };

            // Act
            ApplicationSaveResult result = await service.SaveApplicationAsync(app, isNew: true);

            // Assert
            Assert.True(result.Success);
            Assert.False(string.IsNullOrWhiteSpace(capturedScript));

            string appJson = ExtractEmbeddedApplicationJson(capturedScript!);
            WriteDatabase(repoRoot, $$"""
            {
              "DatabaseVersion": "test",
              "Applications": {
                "WingetOnly": {{appJson}}
              }
            }
            """);

            List<EditableApplicationModel> applications = (await service.LoadApplicationsAsync()).ToList();
            EditableApplicationModel reloaded = Assert.Single(applications);
            Assert.Equal("/verysilent", reloaded.InstallArguments);
        }
        finally
        {
            TryDeleteDirectory(repoRoot);
        }
    }

    [Fact]
    public async Task Validation_RejectsNonSha256Checksum()
    {
        DirectDownloadSourceConfig sha1Config = new DirectDownloadSourceConfig
        {
            Checksum = "sha1:abc"
        };
        DirectDownloadSourceConfig md5Config = new DirectDownloadSourceConfig
        {
            Checksum = "md5:abc"
        };
        DirectDownloadSourceConfig sha256Config = new DirectDownloadSourceConfig
        {
            Checksum = "sha256:" + LowerSha256
        };

        Assert.False(IsDirectDownloadConfigValid(sha1Config));
        Assert.False(IsDirectDownloadConfigValid(md5Config));
        Assert.True(IsDirectDownloadConfigValid(sha256Config));

        string repoRoot = CreateTempRepository();
        try
        {
            WriteDatabase(repoRoot, """
            {
              "DatabaseVersion": "test",
              "Applications": {}
            }
            """);

            Mock<IPowerShellBridge> bridge = CreateBridge(repoRoot);
            ApplicationDatabaseService service = new ApplicationDatabaseService(bridge.Object);
            EditableApplicationModel app = new EditableApplicationModel
            {
                AppId = "BadChecksum",
                Name = "Bad Checksum",
                Category = "Utility",
                Sources = new ApplicationSourcesModel
                {
                    DirectUrl = "https://example.com/installer.exe",
                    DirectDownloadConfig = sha1Config
                }
            };

            ApplicationValidationResult validation = await service.ValidateApplicationAsync(app, isNew: true);

            Assert.False(validation.IsValid);
            Assert.Contains(validation.Errors, error => error.Field.Contains("Checksum", StringComparison.Ordinal));
        }
        finally
        {
            TryDeleteDirectory(repoRoot);
        }
    }

    private static Mock<IPowerShellBridge> CreateBridge(string repositoryRoot)
    {
        Mock<IPowerShellBridge> bridge = new Mock<IPowerShellBridge>(MockBehavior.Strict);
        bridge.SetupGet(x => x.RepositoryRoot).Returns(repositoryRoot);
        bridge
            .Setup(x => x.ExecuteCommandAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("""{"Success":true}""");

        return bridge;
    }

    private static string CreateTempRepository()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            "Win11Forge.Gui.Tests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(Path.Combine(root, "Apps", "Database"));
        return root;
    }

    private static void WriteDatabase(string repoRoot, string json)
    {
        string path = Path.Combine(repoRoot, "Apps", "Database", "applications.json");
        File.WriteAllText(path, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string ExtractEmbeddedApplicationJson(string script)
    {
        const string StartMarker = "$appData = @'";
        const string EndMarker = "\n'@ | ConvertFrom-Json";

        int startMarkerIndex = script.IndexOf(StartMarker, StringComparison.Ordinal);
        Assert.True(startMarkerIndex >= 0, "PowerShell script should include the appData here-string.");

        int jsonStart = script.IndexOf('\n', startMarkerIndex);
        Assert.True(jsonStart >= 0, "PowerShell script should put JSON on the line after the here-string marker.");
        jsonStart++;

        int jsonEnd = script.IndexOf(EndMarker, jsonStart, StringComparison.Ordinal);
        Assert.True(jsonEnd > jsonStart, "PowerShell script should close the appData here-string after JSON.");

        return script.Substring(jsonStart, jsonEnd - jsonStart).Trim();
    }

    private static bool IsDirectDownloadConfigValid(DirectDownloadSourceConfig config)
    {
        DataValidationContext context = new DataValidationContext(config);
        List<DataValidationResult> results = new List<DataValidationResult>();
        return DataValidator.TryValidateObject(config, context, results, validateAllProperties: true);
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

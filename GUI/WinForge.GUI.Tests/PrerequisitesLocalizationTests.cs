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
using Moq;
using WinForge.GUI.Localization;
using WinForge.GUI.Services;
using WinForge.GUI.Services.Implementations;
using WinForge.GUI.Services.PowerShell;
using WinForge.GUI.Tests.TestInfrastructure;

namespace WinForge.GUI.Tests;

public class PrerequisitesLocalizationTests
{
    [Theory]
    [InlineData("en", "en")]
    [InlineData("en-US", "en")]
    [InlineData("fr", "fr")]
    [InlineData("fr-FR", "fr")]
    [InlineData("de-DE", "en")]
    [InlineData("", "en")]
    [InlineData(null, "en")]
    public void ResolvePowerShellLocaleCode_NormalizesToSupportedLocales(
        string? languageCode,
        string expected)
    {
        string actual = PrerequisitesServiceImpl.ResolvePowerShellLocaleCode(languageCode);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void SupportedLocales_HaveMatchingResxFiles()
    {
        string resourcesDirectory = RepositoryPathHelper.FindDirectory("GUI", "WinForge.GUI", "Resources");
        List<string> expectedFiles = SupportedLocales.Codes
            .Select(code => code == SupportedLocales.Default
                ? "Resources.resx"
                : $"Resources.{code}.resx")
            .ToList();

        foreach (string? file in expectedFiles)
        {
            string path = Path.Combine(resourcesDirectory, file);
            Assert.True(File.Exists(path), $"Missing resx for declared locale: {file}");
        }

        List<(string FileName, string Code)> localizedResxFiles = Directory.GetFiles(resourcesDirectory, "Resources.*.resx")
            .Select(path => (
                FileName: Path.GetFileName(path),
                Code: Path.GetFileNameWithoutExtension(path)!
                    .Replace("Resources.", string.Empty, StringComparison.Ordinal)))
            .ToList();

        foreach ((string FileName, string Code) resxFile in localizedResxFiles)
        {
            Assert.True(
                SupportedLocales.Codes.Contains(resxFile.Code, StringComparer.OrdinalIgnoreCase),
                $"Resx file {resxFile.FileName} has no entry in SupportedLocales.Codes");
        }
    }

    [Fact]
    public async Task InstallPrerequisitesAsync_InitializesPowerShellLocaleFromAppSettings()
    {
        CapturingPowerShellExecutionService executionService = new CapturingPowerShellExecutionService();
        Mock<IAppSettingsService> settingsService = new Mock<IAppSettingsService>();
        settingsService
            .Setup(service => service.LoadSettings())
            .Returns(new AppSettings { LanguageCode = "fr-FR" });

        PrerequisitesServiceImpl service = new PrerequisitesServiceImpl(
            new RepositoryPathService(),
            executionService,
            Mock.Of<IVersionService>(),
            Mock.Of<ISystemInfoService>(),
            settingsService.Object);

        bool success = await service.InstallPrerequisitesAsync();

        Assert.True(success);
        Assert.NotNull(executionService.StreamingScript);
        Assert.Contains("Initialize-Localization -Locale 'fr'", executionService.StreamingScript);
        Assert.Contains("[Console]::OutputEncoding = $utf8NoBom", executionService.StreamingScript);
        Assert.Contains("$OutputEncoding = $utf8NoBom", executionService.StreamingScript);
    }

    private sealed class CapturingPowerShellExecutionService : IPowerShellExecutionService
    {
        public int DefaultQueryTimeoutMs => 300000;

        public int InstallationTimeoutMs => 2850000;

        public string? StreamingScript { get; private set; }

        public string GetPowerShellPath() => "pwsh";

        public Task<string> ExecutePowerShellScriptAsync(
            string script,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(string.Empty);
        }

        public Task<(bool Success, string ErrorMessage)> ExecutePowerShellWithStreamingAsync(
            string script,
            Action<string>? outputCallback,
            CancellationToken cancellationToken = default)
        {
            StreamingScript = script;
            return Task.FromResult((true, string.Empty));
        }
    }
}

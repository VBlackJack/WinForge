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
using Win11Forge.GUI.Services;
using Win11Forge.GUI.Services.Implementations;
using Win11Forge.GUI.Services.PowerShell;

namespace Win11Forge.GUI.Tests;

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
        var actual = PrerequisitesServiceImpl.ResolvePowerShellLocaleCode(languageCode);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task InstallPrerequisitesAsync_InitializesPowerShellLocaleFromAppSettings()
    {
        var executionService = new CapturingPowerShellExecutionService();
        var settingsService = new Mock<IAppSettingsService>();
        settingsService
            .Setup(service => service.LoadSettings())
            .Returns(new AppSettings { LanguageCode = "fr-FR" });

        var service = new PrerequisitesServiceImpl(
            new RepositoryPathService(),
            executionService,
            Mock.Of<IVersionService>(),
            Mock.Of<ISystemInfoService>(),
            settingsService.Object);

        var success = await service.InstallPrerequisitesAsync();

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

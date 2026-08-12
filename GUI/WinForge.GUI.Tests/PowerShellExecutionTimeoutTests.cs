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
using System.Text.Json;
using WinForge.GUI.Services.PowerShell;

namespace WinForge.GUI.Tests;

/// <summary>
/// The GUI timeouts used to be hardcoded while the authoritative values lived in
/// Config/timeouts-settings.json, so raising a timeout in configuration silently left the
/// GUI killing installs early.
/// </summary>
public class PowerShellExecutionTimeoutTests
{
    [Fact]
    public void InstallationTimeout_ExceedsTheSlowestConfiguredInstall()
    {
        RepositoryPathService pathService = new RepositoryPathService();
        PowerShellExecutionService service = new PowerShellExecutionService(pathService);

        string configPath = pathService.GetPath("Config", "timeouts-settings.json");
        Assert.True(File.Exists(configPath), $"Timeout configuration not found at {configPath}");

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(configPath));
        JsonElement installation = document.RootElement.GetProperty("installation");

        int slowestSeconds = Math.Max(
            installation.GetProperty("officeTimeoutSeconds").GetInt32(),
            installation.GetProperty("defaultTimeoutSeconds").GetInt32());

        Assert.True(
            service.InstallationTimeoutMs > slowestSeconds * 1000,
            $"GUI installation timeout ({service.InstallationTimeoutMs} ms) must exceed the slowest "
            + $"configured install ({slowestSeconds * 1000} ms), otherwise the GUI reports a timeout "
            + "the engine never saw.");
    }

    [Fact]
    public void QueryTimeout_MatchesTheConfiguredDownloadTimeout()
    {
        RepositoryPathService pathService = new RepositoryPathService();
        PowerShellExecutionService service = new PowerShellExecutionService(pathService);

        using JsonDocument document = JsonDocument.Parse(
            File.ReadAllText(pathService.GetPath("Config", "timeouts-settings.json")));

        int downloadSeconds = document.RootElement
            .GetProperty("download")
            .GetProperty("timeoutSeconds")
            .GetInt32();

        Assert.Equal(downloadSeconds * 1000, service.DefaultQueryTimeoutMs);
    }
}

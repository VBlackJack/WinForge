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

using System.Reflection;
using Win11Forge.GUI.Models;
using Win11Forge.GUI.Services.Implementations;

namespace Win11Forge.GUI.Tests;

public class ApplicationManagementServiceDetectionMatchingTests
{
    [Theory]
    [InlineData("ClaudeDesktop", "Claude Desktop", "Claude", "Claude")]
    [InlineData("OpenAICodex", "Codex CLI", "OpenAI.Codex", "OpenAI.Codex")]
    public void FastDetectionMatching_ShouldMatchAppXPackageAliases(
        string appId,
        string appName,
        string packageId,
        string packageName)
    {
        var detectionResult = new BatchDetectionResult
        {
            Packages = new Dictionary<string, InstalledPackageInfo>(StringComparer.OrdinalIgnoreCase)
            {
                [packageId] = new()
                {
                    Id = packageId,
                    Name = packageName,
                    InstalledVersion = "1.2.3",
                    Source = DetectionSource.AppX
                }
            }
        };

        var app = new ApplicationModel
        {
            AppId = appId,
            Name = appName
        };

        var method = typeof(ApplicationManagementServiceImpl).GetMethod(
            "FindDetectedPackage",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);
        var match = Assert.IsType<InstalledPackageInfo>(
            method.Invoke(null, new object[] { app, detectionResult }));
        Assert.Equal(packageId, match.Id);
    }
}

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
using Win11Forge.GUI.Services;

namespace Win11Forge.GUI.Tests;

public class CriticalPathLoggingTests
{
    [Fact]
    public async Task PackageVerification_InvalidWingetId_WritesThroughFileLoggerFactory()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), "Win11Forge.CriticalPathLoggingTests", Guid.NewGuid().ToString("N"));
        string maliciousPackageId = "evil\" & calc";
        string? originalValue = Environment.GetEnvironmentVariable("WIN11FORGE_FILE_LOG");

        try
        {
            Environment.SetEnvironmentVariable("WIN11FORGE_FILE_LOG", null);
            Directory.CreateDirectory(tempDirectory);
            FileLogWriter writer = new FileLogWriter(tempDirectory);
            LoggerFactory loggerFactory = new LoggerFactory(writer);
            PackageVerificationService service = new PackageVerificationService(loggerFactory);

            await service.VerifyWingetPackageAsync(maliciousPackageId);

            string logPath = Path.Combine(tempDirectory, $"Win11Forge_{DateTime.Now:yyyy-MM-dd}.log");
            Assert.True(File.Exists(logPath));

            string content = File.ReadAllText(logPath);
            Assert.Contains("[WARNING]", content, StringComparison.Ordinal);
            Assert.Contains("Rejected invalid Winget package id", content, StringComparison.Ordinal);
            Assert.DoesNotContain(maliciousPackageId, content, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("WIN11FORGE_FILE_LOG", originalValue);
            TryDeleteDirectory(tempDirectory);
        }
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

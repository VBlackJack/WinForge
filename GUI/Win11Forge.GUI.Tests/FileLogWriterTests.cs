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
using System.Text.RegularExpressions;
using Win11Forge.GUI.Services;

namespace Win11Forge.GUI.Tests;

public class FileLogWriterTests
{
    [Fact]
    public void Write_CreatesDailyFile_WithStandardFormat()
    {
        string tempDirectory = CreateTempDirectory();
        try
        {
            FileLogWriter writer = new FileLogWriter(tempDirectory);
            LoggingService logger = new LoggingService(writer, "Cat");

            logger.LogInfo("hello");

            string logFilePath = GetTodayLogFilePath(tempDirectory);
            Assert.True(File.Exists(logFilePath));

            string content = File.ReadAllText(logFilePath);
            Assert.Contains("[INFO]", content, StringComparison.Ordinal);
            Assert.Contains("hello", content, StringComparison.Ordinal);
            Assert.Matches(
                new Regex(@"^\[\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\] \[INFO\]", RegexOptions.Multiline),
                content);
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void Disabled_ViaEnvVar_WritesNothing()
    {
        string tempDirectory = CreateTempDirectory();
        string? originalValue = Environment.GetEnvironmentVariable("WIN11FORGE_FILE_LOG");
        try
        {
            Environment.SetEnvironmentVariable("WIN11FORGE_FILE_LOG", "0");
            FileLogWriter writer = new FileLogWriter(tempDirectory);

            writer.Write("this should not be written");

            Assert.False(Directory.Exists(tempDirectory) && Directory.GetFiles(tempDirectory, "*.log").Length > 0);
        }
        finally
        {
            Environment.SetEnvironmentVariable("WIN11FORGE_FILE_LOG", originalValue);
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public async Task ThreadSafe_ConcurrentWrites_AllLinesPresent()
    {
        string tempDirectory = CreateTempDirectory();
        try
        {
            FileLogWriter writer = new FileLogWriter(tempDirectory);
            Task[] tasks = new Task[8];

            for (int taskIndex = 0; taskIndex < tasks.Length; taskIndex++)
            {
                int capturedTaskIndex = taskIndex;
                tasks[taskIndex] = Task.Run(() =>
                {
                    for (int lineIndex = 0; lineIndex < 100; lineIndex++)
                    {
                        writer.Write($"line {capturedTaskIndex:D2}-{lineIndex:D3}");
                    }
                });
            }

            await Task.WhenAll(tasks);

            string[] lines = File.ReadAllLines(GetTodayLogFilePath(tempDirectory));
            Assert.Equal(800, lines.Length);
            Assert.DoesNotContain(lines, string.IsNullOrWhiteSpace);
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void LogError_IncludesException()
    {
        string tempDirectory = CreateTempDirectory();
        try
        {
            FileLogWriter writer = new FileLogWriter(tempDirectory);
            LoggingService logger = new LoggingService(writer, "Cat");

            logger.LogError("boom", new InvalidOperationException("x"));

            string content = File.ReadAllText(GetTodayLogFilePath(tempDirectory));
            Assert.Contains("[ERROR]", content, StringComparison.Ordinal);
            Assert.Contains("InvalidOperationException", content, StringComparison.Ordinal);
            Assert.Contains("x", content, StringComparison.Ordinal);
        }
        finally
        {
            TryDeleteDirectory(tempDirectory);
        }
    }

    [Fact]
    public void Log_WhenWriterThrows_DoesNotThrow()
    {
        LoggingService logger = new LoggingService(new ThrowingFileLogWriter(), "Cat");

        Exception? exception = Record.Exception(() => logger.LogInfo("hello"));

        Assert.Null(exception);
    }

    private static string CreateTempDirectory()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), "Win11Forge.FileLogWriterTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        return tempDirectory;
    }

    private static string GetTodayLogFilePath(string tempDirectory)
    {
        return Path.Combine(tempDirectory, $"Win11Forge_{DateTime.Now:yyyy-MM-dd}.log");
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

    private sealed class ThrowingFileLogWriter : IFileLogWriter
    {
        public bool IsEnabled => true;

        public void Write(string line)
        {
            throw new IOException("writer failed");
        }
    }
}

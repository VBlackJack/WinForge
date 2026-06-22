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

#nullable enable

using System.Diagnostics;
using System.IO;

namespace Win11Forge.GUI.Services;

public interface IFileLogWriter
{
    void Write(string line);

    bool IsEnabled { get; }
}

public sealed class FileLogWriter : IFileLogWriter, IDisposable
{
    private readonly object _lock = new object();
    private readonly string _logsDirectory;

    public FileLogWriter(string logsDirectory)
    {
        _logsDirectory = logsDirectory;
        string? fileLogSetting = Environment.GetEnvironmentVariable("WIN11FORGE_FILE_LOG");
        IsEnabled = !string.Equals(fileLogSetting, "0", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(fileLogSetting, "false", StringComparison.OrdinalIgnoreCase);
    }

    public bool IsEnabled { get; }

    public void Write(string line)
    {
        if (!IsEnabled)
        {
            return;
        }

        try
        {
            lock (_lock)
            {
                Directory.CreateDirectory(_logsDirectory);
                string filePath = Path.Combine(_logsDirectory, $"Win11Forge_{DateTime.Now:yyyy-MM-dd}.log");
                File.AppendAllText(filePath, line + Environment.NewLine);
            }
        }
        catch (Exception ex)
        {
            // Intentional Debug.WriteLine: last-resort trace for file-writer failures (cannot self-log).
            Debug.WriteLine($"[FileLogWriter] {ex.Message}");
        }
    }

    public void Dispose()
    {
    }
}

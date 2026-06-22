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

using Win11Forge.GUI.Services;

namespace Win11Forge.GUI.Tests;

internal sealed class TestFileDialogService : IFileDialogService
{
    private readonly Queue<string?> _openResults = new();
    private readonly Queue<string?> _saveResults = new();

    public List<FileDialogOptions> OpenOptions { get; } = [];

    public List<FileDialogOptions> SaveOptions { get; } = [];

    public void QueueOpenResult(string? filePath)
    {
        _openResults.Enqueue(filePath);
    }

    public void QueueSaveResult(string? filePath)
    {
        _saveResults.Enqueue(filePath);
    }

    public Task<string?> ShowOpenAsync(FileDialogOptions options)
    {
        OpenOptions.Add(options);
        return Task.FromResult(_openResults.Count > 0 ? _openResults.Dequeue() : null);
    }

    public Task<string?> ShowSaveAsync(FileDialogOptions options)
    {
        SaveOptions.Add(options);
        return Task.FromResult(_saveResults.Count > 0 ? _saveResults.Dequeue() : null);
    }
}

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

namespace WinForge.GUI.Services;

/// <summary>
/// Manual reset gate used to pause and resume batch application operations.
/// </summary>
public sealed class PauseGate : IPauseGate, IDisposable
{
    private readonly ManualResetEventSlim _gate = new(initialState: true);
    private bool _disposed;

    /// <inheritdoc/>
    public void Pause() => _gate.Reset();

    /// <inheritdoc/>
    public void Resume() => _gate.Set();

    /// <inheritdoc/>
    public void Wait(CancellationToken cancellationToken = default)
    {
        _gate.Wait(cancellationToken);
    }

    /// <inheritdoc/>
    public Task WaitAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() => _gate.Wait(cancellationToken), cancellationToken);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;

        _gate.Dispose();
        _disposed = true;
    }
}

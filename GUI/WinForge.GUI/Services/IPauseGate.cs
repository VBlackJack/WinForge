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
/// Coordinates pause and resume state for batch application operations.
/// </summary>
public interface IPauseGate
{
    /// <summary>
    /// Blocks subsequent waiters until resumed.
    /// </summary>
    void Pause();

    /// <summary>
    /// Releases waiters and allows operations to continue.
    /// </summary>
    void Resume();

    /// <summary>
    /// Waits synchronously until the gate is resumed.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the wait.</param>
    void Wait(CancellationToken cancellationToken = default);

    /// <summary>
    /// Waits asynchronously until the gate is resumed.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the wait.</param>
    Task WaitAsync(CancellationToken cancellationToken = default);
}

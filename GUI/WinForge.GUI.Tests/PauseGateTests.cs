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

using WinForge.GUI.Services;

namespace WinForge.GUI.Tests;

public class PauseGateTests
{
    [Fact]
    public async Task WaitAsync_WhenNotPaused_ShouldCompleteImmediately()
    {
        using PauseGate pauseGate = new PauseGate();

        await pauseGate.WaitAsync().WaitAsync(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task WaitAsync_WhenPaused_ShouldBlockUntilResumed()
    {
        using PauseGate pauseGate = new PauseGate();
        pauseGate.Pause();

        Task waitTask = pauseGate.WaitAsync();
        await Task.Delay(75);

        Assert.False(waitTask.IsCompleted);

        pauseGate.Resume();
        await waitTask.WaitAsync(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task WaitAsync_WhenCancelled_ShouldPropagateCancellation()
    {
        using PauseGate pauseGate = new PauseGate();
        using CancellationTokenSource cts = new CancellationTokenSource();
        pauseGate.Pause();

        Task waitTask = pauseGate.WaitAsync(cts.Token);
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waitTask);
    }
}

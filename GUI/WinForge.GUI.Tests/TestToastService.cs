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

/// <summary>
/// Test double for IToastService that records all toast invocations without rendering anything.
/// </summary>
internal sealed class TestToastService : IToastService
{
    public List<(string Message, ToastLevel Level)> Toasts { get; } = [];

    public void Show(string message, ToastLevel level = ToastLevel.Info, int durationMs = 3000)
        => Toasts.Add((message, level));

    public void ShowWithAction(string message, string actionText, Action action, ToastLevel level = ToastLevel.Info)
        => Toasts.Add((message, level));

    public void ShowSuccess(string message) => Toasts.Add((message, ToastLevel.Success));

    public void ShowError(string message) => Toasts.Add((message, ToastLevel.Error));

    public void ShowWarning(string message) => Toasts.Add((message, ToastLevel.Warning));

    public void ShowInfo(string message) => Toasts.Add((message, ToastLevel.Info));
}

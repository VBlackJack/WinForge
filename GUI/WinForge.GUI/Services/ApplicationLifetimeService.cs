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

using System.Windows;

namespace WinForge.GUI.Services;

/// <summary>
/// WPF implementation of application lifetime operations.
/// </summary>
public sealed class ApplicationLifetimeService : IApplicationLifetimeService
{
    /// <inheritdoc/>
    public void RequestShutdown(int exitCode = 0)
    {
        // No null-conditional: this service is resolved after App.OnStartup.
        // A null Application.Current here signals a structural runtime bug.
        Application.Current.Shutdown(exitCode);
    }
}

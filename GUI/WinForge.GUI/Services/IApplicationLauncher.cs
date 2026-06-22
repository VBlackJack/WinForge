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

using WinForge.GUI.Models;

namespace WinForge.GUI.Services;

/// <summary>
/// Launches installed applications through the shell.
/// </summary>
public interface IApplicationLauncher
{
    /// <summary>
    /// Attempts to launch the specified application.
    /// </summary>
    /// <param name="app">Application to launch.</param>
    /// <returns>True when a launch strategy succeeds; otherwise false.</returns>
    Task<bool> LaunchApplicationAsync(ApplicationModel app);
}

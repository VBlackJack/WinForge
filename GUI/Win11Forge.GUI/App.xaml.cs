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
using Win11Forge.GUI.Services;

namespace Win11Forge.GUI;

/// <summary>
/// Application entry point.
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// Called on application startup.
    /// Loads and applies persisted user settings (theme, language).
    /// </summary>
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Apply saved settings (theme + language) before showing UI
        AppSettingsService.ApplyStartupSettings();
    }
}

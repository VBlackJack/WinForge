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
using System.Windows;
using Win11Forge.GUI.Services;

namespace Win11Forge.GUI;

/// <summary>
/// Application entry point.
/// </summary>
public partial class App : Application
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
        "Win11Forge_startup.log");

    /// <summary>
    /// Called on application startup.
    /// Loads and applies persisted user settings (theme, language).
    /// </summary>
    protected override void OnStartup(StartupEventArgs e)
    {
        // Global exception handlers
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            LogError("UnhandledException", args.ExceptionObject as Exception);
        DispatcherUnhandledException += (s, args) =>
        {
            LogError("DispatcherUnhandledException", args.Exception);
            args.Handled = true;
        };
        TaskScheduler.UnobservedTaskException += (s, args) =>
            LogError("UnobservedTaskException", args.Exception);

        try
        {
            Log("App.OnStartup starting...");
            base.OnStartup(e);

            Log("Applying startup settings...");
            AppSettingsService.ApplyStartupSettings();

            Log("Startup complete.");
        }
        catch (Exception ex)
        {
            LogError("OnStartup", ex);
            throw;
        }
    }

    private static void Log(string message)
    {
        try
        {
            File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] {message}\n");
        }
        catch { }
    }

    private static void LogError(string context, Exception? ex)
    {
        try
        {
            File.AppendAllText(LogPath,
                $"[{DateTime.Now:HH:mm:ss}] ERROR in {context}:\n{ex}\n\n");
        }
        catch { }
    }
}

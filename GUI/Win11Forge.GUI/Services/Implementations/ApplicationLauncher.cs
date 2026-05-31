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

using System.Diagnostics;
using System.IO;
using Win11Forge.GUI.Helpers;
using Win11Forge.GUI.Models;
using Win11Forge.GUI.Services.PowerShell;

namespace Win11Forge.GUI.Services.Implementations;

/// <summary>
/// Launches installed applications through known Windows shell locations.
/// </summary>
public class ApplicationLauncher : IApplicationLauncher
{
    private readonly IApplicationCacheService _cacheService;

    /// <summary>
    /// Initializes a new instance of the ApplicationLauncher class.
    /// </summary>
    public ApplicationLauncher(IApplicationCacheService cacheService)
    {
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
    }

    /// <inheritdoc/>
    public async Task<bool> LaunchApplicationAsync(ApplicationModel app)
    {
        await _cacheService.EnsureApplicationsCacheAsync();

        return await Task.Run(() =>
        {
            try
            {
                string? executableName = null;

                if (_cacheService.TryGetApplicationData(app.AppId, out var appData))
                {
                    executableName = JsonHelper.GetJsonString(appData, "Executable");
                }

                // Strategy 1: Use executable name if available
                if (!string.IsNullOrEmpty(executableName))
                {
                    try
                    {
                        // Process.Start returns null if UseShellExecute=true and shell handled it
                        // No exception means shell accepted the request
                        using var process = Process.Start(new ProcessStartInfo
                        {
                            FileName = executableName,
                            UseShellExecute = true
                        });
                        // Dispose handle - the GUI app continues running independently
                        // Using declaration ensures disposal even though process may be null
                        return true;
                    }
                    catch
                    {
                        // Continue to next strategy
                    }
                }

                var searchTerms = new List<string> { app.Name };

                if (!string.IsNullOrEmpty(app.AppId))
                {
                    var idParts = app.AppId.Split('.');
                    searchTerms.AddRange(idParts.Where(p => p.Length > 2));
                }

                // Strategy 2: Try to find in Start Menu
                var startMenuPaths = new[]
                {
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
                    Environment.GetFolderPath(Environment.SpecialFolder.StartMenu)
                };

                foreach (var startMenuPath in startMenuPaths)
                {
                    var programsPath = Path.Combine(startMenuPath, "Programs");
                    if (!Directory.Exists(programsPath))
                        continue;

                    try
                    {
                        var shortcuts = Directory.GetFiles(programsPath, "*.lnk", SearchOption.AllDirectories);

                        var exactMatch = shortcuts.FirstOrDefault(s =>
                            Path.GetFileNameWithoutExtension(s)
                                .Equals(app.Name, StringComparison.OrdinalIgnoreCase));

                        if (exactMatch != null)
                        {
                            using var process = Process.Start(new ProcessStartInfo
                            {
                                FileName = exactMatch,
                                UseShellExecute = true
                            });
                            return true;
                        }

                        foreach (var term in searchTerms.Distinct())
                        {
                            var matchingShortcut = shortcuts.FirstOrDefault(s =>
                            {
                                var shortcutName = Path.GetFileNameWithoutExtension(s);
                                return shortcutName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                                       term.Contains(shortcutName, StringComparison.OrdinalIgnoreCase);
                            });

                            if (matchingShortcut != null)
                            {
                                using var process = Process.Start(new ProcessStartInfo
                                {
                                    FileName = matchingShortcut,
                                    UseShellExecute = true
                                });
                                return true;
                            }
                        }
                    }
                    catch
                    {
                        // Continue searching
                    }
                }

                // Strategy 3: Search in Program Files
                var programDirs = new[]
                {
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs")
                };

                foreach (var programDir in programDirs.Where(d => Directory.Exists(d)))
                {
                    foreach (var term in searchTerms.Distinct())
                    {
                        try
                        {
                            var appFolders = Directory.GetDirectories(programDir, $"*{term}*",
                                SearchOption.TopDirectoryOnly);

                            foreach (var folder in appFolders)
                            {
                                var exeFiles = Directory.GetFiles(folder, "*.exe", SearchOption.TopDirectoryOnly);
                                var mainExe = exeFiles.FirstOrDefault(e =>
                                {
                                    var exeName = Path.GetFileNameWithoutExtension(e);
                                    return searchTerms.Any(t =>
                                        exeName.Contains(t, StringComparison.OrdinalIgnoreCase) ||
                                        t.Contains(exeName, StringComparison.OrdinalIgnoreCase));
                                }) ?? exeFiles.FirstOrDefault();

                                if (mainExe != null)
                                {
                                    using var process = Process.Start(new ProcessStartInfo
                                    {
                                        FileName = mainExe,
                                        UseShellExecute = true
                                    });
                                    return true;
                                }
                            }
                        }
                        catch
                        {
                            // Continue searching
                        }
                    }
                }

                // Strategy 4: Try common executable names in PATH
                var possibleExeNames = searchTerms
                    .SelectMany(name => new[]
                    {
                        $"{name}.exe",
                        $"{name.Replace(" ", "")}.exe",
                        $"{name.Replace(" ", "-")}.exe",
                        $"{name.ToLowerInvariant()}.exe",
                        $"{name.Replace(" ", "").ToLowerInvariant()}.exe"
                    })
                    .Distinct();

                foreach (var exeName in possibleExeNames)
                {
                    try
                    {
                        using var process = Process.Start(new ProcessStartInfo
                        {
                            FileName = exeName,
                            UseShellExecute = true
                        });
                        return true;
                    }
                    catch
                    {
                        // Continue to next
                    }
                }

                return false;
            }
            catch
            {
                return false;
            }
        });
    }
}

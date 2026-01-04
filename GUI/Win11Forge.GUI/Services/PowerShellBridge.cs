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

using System.Collections;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using Win11Forge.GUI.Models;

namespace Win11Forge.GUI.Services;

/// <summary>
/// PowerShell bridge implementation for executing Win11Forge scripts.
/// Handles path resolution from GUI executable to repository root.
/// </summary>
public class PowerShellBridge : IPowerShellBridge
{
    private readonly string _repositoryRoot;
    private Dictionary<string, JsonElement>? _applicationsCache;

    /// <summary>
    /// Initializes a new instance of the PowerShellBridge.
    /// Calculates repository root relative to executable location.
    /// </summary>
    public PowerShellBridge()
    {
        _repositoryRoot = ResolveRepositoryRootSafe();
    }

    /// <summary>
    /// Finds the PowerShell executable path.
    /// </summary>
    private static string? _powerShellPath;
    private static string GetPowerShellPath()
    {
        if (_powerShellPath != null) return _powerShellPath;

        // Try PowerShell 7 first
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (!string.IsNullOrEmpty(programFiles))
        {
            var pwshPath = Path.Combine(programFiles, "PowerShell", "7", "pwsh.exe");
            if (File.Exists(pwshPath))
            {
                _powerShellPath = pwshPath;
                return _powerShellPath;
            }
        }

        // Try Windows PowerShell
        var systemPath = Environment.GetFolderPath(Environment.SpecialFolder.System);
        if (!string.IsNullOrEmpty(systemPath))
        {
            var winPsPath = Path.Combine(systemPath, "WindowsPowerShell", "v1.0", "powershell.exe");
            if (File.Exists(winPsPath))
            {
                _powerShellPath = winPsPath;
                return _powerShellPath;
            }
        }

        // Fallback to just "pwsh" or "powershell" hoping it's in PATH
        _powerShellPath = "pwsh";
        return _powerShellPath;
    }

    /// <summary>
    /// Creates a PowerShell instance using external process.
    /// The returned wrapper uses process-based execution.
    /// </summary>
    private PowerShellProcessWrapper CreatePowerShellInstance()
    {
        return new PowerShellProcessWrapper(GetPowerShellPath(), GetSafeRepositoryRoot());
    }

    /// <summary>
    /// Executes a PowerShell script using external process.
    /// This avoids SDK issues in single-file deployments.
    /// </summary>
    private async Task<string> ExecutePowerShellScriptAsync(string script)
    {
        var psPath = GetPowerShellPath();
        var repoRoot = GetSafeRepositoryRoot();

        // Escape script for command line
        var encodedScript = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(script));

        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = psPath,
            Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {encodedScript}",
            WorkingDirectory = repoRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = new System.Diagnostics.Process { StartInfo = startInfo };
        process.Start();

        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        if (process.ExitCode != 0 && !string.IsNullOrEmpty(error))
        {
            throw new InvalidOperationException($"PowerShell error: {error}");
        }

        return output;
    }

    /// <summary>
    /// Resolves repository root with guaranteed non-null result.
    /// </summary>
    private static string ResolveRepositoryRootSafe()
    {
        try
        {
            var result = ResolveRepositoryRoot();
            if (!string.IsNullOrEmpty(result))
            {
                return result;
            }
        }
        catch
        {
            // Fall through to fallbacks
        }

        // Fallback chain - try each option until we get a non-null value
        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(processPath))
        {
            var dir = Path.GetDirectoryName(processPath);
            if (!string.IsNullOrEmpty(dir))
            {
                return dir;
            }
        }

        var currentDir = Environment.CurrentDirectory;
        if (!string.IsNullOrEmpty(currentDir))
        {
            return currentDir;
        }

        var baseDir = AppContext.BaseDirectory;
        if (!string.IsNullOrEmpty(baseDir))
        {
            return baseDir;
        }

        // Ultimate fallback - temp folder (always exists)
        return Path.GetTempPath();
    }

    /// <inheritdoc/>
    public string RepositoryRoot => _repositoryRoot;

    /// <summary>
    /// Gets the repository root, throwing if not properly initialized.
    /// </summary>
    private string GetSafeRepositoryRoot()
    {
        if (string.IsNullOrEmpty(_repositoryRoot))
        {
            throw new InvalidOperationException(
                $"Repository root is not initialized. " +
                $"ProcessPath={Environment.ProcessPath}, " +
                $"BaseDirectory={AppContext.BaseDirectory}, " +
                $"CurrentDirectory={Environment.CurrentDirectory}");
        }
        return _repositoryRoot;
    }

    /// <inheritdoc/>
    public async Task<string> GetWin11ForgeVersionAsync()
    {
        var repoRoot = GetSafeRepositoryRoot();
        var versionFilePath = Path.Combine(repoRoot, "Config", "version.json");

        if (!File.Exists(versionFilePath))
        {
            return "Unknown";
        }

        try
        {
            var jsonContent = await File.ReadAllTextAsync(versionFilePath);
            using var document = JsonDocument.Parse(jsonContent);

            if (document.RootElement.TryGetProperty("Version", out var versionElement))
            {
                return versionElement.GetString() ?? "Unknown";
            }

            return "Unknown";
        }
        catch (Exception)
        {
            return "Error";
        }
    }

    /// <inheritdoc/>
    public async Task<List<string>> GetAvailableProfilesAsync()
    {
        var repoRoot = GetSafeRepositoryRoot();
        var profilesDir = Path.Combine(repoRoot, "Profiles");

        if (!Directory.Exists(profilesDir))
        {
            return [];
        }

        return await Task.Run(() =>
        {
            var profiles = Directory.GetFiles(profilesDir, "*.json")
                .Select(f => Path.GetFileNameWithoutExtension(f))
                .OrderBy(name => name)
                .ToList();

            return profiles;
        });
    }

    /// <inheritdoc/>
    public async Task<DeploymentProfileModel> LoadProfileAsync(string profileName)
    {
        // Ensure applications database is loaded
        await EnsureApplicationsCacheAsync();

        var repoRoot = GetSafeRepositoryRoot();
        var profilesDir = Path.Combine(repoRoot, "Profiles");

        // Load profile directly from JSON (no PowerShell needed)
        return await LoadProfileFromJsonAsync(profileName, profilesDir, new List<string>());
    }

    /// <summary>
    /// Loads a profile from JSON file with inheritance support.
    /// </summary>
    private async Task<DeploymentProfileModel> LoadProfileFromJsonAsync(
        string profileName,
        string profilesDir,
        List<string> inheritanceChain)
    {
        var profilePath = Path.Combine(profilesDir, $"{profileName}.json");

        if (!File.Exists(profilePath))
        {
            throw new FileNotFoundException($"Profile not found: {profileName}");
        }

        var jsonContent = await File.ReadAllTextAsync(profilePath);
        using var document = JsonDocument.Parse(jsonContent);
        var root = document.RootElement;

        var profile = new DeploymentProfileModel
        {
            Name = GetJsonString(root, "Name") ?? profileName,
            Description = GetJsonString(root, "Description") ?? string.Empty,
            Version = GetJsonString(root, "Version") ?? "1.0.0"
        };

        // Track inheritance chain
        inheritanceChain.Add(profileName);

        // Handle inheritance
        var allAppIds = new List<string>();

        if (root.TryGetProperty("Inherits", out var inheritsElement) &&
            inheritsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var parentName in inheritsElement.EnumerateArray())
            {
                var parentNameStr = parentName.GetString();
                if (!string.IsNullOrEmpty(parentNameStr) && !inheritanceChain.Contains(parentNameStr))
                {
                    profile.InheritedFrom.Add(parentNameStr);

                    // Load parent profile and merge apps
                    var parentProfile = await LoadProfileFromJsonAsync(parentNameStr, profilesDir, inheritanceChain);
                    foreach (var app in parentProfile.Applications)
                    {
                        if (!allAppIds.Contains(app.AppId))
                        {
                            allAppIds.Add(app.AppId);
                        }
                    }
                }
            }
        }

        // Add this profile's applications
        if (root.TryGetProperty("Applications", out var appsElement) &&
            appsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var appElement in appsElement.EnumerateArray())
            {
                string? appId = null;

                if (appElement.ValueKind == JsonValueKind.String)
                {
                    appId = appElement.GetString();
                }
                else if (appElement.ValueKind == JsonValueKind.Object)
                {
                    appId = GetJsonString(appElement, "AppId");
                }

                if (!string.IsNullOrEmpty(appId) && !allAppIds.Contains(appId))
                {
                    allAppIds.Add(appId);
                }
            }
        }

        // Convert app IDs to ApplicationModels
        profile.Applications = new ObservableCollection<ApplicationModel>(
            allAppIds.Select(appId => CreateApplicationModel(appId))
                     .OrderBy(a => a.Priority)
        );

        return profile;
    }

    /// <summary>
    /// Creates an ApplicationModel from an app ID using the applications cache.
    /// </summary>
    private ApplicationModel CreateApplicationModel(string appId)
    {
        var app = new ApplicationModel
        {
            AppId = appId,
            Name = appId,
            Category = "Unknown",
            Priority = 50,
            IsRequired = false,
            Status = ApplicationStatus.Pending,
            IsSelected = true
        };

        // Enrich from applications database
        if (_applicationsCache != null && _applicationsCache.TryGetValue(appId, out var appData))
        {
            app.Name = GetJsonString(appData, "Name") ?? appId;
            app.Category = GetJsonString(appData, "Category") ?? "Unknown";
            app.Description = GetJsonString(appData, "Description") ?? string.Empty;

            if (appData.TryGetProperty("DefaultPriority", out var priorityProp) &&
                priorityProp.ValueKind == JsonValueKind.Number)
            {
                app.Priority = priorityProp.GetInt32();
            }

            if (appData.TryGetProperty("DefaultRequired", out var requiredProp) &&
                requiredProp.ValueKind == JsonValueKind.True)
            {
                app.IsRequired = true;
            }
        }

        return app;
    }

    /// <inheritdoc/>
    public async Task<InstallResult> InstallApplicationAsync(
        ApplicationModel app,
        bool isDryRun,
        Action<string>? progressCallback = null)
    {
        // Handle dry run mode
        if (isDryRun)
        {
            progressCallback?.Invoke($"[DRY RUN] Simulating installation of {app.Name}...");
            await Task.Delay(500); // Simulate some work
            return InstallResult.DryRun(app.Name);
        }

        var repoRoot = GetSafeRepositoryRoot();
        var corePath = Path.Combine(repoRoot, "Core", "Core.psm1");
        var dbModulePath = Path.Combine(repoRoot, "Modules", "ApplicationDatabase.psm1");
        var enginePath = Path.Combine(repoRoot, "Modules", "InstallationEngine.psm1");

        return await Task.Run(() =>
        {
            var logBuilder = new System.Text.StringBuilder();

            try
            {
                // Create isolated PowerShell instance for thread safety
                using var ps = CreatePowerShellInstance();

                // Capture output streams
                ps.Streams.Information.DataAdded += (s, e) =>
                {
                    var msg = ps.Streams.Information[e.Index]?.ToString() ?? string.Empty;
                    logBuilder.AppendLine(msg);
                    progressCallback?.Invoke(msg);
                };

                ps.Streams.Warning.DataAdded += (s, e) =>
                {
                    var msg = $"WARNING: {ps.Streams.Warning[e.Index]}";
                    logBuilder.AppendLine(msg);
                    progressCallback?.Invoke(msg);
                };

                ps.Streams.Verbose.DataAdded += (s, e) =>
                {
                    var msg = ps.Streams.Verbose[e.Index]?.ToString() ?? string.Empty;
                    logBuilder.AppendLine(msg);
                };

                // Set execution policy
                progressCallback?.Invoke($"Preparing to install {app.Name}...");
                ps.AddCommand("Set-ExecutionPolicy")
                  .AddParameter("ExecutionPolicy", "Bypass")
                  .AddParameter("Scope", "Process")
                  .AddParameter("Force");
                ps.Invoke();
                ps.Commands.Clear();

                // Import required modules
                progressCallback?.Invoke("Loading modules...");
                ps.AddScript($@"
                    Import-Module '{corePath}' -Force -ErrorAction SilentlyContinue
                    Import-Module '{dbModulePath}' -Force
                    Import-Module '{enginePath}' -Force
                ");
                ps.Invoke();
                ps.Commands.Clear();

                if (ps.HadErrors)
                {
                    var moduleErrors = string.Join(Environment.NewLine,
                        ps.Streams.Error.Select(e => e.ToString()));
                    logBuilder.AppendLine($"Module import errors: {moduleErrors}");
                    return InstallResult.Failed($"Failed to load modules", logBuilder.ToString());
                }

                // Get application object from database
                progressCallback?.Invoke($"Loading application: {app.AppId}...");
                ps.AddCommand("Get-ApplicationById")
                  .AddParameter("AppId", app.AppId);

                var appResults = ps.Invoke();
                ps.Commands.Clear();

                if (appResults.Count == 0)
                {
                    return InstallResult.Failed(
                        $"Application '{app.AppId}' not found in database",
                        logBuilder.ToString());
                }

                var appObject = appResults[0];

                // Call Install-Application
                progressCallback?.Invoke($"Installing {app.Name}...");
                ps.AddCommand("Install-Application")
                  .AddParameter("Application", appObject);

                var installResults = ps.Invoke();

                // Capture any remaining output
                foreach (var result in installResults)
                {
                    logBuilder.AppendLine(result?.ToString() ?? string.Empty);
                }

                // Check for errors
                if (ps.HadErrors)
                {
                    var errors = string.Join(Environment.NewLine,
                        ps.Streams.Error.Select(e => e.ToString()));
                    logBuilder.AppendLine($"Errors: {errors}");
                    return InstallResult.Failed(errors, logBuilder.ToString());
                }

                // Parse result hashtable
                if (installResults.Count > 0 && installResults[0]?.BaseObject is Hashtable resultHt)
                {
                    var success = resultHt["Success"] is bool s && s;
                    var message = resultHt["Message"]?.ToString() ?? string.Empty;
                    var method = resultHt["Method"]?.ToString() ?? string.Empty;
                    var alreadyInstalled = resultHt["AlreadyInstalled"] is bool ai && ai;

                    logBuilder.AppendLine($"Result: Success={success}, Method={method}, Message={message}");

                    if (success)
                    {
                        return InstallResult.Successful(message, logBuilder.ToString(), method, alreadyInstalled);
                    }
                    else
                    {
                        return InstallResult.Failed(message, logBuilder.ToString());
                    }
                }

                // Default success if no explicit result
                return InstallResult.Successful(
                    $"Installation completed for {app.Name}",
                    logBuilder.ToString());
            }
            catch (Exception ex)
            {
                logBuilder.AppendLine($"Exception: {ex.Message}");
                logBuilder.AppendLine(ex.StackTrace);
                return InstallResult.Failed(ex.Message, logBuilder.ToString());
            }
        });
    }

    /// <summary>
    /// Converts a PowerShell result (Hashtable/PSObject) to DeploymentProfileModel.
    /// </summary>
    private DeploymentProfileModel ConvertToDeploymentProfile(PSObject? psObject)
    {
        if (psObject?.BaseObject is not Hashtable profileData)
        {
            throw new InvalidOperationException("Profile data is not a valid Hashtable");
        }

        var profile = new DeploymentProfileModel
        {
            Name = GetStringValue(profileData, "Name"),
            Description = GetStringValue(profileData, "Description"),
            Version = GetStringValue(profileData, "Version")
        };

        // Get inherited profiles list
        if (profileData["InheritedFrom"] is object[] inheritedArray)
        {
            profile.InheritedFrom = inheritedArray.Select(i => i?.ToString() ?? string.Empty).ToList();
        }

        // Convert applications
        if (profileData["Applications"] is object[] applicationsArray)
        {
            profile.Applications = new ObservableCollection<ApplicationModel>(
                applicationsArray
                    .Select(ConvertToApplicationModel)
                    .Where(a => a != null)
                    .Cast<ApplicationModel>()
                    .OrderBy(a => a.Priority)
            );
        }

        return profile;
    }

    /// <summary>
    /// Converts a PowerShell application object to ApplicationModel.
    /// Enriches with data from the applications database.
    /// </summary>
    private ApplicationModel? ConvertToApplicationModel(object? appObj)
    {
        if (appObj == null) return null;

        string? appId = null;
        int priority = 50;
        bool isRequired = false;

        // Handle different input formats
        if (appObj is PSObject psApp)
        {
            appId = psApp.Properties["AppId"]?.Value?.ToString();
            if (psApp.Properties["Priority"]?.Value is int p) priority = p;
            if (psApp.Properties["Required"]?.Value is bool r) isRequired = r;
        }
        else if (appObj is Hashtable htApp)
        {
            appId = htApp["AppId"]?.ToString();
            if (htApp["Priority"] is int p) priority = p;
            if (htApp["Required"] is bool r) isRequired = r;
        }
        else if (appObj is string strAppId)
        {
            appId = strAppId;
        }

        if (string.IsNullOrEmpty(appId)) return null;

        // Enrich from applications database
        var app = new ApplicationModel
        {
            AppId = appId,
            Priority = priority,
            IsRequired = isRequired,
            Status = ApplicationStatus.Pending,
            IsSelected = true
        };

        // Look up additional info from database
        if (_applicationsCache != null && _applicationsCache.TryGetValue(appId, out var appData))
        {
            app.Name = GetJsonString(appData, "Name") ?? appId;
            app.Category = GetJsonString(appData, "Category") ?? "Unknown";
            app.Description = GetJsonString(appData, "Description") ?? string.Empty;

            if (appData.TryGetProperty("DefaultPriority", out var defaultPriority) &&
                priority == 50)
            {
                app.Priority = defaultPriority.GetInt32();
            }

            if (appData.TryGetProperty("DefaultRequired", out var defaultRequired))
            {
                app.IsRequired = defaultRequired.GetBoolean();
            }
        }
        else
        {
            app.Name = appId;
            app.Category = "Unknown";
        }

        return app;
    }

    /// <summary>
    /// Ensures the applications database is loaded into cache.
    /// </summary>
    private async Task EnsureApplicationsCacheAsync()
    {
        if (_applicationsCache != null) return;

        var repoRoot = GetSafeRepositoryRoot();
        var dbPath = Path.Combine(repoRoot, "Apps", "Database", "applications.json");

        if (!File.Exists(dbPath))
        {
            _applicationsCache = new Dictionary<string, JsonElement>();
            return;
        }

        var jsonContent = await File.ReadAllTextAsync(dbPath);
        using var document = JsonDocument.Parse(jsonContent);

        _applicationsCache = new Dictionary<string, JsonElement>();

        if (document.RootElement.TryGetProperty("Applications", out var apps))
        {
            foreach (var app in apps.EnumerateObject())
            {
                _applicationsCache[app.Name] = app.Value.Clone();
            }
        }
    }

    /// <summary>
    /// Safely gets a string value from a Hashtable.
    /// </summary>
    private static string GetStringValue(Hashtable ht, string key)
    {
        return ht[key]?.ToString() ?? string.Empty;
    }

    /// <summary>
    /// Safely gets a string from a JsonElement.
    /// </summary>
    private static string? GetJsonString(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop) &&
            prop.ValueKind == JsonValueKind.String)
        {
            return prop.GetString();
        }
        return null;
    }

    /// <summary>
    /// Executes a PowerShell script file from the repository.
    /// </summary>
    public async Task<string> ExecuteScriptAsync(string relativePath)
    {
        var repoRoot = GetSafeRepositoryRoot();
        var scriptPath = Path.Combine(repoRoot, relativePath);

        if (!File.Exists(scriptPath))
        {
            throw new FileNotFoundException($"Script not found: {scriptPath}");
        }

        return await Task.Run(() =>
        {
            using var ps = CreatePowerShellInstance();
            ps.AddCommand("Set-ExecutionPolicy")
              .AddParameter("ExecutionPolicy", "Bypass")
              .AddParameter("Scope", "Process")
              .AddParameter("Force");
            ps.Invoke();
            ps.Commands.Clear();

            ps.AddScript($"& '{scriptPath}'");
            var results = ps.Invoke();

            if (ps.HadErrors)
            {
                var errors = string.Join(Environment.NewLine,
                    ps.Streams.Error.Select(e => e.ToString()));
                throw new InvalidOperationException($"PowerShell error: {errors}");
            }

            return string.Join(Environment.NewLine, results.Select(r => r?.ToString() ?? string.Empty));
        });
    }

    /// <summary>
    /// Executes a PowerShell command string.
    /// </summary>
    public async Task<string> ExecuteCommandAsync(string command)
    {
        var repoRoot = GetSafeRepositoryRoot();
        return await Task.Run(() =>
        {
            using var ps = CreatePowerShellInstance();

            ps.AddScript($"Set-Location '{repoRoot}'");
            ps.Invoke();
            ps.Commands.Clear();

            ps.AddScript(command);
            var results = ps.Invoke();

            if (ps.HadErrors)
            {
                var errors = string.Join(Environment.NewLine,
                    ps.Streams.Error.Select(e => e.ToString()));
                throw new InvalidOperationException($"PowerShell error: {errors}");
            }

            return string.Join(Environment.NewLine, results.Select(r => r?.ToString() ?? string.Empty));
        });
    }

    /// <inheritdoc/>
    public async Task<List<ApplicationModel>> GetAllApplicationsAsync()
    {
        await EnsureApplicationsCacheAsync();

        if (_applicationsCache == null || _applicationsCache.Count == 0)
        {
            return [];
        }

        var applications = new List<ApplicationModel>();

        foreach (var kvp in _applicationsCache)
        {
            var appId = kvp.Key;
            var appData = kvp.Value;

            var app = new ApplicationModel
            {
                AppId = appId,
                Name = GetJsonString(appData, "Name") ?? appId,
                Category = GetJsonString(appData, "Category") ?? "Unknown",
                Description = GetJsonString(appData, "Description") ?? string.Empty,
                Status = ApplicationStatus.Pending,
                IsSelected = false
            };

            // Get priority from DefaultPriority
            if (appData.TryGetProperty("DefaultPriority", out var priorityProp) &&
                priorityProp.ValueKind == JsonValueKind.Number)
            {
                app.Priority = priorityProp.GetInt32();
            }

            // Get required from DefaultRequired
            if (appData.TryGetProperty("DefaultRequired", out var requiredProp) &&
                (requiredProp.ValueKind == JsonValueKind.True || requiredProp.ValueKind == JsonValueKind.False))
            {
                app.IsRequired = requiredProp.GetBoolean();
            }

            // Build sources list
            var sources = new List<string>();
            if (appData.TryGetProperty("WingetId", out var winget) &&
                winget.ValueKind == JsonValueKind.String &&
                !string.IsNullOrEmpty(winget.GetString()))
            {
                sources.Add("Winget");
            }
            if (appData.TryGetProperty("ChocolateyId", out var choco) &&
                choco.ValueKind == JsonValueKind.String &&
                !string.IsNullOrEmpty(choco.GetString()))
            {
                sources.Add("Chocolatey");
            }
            if (appData.TryGetProperty("StoreId", out var store) &&
                store.ValueKind == JsonValueKind.String &&
                !string.IsNullOrEmpty(store.GetString()))
            {
                sources.Add("Store");
            }
            if (appData.TryGetProperty("DirectUrl", out var url) &&
                url.ValueKind == JsonValueKind.String &&
                !string.IsNullOrEmpty(url.GetString()))
            {
                sources.Add("Direct");
            }

            app.Sources = string.Join(", ", sources);

            applications.Add(app);
        }

        return applications.OrderBy(a => a.Name).ToList();
    }

    /// <inheritdoc/>
    public async Task<DeploymentProfileModel> GetRawProfileAsync(string profileName)
    {
        await EnsureApplicationsCacheAsync();

        var repoRoot = GetSafeRepositoryRoot();
        var modulePath = Path.Combine(repoRoot, "Modules", "ProfileManager.psm1");
        var corePath = Path.Combine(repoRoot, "Core", "Core.psm1");
        var profilesDir = Path.Combine(repoRoot, "Profiles");
        var profilePath = Path.Combine(profilesDir, $"{profileName}.json");

        return await Task.Run(() =>
        {
            using var ps = CreatePowerShellInstance();

            // Set execution policy
            ps.AddCommand("Set-ExecutionPolicy")
              .AddParameter("ExecutionPolicy", "Bypass")
              .AddParameter("Scope", "Process")
              .AddParameter("Force");
            ps.Invoke();
            ps.Commands.Clear();

            // Import modules
            ps.AddScript($"Import-Module '{corePath}' -Force -ErrorAction SilentlyContinue");
            ps.Invoke();
            ps.Commands.Clear();

            ps.AddScript($"Import-Module '{modulePath}' -Force");
            ps.Invoke();
            ps.Commands.Clear();

            // Call Import-ProfileJson to get raw profile
            ps.AddCommand("Import-ProfileJson")
              .AddParameter("Path", profilePath);

            var results = ps.Invoke();

            if (ps.HadErrors || results.Count == 0)
            {
                var errors = string.Join(Environment.NewLine,
                    ps.Streams.Error.Select(e => e.ToString()));
                throw new InvalidOperationException($"Failed to load raw profile: {errors}");
            }

            return ConvertToRawDeploymentProfile(results[0]);
        });
    }

    /// <inheritdoc/>
    public async Task<DeploymentProfileModel> GetResolvedProfileAsync(string profileName)
    {
        return await LoadProfileAsync(profileName);
    }

    /// <summary>
    /// Converts a raw PowerShell profile object to DeploymentProfileModel.
    /// Only includes applications defined in this specific profile (not inherited).
    /// </summary>
    private DeploymentProfileModel ConvertToRawDeploymentProfile(PSObject? psObject)
    {
        if (psObject == null)
        {
            throw new InvalidOperationException("Profile object is null");
        }

        var profile = new DeploymentProfileModel
        {
            Name = psObject.Properties["Name"]?.Value?.ToString() ?? string.Empty,
            Description = psObject.Properties["Description"]?.Value?.ToString() ?? string.Empty,
            Version = psObject.Properties["Version"]?.Value?.ToString() ?? string.Empty
        };

        // Get parent profile (Inherits property)
        var inherits = psObject.Properties["Inherits"]?.Value;
        if (inherits is object[] inheritArray)
        {
            profile.InheritedFrom = inheritArray.Select(i => i?.ToString() ?? string.Empty).ToList();
        }
        else if (inherits is string inheritStr && !string.IsNullOrEmpty(inheritStr))
        {
            profile.InheritedFrom = [inheritStr];
        }

        // Get applications defined in this profile only
        var applications = psObject.Properties["Applications"]?.Value;
        if (applications is object[] appArray)
        {
            var appModels = new List<ApplicationModel>();

            foreach (var appObj in appArray)
            {
                var appModel = ConvertToApplicationModel(appObj);
                if (appModel != null)
                {
                    appModels.Add(appModel);
                }
            }

            profile.Applications = new ObservableCollection<ApplicationModel>(
                appModels.OrderBy(a => a.Priority));
        }

        return profile;
    }

    /// <inheritdoc/>
    public async Task<ApplicationStatus> GetApplicationStatusAsync(string appId)
    {
        var repoRoot = GetSafeRepositoryRoot();
        var corePath = Path.Combine(repoRoot, "Core", "Core.psm1");
        var dbModulePath = Path.Combine(repoRoot, "Modules", "ApplicationDatabase.psm1");
        var enginePath = Path.Combine(repoRoot, "Modules", "InstallationEngine.psm1");

        return await Task.Run(() =>
        {
            try
            {
                using var ps = CreatePowerShellInstance();

                // Set execution policy
                ps.AddCommand("Set-ExecutionPolicy")
                  .AddParameter("ExecutionPolicy", "Bypass")
                  .AddParameter("Scope", "Process")
                  .AddParameter("Force");
                ps.Invoke();
                ps.Commands.Clear();

                // Import required modules
                ps.AddScript($@"
                    Import-Module '{corePath}' -Force -ErrorAction SilentlyContinue
                    Import-Module '{dbModulePath}' -Force
                    Import-Module '{enginePath}' -Force
                ");
                ps.Invoke();
                ps.Commands.Clear();

                if (ps.HadErrors)
                {
                    return ApplicationStatus.Pending;
                }

                // Get full application object from database
                ps.AddCommand("Get-ApplicationById")
                  .AddParameter("AppId", appId);

                var appResults = ps.Invoke();
                ps.Commands.Clear();

                if (appResults.Count == 0)
                {
                    return ApplicationStatus.Pending;
                }

                var appObject = appResults[0];

                // Call Test-ApplicationInstalled
                ps.AddCommand("Test-ApplicationInstalled")
                  .AddParameter("Application", appObject);

                var testResults = ps.Invoke();

                if (testResults.Count > 0 && testResults[0]?.BaseObject is bool isInstalled)
                {
                    return isInstalled ? ApplicationStatus.Installed : ApplicationStatus.Pending;
                }

                return ApplicationStatus.Pending;
            }
            catch
            {
                return ApplicationStatus.Pending;
            }
        });
    }

    /// <inheritdoc/>
    public async Task SaveProfileAsync(string profileName, string description, string? parentProfile, List<string> addedAppIds)
    {
        var repoRoot = GetSafeRepositoryRoot();
        var profilesDir = Path.Combine(repoRoot, "Profiles");
        var profilePath = Path.Combine(profilesDir, $"{profileName}.json");

        // Ensure profiles directory exists
        if (!Directory.Exists(profilesDir))
        {
            Directory.CreateDirectory(profilesDir);
        }

        // Get current version for the profile
        var version = await GetWin11ForgeVersionAsync();
        if (version == "Unknown" || version == "Error")
        {
            version = "2.6.0";
        }

        await Task.Run(() =>
        {
            // Build the profile JSON structure
            var profileObj = new Dictionary<string, object>
            {
                ["Name"] = profileName,
                ["Description"] = description,
                ["Version"] = version
            };

            // Add inheritance if parent is specified
            if (!string.IsNullOrEmpty(parentProfile) &&
                parentProfile != Resources.Resources.Editor_NoParent)
            {
                profileObj["Inherits"] = new[] { parentProfile };
            }

            // Add applications as string array of AppIds (database mode)
            if (addedAppIds.Count > 0)
            {
                profileObj["Applications"] = addedAppIds.ToArray();
            }
            else
            {
                profileObj["Applications"] = Array.Empty<string>();
            }

            // Serialize with indentation
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = null // Keep PascalCase
            };

            var jsonContent = JsonSerializer.Serialize(profileObj, options);

            // Write to file
            File.WriteAllText(profilePath, jsonContent);
        });
    }

    /// <inheritdoc/>
    public async Task<SystemInfoModel> GetSystemInfoAsync()
    {
        return await Task.Run(() =>
        {
            var info = new SystemInfoModel
            {
                Hostname = Environment.MachineName,
                Username = Environment.UserName,
                ProcessorCount = Environment.ProcessorCount
            };

            try
            {
                using var ps = CreatePowerShellInstance();

                // Set execution policy
                ps.AddCommand("Set-ExecutionPolicy")
                  .AddParameter("ExecutionPolicy", "Bypass")
                  .AddParameter("Scope", "Process")
                  .AddParameter("Force");
                ps.Invoke();
                ps.Commands.Clear();

                // Get Windows version info
                ps.AddScript(@"
                    $os = Get-CimInstance Win32_OperatingSystem
                    @{
                        Caption = $os.Caption
                        BuildNumber = $os.BuildNumber
                        Version = $os.Version
                    }
                ");
                var osResult = ps.Invoke();
                ps.Commands.Clear();

                if (osResult.Count > 0 && osResult[0]?.BaseObject is System.Collections.Hashtable osHt)
                {
                    info.WindowsVersion = osHt["Caption"]?.ToString() ?? "Windows";
                    info.WindowsBuild = osHt["Version"]?.ToString() ?? osHt["BuildNumber"]?.ToString() ?? "Unknown";
                }

                // Get total memory
                ps.AddScript("(Get-CimInstance Win32_ComputerSystem).TotalPhysicalMemory");
                var memResult = ps.Invoke();
                ps.Commands.Clear();

                if (memResult.Count > 0 && memResult[0]?.BaseObject is ulong totalMem)
                {
                    info.TotalMemoryGB = Math.Round(totalMem / 1024.0 / 1024.0 / 1024.0, 1);
                }

                // Check if running as administrator
                ps.AddScript(@"
                    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
                    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
                    $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
                ");
                var adminResult = ps.Invoke();
                ps.Commands.Clear();

                if (adminResult.Count > 0 && adminResult[0]?.BaseObject is bool isAdmin)
                {
                    info.IsAdministrator = isAdmin;
                }

                // Check Winget availability
                ps.AddScript(@"
                    try {
                        $ver = winget --version 2>$null
                        if ($ver) { $ver.Trim() } else { '' }
                    } catch { '' }
                ");
                var wingetResult = ps.Invoke();
                ps.Commands.Clear();

                if (wingetResult.Count > 0)
                {
                    var wingetVer = wingetResult[0]?.ToString()?.Trim() ?? string.Empty;
                    info.WingetAvailable = !string.IsNullOrEmpty(wingetVer);
                    info.WingetVersion = wingetVer;
                }

                // Check Chocolatey availability
                ps.AddScript(@"
                    try {
                        $ver = choco --version 2>$null
                        if ($ver) { $ver.Trim() } else { '' }
                    } catch { '' }
                ");
                var chocoResult = ps.Invoke();
                ps.Commands.Clear();

                if (chocoResult.Count > 0)
                {
                    var chocoVer = chocoResult[0]?.ToString()?.Trim() ?? string.Empty;
                    info.ChocolateyAvailable = !string.IsNullOrEmpty(chocoVer);
                    info.ChocolateyVersion = chocoVer;
                }
            }
            catch
            {
                // Return partial info on error
            }

            return info;
        });
    }

    /// <inheritdoc/>
    public async Task<PrerequisitesStatus> CheckPrerequisitesAsync()
    {
        return await Task.Run(() =>
        {
            var status = new PrerequisitesStatus();

            try
            {
                using var ps = CreatePowerShellInstance();

                // Check PowerShell 7
                ps.AddScript(@"
                    try {
                        $ver = pwsh --version 2>$null
                        if ($ver) { $ver.Trim() } else { '' }
                    } catch { '' }
                ");
                var ps7Result = ps.Invoke();
                ps.Commands.Clear();

                if (ps7Result.Count > 0)
                {
                    var version = ps7Result[0]?.ToString()?.Trim() ?? string.Empty;
                    status.PowerShell7Installed = !string.IsNullOrEmpty(version);
                    status.PowerShellVersion = version;
                }

                // Check Winget
                ps.AddScript(@"
                    try {
                        $null = winget --version 2>$null
                        $LASTEXITCODE -eq 0
                    } catch { $false }
                ");
                var wingetResult = ps.Invoke();
                ps.Commands.Clear();

                if (wingetResult.Count > 0)
                {
                    status.WingetInstalled = wingetResult[0]?.ToString()?.Trim().ToLower() == "true";
                }

                // Check Chocolatey
                ps.AddScript(@"
                    try {
                        $null = choco --version 2>$null
                        $LASTEXITCODE -eq 0
                    } catch { $false }
                ");
                var chocoResult = ps.Invoke();
                ps.Commands.Clear();

                if (chocoResult.Count > 0)
                {
                    status.ChocolateyInstalled = chocoResult[0]?.ToString()?.Trim().ToLower() == "true";
                }
            }
            catch
            {
                // Return default status on error
            }

            return status;
        });
    }

    /// <inheritdoc/>
    public async Task<bool> InstallPrerequisitesAsync(Action<string>? progressCallback = null)
    {
        var repoRoot = GetSafeRepositoryRoot();
        var prerequisitesModule = Path.Combine(repoRoot, "Modules", "Prerequisites.psm1");
        var corePath = Path.Combine(repoRoot, "Core", "Core.psm1");

        return await Task.Run(() =>
        {
            try
            {
                progressCallback?.Invoke(Resources.Resources.Prerequisites_Starting);

                // Create a temporary script to run with elevation
                var tempScript = Path.Combine(Path.GetTempPath(), $"Win11Forge_Prerequisites_{Guid.NewGuid():N}.ps1");
                var scriptContent = $@"
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass -Force
$ErrorActionPreference = 'Stop'

try {{
    Import-Module '{corePath.Replace("'", "''")}' -Force -ErrorAction SilentlyContinue
    Import-Module '{prerequisitesModule.Replace("'", "''")}' -Force

    Start-PrerequisitesInstallation

    Write-Host ''
    Write-Host 'Prerequisites installation complete. Press any key to close...' -ForegroundColor Green
    $null = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown')
}} catch {{
    Write-Host ""Error: $($_.Exception.Message)"" -ForegroundColor Red
    Write-Host ''
    Write-Host 'Press any key to close...' -ForegroundColor Yellow
    $null = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown')
    exit 1
}}
";
                File.WriteAllText(tempScript, scriptContent);

                progressCallback?.Invoke(Resources.Resources.Prerequisites_Installing);

                // Find PowerShell executable (prefer pwsh.exe, fall back to powershell.exe)
                var pwshPath = FindPowerShellExecutable();

                // Launch elevated PowerShell process
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = pwshPath,
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{tempScript}\"",
                    UseShellExecute = true,
                    Verb = "runas", // Request UAC elevation
                    WorkingDirectory = repoRoot
                };

                using var process = System.Diagnostics.Process.Start(psi);
                if (process == null)
                {
                    progressCallback?.Invoke("Failed to start elevated process");
                    return false;
                }

                process.WaitForExit();

                // Clean up temp script
                try { File.Delete(tempScript); } catch { /* Ignore cleanup errors */ }

                if (process.ExitCode == 0)
                {
                    progressCallback?.Invoke(Resources.Resources.Prerequisites_Complete);
                    return true;
                }
                else
                {
                    progressCallback?.Invoke($"Installation failed (exit code: {process.ExitCode})");
                    return false;
                }
            }
            catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                // User cancelled UAC prompt
                progressCallback?.Invoke("Installation cancelled by user");
                return false;
            }
            catch (Exception ex)
            {
                progressCallback?.Invoke($"Exception: {ex.Message}");
                return false;
            }
        });
    }

    /// <summary>
    /// Finds the PowerShell executable path (pwsh.exe or powershell.exe).
    /// </summary>
    private static string FindPowerShellExecutable()
    {
        // Check for pwsh.exe in common locations
        var pwshPaths = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PowerShell", "7", "pwsh.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "PowerShell", "7", "pwsh.exe"),
            @"C:\Program Files\PowerShell\7\pwsh.exe"
        };

        foreach (var path in pwshPaths)
        {
            if (File.Exists(path))
                return path;
        }

        // Fall back to Windows PowerShell
        return "powershell.exe";
    }

    /// <summary>
    /// Resolves the repository root path from the executable location.
    /// Walks up directories looking for repository markers (Config/version.json, Modules/).
    /// </summary>
    private static string ResolveRepositoryRoot()
    {
        // Build list of candidate paths, filtering out nulls
        var candidatePaths = new List<string>();

        // Priority 1: ProcessPath directory (best for single-file apps)
        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(processPath))
        {
            var dir = Path.GetDirectoryName(processPath);
            if (!string.IsNullOrEmpty(dir))
            {
                candidatePaths.Add(dir);
            }
        }

        // Priority 2: BaseDirectory
        var baseDir = AppContext.BaseDirectory;
        if (!string.IsNullOrEmpty(baseDir))
        {
            candidatePaths.Add(baseDir);
        }

        // Priority 3: CurrentDirectory
        var currentDir = Environment.CurrentDirectory;
        if (!string.IsNullOrEmpty(currentDir))
        {
            candidatePaths.Add(currentDir);
        }

        foreach (var basePath in candidatePaths)
        {
            var result = TryFindRepositoryRoot(basePath);
            if (!string.IsNullOrEmpty(result))
            {
                return result;
            }
        }

        throw new DirectoryNotFoundException(
            $"Could not locate Win11Forge repository root. Searched from: {string.Join(", ", candidatePaths)}");
    }

    /// <summary>
    /// Attempts to find repository root by walking up from a base path.
    /// </summary>
    private static string? TryFindRepositoryRoot(string basePath)
    {
        var currentDir = new DirectoryInfo(basePath);

        while (currentDir != null)
        {
            // Check for Config/version.json
            var versionFile = Path.Combine(currentDir.FullName, "Config", "version.json");
            if (File.Exists(versionFile))
            {
                return currentDir.FullName;
            }

            // Check for Modules/InstallationEngine.psm1
            var modulesDir = Path.Combine(currentDir.FullName, "Modules");
            if (Directory.Exists(modulesDir))
            {
                var coreModule = Path.Combine(modulesDir, "InstallationEngine.psm1");
                if (File.Exists(coreModule))
                {
                    return currentDir.FullName;
                }
            }

            currentDir = currentDir.Parent;
        }

        return null;
    }
}

/// <summary>
/// Wrapper that mimics PowerShell SDK API but uses external process execution.
/// This is needed because the PowerShell SDK doesn't work in single-file deployments.
/// </summary>
internal class PowerShellProcessWrapper : IDisposable
{
    private readonly string _psPath;
    private readonly string _workingDir;
    private readonly List<string> _scripts = new();
    private readonly List<string> _errors = new();
    private readonly List<object> _results = new();
    private bool _hadErrors;

    public PowerShellProcessWrapper(string psPath, string workingDir)
    {
        _psPath = psPath;
        _workingDir = workingDir;
    }

    public bool HadErrors => _hadErrors;
    public PowerShellStreams Streams => new(_errors);

    public PowerShellProcessWrapper AddCommand(string command)
    {
        _scripts.Add(command);
        return this;
    }

    public PowerShellProcessWrapper AddParameter(string name, object? value = null)
    {
        if (_scripts.Count > 0)
        {
            var lastScript = _scripts[^1];
            if (value != null)
            {
                var valueStr = value.ToString()?.Replace("'", "''") ?? "";
                _scripts[^1] = $"{lastScript} -{name} '{valueStr}'";
            }
            else
            {
                // Switch parameter (no value)
                _scripts[^1] = $"{lastScript} -{name}";
            }
        }
        return this;
    }

    public PowerShellProcessWrapper AddScript(string script)
    {
        _scripts.Add(script);
        return this;
    }

    public System.Collections.ObjectModel.Collection<PSObject> Invoke()
    {
        var result = new System.Collections.ObjectModel.Collection<PSObject>();

        if (_scripts.Count == 0)
            return result;

        var fullScript = string.Join("; ", _scripts);
        var encodedScript = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(fullScript));

        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = _psPath,
            Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {encodedScript}",
            WorkingDirectory = _workingDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        try
        {
            using var process = new System.Diagnostics.Process { StartInfo = startInfo };
            process.Start();

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();

            process.WaitForExit();

            if (!string.IsNullOrEmpty(error))
            {
                _errors.Add(error);
                _hadErrors = true;
            }

            if (!string.IsNullOrEmpty(output))
            {
                foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    result.Add(new PSObject(line.Trim()));
                }
            }
        }
        catch (Exception ex)
        {
            _errors.Add(ex.Message);
            _hadErrors = true;
        }

        return result;
    }

    public void Clear()
    {
        _scripts.Clear();
    }

    public PowerShellCommands Commands => new(this);

    public void Dispose()
    {
        _scripts.Clear();
        _errors.Clear();
    }
}

/// <summary>
/// Wrapper for PowerShell streams.
/// </summary>
internal class PowerShellStreams
{
    private readonly List<string> _errors;

    public PowerShellStreams(List<string> errors)
    {
        _errors = errors;
    }

    public IEnumerable<PowerShellErrorRecord> Error => _errors.Select(e => new PowerShellErrorRecord(e));
    public PowerShellDataCollection Information => new();
    public PowerShellDataCollection Warning => new();
    public PowerShellDataCollection Verbose => new();
}

/// <summary>
/// Wrapper for error records.
/// </summary>
internal class PowerShellErrorRecord
{
    private readonly string _message;

    public PowerShellErrorRecord(string message)
    {
        _message = message;
    }

    public override string ToString() => _message;
}

/// <summary>
/// Wrapper for data collection with DataAdded event.
/// </summary>
internal class PowerShellDataCollection
{
    private readonly List<string> _items = new();

    public event EventHandler<PowerShellDataAddedEventArgs>? DataAdded;

    public string? this[int index] => index >= 0 && index < _items.Count ? _items[index] : null;

    public void Add(string item)
    {
        _items.Add(item);
        DataAdded?.Invoke(this, new PowerShellDataAddedEventArgs { Index = _items.Count - 1 });
    }
}

/// <summary>
/// Event args for data added.
/// </summary>
internal class PowerShellDataAddedEventArgs : EventArgs
{
    public int Index { get; set; }
}

/// <summary>
/// Wrapper for commands collection.
/// </summary>
internal class PowerShellCommands
{
    private readonly PowerShellProcessWrapper _wrapper;

    public PowerShellCommands(PowerShellProcessWrapper wrapper)
    {
        _wrapper = wrapper;
    }

    public void Clear() => _wrapper.Clear();
}

/// <summary>
/// Simple PSObject replacement for process-based execution.
/// </summary>
internal class PSObject
{
    public object? BaseObject { get; }

    public PSObject(object? baseObject = null)
    {
        BaseObject = baseObject;
    }

    public PSObjectProperties Properties => new();

    public override string? ToString() => BaseObject?.ToString();
}

/// <summary>
/// Properties collection for PSObject.
/// </summary>
internal class PSObjectProperties
{
    public PSObjectProperty? this[string name] => null;
}

/// <summary>
/// Property for PSObject.
/// </summary>
internal class PSObjectProperty
{
    public object? Value { get; set; }
}

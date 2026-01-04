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
using System.Management.Automation;
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
        _repositoryRoot = ResolveRepositoryRoot();
    }

    /// <inheritdoc/>
    public string RepositoryRoot => _repositoryRoot;

    /// <inheritdoc/>
    public async Task<string> GetWin11ForgeVersionAsync()
    {
        var versionFilePath = Path.Combine(_repositoryRoot, "Config", "version.json");

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
        var profilesDir = Path.Combine(_repositoryRoot, "Profiles");

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

        var modulePath = Path.Combine(_repositoryRoot, "Modules", "ProfileManager.psm1");
        var corePath = Path.Combine(_repositoryRoot, "Core", "Core.psm1");
        var profilesDir = Path.Combine(_repositoryRoot, "Profiles");

        return await Task.Run(() =>
        {
            using var ps = PowerShell.Create();

            // Set execution policy for this process
            ps.AddCommand("Set-ExecutionPolicy")
              .AddParameter("ExecutionPolicy", "Bypass")
              .AddParameter("Scope", "Process")
              .AddParameter("Force");
            ps.Invoke();
            ps.Commands.Clear();

            // Import Core module first (provides Write-Status)
            ps.AddScript($"Import-Module '{corePath}' -Force -ErrorAction SilentlyContinue");
            ps.Invoke();
            ps.Commands.Clear();

            // Import ProfileManager module
            ps.AddScript($"Import-Module '{modulePath}' -Force");
            ps.Invoke();
            ps.Commands.Clear();

            // Call Get-DeploymentProfile
            ps.AddCommand("Get-DeploymentProfile")
              .AddParameter("ProfileName", profileName)
              .AddParameter("ProfilesDirectory", profilesDir);

            var results = ps.Invoke();

            if (ps.HadErrors)
            {
                var errors = string.Join(Environment.NewLine,
                    ps.Streams.Error.Select(e => e.ToString()));
                throw new InvalidOperationException($"Failed to load profile: {errors}");
            }

            if (results.Count == 0)
            {
                throw new InvalidOperationException($"Profile '{profileName}' returned no data");
            }

            // Convert PSObject/Hashtable to our model
            var psResult = results[0];
            return ConvertToDeploymentProfile(psResult);
        });
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

        var corePath = Path.Combine(_repositoryRoot, "Core", "Core.psm1");
        var dbModulePath = Path.Combine(_repositoryRoot, "Modules", "ApplicationDatabase.psm1");
        var enginePath = Path.Combine(_repositoryRoot, "Modules", "InstallationEngine.psm1");

        return await Task.Run(() =>
        {
            var logBuilder = new System.Text.StringBuilder();

            try
            {
                // Create isolated PowerShell instance for thread safety
                using var ps = PowerShell.Create();

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

        var dbPath = Path.Combine(_repositoryRoot, "Apps", "Database", "applications.json");

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
        var scriptPath = Path.Combine(_repositoryRoot, relativePath);

        if (!File.Exists(scriptPath))
        {
            throw new FileNotFoundException($"Script not found: {scriptPath}");
        }

        return await Task.Run(() =>
        {
            using var ps = PowerShell.Create();
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
        return await Task.Run(() =>
        {
            using var ps = PowerShell.Create();

            ps.AddScript($"Set-Location '{_repositoryRoot}'");
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
                requiredProp.ValueKind == JsonValueKind.True || requiredProp.ValueKind == JsonValueKind.False)
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

        var modulePath = Path.Combine(_repositoryRoot, "Modules", "ProfileManager.psm1");
        var corePath = Path.Combine(_repositoryRoot, "Core", "Core.psm1");
        var profilesDir = Path.Combine(_repositoryRoot, "Profiles");
        var profilePath = Path.Combine(profilesDir, $"{profileName}.json");

        return await Task.Run(() =>
        {
            using var ps = PowerShell.Create();

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
        var corePath = Path.Combine(_repositoryRoot, "Core", "Core.psm1");
        var dbModulePath = Path.Combine(_repositoryRoot, "Modules", "ApplicationDatabase.psm1");
        var enginePath = Path.Combine(_repositoryRoot, "Modules", "InstallationEngine.psm1");

        return await Task.Run(() =>
        {
            try
            {
                using var ps = PowerShell.Create();

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
        var profilesDir = Path.Combine(_repositoryRoot, "Profiles");
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
                using var ps = PowerShell.Create();

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

    /// <summary>
    /// Resolves the repository root path from the executable location.
    /// Walks up directories looking for repository markers (Config/version.json, Modules/).
    /// </summary>
    private static string ResolveRepositoryRoot()
    {
        var exeDirectory = AppContext.BaseDirectory;
        var currentDir = new DirectoryInfo(exeDirectory);

        while (currentDir != null)
        {
            var versionFile = Path.Combine(currentDir.FullName, "Config", "version.json");
            if (File.Exists(versionFile))
            {
                return currentDir.FullName;
            }

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

        var fallbackPath = Path.GetFullPath(Path.Combine(exeDirectory, "..", "..", "..", "..", ".."));

        if (Directory.Exists(fallbackPath))
        {
            return fallbackPath;
        }

        throw new DirectoryNotFoundException(
            $"Could not locate Win11Forge repository root from {exeDirectory}");
    }
}

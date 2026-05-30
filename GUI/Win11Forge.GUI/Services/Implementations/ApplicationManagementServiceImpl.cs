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
using System.Text;
using System.Text.Json;
using Win11Forge.GUI.Helpers;
using Win11Forge.GUI.Models;
using Win11Forge.GUI.Services.PowerShell;

namespace Win11Forge.GUI.Services.Implementations;

/// <summary>
/// Implementation of IApplicationManagementService for application lifecycle operations.
/// </summary>
public class ApplicationManagementServiceImpl : IApplicationManagementService
{
    private readonly IRepositoryPathService _pathService;
    private readonly IPowerShellExecutionService _executionService;
    private readonly IApplicationCacheService _cacheService;
    private readonly IApplicationDetectionService _detectionService;

    /// <summary>
    /// Initializes a new instance of the ApplicationManagementServiceImpl.
    /// </summary>
    public ApplicationManagementServiceImpl(
        IRepositoryPathService pathService,
        IPowerShellExecutionService executionService,
        IApplicationCacheService cacheService,
        IApplicationDetectionService detectionService)
    {
        _pathService = pathService ?? throw new ArgumentNullException(nameof(pathService));
        _executionService = executionService ?? throw new ArgumentNullException(nameof(executionService));
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        _detectionService = detectionService ?? throw new ArgumentNullException(nameof(detectionService));
    }

    /// <inheritdoc/>
    public async Task<List<ApplicationModel>> GetAllApplicationsAsync()
    {
        await _cacheService.EnsureApplicationsCacheAsync();

        var cache = _cacheService.ApplicationsCache;
        if (cache == null || cache.Count == 0)
        {
            return [];
        }

        var applications = new List<ApplicationModel>();

        foreach (var kvp in cache)
        {
            var appId = kvp.Key;
            var appData = kvp.Value;

            var app = new ApplicationModel
            {
                AppId = appId,
                Name = JsonHelper.GetJsonString(appData, "Name") ?? appId,
                Category = JsonHelper.GetJsonString(appData, "Category") ?? "Unknown",
                Description = JsonHelper.GetJsonString(appData, "Description") ?? string.Empty,
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

            // Build sources list from Sources object
            var sourcesList = new List<string>();
            if (appData.TryGetProperty("Sources", out var sourcesObj) &&
                sourcesObj.ValueKind == JsonValueKind.Object)
            {
                if (sourcesObj.TryGetProperty("Winget", out var winget) &&
                    winget.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrEmpty(winget.GetString()))
                {
                    sourcesList.Add("Winget");
                }
                if (sourcesObj.TryGetProperty("Chocolatey", out var choco) &&
                    choco.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrEmpty(choco.GetString()))
                {
                    sourcesList.Add("Chocolatey");
                }
                if (sourcesObj.TryGetProperty("Store", out var store) &&
                    store.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrEmpty(store.GetString()))
                {
                    sourcesList.Add("Store");
                }
                if (sourcesObj.TryGetProperty("DirectUrl", out var url) &&
                    url.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrEmpty(url.GetString()))
                {
                    sourcesList.Add("Direct");
                }
            }

            app.Sources = string.Join(", ", sourcesList);

            // Get ManualInstallOnly flag
            if (appData.TryGetProperty("ManualInstallOnly", out var manualProp) &&
                manualProp.ValueKind == JsonValueKind.True)
            {
                app.ManualInstallOnly = true;
            }

            // Get official URL (Homepage field)
            app.OfficialUrl = JsonHelper.GetJsonString(appData, "Homepage") ?? string.Empty;

            // Get install notes
            app.InstallNotes = JsonHelper.GetJsonString(appData, "InstallNotes") ?? string.Empty;

            applications.Add(app);
        }

        return applications.OrderBy(a => a.Name).ToList();
    }

    /// <inheritdoc/>
    public async Task<ApplicationStatus> GetApplicationStatusAsync(string appId)
    {
        var corePath = _pathService.GetPathForPowerShell("Core", "Core.psm1");
        var dbModulePath = _pathService.GetPathForPowerShell("Modules", "ApplicationDatabase.psm1");
        var enginePath = _pathService.GetPathForPowerShell("Modules", "InstallationEngine.psm1");
        var detectionPath = _pathService.GetPathForPowerShell("Modules", "ApplicationDetection.psm1");

        try
        {
            var escapedAppId = PowerShellValidation.EscapeForPowerShell(appId);

            var script = $@"
Set-ExecutionPolicy -Scope Process -ExecutionPolicy RemoteSigned -Force
$ErrorActionPreference = 'SilentlyContinue'

try {{
    Import-Module '{corePath}' -Force -ErrorAction SilentlyContinue
    Import-Module '{dbModulePath}' -Force -ErrorAction Stop
    Import-Module '{enginePath}' -Force -ErrorAction Stop
    Import-Module '{detectionPath}' -Force -ErrorAction Stop

    $app = Get-ApplicationById -AppId '{escapedAppId}'
    if (-not $app) {{
        Write-Output 'NOT_FOUND'
        exit
    }}

    $isInstalled = Test-ApplicationInstalled -Application $app
    if ($isInstalled) {{
        Write-Output 'INSTALLED'
    }} else {{
        Write-Output 'NOT_INSTALLED'
    }}
}} catch {{
    Write-Output 'ERROR'
}}
";

            var output = await _executionService.ExecutePowerShellScriptAsync(script);
            var result = output.Trim().Split('\n').LastOrDefault()?.Trim() ?? string.Empty;

            return result switch
            {
                "INSTALLED" => ApplicationStatus.Installed,
                "NOT_INSTALLED" => ApplicationStatus.Pending,
                _ => ApplicationStatus.Pending
            };
        }
        catch
        {
            return ApplicationStatus.Pending;
        }
    }

    /// <inheritdoc/>
    public async Task<Dictionary<string, BatchAppStatus>?> GetBatchApplicationStatusAsync(IReadOnlyList<ApplicationModel> apps)
    {
        if (apps == null || apps.Count == 0)
        {
            return new Dictionary<string, BatchAppStatus>();
        }

        // Try fast detection first
        var fastResult = await GetBatchApplicationStatusFastAsync(apps);
        if (fastResult != null)
        {
            return fastResult;
        }

        // Fall back to PowerShell detection
        return await GetBatchApplicationStatusPowerShellAsync(apps);
    }

    /// <summary>
    /// Gets batch application status using optimized registry-first detection.
    /// </summary>
    private async Task<Dictionary<string, BatchAppStatus>?> GetBatchApplicationStatusFastAsync(IReadOnlyList<ApplicationModel> apps)
    {
        try
        {
            var detectionResult = await _detectionService.GetInstalledPackagesAsync();
            var result = new Dictionary<string, BatchAppStatus>(StringComparer.OrdinalIgnoreCase);

            foreach (var app in apps)
            {
                var appId = app.AppId;
                var packageInfo = FindDetectedPackage(app, detectionResult);

                if (packageInfo != null)
                {
                    result[appId] = new BatchAppStatus(
                        ApplicationStatus.Installed,
                        packageInfo.InstalledVersion);
                }
                else
                {
                    result[appId] = new BatchAppStatus(ApplicationStatus.Pending, null);
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Fast batch detection failed: {ex.Message}");
            return null;
        }
    }

    private static InstalledPackageInfo? FindDetectedPackage(
        ApplicationModel app,
        BatchDetectionResult detectionResult)
    {
        if (!string.IsNullOrEmpty(app.AppId))
        {
            var packageInfo = detectionResult.GetPackage(app.AppId);
            if (packageInfo != null)
            {
                return packageInfo;
            }
        }

        if (!string.IsNullOrEmpty(app.Name))
        {
            var normalizedName = NormalizePackageLookupKey(app.Name);
            var packageInfo = detectionResult.GetPackage(normalizedName);
            if (packageInfo != null)
            {
                return packageInfo;
            }
        }

        foreach (var packageInfo in detectionResult.Packages.Values.DistinctBy(p => $"{p.Id}|{p.Name}"))
        {
            if (IsPackageMatch(app, packageInfo))
            {
                return packageInfo;
            }
        }

        return null;
    }

    private static bool IsPackageMatch(ApplicationModel app, InstalledPackageInfo packageInfo)
    {
        if (string.IsNullOrWhiteSpace(app.Name))
        {
            return false;
        }

        foreach (var candidate in new[] { packageInfo.Name, packageInfo.Id }.Where(static c => !string.IsNullOrWhiteSpace(c)))
        {
            var normalizedAppName = NormalizePackageLookupKey(app.Name);
            var normalizedCandidate = NormalizePackageLookupKey(candidate);

            if (normalizedAppName.Length >= 4 &&
                normalizedCandidate.Length >= 4 &&
                (normalizedCandidate.Contains(normalizedAppName, StringComparison.OrdinalIgnoreCase) ||
                 normalizedAppName.Contains(normalizedCandidate, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            if (HasMeaningfulTokenOverlap(app.Name, candidate))
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizePackageLookupKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(char.ToLowerInvariant(ch));
            }
        }

        return builder.ToString();
    }

    private static bool HasMeaningfulTokenOverlap(string appName, string packageName)
    {
        var appTokens = GetMeaningfulTokens(appName);
        if (appTokens.Count == 0)
        {
            return false;
        }

        var packageTokens = GetMeaningfulTokens(packageName);
        return packageTokens.Any(appTokens.Contains);
    }

    private static HashSet<string> GetMeaningfulTokens(string value)
    {
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var builder = new StringBuilder();

        foreach (var ch in value)
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(char.ToLowerInvariant(ch));
            }
            else
            {
                AddToken(builder, tokens);
            }
        }

        AddToken(builder, tokens);
        return tokens;
    }

    private static void AddToken(StringBuilder builder, HashSet<string> tokens)
    {
        if (builder.Length == 0)
        {
            return;
        }

        var token = builder.ToString();
        builder.Clear();

        if (token.Length < 4 || CommonPackageMatchTokens.Contains(token))
        {
            return;
        }

        tokens.Add(token);
    }

    private static readonly HashSet<string> CommonPackageMatchTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "app",
        "apps",
        "client",
        "desktop",
        "shell",
        "studio",
        "tool",
        "tools"
    };

    /// <summary>
    /// Gets batch application status using PowerShell detection.
    /// </summary>
    private async Task<Dictionary<string, BatchAppStatus>?> GetBatchApplicationStatusPowerShellAsync(IReadOnlyList<ApplicationModel> apps)
    {
        var corePath = _pathService.GetPathForPowerShell("Core", "Core.psm1");
        var dbModulePath = _pathService.GetPathForPowerShell("Modules", "ApplicationDatabase.psm1");
        var enginePath = _pathService.GetPathForPowerShell("Modules", "InstallationEngine.psm1");
        var detectionPath = _pathService.GetPathForPowerShell("Modules", "ApplicationDetection.psm1");

        try
        {
            var appIds = apps.Select(a => a.AppId).ToList();
            var appIdsJson = PowerShellValidation.EscapeForPowerShell(JsonSerializer.Serialize(appIds));

            var script = $@"
Set-ExecutionPolicy -Scope Process -ExecutionPolicy RemoteSigned -Force
$ErrorActionPreference = 'SilentlyContinue'

try {{
    Import-Module '{corePath}' -Force -ErrorAction SilentlyContinue
    Import-Module '{dbModulePath}' -Force -ErrorAction Stop
    Import-Module '{enginePath}' -Force -ErrorAction Stop
    Import-Module '{detectionPath}' -Force -ErrorAction Stop

    $appIds = '{appIdsJson}' | ConvertFrom-Json
    $apps = @()
    foreach ($appId in $appIds) {{
        $app = Get-ApplicationById -AppId $appId
        if ($app) {{
            $apps += $app
        }}
    }}

    $results = Get-ApplicationsInstallationStatus -Applications $apps
    $results | ConvertTo-Json -Compress
}} catch {{
    Write-Output ""___BATCH_ERROR___: $($_.Exception.Message)""
}}
";

            var output = await _executionService.ExecutePowerShellScriptAsync(script);
            var lines = output.Trim().Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                if (line.Contains("___BATCH_ERROR___"))
                {
                    return null;
                }
            }

            foreach (var line in lines.Reverse())
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("{") && trimmed.EndsWith("}"))
                {
                    using var doc = JsonDocument.Parse(trimmed);
                    var root = doc.RootElement;

                    var result = new Dictionary<string, BatchAppStatus>();
                    foreach (var prop in root.EnumerateObject())
                    {
                        var appId = prop.Name;
                        var isInstalled = false;
                        string? version = null;

                        if (prop.Value.ValueKind == JsonValueKind.Object)
                        {
                            if (prop.Value.TryGetProperty("IsInstalled", out var installedProp))
                            {
                                isInstalled = installedProp.ValueKind == JsonValueKind.True;
                            }
                            if (prop.Value.TryGetProperty("Version", out var versionProp) &&
                                versionProp.ValueKind == JsonValueKind.String)
                            {
                                version = versionProp.GetString();
                            }
                        }
                        else if (prop.Value.ValueKind == JsonValueKind.True ||
                                 prop.Value.ValueKind == JsonValueKind.False)
                        {
                            isInstalled = prop.Value.GetBoolean();
                        }

                        var status = isInstalled ? ApplicationStatus.Installed : ApplicationStatus.Pending;
                        result[appId] = new BatchAppStatus(status, version);
                    }

                    return result;
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<InstallResult> InstallApplicationAsync(
        ApplicationModel app,
        bool isDryRun,
        bool forceUpdate = false,
        Action<string>? progressCallback = null)
    {
        // Handle dry run mode
        if (isDryRun)
        {
            progressCallback?.Invoke($"{Resources.Resources.Common_DryRun} Simulating installation of {app.Name}...");
            await Task.Delay(500);
            return InstallResult.DryRun(app.Name);
        }

        var corePath = _pathService.GetPathForPowerShell("Core", "Core.psm1");
        var dbModulePath = _pathService.GetPathForPowerShell("Modules", "ApplicationDatabase.psm1");
        var enginePath = _pathService.GetPathForPowerShell("Modules", "InstallationEngine.psm1");
        var orchestratorPath = _pathService.GetPathForPowerShell("Modules", "InstallationOrchestrator.psm1");

        return await Task.Run(async () =>
        {
            var logBuilder = new StringBuilder();
            var outputLines = new List<string>();

            try
            {
                progressCallback?.Invoke($"Preparing to install {app.Name}...");

                var forceUpdateSwitch = forceUpdate ? " -ForceUpdate" : "";
                var validatedAppId = PowerShellValidation.ValidateAppId(app.AppId);
                var escapedAppId = PowerShellValidation.EscapeForPowerShell(validatedAppId);

                var script = $@"
Set-ExecutionPolicy -Scope Process -ExecutionPolicy RemoteSigned -Force
$ErrorActionPreference = 'Continue'

$script:OriginalHost = $host.UI.RawUI

try {{
    Import-Module '{corePath}' -Force -ErrorAction SilentlyContinue
    Import-Module '{dbModulePath}' -Force -ErrorAction Stop
    Import-Module '{enginePath}' -Force -ErrorAction Stop
    Import-Module '{orchestratorPath}' -Force -ErrorAction Stop

    $app = Get-ApplicationById -AppId '{escapedAppId}'
    if (-not $app) {{
        Write-Output '[STATUS] Application not found in database'
        @{{ Success = $false; Message = 'Application not found in database'; Method = ''; AlreadyInstalled = $false }} | ConvertTo-Json -Compress
        exit
    }}

    Write-Output '[STATUS] Starting installation...'
    $result = Install-Application -Application $app{forceUpdateSwitch}
    $result | ConvertTo-Json -Compress
}} catch {{
    Write-Output ""[ERROR] $($_.Exception.Message)""
    @{{ Success = $false; Message = $_.Exception.Message; Method = ''; AlreadyInstalled = $false }} | ConvertTo-Json -Compress
}}
";

                progressCallback?.Invoke($"Installing {app.Name}...");

                var psPath = _executionService.GetPowerShellPath();
                var repoRoot = _pathService.GetSafeRepositoryRoot();
                var encodedScript = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));

                var startInfo = new ProcessStartInfo
                {
                    FileName = psPath,
                    Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy RemoteSigned -EncodedCommand {encodedScript}",
                    WorkingDirectory = repoRoot,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = startInfo };

                process.OutputDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        var line = e.Data.Trim();
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            outputLines.Add(line);
                            logBuilder.AppendLine(line);

                            if (line.StartsWith("[STATUS]") || line.StartsWith("[ERROR]") ||
                                line.StartsWith("[INFO]") || line.StartsWith("[SUCCESS]") ||
                                line.StartsWith("[WARNING]"))
                            {
                                progressCallback?.Invoke($"{app.Name}: {line}");
                            }
                            else if (line.Contains("Installing") || line.Contains("Downloading") ||
                                     line.Contains("Verifying") || line.Contains("Completed") ||
                                     line.Contains("already installed", StringComparison.OrdinalIgnoreCase) ||
                                     line.Contains("Successfully", StringComparison.OrdinalIgnoreCase))
                            {
                                progressCallback?.Invoke($"{app.Name}: {line}");
                            }
                        }
                    }
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                    {
                        var cleanLine = ExtractReadableMessage(e.Data);
                        if (!string.IsNullOrWhiteSpace(cleanLine))
                        {
                            logBuilder.AppendLine($"[STDERR] {cleanLine}");
                            progressCallback?.Invoke($"{app.Name}: [Error] {cleanLine}");
                        }
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                using var installTimeoutCts = new CancellationTokenSource(_executionService.InstallationTimeoutMs);
                try
                {
                    await process.WaitForExitAsync(installTimeoutCts.Token);
                }
                catch (OperationCanceledException) when (installTimeoutCts.IsCancellationRequested)
                {
                    try { process.Kill(entireProcessTree: true); } catch (Exception ex) { Debug.WriteLine($"Process kill failed (best effort): {ex.Message}"); }
                    throw new TimeoutException($"Installation of {app.Name} timed out after {_executionService.InstallationTimeoutMs / 60000} minutes");
                }

                // Find JSON line in output
                string? jsonLine = null;
                for (int i = outputLines.Count - 1; i >= 0; i--)
                {
                    var trimmed = outputLines[i].Trim();
                    if (trimmed.StartsWith("{") && trimmed.EndsWith("}"))
                    {
                        jsonLine = trimmed;
                        break;
                    }
                }

                if (!string.IsNullOrEmpty(jsonLine))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(jsonLine);
                        var root = doc.RootElement;

                        var success = root.TryGetProperty("Success", out var successProp) &&
                                      successProp.ValueKind == JsonValueKind.True;
                        var message = root.TryGetProperty("Message", out var msgProp)
                            ? msgProp.GetString() ?? string.Empty
                            : string.Empty;
                        var method = root.TryGetProperty("Method", out var methodProp)
                            ? methodProp.GetString() ?? string.Empty
                            : string.Empty;
                        var alreadyInstalled = root.TryGetProperty("AlreadyInstalled", out var aiProp) &&
                                               aiProp.ValueKind == JsonValueKind.True;

                        logBuilder.AppendLine($"Result: Success={success}, Method={method}, Message={message}");

                        if (success)
                        {
                            progressCallback?.Invoke($"Completed: {app.Name}");
                            return InstallResult.Successful(message, logBuilder.ToString(), method, alreadyInstalled);
                        }
                        else
                        {
                            progressCallback?.Invoke($"Failed: {app.Name} - {message}");
                            return InstallResult.Failed(message, logBuilder.ToString());
                        }
                    }
                    catch (JsonException)
                    {
                        return InstallResult.Failed($"Invalid response format", logBuilder.ToString());
                    }
                }

                var fullOutput = string.Join("\n", outputLines);
                if (fullOutput.Contains("successfully", StringComparison.OrdinalIgnoreCase) ||
                    fullOutput.Contains("installed", StringComparison.OrdinalIgnoreCase))
                {
                    return InstallResult.Successful(
                        $"Installation completed for {app.Name}",
                        logBuilder.ToString());
                }

                return InstallResult.Failed(
                    "Installation completed but status unknown",
                    logBuilder.ToString());
            }
            catch (Exception ex)
            {
                logBuilder.AppendLine($"Exception: {ex.Message}");
                logBuilder.AppendLine(ex.StackTrace);
                progressCallback?.Invoke($"Error: {ex.Message}");
                return InstallResult.Failed(ex.Message, logBuilder.ToString());
            }
        });
    }

    /// <inheritdoc/>
    public async Task<InstallResult> UninstallApplicationAsync(
        ApplicationModel app,
        Action<string>? progressCallback = null)
    {
        await _cacheService.EnsureApplicationsCacheAsync();

        return await Task.Run(async () =>
        {
            var logBuilder = new StringBuilder();

            try
            {
                progressCallback?.Invoke($"Preparing to uninstall {app.Name}...");
                logBuilder.AppendLine($"Uninstalling: {app.Name}");

                string? wingetId = null;
                string? chocoPackage = null;

                if (_cacheService.TryGetApplicationData(app.AppId, out var appData))
                {
                    if (appData.TryGetProperty("Sources", out var sources))
                    {
                        wingetId = JsonHelper.GetJsonString(sources, "Winget");
                        chocoPackage = JsonHelper.GetJsonString(sources, "Chocolatey");
                    }
                }

                // Try Winget first
                if (!string.IsNullOrEmpty(wingetId))
                {
                    progressCallback?.Invoke($"Uninstalling via Winget: {wingetId}");
                    logBuilder.AppendLine($"Uninstalling via Winget: {wingetId}");

                    var wingetResult = await ExecuteUninstallCommandAsync(
                        "winget",
                        $"uninstall --id \"{wingetId}\" --silent --accept-source-agreements",
                        logBuilder);

                    if (wingetResult.Success)
                    {
                        progressCallback?.Invoke($"Uninstalled: {app.Name}");
                        logBuilder.AppendLine("Winget uninstallation succeeded");
                        return InstallResult.Successful(
                            $"Successfully uninstalled {app.Name}",
                            logBuilder.ToString(),
                            "Winget",
                            alreadyInstalled: false);
                    }

                    logBuilder.AppendLine($"Winget uninstallation failed (exit code: {wingetResult.ExitCode})");
                }

                // Try Chocolatey as fallback
                if (!string.IsNullOrEmpty(chocoPackage))
                {
                    progressCallback?.Invoke($"Uninstalling via Chocolatey: {chocoPackage}");
                    logBuilder.AppendLine($"Uninstalling via Chocolatey: {chocoPackage}");

                    var chocoResult = await ExecuteUninstallCommandAsync(
                        "choco",
                        $"uninstall {chocoPackage} -y --no-progress",
                        logBuilder);

                    if (chocoResult.Success)
                    {
                        progressCallback?.Invoke($"Uninstalled: {app.Name}");
                        logBuilder.AppendLine("Chocolatey uninstallation succeeded");
                        return InstallResult.Successful(
                            $"Successfully uninstalled {app.Name}",
                            logBuilder.ToString(),
                            "Chocolatey",
                            alreadyInstalled: false);
                    }

                    logBuilder.AppendLine($"Chocolatey uninstallation failed (exit code: {chocoResult.ExitCode})");
                }

                var errorMsg = string.IsNullOrEmpty(wingetId) && string.IsNullOrEmpty(chocoPackage)
                    ? "No uninstall sources available for this application"
                    : "All uninstallation methods failed";

                progressCallback?.Invoke($"Failed: {app.Name}");
                logBuilder.AppendLine($"Uninstallation failed: {errorMsg}");
                return InstallResult.Failed(errorMsg, logBuilder.ToString());
            }
            catch (Exception ex)
            {
                logBuilder.AppendLine($"Exception: {ex.Message}");
                logBuilder.AppendLine(ex.StackTrace);
                progressCallback?.Invoke($"Error: {ex.Message}");
                return InstallResult.Failed(ex.Message, logBuilder.ToString());
            }
        });
    }

    /// <inheritdoc/>
    public async Task<UpdateCheckResult> CheckApplicationUpdateAsync(ApplicationModel app)
    {
        if (app.Status != ApplicationStatus.Installed &&
            app.Status != ApplicationStatus.AlreadyInstalled &&
            app.Status != ApplicationStatus.UpdateAvailable)
        {
            return UpdateCheckResult.UpToDate();
        }

        await _cacheService.EnsureApplicationsCacheAsync();

        return await Task.Run(async () =>
        {
            try
            {
                string? wingetId = null;

                if (_cacheService.TryGetApplicationData(app.AppId, out var appData))
                {
                    if (appData.TryGetProperty("Sources", out var sources))
                    {
                        wingetId = JsonHelper.GetJsonString(sources, "Winget");
                    }
                }

                if (string.IsNullOrEmpty(wingetId))
                {
                    return UpdateCheckResult.UpToDate();
                }

                var installedVersion = await GetInstalledVersionAsync(wingetId);
                var availableVersion = await GetRepositoryVersionAsync(wingetId);

                if (string.IsNullOrEmpty(installedVersion))
                {
                    return UpdateCheckResult.UpToDate();
                }

                if (string.IsNullOrEmpty(availableVersion))
                {
                    return UpdateCheckResult.UpToDate(installedVersion);
                }

                var comparison = VersionServiceImpl.CompareVersions(installedVersion, availableVersion);

                if (comparison < 0)
                {
                    return UpdateCheckResult.UpdateAvailable(installedVersion, availableVersion);
                }

                return UpdateCheckResult.UpToDate(installedVersion);
            }
            catch (Exception ex)
            {
                return UpdateCheckResult.Failed(ex.Message);
            }
        });
    }

    /// <inheritdoc/>
    public async Task<InstallResult> UpdateApplicationAsync(
        ApplicationModel app,
        Action<string>? progressCallback = null)
    {
        await _cacheService.EnsureApplicationsCacheAsync();

        return await Task.Run(async () =>
        {
            var logBuilder = new StringBuilder();

            try
            {
                progressCallback?.Invoke($"Preparing to update {app.Name}...");
                logBuilder.AppendLine($"Updating: {app.Name}");

                string? wingetId = null;
                string? chocoPackage = null;

                if (_cacheService.TryGetApplicationData(app.AppId, out var appData))
                {
                    if (appData.TryGetProperty("Sources", out var sources))
                    {
                        wingetId = JsonHelper.GetJsonString(sources, "Winget");
                        chocoPackage = JsonHelper.GetJsonString(sources, "Chocolatey");
                    }
                }

                // Try Winget first
                if (!string.IsNullOrEmpty(wingetId))
                {
                    progressCallback?.Invoke($"Updating via Winget: {wingetId}");
                    logBuilder.AppendLine($"Updating via Winget: {wingetId}");

                    var wingetResult = await ExecuteUpdateCommandAsync(
                        "winget",
                        $"upgrade --id \"{wingetId}\" --silent --accept-package-agreements --accept-source-agreements",
                        logBuilder);

                    if (wingetResult.Success)
                    {
                        progressCallback?.Invoke($"Updated: {app.Name}");
                        logBuilder.AppendLine("Winget update succeeded");
                        return InstallResult.Successful(
                            $"Successfully updated {app.Name}",
                            logBuilder.ToString(),
                            "Winget");
                    }

                    if (wingetResult.ExitCode == -1978335189)
                    {
                        progressCallback?.Invoke($"Already up to date: {app.Name}");
                        logBuilder.AppendLine("Application is already up to date");
                        return InstallResult.Successful(
                            $"{app.Name} is already up to date",
                            logBuilder.ToString(),
                            "Winget",
                            alreadyInstalled: true);
                    }

                    logBuilder.AppendLine($"Winget update failed (exit code: {wingetResult.ExitCode})");
                }

                // Try Chocolatey as fallback
                if (!string.IsNullOrEmpty(chocoPackage))
                {
                    progressCallback?.Invoke($"Updating via Chocolatey: {chocoPackage}");
                    logBuilder.AppendLine($"Updating via Chocolatey: {chocoPackage}");

                    var chocoResult = await ExecuteUpdateCommandAsync(
                        "choco",
                        $"upgrade {chocoPackage} -y --no-progress",
                        logBuilder);

                    if (chocoResult.Success)
                    {
                        progressCallback?.Invoke($"Updated: {app.Name}");
                        logBuilder.AppendLine("Chocolatey update succeeded");
                        return InstallResult.Successful(
                            $"Successfully updated {app.Name}",
                            logBuilder.ToString(),
                            "Chocolatey");
                    }

                    logBuilder.AppendLine($"Chocolatey update failed (exit code: {chocoResult.ExitCode})");
                }

                var errorMsg = string.IsNullOrEmpty(wingetId) && string.IsNullOrEmpty(chocoPackage)
                    ? "No update sources available for this application"
                    : "All update methods failed";

                progressCallback?.Invoke($"Failed: {app.Name}");
                logBuilder.AppendLine($"Update failed: {errorMsg}");
                return InstallResult.Failed(errorMsg, logBuilder.ToString());
            }
            catch (Exception ex)
            {
                logBuilder.AppendLine($"Exception: {ex.Message}");
                logBuilder.AppendLine(ex.StackTrace);
                progressCallback?.Invoke($"Error: {ex.Message}");
                return InstallResult.Failed(ex.Message, logBuilder.ToString());
            }
        });
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

    /// <summary>
    /// Gets the installed version of a package using winget list.
    /// </summary>
    private async Task<string> GetInstalledVersionAsync(string wingetId)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "winget",
                Arguments = $"list --id \"{wingetId}\" --exact --disable-interactivity",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            var output = await process.StandardOutput.ReadToEndAsync();
            using var timeoutCts = new CancellationTokenSource(_executionService.DefaultQueryTimeoutMs);
            try
            {
                await process.WaitForExitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                return string.Empty;
            }

            var cleanOutput = CleanWingetOutput(output);
            return VersionServiceImpl.ParseVersionFromWingetList(cleanOutput);
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Gets the available version from repository using winget show.
    /// </summary>
    private async Task<string> GetRepositoryVersionAsync(string wingetId)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "winget",
                Arguments = $"show --id \"{wingetId}\" --exact --disable-interactivity",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            var output = await process.StandardOutput.ReadToEndAsync();
            using var timeoutCts = new CancellationTokenSource(_executionService.DefaultQueryTimeoutMs);
            try
            {
                await process.WaitForExitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                return string.Empty;
            }

            var cleanOutput = CleanWingetOutput(output);
            return VersionServiceImpl.ParseVersionFromWingetShow(cleanOutput);
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Cleans winget output by removing progress spinner characters.
    /// </summary>
    private static string CleanWingetOutput(string output)
    {
        if (string.IsNullOrEmpty(output))
            return output;

        var cleanLines = new List<string>();
        var lines = output.Split('\n');

        foreach (var line in lines)
        {
            var segments = line.Split('\r');
            var lastSegment = segments
                .Select(s => s.Trim())
                .LastOrDefault(s => !string.IsNullOrEmpty(s) &&
                                    !s.All(c => c == '-' || c == '\\' || c == '|' || c == '/' || c == ' '));

            if (!string.IsNullOrEmpty(lastSegment))
            {
                cleanLines.Add(lastSegment);
            }
        }

        return string.Join("\n", cleanLines);
    }

    /// <summary>
    /// Executes an update command and returns the result.
    /// </summary>
    private async Task<(bool Success, int ExitCode, string Output)> ExecuteUpdateCommandAsync(
        string command,
        string arguments,
        StringBuilder logBuilder)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            using var updateTimeoutCts = new CancellationTokenSource(_executionService.InstallationTimeoutMs);
            try
            {
                await Task.WhenAll(outputTask, errorTask, process.WaitForExitAsync(updateTimeoutCts.Token));
            }
            catch (OperationCanceledException) when (updateTimeoutCts.IsCancellationRequested)
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                logBuilder.AppendLine($"[ERROR] Update command timed out after {_executionService.InstallationTimeoutMs / 60000} minutes");
                return (false, -1, "Update command timed out");
            }

            var output = await outputTask;
            var error = await errorTask;

            logBuilder.AppendLine(output);
            if (!string.IsNullOrEmpty(error))
            {
                logBuilder.AppendLine($"[stderr] {error}");
            }

            return (process.ExitCode == 0, process.ExitCode, output);
        }
        catch (Exception ex)
        {
            logBuilder.AppendLine($"Command execution failed: {ex.Message}");
            return (false, -1, ex.Message);
        }
    }

    /// <summary>
    /// Executes an uninstall command and returns the result.
    /// </summary>
    private async Task<(bool Success, int ExitCode, string Output)> ExecuteUninstallCommandAsync(
        string command,
        string arguments,
        StringBuilder logBuilder)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            using var uninstallTimeoutCts = new CancellationTokenSource(_executionService.InstallationTimeoutMs);
            try
            {
                await Task.WhenAll(outputTask, errorTask, process.WaitForExitAsync(uninstallTimeoutCts.Token));
            }
            catch (OperationCanceledException) when (uninstallTimeoutCts.IsCancellationRequested)
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                logBuilder.AppendLine($"[ERROR] Uninstall command timed out after {_executionService.InstallationTimeoutMs / 60000} minutes");
                return (false, -1, "Uninstall command timed out");
            }

            var output = await outputTask;
            var error = await errorTask;

            logBuilder.AppendLine(output);
            if (!string.IsNullOrEmpty(error))
            {
                logBuilder.AppendLine($"[stderr] {error}");
            }

            return (process.ExitCode == 0, process.ExitCode, output);
        }
        catch (Exception ex)
        {
            logBuilder.AppendLine($"Command execution failed: {ex.Message}");
            return (false, -1, ex.Message);
        }
    }

    /// <summary>
    /// Extracts readable messages from PowerShell output.
    /// Filters out binary content and CLIXML serialization artifacts.
    /// </summary>
    private static string ExtractReadableMessage(string line)
    {
        // Filter out binary content (non-printable characters indicate binary data)
        // Check for common binary signatures: MZ (DOS exe), PK (ZIP), etc.
        if (line.Length > 0 && (line[0] == 'M' && line.Length > 1 && line[1] == 'Z'))
        {
            return string.Empty; // DOS executable header - skip binary content
        }

        // Check for high ratio of non-printable characters (indicates binary data)
        int nonPrintable = 0;
        int checkLength = Math.Min(line.Length, 100); // Check first 100 chars
        for (int i = 0; i < checkLength; i++)
        {
            char c = line[i];
            if (c < 32 && c != '\t' && c != '\n' && c != '\r')
            {
                nonPrintable++;
            }
        }
        if (checkLength > 0 && nonPrintable > checkLength / 4) // More than 25% non-printable
        {
            return string.Empty; // Likely binary data
        }

        if (line.Contains("<Objs") || line.Contains("<ToString>"))
        {
            var messages = new List<string>();

            var toStringPattern = new System.Text.RegularExpressions.Regex(
                @"<ToString>([^<]*)</ToString>",
                System.Text.RegularExpressions.RegexOptions.Compiled);

            var matches = toStringPattern.Matches(line);
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                var message = match.Groups[1].Value;
                if (!string.IsNullOrWhiteSpace(message))
                {
                    message = message.Replace("_x000D__x000A_", "\n")
                                     .Replace("_x000A_", "\n")
                                     .Replace("&lt;", "<")
                                     .Replace("&gt;", ">")
                                     .Replace("&amp;", "&")
                                     .Trim();

                    if (!string.IsNullOrWhiteSpace(message) && !messages.Contains(message))
                    {
                        messages.Add(message);
                    }
                }
            }

            return string.Join("\n", messages);
        }

        if (line.StartsWith("<") && line.Contains(">"))
        {
            return string.Empty;
        }

        return line;
    }
}

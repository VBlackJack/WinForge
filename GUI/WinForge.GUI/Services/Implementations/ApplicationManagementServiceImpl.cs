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
using System.Text.RegularExpressions;
using WinForge.GUI.Helpers;
using WinForge.GUI.Models;
using WinForge.GUI.Services.PowerShell;

namespace WinForge.GUI.Services.Implementations;

/// <summary>
/// Implementation of IApplicationManagementService for application lifecycle operations.
/// </summary>
public class ApplicationManagementServiceImpl : IApplicationManagementService
{
    private const string PreferredUpdateSourcePropertyName = "PreferredUpdateSource";
    private const string DetectionPropertyName = "Detection";
    private const string DetectionMethodPropertyName = "Method";
    private const string DetectionCommandPropertyName = "Command";
    private const string DetectionVersionRegexPropertyName = "VersionRegex";
    private const string DetectionMethodCommand = "Command";
    private const string PrerequisiteTag = "prerequisite";
    private static readonly TimeSpan DetectionRegexTimeout = TimeSpan.FromMilliseconds(500);

    private readonly IRepositoryPathService _pathService;
    private readonly IPowerShellExecutionService _executionService;
    private readonly IApplicationCacheService _cacheService;
    private readonly IApplicationDetectionService _detectionService;
    private readonly IApplicationLauncher _launcher;
    private readonly ILoggingService _logger;

    /// <summary>
    /// Command-detection executable allowlist, loaded once on first use. Shared with
    /// the detection probe so the post-update verification path cannot launch an
    /// executable that catalog-driven detection would otherwise reject (fail-closed).
    /// </summary>
    private readonly VendorCommandRunner _commandRunner;

    /// <summary>
    /// Initializes a new instance of the ApplicationManagementServiceImpl.
    /// </summary>
    public ApplicationManagementServiceImpl(
        IRepositoryPathService pathService,
        IPowerShellExecutionService executionService,
        IApplicationCacheService cacheService,
        IApplicationDetectionService detectionService,
        IApplicationLauncher launcher,
        ILoggerFactory? loggerFactory = null)
    {
        _pathService = pathService ?? throw new ArgumentNullException(nameof(pathService));
        _executionService = executionService ?? throw new ArgumentNullException(nameof(executionService));
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        _detectionService = detectionService ?? throw new ArgumentNullException(nameof(detectionService));
        _launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
        _logger = (loggerFactory ?? new LoggerFactory()).CreateLogger<ApplicationManagementServiceImpl>();
        _commandRunner = new VendorCommandRunner(_executionService, _pathService, _logger);
    }

    /// <inheritdoc/>
    public async Task<List<ApplicationModel>> GetAllApplicationsAsync()
    {
        await _cacheService.EnsureApplicationsCacheAsync();

        IReadOnlyDictionary<string, JsonElement>? cache = _cacheService.ApplicationsCache;
        if (cache == null || cache.Count == 0)
        {
            return [];
        }

        List<ApplicationModel> applications = new List<ApplicationModel>();

        foreach (KeyValuePair<string, JsonElement> kvp in cache)
        {
            string appId = kvp.Key;
            JsonElement appData = kvp.Value;

            ApplicationModel app = new ApplicationModel
            {
                AppId = appId,
                Name = JsonHelper.GetJsonString(appData, "Name") ?? appId,
                Category = JsonHelper.GetJsonString(appData, "Category") ?? "Unknown",
                Description = JsonHelper.GetJsonString(appData, "Description") ?? string.Empty,
                Status = ApplicationStatus.Pending,
                IsSelected = false
            };

            // Get priority from DefaultPriority
            if (appData.TryGetProperty("DefaultPriority", out JsonElement priorityProp) &&
                priorityProp.ValueKind == JsonValueKind.Number)
            {
                app.Priority = priorityProp.GetInt32();
            }

            // Get required from DefaultRequired
            if (appData.TryGetProperty("DefaultRequired", out JsonElement requiredProp) &&
                (requiredProp.ValueKind == JsonValueKind.True || requiredProp.ValueKind == JsonValueKind.False))
            {
                app.IsRequired = requiredProp.GetBoolean();
            }

            app.IsPrerequisite = HasTag(appData, PrerequisiteTag);

            // Build sources list from Sources object
            List<string> sourcesList = new List<string>();
            if (appData.TryGetProperty("Sources", out JsonElement sourcesObj) &&
                sourcesObj.ValueKind == JsonValueKind.Object)
            {
                if (sourcesObj.TryGetProperty("Winget", out JsonElement winget) &&
                    winget.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrEmpty(winget.GetString()))
                {
                    sourcesList.Add("Winget");
                }
                if (sourcesObj.TryGetProperty("Chocolatey", out JsonElement choco) &&
                    choco.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrEmpty(choco.GetString()))
                {
                    sourcesList.Add("Chocolatey");
                }
                if (sourcesObj.TryGetProperty("Store", out JsonElement store) &&
                    store.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrEmpty(store.GetString()))
                {
                    sourcesList.Add("Store");
                }
                if (sourcesObj.TryGetProperty("DirectUrl", out JsonElement url) &&
                    url.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrEmpty(url.GetString()))
                {
                    sourcesList.Add("Direct");
                }
            }

            app.Sources = string.Join(", ", sourcesList);

            // Get ManualInstallOnly flag
            if (appData.TryGetProperty("ManualInstallOnly", out JsonElement manualProp) &&
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

    private static bool HasTag(JsonElement appData, string tag)
    {
        if (!appData.TryGetProperty("Tags", out JsonElement tagsElement) ||
            tagsElement.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (JsonElement tagElement in tagsElement.EnumerateArray())
        {
            if (tagElement.ValueKind == JsonValueKind.String &&
                string.Equals(tagElement.GetString(), tag, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc/>
    public async Task<ApplicationStatus> GetApplicationStatusAsync(string appId)
    {
        string corePath = _pathService.GetPathForPowerShell("Core", "Core.psm1");
        string dbModulePath = _pathService.GetPathForPowerShell("Modules", "ApplicationDatabase.psm1");
        string enginePath = _pathService.GetPathForPowerShell("Modules", "InstallationEngine.psm1");
        string detectionPath = _pathService.GetPathForPowerShell("Modules", "ApplicationDetection.psm1");

        try
        {
            string escapedAppId = PowerShellValidation.EscapeForPowerShell(appId);

            string script = $@"
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

            string output = await _executionService.ExecutePowerShellScriptAsync(script);
            string result = output.Trim().Split('\n').LastOrDefault()?.Trim() ?? string.Empty;

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
        Dictionary<string, BatchAppStatus>? fastResult = await GetBatchApplicationStatusFastAsync(apps);
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
            BatchDetectionResult detectionResult = await _detectionService.GetInstalledPackagesAsync();
            Dictionary<string, BatchAppStatus> result = new Dictionary<string, BatchAppStatus>(StringComparer.OrdinalIgnoreCase);

            foreach (ApplicationModel app in apps)
            {
                string appId = app.AppId;
                InstalledPackageInfo? packageInfo = PackageMatcher.FindDetectedPackage(app, detectionResult);

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
            _logger.LogWarning($"Fast batch detection failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Gets batch application status using PowerShell detection.
    /// </summary>
    private async Task<Dictionary<string, BatchAppStatus>?> GetBatchApplicationStatusPowerShellAsync(IReadOnlyList<ApplicationModel> apps)
    {
        string corePath = _pathService.GetPathForPowerShell("Core", "Core.psm1");
        string dbModulePath = _pathService.GetPathForPowerShell("Modules", "ApplicationDatabase.psm1");
        string enginePath = _pathService.GetPathForPowerShell("Modules", "InstallationEngine.psm1");
        string detectionPath = _pathService.GetPathForPowerShell("Modules", "ApplicationDetection.psm1");

        try
        {
            List<string> appIds = apps.Select(a => a.AppId).ToList();
            string appIdsJson = PowerShellValidation.EscapeForPowerShell(JsonSerializer.Serialize(appIds));

            string script = $@"
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

            string output = await _executionService.ExecutePowerShellScriptAsync(script);
            string[] lines = output.Trim().Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (string line in lines)
            {
                if (line.Contains("___BATCH_ERROR___"))
                {
                    return null;
                }
            }

            foreach (string? line in lines.Reverse())
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith("{") && trimmed.EndsWith("}"))
                {
                    using JsonDocument doc = JsonDocument.Parse(trimmed);
                    JsonElement root = doc.RootElement;

                    Dictionary<string, BatchAppStatus> result = new Dictionary<string, BatchAppStatus>();
                    foreach (JsonProperty prop in root.EnumerateObject())
                    {
                        string appId = prop.Name;
                        bool isInstalled = false;
                        string? version = null;

                        if (prop.Value.ValueKind == JsonValueKind.Object)
                        {
                            if (prop.Value.TryGetProperty("IsInstalled", out JsonElement installedProp))
                            {
                                isInstalled = installedProp.ValueKind == JsonValueKind.True;
                            }
                            if (prop.Value.TryGetProperty("Version", out JsonElement versionProp) &&
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

                        ApplicationStatus status = isInstalled ? ApplicationStatus.Installed : ApplicationStatus.Pending;
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
            progressCallback?.Invoke($"{Resources.Resources.Common_DryRun} {LogFormat(nameof(Resources.Resources.AppManagement_SimulatingInstall), app.Name)}");
            await Task.Delay(500);
            return InstallResult.DryRun(app.Name);
        }

        string corePath = _pathService.GetPathForPowerShell("Core", "Core.psm1");
        string dbModulePath = _pathService.GetPathForPowerShell("Modules", "ApplicationDatabase.psm1");
        string enginePath = _pathService.GetPathForPowerShell("Modules", "InstallationEngine.psm1");
        string orchestratorPath = _pathService.GetPathForPowerShell("Modules", "InstallationOrchestrator.psm1");

        return await Task.Run(async () =>
        {
            StringBuilder logBuilder = new StringBuilder();
            List<string> outputLines = new List<string>();

            try
            {
                progressCallback?.Invoke(LogFormat(nameof(Resources.Resources.AppManagement_PreparingInstall), app.Name));

                string forceUpdateSwitch = forceUpdate ? " -ForceUpdate" : "";
                string validatedAppId = PowerShellValidation.ValidateAppId(app.AppId);
                string escapedAppId = PowerShellValidation.EscapeForPowerShell(validatedAppId);

                string script = $@"
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

                progressCallback?.Invoke(LogFormat(nameof(Resources.Resources.Common_InstallingItem), app.Name));

                string psPath = _executionService.GetPowerShellPath();
                string repoRoot = _pathService.GetSafeRepositoryRoot();
                string encodedScript = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));

                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = psPath,
                    Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy RemoteSigned -EncodedCommand {encodedScript}",
                    WorkingDirectory = repoRoot,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                    CreateNoWindow = true
                };

                using Process process = new Process { StartInfo = startInfo };
                bool rawOutputOmitted = false;
                bool rawErrorOutputOmitted = false;

                process.OutputDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        string line = e.Data.Trim();
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            bool isWinForgeLine =
                                line.StartsWith("[STATUS]", StringComparison.Ordinal) ||
                                line.StartsWith("[ERROR]", StringComparison.Ordinal) ||
                                line.StartsWith("[INFO]", StringComparison.Ordinal) ||
                                line.StartsWith("[SUCCESS]", StringComparison.Ordinal) ||
                                line.StartsWith("[WARNING]", StringComparison.Ordinal);
                            bool isJsonResult = line.StartsWith("{", StringComparison.Ordinal) &&
                                line.EndsWith("}", StringComparison.Ordinal);

                            if (isWinForgeLine || isJsonResult)
                            {
                                outputLines.Add(line);
                                logBuilder.AppendLine(line);
                            }

                            if (isWinForgeLine)
                            {
                                progressCallback?.Invoke($"{app.Name}: {line}");
                            }
                            else if (!isJsonResult)
                            {
                                // Vendor tools localize stdout according to Windows display language.
                                // Keep raw vendor text out of the main deployment log.
                                if (!rawOutputOmitted)
                                {
                                    rawOutputOmitted = true;
                                    logBuilder.AppendLine("Raw process output omitted from main log");
                                }
                            }
                        }
                    }
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                    {
                        if (!rawErrorOutputOmitted)
                        {
                            rawErrorOutputOmitted = true;
                            logBuilder.AppendLine("[STDERR] Raw process error output omitted from main log");
                            progressCallback?.Invoke($"{app.Name}: {GetLogResource(nameof(Resources.Resources.AppManagement_ProcessErrorReceived))}");
                        }
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                using CancellationTokenSource installTimeoutCts = new CancellationTokenSource(_executionService.InstallationTimeoutMs);
                try
                {
                    await process.WaitForExitAsync(installTimeoutCts.Token);
                }
                catch (OperationCanceledException) when (installTimeoutCts.IsCancellationRequested)
                {
                    try { process.Kill(entireProcessTree: true); } catch (Exception ex) { _logger.LogWarning($"Process kill failed (best effort): {ex.Message}"); }
                    throw new TimeoutException($"Installation of {app.Name} timed out after {_executionService.InstallationTimeoutMs / 60000} minutes");
                }

                // Find JSON line in output
                string? jsonLine = null;
                for (int i = outputLines.Count - 1; i >= 0; i--)
                {
                    string trimmed = outputLines[i].Trim();
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
                        using JsonDocument doc = JsonDocument.Parse(jsonLine);
                        JsonElement root = doc.RootElement;

                        bool success = root.TryGetProperty("Success", out JsonElement successProp) &&
                                      successProp.ValueKind == JsonValueKind.True;
                        string message = root.TryGetProperty("Message", out JsonElement msgProp)
                            ? msgProp.GetString() ?? string.Empty
                            : string.Empty;
                        string method = root.TryGetProperty("Method", out JsonElement methodProp)
                            ? methodProp.GetString() ?? string.Empty
                            : string.Empty;
                        bool alreadyInstalled = root.TryGetProperty("AlreadyInstalled", out JsonElement aiProp) &&
                                               aiProp.ValueKind == JsonValueKind.True;

                        logBuilder.AppendLine($"Result: Success={success}, Method={method}, Message={message}");

                        if (success)
                        {
                            progressCallback?.Invoke(LogFormat(nameof(Resources.Resources.AppManagement_Completed), app.Name));
                            return InstallResult.Successful(message, logBuilder.ToString(), method, alreadyInstalled);
                        }
                        else
                        {
                            progressCallback?.Invoke(LogFormat(nameof(Resources.Resources.AppManagement_FailedReason), app.Name, message));
                            return InstallResult.Failed(message, logBuilder.ToString());
                        }
                    }
                    catch (JsonException)
                    {
                        return InstallResult.Failed($"Invalid response format", logBuilder.ToString());
                    }
                }

                string fullOutput = string.Join("\n", outputLines);
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
                _logger.LogError("Application management operation failed.", ex);
                progressCallback?.Invoke(LogFormat(nameof(Resources.Resources.Common_ErrorWithMessage), ex.Message));
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
            StringBuilder logBuilder = new StringBuilder();

            try
            {
                progressCallback?.Invoke(LogFormat(nameof(Resources.Resources.AppManagement_PreparingUninstall), app.Name));
                logBuilder.AppendLine($"Uninstalling: {app.Name}");

                string? wingetId = null;
                string? chocoPackage = null;

                if (_cacheService.TryGetApplicationData(app.AppId, out JsonElement appData))
                {
                    if (appData.TryGetProperty("Sources", out JsonElement sources))
                    {
                        wingetId = JsonHelper.GetJsonString(sources, "Winget");
                        chocoPackage = JsonHelper.GetJsonString(sources, "Chocolatey");
                    }
                }

                bool rejectedInvalidPackageId = false;
                if (UpdateSourcePolicy.RejectInvalidPackageId(wingetId, "Winget", logBuilder))
                {
                    wingetId = null;
                    rejectedInvalidPackageId = true;
                }

                if (UpdateSourcePolicy.RejectInvalidPackageId(chocoPackage, "Chocolatey", logBuilder))
                {
                    chocoPackage = null;
                    rejectedInvalidPackageId = true;
                }

                // Try Winget first
                if (!string.IsNullOrEmpty(wingetId))
                {
                    progressCallback?.Invoke(LogFormat(nameof(Resources.Resources.AppManagement_UninstallingViaWinget), wingetId));
                    logBuilder.AppendLine($"Uninstalling via Winget: {wingetId}");

                    (bool Success, int ExitCode, string Output) wingetResult = await ExecuteUninstallCommandAsync(
                        "winget",
                        $"uninstall --id \"{wingetId}\" --silent --accept-source-agreements",
                        logBuilder);

                    if (wingetResult.Success)
                    {
                        progressCallback?.Invoke(LogFormat(nameof(Resources.Resources.AppManagement_Uninstalled), app.Name));
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
                    progressCallback?.Invoke(LogFormat(nameof(Resources.Resources.AppManagement_UninstallingViaChocolatey), chocoPackage));
                    logBuilder.AppendLine($"Uninstalling via Chocolatey: {chocoPackage}");

                    (bool Success, int ExitCode, string Output) chocoResult = await ExecuteUninstallCommandAsync(
                        "choco",
                        $"uninstall {chocoPackage} -y --no-progress",
                        logBuilder);

                    if (chocoResult.Success)
                    {
                        progressCallback?.Invoke(LogFormat(nameof(Resources.Resources.AppManagement_Uninstalled), app.Name));
                        logBuilder.AppendLine("Chocolatey uninstallation succeeded");
                        return InstallResult.Successful(
                            $"Successfully uninstalled {app.Name}",
                            logBuilder.ToString(),
                            "Chocolatey",
                            alreadyInstalled: false);
                    }

                    logBuilder.AppendLine($"Chocolatey uninstallation failed (exit code: {chocoResult.ExitCode})");
                }

                string errorMsg = string.IsNullOrEmpty(wingetId) && string.IsNullOrEmpty(chocoPackage)
                    ? (rejectedInvalidPackageId
                        ? GetLogResource(nameof(Resources.Resources.AppManagement_InvalidPackageId))
                        : "No uninstall sources available for this application")
                    : "All uninstallation methods failed";

                progressCallback?.Invoke(LogFormat(nameof(Resources.Resources.AppManagement_Failed), app.Name));
                logBuilder.AppendLine($"Uninstallation failed: {errorMsg}");
                return InstallResult.Failed(errorMsg, logBuilder.ToString());
            }
            catch (Exception ex)
            {
                logBuilder.AppendLine($"Exception: {ex.Message}");
                _logger.LogError("Application management operation failed.", ex);
                progressCallback?.Invoke(LogFormat(nameof(Resources.Resources.Common_ErrorWithMessage), ex.Message));
                return InstallResult.Failed(ex.Message, logBuilder.ToString());
            }
        });
    }

    /// <inheritdoc/>
    public Task<UpdateCheckResult> CheckApplicationUpdateAsync(ApplicationModel app)
        => CheckApplicationUpdateAsync(app, forceRefresh: false);

    /// <inheritdoc/>
    public async Task<UpdateCheckResult> CheckApplicationUpdateAsync(ApplicationModel app, bool forceRefresh)
    {
        if (app.Status != ApplicationStatus.Installed &&
            app.Status != ApplicationStatus.AlreadyInstalled &&
            app.Status != ApplicationStatus.UpdateAvailable)
        {
            return UpdateCheckResult.UpToDate();
        }

        if (forceRefresh)
        {
            await InvalidateUpdateCacheAsync().ConfigureAwait(false);
        }

        await _cacheService.EnsureApplicationsCacheAsync();

        return await Task.Run(async () =>
        {
            try
            {
                string? wingetId = null;

                if (_cacheService.TryGetApplicationData(app.AppId, out JsonElement appData))
                {
                    if (appData.TryGetProperty("Sources", out JsonElement sources))
                    {
                        wingetId = JsonHelper.GetJsonString(sources, "Winget");
                    }
                }

                if (string.IsNullOrEmpty(wingetId))
                {
                    return UpdateCheckResult.UpToDate();
                }

                if (!PackageIdValidator.IsValidPackageId(wingetId))
                {
                    return UpdateCheckResult.Failed(GetLogResource(nameof(Resources.Resources.AppManagement_CannotDetermineVersion)));
                }

                string installedVersion = await GetInstalledVersionAsync(wingetId);
                string availableVersion = await GetRepositoryVersionAsync(wingetId);

                if (string.IsNullOrEmpty(installedVersion))
                {
                    return UpdateCheckResult.UpToDate();
                }

                if (string.IsNullOrEmpty(availableVersion))
                {
                    return UpdateCheckResult.UpToDate(installedVersion);
                }

                int comparison = VersionServiceImpl.CompareVersions(installedVersion, availableVersion);

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
    public async Task InvalidateUpdateCacheAsync()
    {
        try
        {
            _detectionService.InvalidateAllCache(CacheInvalidationReason.UserRequested);
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"[ApplicationManagementService] Detection cache invalidation failed: {ex.Message}");
        }

        try
        {
            string updateManagerPath = _pathService.GetPathForPowerShell("Modules", "UpdateManager.psm1");
            string escapedUpdateManagerPath = PowerShellValidation.EscapeForPowerShell(updateManagerPath);
            string script = $@"
Import-Module '{escapedUpdateManagerPath}' -Force
if (Get-Command -Name 'Clear-WingetUpdatesCache' -ErrorAction SilentlyContinue) {{
    Clear-WingetUpdatesCache
}} elseif (Get-Command -Name 'Clear-BatchUpdateCache' -ErrorAction SilentlyContinue) {{
    Clear-BatchUpdateCache
}}
";

            await _executionService.ExecutePowerShellScriptAsync(script).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"[ApplicationManagementService] Update cache invalidation failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<InstallResult> UpdateApplicationAsync(
        ApplicationModel app,
        Action<string>? progressCallback = null)
    {
        await _cacheService.EnsureApplicationsCacheAsync();

        return await Task.Run(async () =>
        {
            StringBuilder logBuilder = new StringBuilder();

            try
            {
                progressCallback?.Invoke(LogFormat(nameof(Resources.Resources.AppManagement_PreparingUpdate), app.Name));
                logBuilder.AppendLine($"Updating: {app.Name}");

                string? wingetId = null;
                string? chocoPackage = null;
                string? preferredUpdateSource = null;
                JsonElement appData = default;
                bool hasApplicationData = _cacheService.TryGetApplicationData(app.AppId, out appData);

                if (hasApplicationData)
                {
                    if (appData.TryGetProperty("Sources", out JsonElement sources))
                    {
                        wingetId = JsonHelper.GetJsonString(sources, "Winget");
                        chocoPackage = JsonHelper.GetJsonString(sources, "Chocolatey");
                    }

                    preferredUpdateSource = UpdateSourcePolicy.Normalize(
                        JsonHelper.GetJsonString(appData, PreferredUpdateSourcePropertyName));
                }

                bool rejectedInvalidPackageId = false;
                if (UpdateSourcePolicy.RejectInvalidPackageId(wingetId, UpdateSourcePolicy.SourceWinget, logBuilder))
                {
                    wingetId = null;
                    rejectedInvalidPackageId = true;
                }

                if (UpdateSourcePolicy.RejectInvalidPackageId(chocoPackage, UpdateSourcePolicy.SourceChocolatey, logBuilder))
                {
                    chocoPackage = null;
                    rejectedInvalidPackageId = true;
                }

                InstallResult? result = null;
                if (string.Equals(preferredUpdateSource, UpdateSourcePolicy.SourceChocolatey, StringComparison.OrdinalIgnoreCase))
                {
                    logBuilder.AppendLine($"Preferred update source: {UpdateSourcePolicy.SourceChocolatey}");
                    result = await TryUpdateViaChocolateyAsync(
                        app,
                        chocoPackage,
                        hasApplicationData ? appData : null,
                        verifyCommandDetection: true,
                        progressCallback,
                        logBuilder);
                }
                else if (string.Equals(preferredUpdateSource, UpdateSourcePolicy.SourceWinget, StringComparison.OrdinalIgnoreCase))
                {
                    logBuilder.AppendLine($"Preferred update source: {UpdateSourcePolicy.SourceWinget}");
                    result = await TryUpdateViaWingetAsync(app, wingetId, progressCallback, logBuilder);
                }
                else
                {
                    result = await TryUpdateViaWingetAsync(app, wingetId, progressCallback, logBuilder);
                    result ??= await TryUpdateViaChocolateyAsync(
                        app,
                        chocoPackage,
                        appData: null,
                        verifyCommandDetection: false,
                        progressCallback,
                        logBuilder);
                }

                if (result != null)
                {
                    return result;
                }

                string errorMsg = UpdateSourcePolicy.IsPreferredSourceUnavailable(preferredUpdateSource, wingetId, chocoPackage)
                    ? $"Preferred update source {preferredUpdateSource} is not available for this application"
                    : string.IsNullOrEmpty(wingetId) && string.IsNullOrEmpty(chocoPackage)
                    ? (rejectedInvalidPackageId
                        ? GetLogResource(nameof(Resources.Resources.AppManagement_InvalidPackageId))
                        : "No update sources available for this application")
                    : "All update methods failed";

                progressCallback?.Invoke(LogFormat(nameof(Resources.Resources.AppManagement_Failed), app.Name));
                logBuilder.AppendLine($"Update failed: {errorMsg}");
                return InstallResult.Failed(errorMsg, logBuilder.ToString());
            }
            catch (Exception ex)
            {
                logBuilder.AppendLine($"Exception: {ex.Message}");
                _logger.LogError("Application management operation failed.", ex);
                progressCallback?.Invoke(LogFormat(nameof(Resources.Resources.Common_ErrorWithMessage), ex.Message));
                return InstallResult.Failed(ex.Message, logBuilder.ToString());
            }
        });
    }

    /// <inheritdoc/>
    public Task<bool> LaunchApplicationAsync(ApplicationModel app) => _launcher.LaunchApplicationAsync(app);

    private async Task<InstallResult?> TryUpdateViaWingetAsync(
        ApplicationModel app,
        string? wingetId,
        Action<string>? progressCallback,
        StringBuilder logBuilder)
    {
        if (string.IsNullOrEmpty(wingetId))
        {
            return null;
        }

        progressCallback?.Invoke(LogFormat(nameof(Resources.Resources.AppManagement_UpdatingViaWinget), wingetId));
        logBuilder.AppendLine($"Updating via Winget: {wingetId}");

        (bool Success, int ExitCode, string Output) wingetResult = await ExecuteUpdateCommandAsync(
            "winget",
            $"upgrade --id \"{wingetId}\" --silent --accept-package-agreements --accept-source-agreements",
            logBuilder);

        if (wingetResult.Success)
        {
            progressCallback?.Invoke(LogFormat(nameof(Resources.Resources.AppManagement_Updated), app.Name));
            logBuilder.AppendLine("Winget update succeeded");
            return InstallResult.Successful(
                $"Successfully updated {app.Name}",
                logBuilder.ToString(),
                UpdateSourcePolicy.SourceWinget);
        }

        if (wingetResult.ExitCode == -1978335189)
        {
            progressCallback?.Invoke(LogFormat(nameof(Resources.Resources.AppManagement_AlreadyUpToDate), app.Name));
            logBuilder.AppendLine("Application is already up to date");
            return InstallResult.Successful(
                $"{app.Name} is already up to date",
                logBuilder.ToString(),
                UpdateSourcePolicy.SourceWinget,
                alreadyInstalled: true);
        }

        logBuilder.AppendLine($"Winget update failed (exit code: {wingetResult.ExitCode})");
        return null;
    }

    private async Task<InstallResult?> TryUpdateViaChocolateyAsync(
        ApplicationModel app,
        string? chocoPackage,
        JsonElement? appData,
        bool verifyCommandDetection,
        Action<string>? progressCallback,
        StringBuilder logBuilder)
    {
        if (string.IsNullOrEmpty(chocoPackage))
        {
            return null;
        }

        progressCallback?.Invoke(LogFormat(nameof(Resources.Resources.AppManagement_UpdatingViaChocolatey), chocoPackage));
        logBuilder.AppendLine($"Updating via Chocolatey: {chocoPackage}");

        (bool Success, int ExitCode, string Output) chocoResult = await ExecuteUpdateCommandAsync(
            "choco",
            $"upgrade {chocoPackage} -y --no-progress",
            logBuilder);

        if (chocoResult.Success)
        {
            if (verifyCommandDetection &&
                appData.HasValue &&
                !await VerifyCommandDetectionAsync(appData.Value, logBuilder))
            {
                progressCallback?.Invoke(LogFormat(nameof(Resources.Resources.AppManagement_Failed), app.Name));
                return InstallResult.Failed(
                    "Chocolatey update completed but post-update verification failed",
                    logBuilder.ToString());
            }

            progressCallback?.Invoke(LogFormat(nameof(Resources.Resources.AppManagement_Updated), app.Name));
            logBuilder.AppendLine("Chocolatey update succeeded");
            return InstallResult.Successful(
                $"Successfully updated {app.Name}",
                logBuilder.ToString(),
                UpdateSourcePolicy.SourceChocolatey);
        }

        logBuilder.AppendLine($"Chocolatey update failed (exit code: {chocoResult.ExitCode})");
        return null;
    }

    private async Task<bool> VerifyCommandDetectionAsync(JsonElement appData, StringBuilder logBuilder)
    {
        if (!appData.TryGetProperty(DetectionPropertyName, out JsonElement detection) ||
            detection.ValueKind != JsonValueKind.Object)
        {
            logBuilder.AppendLine("Post-update command detection skipped: no detection configuration");
            return true;
        }

        string? method = JsonHelper.GetJsonString(detection, DetectionMethodPropertyName);
        string? commandLine = JsonHelper.GetJsonString(detection, DetectionCommandPropertyName);
        if (!string.Equals(method, DetectionMethodCommand, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(commandLine))
        {
            logBuilder.AppendLine("Post-update command detection skipped: detection method is not Command");
            return true;
        }

        logBuilder.AppendLine($"Verifying update via command detection: {commandLine}");
        (bool Success, int ExitCode, string Output) detectionResult =
            await ExecuteCommandDetectionAsync(commandLine, logBuilder);

        if (!detectionResult.Success)
        {
            logBuilder.AppendLine($"Post-update command detection failed (exit code: {detectionResult.ExitCode})");
            return false;
        }

        string? versionRegex = JsonHelper.GetJsonString(detection, DetectionVersionRegexPropertyName);
        if (!string.IsNullOrWhiteSpace(versionRegex) &&
            !IsDetectionOutputMatch(detectionResult.Output, versionRegex, logBuilder))
        {
            logBuilder.AppendLine("Post-update command detection failed: version pattern did not match");
            return false;
        }

        logBuilder.AppendLine("Post-update command detection succeeded");
        return true;
    }

    private static bool IsDetectionOutputMatch(string output, string versionRegex, StringBuilder logBuilder)
    {
        try
        {
            return Regex.IsMatch(output, versionRegex, RegexOptions.None, DetectionRegexTimeout);
        }
        catch (ArgumentException ex)
        {
            logBuilder.AppendLine($"Post-update command detection failed: invalid version pattern ({ex.Message})");
            return false;
        }
        catch (RegexMatchTimeoutException ex)
        {
            logBuilder.AppendLine($"Post-update command detection failed: version pattern timed out ({ex.Message})");
            return false;
        }
    }

    private static string GetLogResource(string resourceName)
        => DeploymentLog.GetResource(resourceName);

    private static string LogFormat(string resourceName, params object?[] args)
        => DeploymentLog.Format(resourceName, args);

    /// <summary>
    /// Kept as a protected member because derived test doubles call it when they stand in
    /// for a vendor command.
    /// </summary>
    protected static void AppendVendorOutputSummary(StringBuilder logBuilder, string output, string error)
        => DeploymentLog.AppendVendorOutputSummary(logBuilder, output, error);

    /// <summary>
    /// Gets the installed version of a package using winget list.
    /// </summary>
    private Task<string> GetInstalledVersionAsync(string wingetId)
        => _commandRunner.QueryWingetVersionAsync(
            wingetId,
            $"list --id \"{wingetId}\" --exact --disable-interactivity",
            VersionServiceImpl.ParseVersionFromWingetList,
            "installed version");

    /// <summary>
    /// Gets the available version from repository using winget show.
    /// </summary>
    private Task<string> GetRepositoryVersionAsync(string wingetId)
        => _commandRunner.QueryWingetVersionAsync(
            wingetId,
            $"show --id \"{wingetId}\" --exact --disable-interactivity",
            VersionServiceImpl.ParseVersionFromWingetShow,
            "repository version");

    /// <summary>
    /// Executes an update command and returns the result.
    /// </summary>
    protected virtual async Task<(bool Success, int ExitCode, string Output)> ExecuteUpdateCommandAsync(
        string command,
        string arguments,
        StringBuilder logBuilder)
    {
        VendorCommandResult result = await _commandRunner.RunAsync(command, arguments, "Update command", logBuilder);
        return (result.Success, result.ExitCode, result.Output);
    }

    /// <summary>
    /// Executes a command detection probe and returns the result.
    /// </summary>
    protected virtual async Task<(bool Success, int ExitCode, string Output)> ExecuteCommandDetectionAsync(
        string commandLine,
        StringBuilder logBuilder)
    {
        VendorCommandResult result = await _commandRunner.RunDetectionAsync(commandLine, logBuilder);
        return (result.Success, result.ExitCode, result.Output);
    }

    /// <summary>
    /// Executes an uninstall command and returns the result.
    /// </summary>
    protected virtual async Task<(bool Success, int ExitCode, string Output)> ExecuteUninstallCommandAsync(
        string command,
        string arguments,
        StringBuilder logBuilder)
    {
        VendorCommandResult result = await _commandRunner.RunAsync(command, arguments, "Uninstall command", logBuilder);
        return (result.Success, result.ExitCode, result.Output);
    }
}

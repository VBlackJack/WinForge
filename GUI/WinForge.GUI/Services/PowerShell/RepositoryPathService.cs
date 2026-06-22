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
using WinForge.GUI.Configuration;

namespace WinForge.GUI.Services.PowerShell;

/// <summary>
/// Service for resolving and managing the WinForge repository root path.
/// </summary>
public class RepositoryPathService : IRepositoryPathService
{
    private readonly string _repositoryRoot;
    private readonly string _userDataRoot;
    private readonly bool _isUserDataFallbackActive;

    /// <summary>
    /// Initializes a new instance of the RepositoryPathService.
    /// </summary>
    public RepositoryPathService()
        : this(ResolveRepositoryRootSafe(), ResolveUserDataRootSafe())
    {
    }

    internal RepositoryPathService(string repositoryRoot, IEnumerable<string?> userDataBasePathCandidates)
        : this(NormalizeRequiredPath(repositoryRoot, nameof(repositoryRoot)), ResolveUserDataRootSafe(userDataBasePathCandidates))
    {
    }

    private RepositoryPathService(string repositoryRoot, UserDataResolution userDataResolution)
    {
        _repositoryRoot = repositoryRoot;
        _userDataRoot = userDataResolution.Path;
        _isUserDataFallbackActive = userDataResolution.IsFallback;

        if (_isUserDataFallbackActive || !string.IsNullOrWhiteSpace(userDataResolution.Reason))
        {
            Trace.WriteLine(
                $"WinForge user data resolution notice. Using '{_userDataRoot}'. Reason: {userDataResolution.Reason}");
        }
    }

    /// <inheritdoc/>
    public string RepositoryRoot => _repositoryRoot;

    /// <inheritdoc/>
    public string UserDataRoot => _userDataRoot;

    /// <inheritdoc/>
    public string LogsDirectory => GetUserDataPath(WinForgePathNames.LogsDirectoryName);

    /// <inheritdoc/>
    public string SettingsFilePath => GetUserDataPath(WinForgePathNames.SettingsFileName);

    /// <inheritdoc/>
    public string DeploymentHistoryFilePath => GetUserDataPath(WinForgePathNames.DeploymentHistoryFileName);

    /// <inheritdoc/>
    public string UserProfilesDirectory => GetUserDataPath(WinForgePathNames.ProfilesDirectoryName);

    /// <inheritdoc/>
    public string DefaultProfilesDirectory
    {
        get
        {
            string defaultsPath = GetPath(
                WinForgePathNames.ProfilesDirectoryName,
                WinForgePathNames.DefaultProfilesDirectoryName);

            return Directory.Exists(defaultsPath)
                ? defaultsPath
                : LegacyInstallProfilesDirectory;
        }
    }

    /// <inheritdoc/>
    public string LegacyInstallProfilesDirectory => GetPath(WinForgePathNames.ProfilesDirectoryName);

    /// <inheritdoc/>
    public bool IsUserDataFallbackActive => _isUserDataFallbackActive;

    /// <inheritdoc/>
    public string GetSafeRepositoryRoot()
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
    public string GetPath(params string[] relativePath)
    {
        string[] paths = new string[relativePath.Length + 1];
        paths[0] = GetSafeRepositoryRoot();
        Array.Copy(relativePath, 0, paths, 1, relativePath.Length);
        return Path.Combine(paths);
    }

    /// <inheritdoc/>
    public string GetPathForPowerShell(params string[] relativePath)
    {
        return GetPath(relativePath).Replace("\\", "/");
    }

    /// <inheritdoc/>
    public string GetUserDataPath(params string[] relativePath)
    {
        string[] paths = new string[relativePath.Length + 1];
        paths[0] = UserDataRoot;
        Array.Copy(relativePath, 0, paths, 1, relativePath.Length);
        return Path.Combine(paths);
    }

    /// <summary>
    /// Resolves repository root with guaranteed non-null result.
    /// </summary>
    private static string ResolveRepositoryRootSafe()
    {
        try
        {
            string result = ResolveRepositoryRoot();
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
        string? processPath = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(processPath))
        {
            string? dir = Path.GetDirectoryName(processPath);
            if (!string.IsNullOrEmpty(dir))
            {
                return dir;
            }
        }

        string currentDir = Environment.CurrentDirectory;
        if (!string.IsNullOrEmpty(currentDir))
        {
            return currentDir;
        }

        string baseDir = AppContext.BaseDirectory;
        if (!string.IsNullOrEmpty(baseDir))
        {
            return baseDir;
        }

        // Ultimate fallback - temp folder (always exists)
        return Path.GetTempPath();
    }

    /// <summary>
    /// Resolves the repository root path from the executable location.
    /// Walks up directories looking for repository markers (Config/version.json, Modules/).
    /// </summary>
    private static string ResolveRepositoryRoot()
    {
        // Build list of candidate paths, filtering out nulls
        List<string> candidatePaths = new List<string>();

        // Priority 1: ProcessPath directory (best for single-file apps)
        string? processPath = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(processPath))
        {
            string? dir = Path.GetDirectoryName(processPath);
            if (!string.IsNullOrEmpty(dir))
            {
                candidatePaths.Add(dir);
            }
        }

        // Priority 2: BaseDirectory
        string baseDir = AppContext.BaseDirectory;
        if (!string.IsNullOrEmpty(baseDir))
        {
            candidatePaths.Add(baseDir);
        }

        // Priority 3: CurrentDirectory
        string currentDir = Environment.CurrentDirectory;
        if (!string.IsNullOrEmpty(currentDir))
        {
            candidatePaths.Add(currentDir);
        }

        foreach (string basePath in candidatePaths)
        {
            string? result = TryFindRepositoryRoot(basePath);
            if (!string.IsNullOrEmpty(result))
            {
                return result;
            }
        }

        throw new DirectoryNotFoundException(
            $"Could not locate WinForge repository root. Searched from: {string.Join(", ", candidatePaths)}");
    }

    /// <summary>
    /// Attempts to find repository root by walking up from a base path.
    /// </summary>
    private static string? TryFindRepositoryRoot(string basePath)
    {
        DirectoryInfo? currentDir = new DirectoryInfo(basePath);

        while (currentDir != null)
        {
            // Check for Config/version.json
            string versionFile = Path.Combine(
                currentDir.FullName,
                WinForgePathNames.ConfigDirectoryName,
                WinForgePathNames.VersionFileName);
            if (File.Exists(versionFile))
            {
                return currentDir.FullName;
            }

            // Check for Modules/InstallationEngine.psm1
            string modulesDir = Path.Combine(currentDir.FullName, WinForgePathNames.ModulesDirectoryName);
            if (Directory.Exists(modulesDir))
            {
                string coreModule = Path.Combine(modulesDir, WinForgePathNames.InstallationEngineModuleFileName);
                if (File.Exists(coreModule))
                {
                    return currentDir.FullName;
                }
            }

            currentDir = currentDir.Parent;
        }

        return null;
    }

    private static string NormalizeRequiredPath(string path, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path cannot be empty.", parameterName);
        }

        return Path.GetFullPath(path);
    }

    private static UserDataResolution ResolveUserDataRootSafe()
    {
        return ResolveUserDataRootSafe(GetDefaultUserDataBasePathCandidates());
    }

    private static IEnumerable<string?> GetDefaultUserDataBasePathCandidates()
    {
        yield return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        yield return Path.GetTempPath();
    }

    private static UserDataResolution ResolveUserDataRootSafe(IEnumerable<string?> userDataBasePathCandidates)
    {
        List<string> errors = new List<string>();
        int index = 0;

        foreach (string? candidate in userDataBasePathCandidates)
        {
            bool isFallbackCandidate = index > 0;
            index++;

            if (string.IsNullOrWhiteSpace(candidate))
            {
                errors.Add("Candidate was empty.");
                continue;
            }

            string userDataPath;
            try
            {
                userDataPath = ResolveProductDataPath(candidate, errors);
            }
            catch (Exception ex)
            {
                errors.Add($"Candidate '{candidate}' was invalid: {ex.Message}");
                continue;
            }

            if (TryEnsureWritableDirectory(userDataPath, out string? error))
            {
                return new UserDataResolution(
                    Path.GetFullPath(userDataPath),
                    isFallbackCandidate,
                    isFallbackCandidate ? string.Join("; ", errors) : string.Empty);
            }

            errors.Add($"Candidate '{userDataPath}' was not writable: {error}");
        }

        throw new InvalidOperationException(
            $"Could not resolve a writable WinForge user data directory. {string.Join("; ", errors)}");
    }

    private static string ResolveProductDataPath(string candidate, List<string> diagnostics)
    {
        string productPath = Path.Combine(candidate, WinForgePathNames.ProductDirectoryName);
        string legacyPath = Path.Combine(candidate, WinForgePathNames.LegacyProductDirectoryName);

        if (Directory.Exists(productPath) || !Directory.Exists(legacyPath))
        {
            return productPath;
        }

        try
        {
            Directory.Move(legacyPath, productPath);
            diagnostics.Add($"Migrated user data directory from '{legacyPath}' to '{productPath}'.");
            return productPath;
        }
        catch (Exception ex)
        {
            diagnostics.Add(
                $"Failed to migrate user data directory from '{legacyPath}' to '{productPath}': {ex.Message}. " +
                "Using legacy directory for this session.");
            return legacyPath;
        }
    }

    private static bool TryEnsureWritableDirectory(string directoryPath, out string? error)
    {
        try
        {
            Directory.CreateDirectory(directoryPath);

            string probePath = Path.Combine(
                directoryPath,
                $"{WinForgePathNames.WriteProbeFilePrefix}{Guid.NewGuid():N}");

            using (File.Create(probePath, 1, FileOptions.DeleteOnClose))
            {
            }

            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private sealed record UserDataResolution(string Path, bool IsFallback, string Reason);
}

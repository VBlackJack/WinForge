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
using System.Diagnostics;
using Win11Forge.GUI.Configuration;

namespace Win11Forge.GUI.Services.PowerShell;

/// <summary>
/// Service for resolving and managing the Win11Forge repository root path.
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

        if (_isUserDataFallbackActive)
        {
            Debug.WriteLine(
                $"Win11Forge user data fallback active. Using '{_userDataRoot}'. Reason: {userDataResolution.Reason}");
        }
    }

    /// <inheritdoc/>
    public string RepositoryRoot => _repositoryRoot;

    /// <inheritdoc/>
    public string UserDataRoot => _userDataRoot;

    /// <inheritdoc/>
    public string LogsDirectory => GetUserDataPath(Win11ForgePathNames.LogsDirectoryName);

    /// <inheritdoc/>
    public string SettingsFilePath => GetUserDataPath(Win11ForgePathNames.SettingsFileName);

    /// <inheritdoc/>
    public string DeploymentHistoryFilePath => GetUserDataPath(Win11ForgePathNames.DeploymentHistoryFileName);

    /// <inheritdoc/>
    public string UserProfilesDirectory => GetUserDataPath(Win11ForgePathNames.ProfilesDirectoryName);

    /// <inheritdoc/>
    public string DefaultProfilesDirectory
    {
        get
        {
            var defaultsPath = GetPath(
                Win11ForgePathNames.ProfilesDirectoryName,
                Win11ForgePathNames.DefaultProfilesDirectoryName);

            return Directory.Exists(defaultsPath)
                ? defaultsPath
                : LegacyInstallProfilesDirectory;
        }
    }

    /// <inheritdoc/>
    public string LegacyInstallProfilesDirectory => GetPath(Win11ForgePathNames.ProfilesDirectoryName);

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
        var paths = new string[relativePath.Length + 1];
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
        var paths = new string[relativePath.Length + 1];
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
            var versionFile = Path.Combine(
                currentDir.FullName,
                Win11ForgePathNames.ConfigDirectoryName,
                Win11ForgePathNames.VersionFileName);
            if (File.Exists(versionFile))
            {
                return currentDir.FullName;
            }

            // Check for Modules/InstallationEngine.psm1
            var modulesDir = Path.Combine(currentDir.FullName, Win11ForgePathNames.ModulesDirectoryName);
            if (Directory.Exists(modulesDir))
            {
                var coreModule = Path.Combine(modulesDir, Win11ForgePathNames.InstallationEngineModuleFileName);
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
        var errors = new List<string>();
        var index = 0;

        foreach (var candidate in userDataBasePathCandidates)
        {
            var isFallbackCandidate = index > 0;
            index++;

            if (string.IsNullOrWhiteSpace(candidate))
            {
                errors.Add("Candidate was empty.");
                continue;
            }

            string userDataPath;
            try
            {
                userDataPath = Path.Combine(candidate, Win11ForgePathNames.ProductDirectoryName);
            }
            catch (Exception ex)
            {
                errors.Add($"Candidate '{candidate}' was invalid: {ex.Message}");
                continue;
            }

            if (TryEnsureWritableDirectory(userDataPath, out var error))
            {
                return new UserDataResolution(
                    Path.GetFullPath(userDataPath),
                    isFallbackCandidate,
                    isFallbackCandidate ? string.Join("; ", errors) : string.Empty);
            }

            errors.Add($"Candidate '{userDataPath}' was not writable: {error}");
        }

        throw new InvalidOperationException(
            $"Could not resolve a writable Win11Forge user data directory. {string.Join("; ", errors)}");
    }

    private static bool TryEnsureWritableDirectory(string directoryPath, out string? error)
    {
        try
        {
            Directory.CreateDirectory(directoryPath);

            var probePath = Path.Combine(
                directoryPath,
                $"{Win11ForgePathNames.WriteProbeFilePrefix}{Guid.NewGuid():N}");

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

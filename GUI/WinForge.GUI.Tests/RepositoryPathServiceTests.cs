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
using WinForge.GUI.Services.PowerShell;

namespace WinForge.GUI.Tests;

public class RepositoryPathServiceTests
{
    [Fact]
    public void Constructor_ShouldResolveExposedPaths()
    {
        using TestWorkspace workspace = new TestWorkspace();
        string defaultProfiles = Path.Combine(
            workspace.RepositoryRoot,
            WinForgePathNames.ProfilesDirectoryName,
            WinForgePathNames.DefaultProfilesDirectoryName);
        Directory.CreateDirectory(defaultProfiles);

        RepositoryPathService service = new RepositoryPathService(workspace.RepositoryRoot, [workspace.UserDataBasePath]);

        Assert.Equal(workspace.RepositoryRoot, service.RepositoryRoot);
        Assert.Equal(Path.Combine(workspace.UserDataBasePath, WinForgePathNames.ProductDirectoryName), service.UserDataRoot);
        Assert.Equal(Path.Combine(service.UserDataRoot, WinForgePathNames.LogsDirectoryName), service.LogsDirectory);
        Assert.Equal(Path.Combine(service.UserDataRoot, WinForgePathNames.SettingsFileName), service.SettingsFilePath);
        Assert.Equal(Path.Combine(service.UserDataRoot, WinForgePathNames.DeploymentHistoryFileName), service.DeploymentHistoryFilePath);
        Assert.Equal(Path.Combine(service.UserDataRoot, WinForgePathNames.ProfilesDirectoryName), service.UserProfilesDirectory);
        Assert.Equal(defaultProfiles, service.DefaultProfilesDirectory);
        Assert.Equal(Path.Combine(workspace.RepositoryRoot, WinForgePathNames.ProfilesDirectoryName), service.LegacyInstallProfilesDirectory);
        Assert.False(service.IsUserDataFallbackActive);
    }

    [Fact]
    public void Constructor_WhenLegacyUserDataExists_ShouldMigrateToProductDirectory()
    {
        using TestWorkspace workspace = new TestWorkspace();
        string legacyRoot = Path.Combine(workspace.UserDataBasePath, WinForgePathNames.LegacyProductDirectoryName);
        string markerPath = Path.Combine(legacyRoot, "settings.json");
        Directory.CreateDirectory(legacyRoot);
        File.WriteAllText(markerPath, "{}");

        RepositoryPathService service = new RepositoryPathService(workspace.RepositoryRoot, [workspace.UserDataBasePath]);

        Assert.Equal(workspace.UserDataRoot, service.UserDataRoot);
        Assert.True(File.Exists(Path.Combine(workspace.UserDataRoot, "settings.json")));
        Assert.False(Directory.Exists(legacyRoot));
    }

    [Fact]
    public void Constructor_WhenPreferredUserDataIsUnavailable_ShouldUseFallbackAndEmitDebug()
    {
        using TestWorkspace workspace = new TestWorkspace();
        string blockedBasePath = Path.Combine(workspace.RootPath, "blocked");
        File.WriteAllText(blockedBasePath, "not a directory");
        string fallbackBasePath = Path.Combine(workspace.RootPath, "fallback");
        using CapturingTraceListener listener = new CapturingTraceListener();
        Trace.Listeners.Add(listener);

        try
        {
            RepositoryPathService service = new RepositoryPathService(workspace.RepositoryRoot, [blockedBasePath, fallbackBasePath]);

            Assert.True(service.IsUserDataFallbackActive);
            Assert.Equal(Path.Combine(fallbackBasePath, WinForgePathNames.ProductDirectoryName), service.UserDataRoot);
            Assert.Contains(
                "user data resolution notice",
                listener.Output,
                StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Trace.Listeners.Remove(listener);
        }
    }

    [Fact]
    public void DefaultProfilesDirectory_WhenDefaultsFolderMissing_ShouldFallBackToLegacyInstallProfiles()
    {
        using TestWorkspace workspace = new TestWorkspace();
        string legacyProfilesDirectory = Path.Combine(
            workspace.RepositoryRoot,
            WinForgePathNames.ProfilesDirectoryName);
        Directory.CreateDirectory(legacyProfilesDirectory);

        RepositoryPathService service = new RepositoryPathService(workspace.RepositoryRoot, [workspace.UserDataBasePath]);

        Assert.Equal(service.LegacyInstallProfilesDirectory, service.DefaultProfilesDirectory);
        Assert.Equal(legacyProfilesDirectory, service.DefaultProfilesDirectory);
        Assert.True(Directory.Exists(service.DefaultProfilesDirectory));
    }

    [Fact]
    public void DefaultProfilesDirectory_WhenDefaultsFolderExists_ShouldReturnDefaultsPath()
    {
        using TestWorkspace workspace = new TestWorkspace();
        string defaultsDirectory = Path.Combine(
            workspace.RepositoryRoot,
            WinForgePathNames.ProfilesDirectoryName,
            WinForgePathNames.DefaultProfilesDirectoryName);
        Directory.CreateDirectory(defaultsDirectory);

        RepositoryPathService service = new RepositoryPathService(workspace.RepositoryRoot, [workspace.UserDataBasePath]);

        Assert.Equal(defaultsDirectory, service.DefaultProfilesDirectory);
        Assert.EndsWith(WinForgePathNames.DefaultProfilesDirectoryName, service.DefaultProfilesDirectory);
        Assert.NotEqual(service.LegacyInstallProfilesDirectory, service.DefaultProfilesDirectory);
    }

    private sealed class CapturingTraceListener : TraceListener
    {
        private readonly StringWriter _writer = new();

        public string Output => _writer.ToString();

        public override void Write(string? message)
        {
            _writer.Write(message);
        }

        public override void WriteLine(string? message)
        {
            _writer.WriteLine(message);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _writer.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}

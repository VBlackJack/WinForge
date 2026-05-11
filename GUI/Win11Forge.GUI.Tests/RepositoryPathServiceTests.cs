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
using Win11Forge.GUI.Configuration;
using Win11Forge.GUI.Services.PowerShell;

namespace Win11Forge.GUI.Tests;

public class RepositoryPathServiceTests
{
    [Fact]
    public void Constructor_ShouldResolveExposedPaths()
    {
        using var workspace = new TestWorkspace();
        var defaultProfiles = Path.Combine(
            workspace.RepositoryRoot,
            Win11ForgePathNames.ProfilesDirectoryName,
            Win11ForgePathNames.DefaultProfilesDirectoryName);
        Directory.CreateDirectory(defaultProfiles);

        var service = new RepositoryPathService(workspace.RepositoryRoot, [workspace.UserDataBasePath]);

        Assert.Equal(workspace.RepositoryRoot, service.RepositoryRoot);
        Assert.Equal(Path.Combine(workspace.UserDataBasePath, Win11ForgePathNames.ProductDirectoryName), service.UserDataRoot);
        Assert.Equal(Path.Combine(service.UserDataRoot, Win11ForgePathNames.LogsDirectoryName), service.LogsDirectory);
        Assert.Equal(Path.Combine(service.UserDataRoot, Win11ForgePathNames.SettingsFileName), service.SettingsFilePath);
        Assert.Equal(Path.Combine(service.UserDataRoot, Win11ForgePathNames.DeploymentHistoryFileName), service.DeploymentHistoryFilePath);
        Assert.Equal(Path.Combine(service.UserDataRoot, Win11ForgePathNames.ProfilesDirectoryName), service.UserProfilesDirectory);
        Assert.Equal(defaultProfiles, service.DefaultProfilesDirectory);
        Assert.Equal(Path.Combine(workspace.RepositoryRoot, Win11ForgePathNames.ProfilesDirectoryName), service.LegacyInstallProfilesDirectory);
        Assert.False(service.IsUserDataFallbackActive);
    }

    [Fact]
    public void Constructor_WhenPreferredUserDataIsUnavailable_ShouldUseFallbackAndEmitDebug()
    {
        using var workspace = new TestWorkspace();
        var blockedBasePath = Path.Combine(workspace.RootPath, "blocked");
        File.WriteAllText(blockedBasePath, "not a directory");
        var fallbackBasePath = Path.Combine(workspace.RootPath, "fallback");
        using var listener = new CapturingTraceListener();
        Trace.Listeners.Add(listener);

        try
        {
            var service = new RepositoryPathService(workspace.RepositoryRoot, [blockedBasePath, fallbackBasePath]);

            Assert.True(service.IsUserDataFallbackActive);
            Assert.Equal(Path.Combine(fallbackBasePath, Win11ForgePathNames.ProductDirectoryName), service.UserDataRoot);
            Assert.Contains(
                "user data fallback active",
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
        using var workspace = new TestWorkspace();
        var legacyProfilesDirectory = Path.Combine(
            workspace.RepositoryRoot,
            Win11ForgePathNames.ProfilesDirectoryName);
        Directory.CreateDirectory(legacyProfilesDirectory);

        var service = new RepositoryPathService(workspace.RepositoryRoot, [workspace.UserDataBasePath]);

        Assert.Equal(service.LegacyInstallProfilesDirectory, service.DefaultProfilesDirectory);
        Assert.Equal(legacyProfilesDirectory, service.DefaultProfilesDirectory);
        Assert.True(Directory.Exists(service.DefaultProfilesDirectory));
    }

    [Fact]
    public void DefaultProfilesDirectory_WhenDefaultsFolderExists_ShouldReturnDefaultsPath()
    {
        using var workspace = new TestWorkspace();
        var defaultsDirectory = Path.Combine(
            workspace.RepositoryRoot,
            Win11ForgePathNames.ProfilesDirectoryName,
            Win11ForgePathNames.DefaultProfilesDirectoryName);
        Directory.CreateDirectory(defaultsDirectory);

        var service = new RepositoryPathService(workspace.RepositoryRoot, [workspace.UserDataBasePath]);

        Assert.Equal(defaultsDirectory, service.DefaultProfilesDirectory);
        Assert.EndsWith(Win11ForgePathNames.DefaultProfilesDirectoryName, service.DefaultProfilesDirectory);
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

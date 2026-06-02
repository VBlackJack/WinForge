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
using Win11Forge.GUI.Configuration;

namespace Win11Forge.GUI.Tests;

internal sealed class TestWorkspace : IDisposable
{
    private const int DeleteRetryCount = 5;
    private const int DeleteRetryDelayMilliseconds = 50;

    public TestWorkspace()
    {
        RootPath = Path.Combine(
            Path.GetTempPath(),
            "Win11Forge.Tests",
            Guid.NewGuid().ToString("N"));
        RepositoryRoot = Path.Combine(RootPath, "repo");
        UserDataBasePath = Path.Combine(RootPath, "user-data-base");
        UserDataRoot = Path.Combine(UserDataBasePath, Win11ForgePathNames.ProductDirectoryName);
        UserProfilesDirectory = Path.Combine(UserDataRoot, Win11ForgePathNames.ProfilesDirectoryName);

        Directory.CreateDirectory(RepositoryRoot);
        Directory.CreateDirectory(UserDataBasePath);
    }

    public string RootPath { get; }

    public string RepositoryRoot { get; }

    public string UserDataBasePath { get; }

    public string UserDataRoot { get; }

    public string UserProfilesDirectory { get; }

    public void Dispose()
    {
        for (int attempt = 1; attempt <= DeleteRetryCount; attempt++)
        {
            try
            {
                if (Directory.Exists(RootPath))
                {
                    Directory.Delete(RootPath, recursive: true);
                }

                return;
            }
            catch (IOException) when (attempt < DeleteRetryCount)
            {
                Thread.Sleep(DeleteRetryDelayMilliseconds);
            }
            catch (UnauthorizedAccessException) when (attempt < DeleteRetryCount)
            {
                Thread.Sleep(DeleteRetryDelayMilliseconds);
            }
        }
    }
}

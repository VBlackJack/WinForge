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

#nullable enable

namespace Win11Forge.GUI.Models;

/// <summary>
/// Defines the strategy for handling application updates during deployment.
/// </summary>
public enum UpdateStrategy
{
    /// <summary>
    /// Skip applications that are already installed.
    /// Default behavior for initial deployments.
    /// </summary>
    SkipInstalled,

    /// <summary>
    /// Update applications that have newer versions available.
    /// Checks for updates and installs them if found.
    /// </summary>
    UpdateIfAvailable,

    /// <summary>
    /// Always reinstall applications, even if already installed.
    /// Useful for repair scenarios.
    /// </summary>
    ForceReinstall,

    /// <summary>
    /// Only install applications that are not present.
    /// Skip both installed apps and those with updates available.
    /// </summary>
    InstallMissingOnly,

    /// <summary>
    /// Automatic strategy based on application state.
    /// - Not installed: Install
    /// - Installed with update: Update
    /// - Installed (current): Skip
    /// </summary>
    Auto
}

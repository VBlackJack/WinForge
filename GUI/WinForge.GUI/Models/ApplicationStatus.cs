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
/// Represents the installation status of an application.
/// </summary>
public enum ApplicationStatus
{
    /// <summary>Application is pending installation.</summary>
    Pending,

    /// <summary>Application is currently being installed.</summary>
    Installing,

    /// <summary>Application was successfully installed.</summary>
    Installed,

    /// <summary>Application installation failed.</summary>
    Failed,

    /// <summary>Application was skipped (e.g., environment restriction).</summary>
    Skipped,

    /// <summary>Application was already installed before deployment.</summary>
    AlreadyInstalled,

    /// <summary>Application is currently being uninstalled.</summary>
    Uninstalling,

    /// <summary>Application was successfully uninstalled.</summary>
    Uninstalled,

    /// <summary>An update is available for the installed application.</summary>
    UpdateAvailable,

    /// <summary>Application is currently being updated.</summary>
    Updating
}

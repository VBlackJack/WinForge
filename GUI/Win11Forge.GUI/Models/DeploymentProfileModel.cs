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

using System.Collections.ObjectModel;

namespace Win11Forge.GUI.Models;

/// <summary>
/// Represents a deployment profile with its applications and configuration.
/// </summary>
public class DeploymentProfileModel
{
    /// <summary>Profile name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Profile description.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Profile version.</summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>List of parent profiles this profile inherits from.</summary>
    public List<string> InheritedFrom { get; set; } = [];

    /// <summary>Applications included in this profile.</summary>
    public ObservableCollection<ApplicationModel> Applications { get; set; } = [];

    /// <summary>Total number of applications.</summary>
    public int TotalApplications => Applications.Count;

    /// <summary>Number of required applications.</summary>
    public int RequiredApplications => Applications.Count(a => a.IsRequired);
}

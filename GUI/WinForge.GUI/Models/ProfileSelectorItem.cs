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

namespace WinForge.GUI.Models;

/// <summary>
/// Display item for the Applications profile selector.
/// </summary>
public sealed class ProfileSelectorItem
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProfileSelectorItem"/> class.
    /// </summary>
    /// <param name="displayName">Text shown in the selector.</param>
    /// <param name="profileName">Underlying profile name, or null for manual custom selection.</param>
    public ProfileSelectorItem(string displayName, string? profileName)
    {
        DisplayName = displayName;
        ProfileName = profileName;
    }

    /// <summary>
    /// Text shown in the selector.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Underlying profile name, or null for manual custom selection.
    /// </summary>
    public string? ProfileName { get; }

    /// <summary>
    /// Indicates the manual selection entry.
    /// </summary>
    public bool IsCustom => string.IsNullOrEmpty(ProfileName);
}

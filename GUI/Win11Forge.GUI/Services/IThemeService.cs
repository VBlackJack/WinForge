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

using Win11Forge.GUI.Models;

namespace Win11Forge.GUI.Services;

/// <summary>
/// Applies application themes on the UI thread and exposes theme change notifications.
/// </summary>
public interface IThemeService
{
    /// <summary>
    /// Gets the canonical name of the currently applied theme.
    /// </summary>
    string CurrentTheme { get; }

    /// <summary>
    /// Gets a monotonic counter incremented after each successful theme swap.
    /// </summary>
    int ThemeRevision { get; }

    /// <summary>
    /// Gets the available theme catalogue.
    /// </summary>
    IReadOnlyList<ThemeDescriptor> AvailableThemes { get; }

    /// <summary>
    /// Gets the canonical name of the currently applied accent tint.
    /// </summary>
    string CurrentAccentTint { get; }

    /// <summary>
    /// Gets the available accent tint catalogue.
    /// </summary>
    IReadOnlyList<AccentTintDescriptor> AvailableAccentTints { get; }

    /// <summary>
    /// Raised on the UI thread after a successful theme swap.
    /// </summary>
    event Action<string>? ThemeChanged;

    /// <summary>
    /// Applies a theme by canonical name. Unknown values fall back to the default theme.
    /// </summary>
    void ApplyTheme(string? themeName);

    /// <summary>
    /// Applies an accent tint by canonical name. Unknown values fall back to the default tint.
    /// </summary>
    void ApplyAccentTint(string? accentTintName);
}

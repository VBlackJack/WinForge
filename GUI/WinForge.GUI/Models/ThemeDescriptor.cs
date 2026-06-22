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
/// Immutable descriptor of a theme registered with <c>IThemeService</c>.
/// </summary>
/// <param name="Name">Canonical theme name used as the persisted settings value.</param>
/// <param name="IsDark">Whether the theme is dark and should drive dark Mica and DWM mode.</param>
/// <param name="ResourceUri">URI of the ResourceDictionary to merge, or <see langword="null"/> for WPF-UI native themes.</param>
/// <param name="DisplayKey">Resource key in <c>Resources.resx</c> for the localized display name.</param>
public sealed record ThemeDescriptor(
    string Name,
    bool IsDark,
    Uri? ResourceUri,
    string DisplayKey);

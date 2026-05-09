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

namespace Win11Forge.GUI.Resources;

/// <summary>
/// Canonical theme name constants.
/// </summary>
public static class ThemeNames
{
    public const string Light = "Light";
    public const string DraculaPro = "DraculaPro";
    public const string Alucard = "Alucard";
    public const string Blade = "Blade";
    public const string Buffy = "Buffy";
    public const string Lincoln = "Lincoln";
    public const string Morbius = "Morbius";
    public const string VanHelsing = "VanHelsing";

    /// <summary>
    /// Fallback theme for fresh installs and unknown values.
    /// </summary>
    public const string Default = Light;

    /// <summary>
    /// Relative resource path prefix for Dracula theme dictionaries.
    /// </summary>
    public const string DraculaResourcePathPrefix = "Themes/Dracula/";
}

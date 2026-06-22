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

using System.Windows.Data;

namespace Win11Forge.GUI.Resources;

/// <summary>
/// Provides XAML binding to localized resources.
/// Usage: {loc:Loc App_Title}
/// </summary>
public class LocExtension : Binding
{
    public LocExtension(string key) : base($"[{key}]")
    {
        Mode = BindingMode.OneWay;
        Source = LocalizationProvider.Instance;
    }
}

/// <summary>
/// Singleton provider for localized resource access.
/// Implements indexer for XAML binding support.
/// </summary>
public class LocalizationProvider
{
    private static readonly Lazy<LocalizationProvider> _instance = new(() => new LocalizationProvider());

    public static LocalizationProvider Instance => _instance.Value;

    private LocalizationProvider() { }

    /// <summary>
    /// Gets localized string by key.
    /// </summary>
    /// <param name="key">Resource key name</param>
    /// <returns>Localized string or key if not found</returns>
    public string this[string key]
    {
        get
        {
            string? value = Resources.ResourceManager.GetString(key);
            return value ?? $"[{key}]";
        }
    }
}

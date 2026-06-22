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

using System.Collections;
using System.Text.Json;

namespace WinForge.GUI.Helpers;

/// <summary>
/// Provides helper methods for JSON and Hashtable value extraction.
/// </summary>
internal static class JsonHelper
{
    /// <summary>
    /// Safely gets a string from a JsonElement by property name.
    /// </summary>
    /// <param name="element">The JSON element to read from.</param>
    /// <param name="propertyName">The property name to retrieve.</param>
    /// <returns>The string value or null if not found or not a string.</returns>
    public static string? GetJsonString(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out JsonElement prop) &&
            prop.ValueKind == JsonValueKind.String)
        {
            return prop.GetString();
        }
        return null;
    }

    /// <summary>
    /// Safely gets a boolean from a JsonElement by property name.
    /// </summary>
    /// <param name="root">The JSON element to read from.</param>
    /// <param name="propertyName">The property name to retrieve.</param>
    /// <returns>True if the property exists and is true, false otherwise.</returns>
    public static bool GetJsonBool(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out JsonElement prop) && prop.ValueKind == JsonValueKind.True;
    }

    /// <summary>
    /// Safely gets an integer from a JsonElement by property name.
    /// </summary>
    /// <param name="element">The JSON element to read from.</param>
    /// <param name="propertyName">The property name to retrieve.</param>
    /// <param name="defaultValue">The default value if not found.</param>
    /// <returns>The integer value or the default value if not found or not a number.</returns>
    public static int GetJsonInt(JsonElement element, string propertyName, int defaultValue = 0)
    {
        if (element.TryGetProperty(propertyName, out JsonElement prop) &&
            prop.ValueKind == JsonValueKind.Number)
        {
            return prop.GetInt32();
        }
        return defaultValue;
    }

    /// <summary>
    /// Safely gets a string value from a Hashtable.
    /// </summary>
    /// <param name="ht">The hashtable to read from.</param>
    /// <param name="key">The key to retrieve.</param>
    /// <returns>The string value or empty string if not found.</returns>
    public static string GetStringValue(Hashtable ht, string key)
    {
        return ht[key]?.ToString() ?? string.Empty;
    }

    /// <summary>
    /// Safely gets a boolean value from a Hashtable.
    /// </summary>
    /// <param name="ht">The hashtable to read from.</param>
    /// <param name="key">The key to retrieve.</param>
    /// <returns>The boolean value or false if not found or not a boolean.</returns>
    public static bool GetBoolValue(Hashtable ht, string key)
    {
        if (ht[key] is bool value)
        {
            return value;
        }
        return false;
    }

    /// <summary>
    /// Safely gets an integer value from a Hashtable.
    /// </summary>
    /// <param name="ht">The hashtable to read from.</param>
    /// <param name="key">The key to retrieve.</param>
    /// <param name="defaultValue">The default value if not found.</param>
    /// <returns>The integer value or the default value if not found or not an integer.</returns>
    public static int GetIntValue(Hashtable ht, string key, int defaultValue = 0)
    {
        if (ht[key] is int value)
        {
            return value;
        }
        return defaultValue;
    }
}

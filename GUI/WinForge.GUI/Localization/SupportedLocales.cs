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

namespace WinForge.GUI.Localization;

/// <summary>
/// Single source of truth for the locales WinForge ships with.
/// </summary>
internal static class SupportedLocales
{
    /// <summary>
    /// Fallback two-letter ISO code when the configured language is unknown or empty.
    /// </summary>
    public const string Default = "en";

    private static readonly LocaleDefinition[] Locales =
    [
        new(Default, "English"),
        new("fr", "Français")
    ];

    /// <summary>
    /// All supported two-letter ISO codes. Order is the canonical UI display order.
    /// </summary>
    public static IReadOnlyList<string> Codes { get; } = Locales
        .Select(locale => locale.Code)
        .ToArray();

    /// <summary>
    /// Language display names keyed by supported two-letter ISO code.
    /// </summary>
    public static IReadOnlyDictionary<string, string> DisplayNames { get; } = Locales
        .ToDictionary(
            locale => locale.Code,
            locale => locale.DisplayName,
            StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Normalises an arbitrary culture string to a supported two-letter code.
    /// </summary>
    public static string Resolve(string? languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            return Default;
        }

        string twoLetter = languageCode
            .Split('-', StringSplitOptions.RemoveEmptyEntries)[0]
            .Trim()
            .ToLowerInvariant();

        return Codes.Contains(twoLetter) ? twoLetter : Default;
    }

    private readonly record struct LocaleDefinition(string Code, string DisplayName);
}

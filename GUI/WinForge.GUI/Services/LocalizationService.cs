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

using System.Globalization;
using System.Windows;

namespace Win11Forge.GUI.Services;

/// <summary>
/// Service for localization including RTL language support.
/// </summary>
public static class LocalizationService
{
    /// <summary>
    /// RTL language codes that require right-to-left layout.
    /// </summary>
    private static readonly HashSet<string> RtlLanguages =
    [
        "ar",  // Arabic
        "he",  // Hebrew
        "fa",  // Persian/Farsi
        "ur",  // Urdu
        "yi",  // Yiddish
        "ps",  // Pashto
        "sd",  // Sindhi
        "ug"   // Uyghur
    ];

    /// <summary>
    /// Gets the appropriate FlowDirection for the current culture.
    /// </summary>
    public static FlowDirection GetFlowDirection()
    {
        return GetFlowDirection(CultureInfo.CurrentUICulture);
    }

    /// <summary>
    /// Gets the appropriate FlowDirection for a specific culture.
    /// </summary>
    public static FlowDirection GetFlowDirection(CultureInfo culture)
    {
        if (culture == null)
        {
            return FlowDirection.LeftToRight;
        }

        // Check if the language is RTL
        string languageCode = culture.TwoLetterISOLanguageName.ToLowerInvariant();
        return RtlLanguages.Contains(languageCode)
            ? FlowDirection.RightToLeft
            : FlowDirection.LeftToRight;
    }

    /// <summary>
    /// Gets the appropriate FlowDirection for a language code.
    /// </summary>
    public static FlowDirection GetFlowDirection(string languageCode)
    {
        if (string.IsNullOrEmpty(languageCode))
        {
            return FlowDirection.LeftToRight;
        }

        string code = languageCode.ToLowerInvariant();
        if (code.Contains('-'))
        {
            code = code.Split('-')[0];
        }

        return RtlLanguages.Contains(code)
            ? FlowDirection.RightToLeft
            : FlowDirection.LeftToRight;
    }

    /// <summary>
    /// Checks if the current culture is RTL.
    /// </summary>
    public static bool IsRtl()
    {
        return GetFlowDirection() == FlowDirection.RightToLeft;
    }

    /// <summary>
    /// Checks if a specific language code is RTL.
    /// </summary>
    public static bool IsRtl(string languageCode)
    {
        return GetFlowDirection(languageCode) == FlowDirection.RightToLeft;
    }

    /// <summary>
    /// Applies the appropriate FlowDirection to a window based on current culture.
    /// </summary>
    public static void ApplyFlowDirection(Window window)
    {
        if (window != null)
        {
            window.FlowDirection = GetFlowDirection();
        }
    }

    /// <summary>
    /// Applies the appropriate FlowDirection to a framework element based on current culture.
    /// </summary>
    public static void ApplyFlowDirection(FrameworkElement element)
    {
        if (element != null)
        {
            element.FlowDirection = GetFlowDirection();
        }
    }
}

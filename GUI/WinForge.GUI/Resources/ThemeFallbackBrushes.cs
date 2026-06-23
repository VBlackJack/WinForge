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

using System.Windows.Media;

namespace WinForge.GUI.Resources;

/// <summary>
/// Last-resort ThemeForge-aligned brushes for converters and controls.
/// </summary>
internal static class ThemeFallbackBrushes
{
    internal static readonly SolidColorBrush Accent = Create(0xBD, 0x93, 0xF9);
    internal static readonly SolidColorBrush Success = Create(0x50, 0xFA, 0x7B);
    internal static readonly SolidColorBrush Warning = Create(0xFF, 0xB8, 0x6C);
    internal static readonly SolidColorBrush Error = Create(0xFF, 0x55, 0x55);
    internal static readonly SolidColorBrush Info = Create(0x8B, 0xE9, 0xFD);
    internal static readonly SolidColorBrush TextDisabled = Create(0xB3, 0xBB, 0xD6, 0.65);
    internal static readonly SolidColorBrush Transparent = Create(0x00, 0x00, 0x00, 0.0);

    internal static readonly SolidColorBrush SuccessSubtleBackground = Create(0x50, 0xFA, 0x7B, 0.12);
    internal static readonly SolidColorBrush WarningSubtleBackground = Create(0xFF, 0xB8, 0x6C, 0.16);
    internal static readonly SolidColorBrush ErrorSubtleBackground = Create(0xFF, 0x55, 0x55, 0.14);
    internal static readonly SolidColorBrush InfoSubtleBackground = Create(0x8B, 0xE9, 0xFD, 0.12);
    internal static readonly SolidColorBrush NeutralSubtleBackground = Create(0xB3, 0xBB, 0xD6, 0.12);
    internal static readonly SolidColorBrush CriticalBackground = Create(0xFF, 0x55, 0x55, 0.30);

    internal static readonly SolidColorBrush AlreadyInstalled = Create(0x50, 0xFA, 0x7B, 0.75);

    private static SolidColorBrush Create(byte red, byte green, byte blue, double opacity = 1.0)
    {
        SolidColorBrush brush = new SolidColorBrush(Color.FromRgb(red, green, blue))
        {
            Opacity = opacity
        };
        brush.Freeze();
        return brush;
    }
}

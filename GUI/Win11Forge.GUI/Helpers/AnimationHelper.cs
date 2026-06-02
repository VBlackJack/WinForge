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

using System.Windows;
using System.Windows.Media.Animation;
using Microsoft.Win32;

namespace Win11Forge.GUI.Helpers;

/// <summary>
/// Helper class for animation management with reduced motion support.
/// Respects user accessibility preferences for reduced motion.
/// </summary>
public static class AnimationHelper
{
    private static bool? _reducedMotion;

    /// <summary>
    /// Gets whether reduced motion is preferred by the user.
    /// Checks Windows system settings for animation preferences.
    /// </summary>
    public static bool ReducedMotion
    {
        get
        {
            _reducedMotion ??= DetectReducedMotion();
            return _reducedMotion.Value;
        }
    }

    /// <summary>
    /// Refreshes the reduced motion setting from system preferences.
    /// Call this when system settings may have changed.
    /// </summary>
    public static void RefreshReducedMotionSetting()
    {
        _reducedMotion = DetectReducedMotion();
    }

    /// <summary>
    /// Gets an adjusted duration based on reduced motion preference.
    /// Returns zero duration if reduced motion is enabled.
    /// </summary>
    /// <param name="normalDuration">The normal animation duration.</param>
    /// <returns>Zero duration if reduced motion is enabled, otherwise the normal duration.</returns>
    public static Duration GetDuration(Duration normalDuration)
    {
        return ReducedMotion ? new Duration(TimeSpan.Zero) : normalDuration;
    }

    /// <summary>
    /// Gets an adjusted duration based on reduced motion preference.
    /// </summary>
    /// <param name="milliseconds">The normal animation duration in milliseconds.</param>
    /// <returns>Zero if reduced motion is enabled, otherwise the specified duration.</returns>
    public static Duration GetDuration(double milliseconds)
    {
        return ReducedMotion
            ? new Duration(TimeSpan.Zero)
            : new Duration(TimeSpan.FromMilliseconds(milliseconds));
    }

    /// <summary>
    /// Gets an adjusted TimeSpan based on reduced motion preference.
    /// </summary>
    /// <param name="normalTimeSpan">The normal animation time span.</param>
    /// <returns>Zero if reduced motion is enabled, otherwise the normal time span.</returns>
    public static TimeSpan GetTimeSpan(TimeSpan normalTimeSpan)
    {
        return ReducedMotion ? TimeSpan.Zero : normalTimeSpan;
    }

    /// <summary>
    /// Creates a fade animation that respects reduced motion settings.
    /// </summary>
    /// <param name="from">Starting opacity (0.0 to 1.0).</param>
    /// <param name="to">Ending opacity (0.0 to 1.0).</param>
    /// <param name="durationMs">Animation duration in milliseconds.</param>
    /// <returns>A configured DoubleAnimation for opacity.</returns>
    public static DoubleAnimation CreateFadeAnimation(double from, double to, double durationMs = 300)
    {
        DoubleAnimation animation = new DoubleAnimation
        {
            From = from,
            To = to,
            Duration = GetDuration(durationMs),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        if (ReducedMotion)
        {
            animation.From = to;
        }

        return animation;
    }

    /// <summary>
    /// Creates a slide animation that respects reduced motion settings.
    /// </summary>
    /// <param name="from">Starting margin.</param>
    /// <param name="to">Ending margin.</param>
    /// <param name="durationMs">Animation duration in milliseconds.</param>
    /// <returns>A configured ThicknessAnimation for margin.</returns>
    public static ThicknessAnimation CreateSlideAnimation(Thickness from, Thickness to, double durationMs = 300)
    {
        ThicknessAnimation animation = new ThicknessAnimation
        {
            From = ReducedMotion ? to : from,
            To = to,
            Duration = GetDuration(durationMs),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        return animation;
    }

    /// <summary>
    /// Applies a fade-in animation to a framework element.
    /// </summary>
    /// <param name="element">The element to animate.</param>
    /// <param name="durationMs">Animation duration in milliseconds.</param>
    public static void FadeIn(FrameworkElement element, double durationMs = 300)
    {
        if (ReducedMotion)
        {
            element.Opacity = 1;
            return;
        }

        DoubleAnimation animation = CreateFadeAnimation(0, 1, durationMs);
        element.BeginAnimation(UIElement.OpacityProperty, animation);
    }

    /// <summary>
    /// Applies a fade-out animation to a framework element.
    /// </summary>
    /// <param name="element">The element to animate.</param>
    /// <param name="durationMs">Animation duration in milliseconds.</param>
    public static void FadeOut(FrameworkElement element, double durationMs = 300)
    {
        if (ReducedMotion)
        {
            element.Opacity = 0;
            return;
        }

        DoubleAnimation animation = CreateFadeAnimation(1, 0, durationMs);
        element.BeginAnimation(UIElement.OpacityProperty, animation);
    }

    /// <summary>
    /// Detects whether the user prefers reduced motion based on Windows settings.
    /// Checks multiple sources: SystemParametersInfo and Registry.
    /// </summary>
    private static bool DetectReducedMotion()
    {
        try
        {
            // Check Windows animation setting via registry
            // HKEY_CURRENT_USER\Control Panel\Desktop\WindowMetrics
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop\WindowMetrics");
            if (key != null)
            {
                string? minAnimate = key.GetValue("MinAnimate") as string;
                if (minAnimate == "0")
                {
                    return true;
                }
            }

            // Check Visual Effects settings
            // HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects
            using RegistryKey? visualKey = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects");
            if (visualKey != null)
            {
                object? visualFx = visualKey.GetValue("VisualFXSetting");
                if (visualFx is int setting && setting == 2)
                {
                    // 2 = "Adjust for best performance" (minimal animations)
                    return true;
                }
            }

            // Check SystemParametersInfo for UI effects
            // HKEY_CURRENT_USER\Control Panel\Desktop\UserPreferencesMask
            using RegistryKey? desktopKey = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop");
            if (desktopKey != null)
            {
                byte[]? userPrefs = desktopKey.GetValue("UserPreferencesMask") as byte[];
                if (userPrefs != null && userPrefs.Length > 0)
                {
                    // Bit 1 of first byte controls menu animations
                    // Bit 2 controls combo box animations
                    // If animations are disabled, prefer reduced motion
                    if ((userPrefs[0] & 0x02) == 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
        catch
        {
            // If we can't determine preferences, default to allowing animations
            return false;
        }
    }
}

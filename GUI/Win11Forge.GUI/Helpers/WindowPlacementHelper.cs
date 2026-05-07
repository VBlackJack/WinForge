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

using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using Win11Forge.GUI.Services;

namespace Win11Forge.GUI.Helpers;

/// <summary>
/// Calculates and persists safe main-window placement.
/// </summary>
internal static class WindowPlacementHelper
{
    internal const double DefaultWindowWidth = 1680;
    internal const double DefaultWindowHeight = 960;

    private const double WorkAreaPadding = 24;
    private const double MinimumVisibleLength = 64;

    internal static WindowPlacementDecision CalculatePlacement(
        WindowPlacementSettings? savedPlacement,
        IReadOnlyList<Rect> workAreas,
        Size defaultSize,
        Size minimumSize)
    {
        var validWorkAreas = workAreas
            .Where(IsUsableRect)
            .ToArray();

        if (validWorkAreas.Length == 0)
        {
            validWorkAreas =
            [
                new Rect(
                    0,
                    0,
                    Math.Max(1, Math.Max(defaultSize.Width, minimumSize.Width)),
                    Math.Max(1, Math.Max(defaultSize.Height, minimumSize.Height)))
            ];
        }

        var primaryWorkArea = validWorkAreas[0];
        var requestedState = ParseWindowState(savedPlacement?.WindowState);
        var defaultBounds = CenterInWorkArea(
            ClampSize(defaultSize, primaryWorkArea, minimumSize),
            primaryWorkArea);

        if (savedPlacement is null)
        {
            return new WindowPlacementDecision(defaultBounds, WindowState.Normal);
        }

        if (TryCreateSavedBounds(savedPlacement, out var savedBounds))
        {
            var targetWorkArea = FindBestIntersectingWorkArea(savedBounds, validWorkAreas);
            if (targetWorkArea.HasValue && IsMeaningfullyVisible(savedBounds, targetWorkArea.Value))
            {
                return new WindowPlacementDecision(
                    ClampBoundsToWorkArea(savedBounds, targetWorkArea.Value, minimumSize),
                    requestedState);
            }
        }

        return new WindowPlacementDecision(defaultBounds, requestedState);
    }

    internal static void ApplyStartupPlacement(Window window, WindowPlacementSettings? savedPlacement)
    {
        ArgumentNullException.ThrowIfNull(window);

        var workAreas = GetMonitorWorkAreas(window);
        var decision = CalculatePlacement(
            savedPlacement,
            workAreas,
            new Size(DefaultWindowWidth, DefaultWindowHeight),
            new Size(window.MinWidth, window.MinHeight));

        var targetWorkArea = FindBestIntersectingWorkArea(decision.Bounds, workAreas)
            ?? workAreas.FirstOrDefault();

        if (IsUsableRect(targetWorkArea))
        {
            var maxSize = GetAvailableSize(targetWorkArea);
            window.MinWidth = Math.Min(window.MinWidth, maxSize.Width);
            window.MinHeight = Math.Min(window.MinHeight, maxSize.Height);
        }

        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.WindowState = WindowState.Normal;
        window.Left = decision.Bounds.Left;
        window.Top = decision.Bounds.Top;
        window.Width = decision.Bounds.Width;
        window.Height = decision.Bounds.Height;

        if (decision.WindowState == WindowState.Maximized)
        {
            window.SourceInitialized += (_, _) => window.WindowState = WindowState.Maximized;
        }
    }

    internal static WindowPlacementSettings CapturePlacement(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        var persistedState = window.WindowState == WindowState.Maximized
            ? WindowState.Maximized
            : WindowState.Normal;

        var bounds = window.WindowState == WindowState.Normal
            ? new Rect(window.Left, window.Top, window.Width, window.Height)
            : window.RestoreBounds;

        if (!IsUsableRect(bounds))
        {
            bounds = new Rect(window.Left, window.Top, window.Width, window.Height);
        }

        return new WindowPlacementSettings
        {
            Left = bounds.Left,
            Top = bounds.Top,
            Width = bounds.Width,
            Height = bounds.Height,
            WindowState = persistedState.ToString()
        };
    }

    private static WindowState ParseWindowState(string? value)
    {
        return Enum.TryParse<WindowState>(value, ignoreCase: true, out var state)
            && state == WindowState.Maximized
                ? WindowState.Maximized
                : WindowState.Normal;
    }

    private static bool TryCreateSavedBounds(WindowPlacementSettings placement, out Rect bounds)
    {
        bounds = new Rect(placement.Left, placement.Top, placement.Width, placement.Height);
        return IsUsableRect(bounds);
    }

    private static Rect? FindBestIntersectingWorkArea(Rect bounds, IReadOnlyList<Rect> workAreas)
    {
        Rect? bestWorkArea = null;
        var bestArea = 0d;

        foreach (var workArea in workAreas)
        {
            if (!IsUsableRect(workArea))
            {
                continue;
            }

            var intersection = Rect.Intersect(bounds, workArea);
            if (intersection.IsEmpty)
            {
                continue;
            }

            var area = intersection.Width * intersection.Height;
            if (area > bestArea)
            {
                bestArea = area;
                bestWorkArea = workArea;
            }
        }

        return bestWorkArea;
    }

    private static bool IsMeaningfullyVisible(Rect bounds, Rect workArea)
    {
        var intersection = Rect.Intersect(bounds, workArea);
        if (intersection.IsEmpty)
        {
            return false;
        }

        var requiredWidth = Math.Min(MinimumVisibleLength, Math.Min(bounds.Width, workArea.Width));
        var requiredHeight = Math.Min(MinimumVisibleLength, Math.Min(bounds.Height, workArea.Height));

        return intersection.Width >= requiredWidth
            && intersection.Height >= requiredHeight;
    }

    private static Rect ClampBoundsToWorkArea(Rect bounds, Rect workArea, Size minimumSize)
    {
        var size = ClampSize(new Size(bounds.Width, bounds.Height), workArea, minimumSize);
        var availableSize = GetAvailableSize(workArea);
        var paddingX = workArea.Width > availableSize.Width ? WorkAreaPadding : 0;
        var paddingY = workArea.Height > availableSize.Height ? WorkAreaPadding : 0;

        var minLeft = workArea.Left + paddingX;
        var maxLeft = workArea.Right - paddingX - size.Width;
        var minTop = workArea.Top + paddingY;
        var maxTop = workArea.Bottom - paddingY - size.Height;

        var left = maxLeft >= minLeft
            ? Math.Clamp(bounds.Left, minLeft, maxLeft)
            : workArea.Left + Math.Max(0, (workArea.Width - size.Width) / 2);
        var top = maxTop >= minTop
            ? Math.Clamp(bounds.Top, minTop, maxTop)
            : workArea.Top + Math.Max(0, (workArea.Height - size.Height) / 2);

        return new Rect(left, top, size.Width, size.Height);
    }

    private static Rect CenterInWorkArea(Size size, Rect workArea)
    {
        return new Rect(
            workArea.Left + Math.Max(0, (workArea.Width - size.Width) / 2),
            workArea.Top + Math.Max(0, (workArea.Height - size.Height) / 2),
            size.Width,
            size.Height);
    }

    private static Size ClampSize(Size requestedSize, Rect workArea, Size minimumSize)
    {
        var availableSize = GetAvailableSize(workArea);
        var minimumWidth = Math.Min(SanitizeLength(minimumSize.Width, 1), availableSize.Width);
        var minimumHeight = Math.Min(SanitizeLength(minimumSize.Height, 1), availableSize.Height);

        var width = Math.Clamp(
            SanitizeLength(requestedSize.Width, minimumWidth),
            minimumWidth,
            availableSize.Width);
        var height = Math.Clamp(
            SanitizeLength(requestedSize.Height, minimumHeight),
            minimumHeight,
            availableSize.Height);

        return new Size(width, height);
    }

    private static Size GetAvailableSize(Rect workArea)
    {
        var width = workArea.Width > WorkAreaPadding * 2
            ? workArea.Width - WorkAreaPadding * 2
            : workArea.Width;
        var height = workArea.Height > WorkAreaPadding * 2
            ? workArea.Height - WorkAreaPadding * 2
            : workArea.Height;

        return new Size(Math.Max(1, width), Math.Max(1, height));
    }

    private static double SanitizeLength(double value, double fallback)
    {
        return double.IsFinite(value) && value > 0
            ? value
            : fallback;
    }

    private static bool IsUsableRect(Rect rect)
    {
        return double.IsFinite(rect.Left)
            && double.IsFinite(rect.Top)
            && double.IsFinite(rect.Width)
            && double.IsFinite(rect.Height)
            && rect.Width > 0
            && rect.Height > 0;
    }

    private static IReadOnlyList<Rect> GetMonitorWorkAreas(Window window)
    {
        try
        {
            var dpi = VisualTreeHelper.GetDpi(window);
            var scaleX = dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1;
            var scaleY = dpi.DpiScaleY > 0 ? dpi.DpiScaleY : 1;
            var primary = new List<Rect>();
            var secondary = new List<Rect>();

            NativeMethods.EnumDisplayMonitors(
                IntPtr.Zero,
                IntPtr.Zero,
                (IntPtr monitor, IntPtr _, ref NativeMethods.NativeRect __, IntPtr ___) =>
                {
                    var info = new NativeMethods.MonitorInfo
                    {
                        Size = Marshal.SizeOf<NativeMethods.MonitorInfo>()
                    };

                    if (!NativeMethods.GetMonitorInfo(monitor, ref info))
                    {
                        return true;
                    }

                    var workArea = ToDeviceIndependentRect(info.WorkArea, scaleX, scaleY);
                    if (info.IsPrimary)
                    {
                        primary.Add(workArea);
                    }
                    else
                    {
                        secondary.Add(workArea);
                    }

                    return true;
                },
                IntPtr.Zero);

            var workAreas = primary.Concat(secondary).Where(IsUsableRect).ToArray();
            if (workAreas.Length > 0)
            {
                return workAreas;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to enumerate monitor work areas: {ex.Message}");
        }

        return [SystemParameters.WorkArea];
    }

    private static Rect ToDeviceIndependentRect(
        NativeMethods.NativeRect rect,
        double scaleX,
        double scaleY)
    {
        return new Rect(
            rect.Left / scaleX,
            rect.Top / scaleY,
            (rect.Right - rect.Left) / scaleX,
            (rect.Bottom - rect.Top) / scaleY);
    }

    private static class NativeMethods
    {
        private const int MonitorInfoPrimary = 0x00000001;

        public delegate bool MonitorEnumProc(
            IntPtr monitor,
            IntPtr deviceContext,
            ref NativeRect monitorRect,
            IntPtr data);

        [DllImport("user32.dll")]
        public static extern bool EnumDisplayMonitors(
            IntPtr deviceContext,
            IntPtr clipRect,
            MonitorEnumProc callback,
            IntPtr data);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo monitorInfo);

        [StructLayout(LayoutKind.Sequential)]
        public struct MonitorInfo
        {
            public int Size;
            public NativeRect MonitorArea;
            public NativeRect WorkArea;
            public int Flags;

            public bool IsPrimary => (Flags & MonitorInfoPrimary) != 0;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
    }
}

internal readonly record struct WindowPlacementDecision(Rect Bounds, WindowState WindowState);

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
using WinForge.GUI.Services;

namespace WinForge.GUI.Helpers;

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
        Rect[] validWorkAreas = workAreas
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

        Rect primaryWorkArea = validWorkAreas[0];
        WindowState requestedState = ParseWindowState(savedPlacement?.WindowState);
        Rect defaultBounds = CenterInWorkArea(
            ClampSize(defaultSize, primaryWorkArea, minimumSize),
            primaryWorkArea);

        if (savedPlacement is null)
        {
            return new WindowPlacementDecision(defaultBounds, WindowState.Normal);
        }

        if (TryCreateSavedBounds(savedPlacement, out Rect savedBounds))
        {
            Rect? targetWorkArea = FindBestIntersectingWorkArea(savedBounds, validWorkAreas);
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

        IReadOnlyList<Rect> workAreas = GetMonitorWorkAreas(window);
        WindowPlacementDecision decision = CalculatePlacement(
            savedPlacement,
            workAreas,
            new Size(DefaultWindowWidth, DefaultWindowHeight),
            new Size(window.MinWidth, window.MinHeight));

        Rect targetWorkArea = FindBestIntersectingWorkArea(decision.Bounds, workAreas)
            ?? workAreas.FirstOrDefault();

        if (IsUsableRect(targetWorkArea))
        {
            Size maxSize = GetAvailableSize(targetWorkArea);
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

        WindowState persistedState = window.WindowState == WindowState.Maximized
            ? WindowState.Maximized
            : WindowState.Normal;

        Rect bounds = window.WindowState == WindowState.Normal
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
        return Enum.TryParse<WindowState>(value, ignoreCase: true, out WindowState state)
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
        double bestArea = 0d;

        foreach (Rect workArea in workAreas)
        {
            if (!IsUsableRect(workArea))
            {
                continue;
            }

            Rect intersection = Rect.Intersect(bounds, workArea);
            if (intersection.IsEmpty)
            {
                continue;
            }

            double area = intersection.Width * intersection.Height;
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
        Rect intersection = Rect.Intersect(bounds, workArea);
        if (intersection.IsEmpty)
        {
            return false;
        }

        double requiredWidth = Math.Min(MinimumVisibleLength, Math.Min(bounds.Width, workArea.Width));
        double requiredHeight = Math.Min(MinimumVisibleLength, Math.Min(bounds.Height, workArea.Height));

        return intersection.Width >= requiredWidth
            && intersection.Height >= requiredHeight;
    }

    private static Rect ClampBoundsToWorkArea(Rect bounds, Rect workArea, Size minimumSize)
    {
        Size size = ClampSize(new Size(bounds.Width, bounds.Height), workArea, minimumSize);
        Size availableSize = GetAvailableSize(workArea);
        double paddingX = workArea.Width > availableSize.Width ? WorkAreaPadding : 0;
        double paddingY = workArea.Height > availableSize.Height ? WorkAreaPadding : 0;

        double minLeft = workArea.Left + paddingX;
        double maxLeft = workArea.Right - paddingX - size.Width;
        double minTop = workArea.Top + paddingY;
        double maxTop = workArea.Bottom - paddingY - size.Height;

        double left = maxLeft >= minLeft
            ? Math.Clamp(bounds.Left, minLeft, maxLeft)
            : workArea.Left + Math.Max(0, (workArea.Width - size.Width) / 2);
        double top = maxTop >= minTop
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
        Size availableSize = GetAvailableSize(workArea);
        double minimumWidth = Math.Min(SanitizeLength(minimumSize.Width, 1), availableSize.Width);
        double minimumHeight = Math.Min(SanitizeLength(minimumSize.Height, 1), availableSize.Height);

        double width = Math.Clamp(
            SanitizeLength(requestedSize.Width, minimumWidth),
            minimumWidth,
            availableSize.Width);
        double height = Math.Clamp(
            SanitizeLength(requestedSize.Height, minimumHeight),
            minimumHeight,
            availableSize.Height);

        return new Size(width, height);
    }

    private static Size GetAvailableSize(Rect workArea)
    {
        double width = workArea.Width > WorkAreaPadding * 2
            ? workArea.Width - WorkAreaPadding * 2
            : workArea.Width;
        double height = workArea.Height > WorkAreaPadding * 2
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
            DpiScale dpi = VisualTreeHelper.GetDpi(window);
            double scaleX = dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1;
            double scaleY = dpi.DpiScaleY > 0 ? dpi.DpiScaleY : 1;
            List<Rect> primary = new List<Rect>();
            List<Rect> secondary = new List<Rect>();

            NativeMethods.EnumDisplayMonitors(
                IntPtr.Zero,
                IntPtr.Zero,
                (IntPtr monitor, IntPtr _, ref NativeMethods.NativeRect __, IntPtr ___) =>
                {
                    NativeMethods.MonitorInfo info = new NativeMethods.MonitorInfo
                    {
                        Size = Marshal.SizeOf<NativeMethods.MonitorInfo>()
                    };

                    if (!NativeMethods.GetMonitorInfo(monitor, ref info))
                    {
                        return true;
                    }

                    Rect workArea = ToDeviceIndependentRect(info.WorkArea, scaleX, scaleY);
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

            Rect[] workAreas = primary.Concat(secondary).Where(IsUsableRect).ToArray();
            if (workAreas.Length > 0)
            {
                return workAreas;
            }
        }
        catch (Exception ex)
        {
            // Intentional Debug.WriteLine: static helper with no DI/logger reachable; best-effort monitor enumeration.
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

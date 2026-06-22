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

using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;

namespace WinForge.GUI.UITests;

internal sealed class WinForgeAppSession : IDisposable
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(20);
    private readonly Process _process;

    private WinForgeAppSession(Process process, AutomationElement mainWindow, string artifactDirectory)
    {
        _process = process;
        MainWindow = mainWindow;
        ArtifactDirectory = artifactDirectory;
    }

    public AutomationElement MainWindow { get; private set; }

    public string ArtifactDirectory { get; }

    public static WinForgeAppSession Launch()
    {
        string appAssemblyPath = ResolveAppAssemblyPath();
        string artifactDirectory = ResolveArtifactDirectory();
        Directory.CreateDirectory(artifactDirectory);

        Process process = Process.Start(new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{appAssemblyPath}\"",
            WorkingDirectory = Path.GetDirectoryName(appAssemblyPath)!,
            UseShellExecute = false
        }) ?? throw new InvalidOperationException($"Failed to launch {appAssemblyPath}.");

        WinForgeAppSession session = new WinForgeAppSession(
            process,
            WaitForMainWindow(process, DefaultTimeout),
            artifactDirectory);

        session.BringMainWindowToFront();
        session.WaitForElementByAutomationId("NavDashboard", DefaultTimeout);
        return session;
    }

    public string CaptureWindow(string name)
    {
        MainWindow = WaitForMainWindow(_process, DefaultTimeout);
        Rect rect = MainWindow.Current.BoundingRectangle;
        if (rect.IsEmpty || rect.Width <= 0 || rect.Height <= 0)
        {
            throw new InvalidOperationException("Main window has an invalid bounding rectangle.");
        }

        string path = Path.Combine(ArtifactDirectory, $"{name}.png");
        CaptureScreenRect(rect, path);
        return path;
    }

    public AutomationElement WaitForElementByAutomationId(string automationId, TimeSpan timeout)
    {
        return WaitUntil(
            () =>
            {
                RefreshMainWindow();
                return MainWindow.FindFirst(
                    TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.AutomationIdProperty, automationId));
            },
            timeout,
            $"Timed out waiting for AutomationId '{automationId}'.");
    }

    public AutomationElement WaitForElementByName(string name, TimeSpan timeout)
    {
        return WaitUntil(
            () =>
            {
                RefreshMainWindow();
                return MainWindow.FindFirst(
                    TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.NameProperty, name));
            },
            timeout,
            $"Timed out waiting for element named '{name}'.");
    }

    public void NavigateByAutomationId(string automationId)
    {
        AutomationElement element = WaitForElementByAutomationId(automationId, DefaultTimeout);
        BringMainWindowToFront();
        InvokeOrClick(element);
        WaitForIdle();
    }

    public void WaitForIdle()
    {
        try
        {
            _process.WaitForInputIdle((int)TimeSpan.FromSeconds(5).TotalMilliseconds);
        }
        catch (InvalidOperationException)
        {
            // Some WPF startup phases do not expose an input idle state.
        }

        Thread.Sleep(500);
        MainWindow = WaitForMainWindow(_process, DefaultTimeout);
    }

    private void RefreshMainWindow()
    {
        _process.Refresh();
        if (!_process.HasExited && _process.MainWindowHandle != IntPtr.Zero)
        {
            MainWindow = AutomationElement.FromHandle(_process.MainWindowHandle);
        }
    }

    private void BringMainWindowToFront()
    {
        _process.Refresh();
        if (_process.HasExited || _process.MainWindowHandle == IntPtr.Zero)
        {
            return;
        }

        nint windowHandle = _process.MainWindowHandle;
        NativeMethods.ShowWindow(windowHandle, NativeMethods.SwRestore);
        NativeMethods.SetWindowPos(
            windowHandle,
            NativeMethods.HwndTopMost,
            0,
            0,
            0,
            0,
            NativeMethods.SwpNoMove | NativeMethods.SwpNoSize | NativeMethods.SwpShowWindow);
        NativeMethods.SetWindowPos(
            windowHandle,
            NativeMethods.HwndNoTopMost,
            0,
            0,
            0,
            0,
            NativeMethods.SwpNoMove | NativeMethods.SwpNoSize | NativeMethods.SwpShowWindow);
        NativeMethods.SetForegroundWindow(windowHandle);
        MainWindow = AutomationElement.FromHandle(windowHandle);

        try
        {
            MainWindow.SetFocus();
        }
        catch (InvalidOperationException)
        {
            // WPF can temporarily reject focus while starting; foreground placement above is enough for click fallback.
        }

        Thread.Sleep(150);
    }

    public void Dispose()
    {
        if (_process.HasExited)
        {
            _process.Dispose();
            return;
        }

        try
        {
            _process.CloseMainWindow();
            if (!_process.WaitForExit((int)TimeSpan.FromSeconds(3).TotalMilliseconds))
            {
                _process.Kill(entireProcessTree: true);
            }
        }
        finally
        {
            _process.Dispose();
        }
    }

    private static void InvokeOrClick(AutomationElement element)
    {
        Rect rect = element.Current.BoundingRectangle;
        if (!rect.IsEmpty)
        {
            int x = (int)(rect.Left + rect.Width / 2);
            int y = (int)(rect.Top + rect.Height / 2);
            NativeMethods.SetCursorPos(x, y);
            NativeMethods.mouse_event(NativeMethods.MouseEventLeftDown, 0, 0, 0, UIntPtr.Zero);
            NativeMethods.mouse_event(NativeMethods.MouseEventLeftUp, 0, 0, 0, UIntPtr.Zero);
            return;
        }

        if (element.TryGetCurrentPattern(InvokePattern.Pattern, out object? invokePattern))
        {
            ((InvokePattern)invokePattern).Invoke();
            return;
        }

        if (element.TryGetCurrentPattern(SelectionItemPattern.Pattern, out object? selectionPattern))
        {
            ((SelectionItemPattern)selectionPattern).Select();
            return;
        }

        throw new InvalidOperationException($"Element '{element.Current.AutomationId}' cannot be invoked and has no clickable bounds.");
    }

    private static AutomationElement WaitForMainWindow(Process process, TimeSpan timeout)
    {
        return WaitUntil(
            () =>
            {
                process.Refresh();
                if (process.HasExited)
                {
                    throw new InvalidOperationException($"WinForge exited with code {process.ExitCode}.");
                }

                if (process.MainWindowHandle != IntPtr.Zero)
                {
                    return AutomationElement.FromHandle(process.MainWindowHandle);
                }

                PropertyCondition processIdCondition = new PropertyCondition(AutomationElement.ProcessIdProperty, process.Id);
                AutomationElementCollection windows = AutomationElement.RootElement.FindAll(TreeScope.Children, processIdCondition);
                foreach (AutomationElement window in windows)
                {
                    return window;
                }

                return null;
            },
            timeout,
            "Timed out waiting for the WinForge main window.");
    }

    private static T WaitUntil<T>(Func<T?> query, TimeSpan timeout, string failureMessage)
        where T : class
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        Exception? lastException = null;

        while (stopwatch.Elapsed < timeout)
        {
            try
            {
                T? result = query();
                if (result is not null)
                {
                    return result;
                }
            }
            catch (ElementNotAvailableException ex)
            {
                lastException = ex;
            }

            Thread.Sleep(100);
        }

        throw new TimeoutException(lastException is null
            ? failureMessage
            : $"{failureMessage} Last UIA error: {lastException.Message}");
    }

    private static string ResolveAppAssemblyPath()
    {
        string baseDirectory = AppContext.BaseDirectory;
        string localCopy = Path.Combine(baseDirectory, "WinForge.GUI.dll");
        if (File.Exists(localCopy))
        {
            return localCopy;
        }

        DirectoryInfo? directory = new DirectoryInfo(baseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(
                directory.FullName,
                "WinForge.GUI",
                "bin",
                "Release",
                "net10.0-windows",
                "WinForge.GUI.dll");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate WinForge.GUI.dll. Build the GUI project first.");
    }

    private static string ResolveArtifactDirectory()
    {
        string? configuredDirectory =
            Environment.GetEnvironmentVariable("WINFORGE_UIA_ARTIFACTS") ??
            Environment.GetEnvironmentVariable("WIN11FORGE_UIA_ARTIFACTS");
        if (!string.IsNullOrWhiteSpace(configuredDirectory))
        {
            return Path.GetFullPath(configuredDirectory);
        }

        return Path.Combine(
            Path.GetTempPath(),
            "WinForge",
            "UIA",
            DateTime.Now.ToString("yyyyMMdd-HHmmss"));
    }

    private static void CaptureScreenRect(System.Windows.Rect rect, string path)
    {
        using Bitmap bitmap = new Bitmap((int)Math.Ceiling(rect.Width), (int)Math.Ceiling(rect.Height), PixelFormat.Format32bppArgb);
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(
            (int)Math.Floor(rect.Left),
            (int)Math.Floor(rect.Top),
            0,
            0,
            bitmap.Size,
            CopyPixelOperation.SourceCopy);
        bitmap.Save(path, ImageFormat.Png);
    }

    private static class NativeMethods
    {
        public const uint MouseEventLeftDown = 0x0002;
        public const uint MouseEventLeftUp = 0x0004;
        public const int SwRestore = 9;
        public const uint SwpNoSize = 0x0001;
        public const uint SwpNoMove = 0x0002;
        public const uint SwpShowWindow = 0x0040;
        public static readonly IntPtr HwndTopMost = new(-1);
        public static readonly IntPtr HwndNoTopMost = new(-2);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);
    }
}

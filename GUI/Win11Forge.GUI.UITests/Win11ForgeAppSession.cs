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
using System.Windows.Automation;

namespace Win11Forge.GUI.UITests;

internal sealed class Win11ForgeAppSession : IDisposable
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(20);
    private readonly Process _process;

    private Win11ForgeAppSession(Process process, AutomationElement mainWindow, string artifactDirectory)
    {
        _process = process;
        MainWindow = mainWindow;
        ArtifactDirectory = artifactDirectory;
    }

    public AutomationElement MainWindow { get; private set; }

    public string ArtifactDirectory { get; }

    public static Win11ForgeAppSession Launch()
    {
        var appAssemblyPath = ResolveAppAssemblyPath();
        var artifactDirectory = ResolveArtifactDirectory();
        Directory.CreateDirectory(artifactDirectory);

        var process = Process.Start(new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{appAssemblyPath}\"",
            WorkingDirectory = Path.GetDirectoryName(appAssemblyPath)!,
            UseShellExecute = false
        }) ?? throw new InvalidOperationException($"Failed to launch {appAssemblyPath}.");

        var session = new Win11ForgeAppSession(
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
        var rect = MainWindow.Current.BoundingRectangle;
        if (rect.IsEmpty || rect.Width <= 0 || rect.Height <= 0)
        {
            throw new InvalidOperationException("Main window has an invalid bounding rectangle.");
        }

        var path = Path.Combine(ArtifactDirectory, $"{name}.png");
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
        var element = WaitForElementByAutomationId(automationId, DefaultTimeout);
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

        NativeMethods.ShowWindow(_process.MainWindowHandle, NativeMethods.SwRestore);
        NativeMethods.SetForegroundWindow(_process.MainWindowHandle);
        MainWindow = AutomationElement.FromHandle(_process.MainWindowHandle);
        Thread.Sleep(100);
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
        if (element.TryGetCurrentPattern(InvokePattern.Pattern, out var invokePattern))
        {
            ((InvokePattern)invokePattern).Invoke();
            return;
        }

        var rect = element.Current.BoundingRectangle;
        if (!rect.IsEmpty)
        {
            var x = (int)(rect.Left + rect.Width / 2);
            var y = (int)(rect.Top + rect.Height / 2);
            NativeMethods.SetCursorPos(x, y);
            NativeMethods.mouse_event(NativeMethods.MouseEventLeftDown, 0, 0, 0, UIntPtr.Zero);
            NativeMethods.mouse_event(NativeMethods.MouseEventLeftUp, 0, 0, 0, UIntPtr.Zero);
            return;
        }

        if (element.TryGetCurrentPattern(SelectionItemPattern.Pattern, out var selectionPattern))
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
                    throw new InvalidOperationException($"Win11Forge exited with code {process.ExitCode}.");
                }

                if (process.MainWindowHandle != IntPtr.Zero)
                {
                    return AutomationElement.FromHandle(process.MainWindowHandle);
                }

                var processIdCondition = new PropertyCondition(AutomationElement.ProcessIdProperty, process.Id);
                var windows = AutomationElement.RootElement.FindAll(TreeScope.Children, processIdCondition);
                foreach (AutomationElement window in windows)
                {
                    return window;
                }

                return null;
            },
            timeout,
            "Timed out waiting for the Win11Forge main window.");
    }

    private static T WaitUntil<T>(Func<T?> query, TimeSpan timeout, string failureMessage)
        where T : class
    {
        var stopwatch = Stopwatch.StartNew();
        Exception? lastException = null;

        while (stopwatch.Elapsed < timeout)
        {
            try
            {
                var result = query();
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
        var baseDirectory = AppContext.BaseDirectory;
        var localCopy = Path.Combine(baseDirectory, "Win11Forge.GUI.dll");
        if (File.Exists(localCopy))
        {
            return localCopy;
        }

        var directory = new DirectoryInfo(baseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "Win11Forge.GUI",
                "bin",
                "Release",
                "net8.0-windows",
                "Win11Forge.GUI.dll");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate Win11Forge.GUI.dll. Build the GUI project first.");
    }

    private static string ResolveArtifactDirectory()
    {
        var configuredDirectory = Environment.GetEnvironmentVariable("WIN11FORGE_UIA_ARTIFACTS");
        if (!string.IsNullOrWhiteSpace(configuredDirectory))
        {
            return Path.GetFullPath(configuredDirectory);
        }

        return Path.Combine(
            Path.GetTempPath(),
            "Win11Forge",
            "UIA",
            DateTime.Now.ToString("yyyyMMdd-HHmmss"));
    }

    private static void CaptureScreenRect(System.Windows.Rect rect, string path)
    {
        using var bitmap = new Bitmap((int)Math.Ceiling(rect.Width), (int)Math.Ceiling(rect.Height), PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
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

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);
    }
}

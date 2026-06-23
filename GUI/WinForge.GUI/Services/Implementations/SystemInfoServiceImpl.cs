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
using System.Security.Principal;
using System.Text;
using Microsoft.Win32;
using WinForge.GUI.Models;
using WinForge.GUI.Services.PowerShell;

namespace WinForge.GUI.Services.Implementations;

/// <summary>
/// Implementation of ISystemInfoService for retrieving system information.
/// </summary>
public class SystemInfoServiceImpl : ISystemInfoService
{
    private readonly IRepositoryPathService _pathService;

    /// <summary>
    /// Initializes a new instance of the SystemInfoServiceImpl.
    /// </summary>
    /// <param name="pathService">The repository path service.</param>
    public SystemInfoServiceImpl(IRepositoryPathService pathService)
    {
        _pathService = pathService ?? throw new ArgumentNullException(nameof(pathService));
    }

    /// <inheritdoc/>
    public string RepositoryRoot => _pathService.RepositoryRoot;

    /// <inheritdoc/>
    public async Task<SystemInfoModel> GetSystemInfoAsync()
    {
        return await Task.Run(() =>
        {
            SystemInfoModel info = new SystemInfoModel
            {
                Hostname = Environment.MachineName,
                Username = Environment.UserName,
                ProcessorCount = Environment.ProcessorCount
            };

            try
            {
                // Get Windows version using native .NET (more reliable than PowerShell SDK)
                info.WindowsVersion = GetWindowsVersionNative();
                info.WindowsBuild = GetWindowsBuildNative();

                // Get total memory using native .NET
                info.TotalMemoryGB = GetTotalMemoryNative();

                // Check if running as administrator using native .NET
                info.IsAdministrator = IsRunningAsAdministrator();

                // Check Winget availability using process
                string wingetVer = GetCommandVersion("winget", "--version");
                info.WingetAvailable = !string.IsNullOrEmpty(wingetVer);
                info.WingetVersion = wingetVer;

                // Check Chocolatey availability using process
                string chocoVer = GetCommandVersion("choco", "--version");
                info.ChocolateyAvailable = !string.IsNullOrEmpty(chocoVer);
                info.ChocolateyVersion = chocoVer;
            }
            catch
            {
                // Return partial info on error
            }

            return info;
        });
    }

    /// <summary>
    /// Gets Windows version string using native .NET/Registry.
    /// </summary>
    public static string GetWindowsVersionNative()
    {
        try
        {
            using RegistryKey? key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            string productName = key?.GetValue("ProductName")?.ToString() ?? "Windows";
            string? displayVersion = key?.GetValue("DisplayVersion")?.ToString();
            if (!string.IsNullOrEmpty(displayVersion))
            {
                return $"{productName} ({displayVersion})";
            }
            return productName;
        }
        catch
        {
            return $"Windows {Environment.OSVersion.Version.Major}";
        }
    }

    /// <summary>
    /// Gets Windows build number using native .NET/Registry.
    /// </summary>
    public static string GetWindowsBuildNative()
    {
        try
        {
            using RegistryKey? key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            string? build = key?.GetValue("CurrentBuildNumber")?.ToString() ?? key?.GetValue("CurrentBuild")?.ToString();
            string? ubr = key?.GetValue("UBR")?.ToString();
            if (!string.IsNullOrEmpty(build))
            {
                return !string.IsNullOrEmpty(ubr) ? $"{build}.{ubr}" : build;
            }
            return Environment.OSVersion.Version.Build.ToString();
        }
        catch
        {
            return Environment.OSVersion.Version.Build.ToString();
        }
    }

    /// <summary>
    /// Gets total physical memory using native .NET.
    /// </summary>
    public static double GetTotalMemoryNative()
    {
        try
        {
            // Use GC to get approximate total memory (not perfect but works without WMI)
            GCMemoryInfo gcInfo = GC.GetGCMemoryInfo();
            return Math.Round(gcInfo.TotalAvailableMemoryBytes / 1024.0 / 1024.0 / 1024.0, 1);
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Checks if the current process is running as administrator.
    /// </summary>
    public static bool IsRunningAsAdministrator()
    {
        try
        {
            using WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets version string from a command using process execution.
    /// </summary>
    public static string GetCommandVersion(string command, string arguments)
    {
        try
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                CreateNoWindow = true
            };

            using Process? process = Process.Start(startInfo);
            if (process == null) return string.Empty;

            try
            {
                string output = process.StandardOutput.ReadToEnd().Trim();
                return output;
            }
            finally
            {
                // Ensure process cleanup even if ReadToEnd throws
                process.WaitForExit(5000);
            }
        }
        catch
        {
            return string.Empty;
        }
    }
}

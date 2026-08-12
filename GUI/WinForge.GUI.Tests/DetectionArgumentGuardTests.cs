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

using System.IO;
using System.Text.RegularExpressions;
using WinForge.GUI.Services;

namespace WinForge.GUI.Tests;

/// <summary>
/// Guards the GUI detection probe against the argument-injection gap that the
/// PowerShell detection paths already close.
/// </summary>
public class DetectionArgumentGuardTests
{
    [Theory]
    [InlineData("-c \"import os;os.system('calc')\"")]   // interpreter code execution
    [InlineData("--version; whoami")]                     // command chaining
    [InlineData("--version && whoami")]
    [InlineData("--version | whoami")]
    [InlineData("--version `whoami`")]                    // backtick substitution
    [InlineData("--version $(whoami)")]                   // subexpression
    [InlineData("--version >> C:\\Windows\\Temp\\out.txt")]
    [InlineData("--version\nwhoami")]                     // newline injection
    [InlineData("--version\r\nwhoami")]
    public void IsDangerous_RejectsInjectionVectors(string arguments)
    {
        Assert.True(DetectionArgumentGuard.IsDangerous(arguments));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("--version")]
    [InlineData("-v")]
    [InlineData("--version --json")]
    [InlineData("list --local-only --exact 7zip")]
    // Single '>' is not a redirection here: neither path goes through a shell
    // (UseShellExecute=false in C#, -ArgumentList arrays in PowerShell), so it is a
    // literal argument. Both guards accept it, and this pins that agreement.
    [InlineData("--version > out.txt")]
    public void IsDangerous_AllowsPlainDetectionArguments(string? arguments)
    {
        Assert.False(DetectionArgumentGuard.IsDangerous(arguments));
    }

    /// <summary>
    /// The two guards must agree. If the PowerShell regex is edited without editing the
    /// C# one, a catalog entry refused by the PowerShell detector would still run through
    /// the GUI probe. This asserts the pattern itself has not drifted.
    /// </summary>
    [Fact]
    public void GuardPattern_MatchesThePowerShellGuard()
    {
        string guardModule = Path.Combine(
            RepositoryRoot(), "Modules", "DetectionArgumentGuard.psm1");

        Assert.True(File.Exists(guardModule), $"PowerShell guard not found at {guardModule}");

        string source = File.ReadAllText(guardModule);
        Match match = Regex.Match(source, @"\$Arguments -match '(?<pattern>.+?)'\)");

        Assert.True(match.Success, "Could not locate the PowerShell guard regex literal.");

        // The PowerShell literal escapes the backtick inside a single-quoted string the
        // same way the C# verbatim string does, so the two patterns compare directly.
        string powerShellPattern = match.Groups["pattern"].Value;
        Assert.Equal(@"[;&|`$\(\)\r\n]|>>|<<|[\x00-\x1f]", powerShellPattern);

        // Every vector the PowerShell pattern rejects must also be rejected by the C# guard.
        Regex powerShellGuard = new(powerShellPattern);
        string[] probes =
        [
            "-c \"code\"", "a;b", "a&b", "a|b", "a`b", "a$b", "a(b)", "a>>b", "a<<b", "a\rb", "a\nb"
        ];

        foreach (string probe in probes)
        {
            Assert.Equal(powerShellGuard.IsMatch(probe), DetectionArgumentGuard.IsDangerous(probe));
        }
    }

    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "Modules")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory!.FullName;
    }
}

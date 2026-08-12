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

using System.Text.RegularExpressions;

namespace WinForge.GUI.Services;

/// <summary>
/// Sanitization gate for Detection.Command arguments.
/// </summary>
/// <remarks>
/// This is the C# half of a control that must behave identically on both execution
/// paths. The PowerShell modules gate every Command detection through
/// Test-DetectionArgumentDangerous (Modules/DetectionArgumentGuard.psm1); without the
/// same gate here, a catalog entry rejected by the PowerShell detector would still run
/// through the GUI probe. That matters because Config/detection-allowlist.json permits
/// interpreters (python, node, pwsh, ruby, perl, php), so an unguarded argument string
/// such as <c>-c "&lt;code&gt;"</c> is arbitrary code execution when an untrusted
/// applications catalog is imported.
/// The pattern is kept character-for-character in sync with the PowerShell regex.
/// </remarks>
internal static class DetectionArgumentGuard
{
    /// <summary>
    /// Shell metacharacters, command substitution, redirection and control characters.
    /// Mirrors the PowerShell guard's regex exactly.
    /// </summary>
    private const string DangerousArgumentPattern = @"[;&|`$()\r\n]|>>|<<|[\x00-\x1f]";

    private static readonly Regex DangerousArgumentRegex = new(
        DangerousArgumentPattern,
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Determines whether a Detection.Command argument string must not be executed.
    /// </summary>
    /// <param name="arguments">The argument portion parsed from a Detection.Command entry.</param>
    /// <returns><see langword="true"/> when the arguments are dangerous; <see langword="false"/> for empty or safe arguments.</returns>
    public static bool IsDangerous(string? arguments)
    {
        if (string.IsNullOrEmpty(arguments))
        {
            return false;
        }

        return DangerousArgumentRegex.IsMatch(arguments);
    }

    /// <summary>
    /// Determines whether a Detection.Command argument string is on the configured
    /// allowlist of permitted detection arguments.
    /// </summary>
    /// <remarks>
    /// Rejecting metacharacters is necessary but not sufficient. The executable allowlist
    /// permits interpreters that accept code as an argument, and
    /// <c>pwsh -Command Start-Process calc</c> contains no metacharacter at all. Detection
    /// only ever needs to ask a program for its version, so the argument side is an
    /// allowlist as well, configured in <c>Config/detection-allowlist.json</c>.
    /// An empty argument string is allowed: running the bare executable is how several
    /// programs report their presence.
    /// </remarks>
    /// <param name="arguments">The argument portion parsed from a Detection.Command entry.</param>
    /// <param name="allowedArguments">The configured allowlist.</param>
    /// <returns><see langword="true"/> when the arguments may be executed.</returns>
    public static bool IsAllowed(string? arguments, IReadOnlySet<string> allowedArguments)
    {
        if (string.IsNullOrWhiteSpace(arguments))
        {
            return true;
        }

        if (IsDangerous(arguments))
        {
            return false;
        }

        return allowedArguments.Contains(arguments.Trim());
    }
}

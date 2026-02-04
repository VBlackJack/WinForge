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

namespace Win11Forge.GUI.Services.PowerShell;

/// <summary>
/// Provides validation and sanitization utilities for PowerShell-related operations.
/// </summary>
internal static class PowerShellValidation
{
    /// <summary>
    /// Maximum allowed profile name length.
    /// </summary>
    private const int MaxProfileNameLength = 100;

    /// <summary>
    /// Maximum allowed application ID length.
    /// </summary>
    private const int MaxAppIdLength = 200;

    /// <summary>
    /// Validates and sanitizes a profile name to prevent path traversal attacks.
    /// </summary>
    /// <param name="profileName">The profile name to validate.</param>
    /// <returns>The validated profile name.</returns>
    /// <exception cref="ArgumentException">Thrown if the profile name is invalid.</exception>
    public static string ValidateProfileName(string profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName))
        {
            throw new ArgumentException("Profile name cannot be empty.", nameof(profileName));
        }

        // Check for path traversal attempts
        if (profileName.Contains("..") || profileName.Contains('/') || profileName.Contains('\\'))
        {
            throw new ArgumentException("Profile name contains invalid characters.", nameof(profileName));
        }

        // Check for other invalid path characters
        var invalidChars = Path.GetInvalidFileNameChars();
        if (profileName.IndexOfAny(invalidChars) >= 0)
        {
            throw new ArgumentException("Profile name contains invalid characters.", nameof(profileName));
        }

        // Limit length to prevent buffer issues
        if (profileName.Length > MaxProfileNameLength)
        {
            throw new ArgumentException("Profile name is too long.", nameof(profileName));
        }

        return profileName;
    }

    /// <summary>
    /// Validates that a file path is within the expected directory to prevent path traversal.
    /// </summary>
    /// <param name="filePath">The file path to validate.</param>
    /// <param name="expectedBaseDir">The expected base directory.</param>
    /// <returns>The validated absolute path.</returns>
    /// <exception cref="ArgumentException">Thrown if the path escapes the expected directory.</exception>
    public static string ValidatePathWithinDirectory(string filePath, string expectedBaseDir)
    {
        var fullPath = Path.GetFullPath(filePath);
        var fullBaseDir = Path.GetFullPath(expectedBaseDir);

        // Ensure the base directory ends with a separator for proper prefix checking
        if (!fullBaseDir.EndsWith(Path.DirectorySeparatorChar.ToString()))
        {
            fullBaseDir += Path.DirectorySeparatorChar;
        }

        if (!fullPath.StartsWith(fullBaseDir, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"Path traversal detected: {filePath} is outside of {expectedBaseDir}", nameof(filePath));
        }

        return fullPath;
    }

    /// <summary>
    /// Validates an application ID to prevent injection attacks.
    /// Blocks all PowerShell special characters including backticks, $, (), etc.
    /// </summary>
    /// <param name="appId">The application ID to validate.</param>
    /// <returns>The validated application ID.</returns>
    /// <exception cref="ArgumentException">Thrown when the ID contains invalid characters.</exception>
    public static string ValidateAppId(string appId)
    {
        if (string.IsNullOrWhiteSpace(appId))
        {
            throw new ArgumentException("Application ID cannot be empty.", nameof(appId));
        }

        // AppIds should only contain alphanumeric, dots, hyphens, underscores, and spaces
        // This BLOCKS all PowerShell injection vectors:
        // - Backticks (`) for escape sequences
        // - Dollar signs ($) for variables and subexpressions
        // - Parentheses () for subexpressions
        // - Semicolons (;) for command chaining
        // - Pipes (|) for command piping
        // - Single/double quotes for string manipulation
        // Examples: Microsoft.VisualStudioCode, 7zip.7zip, VideoLAN.VLC
        foreach (char c in appId)
        {
            if (!char.IsLetterOrDigit(c) && c != '.' && c != '-' && c != '_' && c != ' ')
            {
                throw new ArgumentException($"Application ID contains invalid character: '{c}'", nameof(appId));
            }
        }

        // Limit length to prevent buffer issues
        if (appId.Length > MaxAppIdLength)
        {
            throw new ArgumentException("Application ID is too long.", nameof(appId));
        }

        return appId;
    }

    /// <summary>
    /// Escapes a string for safe use in PowerShell single-quoted strings.
    /// Even though ValidateAppId blocks dangerous characters, this provides defense in depth.
    /// </summary>
    /// <param name="value">The value to escape.</param>
    /// <returns>The escaped value safe for use in PowerShell.</returns>
    public static string EscapeForPowerShell(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        // In single-quoted PowerShell strings, only single quotes need escaping (doubled)
        // But we also remove any potentially dangerous characters as defense in depth
        return value
            .Replace("'", "''")           // Escape single quotes
            .Replace("`", "")             // Remove backticks (escape char)
            .Replace("$", "")             // Remove dollar signs (variable expansion)
            .Replace("(", "")             // Remove opening parens (subexpression)
            .Replace(")", "");            // Remove closing parens (subexpression)
    }
}

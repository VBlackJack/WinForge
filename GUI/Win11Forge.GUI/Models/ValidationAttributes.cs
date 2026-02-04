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

#nullable enable

using System.ComponentModel.DataAnnotations;

namespace Win11Forge.GUI.Models;

/// <summary>
/// Validates that a string is a well-formed HTTP or HTTPS URL.
/// Empty or null strings are considered valid (use [Required] for mandatory URLs).
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
public sealed class ValidUrlAttribute : ValidationAttribute
{
    /// <summary>
    /// Whether to allow HTTP URLs (default true). Set to false to require HTTPS only.
    /// </summary>
    public bool AllowHttp { get; set; } = true;

    /// <summary>
    /// Validates that the value is a well-formed URL.
    /// </summary>
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        // Null or empty is valid - use [Required] for mandatory fields
        if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
        {
            return ValidationResult.Success;
        }

        var url = value.ToString()!;
        var memberNames = validationContext.MemberName != null ? new[] { validationContext.MemberName } : null;

        // Check if it's a well-formed URI
        if (!Uri.IsWellFormedUriString(url, UriKind.Absolute))
        {
            return new ValidationResult(FormatErrorMessage(validationContext.DisplayName), memberNames);
        }

        // Parse and validate scheme
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return new ValidationResult(FormatErrorMessage(validationContext.DisplayName), memberNames);
        }

        // Validate scheme (HTTP/HTTPS)
        var scheme = uri.Scheme.ToLowerInvariant();
        if (scheme != "https" && (scheme != "http" || !AllowHttp))
        {
            return new ValidationResult(FormatErrorMessage(validationContext.DisplayName), memberNames);
        }

        // Validate that host is present
        if (string.IsNullOrWhiteSpace(uri.Host))
        {
            return new ValidationResult(FormatErrorMessage(validationContext.DisplayName), memberNames);
        }

        return ValidationResult.Success;
    }
}

/// <summary>
/// Validates that a string represents a valid semantic version (e.g., "1.0.0", "2.1.3-beta").
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
public sealed class SemanticVersionAttribute : ValidationAttribute
{
    private static readonly System.Text.RegularExpressions.Regex VersionPattern = new(
        @"^v?(\d+)(?:\.(\d+))?(?:\.(\d+))?(?:\.(\d+))?(?:[-+][\w.]+)?$",
        System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    /// <summary>
    /// Validates that the value is a valid semantic version.
    /// </summary>
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        // Null or empty is valid - use [Required] for mandatory fields
        if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
        {
            return ValidationResult.Success;
        }

        var version = value.ToString()!;

        if (!VersionPattern.IsMatch(version))
        {
            return new ValidationResult(
                ErrorMessage ?? "The value must be a valid version number (e.g., 1.0.0, 2.1.3-beta).",
                validationContext.MemberName != null ? new[] { validationContext.MemberName } : null);
        }

        return ValidationResult.Success;
    }
}

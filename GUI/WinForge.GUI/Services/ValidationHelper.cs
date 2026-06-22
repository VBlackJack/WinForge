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

namespace Win11Forge.GUI.Services;

/// <summary>
/// Helper class for form validation with localized error messages.
/// </summary>
public static partial class ValidationHelper
{
    // Invalid characters for file/profile names
    private static readonly char[] InvalidNameChars = ['\\', '/', ':', '*', '?', '"', '<', '>', '|'];

    /// <summary>
    /// Validates that a field is not empty.
    /// </summary>
    public static ValidationResult ValidateRequired(string? value, string? fieldName = null)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new ValidationResult(false, Resources.Resources.Validation_Required);
        }
        return ValidationResult.Success;
    }

    /// <summary>
    /// Validates that a string doesn't exceed maximum length.
    /// </summary>
    public static ValidationResult ValidateMaxLength(string? value, int maxLength)
    {
        if (value != null && value.Length > maxLength)
        {
            return new ValidationResult(false, string.Format(Resources.Resources.Validation_TooLong, maxLength));
        }
        return ValidationResult.Success;
    }

    /// <summary>
    /// Validates that a name doesn't contain invalid characters.
    /// </summary>
    public static ValidationResult ValidateFileName(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return ValidationResult.Success;
        }

        if (value.IndexOfAny(InvalidNameChars) >= 0)
        {
            return new ValidationResult(false, Resources.Resources.Validation_InvalidChars);
        }

        return ValidationResult.Success;
    }

    /// <summary>
    /// Validates a profile name with all rules.
    /// </summary>
    public static ValidationResult ValidateProfileName(string? value, IEnumerable<string>? existingNames = null)
    {
        ValidationResult required = ValidateRequired(value);
        if (!required.IsValid) return required;

        ValidationResult maxLength = ValidateMaxLength(value, 100);
        if (!maxLength.IsValid) return maxLength;

        ValidationResult fileName = ValidateFileName(value);
        if (!fileName.IsValid) return fileName;

        if (existingNames != null && value != null)
        {
            if (existingNames.Any(n => n.Equals(value, StringComparison.OrdinalIgnoreCase)))
            {
                return new ValidationResult(false, Resources.Resources.Validation_AlreadyExists);
            }
        }

        return ValidationResult.Success;
    }

    /// <summary>
    /// Validates an email address format.
    /// </summary>
    public static ValidationResult ValidateEmail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return ValidationResult.Success;
        }

        if (!EmailRegex().IsMatch(value))
        {
            return new ValidationResult(false, "Invalid email format");
        }

        return ValidationResult.Success;
    }

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase)]
    private static partial Regex EmailRegex();
}

/// <summary>
/// Result of a validation operation.
/// </summary>
public class ValidationResult
{
    public static ValidationResult Success { get; } = new(true, null);

    public bool IsValid { get; }
    public string? ErrorMessage { get; }

    public ValidationResult(bool isValid, string? errorMessage)
    {
        IsValid = isValid;
        ErrorMessage = errorMessage;
    }
}

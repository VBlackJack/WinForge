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

using Win11Forge.GUI.Models;
using DataAnnotationsValidationContext = System.ComponentModel.DataAnnotations.ValidationContext;
using DataAnnotationsValidationResult = System.ComponentModel.DataAnnotations.ValidationResult;
using DataAnnotationsValidator = System.ComponentModel.DataAnnotations.Validator;

namespace Win11Forge.GUI.Services;

/// <summary>
/// Interface for profile validation service.
/// </summary>
public interface IProfileValidationService
{
    /// <summary>
    /// Validates that all inherited profiles exist.
    /// </summary>
    ValidationResult ValidateInheritanceExists(DeploymentProfileModel profile, IEnumerable<string> availableProfiles);

    /// <summary>
    /// Validates that there are no circular dependencies in the inheritance chain.
    /// </summary>
    ValidationResult ValidateNoCircularInheritance(
        string profileName,
        IReadOnlyList<string> inheritedFrom,
        Func<string, IReadOnlyList<string>?> getParentProfiles,
        int maxDepth = 10);

    /// <summary>
    /// Validates that all application IDs in the profile exist in the database.
    /// </summary>
    ValidationResult ValidateApplicationsExist(DeploymentProfileModel profile, IEnumerable<string> availableAppIds);

    /// <summary>
    /// Performs all validations and returns a list of validation results.
    /// </summary>
    IEnumerable<ValidationResult> ValidateProfile(
        DeploymentProfileModel profile,
        IEnumerable<string> availableProfiles,
        IEnumerable<string> availableAppIds,
        Func<string, IReadOnlyList<string>?> getParentProfiles);
}

/// <summary>
/// Service for validating deployment profiles with cross-model validation.
/// </summary>
public class ProfileValidationService : IProfileValidationService
{
    /// <inheritdoc/>
    public ValidationResult ValidateInheritanceExists(
        DeploymentProfileModel profile,
        IEnumerable<string> availableProfiles)
    {
        if (profile.InheritedFrom == null || profile.InheritedFrom.Count == 0)
            return ValidationResult.Success;

        HashSet<string> availableSet = new HashSet<string>(availableProfiles, StringComparer.OrdinalIgnoreCase);
        List<string> missingProfiles = profile.InheritedFrom
            .Where(p => !availableSet.Contains(p))
            .ToList();

        if (missingProfiles.Count > 0)
        {
            return new ValidationResult(
                false,
                $"Referenced parent profiles do not exist: {string.Join(", ", missingProfiles)}");
        }

        return ValidationResult.Success;
    }

    /// <inheritdoc/>
    public ValidationResult ValidateNoCircularInheritance(
        string profileName,
        IReadOnlyList<string> inheritedFrom,
        Func<string, IReadOnlyList<string>?> getParentProfiles,
        int maxDepth = 10)
    {
        if (inheritedFrom == null || inheritedFrom.Count == 0)
            return ValidationResult.Success;

        HashSet<string> visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { profileName };
        Queue<(string Profile, int Depth)> queue = new Queue<(string Profile, int Depth)>();

        foreach (string parent in inheritedFrom)
        {
            queue.Enqueue((parent, 1));
        }

        while (queue.Count > 0)
        {
            (string? current, int depth) = queue.Dequeue();

            // Check for circular reference back to original profile
            if (string.Equals(current, profileName, StringComparison.OrdinalIgnoreCase))
            {
                return new ValidationResult(
                    false,
                    $"Circular inheritance detected: profile '{profileName}' appears in its own inheritance chain");
            }

            // Check depth limit
            if (depth > maxDepth)
            {
                return new ValidationResult(
                    false,
                    $"Inheritance chain exceeds maximum depth of {maxDepth}");
            }

            // Skip if already visited
            if (!visited.Add(current))
                continue;

            // Get parent's parents
            IReadOnlyList<string>? parentParents = getParentProfiles(current);
            if (parentParents != null)
            {
                foreach (string grandparent in parentParents)
                {
                    queue.Enqueue((grandparent, depth + 1));
                }
            }
        }

        return ValidationResult.Success;
    }

    /// <inheritdoc/>
    public ValidationResult ValidateApplicationsExist(
        DeploymentProfileModel profile,
        IEnumerable<string> availableAppIds)
    {
        if (profile.Applications == null || profile.Applications.Count == 0)
            return ValidationResult.Success;

        HashSet<string> availableSet = new HashSet<string>(availableAppIds, StringComparer.OrdinalIgnoreCase);
        List<string> missingApps = profile.Applications
            .Select(a => a.AppId)
            .Where(id => !string.IsNullOrEmpty(id) && !availableSet.Contains(id))
            .Distinct()
            .ToList();

        if (missingApps.Count > 0)
        {
            return new ValidationResult(
                false,
                $"Referenced applications do not exist in database: {string.Join(", ", missingApps.Take(10))}" +
                (missingApps.Count > 10 ? $" and {missingApps.Count - 10} more" : ""));
        }

        return ValidationResult.Success;
    }

    /// <inheritdoc/>
    public IEnumerable<ValidationResult> ValidateProfile(
        DeploymentProfileModel profile,
        IEnumerable<string> availableProfiles,
        IEnumerable<string> availableAppIds,
        Func<string, IReadOnlyList<string>?> getParentProfiles)
    {
        List<ValidationResult> results = new List<ValidationResult>();

        // Standard DataAnnotations validation
        DataAnnotationsValidationContext context = new DataAnnotationsValidationContext(profile);
        List<DataAnnotationsValidationResult> annotationResults = new List<DataAnnotationsValidationResult>();
        DataAnnotationsValidator.TryValidateObject(profile, context, annotationResults, validateAllProperties: true);

        // Convert DataAnnotations results to our ValidationResult type
        foreach (DataAnnotationsValidationResult result in annotationResults)
        {
            if (result != DataAnnotationsValidationResult.Success && result.ErrorMessage != null)
            {
                results.Add(new ValidationResult(false, result.ErrorMessage));
            }
        }

        // IValidatableObject validation
        foreach (DataAnnotationsValidationResult result in profile.Validate(context))
        {
            if (result != DataAnnotationsValidationResult.Success && result.ErrorMessage != null)
            {
                results.Add(new ValidationResult(false, result.ErrorMessage));
            }
        }

        // Cross-model validations
        ValidationResult inheritanceExists = ValidateInheritanceExists(profile, availableProfiles);
        if (!inheritanceExists.IsValid)
            results.Add(inheritanceExists);

        ValidationResult circularCheck = ValidateNoCircularInheritance(
            profile.Name,
            profile.InheritedFrom,
            getParentProfiles);
        if (!circularCheck.IsValid)
            results.Add(circularCheck);

        ValidationResult appsExist = ValidateApplicationsExist(profile, availableAppIds);
        if (!appsExist.IsValid)
            results.Add(appsExist);

        return results;
    }
}

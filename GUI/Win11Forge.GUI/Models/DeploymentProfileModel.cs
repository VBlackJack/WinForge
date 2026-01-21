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

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;

namespace Win11Forge.GUI.Models;

/// <summary>
/// Represents a deployment profile with its applications and configuration.
/// </summary>
public class DeploymentProfileModel : IValidatableObject
{
    /// <summary>Profile name.</summary>
    [Required(ErrorMessage = "Profile name is required")]
    [StringLength(128, MinimumLength = 1, ErrorMessage = "Profile name must be between 1 and 128 characters")]
    [RegularExpression(@"^[a-zA-Z0-9_-]+$", ErrorMessage = "Profile name can only contain letters, numbers, underscores, and hyphens")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Profile description.</summary>
    [StringLength(2048, ErrorMessage = "Description must not exceed 2048 characters")]
    public string Description { get; set; } = string.Empty;

    /// <summary>Profile version.</summary>
    [StringLength(32, ErrorMessage = "Version must not exceed 32 characters")]
    [RegularExpression(@"^\d+\.\d+(\.\d+)?$", ErrorMessage = "Version must be in format X.Y or X.Y.Z")]
    public string Version { get; set; } = string.Empty;

    /// <summary>List of parent profiles this profile inherits from.</summary>
    [MaxLength(10, ErrorMessage = "Cannot inherit from more than 10 profiles")]
    public List<string> InheritedFrom { get; set; } = [];

    /// <summary>Applications included in this profile.</summary>
    public ObservableCollection<ApplicationModel> Applications { get; set; } = [];

    /// <summary>Total number of applications.</summary>
    public int TotalApplications => Applications.Count;

    /// <summary>Number of required applications.</summary>
    public int RequiredApplications => Applications.Count(a => a.IsRequired);

    /// <summary>
    /// Validates the model with complex business rules.
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // Check for circular inheritance (profile inheriting from itself)
        if (InheritedFrom.Contains(Name))
        {
            yield return new ValidationResult(
                "A profile cannot inherit from itself",
                new[] { nameof(InheritedFrom) });
        }

        // Check for duplicate parent profiles
        var distinctParents = InheritedFrom.Distinct().ToList();
        if (distinctParents.Count != InheritedFrom.Count)
        {
            yield return new ValidationResult(
                "Duplicate parent profiles detected",
                new[] { nameof(InheritedFrom) });
        }

        // Check for duplicate application IDs
        var appIds = Applications.Select(a => a.AppId).ToList();
        var distinctAppIds = appIds.Distinct().ToList();
        if (distinctAppIds.Count != appIds.Count)
        {
            yield return new ValidationResult(
                "Duplicate application IDs detected in profile",
                new[] { nameof(Applications) });
        }
    }
}

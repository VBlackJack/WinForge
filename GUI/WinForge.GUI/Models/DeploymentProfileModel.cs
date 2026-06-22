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
    [Required(ErrorMessageResourceName = nameof(Resources.Resources.Validation_Profile_Name_Required), ErrorMessageResourceType = typeof(Resources.Resources))]
    [StringLength(128, MinimumLength = 1, ErrorMessageResourceName = nameof(Resources.Resources.Validation_Profile_Name_Length), ErrorMessageResourceType = typeof(Resources.Resources))]
    [RegularExpression(@"^[a-zA-Z0-9_-]+$", ErrorMessageResourceName = nameof(Resources.Resources.Validation_Profile_Name_Pattern), ErrorMessageResourceType = typeof(Resources.Resources))]
    public string Name { get; set; } = string.Empty;

    /// <summary>Profile description.</summary>
    [StringLength(2048, ErrorMessageResourceName = nameof(Resources.Resources.Validation_Profile_Description_MaxLength), ErrorMessageResourceType = typeof(Resources.Resources))]
    public string Description { get; set; } = string.Empty;

    /// <summary>Profile version.</summary>
    [StringLength(32, ErrorMessageResourceName = nameof(Resources.Resources.Validation_Profile_Version_MaxLength), ErrorMessageResourceType = typeof(Resources.Resources))]
    [RegularExpression(@"^\d+\.\d+(\.\d+)?$", ErrorMessageResourceName = nameof(Resources.Resources.Validation_Profile_Version_Pattern), ErrorMessageResourceType = typeof(Resources.Resources))]
    public string Version { get; set; } = string.Empty;

    /// <summary>List of parent profiles this profile inherits from.</summary>
    [MaxLength(10, ErrorMessageResourceName = nameof(Resources.Resources.Validation_Profile_InheritedFrom_MaxCount), ErrorMessageResourceType = typeof(Resources.Resources))]
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
                Resources.Resources.Validation_Profile_CircularInheritance,
                new[] { nameof(InheritedFrom) });
        }

        // Check for duplicate parent profiles
        List<string> distinctParents = InheritedFrom.Distinct().ToList();
        if (distinctParents.Count != InheritedFrom.Count)
        {
            yield return new ValidationResult(
                Resources.Resources.Validation_Profile_DuplicateParents,
                new[] { nameof(InheritedFrom) });
        }

        // Check for duplicate application IDs
        List<string> appIds = Applications.Select(a => a.AppId).ToList();
        List<string> distinctAppIds = appIds.Distinct().ToList();
        if (distinctAppIds.Count != appIds.Count)
        {
            yield return new ValidationResult(
                Resources.Resources.Validation_Profile_DuplicateApps,
                new[] { nameof(Applications) });
        }
    }
}

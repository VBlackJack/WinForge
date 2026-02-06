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

using Win11Forge.GUI.Models;
using Win11Forge.GUI.Services;

namespace Win11Forge.GUI.Tests;

/// <summary>
/// Tests for ProfileValidationService - cross-model profile validation rules.
/// </summary>
public class ProfileValidationServiceTests
{
    private readonly ProfileValidationService _service;

    public ProfileValidationServiceTests()
    {
        _service = new ProfileValidationService();
    }

    #region ValidateInheritanceExists

    /// <summary>
    /// Verifies that a profile with no inheritance returns success.
    /// </summary>
    [Fact]
    public void ValidateInheritanceExists_NoInheritance_ReturnsSuccess()
    {
        // Arrange
        var profile = new DeploymentProfileModel
        {
            Name = "Standalone",
            InheritedFrom = []
        };

        // Act
        var result = _service.ValidateInheritanceExists(profile, ["Base", "Office"]);

        // Assert
        Assert.True(result.IsValid);
    }

    /// <summary>
    /// Verifies that a profile with null InheritedFrom returns success.
    /// </summary>
    [Fact]
    public void ValidateInheritanceExists_NullInheritance_ReturnsSuccess()
    {
        // Arrange
        var profile = new DeploymentProfileModel
        {
            Name = "Standalone",
            InheritedFrom = null!
        };

        // Act
        var result = _service.ValidateInheritanceExists(profile, ["Base"]);

        // Assert
        Assert.True(result.IsValid);
    }

    /// <summary>
    /// Verifies that a profile with valid parent profiles returns success.
    /// </summary>
    [Fact]
    public void ValidateInheritanceExists_AllParentsExist_ReturnsSuccess()
    {
        // Arrange
        var profile = new DeploymentProfileModel
        {
            Name = "Gaming",
            InheritedFrom = ["Base", "Office"]
        };

        // Act
        var result = _service.ValidateInheritanceExists(profile, ["Base", "Office", "Gaming"]);

        // Assert
        Assert.True(result.IsValid);
    }

    /// <summary>
    /// Verifies that a profile with missing parent returns failure.
    /// </summary>
    [Fact]
    public void ValidateInheritanceExists_MissingParent_ReturnsFailure()
    {
        // Arrange
        var profile = new DeploymentProfileModel
        {
            Name = "Gaming",
            InheritedFrom = ["Base", "NonExistent"]
        };

        // Act
        var result = _service.ValidateInheritanceExists(profile, ["Base", "Office"]);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("NonExistent", result.ErrorMessage!);
    }

    /// <summary>
    /// Verifies that parent matching is case-insensitive.
    /// </summary>
    [Fact]
    public void ValidateInheritanceExists_CaseInsensitiveMatch_ReturnsSuccess()
    {
        // Arrange
        var profile = new DeploymentProfileModel
        {
            Name = "Gaming",
            InheritedFrom = ["base", "OFFICE"]
        };

        // Act
        var result = _service.ValidateInheritanceExists(profile, ["Base", "Office"]);

        // Assert
        Assert.True(result.IsValid);
    }

    /// <summary>
    /// Verifies that multiple missing parents are reported.
    /// </summary>
    [Fact]
    public void ValidateInheritanceExists_MultipleMissingParents_ReportsAll()
    {
        // Arrange
        var profile = new DeploymentProfileModel
        {
            Name = "Custom",
            InheritedFrom = ["Missing1", "Missing2", "Base"]
        };

        // Act
        var result = _service.ValidateInheritanceExists(profile, ["Base"]);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("Missing1", result.ErrorMessage!);
        Assert.Contains("Missing2", result.ErrorMessage!);
    }

    #endregion

    #region ValidateNoCircularInheritance

    /// <summary>
    /// Verifies that a profile with no parents returns success.
    /// </summary>
    [Fact]
    public void ValidateNoCircularInheritance_NoParents_ReturnsSuccess()
    {
        // Act
        var result = _service.ValidateNoCircularInheritance(
            "Base",
            [],
            _ => null);

        // Assert
        Assert.True(result.IsValid);
    }

    /// <summary>
    /// Verifies that a profile with null parents returns success.
    /// </summary>
    [Fact]
    public void ValidateNoCircularInheritance_NullParents_ReturnsSuccess()
    {
        // Act
        var result = _service.ValidateNoCircularInheritance(
            "Base",
            null!,
            _ => null);

        // Assert
        Assert.True(result.IsValid);
    }

    /// <summary>
    /// Verifies that a valid linear inheritance chain returns success.
    /// </summary>
    [Fact]
    public void ValidateNoCircularInheritance_LinearChain_ReturnsSuccess()
    {
        // Arrange - Gaming -> Office -> Base -> (no parent)
        IReadOnlyList<string>? GetParents(string name) => name switch
        {
            "Office" => new List<string> { "Base" },
            "Base" => [],
            _ => null
        };

        // Act
        var result = _service.ValidateNoCircularInheritance(
            "Gaming",
            new List<string> { "Office" },
            GetParents);

        // Assert
        Assert.True(result.IsValid);
    }

    /// <summary>
    /// Verifies that a direct circular reference is detected.
    /// </summary>
    [Fact]
    public void ValidateNoCircularInheritance_DirectCircle_ReturnsFailure()
    {
        // Arrange - A -> B -> A (circular)
        IReadOnlyList<string>? GetParents(string name) => name switch
        {
            "B" => new List<string> { "A" },
            _ => null
        };

        // Act
        var result = _service.ValidateNoCircularInheritance(
            "A",
            new List<string> { "B" },
            GetParents);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("Circular", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies that an indirect circular reference is detected.
    /// </summary>
    [Fact]
    public void ValidateNoCircularInheritance_IndirectCircle_ReturnsFailure()
    {
        // Arrange - A -> B -> C -> A (indirect circular)
        IReadOnlyList<string>? GetParents(string name) => name switch
        {
            "B" => new List<string> { "C" },
            "C" => new List<string> { "A" },
            _ => null
        };

        // Act
        var result = _service.ValidateNoCircularInheritance(
            "A",
            new List<string> { "B" },
            GetParents);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("Circular", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies that max depth is enforced.
    /// </summary>
    [Fact]
    public void ValidateNoCircularInheritance_ExceedsMaxDepth_ReturnsFailure()
    {
        // Arrange - A deeply nested chain that exceeds max depth of 3
        IReadOnlyList<string>? GetParents(string name) => name switch
        {
            "P1" => new List<string> { "P2" },
            "P2" => new List<string> { "P3" },
            "P3" => new List<string> { "P4" },
            "P4" => new List<string> { "P5" },
            _ => null
        };

        // Act
        var result = _service.ValidateNoCircularInheritance(
            "Root",
            new List<string> { "P1" },
            GetParents,
            maxDepth: 3);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("depth", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies that a diamond inheritance (shared ancestor) does not cause false positives.
    /// </summary>
    [Fact]
    public void ValidateNoCircularInheritance_DiamondInheritance_ReturnsSuccess()
    {
        // Arrange - D -> B, C; B -> A; C -> A (diamond shape, not circular)
        IReadOnlyList<string>? GetParents(string name) => name switch
        {
            "B" => new List<string> { "A" },
            "C" => new List<string> { "A" },
            "A" => [],
            _ => null
        };

        // Act
        var result = _service.ValidateNoCircularInheritance(
            "D",
            new List<string> { "B", "C" },
            GetParents);

        // Assert
        Assert.True(result.IsValid);
    }

    /// <summary>
    /// Verifies that case-insensitive profile name matching detects cycles.
    /// </summary>
    [Fact]
    public void ValidateNoCircularInheritance_CaseInsensitiveCircle_ReturnsFailure()
    {
        // Arrange - "myProfile" -> B -> "MyProfile" (case-insensitive circular)
        IReadOnlyList<string>? GetParents(string name) => name switch
        {
            "B" => new List<string> { "MyProfile" },
            _ => null
        };

        // Act
        var result = _service.ValidateNoCircularInheritance(
            "myProfile",
            new List<string> { "B" },
            GetParents);

        // Assert
        Assert.False(result.IsValid);
    }

    /// <summary>
    /// Verifies that a parent returning null parents (unknown profile) is handled gracefully.
    /// </summary>
    [Fact]
    public void ValidateNoCircularInheritance_UnknownParent_ReturnsSuccess()
    {
        // Arrange - Parent lookup returns null for unknown profiles
        IReadOnlyList<string>? GetParents(string _) => null;

        // Act
        var result = _service.ValidateNoCircularInheritance(
            "Profile",
            new List<string> { "Unknown1", "Unknown2" },
            GetParents);

        // Assert
        Assert.True(result.IsValid);
    }

    #endregion

    #region ValidateApplicationsExist

    /// <summary>
    /// Verifies that a profile with no applications returns success.
    /// </summary>
    [Fact]
    public void ValidateApplicationsExist_NoApplications_ReturnsSuccess()
    {
        // Arrange
        var profile = new DeploymentProfileModel
        {
            Name = "Empty"
        };

        // Act
        var result = _service.ValidateApplicationsExist(profile, ["App1", "App2"]);

        // Assert
        Assert.True(result.IsValid);
    }

    /// <summary>
    /// Verifies that a profile with all valid applications returns success.
    /// </summary>
    [Fact]
    public void ValidateApplicationsExist_AllAppsExist_ReturnsSuccess()
    {
        // Arrange
        var profile = new DeploymentProfileModel { Name = "Test" };
        profile.Applications.Add(new ApplicationModel { AppId = "Chrome", Name = "Google Chrome" });
        profile.Applications.Add(new ApplicationModel { AppId = "Firefox", Name = "Mozilla Firefox" });

        // Act
        var result = _service.ValidateApplicationsExist(profile, ["Chrome", "Firefox", "VLC"]);

        // Assert
        Assert.True(result.IsValid);
    }

    /// <summary>
    /// Verifies that a profile with missing applications returns failure.
    /// </summary>
    [Fact]
    public void ValidateApplicationsExist_MissingApps_ReturnsFailure()
    {
        // Arrange
        var profile = new DeploymentProfileModel { Name = "Test" };
        profile.Applications.Add(new ApplicationModel { AppId = "Chrome", Name = "Google Chrome" });
        profile.Applications.Add(new ApplicationModel { AppId = "NonExistentApp", Name = "Unknown" });

        // Act
        var result = _service.ValidateApplicationsExist(profile, ["Chrome", "Firefox"]);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("NonExistentApp", result.ErrorMessage!);
    }

    /// <summary>
    /// Verifies that application matching is case-insensitive.
    /// </summary>
    [Fact]
    public void ValidateApplicationsExist_CaseInsensitiveMatch_ReturnsSuccess()
    {
        // Arrange
        var profile = new DeploymentProfileModel { Name = "Test" };
        profile.Applications.Add(new ApplicationModel { AppId = "chrome", Name = "Google Chrome" });

        // Act
        var result = _service.ValidateApplicationsExist(profile, ["Chrome"]);

        // Assert
        Assert.True(result.IsValid);
    }

    /// <summary>
    /// Verifies that null/empty AppId values are ignored in validation.
    /// </summary>
    [Fact]
    public void ValidateApplicationsExist_EmptyAppId_IsIgnored()
    {
        // Arrange
        var profile = new DeploymentProfileModel { Name = "Test" };
        profile.Applications.Add(new ApplicationModel { AppId = string.Empty, Name = "Empty ID" });
        profile.Applications.Add(new ApplicationModel { AppId = "Chrome", Name = "Google Chrome" });

        // Act
        var result = _service.ValidateApplicationsExist(profile, ["Chrome"]);

        // Assert
        Assert.True(result.IsValid);
    }

    /// <summary>
    /// Verifies that many missing apps are truncated in the error message.
    /// </summary>
    [Fact]
    public void ValidateApplicationsExist_ManyMissingApps_TruncatesMessage()
    {
        // Arrange
        var profile = new DeploymentProfileModel { Name = "Test" };
        for (int i = 1; i <= 15; i++)
        {
            profile.Applications.Add(new ApplicationModel { AppId = $"MissingApp{i}", Name = $"Missing {i}" });
        }

        // Act
        var result = _service.ValidateApplicationsExist(profile, []);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("and 5 more", result.ErrorMessage!);
    }

    /// <summary>
    /// Verifies that duplicate missing apps are only reported once.
    /// </summary>
    [Fact]
    public void ValidateApplicationsExist_DuplicateMissingApps_ReportsDistinct()
    {
        // Arrange
        var profile = new DeploymentProfileModel { Name = "Test" };
        profile.Applications.Add(new ApplicationModel { AppId = "MissingApp", Name = "Missing 1" });
        profile.Applications.Add(new ApplicationModel { AppId = "MissingApp", Name = "Missing 2" });

        // Act
        var result = _service.ValidateApplicationsExist(profile, []);

        // Assert
        Assert.False(result.IsValid);
        // "MissingApp" should appear only once in the error message
        var occurrences = result.ErrorMessage!.Split("MissingApp").Length - 1;
        Assert.Equal(1, occurrences);
    }

    #endregion

    #region ValidateProfile (Composite Validation)

    /// <summary>
    /// Verifies that a fully valid profile returns no validation errors.
    /// </summary>
    [Fact]
    public void ValidateProfile_ValidProfile_ReturnsNoErrors()
    {
        // Arrange
        var profile = new DeploymentProfileModel
        {
            Name = "Gaming",
            Description = "Gaming profile",
            Version = "1.0.0",
            InheritedFrom = ["Base"]
        };
        profile.Applications.Add(new ApplicationModel { AppId = "Steam", Name = "Steam" });

        IReadOnlyList<string>? GetParents(string name) => name switch
        {
            "Base" => [],
            _ => null
        };

        // Act
        var results = _service.ValidateProfile(
            profile,
            ["Base", "Gaming"],
            ["Steam", "Discord"],
            GetParents);

        // Assert
        Assert.Empty(results);
    }

    /// <summary>
    /// Verifies that multiple validation issues are collected together.
    /// </summary>
    [Fact]
    public void ValidateProfile_MultipleIssues_ReturnsAllErrors()
    {
        // Arrange - Profile with invalid name, missing parent, and missing app
        var profile = new DeploymentProfileModel
        {
            Name = string.Empty, // Invalid - required
            InheritedFrom = ["NonExistentParent"]
        };
        profile.Applications.Add(new ApplicationModel { AppId = "MissingApp", Name = "Missing" });

        IReadOnlyList<string>? GetParents(string _) => null;

        // Act
        var results = _service.ValidateProfile(
            profile,
            [],
            [],
            GetParents).ToList();

        // Assert
        Assert.True(results.Count >= 2); // At least name + inheritance issues
    }

    /// <summary>
    /// Verifies that circular inheritance is detected in composite validation.
    /// </summary>
    [Fact]
    public void ValidateProfile_CircularInheritance_ReturnsError()
    {
        // Arrange
        var profile = new DeploymentProfileModel
        {
            Name = "A",
            InheritedFrom = ["B"]
        };

        IReadOnlyList<string>? GetParents(string name) => name switch
        {
            "B" => new List<string> { "A" },
            _ => null
        };

        // Act
        var results = _service.ValidateProfile(
            profile,
            ["A", "B"],
            [],
            GetParents).ToList();

        // Assert
        Assert.Contains(results, r => !r.IsValid &&
            r.ErrorMessage!.Contains("Circular", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Verifies that self-referencing profile (from IValidatableObject) is caught.
    /// </summary>
    [Fact]
    public void ValidateProfile_SelfReference_ReturnsError()
    {
        // Arrange
        var profile = new DeploymentProfileModel
        {
            Name = "SelfRef",
            InheritedFrom = ["SelfRef"]
        };

        IReadOnlyList<string>? GetParents(string _) => null;

        // Act
        var results = _service.ValidateProfile(
            profile,
            ["SelfRef"],
            [],
            GetParents).ToList();

        // Assert
        Assert.NotEmpty(results);
    }

    /// <summary>
    /// Verifies that duplicate parents are caught by IValidatableObject in composite validation.
    /// </summary>
    [Fact]
    public void ValidateProfile_DuplicateParents_ReturnsError()
    {
        // Arrange
        var profile = new DeploymentProfileModel
        {
            Name = "Test",
            InheritedFrom = ["Base", "Base"]
        };

        IReadOnlyList<string>? GetParents(string name) => name switch
        {
            "Base" => [],
            _ => null
        };

        // Act
        var results = _service.ValidateProfile(
            profile,
            ["Base", "Test"],
            [],
            GetParents).ToList();

        // Assert
        Assert.NotEmpty(results);
    }

    /// <summary>
    /// Verifies that missing applications are caught in composite validation.
    /// </summary>
    [Fact]
    public void ValidateProfile_MissingApplications_ReturnsError()
    {
        // Arrange
        var profile = new DeploymentProfileModel
        {
            Name = "Test"
        };
        profile.Applications.Add(new ApplicationModel { AppId = "NonExistent", Name = "Unknown App" });

        IReadOnlyList<string>? GetParents(string _) => null;

        // Act
        var results = _service.ValidateProfile(
            profile,
            ["Test"],
            ["ExistingApp"],
            GetParents).ToList();

        // Assert
        Assert.Contains(results, r => !r.IsValid &&
            r.ErrorMessage!.Contains("NonExistent"));
    }

    #endregion
}

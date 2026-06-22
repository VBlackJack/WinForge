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

using System.ComponentModel.DataAnnotations;
using Win11Forge.GUI.Models;
using Win11Forge.GUI.Services;
using DataAnnotationsValidationResult = System.ComponentModel.DataAnnotations.ValidationResult;

namespace Win11Forge.GUI.Tests;

/// <summary>
/// Tests for ValidationService - DataAnnotations-based model validation.
/// </summary>
public class ValidationServiceTests
{
    private readonly ValidationService _service;

    public ValidationServiceTests()
    {
        _service = new ValidationService();
    }

    #region Validate<T>

    /// <summary>
    /// Validates that a null model returns a single error result.
    /// </summary>
    [Fact]
    public void Validate_NullModel_ReturnsNullError()
    {
        // Act
        IList<DataAnnotationsValidationResult> results = _service.Validate<DeploymentProfileModel>(null!);

        // Assert
        Assert.Single(results);
        Assert.Contains("null", results[0].ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Validates that a valid model returns no errors.
    /// </summary>
    [Fact]
    public void Validate_ValidModel_ReturnsEmptyList()
    {
        // Arrange
        DeploymentProfileModel profile = new DeploymentProfileModel
        {
            Name = "TestProfile",
            Description = "A valid test profile"
        };

        // Act
        IList<DataAnnotationsValidationResult> results = _service.Validate(profile);

        // Assert
        Assert.Empty(results);
    }

    /// <summary>
    /// Validates that a model with missing required field returns errors.
    /// </summary>
    [Fact]
    public void Validate_MissingRequiredField_ReturnsErrors()
    {
        // Arrange - Name is required but empty
        DeploymentProfileModel profile = new DeploymentProfileModel
        {
            Name = string.Empty
        };

        // Act
        IList<DataAnnotationsValidationResult> results = _service.Validate(profile);

        // Assert
        Assert.NotEmpty(results);
    }

    /// <summary>
    /// Validates that a model with an invalid pattern returns errors.
    /// </summary>
    [Fact]
    public void Validate_InvalidNamePattern_ReturnsErrors()
    {
        // Arrange - Name contains invalid characters (spaces not allowed by regex)
        DeploymentProfileModel profile = new DeploymentProfileModel
        {
            Name = "Invalid Profile Name!"
        };

        // Act
        IList<DataAnnotationsValidationResult> results = _service.Validate(profile);

        // Assert
        Assert.NotEmpty(results);
    }

    /// <summary>
    /// Validates that a model with valid hyphenated name passes.
    /// </summary>
    [Fact]
    public void Validate_HyphenatedName_PassesValidation()
    {
        // Arrange
        DeploymentProfileModel profile = new DeploymentProfileModel
        {
            Name = "My-Profile_v2"
        };

        // Act
        IList<DataAnnotationsValidationResult> results = _service.Validate(profile);

        // Assert
        Assert.Empty(results);
    }

    /// <summary>
    /// Validates that a profile exceeding name length returns errors.
    /// </summary>
    [Fact]
    public void Validate_NameExceedsMaxLength_ReturnsErrors()
    {
        // Arrange - Name max is 128 chars
        DeploymentProfileModel profile = new DeploymentProfileModel
        {
            Name = new string('A', 200)
        };

        // Act
        IList<DataAnnotationsValidationResult> results = _service.Validate(profile);

        // Assert
        Assert.NotEmpty(results);
    }

    /// <summary>
    /// Validates that a profile with invalid version pattern returns errors.
    /// </summary>
    [Fact]
    public void Validate_InvalidVersionPattern_ReturnsErrors()
    {
        // Arrange - Version must match ^\d+\.\d+(\.\d+)?$
        DeploymentProfileModel profile = new DeploymentProfileModel
        {
            Name = "TestProfile",
            Version = "not-a-version"
        };

        // Act
        IList<DataAnnotationsValidationResult> results = _service.Validate(profile);

        // Assert
        Assert.NotEmpty(results);
    }

    /// <summary>
    /// Validates that a profile with valid version passes.
    /// </summary>
    [Theory]
    [InlineData("1.0")]
    [InlineData("1.0.0")]
    [InlineData("10.20.30")]
    public void Validate_ValidVersion_PassesValidation(string version)
    {
        // Arrange
        DeploymentProfileModel profile = new DeploymentProfileModel
        {
            Name = "TestProfile",
            Version = version
        };

        // Act
        IList<DataAnnotationsValidationResult> results = _service.Validate(profile);

        // Assert
        Assert.Empty(results);
    }

    /// <summary>
    /// Validates that a profile with too many parents returns errors.
    /// </summary>
    [Fact]
    public void Validate_TooManyInheritedProfiles_ReturnsErrors()
    {
        // Arrange - MaxLength of InheritedFrom is 10
        DeploymentProfileModel profile = new DeploymentProfileModel
        {
            Name = "TestProfile"
        };
        for (int i = 0; i < 15; i++)
        {
            profile.InheritedFrom.Add($"Parent{i}");
        }

        // Act
        IList<DataAnnotationsValidationResult> results = _service.Validate(profile);

        // Assert
        Assert.NotEmpty(results);
    }

    #endregion

    #region ValidateAndThrow<T>

    /// <summary>
    /// Validates that ValidateAndThrow throws for null model.
    /// </summary>
    [Fact]
    public void ValidateAndThrow_NullModel_ThrowsValidationException()
    {
        // Act & Assert
        Assert.Throws<ValidationException>(() =>
            _service.ValidateAndThrow<DeploymentProfileModel>(null!));
    }

    /// <summary>
    /// Validates that ValidateAndThrow does not throw for valid model.
    /// </summary>
    [Fact]
    public void ValidateAndThrow_ValidModel_DoesNotThrow()
    {
        // Arrange
        DeploymentProfileModel profile = new DeploymentProfileModel
        {
            Name = "ValidProfile",
            Description = "A valid profile"
        };

        // Act & Assert - should not throw
        Exception exception = Record.Exception(() => _service.ValidateAndThrow(profile));
        Assert.Null(exception);
    }

    /// <summary>
    /// Validates that ValidateAndThrow throws for invalid model.
    /// </summary>
    [Fact]
    public void ValidateAndThrow_InvalidModel_ThrowsValidationException()
    {
        // Arrange
        DeploymentProfileModel profile = new DeploymentProfileModel
        {
            Name = string.Empty // Required field
        };

        // Act & Assert
        Assert.Throws<ValidationException>(() => _service.ValidateAndThrow(profile));
    }

    #endregion

    #region IsValid<T>

    /// <summary>
    /// Validates that IsValid returns false for null model.
    /// </summary>
    [Fact]
    public void IsValid_NullModel_ReturnsFalse()
    {
        // Act
        bool result = _service.IsValid<DeploymentProfileModel>(null!);

        // Assert
        Assert.False(result);
    }

    /// <summary>
    /// Validates that IsValid returns true for valid model.
    /// </summary>
    [Fact]
    public void IsValid_ValidModel_ReturnsTrue()
    {
        // Arrange
        DeploymentProfileModel profile = new DeploymentProfileModel
        {
            Name = "ValidProfile"
        };

        // Act
        bool result = _service.IsValid(profile);

        // Assert
        Assert.True(result);
    }

    /// <summary>
    /// Validates that IsValid returns false for invalid model.
    /// </summary>
    [Fact]
    public void IsValid_InvalidModel_ReturnsFalse()
    {
        // Arrange
        DeploymentProfileModel profile = new DeploymentProfileModel
        {
            Name = string.Empty
        };

        // Act
        bool result = _service.IsValid(profile);

        // Assert
        Assert.False(result);
    }

    #endregion

    #region GetValidationErrorsAsString<T>

    /// <summary>
    /// Validates that GetValidationErrorsAsString returns null for valid model.
    /// </summary>
    [Fact]
    public void GetValidationErrorsAsString_ValidModel_ReturnsNull()
    {
        // Arrange
        DeploymentProfileModel profile = new DeploymentProfileModel
        {
            Name = "ValidProfile"
        };

        // Act
        string? result = _service.GetValidationErrorsAsString(profile);

        // Assert
        Assert.Null(result);
    }

    /// <summary>
    /// Validates that GetValidationErrorsAsString returns error string for invalid model.
    /// </summary>
    [Fact]
    public void GetValidationErrorsAsString_InvalidModel_ReturnsErrorString()
    {
        // Arrange
        DeploymentProfileModel profile = new DeploymentProfileModel
        {
            Name = string.Empty
        };

        // Act
        string? result = _service.GetValidationErrorsAsString(profile);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    /// <summary>
    /// Validates that GetValidationErrorsAsString returns error string for null model.
    /// </summary>
    [Fact]
    public void GetValidationErrorsAsString_NullModel_ReturnsNullErrorString()
    {
        // Act
        string? result = _service.GetValidationErrorsAsString<DeploymentProfileModel>(null!);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("null", result, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Validates that GetValidationErrorsAsString includes member names when available.
    /// </summary>
    [Fact]
    public void GetValidationErrorsAsString_InvalidModel_IncludesMemberNames()
    {
        // Arrange - Invalid pattern will produce member name
        DeploymentProfileModel profile = new DeploymentProfileModel
        {
            Name = "Invalid Name With Spaces!"
        };

        // Act
        string? result = _service.GetValidationErrorsAsString(profile);

        // Assert
        Assert.NotNull(result);
        // Error should reference the Name property
        Assert.Contains("Name", result);
    }

    #endregion

    #region IValidatableObject Integration

    /// <summary>
    /// Validates that self-referencing inheritance is caught by IValidatableObject.
    /// </summary>
    [Fact]
    public void Validate_ProfileInheritsFromSelf_ReturnsErrors()
    {
        // Arrange
        DeploymentProfileModel profile = new DeploymentProfileModel
        {
            Name = "SelfRef",
            InheritedFrom = ["SelfRef"]
        };

        // Act
        IList<DataAnnotationsValidationResult> results = _service.Validate(profile);

        // Assert
        Assert.NotEmpty(results);
    }

    /// <summary>
    /// Validates that duplicate parent profiles are caught by IValidatableObject.
    /// </summary>
    [Fact]
    public void Validate_DuplicateParents_ReturnsErrors()
    {
        // Arrange
        DeploymentProfileModel profile = new DeploymentProfileModel
        {
            Name = "TestProfile",
            InheritedFrom = ["Base", "Base"]
        };

        // Act
        IList<DataAnnotationsValidationResult> results = _service.Validate(profile);

        // Assert
        Assert.NotEmpty(results);
    }

    /// <summary>
    /// Validates that duplicate application IDs are caught by IValidatableObject.
    /// </summary>
    [Fact]
    public void Validate_DuplicateApplicationIds_ReturnsErrors()
    {
        // Arrange
        DeploymentProfileModel profile = new DeploymentProfileModel
        {
            Name = "TestProfile"
        };
        profile.Applications.Add(new ApplicationModel { AppId = "App1", Name = "Application 1" });
        profile.Applications.Add(new ApplicationModel { AppId = "App1", Name = "Application 1 Duplicate" });

        // Act
        IList<DataAnnotationsValidationResult> results = _service.Validate(profile);

        // Assert
        Assert.NotEmpty(results);
    }

    #endregion
}

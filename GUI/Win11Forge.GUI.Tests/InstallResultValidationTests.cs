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

namespace Win11Forge.GUI.Tests;

/// <summary>
/// Tests for InstallResult model validation.
/// </summary>
public class InstallResultValidationTests
{
    /// <summary>
    /// Validates an object using DataAnnotations.
    /// </summary>
    private static IList<ValidationResult> ValidateModel(object model)
    {
        var validationResults = new List<ValidationResult>();
        var ctx = new ValidationContext(model, null, null);
        Validator.TryValidateObject(model, ctx, validationResults, true);
        return validationResults;
    }

    /// <summary>
    /// Successful result should pass validation.
    /// </summary>
    [Fact]
    public void Successful_Result_Passes_Validation()
    {
        // Arrange
        var result = InstallResult.Successful("Installed successfully", "Installation logs...");

        // Act
        var validationResults = ValidateModel(result);

        // Assert
        Assert.Empty(validationResults);
    }

    /// <summary>
    /// Failed result with message should pass validation.
    /// </summary>
    [Fact]
    public void Failed_Result_With_Message_Passes_Validation()
    {
        // Arrange
        var result = InstallResult.Failed("Installation failed: error XYZ", "Error logs...");

        // Act
        var validationResults = ValidateModel(result);

        // Assert
        Assert.Empty(validationResults);
    }

    /// <summary>
    /// Dry run result should pass validation.
    /// </summary>
    [Fact]
    public void DryRun_Result_Passes_Validation()
    {
        // Arrange
        var result = InstallResult.DryRun("TestApp");

        // Act
        var validationResults = ValidateModel(result);

        // Assert
        Assert.Empty(validationResults);
    }

    /// <summary>
    /// Manual install required with Success=true should fail validation.
    /// </summary>
    [Fact]
    public void ManualInstall_With_Success_Fails_Validation()
    {
        // Arrange - Create invalid state where manual install is marked as success
        var result = new InstallResult
        {
            Success = true,
            IsManualInstallRequired = true,
            Message = "Test"
        };

        // Act
        var validationResults = ValidateModel(result);

        // Assert
        Assert.NotEmpty(validationResults);
        Assert.Contains(validationResults, vr =>
            vr.MemberNames.Contains(nameof(InstallResult.IsManualInstallRequired)));
    }

    /// <summary>
    /// Dry run result with Success=false should fail validation.
    /// </summary>
    [Fact]
    public void DryRun_With_Failure_Fails_Validation()
    {
        // Arrange - Create invalid state where dry run is marked as failed
        var result = new InstallResult
        {
            Success = false,
            IsDryRun = true,
            Message = "Simulated failure"
        };

        // Act
        var validationResults = ValidateModel(result);

        // Assert
        Assert.NotEmpty(validationResults);
        Assert.Contains(validationResults, vr =>
            vr.MemberNames.Contains(nameof(InstallResult.IsDryRun)));
    }

    /// <summary>
    /// Failed result without message should fail validation.
    /// </summary>
    [Fact]
    public void Failed_Without_Message_Fails_Validation()
    {
        // Arrange - Create invalid state where failure has no message
        var result = new InstallResult
        {
            Success = false,
            Message = ""
        };

        // Act
        var validationResults = ValidateModel(result);

        // Assert
        Assert.NotEmpty(validationResults);
        Assert.Contains(validationResults, vr =>
            vr.MemberNames.Contains(nameof(InstallResult.Message)));
    }

    /// <summary>
    /// Manual install required result should have IsManualInstallRequired=true and Success=false.
    /// </summary>
    [Fact]
    public void ManualInstallRequired_Factory_Creates_Valid_Result()
    {
        // Arrange & Act
        var result = InstallResult.ManualInstallRequired("TestApp", "https://example.com/download");

        // Assert
        Assert.True(result.IsManualInstallRequired);
        Assert.False(result.Success);
        Assert.Contains("TestApp", result.Message);

        // Validate
        var validationResults = ValidateModel(result);
        Assert.Empty(validationResults);
    }

    /// <summary>
    /// UpdateCheckResult with update available should have HasUpdate=true.
    /// </summary>
    [Fact]
    public void UpdateAvailable_Factory_Creates_Correct_Result()
    {
        // Arrange & Act
        var result = UpdateCheckResult.UpdateAvailable("1.0.0", "2.0.0");

        // Assert
        Assert.True(result.HasUpdate);
        Assert.Equal("1.0.0", result.CurrentVersion);
        Assert.Equal("2.0.0", result.AvailableVersion);
        Assert.Null(result.ErrorMessage);
    }

    /// <summary>
    /// UpdateCheckResult up to date should have HasUpdate=false.
    /// </summary>
    [Fact]
    public void UpToDate_Factory_Creates_Correct_Result()
    {
        // Arrange & Act
        var result = UpdateCheckResult.UpToDate("1.0.0");

        // Assert
        Assert.False(result.HasUpdate);
        Assert.Equal("1.0.0", result.CurrentVersion);
        Assert.Null(result.ErrorMessage);
    }

    /// <summary>
    /// UpdateCheckResult failed should have error message.
    /// </summary>
    [Fact]
    public void Failed_UpdateCheck_Has_ErrorMessage()
    {
        // Arrange & Act
        var result = UpdateCheckResult.Failed("Network error");

        // Assert
        Assert.False(result.HasUpdate);
        Assert.Equal("Network error", result.ErrorMessage);
    }
}

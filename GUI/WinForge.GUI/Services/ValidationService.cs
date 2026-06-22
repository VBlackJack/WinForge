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
using System.ComponentModel.DataAnnotations;
using System.Linq;
using DataAnnotations = System.ComponentModel.DataAnnotations;

namespace WinForge.GUI.Services;

/// <summary>
/// Service for validating models using DataAnnotations.
/// </summary>
public interface IValidationService
{
    /// <summary>
    /// Validates a model and returns validation results.
    /// </summary>
    /// <typeparam name="T">The type of model to validate.</typeparam>
    /// <param name="model">The model instance to validate.</param>
    /// <returns>A list of validation results. Empty if valid.</returns>
    IList<DataAnnotations.ValidationResult> Validate<T>(T model) where T : class;

    /// <summary>
    /// Validates a model and throws if invalid.
    /// </summary>
    /// <typeparam name="T">The type of model to validate.</typeparam>
    /// <param name="model">The model instance to validate.</param>
    /// <exception cref="DataAnnotations.ValidationException">Thrown when validation fails.</exception>
    void ValidateAndThrow<T>(T model) where T : class;

    /// <summary>
    /// Checks if a model is valid.
    /// </summary>
    /// <typeparam name="T">The type of model to validate.</typeparam>
    /// <param name="model">The model instance to validate.</param>
    /// <returns>True if valid, false otherwise.</returns>
    bool IsValid<T>(T model) where T : class;

    /// <summary>
    /// Gets validation errors as a formatted string.
    /// </summary>
    /// <typeparam name="T">The type of model to validate.</typeparam>
    /// <param name="model">The model instance to validate.</param>
    /// <returns>A formatted error string, or null if valid.</returns>
    string? GetValidationErrorsAsString<T>(T model) where T : class;
}

/// <summary>
/// Implementation of the validation service using DataAnnotations.
/// </summary>
public class ValidationService : IValidationService
{
    /// <inheritdoc/>
    public IList<DataAnnotations.ValidationResult> Validate<T>(T model) where T : class
    {
        if (model == null)
        {
            return new List<DataAnnotations.ValidationResult>
            {
                new DataAnnotations.ValidationResult("Model cannot be null")
            };
        }

        List<DataAnnotations.ValidationResult> results = new List<DataAnnotations.ValidationResult>();
        ValidationContext context = new DataAnnotations.ValidationContext(model);

        DataAnnotations.Validator.TryValidateObject(model, context, results, validateAllProperties: true);

        return results;
    }

    /// <inheritdoc/>
    public void ValidateAndThrow<T>(T model) where T : class
    {
        if (model == null)
        {
            throw new DataAnnotations.ValidationException("Model cannot be null");
        }

        ValidationContext context = new DataAnnotations.ValidationContext(model);
        DataAnnotations.Validator.ValidateObject(model, context, validateAllProperties: true);
    }

    /// <inheritdoc/>
    public bool IsValid<T>(T model) where T : class
    {
        if (model == null) return false;

        List<DataAnnotations.ValidationResult> results = new List<DataAnnotations.ValidationResult>();
        ValidationContext context = new DataAnnotations.ValidationContext(model);

        return DataAnnotations.Validator.TryValidateObject(model, context, results, validateAllProperties: true);
    }

    /// <inheritdoc/>
    public string? GetValidationErrorsAsString<T>(T model) where T : class
    {
        IList<DataAnnotations.ValidationResult> results = Validate(model);

        if (results.Count == 0) return null;

        IEnumerable<string> errors = results
            .Select(r =>
            {
                string members = r.MemberNames.Any()
                    ? $"[{string.Join(", ", r.MemberNames)}] "
                    : "";
                return $"{members}{r.ErrorMessage}";
            });

        return string.Join(Environment.NewLine, errors);
    }
}

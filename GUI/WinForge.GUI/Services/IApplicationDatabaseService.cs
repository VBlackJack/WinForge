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

namespace Win11Forge.GUI.Services;

/// <summary>
/// Service interface for managing the application database.
/// Provides CRUD operations for applications in the applications.json file.
/// </summary>
public interface IApplicationDatabaseService
{
    /// <summary>
    /// Loads all applications from the database.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of all applications.</returns>
    Task<IEnumerable<EditableApplicationModel>> LoadApplicationsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific application by ID.
    /// </summary>
    /// <param name="appId">Application identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The application if found, null otherwise.</returns>
    Task<EditableApplicationModel?> GetApplicationAsync(string appId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves an application to the database (add or update).
    /// </summary>
    /// <param name="application">Application to save.</param>
    /// <param name="isNew">True if adding new application, false if updating.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result of the save operation.</returns>
    Task<ApplicationSaveResult> SaveApplicationAsync(EditableApplicationModel application, bool isNew, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an application from the database.
    /// </summary>
    /// <param name="appId">Application identifier to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if deleted successfully.</returns>
    Task<bool> DeleteApplicationAsync(string appId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates an application configuration.
    /// </summary>
    /// <param name="application">Application to validate.</param>
    /// <param name="isNew">True if validating for new application (checks ID uniqueness).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Validation result with any errors.</returns>
    Task<ApplicationValidationResult> ValidateApplicationAsync(EditableApplicationModel application, bool isNew, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if an application with the given ID exists.
    /// </summary>
    /// <param name="appId">Application identifier to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if application exists.</returns>
    Task<bool> ApplicationExistsAsync(string appId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all unique categories from the database.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of category names.</returns>
    Task<IEnumerable<string>> GetCategoriesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Imports applications from a JSON file.
    /// </summary>
    /// <param name="filePath">Path to the JSON file.</param>
    /// <param name="mode">Import mode (merge or replace).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result of the import operation.</returns>
    Task<ApplicationImportResult> ImportApplicationsAsync(string filePath, ImportMode mode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Exports applications to a JSON file.
    /// </summary>
    /// <param name="appIds">Application IDs to export.</param>
    /// <param name="filePath">Destination file path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if export successful.</returns>
    Task<bool> ExportApplicationsAsync(IEnumerable<string> appIds, string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a backup of the current database.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Path to the backup file.</returns>
    Task<string> CreateBackupAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the path to the application database file.
    /// </summary>
    string DatabasePath { get; }

    /// <summary>
    /// Event raised when the database is modified.
    /// </summary>
    event EventHandler<DatabaseChangedEventArgs>? DatabaseChanged;
}

/// <summary>
/// Result of a save operation.
/// </summary>
public record ApplicationSaveResult(
    bool Success,
    string? ErrorMessage = null,
    IEnumerable<string>? ValidationErrors = null
);

/// <summary>
/// Result of a validation operation.
/// </summary>
public record ApplicationValidationResult(
    bool IsValid,
    IEnumerable<ApplicationValidationError> Errors
)
{
    /// <summary>Creates a successful validation result.</summary>
    public static ApplicationValidationResult Valid() =>
        new(true, Enumerable.Empty<ApplicationValidationError>());

    /// <summary>Creates a failed validation result with errors.</summary>
    public static ApplicationValidationResult Invalid(IEnumerable<ApplicationValidationError> errors) =>
        new(false, errors);

    /// <summary>Creates a failed validation result with a single error.</summary>
    public static ApplicationValidationResult Invalid(string field, string message) =>
        new(false, new[] { new ApplicationValidationError(field, message) });
}

/// <summary>
/// Represents a single validation error.
/// </summary>
public record ApplicationValidationError(
    string Field,
    string Message
);

/// <summary>
/// Result of an import operation.
/// </summary>
public record ApplicationImportResult(
    bool Success,
    int AddedCount,
    int UpdatedCount,
    int SkippedCount,
    IEnumerable<string> Errors
)
{
    /// <summary>Total applications processed.</summary>
    public int TotalProcessed => AddedCount + UpdatedCount + SkippedCount;
}

/// <summary>
/// Import mode for application import.
/// </summary>
public enum ImportMode
{
    /// <summary>Merge with existing applications (skip duplicates).</summary>
    Merge,

    /// <summary>Replace existing applications with same ID.</summary>
    Replace,

    /// <summary>Replace entire database.</summary>
    ReplaceAll
}

/// <summary>
/// Event arguments for database change events.
/// </summary>
public class DatabaseChangedEventArgs : EventArgs
{
    /// <summary>Type of change that occurred.</summary>
    public DatabaseChangeType ChangeType { get; init; }

    /// <summary>Affected application IDs.</summary>
    public IEnumerable<string> AffectedAppIds { get; init; } = Enumerable.Empty<string>();

    /// <summary>Creates event args for an add operation.</summary>
    public static DatabaseChangedEventArgs Added(string appId) =>
        new() { ChangeType = DatabaseChangeType.Added, AffectedAppIds = new[] { appId } };

    /// <summary>Creates event args for an update operation.</summary>
    public static DatabaseChangedEventArgs Updated(string appId) =>
        new() { ChangeType = DatabaseChangeType.Updated, AffectedAppIds = new[] { appId } };

    /// <summary>Creates event args for a delete operation.</summary>
    public static DatabaseChangedEventArgs Deleted(string appId) =>
        new() { ChangeType = DatabaseChangeType.Deleted, AffectedAppIds = new[] { appId } };

    /// <summary>Creates event args for a reload operation.</summary>
    public static DatabaseChangedEventArgs Reloaded() =>
        new() { ChangeType = DatabaseChangeType.Reloaded };
}

/// <summary>
/// Type of database change.
/// </summary>
public enum DatabaseChangeType
{
    /// <summary>Application added.</summary>
    Added,

    /// <summary>Application updated.</summary>
    Updated,

    /// <summary>Application deleted.</summary>
    Deleted,

    /// <summary>Database reloaded from file.</summary>
    Reloaded,

    /// <summary>Multiple applications imported.</summary>
    Imported
}

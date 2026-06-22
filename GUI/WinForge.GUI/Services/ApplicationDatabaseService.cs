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

using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using WinForge.GUI.Models;
using DataValidationContext = System.ComponentModel.DataAnnotations.ValidationContext;
using DataValidationResult = System.ComponentModel.DataAnnotations.ValidationResult;
using DataValidator = System.ComponentModel.DataAnnotations.Validator;
using Loc = WinForge.GUI.Resources.Resources;

namespace WinForge.GUI.Services;

/// <summary>
/// Service implementation for managing the application database.
/// Uses PowerShell backend via IPowerShellBridge for database operations.
/// </summary>
public class ApplicationDatabaseService : IApplicationDatabaseService, IDisposable
{
    private bool _disposed;
    private readonly IPowerShellBridge _powerShellBridge;
    private readonly string _databasePath;
    private readonly string _repositoryRoot;
    private readonly ILoggingService _logger;
    private readonly SemaphoreSlim _operationLock = new(1, 1);

    /// <summary>
    /// Maximum allowed size for the applications database file (10MB).
    /// Prevents DoS attacks via extremely large JSON files.
    /// </summary>
    private const long MaxDatabaseFileSizeBytes = 10 * 1024 * 1024;

    private const string Sha256ChecksumPrefix = "sha256:";
    private const int Sha256HexLength = 64;

    /// <summary>
    /// JSON document options with security limits to prevent DoS attacks.
    /// </summary>
    private static readonly JsonDocumentOptions SecureJsonDocumentOptions = new()
    {
        MaxDepth = 64, // Reasonable limit for applications.json structure
        AllowTrailingCommas = false,
        CommentHandling = JsonCommentHandling.Skip
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        MaxDepth = 64
    };

    /// <inheritdoc/>
    public string DatabasePath => _databasePath;

    /// <inheritdoc/>
    public event EventHandler<DatabaseChangedEventArgs>? DatabaseChanged;

    /// <summary>
    /// Initializes a new instance of ApplicationDatabaseService.
    /// </summary>
    /// <param name="powerShellBridge">PowerShell bridge for script execution.</param>
    public ApplicationDatabaseService(IPowerShellBridge powerShellBridge, ILoggerFactory? loggerFactory = null)
    {
        _powerShellBridge = powerShellBridge ?? throw new ArgumentNullException(nameof(powerShellBridge));
        _repositoryRoot = powerShellBridge.RepositoryRoot;
        _databasePath = Path.Combine(_repositoryRoot, "Apps", "Database", "applications.json");
        _logger = (loggerFactory ?? new LoggerFactory()).CreateLogger<ApplicationDatabaseService>();
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<EditableApplicationModel>> LoadApplicationsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Read JSON file directly instead of using PowerShell - more reliable on first launch
        if (!File.Exists(_databasePath))
        {
            return Enumerable.Empty<EditableApplicationModel>();
        }

        List<EditableApplicationModel> applications = new List<EditableApplicationModel>();

        try
        {
            // Security: Validate file size to prevent DoS via large files
            FileInfo fileInfo = new FileInfo(_databasePath);
            if (fileInfo.Length > MaxDatabaseFileSizeBytes)
            {
                _logger.LogWarning($"Database file too large: {fileInfo.Length} bytes (max: {MaxDatabaseFileSizeBytes})");
                return Enumerable.Empty<EditableApplicationModel>();
            }

            string jsonContent = await File.ReadAllTextAsync(_databasePath, cancellationToken);
            // Security: Use secure JSON options with depth limit
            using JsonDocument document = JsonDocument.Parse(jsonContent, SecureJsonDocumentOptions);

            if (TryGetPropertyCaseInsensitive(document.RootElement, "Applications", out JsonElement appsElement))
            {
                foreach (JsonProperty appProperty in appsElement.EnumerateObject())
                {
                    string appId = appProperty.Name;
                    JsonElement appData = appProperty.Value;

                    EditableApplicationModel? app = ParseApplicationFromJson(appData);
                    if (app != null)
                    {
                        app.AppId = appId; // AppId is the property name in JSON
                        applications.Add(app);
                    }
                }
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            _logger.LogError("Failed to load applications", ex);
            // Return empty list on error
        }

        return applications;
    }

    /// <inheritdoc/>
    public async Task<EditableApplicationModel?> GetApplicationAsync(string appId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(appId))
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();

        // Read directly from JSON file
        if (!File.Exists(_databasePath))
        {
            return null;
        }

        try
        {
            // Security: Validate file size to prevent DoS via large files
            FileInfo fileInfo = new FileInfo(_databasePath);
            if (fileInfo.Length > MaxDatabaseFileSizeBytes)
            {
                _logger.LogWarning($"Database file too large: {fileInfo.Length} bytes");
                return null;
            }

            string jsonContent = await File.ReadAllTextAsync(_databasePath, cancellationToken);
            // Security: Use secure JSON options with depth limit
            using JsonDocument document = JsonDocument.Parse(jsonContent, SecureJsonDocumentOptions);

            if (TryGetPropertyCaseInsensitive(document.RootElement, "Applications", out JsonElement appsElement) &&
                TryGetPropertyCaseInsensitive(appsElement, appId, out JsonElement appData))
            {
                EditableApplicationModel? app = ParseApplicationFromJson(appData);
                if (app != null)
                {
                    app.AppId = appId;
                }
                return app;
            }

            return null;
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<ApplicationSaveResult> SaveApplicationAsync(
        EditableApplicationModel application,
        bool isNew,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(application);

        // Validate locally first
        ApplicationValidationResult validation = await ValidateApplicationAsync(application, isNew, cancellationToken);
        if (!validation.IsValid)
        {
            return new ApplicationSaveResult(
                false,
                Loc.Apps_ValidationFailed,
                validation.Errors.Select(e => $"{e.Field}: {e.Message}"));
        }

        await _operationLock.WaitAsync(cancellationToken);
        try
        {
            string appJson = ConvertToJsonObject(application);

            string script = $@"
                Import-Module '{EscapePath(Path.Combine(_repositoryRoot, "Modules", "ApplicationDatabase.psm1"))}' -Force
                $appData = @'
{appJson}
'@ | ConvertFrom-Json
                $result = Set-Application -Application $appData
                $result | ConvertTo-Json -Depth 5 -Compress
            ";

            string result = await ExecutePowerShellAsync(script, cancellationToken);

            if (string.IsNullOrWhiteSpace(result))
            {
                return new ApplicationSaveResult(false, Loc.Apps_SaveFailed);
            }

            using JsonDocument document = JsonDocument.Parse(result);
            JsonElement root = document.RootElement;

            bool success = root.TryGetProperty("Success", out JsonElement successProp) && successProp.GetBoolean();

            if (!success)
            {
                List<string> errors = new List<string>();
                if (root.TryGetProperty("Errors", out JsonElement errorsProp) && errorsProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement error in errorsProp.EnumerateArray())
                    {
                        string? field = error.TryGetProperty("Field", out JsonElement fieldProp) ? fieldProp.GetString() : "Unknown";
                        string? message = error.TryGetProperty("Message", out JsonElement msgProp) ? msgProp.GetString() : "Error";
                        errors.Add($"{field}: {message}");
                    }
                }

                return new ApplicationSaveResult(false, Loc.Apps_SaveFailed, errors);
            }

            // Raise event
            DatabaseChangeType changeType = isNew ? DatabaseChangeType.Added : DatabaseChangeType.Updated;
            OnDatabaseChanged(new DatabaseChangedEventArgs
            {
                ChangeType = changeType,
                AffectedAppIds = new[] { application.AppId }
            });

            return new ApplicationSaveResult(true);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteApplicationAsync(string appId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(appId))
        {
            return false;
        }

        await _operationLock.WaitAsync(cancellationToken);
        try
        {
            string script = $@"
                Import-Module '{EscapePath(Path.Combine(_repositoryRoot, "Modules", "ApplicationDatabase.psm1"))}' -Force
                $result = Remove-Application -AppId '{EscapeForPowerShell(appId)}'
                $result | ConvertTo-Json -Depth 5 -Compress
            ";

            string result = await ExecutePowerShellAsync(script, cancellationToken);

            if (string.IsNullOrWhiteSpace(result))
            {
                return false;
            }

            using JsonDocument document = JsonDocument.Parse(result);
            bool success = document.RootElement.TryGetProperty("Success", out JsonElement successProp) && successProp.GetBoolean();

            if (success)
            {
                OnDatabaseChanged(DatabaseChangedEventArgs.Deleted(appId));
            }

            return success;
        }
        finally
        {
            _operationLock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<ApplicationValidationResult> ValidateApplicationAsync(
        EditableApplicationModel application,
        bool isNew,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(application);

        List<ApplicationValidationError> errors = new List<ApplicationValidationError>();

        // Local validation using DataAnnotations
        DataValidationContext validationContext = new DataValidationContext(application);
        List<DataValidationResult> validationResults = new List<DataValidationResult>();
        DataValidator.TryValidateObject(application, validationContext, validationResults, true);

        foreach (DataValidationResult vr in validationResults)
        {
            string fieldName = vr.MemberNames.FirstOrDefault() ?? "Unknown";
            errors.Add(new ApplicationValidationError(fieldName, vr.ErrorMessage ?? "Validation failed"));
        }

        // Validate sources
        if (application.Sources == null || !application.Sources.HasAnySource())
        {
            errors.Add(new ApplicationValidationError(nameof(application.Sources), Loc.Validation_AtLeastOneSource));
        }

        if (application.Sources?.DirectDownloadConfig != null)
        {
            AddNestedValidationErrors(
                application.Sources.DirectDownloadConfig,
                $"{nameof(application.Sources)}.{nameof(application.Sources.DirectDownloadConfig)}",
                errors);
        }

        // Check ID uniqueness for new applications
        if (isNew && !string.IsNullOrWhiteSpace(application.AppId))
        {
            if (await ApplicationExistsAsync(application.AppId, cancellationToken))
            {
                errors.Add(new ApplicationValidationError(
                    nameof(application.AppId),
                    string.Format(Loc.Validation_DuplicateAppId, application.AppId)));
            }
        }

        return errors.Count == 0
            ? ApplicationValidationResult.Valid()
            : ApplicationValidationResult.Invalid(errors);
    }

    private static void AddNestedValidationErrors(
        object instance,
        string fieldPrefix,
        ICollection<ApplicationValidationError> errors)
    {
        DataValidationContext validationContext = new DataValidationContext(instance);
        List<DataValidationResult> validationResults = new List<DataValidationResult>();
        DataValidator.TryValidateObject(instance, validationContext, validationResults, validateAllProperties: true);

        foreach (DataValidationResult validationResult in validationResults)
        {
            string memberName = validationResult.MemberNames.FirstOrDefault() ?? string.Empty;
            string fieldName = string.IsNullOrWhiteSpace(memberName)
                ? fieldPrefix
                : $"{fieldPrefix}.{memberName}";
            errors.Add(new ApplicationValidationError(fieldName, validationResult.ErrorMessage ?? "Validation failed"));
        }
    }

    /// <inheritdoc/>
    public async Task<bool> ApplicationExistsAsync(string appId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(appId))
        {
            return false;
        }

        EditableApplicationModel? existing = await GetApplicationAsync(appId, cancellationToken);
        return existing != null;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<string>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Read categories directly from JSON file
        if (!File.Exists(_databasePath))
        {
            return Enumerable.Empty<string>();
        }

        try
        {
            string jsonContent = await File.ReadAllTextAsync(_databasePath, cancellationToken);
            using JsonDocument document = JsonDocument.Parse(jsonContent);

            HashSet<string> categories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (TryGetPropertyCaseInsensitive(document.RootElement, "Applications", out JsonElement appsElement))
            {
                foreach (JsonProperty appProperty in appsElement.EnumerateObject())
                {
                    if (TryGetPropertyCaseInsensitive(appProperty.Value, "Category", out JsonElement categoryElement) &&
                        categoryElement.ValueKind == JsonValueKind.String)
                    {
                        string? category = categoryElement.GetString();
                        if (!string.IsNullOrEmpty(category))
                        {
                            categories.Add(category);
                        }
                    }
                }
            }

            return categories.OrderBy(c => c).ToList();
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            return Enumerable.Empty<string>();
        }
    }

    /// <inheritdoc/>
    public async Task<ApplicationImportResult> ImportApplicationsAsync(
        string filePath,
        ImportMode mode,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return new ApplicationImportResult(false, 0, 0, 0, new[] { Loc.Apps_FileNotFound });
        }

        await _operationLock.WaitAsync(cancellationToken);
        try
        {
            string modeString = mode.ToString();

            string script = $@"
                Import-Module '{EscapePath(Path.Combine(_repositoryRoot, "Modules", "ApplicationDatabase.psm1"))}' -Force
                $result = Import-ApplicationsFromFile -Path '{EscapePath(filePath)}' -Mode '{modeString}'
                $result | ConvertTo-Json -Depth 5 -Compress
            ";

            string result = await ExecutePowerShellAsync(script, cancellationToken);

            if (string.IsNullOrWhiteSpace(result))
            {
                return new ApplicationImportResult(false, 0, 0, 0, new[] { Loc.Apps_ImportFailed });
            }

            using JsonDocument document = JsonDocument.Parse(result);
            JsonElement root = document.RootElement;

            bool success = root.TryGetProperty("Success", out JsonElement successProp) && successProp.GetBoolean();
            int added = root.TryGetProperty("Added", out JsonElement addedProp) ? addedProp.GetInt32() : 0;
            int updated = root.TryGetProperty("Updated", out JsonElement updatedProp) ? updatedProp.GetInt32() : 0;
            int skipped = root.TryGetProperty("Skipped", out JsonElement skippedProp) ? skippedProp.GetInt32() : 0;

            List<string> errors = new List<string>();
            if (root.TryGetProperty("Error", out JsonElement errorProp) && errorProp.ValueKind == JsonValueKind.String)
            {
                string? error = errorProp.GetString();
                if (!string.IsNullOrEmpty(error))
                {
                    errors.Add(error);
                }
            }

            if (success)
            {
                OnDatabaseChanged(new DatabaseChangedEventArgs
                {
                    ChangeType = DatabaseChangeType.Imported
                });
            }

            return new ApplicationImportResult(success, added, updated, skipped, errors);
        }
        finally
        {
            _operationLock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<bool> ExportApplicationsAsync(
        IEnumerable<string> appIds,
        string filePath,
        CancellationToken cancellationToken = default)
    {
        List<string>? appIdList = appIds?.ToList();
        if (appIdList == null || appIdList.Count == 0 || string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        // Validate and escape app IDs
        string escapedIds = string.Join("','", appIdList.Select(EscapeForPowerShell));

        string script = $@"
            Import-Module '{EscapePath(Path.Combine(_repositoryRoot, "Modules", "ApplicationDatabase.psm1"))}' -Force
            $appIds = @('{escapedIds}')
            Export-ApplicationsToFile -AppIds $appIds -Path '{EscapePath(filePath)}'
        ";

        string result = await ExecutePowerShellAsync(script, cancellationToken);

        // PowerShell returns "True" or "False" for boolean output
        return result?.Trim().Equals("True", StringComparison.OrdinalIgnoreCase) ?? false;
    }

    /// <inheritdoc/>
    public async Task<string> CreateBackupAsync(CancellationToken cancellationToken = default)
    {
        string script = $@"
            Import-Module '{EscapePath(Path.Combine(_repositoryRoot, "Modules", "ApplicationDatabase.psm1"))}' -Force
            New-DatabaseBackup
        ";

        string result = await ExecutePowerShellAsync(script, cancellationToken);
        return result?.Trim() ?? string.Empty;
    }

    #region Private Methods

    /// <summary>
    /// Executes a PowerShell script asynchronously.
    /// </summary>
    private async Task<string> ExecutePowerShellAsync(string script, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            return await _powerShellBridge.ExecuteCommandAsync(script, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError("PowerShell execution error", ex);
            return string.Empty;
        }
    }

    /// <summary>
    /// Parses an EditableApplicationModel from a JSON element.
    /// </summary>
    private static EditableApplicationModel? ParseApplicationFromJson(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        EditableApplicationModel app = new EditableApplicationModel
        {
            AppId = GetStringProperty(element, "AppId") ?? string.Empty,
            Name = GetStringProperty(element, "Name") ?? string.Empty,
            Category = GetStringProperty(element, "Category") ?? string.Empty,
            Description = GetStringProperty(element, "Description") ?? string.Empty,
            InstallArguments = GetStringProperty(element, "InstallArguments") ?? string.Empty,
            DefaultPriority = GetIntProperty(element, "DefaultPriority", 50),
            DefaultRequired = GetBoolProperty(element, "DefaultRequired"),
            LastVerified = GetStringProperty(element, "LastVerified") ?? string.Empty,
            Verified = GetBoolProperty(element, "Verified"),
            Homepage = GetStringProperty(element, "Homepage") ?? string.Empty
        };

        // Parse Sources
        if (TryGetPropertyCaseInsensitive(element, "Sources", out JsonElement sourcesElement) && sourcesElement.ValueKind == JsonValueKind.Object)
        {
            app.Sources = ParseSourcesFromJson(sourcesElement, app.InstallArguments);
        }

        // Parse Detection
        if (TryGetPropertyCaseInsensitive(element, "Detection", out JsonElement detectionElement) && detectionElement.ValueKind == JsonValueKind.Object)
        {
            app.Detection = ParseDetectionFromJson(detectionElement);
        }

        // Parse Tags
        if (TryGetPropertyCaseInsensitive(element, "Tags", out JsonElement tagsElement) && tagsElement.ValueKind == JsonValueKind.Array)
        {
            app.Tags = tagsElement.EnumerateArray()
                .Select(e => e.GetString())
                .Where(s => !string.IsNullOrEmpty(s))
                .Cast<string>()
                .ToList();
        }

        // Parse EnvironmentRestrictions
        if (TryGetPropertyCaseInsensitive(element, "EnvironmentRestrictions", out JsonElement envElement) && envElement.ValueKind == JsonValueKind.Array)
        {
            app.EnvironmentRestrictions = envElement.EnumerateArray()
                .Select(e => e.GetString())
                .Where(s => !string.IsNullOrEmpty(s))
                .Cast<string>()
                .ToList();
        }

        return app;
    }

    /// <summary>
    /// Parses ApplicationSourcesModel from JSON.
    /// </summary>
    private static ApplicationSourcesModel ParseSourcesFromJson(JsonElement element, string installArguments)
    {
        ApplicationSourcesModel sources = new ApplicationSourcesModel
        {
            Winget = GetStringProperty(element, "Winget"),
            Chocolatey = GetStringProperty(element, "Chocolatey"),
            Store = GetStringProperty(element, "Store"),
            DirectUrl = GetStringProperty(element, "DirectUrl"),
            ExpectedPublisher = GetStringProperty(element, "ExpectedPublisher")
        };

        // Parse extended configs
        if (TryGetPropertyCaseInsensitive(element, "WingetConfig", out JsonElement wingetConfig) && wingetConfig.ValueKind == JsonValueKind.Object)
        {
            sources.WingetConfig = new WingetSourceConfig
            {
                Version = GetStringProperty(wingetConfig, "Version") ?? string.Empty,
                Source = GetStringProperty(wingetConfig, "Source") ?? string.Empty,
                AdditionalArgs = GetStringProperty(wingetConfig, "AdditionalArgs") ?? string.Empty
            };
        }

        if (TryGetPropertyCaseInsensitive(element, "ChocolateyConfig", out JsonElement chocoConfig) && chocoConfig.ValueKind == JsonValueKind.Object)
        {
            sources.ChocolateyConfig = new ChocolateySourceConfig
            {
                Version = GetStringProperty(chocoConfig, "Version") ?? string.Empty,
                AdditionalArgs = GetStringProperty(chocoConfig, "AdditionalArgs") ?? string.Empty
            };
        }

        if (TryGetPropertyCaseInsensitive(element, "DirectDownloadConfig", out JsonElement directConfig) && directConfig.ValueKind == JsonValueKind.Object)
        {
            sources.DirectDownloadConfig = new DirectDownloadSourceConfig
            {
                InstallerType = GetStringProperty(directConfig, "InstallerType") ?? "exe",
                SilentArgs = GetStringProperty(directConfig, "SilentArgs") ?? string.Empty,
                Checksum = GetStringProperty(directConfig, "Checksum") ?? string.Empty,
                FileName = GetStringProperty(directConfig, "FileName") ?? string.Empty
            };
        }

        if (!string.IsNullOrWhiteSpace(sources.DirectUrl) && sources.DirectDownloadConfig == null)
        {
            sources.DirectDownloadConfig = new DirectDownloadSourceConfig
            {
                InstallerType = "exe",
                SilentArgs = installArguments,
                Checksum = FormatEditorChecksum(GetStringProperty(element, "SHA256")),
                FileName = string.Empty
            };
        }

        return sources;
    }

    /// <summary>
    /// Parses ApplicationDetectionModel from JSON.
    /// </summary>
    private static ApplicationDetectionModel ParseDetectionFromJson(JsonElement element)
    {
        return new ApplicationDetectionModel
        {
            Method = GetStringProperty(element, "Method") ?? "Registry",
            Path = GetStringProperty(element, "Path") ?? string.Empty,
            VersionKey = GetStringProperty(element, "VersionKey") ?? string.Empty,
            MinVersion = GetStringProperty(element, "MinVersion") ?? string.Empty
        };
    }

    /// <summary>
    /// Converts an EditableApplicationModel to a JSON string for PowerShell.
    /// </summary>
    private static string ConvertToJsonObject(EditableApplicationModel app)
    {
        string canonicalInstallArguments = GetCanonicalInstallArguments(app);
        string? canonicalSha256 = ExtractSha256Hex(app.Sources.DirectDownloadConfig?.Checksum);

        object obj = new
        {
            app.AppId,
            app.Name,
            app.Category,
            Description = app.Description ?? string.Empty,
            InstallArguments = canonicalInstallArguments,
            Sources = new
            {
                app.Sources.Winget,
                app.Sources.Chocolatey,
                app.Sources.Store,
                app.Sources.DirectUrl,
                ExpectedPublisher = NormalizeOptionalString(app.Sources.ExpectedPublisher),
                SHA256 = canonicalSha256,
                WingetConfig = app.Sources.WingetConfig != null ? new
                {
                    app.Sources.WingetConfig.Version,
                    app.Sources.WingetConfig.Source,
                    app.Sources.WingetConfig.AdditionalArgs
                } : null,
                ChocolateyConfig = app.Sources.ChocolateyConfig != null ? new
                {
                    app.Sources.ChocolateyConfig.Version,
                    app.Sources.ChocolateyConfig.AdditionalArgs
                } : null,
                DirectDownloadConfig = app.Sources.DirectDownloadConfig != null ? new
                {
                    app.Sources.DirectDownloadConfig.InstallerType,
                    app.Sources.DirectDownloadConfig.SilentArgs,
                    app.Sources.DirectDownloadConfig.Checksum,
                    app.Sources.DirectDownloadConfig.FileName
                } : null
            },
            Detection = app.Detection != null ? new
            {
                app.Detection.Method,
                app.Detection.Path,
                app.Detection.VersionKey,
                app.Detection.MinVersion
            } : null,
            app.DefaultPriority,
            app.DefaultRequired,
            EnvironmentRestrictions = app.EnvironmentRestrictions ?? new List<string>(),
            Tags = app.Tags ?? new List<string>(),
            LastVerified = app.LastVerified ?? string.Empty,
            app.Verified,
            Homepage = app.Homepage ?? string.Empty
        };

        return JsonSerializer.Serialize(obj, JsonOptions);
    }

    private static string? NormalizeOptionalString(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string GetCanonicalInstallArguments(EditableApplicationModel app)
    {
        string? directDownloadArgs = app.Sources.DirectDownloadConfig?.SilentArgs;
        if (!string.IsNullOrWhiteSpace(directDownloadArgs))
        {
            return directDownloadArgs;
        }

        return app.InstallArguments ?? string.Empty;
    }

    private static string? ExtractSha256Hex(string? checksum)
    {
        if (string.IsNullOrWhiteSpace(checksum))
        {
            return null;
        }

        string trimmedChecksum = checksum.Trim();
        if (!trimmedChecksum.StartsWith(Sha256ChecksumPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string hex = trimmedChecksum[Sha256ChecksumPrefix.Length..];
        return IsSha256Hex(hex) ? hex.ToUpperInvariant() : null;
    }

    private static string FormatEditorChecksum(string? bareSha256)
    {
        if (string.IsNullOrWhiteSpace(bareSha256))
        {
            return string.Empty;
        }

        string trimmedSha256 = bareSha256.Trim();
        return IsSha256Hex(trimmedSha256)
            ? $"{Sha256ChecksumPrefix}{trimmedSha256.ToLowerInvariant()}"
            : string.Empty;
    }

    private static bool IsSha256Hex(string value)
    {
        if (value.Length != Sha256HexLength)
        {
            return false;
        }

        foreach (char character in value)
        {
            if (!Uri.IsHexDigit(character))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Gets a string property from a JSON element.
    /// </summary>
    private static string? GetStringProperty(JsonElement element, string propertyName)
    {
        return TryGetPropertyCaseInsensitive(element, propertyName, out JsonElement prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString()
            : null;
    }

    /// <summary>
    /// Gets an integer property from a JSON element.
    /// </summary>
    private static int GetIntProperty(JsonElement element, string propertyName, int defaultValue = 0)
    {
        if (TryGetPropertyCaseInsensitive(element, propertyName, out JsonElement prop))
        {
            if (prop.ValueKind == JsonValueKind.Number)
            {
                return prop.GetInt32();
            }
        }
        return defaultValue;
    }

    /// <summary>
    /// Gets a boolean property from a JSON element.
    /// </summary>
    private static bool GetBoolProperty(JsonElement element, string propertyName, bool defaultValue = false)
    {
        if (TryGetPropertyCaseInsensitive(element, propertyName, out JsonElement prop))
        {
            return prop.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => defaultValue
            };
        }
        return defaultValue;
    }

    /// <summary>
    /// Gets a JSON property by name using case-insensitive comparison.
    /// </summary>
    private static bool TryGetPropertyCaseInsensitive(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty prop in element.EnumerateObject())
            {
                if (string.Equals(prop.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    value = prop.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    /// <summary>
    /// Escapes a string for safe use in PowerShell single-quoted strings.
    /// In PowerShell single-quoted strings, special characters like $, `, (, ) are treated literally.
    /// Only single quotes need to be escaped by doubling them.
    /// </summary>
    private static string EscapeForPowerShell(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        // In single-quoted strings, only single quotes need escaping (doubled)
        // All other characters ($, `, (, ), etc.) are treated literally inside single quotes
        return value.Replace("'", "''");
    }

    /// <summary>
    /// Escapes a file path for PowerShell.
    /// </summary>
    private static string EscapePath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return path;
        }

        // Replace single quotes with doubled quotes for PowerShell
        return path.Replace("'", "''");
    }

    /// <summary>
    /// Raises the DatabaseChanged event.
    /// </summary>
    private void OnDatabaseChanged(DatabaseChangedEventArgs e)
    {
        DatabaseChanged?.Invoke(this, e);
    }

    #endregion

    #region IDisposable

    /// <summary>
    /// Releases all resources used by the ApplicationDatabaseService.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases the unmanaged resources and optionally releases managed resources.
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            _operationLock.Dispose();
        }

        _disposed = true;
    }

    #endregion
}

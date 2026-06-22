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

namespace WinForge.GUI.Exceptions;

/// <summary>
/// Base exception for all WinForge GUI exceptions.
/// </summary>
public class WinForgeGuiException : Exception
{
    public WinForgeGuiException() { }
    public WinForgeGuiException(string message) : base(message) { }
    public WinForgeGuiException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Exception thrown when PowerShell bridge operations fail.
/// </summary>
public class PowerShellBridgeException : WinForgeGuiException
{
    /// <summary>
    /// The PowerShell command that failed.
    /// </summary>
    public string? Command { get; }

    /// <summary>
    /// PowerShell error records if available.
    /// </summary>
    public IReadOnlyList<string>? ErrorRecords { get; }

    public PowerShellBridgeException() { }

    public PowerShellBridgeException(string message) : base(message) { }

    public PowerShellBridgeException(string message, Exception innerException)
        : base(message, innerException) { }

    public PowerShellBridgeException(string message, string command) : base(message)
    {
        Command = command;
    }

    public PowerShellBridgeException(string message, string command, IReadOnlyList<string>? errorRecords)
        : base(message)
    {
        Command = command;
        ErrorRecords = errorRecords;
    }

    public PowerShellBridgeException(string message, string command, Exception innerException)
        : base(message, innerException)
    {
        Command = command;
    }
}

/// <summary>
/// Exception thrown when profile operations fail.
/// </summary>
public class ProfileException : WinForgeGuiException
{
    /// <summary>
    /// The profile name involved in the error.
    /// </summary>
    public string? ProfileName { get; }

    public ProfileException() { }

    public ProfileException(string message) : base(message) { }

    public ProfileException(string message, Exception innerException)
        : base(message, innerException) { }

    public ProfileException(string message, string profileName) : base(message)
    {
        ProfileName = profileName;
    }

    public ProfileException(string message, string profileName, Exception innerException)
        : base(message, innerException)
    {
        ProfileName = profileName;
    }
}

/// <summary>
/// Exception thrown when application database operations fail.
/// </summary>
public class ApplicationDatabaseException : WinForgeGuiException
{
    /// <summary>
    /// The application ID involved in the error, if applicable.
    /// </summary>
    public string? ApplicationId { get; }

    /// <summary>
    /// The operation that failed (e.g., "load", "save", "search").
    /// </summary>
    public string? Operation { get; }

    public ApplicationDatabaseException() { }

    public ApplicationDatabaseException(string message) : base(message) { }

    public ApplicationDatabaseException(string message, Exception innerException)
        : base(message, innerException) { }

    public ApplicationDatabaseException(string message, string operation) : base(message)
    {
        Operation = operation;
    }

    public ApplicationDatabaseException(string message, string operation, string? applicationId)
        : base(message)
    {
        Operation = operation;
        ApplicationId = applicationId;
    }

    public ApplicationDatabaseException(string message, string operation, Exception innerException)
        : base(message, innerException)
    {
        Operation = operation;
    }
}

/// <summary>
/// Exception thrown when deployment operations fail.
/// </summary>
public class DeploymentException : WinForgeGuiException
{
    /// <summary>
    /// The profile being deployed, if applicable.
    /// </summary>
    public string? ProfileName { get; }

    /// <summary>
    /// The applications that failed during deployment.
    /// </summary>
    public IReadOnlyList<string>? FailedApplications { get; }

    public DeploymentException() { }

    public DeploymentException(string message) : base(message) { }

    public DeploymentException(string message, Exception innerException)
        : base(message, innerException) { }

    public DeploymentException(string message, string? profileName) : base(message)
    {
        ProfileName = profileName;
    }

    public DeploymentException(string message, string? profileName, IReadOnlyList<string>? failedApps)
        : base(message)
    {
        ProfileName = profileName;
        FailedApplications = failedApps;
    }

    public DeploymentException(string message, string? profileName, Exception innerException)
        : base(message, innerException)
    {
        ProfileName = profileName;
    }
}

/// <summary>
/// Exception thrown when detection operations fail.
/// </summary>
public class DetectionException : WinForgeGuiException
{
    /// <summary>
    /// The application being detected.
    /// </summary>
    public string? ApplicationId { get; }

    /// <summary>
    /// The detection method that failed.
    /// </summary>
    public string? DetectionMethod { get; }

    public DetectionException() { }

    public DetectionException(string message) : base(message) { }

    public DetectionException(string message, Exception innerException)
        : base(message, innerException) { }

    public DetectionException(string message, string applicationId, string? detectionMethod = null)
        : base(message)
    {
        ApplicationId = applicationId;
        DetectionMethod = detectionMethod;
    }

    public DetectionException(string message, string applicationId, Exception innerException)
        : base(message, innerException)
    {
        ApplicationId = applicationId;
    }
}

/// <summary>
/// Exception thrown when configuration operations fail.
/// </summary>
public class ConfigurationException : WinForgeGuiException
{
    /// <summary>
    /// The configuration file or setting involved.
    /// </summary>
    public string? ConfigurationKey { get; }

    public ConfigurationException() { }

    public ConfigurationException(string message) : base(message) { }

    public ConfigurationException(string message, Exception innerException)
        : base(message, innerException) { }

    public ConfigurationException(string message, string configurationKey) : base(message)
    {
        ConfigurationKey = configurationKey;
    }

    public ConfigurationException(string message, string configurationKey, Exception innerException)
        : base(message, innerException)
    {
        ConfigurationKey = configurationKey;
    }
}

/// <summary>
/// Exception thrown when validation operations fail.
/// </summary>
public class ValidationException : WinForgeGuiException
{
    /// <summary>
    /// The validation errors.
    /// </summary>
    public IReadOnlyList<string>? ValidationErrors { get; }

    /// <summary>
    /// The object that failed validation.
    /// </summary>
    public string? EntityType { get; }

    public ValidationException() { }

    public ValidationException(string message) : base(message) { }

    public ValidationException(string message, Exception innerException)
        : base(message, innerException) { }

    public ValidationException(string message, IReadOnlyList<string> validationErrors) : base(message)
    {
        ValidationErrors = validationErrors;
    }

    public ValidationException(string message, string entityType, IReadOnlyList<string>? validationErrors = null)
        : base(message)
    {
        EntityType = entityType;
        ValidationErrors = validationErrors;
    }
}

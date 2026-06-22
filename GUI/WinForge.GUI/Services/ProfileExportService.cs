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

using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using WinForge.GUI.Models;

namespace WinForge.GUI.Services;

/// <summary>
/// Represents an exported profile with metadata.
/// </summary>
public class ExportedProfile
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0";

    [JsonPropertyName("exportDate")]
    public DateTime ExportDate { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("inheritsFrom")]
    public string? InheritsFrom { get; set; }

    [JsonPropertyName("applications")]
    public List<ExportedApplication> Applications { get; set; } = [];
}

/// <summary>
/// Represents an exported application entry.
/// </summary>
public class ExportedApplication
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("wingetId")]
    public string? WingetId { get; set; }

    [JsonPropertyName("chocoName")]
    public string? ChocoName { get; set; }

    [JsonPropertyName("category")]
    public string? Category { get; set; }

    [JsonPropertyName("isRequired")]
    public bool IsRequired { get; set; }

    [JsonPropertyName("priority")]
    public int Priority { get; set; }
}

/// <summary>
/// Service for exporting and importing deployment profiles.
/// </summary>
public interface IProfileExportService
{
    /// <summary>
    /// Exports a profile to JSON format.
    /// </summary>
    Task<string> ExportToJsonAsync(DeploymentProfileModel profile);

    /// <summary>
    /// Exports a profile to a file.
    /// </summary>
    Task ExportToFileAsync(DeploymentProfileModel profile, string filePath);

    /// <summary>
    /// Imports a profile from JSON string.
    /// </summary>
    Task<ExportedProfile?> ImportFromJsonAsync(string json);

    /// <summary>
    /// Imports a profile from a file.
    /// </summary>
    Task<ExportedProfile?> ImportFromFileAsync(string filePath);

    /// <summary>
    /// Validates an imported profile.
    /// </summary>
    (bool IsValid, string? ErrorMessage) ValidateImport(ExportedProfile profile);
}

/// <summary>
/// Implementation of profile export/import service.
/// </summary>
public class ProfileExportService : IProfileExportService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <inheritdoc/>
    public Task<string> ExportToJsonAsync(DeploymentProfileModel profile)
    {
        ExportedProfile exported = ConvertToExported(profile);
        string json = JsonSerializer.Serialize(exported, JsonOptions);
        return Task.FromResult(json);
    }

    /// <inheritdoc/>
    public async Task ExportToFileAsync(DeploymentProfileModel profile, string filePath)
    {
        string json = await ExportToJsonAsync(profile);
        await File.WriteAllTextAsync(filePath, json);
    }

    /// <inheritdoc/>
    public Task<ExportedProfile?> ImportFromJsonAsync(string json)
    {
        try
        {
            ExportedProfile? profile = JsonSerializer.Deserialize<ExportedProfile>(json, JsonOptions);
            return Task.FromResult(profile);
        }
        catch (JsonException)
        {
            return Task.FromResult<ExportedProfile?>(null);
        }
    }

    /// <inheritdoc/>
    public async Task<ExportedProfile?> ImportFromFileAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        string json = await File.ReadAllTextAsync(filePath);
        return await ImportFromJsonAsync(json);
    }

    /// <inheritdoc/>
    public (bool IsValid, string? ErrorMessage) ValidateImport(ExportedProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.Name))
        {
            return (false, "Profile name is required");
        }

        if (profile.Applications.Count == 0)
        {
            return (false, "Profile must contain at least one application");
        }

        foreach (ExportedApplication app in profile.Applications)
        {
            if (string.IsNullOrWhiteSpace(app.Name))
            {
                return (false, "All applications must have a name");
            }

            if (string.IsNullOrWhiteSpace(app.WingetId) && string.IsNullOrWhiteSpace(app.ChocoName))
            {
                return (false, $"Application '{app.Name}' must have either a Winget ID or Chocolatey name");
            }
        }

        return (true, null);
    }

    private static ExportedProfile ConvertToExported(DeploymentProfileModel profile)
    {
        return new ExportedProfile
        {
            Name = profile.Name,
            Description = profile.Description,
            InheritsFrom = profile.InheritedFrom.FirstOrDefault(),
            Applications = profile.Applications
                .Select(a => new ExportedApplication
                {
                    Name = a.Name,
                    WingetId = a.AppId,
                    ChocoName = null,
                    Category = a.Category,
                    IsRequired = a.IsRequired,
                    Priority = a.Priority
                })
                .ToList()
        };
    }
}

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

using System.Text.Json;
using WinForge.GUI.Models;

namespace WinForge.GUI.Services;

public sealed class ScheduledDeploymentService : IScheduledDeploymentService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IPowerShellBridge _powerShellBridge;

    public ScheduledDeploymentService(IPowerShellBridge powerShellBridge)
    {
        _powerShellBridge = powerShellBridge;
    }

    public async Task<ScheduledDeploymentAvailability> GetAvailabilityAsync(CancellationToken cancellationToken = default)
    {
        string result = await _powerShellBridge.ExecuteCommandAsync($@"
{GetModuleImportScript()}
@{{
    TaskSchedulerAvailable = Test-ScheduledTasksAvailable
    IsAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}} | ConvertTo-Json -Compress
", cancellationToken);

        if (string.IsNullOrWhiteSpace(result))
        {
            return new ScheduledDeploymentAvailability(false, false);
        }

        ScheduledDeploymentAvailability? availability = JsonSerializer.Deserialize<ScheduledDeploymentAvailability>(result, JsonOptions);
        return availability ?? new ScheduledDeploymentAvailability(false, false);
    }

    public async Task<IReadOnlyList<ScheduledDeploymentModel>> GetScheduledDeploymentsAsync(CancellationToken cancellationToken = default)
    {
        string result = await _powerShellBridge.ExecuteCommandAsync($@"
{GetModuleImportScript()}
Get-ScheduledDeployment | ForEach-Object {{
    @{{
        Id = $_.Id
        ProfileName = $_.ProfileName
        ScheduledTime = $_.ScheduledTime.ToString('o')
        TriggerType = $_.TriggerType
        Status = $_.Status
        CreatedBy = $_.CreatedBy
        CreatedAt = $_.CreatedAt.ToString('o')
        LastRunTime = if ($_.LastRunTime -and $_.LastRunTime -ne [datetime]::MinValue) {{ $_.LastRunTime.ToString('o') }} else {{ $null }}
        LastRunResult = $_.LastRunResult
    }}
}} | ConvertTo-Json -Compress
", cancellationToken);

        if (string.IsNullOrWhiteSpace(result) || string.Equals(result.Trim(), "null", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        string trimmed = result.TrimStart();
        List<ScheduledDeploymentJson>? deployments = trimmed.StartsWith('[')
            ? JsonSerializer.Deserialize<List<ScheduledDeploymentJson>>(result, JsonOptions)
            : DeserializeSingleDeployment(result);

        return deployments?.Select(MapToModel).ToList() ?? [];
    }

    public async Task<string?> CreateScheduledDeploymentAsync(
        string profileName,
        DateTime scheduledTime,
        ScheduledTriggerType triggerType,
        CancellationToken cancellationToken = default)
    {
        string escapedProfile = EscapePowerShellSingleQuotedString(profileName);
        string triggerTypeText = EscapePowerShellSingleQuotedString(triggerType.ToString());
        string scheduledTimeText = EscapePowerShellSingleQuotedString(scheduledTime.ToString("o"));

        string result = await _powerShellBridge.ExecuteCommandAsync($@"
{GetModuleImportScript()}
$deployment = New-ScheduledDeployment -ProfileName '{escapedProfile}' -ScheduledTime ([datetime]'{scheduledTimeText}') -TriggerType '{triggerTypeText}'
@{{
    Success = $true
    Id = $deployment.Id
}} | ConvertTo-Json -Compress
", cancellationToken);

        if (string.IsNullOrWhiteSpace(result))
        {
            return null;
        }

        using JsonDocument document = JsonDocument.Parse(result);
        return document.RootElement.TryGetProperty("Success", out JsonElement successProp)
            && successProp.GetBoolean()
            && document.RootElement.TryGetProperty("Id", out JsonElement idProp)
            ? idProp.GetString()
            : null;
    }

    public Task RemoveScheduledDeploymentAsync(string id, CancellationToken cancellationToken = default)
    {
        string escapedId = EscapePowerShellSingleQuotedString(id);
        return _powerShellBridge.ExecuteCommandAsync($@"
{GetModuleImportScript()}
Remove-ScheduledDeployment -Id '{escapedId}' -Force
", cancellationToken);
    }

    public Task StartScheduledDeploymentAsync(string id, CancellationToken cancellationToken = default)
    {
        string escapedId = EscapePowerShellSingleQuotedString(id);
        return _powerShellBridge.ExecuteCommandAsync($@"
{GetModuleImportScript()}
Start-ScheduledDeployment -Id '{escapedId}'
", cancellationToken);
    }

    private string GetModuleImportScript()
    {
        string repoRoot = EscapePowerShellSingleQuotedString(_powerShellBridge.RepositoryRoot);
        return $"Import-Module (Join-Path '{repoRoot}' 'Modules\\ScheduledDeployment.psm1') -Force -ErrorAction Stop";
    }

    private static string EscapePowerShellSingleQuotedString(string value)
    {
        return value.Replace("'", "''", StringComparison.Ordinal);
    }

    private static List<ScheduledDeploymentJson>? DeserializeSingleDeployment(string json)
    {
        ScheduledDeploymentJson? deployment = JsonSerializer.Deserialize<ScheduledDeploymentJson>(json, JsonOptions);
        return deployment == null ? null : [deployment];
    }

    private static ScheduledDeploymentModel MapToModel(ScheduledDeploymentJson json)
    {
        ScheduledDeploymentStatus status = json.Status?.ToLowerInvariant() switch
        {
            "pending" => ScheduledDeploymentStatus.Pending,
            "running" => ScheduledDeploymentStatus.Running,
            "completed" => ScheduledDeploymentStatus.Completed,
            "failed" => ScheduledDeploymentStatus.Failed,
            "cancelled" => ScheduledDeploymentStatus.Cancelled,
            _ => ScheduledDeploymentStatus.Unknown
        };

        ScheduledTriggerType triggerType = json.TriggerType?.ToLowerInvariant() switch
        {
            "onetime" => ScheduledTriggerType.OneTime,
            "daily" => ScheduledTriggerType.Daily,
            "weekly" => ScheduledTriggerType.Weekly,
            "atstartup" => ScheduledTriggerType.AtStartup,
            "atlogon" => ScheduledTriggerType.AtLogon,
            _ => ScheduledTriggerType.OneTime
        };

        return new ScheduledDeploymentModel
        {
            Id = json.Id ?? string.Empty,
            ProfileName = json.ProfileName ?? string.Empty,
            ScheduledTime = DateTime.TryParse(json.ScheduledTime, out DateTime scheduledTime) ? scheduledTime : DateTime.Now,
            TriggerType = triggerType,
            Status = status,
            CreatedBy = json.CreatedBy ?? string.Empty,
            CreatedAt = DateTime.TryParse(json.CreatedAt, out DateTime createdAt) ? createdAt : DateTime.Now,
            LastRunTime = DateTime.TryParse(json.LastRunTime, out DateTime lastRunTime) ? lastRunTime : null,
            LastRunResult = json.LastRunResult
        };
    }

    private sealed class ScheduledDeploymentJson
    {
        public string? Id { get; set; }
        public string? ProfileName { get; set; }
        public string? ScheduledTime { get; set; }
        public string? TriggerType { get; set; }
        public string? Status { get; set; }
        public string? CreatedBy { get; set; }
        public string? CreatedAt { get; set; }
        public string? LastRunTime { get; set; }
        public string? LastRunResult { get; set; }
    }
}

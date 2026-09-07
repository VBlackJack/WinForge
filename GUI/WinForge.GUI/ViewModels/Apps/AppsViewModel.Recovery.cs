/*
 * Copyright 2026 Julien Bombled
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 * Unless required by applicable law or agreed to in writing, software distributed
 * under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR
 * CONDITIONS OF ANY KIND, either express or implied. See the License for the
 * specific language governing permissions and limitations under the License.
 */

using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.Input;
using WinForge.GUI.Models;
using WinForge.GUI.Services;

namespace WinForge.GUI.ViewModels;

public partial class AppsViewModel
{
    /// <summary>Reviews a saved execution and retries only unsuccessful or unattempted items.</summary>
    [RelayCommand]
    private async Task ReviewDeploymentAsync()
    {
        if (IsInstalling || IsUninstalling) { return; }
        string? path = await _fileDialogService.ShowOpenAsync(new FileDialogOptions(
            PreflightText("History"), FileDialogFilters.JsonOnly,
            InitialDirectory: _pathService.GetUserDataPath("state")));
        if (path is null) { return; }
        try
        {
            JsonSerializerOptions options = new() { PropertyNameCaseInsensitive = true };
            options.Converters.Add(new JsonStringEnumConverter());
            BatchCheckpoint? checkpoint = JsonSerializer.Deserialize<BatchCheckpoint>(await File.ReadAllTextAsync(path), options);
            if (checkpoint is null || checkpoint.SchemaVersion != BatchCheckpoint.CurrentSchemaVersion ||
                checkpoint.Plan is null || checkpoint.Completed is null || checkpoint.Options is null ||
                !Enum.IsDefined(checkpoint.OperationKind))
            {
                throw new InvalidDataException(PreflightText("InvalidReceipt"));
            }
            StringBuilder summary = new StringBuilder(checkpoint.StartedAt.ToLocalTime().ToString("g"));
            summary.AppendLine().Append(checkpoint.BatchId);
            foreach (string id in checkpoint.Plan)
            {
                BatchCompletedItem? last = checkpoint.Completed.LastOrDefault(item => string.Equals(item.AppId, id, StringComparison.OrdinalIgnoreCase));
                summary.AppendLine().Append(id).Append(": ").Append(PreflightText(last?.Outcome.ToString() ?? "Pending"));
                if (checkpoint.Observations?.TryGetValue(id, out BatchObservation? observation) == true)
                {
                    summary.AppendLine().Append(observation.Method).Append(" / ")
                        .Append(observation.InstalledVersion ?? PreflightText("Unknown"));
                    if (!string.IsNullOrWhiteSpace(observation.Message)) { summary.AppendLine().Append(observation.Message); }
                }
            }
            await _dialogService.ShowContentAsync(PreflightText("History"), new Views.DeploymentReportView { DataContext = summary.ToString() });
            IReadOnlyList<string> retry = checkpoint.GetRetryAppIds();
            if (retry.Count == 0 || !await _dialogService.ShowConfirmAsync(PreflightText("Retry"),
                PreflightText("RetryExplanation") + Environment.NewLine + string.Join(Environment.NewLine, retry))) { return; }
            IsInstalling = true;
            // Keep the original receipt. Recovery creates its own execution record.
            await ResumeBatchAsync(checkpoint with { Plan = retry, Completed = Array.Empty<BatchCompletedItem>() });
        }
        catch (Exception ex)
        {
            _logger.LogError("Deployment recovery failed; original receipt retained.", ex);
            ErrorMessage = ex.Message;
        }
        finally { IsInstalling = false; }
    }
}

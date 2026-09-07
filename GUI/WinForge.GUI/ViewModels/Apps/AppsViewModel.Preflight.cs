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
using CommunityToolkit.Mvvm.Input;
using WinForge.GUI.Models;
using WinForge.GUI.Resources;

namespace WinForge.GUI.ViewModels;

public partial class AppsViewModel
{
    /// <summary>Creates a fresh, read-only preview from current detection and exact catalog definitions.</summary>
    [RelayCommand]
    private async Task PreviewSelectedAsync()
    {
        List<ApplicationModel> selected = _allApplications.Where(app => app.IsSelected).ToList();
        if (selected.Count == 0 || IsInstalling || IsUninstalling)
        {
            await _dialogService.ShowInfoAsync(PreflightText("Title"), PreflightText("SelectFirst"));
            return;
        }

        try
        {
            string encodedIds = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(selected.Select(app => app.AppId))));
            string root = _pathService.RepositoryRoot.Replace("'", "''", StringComparison.Ordinal);
            string destination = _pathService.GetUserDataPath("plans", Guid.NewGuid().ToString("D") + ".json");
            string quotedDestination = destination.Replace("'", "''", StringComparison.Ordinal);
            string script = $$"""
                $ErrorActionPreference = 'Stop'
                try {
                Import-Module '{{root}}/Modules/ApplicationDatabase.psm1' -ErrorAction Stop
                Import-Module '{{root}}/Modules/DeploymentPlanning.psm1' -ErrorAction Stop
                $ids = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('{{encodedIds}}')) | ConvertFrom-Json
                $applications = @($ids | ForEach-Object { Get-ApplicationById -AppId $_ })
                if ($applications.Count -ne @($ids).Count) { throw 'Selected applications could not be resolved.' }
                $plan = New-DeploymentPlan -Applications $applications
                Save-DeploymentPlan -Plan $plan -Path '{{quotedDestination}}'
                $plan | ConvertTo-Json -Depth 40 -Compress
                } catch { @{ PreviewError = $_.Exception.Message } | ConvertTo-Json -Compress }
                """;
            string output = await _powerShellBridge.ExecuteCommandAsync(script);
            string? json = output.Split('\n').LastOrDefault(line => line.TrimStart().StartsWith('{'));
            if (json is null) { throw new InvalidDataException(PreflightText("PreparationFailed")); }
            using JsonDocument document = JsonDocument.Parse(json);
            if (document.RootElement.TryGetProperty("PreviewError", out JsonElement previewError))
            {
                throw new InvalidOperationException(previewError.GetString());
            }
            StringBuilder preview = new StringBuilder(PreflightText("Explanation"));
            foreach (JsonElement item in document.RootElement.GetProperty("Items").EnumerateArray())
            {
                preview.AppendLine().AppendLine();
                preview.Append(item.GetProperty("Name").GetString()).Append(": ")
                    .Append(PreflightText(item.GetProperty("Action").GetString() ?? "Unknown"));
                preview.AppendLine().Append(PreflightText("Version")).Append(": ")
                    .Append(item.GetProperty("InstalledVersion").GetString() ?? PreflightText("Unknown"));
                preview.AppendLine().Append(PreflightText("Sources")).Append(": ");
                preview.AppendJoin(" > ", item.GetProperty("Sources").EnumerateArray().Select(source =>
                    source.GetProperty("Source").GetString() + ": " + source.GetProperty("Identifier").GetString()));
                foreach (JsonElement source in item.GetProperty("Sources").EnumerateArray())
                {
                    preview.AppendLine().Append(source.GetProperty("Source").GetString()).Append(" / ")
                        .Append(PreflightText(source.GetProperty("Availability").GetString() ?? "Unknown"));
                    foreach (string evidenceKey in new[] { "PackageFoundAt", "DownloadVerifiedAt", "InstallationTestedAt" })
                    {
                        preview.AppendLine().Append(PreflightText(evidenceKey)).Append(": ")
                            .Append(source.GetProperty(evidenceKey).GetString() ?? PreflightText("NotMeasured"));
                    }
                }
                preview.AppendLine().Append(PreflightText("Elevation")).Append(": ")
                    .Append(PreflightText(item.GetProperty("Elevation").GetString() ?? "Unknown"));
                preview.AppendLine().Append(PreflightText("Reboot")).Append(": ")
                    .Append(PreflightText(item.GetProperty("Reboot").GetString() ?? "Unknown"));
                preview.AppendLine().Append(PreflightText("Rollback")).Append(": ")
                    .Append(PreflightText(item.GetProperty("Rollback").GetString() ?? "Unknown"));
            }
            preview.AppendLine().AppendLine().Append(PreflightText("SavedAt")).Append(": ").Append(destination);
            await _dialogService.ShowContentAsync(PreflightText("Title"), new Views.DeploymentReportView { DataContext = preview.ToString() });
        }
        catch (Exception ex)
        {
            _logger.LogError("Deployment preview failed.", ex);
            ErrorMessage = PreflightText("PreparationFailed") + " " + ex.Message;
        }
    }

    private static string PreflightText(string key) => LocalizationProvider.Instance["Preflight_" + key];
}

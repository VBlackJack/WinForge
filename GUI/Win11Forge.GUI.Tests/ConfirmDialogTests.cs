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

using CommunityToolkit.Mvvm.Messaging;
using System.IO;
using Win11Forge.GUI.Models;
using Win11Forge.GUI.Services;
using Win11Forge.GUI.Services.Coordinators;
using Win11Forge.GUI.ViewModels;
using Loc = Win11Forge.GUI.Resources.Resources;

namespace Win11Forge.GUI.Tests;

/// <summary>
/// Tests for confirmation dialog action labels.
/// </summary>
public class ConfirmDialogTests
{
    [Fact]
    public async Task UninstallSelected_WithOneApp_UsesSingularCopyAndActionButtons()
    {
        var dialogService = new TestDialogService();
        dialogService.QueueConfirmResult(false);
        var viewModel = CreateAppsViewModel(dialogService: dialogService);
        await viewModel.InitializeAsync();
        var app = viewModel.FilteredApplications.Cast<ApplicationModel>().First();
        app.Status = ApplicationStatus.Installed;
        app.IsSelected = true;
        viewModel.UpdateSelectedCount();

        await viewModel.UninstallSelectedCommand.ExecuteAsync(null);

        var request = Assert.Single(dialogService.ConfirmRequests);
        Assert.Equal(Loc.Confirm_Uninstall_Title_Single, request.Title);
        Assert.Equal(Loc.Confirm_Uninstall_Message_Single, request.Message);
        Assert.Equal(Loc.Confirm_Uninstall_Btn, request.ConfirmText);
        Assert.Equal(Loc.Common_Cancel, request.CancelText);
    }

    [Fact]
    public async Task UninstallSelected_WithMultipleApps_UsesPluralCopyAndActionButtons()
    {
        var dialogService = new TestDialogService();
        dialogService.QueueConfirmResult(false);
        var viewModel = CreateAppsViewModel(dialogService: dialogService);
        await viewModel.InitializeAsync();
        var apps = viewModel.FilteredApplications.Cast<ApplicationModel>().Take(2).ToList();
        foreach (var app in apps)
        {
            app.Status = ApplicationStatus.Installed;
            app.IsSelected = true;
        }

        viewModel.UpdateSelectedCount();

        await viewModel.UninstallSelectedCommand.ExecuteAsync(null);

        var request = Assert.Single(dialogService.ConfirmRequests);
        Assert.Equal(string.Format(Loc.Confirm_Uninstall_Title_Multiple, 2), request.Title);
        Assert.Equal(string.Format(Loc.Confirm_Uninstall_Message_Multiple, 2), request.Message);
        Assert.Equal(Loc.Confirm_Uninstall_Btn, request.ConfirmText);
        Assert.Equal(Loc.Common_Cancel, request.CancelText);
    }

    [Fact]
    public async Task AppCatalogDelete_UsesDeleteAndCancelButtons()
    {
        var dialogService = new TestDialogService();
        dialogService.QueueConfirmResult(false);
        var viewModel = CreateApplicationsViewModel(dialogService: dialogService);
        await viewModel.LoadApplicationsCommand.ExecuteAsync(null);
        viewModel.SelectedApplication = viewModel.Applications[0];

        await viewModel.DeleteCommand.ExecuteAsync(null);

        var request = Assert.Single(dialogService.ConfirmRequests);
        Assert.Equal(Loc.Confirm_Delete_Btn, request.ConfirmText);
        Assert.Equal(Loc.Common_Cancel, request.CancelText);
    }

    [Fact]
    public async Task AppCatalogImport_UsesReplaceSkipCancelButtons()
    {
        var dialogService = new TestDialogService();
        var fileDialogService = new TestFileDialogService();
        fileDialogService.QueueOpenResult(@"C:\Imports\applications.json");
        dialogService.QueueYesNoCancelResult(null);
        var viewModel = CreateApplicationsViewModel(
            dialogService: dialogService,
            fileDialogService: fileDialogService);

        await viewModel.ImportCommand.ExecuteAsync(null);

        var request = Assert.Single(dialogService.YesNoCancelRequests);
        Assert.Equal(Loc.AppDb_Import_Replace, request.YesText);
        Assert.Equal(Loc.AppDb_Import_Skip, request.NoText);
        Assert.Equal(Loc.Common_Cancel, request.CancelText);
        Assert.DoesNotContain("Yes", request.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("No", request.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SettingsAndLogsConfirmations_UseActionAndCancelButtons()
    {
        var historyDialog = new TestDialogService();
        historyDialog.QueueConfirmResult(false);
        var settingsViewModel = new SettingsViewModel(
            new MockAppSettingsService(),
            new MockDeploymentHistoryService(),
            new MockPowerShellBridge(),
            dialogService: historyDialog);

        await settingsViewModel.ClearHistoryCommand.ExecuteAsync(null);

        var historyRequest = Assert.Single(historyDialog.ConfirmRequests);
        Assert.Equal(Loc.Confirm_ClearHistory_Btn, historyRequest.ConfirmText);
        Assert.Equal(Loc.Common_Cancel, historyRequest.CancelText);

        var logsDialog = new TestDialogService();
        logsDialog.QueueConfirmResult(false);
        var logsViewModel = new LogsViewModel(dialogService: logsDialog);

        await logsViewModel.ClearOldLogsCommand.ExecuteAsync(null);

        var logsRequest = Assert.Single(logsDialog.ConfirmRequests);
        Assert.Equal(Loc.Confirm_ClearOldLogs_Btn, logsRequest.ConfirmText);
        Assert.Equal(Loc.Common_Cancel, logsRequest.CancelText);

        var resetDialog = new TestDialogService();
        resetDialog.QueueConfirmResult(false);
        var resetSettingsViewModel = new SettingsViewModel(
            new MockAppSettingsService(),
            new MockDeploymentHistoryService(),
            new MockPowerShellBridge(),
            dialogService: resetDialog);

        await resetSettingsViewModel.ResetToDefaultsCommand.ExecuteAsync(null);

        var resetRequest = Assert.Single(resetDialog.ConfirmRequests);
        Assert.Equal(Loc.Confirm_Reset_Btn, resetRequest.ConfirmText);
        Assert.Equal(Loc.Common_Cancel, resetRequest.CancelText);
    }

    [Fact]
    public void ConfirmDialogCallSites_DoNotUseGenericYesNoButtons()
    {
        var failures = new List<string>();
        var viewModelsPath = Path.Combine(FindRepositoryRoot(), "GUI", "Win11Forge.GUI", "ViewModels");
        foreach (var file in Directory.EnumerateFiles(viewModelsPath, "*.cs", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            foreach (var marker in new[] { "ShowConfirmAsync(", "ShowYesNoCancelAsync(" })
            {
                var index = text.IndexOf(marker, StringComparison.Ordinal);
                while (index >= 0)
                {
                    var end = text.IndexOf(");", index, StringComparison.Ordinal);
                    if (end < 0)
                    {
                        break;
                    }

                    var call = text[index..end];
                    if (call.Contains("Common_Yes", StringComparison.Ordinal) ||
                        call.Contains("Common_No", StringComparison.Ordinal))
                    {
                        failures.Add($"{Path.GetFileName(file)}: {marker} uses Common_Yes/Common_No");
                    }

                    index = text.IndexOf(marker, end, StringComparison.Ordinal);
                }
            }
        }

        Assert.Empty(failures);
    }

    private static AppsViewModel CreateAppsViewModel(
        IDialogService? dialogService = null,
        IAppUninstallCoordinator? uninstallCoordinator = null)
    {
        var viewModel = new AppsViewModel(
            new MockPowerShellBridge(),
            new MockAppSettingsService(),
            new MockDeploymentStateService(),
            new TestAppScanCoordinator(),
            new TestAppInstallationCoordinator(),
            new TestAppUpdateCoordinator(),
            uninstallCoordinator ?? new TestAppUninstallCoordinator(),
            new TestPauseGate(),
            dialogService ?? new TestDialogService(),
            null,
            null);

        WeakReferenceMessenger.Default.UnregisterAll(viewModel);
        return viewModel;
    }

    private static ApplicationsViewModel CreateApplicationsViewModel(
        IDialogService? dialogService = null,
        IFileDialogService? fileDialogService = null)
    {
        return new ApplicationsViewModel(
            new MockApplicationDatabaseService(),
            new MockUndoService(),
            new MockPackageVerificationService(),
            new TestApplicationEditorDialogService(),
            dialogService ?? new TestDialogService(),
            fileDialogService ?? new TestFileDialogService());
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "TODO.md")) &&
                Directory.Exists(Path.Combine(directory.FullName, "GUI", "Win11Forge.GUI")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find Win11Forge repository root.");
    }
}

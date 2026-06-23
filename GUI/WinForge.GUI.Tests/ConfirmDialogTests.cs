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
using CommunityToolkit.Mvvm.Messaging;
using WinForge.GUI.Models;
using WinForge.GUI.Services;
using WinForge.GUI.Services.Coordinators;
using WinForge.GUI.ViewModels;
using Loc = WinForge.GUI.Resources.Resources;

namespace WinForge.GUI.Tests;

/// <summary>
/// Tests for confirmation dialog action labels.
/// </summary>
public class ConfirmDialogTests
{
    [Fact]
    public async Task UninstallSelected_WithOneApp_UsesSingularCopyAndActionButtons()
    {
        TestDialogService dialogService = new TestDialogService();
        dialogService.QueueConfirmResult(false);
        AppsViewModel viewModel = CreateAppsViewModel(dialogService: dialogService);
        await viewModel.InitializeAsync();
        ApplicationModel app = viewModel.FilteredApplications.Cast<ApplicationModel>().First();
        app.Status = ApplicationStatus.Installed;
        app.IsSelected = true;
        viewModel.UpdateSelectedCount();

        await viewModel.UninstallSelectedCommand.ExecuteAsync(null);

        (string Title, string Message, string? ConfirmText, string? CancelText) request = Assert.Single(dialogService.ConfirmRequests);
        Assert.Equal(Loc.Confirm_Uninstall_Title_Single, request.Title);
        Assert.Equal(Loc.Confirm_Uninstall_Message_Single, request.Message);
        Assert.Equal(Loc.Confirm_Uninstall_Btn, request.ConfirmText);
        Assert.Equal(Loc.Common_Cancel, request.CancelText);
    }

    [Fact]
    public async Task UninstallSelected_WithMultipleApps_UsesPluralCopyAndActionButtons()
    {
        TestDialogService dialogService = new TestDialogService();
        dialogService.QueueConfirmResult(false);
        AppsViewModel viewModel = CreateAppsViewModel(dialogService: dialogService);
        await viewModel.InitializeAsync();
        List<ApplicationModel> apps = viewModel.FilteredApplications.Cast<ApplicationModel>().Take(2).ToList();
        foreach (ApplicationModel? app in apps)
        {
            app.Status = ApplicationStatus.Installed;
            app.IsSelected = true;
        }

        viewModel.UpdateSelectedCount();

        await viewModel.UninstallSelectedCommand.ExecuteAsync(null);

        (string Title, string Message, string? ConfirmText, string? CancelText) request = Assert.Single(dialogService.ConfirmRequests);
        Assert.Equal(string.Format(Loc.Confirm_Uninstall_Title_Multiple, 2), request.Title);
        Assert.Equal(string.Format(Loc.Confirm_Uninstall_Message_Multiple, 2), request.Message);
        Assert.Equal(Loc.Confirm_Uninstall_Btn, request.ConfirmText);
        Assert.Equal(Loc.Common_Cancel, request.CancelText);
    }

    [Fact]
    public async Task UninstallSelected_WithRequiredPrerequisite_UsesPrerequisiteWarningCopy()
    {
        TestDialogService dialogService = new TestDialogService();
        dialogService.QueueConfirmResult(false);
        AppsViewModel viewModel = CreateAppsViewModel(dialogService: dialogService);
        await viewModel.InitializeAsync();
        ApplicationModel app = viewModel.FilteredApplications.Cast<ApplicationModel>().First();
        app.Name = "PowerShell 7";
        app.Status = ApplicationStatus.Installed;
        app.IsRequired = true;
        app.IsPrerequisite = true;
        app.IsSelected = true;
        viewModel.UpdateSelectedCount();

        await viewModel.UninstallSelectedCommand.ExecuteAsync(null);

        (string Title, string Message, string? ConfirmText, string? CancelText) request = Assert.Single(dialogService.ConfirmRequests);
        Assert.Equal(Loc.Confirm_Uninstall_Prerequisite_Title_Single, request.Title);
        Assert.Equal(string.Format(Loc.Confirm_Uninstall_Prerequisite_Message_Single, "PowerShell 7"), request.Message);
        Assert.Equal(Loc.Confirm_Uninstall_Btn, request.ConfirmText);
        Assert.Equal(Loc.Common_Cancel, request.CancelText);
    }

    [Fact]
    public async Task AppCatalogDelete_UsesDeleteAndCancelButtons()
    {
        TestDialogService dialogService = new TestDialogService();
        dialogService.QueueConfirmResult(false);
        AppCatalogViewModel viewModel = CreateAppCatalogViewModel(dialogService: dialogService);
        await viewModel.LoadApplicationsCommand.ExecuteAsync(null);
        viewModel.SelectedApplication = viewModel.Applications[0];

        await viewModel.DeleteCommand.ExecuteAsync(null);

        (string Title, string Message, string? ConfirmText, string? CancelText) request = Assert.Single(dialogService.ConfirmRequests);
        Assert.Equal(Loc.Confirm_Delete_Btn, request.ConfirmText);
        Assert.Equal(Loc.Common_Cancel, request.CancelText);
    }

    [Fact]
    public async Task AppCatalogImport_UsesReplaceSkipCancelButtons()
    {
        TestDialogService dialogService = new TestDialogService();
        TestFileDialogService fileDialogService = new TestFileDialogService();
        fileDialogService.QueueOpenResult(@"C:\Imports\applications.json");
        dialogService.QueueYesNoCancelResult(null);
        AppCatalogViewModel viewModel = CreateAppCatalogViewModel(
            dialogService: dialogService,
            fileDialogService: fileDialogService);

        await viewModel.ImportCommand.ExecuteAsync(null);

        (string Title, string Message, string? YesText, string? NoText, string? CancelText) request = Assert.Single(dialogService.YesNoCancelRequests);
        Assert.Equal(Loc.AppCatalog_Import_Replace, request.YesText);
        Assert.Equal(Loc.AppCatalog_Import_Skip, request.NoText);
        Assert.Equal(Loc.Common_Cancel, request.CancelText);
        Assert.DoesNotContain("Yes", request.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("No", request.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SettingsAndLogsConfirmations_UseActionAndCancelButtons()
    {
        TestDialogService historyDialog = new TestDialogService();
        historyDialog.QueueConfirmResult(false);
        SettingsViewModel settingsViewModel = new SettingsViewModel(
            new MockAppSettingsService(),
            new MockDeploymentHistoryService(),
            new MockPowerShellBridge(),
            dialogService: historyDialog);

        await settingsViewModel.ClearHistoryCommand.ExecuteAsync(null);

        (string Title, string Message, string? ConfirmText, string? CancelText) historyRequest = Assert.Single(historyDialog.ConfirmRequests);
        Assert.Equal(Loc.Confirm_ClearHistory_Btn, historyRequest.ConfirmText);
        Assert.Equal(Loc.Common_Cancel, historyRequest.CancelText);

        TestDialogService logsDialog = new TestDialogService();
        logsDialog.QueueConfirmResult(false);
        LogsViewModel logsViewModel = new LogsViewModel(dialogService: logsDialog);

        await logsViewModel.ClearOldLogsCommand.ExecuteAsync(null);

        (string Title, string Message, string? ConfirmText, string? CancelText) logsRequest = Assert.Single(logsDialog.ConfirmRequests);
        Assert.Equal(Loc.Confirm_ClearOldLogs_Btn, logsRequest.ConfirmText);
        Assert.Equal(Loc.Common_Cancel, logsRequest.CancelText);

        TestDialogService resetDialog = new TestDialogService();
        resetDialog.QueueConfirmResult(false);
        SettingsViewModel resetSettingsViewModel = new SettingsViewModel(
            new MockAppSettingsService(),
            new MockDeploymentHistoryService(),
            new MockPowerShellBridge(),
            dialogService: resetDialog);

        await resetSettingsViewModel.ResetToDefaultsCommand.ExecuteAsync(null);

        (string Title, string Message, string? ConfirmText, string? CancelText) resetRequest = Assert.Single(resetDialog.ConfirmRequests);
        Assert.Equal(Loc.Confirm_Reset_Btn, resetRequest.ConfirmText);
        Assert.Equal(Loc.Common_Cancel, resetRequest.CancelText);
    }

    [Fact]
    public void ConfirmDialogCallSites_DoNotUseGenericYesNoButtons()
    {
        List<string> failures = new List<string>();
        string viewModelsPath = Path.Combine(FindRepositoryRoot(), "GUI", "WinForge.GUI", "ViewModels");
        foreach (string file in Directory.EnumerateFiles(viewModelsPath, "*.cs", SearchOption.AllDirectories))
        {
            string text = File.ReadAllText(file);
            foreach (string? marker in new[] { "ShowConfirmAsync(", "ShowYesNoCancelAsync(" })
            {
                int index = text.IndexOf(marker, StringComparison.Ordinal);
                while (index >= 0)
                {
                    int end = text.IndexOf(");", index, StringComparison.Ordinal);
                    if (end < 0)
                    {
                        break;
                    }

                    string call = text[index..end];
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
        AppsViewModel viewModel = new AppsViewModel(
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

    private static AppCatalogViewModel CreateAppCatalogViewModel(
        IDialogService? dialogService = null,
        IFileDialogService? fileDialogService = null)
    {
        return new AppCatalogViewModel(
            new MockApplicationDatabaseService(),
            new MockUndoService(),
            new MockPackageVerificationService(),
            new TestApplicationEditorDialogService(),
            dialogService ?? new TestDialogService(),
            fileDialogService ?? new TestFileDialogService());
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "GUI", "WinForge.slnx")) &&
                Directory.Exists(Path.Combine(directory.FullName, "GUI", "WinForge.GUI")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find WinForge repository root.");
    }
}

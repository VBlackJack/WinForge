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

using System.Globalization;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Win11Forge.GUI.Controls;
using Win11Forge.GUI.Messages;
using Win11Forge.GUI.Models;
using Win11Forge.GUI.Services;
using Win11Forge.GUI.Services.Resume;
using Loc = Win11Forge.GUI.Resources.Resources;

namespace Win11Forge.GUI.ViewModels;

/// <summary>
/// ViewModel for MainWindow shell state and commands.
/// </summary>
public partial class MainWindowViewModel : ObservableObject, IDisposable
{
    private readonly IUndoService _undoService;
    private readonly IBatchResumeService _batchResumeService;
    private readonly IPowerShellBridge _powerShellBridge;
    private readonly IDialogService _dialogService;
    private readonly IToastService _toastService;
    private readonly EventHandler _undoStateChangedHandler;
    private bool _disposed;
    private bool _batchResumePromptHandled;

    [ObservableProperty]
    private string _windowTitle = Loc.App_Name;

    /// <summary>
    /// Initializes a new instance of the MainWindowViewModel class.
    /// </summary>
    public MainWindowViewModel(
        IUndoService undoService,
        DashboardViewModel dashboardViewModel,
        DeploymentViewModel deploymentViewModel,
        AppsViewModel appsViewModel,
        SettingsViewModel settingsViewModel,
        PrerequisitesViewModel prerequisitesViewModel,
        AppCatalogViewModel appCatalogViewModel,
        IBatchResumeService batchResumeService,
        IPowerShellBridge powerShellBridge,
        IDialogService dialogService,
        IToastService toastService)
    {
        _undoService = undoService ?? throw new ArgumentNullException(nameof(undoService));
        DashboardViewModel = dashboardViewModel ?? throw new ArgumentNullException(nameof(dashboardViewModel));
        DeploymentViewModel = deploymentViewModel ?? throw new ArgumentNullException(nameof(deploymentViewModel));
        AppsViewModel = appsViewModel ?? throw new ArgumentNullException(nameof(appsViewModel));
        SettingsViewModel = settingsViewModel ?? throw new ArgumentNullException(nameof(settingsViewModel));
        PrerequisitesViewModel = prerequisitesViewModel ?? throw new ArgumentNullException(nameof(prerequisitesViewModel));
        AppCatalogViewModel = appCatalogViewModel ?? throw new ArgumentNullException(nameof(appCatalogViewModel));
        _batchResumeService = batchResumeService ?? throw new ArgumentNullException(nameof(batchResumeService));
        _powerShellBridge = powerShellBridge ?? throw new ArgumentNullException(nameof(powerShellBridge));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _toastService = toastService ?? throw new ArgumentNullException(nameof(toastService));

        ShowKeyboardShortcutsCommand = new AsyncRelayCommand(ShowKeyboardShortcutsAsync);
        UndoCommand = new AsyncRelayCommand(UndoAsync, () => CanUndo);
        RedoCommand = new AsyncRelayCommand(RedoAsync, () => CanRedo);

        _undoStateChangedHandler = (_, _) => DispatchUndoStateChanged();
        _undoService.StateChanged += _undoStateChangedHandler;
    }

    /// <summary>
    /// Gets the Dashboard view model.
    /// </summary>
    public DashboardViewModel DashboardViewModel { get; }

    /// <summary>
    /// Gets the Deployment view model.
    /// </summary>
    public DeploymentViewModel DeploymentViewModel { get; }

    /// <summary>
    /// Gets the Apps view model.
    /// </summary>
    public AppsViewModel AppsViewModel { get; }

    /// <summary>
    /// Gets the Settings view model.
    /// </summary>
    public SettingsViewModel SettingsViewModel { get; }

    /// <summary>
    /// Gets the Prerequisites view model.
    /// </summary>
    public PrerequisitesViewModel PrerequisitesViewModel { get; }

    /// <summary>
    /// Gets the application catalog view model.
    /// </summary>
    public AppCatalogViewModel AppCatalogViewModel { get; }

    /// <summary>
    /// Gets the command to show the keyboard shortcuts dialog.
    /// </summary>
    public IAsyncRelayCommand ShowKeyboardShortcutsCommand { get; }

    /// <summary>
    /// Gets the command to undo the last action.
    /// </summary>
    public IAsyncRelayCommand UndoCommand { get; }

    /// <summary>
    /// Gets the command to redo the last undone action.
    /// </summary>
    public IAsyncRelayCommand RedoCommand { get; }

    /// <summary>
    /// Gets whether undo is available.
    /// </summary>
    public bool CanUndo => _undoService.CanUndo;

    /// <summary>
    /// Gets whether redo is available.
    /// </summary>
    public bool CanRedo => _undoService.CanRedo;

    /// <summary>
    /// Updates the shell title with the current Win11Forge version.
    /// </summary>
    public async Task UpdateWindowTitleAsync()
    {
        try
        {
            string version = await _powerShellBridge.GetWin11ForgeVersionAsync();
            WindowTitle = string.Format(Loc.App_Title, version);
        }
        catch (Exception ex)
        {
            // Fallback to static title if version retrieval fails.
            System.Diagnostics.Debug.WriteLine($"Failed to retrieve version for title: {ex.Message}");
            WindowTitle = string.Format(Loc.App_Title, "?");
        }
    }

    /// <summary>
    /// If a checkpoint from a previously interrupted batch is found, prompts the user
    /// to resume, discard, or postpone the decision. The resumed batch (if any) runs
    /// in the background so MainWindow_Loaded is not blocked while the apps install.
    /// </summary>
    /// <remarks>
    /// Only the most recent non-stale pending checkpoint is offered; older pending
    /// checkpoints are logged as ignored and will be cleaned by PruneStaleAsync at a
    /// future startup once they exceed the TTL.
    /// </remarks>
    public async Task PromptBatchResumeIfPendingAsync(IDialogService dialogService)
    {
        ArgumentNullException.ThrowIfNull(dialogService);

        // Re-entrance guard. Top-level Window.Loaded normally fires once per
        // instance, but defensively skip subsequent invocations so a Visibility
        // cycle or visual-tree re-attach cannot re-prompt the user - particularly
        // dangerous after a Resume click, where the new in-flight batch could be
        // surfaced as a fresh InProgress checkpoint.
        if (_batchResumePromptHandled)
        {
            return;
        }
        _batchResumePromptHandled = true;

        IReadOnlyList<BatchCheckpoint> pending;
        try
        {
            pending = await _batchResumeService.ListPendingAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] ListPendingAsync failed: {ex.Message}");
            return;
        }

        if (pending.Count == 0)
        {
            return;
        }

        BatchCheckpoint[] ordered = pending.OrderByDescending(c => c.LastCheckpointAt).ToArray();
        BatchCheckpoint latest = ordered[0];
        for (int i = 1; i < ordered.Length; i++)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[MainWindow] Ignoring older pending checkpoint {ordered[i].BatchId} " +
                $"(LastCheckpointAt={ordered[i].LastCheckpointAt:o}); will be re-offered or pruned later.");
        }

        int remaining = latest.Plan.Count - latest.Completed.Count;
        string messageTemplate = latest.OperationKind switch
        {
            BatchOperationKind.Install => Loc.Resume_Message_Install,
            BatchOperationKind.Update => Loc.Resume_Message_Update,
            BatchOperationKind.Uninstall => Loc.Resume_Message_Uninstall,
            _ => Loc.Resume_Message_Install
        };
        string message = string.Format(
            CultureInfo.CurrentCulture,
            messageTemplate,
            latest.Plan.Count,
            latest.Completed.Count,
            remaining);

        bool? choice;
        try
        {
            choice = await dialogService.ShowYesNoCancelAsync(
                title: Loc.Resume_Title,
                message: message,
                yesText: Loc.Resume_Action_Resume,
                noText: Loc.Resume_Action_Discard,
                cancelText: Loc.Resume_Action_KeepForLater);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] Resume dialog failed: {ex.Message}");
            return;
        }

        switch (choice)
        {
            case true:
                // Resume: navigate to the Apps view and kick off the batch in the
                // background. Delete the old checkpoint immediately so the new batch
                // owns its own state file; if the new batch crashes too, only the new
                // checkpoint will be detected at the next startup.
                WeakReferenceMessenger.Default.Send(new NavigateMessage(ViewIndex.Apps));
                try
                {
                    await _batchResumeService.DeleteCheckpointAsync(latest.BatchId);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[MainWindow] Failed to delete old checkpoint: {ex.Message}");
                }

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await AppsViewModel.ResumeBatchAsync(latest);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[MainWindow] ResumeBatchAsync failed: {ex.Message}");
                    }
                });
                break;

            case false:
                // Discard: explicit user choice, remove the checkpoint.
                try
                {
                    await _batchResumeService.DeleteCheckpointAsync(latest.BatchId);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[MainWindow] Discard failed: {ex.Message}");
                }
                break;

            default:
                // Keep for later (Cancel / Esc): leave the file in place so it is
                // re-offered at the next launch, until the TTL expires and the file
                // is pruned automatically.
                break;
        }
    }

    /// <summary>
    /// Releases all resources used by the MainWindowViewModel.
    /// </summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases managed and unmanaged resources.
    /// </summary>
    /// <param name="disposing">True if disposing managed resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            _undoService.StateChanged -= _undoStateChangedHandler;
        }

        _disposed = true;
    }

    private async Task ShowKeyboardShortcutsAsync()
    {
        try
        {
            KeyboardShortcutsPanel shortcutsPanel = new KeyboardShortcutsPanel();
            await _dialogService.ShowContentAsync(
                Loc.Help_KeyboardShortcuts ?? "Keyboard Shortcuts",
                shortcutsPanel,
                Loc.Common_OK ?? "OK");
        }
        catch (Exception ex)
        {
            // Dialog display is non-critical, but log for diagnostics.
            System.Diagnostics.Debug.WriteLine($"Failed to show keyboard shortcuts dialog: {ex.Message}");
        }
    }

    private async Task UndoAsync()
    {
        string? description = _undoService.NextUndoDescription;
        bool success = await _undoService.UndoAsync();

        if (success)
        {
            string message = string.Format(Loc.Undo_ActionUndone, description ?? "");
            _toastService.ShowInfo(message);
        }
    }

    private async Task RedoAsync()
    {
        string? description = _undoService.NextRedoDescription;
        bool success = await _undoService.RedoAsync();

        if (success)
        {
            string message = string.Format(Loc.Undo_ActionRedone, description ?? "");
            _toastService.ShowInfo(message);
        }
    }

    private void DispatchUndoStateChanged()
    {
        Dispatcher? dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess())
        {
            RaiseUndoStateChanged();
            return;
        }

        dispatcher.BeginInvoke((Action)RaiseUndoStateChanged);
    }

    private void RaiseUndoStateChanged()
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }
}

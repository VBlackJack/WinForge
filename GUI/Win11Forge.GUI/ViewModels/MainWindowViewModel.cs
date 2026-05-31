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

using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Win11Forge.GUI.Controls;
using Win11Forge.GUI.Services;
using Loc = Win11Forge.GUI.Resources.Resources;

namespace Win11Forge.GUI.ViewModels;

/// <summary>
/// ViewModel for MainWindow shell state and commands.
/// </summary>
public partial class MainWindowViewModel : ObservableObject, IDisposable
{
    private readonly IUndoService _undoService;
    private readonly IPowerShellBridge _powerShellBridge;
    private readonly IDialogService _dialogService;
    private readonly IToastService _toastService;
    private readonly EventHandler _undoStateChangedHandler;
    private bool _disposed;

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
            var version = await _powerShellBridge.GetWin11ForgeVersionAsync();
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
            var shortcutsPanel = new KeyboardShortcutsPanel();
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
        var description = _undoService.NextUndoDescription;
        var success = await _undoService.UndoAsync();

        if (success)
        {
            var message = string.Format(Loc.Undo_ActionUndone, description ?? "");
            _toastService.ShowInfo(message);
        }
    }

    private async Task RedoAsync()
    {
        var description = _undoService.NextRedoDescription;
        var success = await _undoService.RedoAsync();

        if (success)
        {
            var message = string.Format(Loc.Undo_ActionRedone, description ?? "");
            _toastService.ShowInfo(message);
        }
    }

    private void DispatchUndoStateChanged()
    {
        var dispatcher = Application.Current?.Dispatcher;
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

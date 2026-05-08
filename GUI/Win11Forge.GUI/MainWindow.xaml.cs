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

using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Wpf.Ui.Controls;
using Win11Forge.GUI.Controls;
using Win11Forge.GUI.Helpers;
using Win11Forge.GUI.Messages;
using Win11Forge.GUI.Services;
using Win11Forge.GUI.ViewModels;
using Win11Forge.GUI.Views;
using Loc = Win11Forge.GUI.Resources.Resources;

namespace Win11Forge.GUI;

/// <summary>
/// Main application window with Fluent Design navigation.
/// Uses NavigationView.ReplaceContent for view switching.
/// Implements cleanup pattern for event handlers and messenger subscriptions.
/// </summary>
public partial class MainWindow : FluentWindow, INotifyPropertyChanged, IDisposable
{
    private bool _disposed;
    private bool _initializationFailed;
    private readonly IPowerShellBridge? _powerShellBridge;
    private readonly IDeploymentHistoryService? _historyService;
    private readonly IAppSettingsService? _settingsService;
    private readonly IProfileExportService? _profileExportService;
    private readonly ToastService? _toastService;
    private readonly INavigationService? _navigationService;
    private readonly IUndoService? _undoService;
    private readonly DashboardViewModel? _dashboardViewModel;
    private readonly DeploymentViewModel? _deploymentViewModel;
    private readonly AppsViewModel? _appsViewModel;
    private readonly SettingsViewModel? _settingsViewModel;
    private readonly PrerequisitesViewModel? _prerequisitesViewModel;
    private readonly AppCatalogViewModel? _appCatalogViewModel;
    private readonly IAccessibilityService? _accessibilityService;

    // Event handlers stored for cleanup
    private EventHandler? _undoStateChangedHandler;
    private EventHandler? _navigationChangedHandler;

    // View cache for lazy creation
    private readonly Dictionary<ViewIndex, FrameworkElement> _viewCache = new();

    private bool _dashboardInitialized;
    private bool _deploymentInitialized;
    private bool _appsInitialized;
    private bool _settingsInitialized;
    private bool _prerequisitesInitialized;
    private bool _appCatalogInitialized;
    private bool _isNavigatingFromService;
    private bool _isNavigating;
    private int _currentViewIndex = -1;
    private bool _showBreadcrumb = true;

    /// <summary>
    /// Event for property change notification.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Command to show keyboard shortcuts dialog.
    /// </summary>
    public IAsyncRelayCommand ShowKeyboardShortcutsCommand { get; }

    /// <summary>
    /// Command to navigate to a specific view by index.
    /// </summary>
    public ICommand NavigateToCommand { get; }

    /// <summary>
    /// Command to navigate back to the previous view.
    /// </summary>
    public ICommand GoBackCommand { get; }

    /// <summary>
    /// Command to undo the last action.
    /// </summary>
    public ICommand UndoCommand { get; }

    /// <summary>
    /// Command to redo the last undone action.
    /// </summary>
    public ICommand RedoCommand { get; }

    /// <summary>
    /// Whether back navigation is available.
    /// </summary>
    public bool CanGoBack => _navigationService?.CanGoBack ?? false;

    /// <summary>
    /// Whether undo is available.
    /// </summary>
    public bool CanUndo => _undoService?.CanUndo ?? false;

    /// <summary>
    /// Whether redo is available.
    /// </summary>
    public bool CanRedo => _undoService?.CanRedo ?? false;

    /// <summary>
    /// Whether to show the breadcrumb navigation bar.
    /// </summary>
    public bool ShowBreadcrumb
    {
        get => _showBreadcrumb;
        set
        {
            if (_showBreadcrumb != value)
            {
                _showBreadcrumb = value;
                OnPropertyChanged();
            }
        }
    }

    public MainWindow()
    {
        InitializeComponent();
        LocalizationService.ApplyFlowDirection(this);

        // Initialize commands
        ShowKeyboardShortcutsCommand = new AsyncRelayCommand(ShowKeyboardShortcutsAsync);
        NavigateToCommand = new RelayCommand<string>(index =>
        {
            if (int.TryParse(index, out var viewIndex))
            {
                NavigateTo(viewIndex);
            }
        });
        GoBackCommand = new RelayCommand(GoBack, () => CanGoBack);
        UndoCommand = new AsyncRelayCommand(UndoAsync, () => CanUndo);
        RedoCommand = new AsyncRelayCommand(RedoAsync, () => CanRedo);

        // Set DataContext for XAML bindings
        DataContext = this;

        try
        {
            // Get services from DI container
            _powerShellBridge = App.GetService<IPowerShellBridge>();
            _historyService = App.GetService<IDeploymentHistoryService>();
            _settingsService = App.GetService<IAppSettingsService>();
            _profileExportService = App.GetService<IProfileExportService>();
            _toastService = App.GetService<ToastService>();
            _navigationService = App.GetService<INavigationService>();
            _undoService = App.GetService<IUndoService>();
            _accessibilityService = App.GetService<IAccessibilityService>();

            RestoreWindowPlacement();

            // Subscribe to undo service state changes (store handler for cleanup)
            _undoStateChangedHandler = (s, e) =>
            {
                Dispatcher.BeginInvoke(() =>
                {
                    OnPropertyChanged(nameof(CanUndo));
                    OnPropertyChanged(nameof(CanRedo));
                });
            };
            _undoService.StateChanged += _undoStateChangedHandler;

            // Subscribe to navigation service changes (store handler for cleanup)
            _navigationChangedHandler = (s, e) =>
            {
                Dispatcher.BeginInvoke(() =>
                {
                    OnPropertyChanged(nameof(CanGoBack));
                    if (!_isNavigatingFromService)
                    {
                        _isNavigatingFromService = true;
                        SelectNavigationItem(_navigationService.CurrentIndex);
                        _isNavigatingFromService = false;
                    }
                });
            };
            _navigationService.NavigationChanged += _navigationChangedHandler;

            // Create ViewModels using DI
            _dashboardViewModel = App.GetService<DashboardViewModel>();
            _deploymentViewModel = App.GetService<DeploymentViewModel>();
            _appsViewModel = App.GetService<AppsViewModel>();
            _settingsViewModel = App.GetService<SettingsViewModel>();
            _prerequisitesViewModel = App.GetService<PrerequisitesViewModel>();
            _appCatalogViewModel = App.GetService<AppCatalogViewModel>();

            // Initialize on window load
            Loaded += MainWindow_Loaded;

            // Subscribe to window closing for cleanup
            Closing += MainWindow_Closing;

            // Subscribe to navigation messages
            WeakReferenceMessenger.Default.Register<NavigateMessage>(this, (r, m) =>
            {
                Dispatcher.BeginInvoke(() => NavigateTo(m.TargetViewIndex));
            });
        }
        catch (Exception ex)
        {
            _initializationFailed = true;

            // Show error dialog and allow graceful exit
            System.Windows.MessageBox.Show(
                string.Format(Loc.Init_ErrorMessage, ex.Message),
                Loc.Init_ErrorTitle,
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);

            // Close the window after showing the error - fields remain null (nullable types)
            Dispatcher.BeginInvoke(() => Close());
        }
    }

    /// <summary>
    /// Gets or creates a view for the specified navigation index.
    /// Views are created lazily and cached for reuse.
    /// </summary>
    private FrameworkElement GetOrCreateView(ViewIndex index)
    {
        if (_viewCache.TryGetValue(index, out var cached))
            return cached;

        FrameworkElement view = index switch
        {
            ViewIndex.Dashboard => new DashboardView { DataContext = _dashboardViewModel },
            ViewIndex.Prerequisites => new PrerequisitesView { DataContext = _prerequisitesViewModel },
            ViewIndex.Apps => new AppsView { DataContext = _appsViewModel },
            ViewIndex.Deployment => new DeploymentView { DataContext = _deploymentViewModel },
            ViewIndex.Settings => new SettingsView { DataContext = _settingsViewModel },
            ViewIndex.AppCatalog => new AppCatalogView { DataContext = _appCatalogViewModel },
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };

        // Ensure theme-aware text color propagates to all child TextBlocks
        view.SetResourceReference(TextElement.ForegroundProperty, "TextFillColorPrimaryBrush");

        _viewCache[index] = view;
        return view;
    }

    /// <summary>
    /// Raises PropertyChanged event.
    /// </summary>
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Handles window closing to clean up resources.
    /// Unsubscribes from events and disposes ViewModels to prevent memory leaks.
    /// </summary>
    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        SaveWindowPlacement();
        Dispose();
    }

    private void RestoreWindowPlacement()
    {
        try
        {
            var settings = _settingsService?.LoadSettings();
            WindowPlacementHelper.ApplyStartupPlacement(this, settings?.MainWindowPlacement);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to restore window placement: {ex.Message}");
        }
    }

    private void SaveWindowPlacement()
    {
        if (_initializationFailed || _settingsService == null)
        {
            return;
        }

        try
        {
            var settings = _settingsService.LoadSettings();
            settings.MainWindowPlacement = WindowPlacementHelper.CapturePlacement(this);
            if (!_settingsService.SaveSettings(settings))
            {
                System.Diagnostics.Debug.WriteLine("Failed to save window placement: settings persistence returned false");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save window placement: {ex.Message}");
        }
    }

    /// <summary>
    /// Navigates back to the previous view.
    /// </summary>
    private void GoBack()
    {
        if (_navigationService?.CanGoBack == true)
        {
            _isNavigatingFromService = true;
            _navigationService.GoBack();
            _isNavigatingFromService = false;
        }
    }

    /// <summary>
    /// Undoes the last action.
    /// </summary>
    private async Task UndoAsync()
    {
        if (_undoService == null) return;

        var description = _undoService.NextUndoDescription;
        var success = await _undoService.UndoAsync();

        if (success && _toastService != null)
        {
            var message = string.Format(Loc.Undo_ActionUndone, description ?? "");
            _toastService.ShowInfo(message);
        }
    }

    /// <summary>
    /// Redoes the last undone action.
    /// </summary>
    private async Task RedoAsync()
    {
        if (_undoService == null) return;

        var description = _undoService.NextRedoDescription;
        var success = await _undoService.RedoAsync();

        if (success && _toastService != null)
        {
            var message = string.Format(Loc.Undo_ActionRedone, description ?? "");
            _toastService.ShowInfo(message);
        }
    }

    /// <summary>
    /// Updates the breadcrumb navigation based on current view.
    /// </summary>
    private void UpdateBreadcrumb(int viewIndex)
    {
        if (BreadcrumbControl == null) return;

        var labels = new[]
        {
            Loc.Nav_Dashboard,
            Loc.Nav_Prerequisites,
            Loc.Nav_Apps,
            Loc.Nav_Deployment,
            Loc.Nav_Settings,
            Loc.Nav_AppDatabase
        };

        // Current view is always the last item, previous views are clickable
        BreadcrumbControl.SetItems(
            labels.Take(viewIndex + 1).ToArray(),
            viewIndex,
            NavigateTo);
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            // Skip if initialization failed
            if (_initializationFailed || _dashboardViewModel == null) return;

            // Initialize toast service with WPF UI snackbar (created programmatically)
            var snackbar = new Snackbar(RootSnackbarPresenter) { Timeout = TimeSpan.FromSeconds(3) };
            _toastService?.SetSnackbarControl(snackbar);

            // Initialize dialog service with ContentDialog host
            var dialogService = App.GetService<IDialogService>();
            if (dialogService is DialogService ds)
            {
                ds.SetDialogHost(RootContentDialog);
            }

            // Initialize accessibility service with live region for screen readers
            if (_accessibilityService is AccessibilityService accessibilityService)
            {
                accessibilityService.Initialize(ScreenReaderLiveRegion);
            }

            // Check for first run and show onboarding
            var settings = _settingsService?.LoadSettings();
            if (settings?.IsFirstRun == true)
            {
                await ShowOnboardingAsync();
            }

            // Restore last navigation state or default to Dashboard
            if (settings != null && settings.LastNavigationIndex > 0 && settings.LastNavigationIndex < RootNavigation.MenuItems.Count)
            {
                SelectNavigationItem(settings.LastNavigationIndex);
            }
            else
            {
                // Initialize Dashboard (default view)
                SelectNavigationItem(0);
            }

            // Set window title with dynamic version
            await UpdateWindowTitleAsync();

            // Start cache pre-warming AFTER the initial view has loaded
            // to avoid resource contention with the first view's initialization
            WarmCacheAsync().SafeFireAndForget(
                onException: ex => System.Diagnostics.Debug.WriteLine($"Cache pre-warming failed (non-critical): {ex.Message}"));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"MainWindow_Loaded failed: {ex}");
            _toastService?.ShowError(string.Format(Loc.Init_ErrorMessage, ex.Message));
        }
    }

    private async Task UpdateWindowTitleAsync()
    {
        try
        {
            if (_powerShellBridge == null)
            {
                Title = string.Format(Loc.App_Title, "?");
                return;
            }
            var version = await _powerShellBridge.GetWin11ForgeVersionAsync();
            Title = string.Format(Loc.App_Title, version);
        }
        catch (Exception ex)
        {
            // Fallback to static title if version retrieval fails
            System.Diagnostics.Debug.WriteLine($"Failed to retrieve version for title: {ex.Message}");
            Title = string.Format(Loc.App_Title, "?");
        }
    }

    /// <summary>
    /// Warms the detection cache in the background for faster subsequent scans.
    /// </summary>
    private async Task WarmCacheAsync()
    {
        if (_powerShellBridge is PowerShellBridgeFacade facade)
        {
            System.Diagnostics.Debug.WriteLine("Starting detection cache pre-warming...");
            await facade.WarmDetectionCacheAsync();
            var stats = facade.GetDetectionCacheStatistics();
            System.Diagnostics.Debug.WriteLine($"Cache warmed: {stats.PackageCount} packages in {stats.AverageDetectionTime.TotalMilliseconds:F0}ms");
        }
    }

    /// <summary>
    /// Handles NavigationView back button requests.
    /// </summary>
    private void RootNavigation_BackRequested(NavigationView sender, RoutedEventArgs args)
    {
        GoBack();
    }

    /// <summary>
    /// Handles NavigationView selection changes (user click on nav item).
    /// </summary>
    private void RootNavigation_SelectionChanged(NavigationView sender, RoutedEventArgs args)
    {
        if (!IsLoaded || _isNavigating) return;

        // Try SelectedItem first
        var selectedItem = RootNavigation.SelectedItem;
        if (selectedItem != null)
        {
            var idx = RootNavigation.MenuItems.IndexOf(selectedItem);
            if (idx >= 0)
            {
                SelectNavigationItem(idx);
                return;
            }
        }

        // Fallback: find the active item
        for (var i = 0; i < RootNavigation.MenuItems.Count; i++)
        {
            if (RootNavigation.MenuItems[i] is NavigationViewItem item && item.IsActive)
            {
                SelectNavigationItem(i);
                return;
            }
        }
    }

    /// <summary>
    /// Handles direct click on a NavigationViewItem.
    /// Used as primary navigation trigger since SelectionChanged may not fire
    /// without TargetPageType set on NavigationViewItems.
    /// </summary>
    private void NavigationViewItem_Click(object sender, RoutedEventArgs e)
    {
        if (_isNavigating) return;

        if (sender is NavigationViewItem item)
        {
            var tag = item.Tag;
            if (tag is string tagStr && int.TryParse(tagStr, out var index))
            {
                SelectNavigationItem(index);
            }
            else
            {
                // Tag might be int if XAML parsed it differently
                var idx = RootNavigation.MenuItems.IndexOf(item);
                if (idx >= 0)
                    SelectNavigationItem(idx);
            }
        }
    }

    /// <summary>
    /// Selects a navigation item by index and loads the corresponding view content.
    /// </summary>
    private void SelectNavigationItem(int index)
    {
        if (index < 0 || index >= RootNavigation.MenuItems.Count) return;

        _isNavigating = true;
        try
        {
            SetActiveItem(index);
            LoadViewContent(index);
        }
        finally
        {
            _isNavigating = false;
        }
    }

    /// <summary>
    /// Sets the visual active state on navigation items.
    /// </summary>
    private void SetActiveItem(int index)
    {
        for (var i = 0; i < RootNavigation.MenuItems.Count; i++)
        {
            if (RootNavigation.MenuItems[i] is NavigationViewItem item)
            {
                item.IsActive = i == index;
            }
        }
    }

    /// <summary>
    /// Loads the view content for the specified index into the NavigationView content area.
    /// Skips if the view is already loaded (deduplication guard).
    /// </summary>
    private void LoadViewContent(int selectedIndex)
    {
        if (selectedIndex == _currentViewIndex) return;

        try
        {
            UpdateBreadcrumb(selectedIndex);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"UpdateBreadcrumb failed: {ex.Message}");
        }

        try
        {
            var viewIndex = (ViewIndex)selectedIndex;
            var view = GetOrCreateView(viewIndex);
            RootNavigation.ReplaceContent(view, null);

            // Only update the index AFTER content was successfully replaced
            _currentViewIndex = selectedIndex;

            switch (viewIndex)
            {
                case ViewIndex.Dashboard:
                    SafeInitializeAsync(InitializeDashboardAsync, "Dashboard").SafeFireAndForget();
                    break;

                case ViewIndex.Prerequisites:
                    SafeInitializeAsync(InitializePrerequisitesAsync, "Prerequisites").SafeFireAndForget();
                    break;

                case ViewIndex.Apps:
                    SafeInitializeAsync(InitializeAppsAsync, "Apps").SafeFireAndForget();
                    break;

                case ViewIndex.Deployment:
                    SafeInitializeAsync(InitializeDeploymentAsync, "Deployment").SafeFireAndForget();
                    break;

                case ViewIndex.Settings:
                    SafeInitializeAsync(InitializeSettingsAsync, "Settings").SafeFireAndForget();
                    break;

                case ViewIndex.AppCatalog:
                    SafeInitializeAsync(InitializeAppCatalogAsync, "AppCatalog").SafeFireAndForget();
                    break;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LoadViewContent failed for index {selectedIndex}: {ex}");
            _toastService?.ShowError(string.Format(Loc.Error_LoadViewFailed, ex.Message));
        }
    }

    /// <summary>
    /// Safely executes an async initialization task, catching and logging exceptions.
    /// Prevents unobserved task exceptions from fire-and-forget calls.
    /// </summary>
    private async Task SafeInitializeAsync(Func<Task> initializeTask, string viewName)
    {
        try
        {
            await initializeTask();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to initialize {viewName} view: {ex.Message}");
            _toastService?.ShowError(string.Format(Loc.Error_LoadViewFailed, ex.Message));
        }
    }

    private async Task InitializeDashboardAsync()
    {
        if (_dashboardInitialized || _dashboardViewModel == null) return;

        await _dashboardViewModel.InitializeAsync();
        _dashboardInitialized = true;
    }

    private async Task InitializeDeploymentAsync()
    {
        if (_deploymentInitialized || _deploymentViewModel == null) return;

        await _deploymentViewModel.InitializeAsync();
        _deploymentInitialized = true;
    }

    private async Task InitializeAppsAsync()
    {
        if (_appsInitialized || _appsViewModel == null) return;

        await _appsViewModel.InitializeAsync();
        _appsInitialized = true;
    }

    private async Task InitializeSettingsAsync()
    {
        if (_settingsInitialized || _settingsViewModel == null) return;

        await _settingsViewModel.InitializeAsync();
        _settingsInitialized = true;
    }

    private async Task InitializePrerequisitesAsync()
    {
        if (_prerequisitesInitialized || _prerequisitesViewModel == null) return;

        await _prerequisitesViewModel.InitializeAsync();
        _prerequisitesInitialized = true;
    }

    private async Task InitializeAppCatalogAsync()
    {
        if (_appCatalogInitialized || _appCatalogViewModel == null) return;

        await _appCatalogViewModel.LoadApplicationsCommand.ExecuteAsync(null);
        _appCatalogInitialized = true;
    }

    /// <summary>
    /// Navigates to the specified view by index.
    /// Can be called from ViewModels via WeakReferenceMessenger or direct reference.
    /// </summary>
    public void NavigateTo(int viewIndex)
    {
        if (viewIndex >= 0 && viewIndex < RootNavigation.MenuItems.Count)
        {
            // Update navigation service (which will trigger navigation)
            if (!_isNavigatingFromService && _navigationService != null)
            {
                _navigationService.NavigateTo(viewIndex);
            }

            SelectNavigationItem(viewIndex);

            // Save navigation state for view preservation
            try
            {
                if (_settingsService == null) return;
                var settings = _settingsService.LoadSettings();
                settings.LastNavigationIndex = viewIndex;
                if (!_settingsService.SaveSettings(settings))
                {
                    System.Diagnostics.Debug.WriteLine("Failed to save navigation state: settings persistence returned false");
                }
            }
            catch (Exception ex)
            {
                // State saving is non-critical, but log for diagnostics
                System.Diagnostics.Debug.WriteLine($"Failed to save navigation state: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Shows the keyboard shortcuts help dialog.
    /// </summary>
    private async Task ShowKeyboardShortcutsAsync()
    {
        try
        {
            var shortcutsPanel = new KeyboardShortcutsPanel();
            var dialog = new Wpf.Ui.Controls.ContentDialog(RootContentDialog)
            {
                Title = Loc.Help_KeyboardShortcuts ?? "Keyboard Shortcuts",
                Content = shortcutsPanel,
                CloseButtonText = Loc.Common_OK ?? "OK"
            };

            shortcutsPanel.RequestClose = () => dialog.Hide(ContentDialogResult.None);
            await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            // Dialog display is non-critical, but log for diagnostics
            System.Diagnostics.Debug.WriteLine($"Failed to show keyboard shortcuts dialog: {ex.Message}");
        }
    }

    /// <summary>
    /// Shows the onboarding dialog for first-run experience.
    /// </summary>
    private async Task ShowOnboardingAsync()
    {
        try
        {
            var onboardingControl = new OnboardingDialog();
            onboardingControl.Completed += (_, dontShowAgain) =>
            {
                if (dontShowAgain && _settingsService != null)
                {
                    var settings = _settingsService.LoadSettings();
                    settings.IsFirstRun = false;
                    if (!_settingsService.SaveSettings(settings))
                    {
                        System.Diagnostics.Debug.WriteLine("Failed to persist onboarding flag: settings persistence returned false");
                    }
                }
            };

            var dialog = new Wpf.Ui.Controls.ContentDialog(RootContentDialog)
            {
                Title = Loc.Onboarding_Welcome ?? "Welcome",
                Content = onboardingControl,
                CloseButtonText = Loc.Common_OK ?? "OK"
            };
            await dialog.ShowAsync();

            // Mark first run complete after dialog closes
            if (_settingsService != null)
            {
                var currentSettings = _settingsService.LoadSettings();
                currentSettings.IsFirstRun = false;
                if (!_settingsService.SaveSettings(currentSettings))
                {
                    System.Diagnostics.Debug.WriteLine("Failed to persist first-run flag: settings persistence returned false");
                }
            }
        }
        catch (Exception ex)
        {
            // Onboarding is non-critical, but log for diagnostics
            System.Diagnostics.Debug.WriteLine($"Failed to show onboarding dialog: {ex.Message}");
        }
    }

    /// <summary>
    /// Releases all resources used by the MainWindow.
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
            // Unsubscribe from service events
            if (_undoService != null && _undoStateChangedHandler != null)
            {
                _undoService.StateChanged -= _undoStateChangedHandler;
                _undoStateChangedHandler = null;
            }

            if (_navigationService != null && _navigationChangedHandler != null)
            {
                _navigationService.NavigationChanged -= _navigationChangedHandler;
                _navigationChangedHandler = null;
            }

            // Unregister from WeakReferenceMessenger
            WeakReferenceMessenger.Default.Unregister<NavigateMessage>(this);

            // Dispose ViewModels that implement IDisposable
            (_deploymentViewModel as IDisposable)?.Dispose();
            (_appsViewModel as IDisposable)?.Dispose();
            (_dashboardViewModel as IDisposable)?.Dispose();
            (_settingsViewModel as IDisposable)?.Dispose();
            (_prerequisitesViewModel as IDisposable)?.Dispose();
            (_appCatalogViewModel as IDisposable)?.Dispose();
        }

        _disposed = true;
    }
}

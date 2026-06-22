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
using WinForge.GUI.Controls;
using WinForge.GUI.Helpers;
using WinForge.GUI.Messages;
using WinForge.GUI.Services;
using WinForge.GUI.ViewModels;
using WinForge.GUI.Views;
using Wpf.Ui.Controls;
using Loc = WinForge.GUI.Resources.Resources;

namespace WinForge.GUI;

/// <summary>
/// Main application window with Fluent Design navigation.
/// Uses NavigationView.ReplaceContent for view switching.
/// Implements cleanup pattern for event handlers and messenger subscriptions.
/// </summary>
public partial class MainWindow : FluentWindow, INotifyPropertyChanged, IDisposable
{
    private bool _disposed;
    private bool _initializationFailed;
    private MainWindowViewModel? _viewModel;
    private readonly IPowerShellBridge? _powerShellBridge;
    private readonly IDeploymentHistoryService? _historyService;
    private readonly IAppSettingsService? _settingsService;
    private readonly IProfileExportService? _profileExportService;
    private readonly ToastService? _toastService;
    private readonly INavigationService? _navigationService;
    private readonly ILoggingService _logger;

    // Event handlers stored for cleanup
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
    public IAsyncRelayCommand? ShowKeyboardShortcutsCommand => _viewModel?.ShowKeyboardShortcutsCommand;

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
    public ICommand? UndoCommand => _viewModel?.UndoCommand;

    /// <summary>
    /// Command to redo the last undone action.
    /// </summary>
    public ICommand? RedoCommand => _viewModel?.RedoCommand;

    /// <summary>
    /// Whether back navigation is available.
    /// </summary>
    public bool CanGoBack => _navigationService?.CanGoBack ?? false;

    /// <summary>
    /// Whether undo is available.
    /// </summary>
    public bool CanUndo => _viewModel?.CanUndo ?? false;

    /// <summary>
    /// Whether redo is available.
    /// </summary>
    public bool CanRedo => _viewModel?.CanRedo ?? false;

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

        // Initialize view-owned commands
        NavigateToCommand = new RelayCommand<string>(index =>
        {
            if (int.TryParse(index, out int viewIndex))
            {
                NavigateTo(viewIndex);
            }
        });
        GoBackCommand = new RelayCommand(GoBack, () => CanGoBack);

        try
        {
            // Get services from DI container
            _logger = App.GetService<ILoggingService>();
            _viewModel = App.GetService<MainWindowViewModel>();
            _powerShellBridge = App.GetService<IPowerShellBridge>();
            _historyService = App.GetService<IDeploymentHistoryService>();
            _settingsService = App.GetService<IAppSettingsService>();
            _profileExportService = App.GetService<IProfileExportService>();
            _toastService = App.GetService<ToastService>();
            _navigationService = App.GetService<INavigationService>();

            SubscribeToViewModel();

            // Set DataContext for XAML bindings
            DataContext = this;

            RestoreWindowPlacement();

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
            _logger = new LoggerFactory().CreateLogger<MainWindow>();
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
        if (_viewCache.TryGetValue(index, out FrameworkElement? cached))
            return cached;

        FrameworkElement view = index switch
        {
            ViewIndex.Dashboard => new DashboardView { DataContext = _viewModel?.DashboardViewModel },
            ViewIndex.Prerequisites => new PrerequisitesView { DataContext = _viewModel?.PrerequisitesViewModel },
            ViewIndex.Apps => new AppsView { DataContext = _viewModel?.AppsViewModel },
            ViewIndex.Deployment => new DeploymentView { DataContext = _viewModel?.DeploymentViewModel },
            ViewIndex.Settings => new SettingsView { DataContext = _viewModel?.SettingsViewModel },
            ViewIndex.AppCatalog => new AppCatalogView { DataContext = _viewModel?.AppCatalogViewModel },
            ViewIndex.Logs => new LogsView { DataContext = _viewModel?.LogsViewModel },
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

    private void SubscribeToViewModel()
    {
        if (_viewModel == null)
        {
            return;
        }

        _viewModel.PropertyChanged += MainWindowViewModel_PropertyChanged;
        SetWindowTitle(_viewModel.WindowTitle);
    }

    private void MainWindowViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainWindowViewModel.CanUndo):
                OnPropertyChanged(nameof(CanUndo));
                break;

            case nameof(MainWindowViewModel.CanRedo):
                OnPropertyChanged(nameof(CanRedo));
                break;

            case nameof(MainWindowViewModel.WindowTitle):
                if (_viewModel != null)
                {
                    SetWindowTitle(_viewModel.WindowTitle);
                }
                break;
        }
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
            AppSettings? settings = _settingsService?.LoadSettings();
            WindowPlacementHelper.ApplyStartupPlacement(this, settings?.MainWindowPlacement);
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Failed to restore window placement: {ex.Message}");
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
            AppSettings settings = _settingsService.LoadSettings();
            settings.MainWindowPlacement = WindowPlacementHelper.CapturePlacement(this);
            if (!_settingsService.SaveSettings(settings))
            {
                _logger.LogWarning("Failed to save window placement: settings persistence returned false");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Failed to save window placement: {ex.Message}");
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
    /// Updates the breadcrumb navigation based on current view.
    /// </summary>
    private void UpdateBreadcrumb(int viewIndex)
    {
        if (BreadcrumbControl == null) return;

        string[] labels = new[]
        {
            Loc.Nav_Dashboard,
            Loc.Nav_Prerequisites,
            Loc.Nav_Apps,
            Loc.Nav_Deployment,
            Loc.Nav_Settings,
            Loc.Nav_AppCatalog,
            Loc.Nav_Logs
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
            if (_initializationFailed || _viewModel == null) return;

            // Initialize toast service with WPF UI snackbar (created programmatically)
            Snackbar snackbar = new Snackbar(RootSnackbarPresenter) { Timeout = TimeSpan.FromSeconds(3) };
            _toastService?.SetSnackbarControl(snackbar);

            // Initialize dialog service with ContentDialog host
            IDialogService dialogService = App.GetService<IDialogService>();
            if (dialogService is DialogService ds)
            {
                ds.SetDialogHost(RootContentDialog);
            }

            // If a previous batch (Install / Update / Uninstall) was interrupted by a
            // crash, BSOD, or forced reboot, offer the user to resume / discard /
            // postpone the decision before any other UI flow takes over.
            await _viewModel.PromptBatchResumeIfPendingAsync(dialogService);

            // Check for first run and show onboarding
            AppSettings? settings = _settingsService?.LoadSettings();
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
            await _viewModel.UpdateWindowTitleAsync();

            // Start cache pre-warming AFTER the initial view has loaded
            // to avoid resource contention with the first view's initialization
            WarmCacheAsync().SafeFireAndForget(
                onException: ex => _logger.LogWarning($"Cache pre-warming failed (non-critical): {ex.Message}"));
        }
        catch (Exception ex)
        {
            _logger.LogError("MainWindow_Loaded failed", ex);
            _toastService?.ShowError(string.Format(Loc.Init_ErrorMessage, ex.Message));
        }
    }

    private void SetWindowTitle(string title)
    {
        Title = title;
        TitleBar.Title = title;
    }

    /// <summary>
    /// Warms the detection cache in the background for faster subsequent scans.
    /// </summary>
    private async Task WarmCacheAsync()
    {
        if (_powerShellBridge is PowerShellBridgeFacade facade)
        {
            await facade.WarmDetectionCacheAsync();
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
        INavigationViewItem? selectedItem = RootNavigation.SelectedItem;
        if (selectedItem is NavigationViewItem selectedNavigationItem
            && TryGetNavigationIndex(selectedNavigationItem, out int selectedIndex))
        {
            SelectNavigationItem(selectedIndex);
            return;
        }

        if (selectedItem != null)
        {
            int idx = RootNavigation.MenuItems.IndexOf(selectedItem);
            if (idx >= 0)
            {
                SelectNavigationItem(idx);
                return;
            }
        }

        // Fallback: find the active item
        for (int i = 0; i < RootNavigation.MenuItems.Count; i++)
        {
            if (RootNavigation.MenuItems[i] is NavigationViewItem item
                && item.IsActive
                && TryGetNavigationIndex(item, out int activeIndex))
            {
                SelectNavigationItem(activeIndex);
                return;
            }

            if (RootNavigation.MenuItems[i] is NavigationViewItem { IsActive: true })
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
            if (TryGetNavigationIndex(item, out int index))
            {
                SelectNavigationItem(index);
            }
            else
            {
                // Tag might be int if XAML parsed it differently
                int idx = RootNavigation.MenuItems.IndexOf(item);
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
        for (int i = 0; i < RootNavigation.MenuItems.Count; i++)
        {
            if (RootNavigation.MenuItems[i] is NavigationViewItem item)
            {
                item.IsActive = TryGetNavigationIndex(item, out int itemIndex)
                    ? itemIndex == index
                    : i == index;
            }
        }
    }

    private static bool TryGetNavigationIndex(NavigationViewItem item, out int index)
    {
        if (item.Tag is string tagString && int.TryParse(tagString, out index))
        {
            return true;
        }

        if (item.Tag is int tagIndex)
        {
            index = tagIndex;
            return true;
        }

        index = -1;
        return false;
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
            _logger.LogWarning($"UpdateBreadcrumb failed: {ex.Message}");
        }

        try
        {
            ViewIndex viewIndex = (ViewIndex)selectedIndex;
            FrameworkElement view = GetOrCreateView(viewIndex);
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
            _logger.LogError($"LoadViewContent failed for index {selectedIndex}", ex);
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
            _logger.LogError($"Failed to initialize {viewName} view", ex);
            _toastService?.ShowError(string.Format(Loc.Error_LoadViewFailed, ex.Message));
        }
    }

    private async Task InitializeDashboardAsync()
    {
        DashboardViewModel? dashboardViewModel = _viewModel?.DashboardViewModel;
        if (_dashboardInitialized || dashboardViewModel == null) return;

        await dashboardViewModel.InitializeAsync();
        _dashboardInitialized = true;
    }

    private async Task InitializeDeploymentAsync()
    {
        DeploymentViewModel? deploymentViewModel = _viewModel?.DeploymentViewModel;
        if (_deploymentInitialized || deploymentViewModel == null) return;

        await deploymentViewModel.InitializeAsync();
        _deploymentInitialized = true;
    }

    private async Task InitializeAppsAsync()
    {
        AppsViewModel? appsViewModel = _viewModel?.AppsViewModel;
        if (_appsInitialized || appsViewModel == null) return;

        await appsViewModel.InitializeAsync();
        _appsInitialized = true;
    }

    private async Task InitializeSettingsAsync()
    {
        SettingsViewModel? settingsViewModel = _viewModel?.SettingsViewModel;
        if (_settingsInitialized || settingsViewModel == null) return;

        await settingsViewModel.InitializeAsync();
        _settingsInitialized = true;
    }

    private async Task InitializePrerequisitesAsync()
    {
        PrerequisitesViewModel? prerequisitesViewModel = _viewModel?.PrerequisitesViewModel;
        if (_prerequisitesInitialized || prerequisitesViewModel == null) return;

        await prerequisitesViewModel.InitializeAsync();
        _prerequisitesInitialized = true;
    }

    private async Task InitializeAppCatalogAsync()
    {
        AppCatalogViewModel? appCatalogViewModel = _viewModel?.AppCatalogViewModel;
        if (_appCatalogInitialized || appCatalogViewModel == null) return;

        await appCatalogViewModel.LoadApplicationsCommand.ExecuteAsync(null);
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
                AppSettings settings = _settingsService.LoadSettings();
                settings.LastNavigationIndex = viewIndex;
                if (!_settingsService.SaveSettings(settings))
                {
                    _logger.LogWarning("Failed to save navigation state: settings persistence returned false");
                }
            }
            catch (Exception ex)
            {
                // State saving is non-critical, but log for diagnostics
                _logger.LogWarning($"Failed to save navigation state: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Shows the onboarding dialog for first-run experience.
    /// </summary>
    private async Task ShowOnboardingAsync()
    {
        try
        {
            OnboardingDialog onboardingControl = new OnboardingDialog();

            ContentDialog dialog = new Wpf.Ui.Controls.ContentDialog(RootContentDialog)
            {
                Title = Loc.Onboarding_Welcome ?? "Welcome",
                Content = onboardingControl,
                CloseButtonText = Loc.Common_OK ?? "OK"
            };

            onboardingControl.Completed += (_, dontShowAgain) =>
            {
                if (dontShowAgain && _settingsService != null)
                {
                    AppSettings settings = _settingsService.LoadSettings();
                    settings.IsFirstRun = false;
                    if (!_settingsService.SaveSettings(settings))
                    {
                        _logger.LogWarning("Failed to persist onboarding flag: settings persistence returned false");
                    }
                }

                // The hosted control has no reference to its ContentDialog, so the primary
                // "Get Started" action must close the dialog here. Without this, the only
                // keyboard-reachable action would leave the dialog open.
                dialog.Hide();
            };

            await dialog.ShowAsync();

            // Mark first run complete after dialog closes
            if (_settingsService != null)
            {
                AppSettings currentSettings = _settingsService.LoadSettings();
                currentSettings.IsFirstRun = false;
                if (!_settingsService.SaveSettings(currentSettings))
                {
                    _logger.LogWarning("Failed to persist first-run flag: settings persistence returned false");
                }
            }
        }
        catch (Exception ex)
        {
            // Onboarding is non-critical, but log for diagnostics
            _logger.LogWarning($"Failed to show onboarding dialog: {ex.Message}");
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
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged -= MainWindowViewModel_PropertyChanged;
                _viewModel.Dispose();
            }

            // Unsubscribe from service events
            if (_navigationService != null && _navigationChangedHandler != null)
            {
                _navigationService.NavigationChanged -= _navigationChangedHandler;
                _navigationChangedHandler = null;
            }

            // Unregister from WeakReferenceMessenger
            WeakReferenceMessenger.Default.Unregister<NavigateMessage>(this);

            // Dispose ViewModels that implement IDisposable
            (_viewModel?.DeploymentViewModel as IDisposable)?.Dispose();
            (_viewModel?.AppsViewModel as IDisposable)?.Dispose();
            (_viewModel?.DashboardViewModel as IDisposable)?.Dispose();
            (_viewModel?.SettingsViewModel as IDisposable)?.Dispose();
            (_viewModel?.PrerequisitesViewModel as IDisposable)?.Dispose();
            (_viewModel?.AppCatalogViewModel as IDisposable)?.Dispose();
        }

        _disposed = true;
    }
}

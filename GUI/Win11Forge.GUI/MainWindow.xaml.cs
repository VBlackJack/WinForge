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
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using MaterialDesignThemes.Wpf;
using Win11Forge.GUI.Controls;
using Win11Forge.GUI.Messages;
using Win11Forge.GUI.Services;
using Win11Forge.GUI.ViewModels;

namespace Win11Forge.GUI;

/// <summary>
/// Main application window with navigation.
/// </summary>
public partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly IPowerShellBridge _powerShellBridge;
    private readonly IDeploymentHistoryService _historyService;
    private readonly IAppSettingsService _settingsService;
    private readonly IProfileExportService _profileExportService;
    private readonly ToastService _toastService;
    private readonly INavigationService _navigationService;
    private readonly DashboardViewModel _dashboardViewModel;
    private readonly DeploymentViewModel _deploymentViewModel;
    private readonly AppsViewModel _appsViewModel;
    private readonly SettingsViewModel _settingsViewModel;
    private readonly PrerequisitesViewModel _prerequisitesViewModel;

    private bool _dashboardInitialized;
    private bool _deploymentInitialized;
    private bool _appsInitialized;
    private bool _settingsInitialized;
    private bool _prerequisitesInitialized;
    private bool _isNavigatingFromService;

    /// <summary>
    /// Event for property change notification.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Command to show keyboard shortcuts dialog.
    /// </summary>
    public ICommand ShowKeyboardShortcutsCommand { get; }

    /// <summary>
    /// Command to navigate to a specific view by index.
    /// </summary>
    public ICommand NavigateToCommand { get; }

    /// <summary>
    /// Command to navigate back to the previous view.
    /// </summary>
    public ICommand GoBackCommand { get; }

    /// <summary>
    /// Whether back navigation is available.
    /// </summary>
    public bool CanGoBack => _navigationService?.CanGoBack ?? false;

    public MainWindow()
    {
        InitializeComponent();

        // Initialize commands
        ShowKeyboardShortcutsCommand = new RelayCommand(ShowKeyboardShortcuts);
        NavigateToCommand = new RelayCommand<string>(index =>
        {
            if (int.TryParse(index, out var viewIndex))
            {
                NavigateTo(viewIndex);
            }
        });
        GoBackCommand = new RelayCommand(GoBack, () => CanGoBack);

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

            // Subscribe to navigation service changes
            _navigationService.NavigationChanged += (s, e) =>
            {
                Dispatcher.Invoke(() =>
                {
                    OnPropertyChanged(nameof(CanGoBack));
                    if (!_isNavigatingFromService)
                    {
                        _isNavigatingFromService = true;
                        NavigationListBox.SelectedIndex = _navigationService.CurrentIndex;
                        _isNavigatingFromService = false;
                    }
                });
            };

            // Create ViewModels using DI
            _dashboardViewModel = App.GetService<DashboardViewModel>();
            _deploymentViewModel = App.GetService<DeploymentViewModel>();
            _appsViewModel = App.GetService<AppsViewModel>();
            _settingsViewModel = App.GetService<SettingsViewModel>();
            _prerequisitesViewModel = App.GetService<PrerequisitesViewModel>();

            // Wire up DataContexts
            DashboardViewControl.DataContext = _dashboardViewModel;
            DeploymentViewControl.DataContext = _deploymentViewModel;
            AppsViewControl.DataContext = _appsViewModel;
            SettingsViewControl.DataContext = _settingsViewModel;
            PrerequisitesViewControl.DataContext = _prerequisitesViewModel;

            // Initialize on window load
            Loaded += MainWindow_Loaded;

            // Subscribe to navigation messages
            WeakReferenceMessenger.Default.Register<NavigateMessage>(this, (r, m) =>
            {
                Dispatcher.Invoke(() => NavigateTo(m.TargetViewIndex));
            });
        }
        catch (Exception ex)
        {
            // Show error dialog and allow graceful exit
            MessageBox.Show(
                $"Failed to initialize Win11Forge:\n\n{ex.Message}\n\nPlease ensure Win11Forge is extracted correctly with all folders (Config/, Modules/, Profiles/).",
                "Initialization Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            // Set to null to avoid further issues (fields will be checked before use)
            _powerShellBridge = null!;
            _historyService = null!;
            _settingsService = null!;
            _profileExportService = null!;
            _toastService = null!;
            _navigationService = null!;
            _dashboardViewModel = null!;
            _deploymentViewModel = null!;
            _appsViewModel = null!;
            _settingsViewModel = null!;
            _prerequisitesViewModel = null!;
        }
    }

    /// <summary>
    /// Raises PropertyChanged event.
    /// </summary>
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Skip if initialization failed
        if (_dashboardViewModel == null) return;

        // Initialize toast service with snackbar
        _toastService.SetMessageQueue(MainSnackbar.MessageQueue);

        // Check for first run and show onboarding
        var settings = _settingsService.LoadSettings();
        if (settings.IsFirstRun)
        {
            await ShowOnboardingAsync();
        }

        // Restore last navigation state
        if (settings.LastNavigationIndex > 0 && settings.LastNavigationIndex < NavigationListBox.Items.Count)
        {
            NavigationListBox.SelectedIndex = settings.LastNavigationIndex;
        }
        else
        {
            // Initialize Dashboard (default view)
            await InitializeDashboardAsync();
        }

        // Set window title with dynamic version
        await UpdateWindowTitleAsync();
    }

    private async Task UpdateWindowTitleAsync()
    {
        try
        {
            var version = await _powerShellBridge.GetWin11ForgeVersionAsync();
            Title = string.Format(Win11Forge.GUI.Resources.Resources.App_Title, version);
        }
        catch
        {
            // Fallback to static title if version retrieval fails
            Title = string.Format(Win11Forge.GUI.Resources.Resources.App_Title, "3.1.0");
        }
    }

    private void NavigationListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Guard: Views not yet initialized during XAML loading
        if (DashboardViewControl == null) return;

        var selectedIndex = NavigationListBox.SelectedIndex;

        // Hide all views
        DashboardViewControl.Visibility = Visibility.Collapsed;
        DeploymentViewControl.Visibility = Visibility.Collapsed;
        AppsViewControl.Visibility = Visibility.Collapsed;
        SettingsViewControl.Visibility = Visibility.Collapsed;
        PrerequisitesViewControl.Visibility = Visibility.Collapsed;

        // Show selected view and initialize if needed
        switch (selectedIndex)
        {
            case 0: // Dashboard
                DashboardViewControl.Visibility = Visibility.Visible;
                _ = InitializeDashboardAsync();
                break;

            case 1: // Prerequisites
                PrerequisitesViewControl.Visibility = Visibility.Visible;
                _ = InitializePrerequisitesAsync();
                break;

            case 2: // Apps
                AppsViewControl.Visibility = Visibility.Visible;
                _ = InitializeAppsAsync();
                break;

            case 3: // Deployment
                DeploymentViewControl.Visibility = Visibility.Visible;
                _ = InitializeDeploymentAsync();
                break;

            case 4: // Settings
                SettingsViewControl.Visibility = Visibility.Visible;
                _ = InitializeSettingsAsync();
                break;
        }
    }

    private async Task InitializeDashboardAsync()
    {
        if (_dashboardInitialized) return;

        await _dashboardViewModel.InitializeAsync();
        _dashboardInitialized = true;
    }

    private async Task InitializeDeploymentAsync()
    {
        if (_deploymentInitialized) return;

        await _deploymentViewModel.InitializeAsync();
        _deploymentInitialized = true;
    }

    private async Task InitializeAppsAsync()
    {
        if (_appsInitialized) return;

        await _appsViewModel.InitializeAsync();
        _appsInitialized = true;
    }

    private async Task InitializeSettingsAsync()
    {
        if (_settingsInitialized) return;

        await _settingsViewModel.InitializeAsync();
        _settingsInitialized = true;
    }

    private async Task InitializePrerequisitesAsync()
    {
        if (_prerequisitesInitialized) return;

        await _prerequisitesViewModel.InitializeAsync();
        _prerequisitesInitialized = true;
    }

    /// <summary>
    /// Navigates to the specified view by index.
    /// Can be called from ViewModels via WeakReferenceMessenger or direct reference.
    /// </summary>
    public void NavigateTo(int viewIndex)
    {
        if (viewIndex >= 0 && viewIndex < NavigationListBox.Items.Count)
        {
            // Update navigation service (which will trigger navigation)
            if (!_isNavigatingFromService && _navigationService != null)
            {
                _navigationService.NavigateTo(viewIndex);
            }

            NavigationListBox.SelectedIndex = viewIndex;

            // Save navigation state for view preservation
            try
            {
                var settings = _settingsService.LoadSettings();
                settings.LastNavigationIndex = viewIndex;
                _settingsService.SaveSettings(settings);
            }
            catch
            {
                // State saving is non-critical
            }
        }
    }

    /// <summary>
    /// Shows the keyboard shortcuts help dialog.
    /// </summary>
    private async void ShowKeyboardShortcuts()
    {
        try
        {
            var panel = new KeyboardShortcutsPanel();
            await DialogHost.Show(panel, "RootDialog");
        }
        catch
        {
            // Dialog display is non-critical
        }
    }

    /// <summary>
    /// Shows the onboarding dialog for first-run experience.
    /// </summary>
    private async Task ShowOnboardingAsync()
    {
        try
        {
            var dialog = new OnboardingDialog();
            dialog.Completed += (_, dontShowAgain) =>
            {
                if (dontShowAgain)
                {
                    var settings = _settingsService.LoadSettings();
                    settings.IsFirstRun = false;
                    _settingsService.SaveSettings(settings);
                }
            };

            await DialogHost.Show(dialog, "RootDialog");

            // Mark first run complete after dialog closes
            var currentSettings = _settingsService.LoadSettings();
            currentSettings.IsFirstRun = false;
            _settingsService.SaveSettings(currentSettings);
        }
        catch
        {
            // Onboarding is non-critical
        }
    }
}

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
using System.Windows.Controls;
using CommunityToolkit.Mvvm.Messaging;
using Win11Forge.GUI.Messages;
using Win11Forge.GUI.Services;
using Win11Forge.GUI.ViewModels;

namespace Win11Forge.GUI;

/// <summary>
/// Main application window with navigation.
/// </summary>
public partial class MainWindow : Window
{
    private readonly IPowerShellBridge _powerShellBridge;
    private readonly IDeploymentHistoryService _historyService;
    private readonly DashboardViewModel _dashboardViewModel;
    private readonly DeploymentViewModel _deploymentViewModel;
    private readonly AppsViewModel _appsViewModel;
    private readonly ProfileEditorViewModel _profileEditorViewModel;
    private readonly SettingsViewModel _settingsViewModel;
    private readonly PrerequisitesViewModel _prerequisitesViewModel;

    private bool _dashboardInitialized;
    private bool _deploymentInitialized;
    private bool _appsInitialized;
    private bool _settingsInitialized;
    private bool _prerequisitesInitialized;

    public MainWindow()
    {
        InitializeComponent();

        try
        {
            // Create shared services
            _powerShellBridge = new PowerShellBridge();
            _historyService = new DeploymentHistoryService();

            // Create ViewModels
            _dashboardViewModel = new DashboardViewModel(_powerShellBridge, _historyService);
            _deploymentViewModel = new DeploymentViewModel(_powerShellBridge, _historyService);
            _appsViewModel = new AppsViewModel(_powerShellBridge);
            _profileEditorViewModel = new ProfileEditorViewModel(_powerShellBridge);
            _settingsViewModel = new SettingsViewModel();
            _prerequisitesViewModel = new PrerequisitesViewModel(_powerShellBridge);

            // Wire up DataContexts
            DashboardViewControl.DataContext = _dashboardViewModel;
            DeploymentViewControl.DataContext = _deploymentViewModel;
            AppsViewControl.DataContext = _appsViewModel;
            ProfileEditorViewControl.DataContext = _profileEditorViewModel;
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
            _dashboardViewModel = null!;
            _deploymentViewModel = null!;
            _appsViewModel = null!;
            _profileEditorViewModel = null!;
            _settingsViewModel = null!;
            _prerequisitesViewModel = null!;
        }
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Skip if initialization failed
        if (_dashboardViewModel == null) return;

        // Initialize Dashboard (default view)
        await InitializeDashboardAsync();

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
            Title = string.Format(Win11Forge.GUI.Resources.Resources.App_Title, "3.0.0");
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
        ProfileEditorViewControl.Visibility = Visibility.Collapsed;
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

            case 2: // Deployment
                DeploymentViewControl.Visibility = Visibility.Visible;
                _ = InitializeDeploymentAsync();
                break;

            case 3: // Apps
                AppsViewControl.Visibility = Visibility.Visible;
                _ = InitializeAppsAsync();
                break;

            case 4: // Profile Editor
                ProfileEditorViewControl.Visibility = Visibility.Visible;
                _ = InitializeProfileEditorNewAsync();
                break;

            case 5: // Settings
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

    private async Task InitializeProfileEditorNewAsync()
    {
        await _profileEditorViewModel.InitializeNewProfileAsync();
    }

    private async Task InitializeProfileEditorEditAsync(string profileName)
    {
        await _profileEditorViewModel.InitializeEditProfileAsync(profileName);
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
            NavigationListBox.SelectedIndex = viewIndex;
        }
    }

    /// <summary>
    /// Navigates to Profile Editor in Edit mode.
    /// </summary>
    public void NavigateToProfileEditor(string? profileName = null)
    {
        // Hide all views
        DashboardViewControl.Visibility = Visibility.Collapsed;
        PrerequisitesViewControl.Visibility = Visibility.Collapsed;
        DeploymentViewControl.Visibility = Visibility.Collapsed;
        AppsViewControl.Visibility = Visibility.Collapsed;
        SettingsViewControl.Visibility = Visibility.Collapsed;
        ProfileEditorViewControl.Visibility = Visibility.Visible;

        // Deselect navigation (since this might be triggered from a button, not nav)
        NavigationListBox.SelectedIndex = 4;

        if (string.IsNullOrEmpty(profileName))
        {
            _ = InitializeProfileEditorNewAsync();
        }
        else
        {
            _ = InitializeProfileEditorEditAsync(profileName);
        }
    }
}

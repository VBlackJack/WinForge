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

    private bool _dashboardInitialized;
    private bool _deploymentInitialized;
    private bool _appsInitialized;
    private bool _settingsInitialized;

    public MainWindow()
    {
        InitializeComponent();

        // Create shared services
        _powerShellBridge = new PowerShellBridge();
        _historyService = new DeploymentHistoryService();

        // Create ViewModels
        _dashboardViewModel = new DashboardViewModel(_powerShellBridge, _historyService);
        _deploymentViewModel = new DeploymentViewModel(_powerShellBridge, _historyService);
        _appsViewModel = new AppsViewModel(_powerShellBridge);
        _profileEditorViewModel = new ProfileEditorViewModel(_powerShellBridge);
        _settingsViewModel = new SettingsViewModel();

        // Wire up DataContexts
        DashboardViewControl.DataContext = _dashboardViewModel;
        DeploymentViewControl.DataContext = _deploymentViewModel;
        AppsViewControl.DataContext = _appsViewModel;
        ProfileEditorViewControl.DataContext = _profileEditorViewModel;
        SettingsViewControl.DataContext = _settingsViewModel;

        // Initialize on window load
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Initialize Dashboard (default view)
        await InitializeDashboardAsync();
    }

    private void NavigationListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selectedIndex = NavigationListBox.SelectedIndex;

        // Hide all views
        DashboardViewControl.Visibility = Visibility.Collapsed;
        DeploymentViewControl.Visibility = Visibility.Collapsed;
        AppsViewControl.Visibility = Visibility.Collapsed;
        ProfileEditorViewControl.Visibility = Visibility.Collapsed;
        SettingsViewControl.Visibility = Visibility.Collapsed;

        // Show selected view and initialize if needed
        switch (selectedIndex)
        {
            case 0: // Dashboard
                DashboardViewControl.Visibility = Visibility.Visible;
                _ = InitializeDashboardAsync();
                break;

            case 1: // Deployment
                DeploymentViewControl.Visibility = Visibility.Visible;
                _ = InitializeDeploymentAsync();
                break;

            case 2: // Apps
                AppsViewControl.Visibility = Visibility.Visible;
                _ = InitializeAppsAsync();
                break;

            case 3: // Profile Editor
                ProfileEditorViewControl.Visibility = Visibility.Visible;
                _ = InitializeProfileEditorNewAsync();
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
        DeploymentViewControl.Visibility = Visibility.Collapsed;
        AppsViewControl.Visibility = Visibility.Collapsed;
        SettingsViewControl.Visibility = Visibility.Collapsed;
        ProfileEditorViewControl.Visibility = Visibility.Visible;

        // Deselect navigation (since this might be triggered from a button, not nav)
        NavigationListBox.SelectedIndex = 3;

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

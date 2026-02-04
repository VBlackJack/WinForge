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

using System;
using System.Diagnostics;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Win11Forge.GUI.Models;
using Win11Forge.GUI.Services;

namespace Win11Forge.GUI.ViewModels;

/// <summary>
/// ViewModel for the Prerequisites view.
/// Displays detailed prerequisites status and allows installation.
/// </summary>
public partial class PrerequisitesViewModel : ViewModelBase
{
    private readonly IPowerShellBridge _powerShellBridge;
    private readonly IDialogService _dialogService;

    /// <summary>
    /// Prerequisites status.
    /// </summary>
    [ObservableProperty]
    private PrerequisitesStatus? _status;

    /// <summary>
    /// Whether prerequisites are being checked.
    /// </summary>
    [ObservableProperty]
    private bool _isChecking;

    /// <summary>
    /// Whether prerequisites are being installed.
    /// </summary>
    [ObservableProperty]
    private bool _isInstalling;

    /// <summary>
    /// Current installation progress message.
    /// </summary>
    [ObservableProperty]
    private string? _progressMessage;

    /// <summary>
    /// Installation log output.
    /// </summary>
    [ObservableProperty]
    private string _logOutput = string.Empty;

    /// <summary>
    /// Initializes a new instance of PrerequisitesViewModel.
    /// </summary>
    public PrerequisitesViewModel(IPowerShellBridge powerShellBridge, IDialogService dialogService)
    {
        _powerShellBridge = powerShellBridge;
        _dialogService = dialogService;
    }

    /// <inheritdoc/>
    public override async Task InitializeAsync()
    {
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            await CheckPrerequisitesAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Checks the status of all prerequisites.
    /// </summary>
    [RelayCommand]
    private async Task CheckPrerequisitesAsync()
    {
        IsChecking = true;
        try
        {
            Status = await _powerShellBridge.CheckPrerequisitesAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsChecking = false;
        }
    }

    /// <summary>
    /// Installs all missing prerequisites.
    /// </summary>
    [RelayCommand]
    private async Task InstallPrerequisitesAsync()
    {
        IsInstalling = true;
        ProgressMessage = Resources.Resources.Prerequisites_Starting;
        LogOutput = string.Empty;

        try
        {
            var success = await _powerShellBridge.InstallPrerequisitesAsync(msg =>
            {
                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    ProgressMessage = msg;
                    LogOutput += msg + Environment.NewLine;
                });
            });

            if (success)
            {
                await CheckPrerequisitesAsync();

                // Show restart dialog if some prerequisites still show as not installed
                // This happens because the current process doesn't have the updated PATH
                LogOutput += Environment.NewLine + Resources.Resources.Prerequisites_RestartRequired + Environment.NewLine;

                var shouldRestart = await _dialogService.ShowConfirmAsync(
                    Resources.Resources.Prerequisites_Complete,
                    Resources.Resources.Prerequisites_RestartRequired,
                    Resources.Resources.Prerequisites_RestartApp,
                    Resources.Resources.Prerequisites_DismissRestart);

                if (shouldRestart)
                {
                    RestartApplication();
                }
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            LogOutput += $"Exception: {ex.Message}" + Environment.NewLine;
        }
        finally
        {
            IsInstalling = false;
        }
    }

    /// <summary>
    /// Clears the log output.
    /// </summary>
    [RelayCommand]
    private void ClearLog()
    {
        LogOutput = string.Empty;
    }

    /// <summary>
    /// Restarts the application to apply environment changes.
    /// </summary>
    private void RestartApplication()
    {
        try
        {
            var exePath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exePath))
            {
                // Start new instance
                var startInfo = new ProcessStartInfo
                {
                    FileName = exePath,
                    UseShellExecute = true,
                    Verb = "runas" // Run as admin
                };
                Process.Start(startInfo);

                // Shutdown current instance
                Application.Current.Shutdown();
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }
}

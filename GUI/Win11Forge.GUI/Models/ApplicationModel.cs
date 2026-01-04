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

using CommunityToolkit.Mvvm.ComponentModel;

namespace Win11Forge.GUI.Models;

/// <summary>
/// Represents an application in the deployment profile.
/// </summary>
public partial class ApplicationModel : ObservableObject
{
    /// <summary>Unique identifier for the application.</summary>
    [ObservableProperty]
    private string _appId = string.Empty;

    /// <summary>Display name of the application.</summary>
    [ObservableProperty]
    private string _name = string.Empty;

    /// <summary>Application category (e.g., Browser, Utility).</summary>
    [ObservableProperty]
    private string _category = string.Empty;

    /// <summary>Description of the application.</summary>
    [ObservableProperty]
    private string _description = string.Empty;

    /// <summary>Installation priority (lower = installed first).</summary>
    [ObservableProperty]
    private int _priority;

    /// <summary>Whether this application is required.</summary>
    [ObservableProperty]
    private bool _isRequired;

    /// <summary>Current installation status.</summary>
    [ObservableProperty]
    private ApplicationStatus _status = ApplicationStatus.Pending;

    /// <summary>Error message if installation failed.</summary>
    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>Whether the application is selected for installation.</summary>
    [ObservableProperty]
    private bool _isSelected = true;

    /// <summary>Installation log output for this application.</summary>
    [ObservableProperty]
    private string _logOutput = string.Empty;

    /// <summary>Installation progress (0-100).</summary>
    [ObservableProperty]
    private double _progressValue;

    /// <summary>Status message displayed during installation.</summary>
    [ObservableProperty]
    private string _statusMessage = string.Empty;

    /// <summary>Available installation sources (e.g., "Winget, Chocolatey").</summary>
    [ObservableProperty]
    private string _sources = string.Empty;
}

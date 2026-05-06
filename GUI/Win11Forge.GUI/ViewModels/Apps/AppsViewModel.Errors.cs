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

using CommunityToolkit.Mvvm.Input;

namespace Win11Forge.GUI.ViewModels;

public partial class AppsViewModel
{
    /// <summary>
    /// Tracks the last operation type for retry functionality.
    /// </summary>
    private string _lastOperationType = string.Empty;

    /// <summary>
    /// Dismisses the current error message.
    /// </summary>
    [RelayCommand]
    private void DismissError()
    {
        ErrorMessage = null;
    }

    /// <summary>
    /// Retries the last failed operation.
    /// </summary>
    [RelayCommand]
    private async Task RetryLastOperationAsync()
    {
        ErrorMessage = null;

        switch (_lastOperationType)
        {
            case "scan":
                if (CanScan)
                    await ScanAsync();
                break;
            case "install":
                await InstallSelectedAsync();
                break;
            case "uninstall":
                await UninstallSelectedAsync();
                break;
            case "load":
                await InitializeAsync();
                break;
            default:
                await InitializeAsync();
                break;
        }
    }
}

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
using Wpf.Ui.Controls;

namespace Win11Forge.GUI.Controls;

/// <summary>
/// Onboarding dialog shown on first run.
/// </summary>
public partial class OnboardingDialog : UserControl
{
    public event EventHandler<bool>? Completed;

    public OnboardingDialog()
    {
        InitializeComponent();
    }

    private void GetStartedButton_Click(object sender, RoutedEventArgs e)
    {
        Completed?.Invoke(this, DontShowAgainCheckbox.IsChecked == true);
        // ContentDialog manages its own lifecycle
    }
}

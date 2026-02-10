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
using System.Windows.Media.Animation;

namespace Win11Forge.GUI.Views;

/// <summary>
/// Splash screen displayed during application startup.
/// Provides visual feedback while the application initializes.
/// </summary>
public partial class SplashScreen : Window
{
    /// <summary>
    /// Initializes a new instance of the SplashScreen.
    /// </summary>
    public SplashScreen()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Updates the status message displayed on the splash screen.
    /// </summary>
    /// <param name="message">The status message to display</param>
    public void UpdateStatus(string message)
    {
        Dispatcher.Invoke(() =>
        {
            StatusText.Text = message;
        });
    }

    /// <summary>
    /// Sets the version number displayed on the splash screen.
    /// </summary>
    /// <param name="version">The version string to display</param>
    public void SetVersion(string version)
    {
        Dispatcher.Invoke(() =>
        {
            VersionText.Text = $"v{version}";
        });
    }

    /// <summary>
    /// Closes the splash screen with a fade-out animation.
    /// </summary>
    public void CloseWithAnimation()
    {
        Dispatcher.Invoke(() =>
        {
            if (App.ReducedMotion)
            {
                Close();
                return;
            }

            var fadeOut = new DoubleAnimation
            {
                From = 1.0,
                To = 0.0,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            fadeOut.Completed += (s, e) => Close();
            BeginAnimation(OpacityProperty, fadeOut);
        });
    }
}

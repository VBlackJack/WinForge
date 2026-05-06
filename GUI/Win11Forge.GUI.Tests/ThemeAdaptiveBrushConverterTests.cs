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

using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Media;
using Win11Forge.GUI.Resources;

namespace Win11Forge.GUI.Tests;

/// <summary>
/// Tests for theme-aware brush conversion.
/// </summary>
[Collection("WpfApplication")]
public class ThemeAdaptiveBrushConverterTests
{
    [Fact]
    public void Convert_DraculaActiveAndAccentBrushAvailable_ReturnsAccentBrush()
    {
        RunOnStaThread(() =>
        {
            var app = Application.Current ?? new Application();
            var expectedBrush = new SolidColorBrush(Colors.MediumPurple);
            app.Resources["AccentBrush"] = expectedBrush;

            var themeService = new MockThemeService();
            themeService.ApplyTheme(ThemeNames.DraculaPro);
            var converter = new ThemeAdaptiveBrushConverter();

            var result = converter.Convert(
                themeService,
                typeof(Brush),
                null!,
                CultureInfo.InvariantCulture);

            Assert.Same(expectedBrush, result);
            app.Resources.Clear();
            app.Shutdown();
        });
    }

    private static void RunOnStaThread(Action action)
    {
        ExceptionDispatchInfo? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ExceptionDispatchInfo.Capture(ex);
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        exception?.Throw();
    }
}

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
using Win11Forge.GUI.Controls;
using Win11Forge.GUI.Tests.TestInfrastructure;

namespace Win11Forge.GUI.Tests;

/// <summary>
/// Tests for LoadingOverlay reduced-motion integration paths.
/// </summary>
[Collection("WpfApplication")]
public class LoadingOverlayTests
{
    [Fact]
    public void Show_WithReducedMotion_SetsVisibleAndOpacityImmediately()
    {
        WpfApplicationScope.RunOnStaThread(() =>
        {
            using WpfApplicationScope scope = WpfApplicationScope.Create();
            using IDisposable reducedMotion = ReducedMotionOverride(true);
            LoadingOverlay overlay = new LoadingOverlay { Opacity = 0 };

            overlay.Show("Loading", "Please wait");

            Assert.Equal(Visibility.Visible, overlay.Visibility);
            Assert.Equal(1, overlay.Opacity);
            Assert.True(overlay.IsIndeterminate);
            Assert.Equal("Loading", overlay.Message);
            Assert.Equal("Please wait", overlay.SubMessage);
        });
    }

    [Fact]
    public void ShowWithProgress_WithReducedMotion_SetsProgressAndOpacityImmediately()
    {
        WpfApplicationScope.RunOnStaThread(() =>
        {
            using WpfApplicationScope scope = WpfApplicationScope.Create();
            using IDisposable reducedMotion = ReducedMotionOverride(true);
            LoadingOverlay overlay = new LoadingOverlay { Opacity = 0 };

            overlay.ShowWithProgress("Installing", 42, "3 of 7");

            Assert.Equal(Visibility.Visible, overlay.Visibility);
            Assert.Equal(1, overlay.Opacity);
            Assert.False(overlay.IsIndeterminate);
            Assert.Equal(42, overlay.Progress);
            Assert.Equal("3 of 7", overlay.ProgressTextValue);
        });
    }

    [Fact]
    public void Hide_WithReducedMotion_CollapsesImmediately()
    {
        WpfApplicationScope.RunOnStaThread(() =>
        {
            using WpfApplicationScope scope = WpfApplicationScope.Create();
            using IDisposable reducedMotion = ReducedMotionOverride(true);
            LoadingOverlay overlay = new LoadingOverlay
            {
                Opacity = 1,
                Visibility = Visibility.Visible
            };

            overlay.Hide();

            Assert.Equal(Visibility.Collapsed, overlay.Visibility);
            Assert.Equal(1, overlay.Opacity);
        });
    }

    [Fact]
    public void Show_WithoutReducedMotion_DoesNotSetOpacityImmediately()
    {
        WpfApplicationScope.RunOnStaThread(() =>
        {
            using WpfApplicationScope scope = WpfApplicationScope.Create();
            using IDisposable reducedMotion = ReducedMotionOverride(false);
            LoadingOverlay overlay = new LoadingOverlay { Opacity = 0.42 };

            overlay.Show("Loading");

            Assert.Equal(Visibility.Visible, overlay.Visibility);
            Assert.NotEqual(1, overlay.Opacity);
        });
    }

    private static IDisposable ReducedMotionOverride(bool enabled)
    {
        DelegateDisposable appOverride = new DelegateDisposable(() => App.SetReducedMotionOverride(null));
        App.SetReducedMotionOverride(enabled);
        IDisposable helperOverride = AnimationHelperTests.AnimationHelperReducedMotionOverride(enabled);

        return new DelegateDisposable(() =>
        {
            helperOverride.Dispose();
            appOverride.Dispose();
        });
    }

    private sealed class DelegateDisposable(Action dispose) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            dispose();
            _disposed = true;
        }
    }
}

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
using WinForge.GUI.Tests.TestInfrastructure;
using SplashScreenView = WinForge.GUI.Views.SplashScreen;

namespace WinForge.GUI.Tests;

/// <summary>
/// Tests for the splash screen reduced-motion animation contract.
/// </summary>
[Collection("WpfApplication")]
public class SplashScreenTests
{
    [Fact]
    public void ApplyReducedMotionPreference_WithReducedMotionTrue_StopsRotationAndResetsAngle()
    {
        WpfApplicationScope.RunOnStaThread(() =>
        {
            using WpfApplicationScope scope = WpfApplicationScope.Create();
            using IDisposable reducedMotion = ReducedMotionOverride(true);
            SplashScreenView splash = new SplashScreenView();
            splash.IconRotateTransform.Angle = 42;

            splash.ApplyReducedMotionPreference();

            Assert.False(splash.IconRotateTransform.HasAnimatedProperties);
            Assert.Equal(0, splash.IconRotateTransform.Angle);
            splash.Close();
        });
    }

    [Fact]
    public void ApplyReducedMotionPreference_WithReducedMotionFalse_StartsForeverRotation()
    {
        WpfApplicationScope.RunOnStaThread(() =>
        {
            using WpfApplicationScope scope = WpfApplicationScope.Create();
            using IDisposable reducedMotion = ReducedMotionOverride(false);
            SplashScreenView splash = new SplashScreenView();

            splash.ApplyReducedMotionPreference();

            Assert.True(splash.IconRotateTransform.HasAnimatedProperties);
            splash.Close();
        });
    }

    [Fact]
    public void ApplyReducedMotionPreference_TogglingReducedMotion_StopsRunningAnimation()
    {
        WpfApplicationScope.RunOnStaThread(() =>
        {
            using WpfApplicationScope scope = WpfApplicationScope.Create();
            SplashScreenView splash = new SplashScreenView();

            using (ReducedMotionOverride(false))
            {
                splash.ApplyReducedMotionPreference();
                Assert.True(splash.IconRotateTransform.HasAnimatedProperties);
            }

            using (ReducedMotionOverride(true))
            {
                splash.ApplyReducedMotionPreference();
            }

            Assert.False(splash.IconRotateTransform.HasAnimatedProperties);
            Assert.Equal(0, splash.IconRotateTransform.Angle);
            splash.Close();
        });
    }

    [Fact]
    public void LoadedHandler_AppliesReducedMotionPreference()
    {
        WpfApplicationScope.RunOnStaThread(() =>
        {
            using WpfApplicationScope scope = WpfApplicationScope.Create();
            using IDisposable reducedMotion = ReducedMotionOverride(true);
            SplashScreenView splash = new SplashScreenView();
            splash.IconRotateTransform.Angle = 42;

            splash.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));

            Assert.False(splash.IconRotateTransform.HasAnimatedProperties);
            Assert.Equal(0, splash.IconRotateTransform.Angle);
            splash.Close();
        });
    }

    private static IDisposable ReducedMotionOverride(bool enabled)
    {
        App.SetReducedMotionOverride(enabled);
        return new DelegateDisposable(() => App.SetReducedMotionOverride(null));
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

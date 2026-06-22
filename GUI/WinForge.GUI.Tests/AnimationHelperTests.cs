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

using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using WinForge.GUI.Helpers;
using WinForge.GUI.Tests.TestInfrastructure;

namespace WinForge.GUI.Tests;

/// <summary>
/// Tests for reduced-motion-aware animation helper behavior.
/// </summary>
[Collection("WpfApplication")]
public class AnimationHelperTests
{
    private static readonly FieldInfo ReducedMotionField =
        typeof(AnimationHelper).GetField("_reducedMotion", BindingFlags.Static | BindingFlags.NonPublic)
        ?? throw new MissingFieldException(typeof(AnimationHelper).FullName, "_reducedMotion");

    [Fact]
    public void FadeIn_WithReducedMotion_SetsOpacityToOneImmediately()
    {
        WpfApplicationScope.RunOnStaThread(() =>
        {
            using IDisposable reducedMotion = AnimationHelperReducedMotionOverride(true);
            Border element = new Border { Opacity = 0 };

            AnimationHelper.FadeIn(element);

            Assert.Equal(1, element.Opacity);
        });
    }

    [Fact]
    public void FadeIn_WithoutReducedMotion_DoesNotSetOpacityImmediately()
    {
        WpfApplicationScope.RunOnStaThread(() =>
        {
            using IDisposable reducedMotion = AnimationHelperReducedMotionOverride(false);
            Border element = new Border { Opacity = 0.42 };

            AnimationHelper.FadeIn(element);

            Assert.NotEqual(1, element.Opacity);
        });
    }

    [Fact]
    public void FadeOut_WithReducedMotion_SetsOpacityToZeroImmediately()
    {
        WpfApplicationScope.RunOnStaThread(() =>
        {
            using IDisposable reducedMotion = AnimationHelperReducedMotionOverride(true);
            Border element = new Border { Opacity = 1 };

            AnimationHelper.FadeOut(element);

            Assert.Equal(0, element.Opacity);
        });
    }

    [Fact]
    public void FadeOut_WithoutReducedMotion_DoesNotSetOpacityImmediately()
    {
        WpfApplicationScope.RunOnStaThread(() =>
        {
            using IDisposable reducedMotion = AnimationHelperReducedMotionOverride(false);
            Border element = new Border { Opacity = 0.42 };

            AnimationHelper.FadeOut(element);

            Assert.NotEqual(0, element.Opacity);
        });
    }

    [Fact]
    public void CreateFadeAnimation_WithReducedMotion_CollapsesFromToAndDuration()
    {
        using IDisposable reducedMotion = AnimationHelperReducedMotionOverride(true);

        DoubleAnimation animation = AnimationHelper.CreateFadeAnimation(0, 1, 250);

        Assert.Equal(1, animation.From);
        Assert.Equal(1, animation.To);
        Assert.Equal(TimeSpan.Zero, animation.Duration.TimeSpan);
    }

    [Fact]
    public void CreateFadeAnimation_WithoutReducedMotion_PreservesFromToAndDuration()
    {
        using IDisposable reducedMotion = AnimationHelperReducedMotionOverride(false);

        DoubleAnimation animation = AnimationHelper.CreateFadeAnimation(0, 1, 250);

        Assert.Equal(0, animation.From);
        Assert.Equal(1, animation.To);
        Assert.Equal(TimeSpan.FromMilliseconds(250), animation.Duration.TimeSpan);
    }

    [Fact]
    public void CreateSlideAnimation_WithReducedMotion_CollapsesFromToAndDuration()
    {
        using IDisposable reducedMotion = AnimationHelperReducedMotionOverride(true);
        Thickness from = new Thickness(0, 15, 0, 0);
        Thickness to = new Thickness(0);

        ThicknessAnimation animation = AnimationHelper.CreateSlideAnimation(from, to, 250);

        Assert.Equal(to, animation.From);
        Assert.Equal(to, animation.To);
        Assert.Equal(TimeSpan.Zero, animation.Duration.TimeSpan);
    }

    [Fact]
    public void CreateSlideAnimation_WithoutReducedMotion_PreservesFromToAndDuration()
    {
        using IDisposable reducedMotion = AnimationHelperReducedMotionOverride(false);
        Thickness from = new Thickness(0, 15, 0, 0);
        Thickness to = new Thickness(0);

        ThicknessAnimation animation = AnimationHelper.CreateSlideAnimation(from, to, 250);

        Assert.Equal(from, animation.From);
        Assert.Equal(to, animation.To);
        Assert.Equal(TimeSpan.FromMilliseconds(250), animation.Duration.TimeSpan);
    }

    [Fact]
    public void GetDuration_WithReducedMotion_ReturnsZero()
    {
        using IDisposable reducedMotion = AnimationHelperReducedMotionOverride(true);

        Duration duration = AnimationHelper.GetDuration(new Duration(TimeSpan.FromMilliseconds(300)));
        Duration durationFromMilliseconds = AnimationHelper.GetDuration(300);

        Assert.Equal(TimeSpan.Zero, duration.TimeSpan);
        Assert.Equal(TimeSpan.Zero, durationFromMilliseconds.TimeSpan);
    }

    [Fact]
    public void GetDuration_WithoutReducedMotion_ReturnsInput()
    {
        using IDisposable reducedMotion = AnimationHelperReducedMotionOverride(false);

        Duration duration = AnimationHelper.GetDuration(new Duration(TimeSpan.FromMilliseconds(300)));
        Duration durationFromMilliseconds = AnimationHelper.GetDuration(250);

        Assert.Equal(TimeSpan.FromMilliseconds(300), duration.TimeSpan);
        Assert.Equal(TimeSpan.FromMilliseconds(250), durationFromMilliseconds.TimeSpan);
    }

    [Fact]
    public void GetTimeSpan_WithReducedMotion_ReturnsZero()
    {
        using IDisposable reducedMotion = AnimationHelperReducedMotionOverride(true);

        TimeSpan timeSpan = AnimationHelper.GetTimeSpan(TimeSpan.FromMilliseconds(300));

        Assert.Equal(TimeSpan.Zero, timeSpan);
    }

    [Fact]
    public void GetTimeSpan_WithoutReducedMotion_ReturnsInput()
    {
        using IDisposable reducedMotion = AnimationHelperReducedMotionOverride(false);
        TimeSpan expected = TimeSpan.FromMilliseconds(300);

        TimeSpan timeSpan = AnimationHelper.GetTimeSpan(expected);

        Assert.Equal(expected, timeSpan);
    }

    internal static IDisposable AnimationHelperReducedMotionOverride(bool enabled)
    {
        object? original = ReducedMotionField.GetValue(null);
        ReducedMotionField.SetValue(null, enabled);
        return new DelegateDisposable(() => ReducedMotionField.SetValue(null, original));
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

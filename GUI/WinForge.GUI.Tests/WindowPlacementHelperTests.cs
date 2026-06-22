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
using Win11Forge.GUI.Helpers;
using Win11Forge.GUI.Services;

namespace Win11Forge.GUI.Tests;

public sealed class WindowPlacementHelperTests
{
    [Fact]
    public void CalculatePlacement_NoSavedPlacement_UsesErgonomicDefaultWhenItFits()
    {
        WindowPlacementDecision decision = WindowPlacementHelper.CalculatePlacement(
            savedPlacement: null,
            workAreas: [new Rect(0, 0, 1920, 1032)],
            defaultSize: new Size(1680, 960),
            minimumSize: new Size(900, 640));

        Assert.Equal(WindowState.Normal, decision.WindowState);
        Assert.Equal(1680, decision.Bounds.Width);
        Assert.Equal(960, decision.Bounds.Height);
        Assert.Equal(120, decision.Bounds.Left);
        Assert.Equal(36, decision.Bounds.Top);
    }

    [Fact]
    public void CalculatePlacement_NoSavedPlacement_ClampsToSmallScreenWorkArea()
    {
        Rect workArea = new Rect(0, 0, 1366, 720);

        WindowPlacementDecision decision = WindowPlacementHelper.CalculatePlacement(
            savedPlacement: null,
            workAreas: [workArea],
            defaultSize: new Size(1680, 960),
            minimumSize: new Size(900, 640));

        Assert.Equal(WindowState.Normal, decision.WindowState);
        Assert.Equal(1318, decision.Bounds.Width);
        Assert.Equal(672, decision.Bounds.Height);
        Assert.True(workArea.Contains(decision.Bounds));
    }

    [Fact]
    public void CalculatePlacement_NegativeLeftOnConnectedSecondary_IsPreserved()
    {
        WindowPlacementSettings placement = new WindowPlacementSettings
        {
            Left = -1500,
            Top = 100,
            Width = 1200,
            Height = 700,
            WindowState = "Normal"
        };

        WindowPlacementDecision decision = WindowPlacementHelper.CalculatePlacement(
            placement,
            [
                new Rect(0, 0, 1920, 1032),
                new Rect(-1600, 0, 1600, 900)
            ],
            defaultSize: new Size(1680, 960),
            minimumSize: new Size(900, 640));

        Assert.Equal(WindowState.Normal, decision.WindowState);
        Assert.Equal(-1500, decision.Bounds.Left);
        Assert.Equal(100, decision.Bounds.Top);
        Assert.Equal(1200, decision.Bounds.Width);
        Assert.Equal(700, decision.Bounds.Height);
    }

    [Fact]
    public void CalculatePlacement_OffscreenSavedBounds_FallsBackVisibleAndPreservesMaximized()
    {
        WindowPlacementSettings placement = new WindowPlacementSettings
        {
            Left = 5000,
            Top = 120,
            Width = 1200,
            Height = 700,
            WindowState = "Maximized"
        };

        WindowPlacementDecision decision = WindowPlacementHelper.CalculatePlacement(
            placement,
            [new Rect(0, 0, 1920, 1032)],
            defaultSize: new Size(1680, 960),
            minimumSize: new Size(900, 640));

        Assert.Equal(WindowState.Maximized, decision.WindowState);
        Assert.Equal(1680, decision.Bounds.Width);
        Assert.Equal(960, decision.Bounds.Height);
        Assert.Equal(120, decision.Bounds.Left);
        Assert.Equal(36, decision.Bounds.Top);
    }

    [Fact]
    public void CalculatePlacement_OversizedSavedBounds_ClampsToCurrentWorkArea()
    {
        Rect workArea = new Rect(0, 0, 1366, 720);
        WindowPlacementSettings placement = new WindowPlacementSettings
        {
            Left = 0,
            Top = 0,
            Width = 3000,
            Height = 2000,
            WindowState = "Normal"
        };

        WindowPlacementDecision decision = WindowPlacementHelper.CalculatePlacement(
            placement,
            [workArea],
            defaultSize: new Size(1680, 960),
            minimumSize: new Size(900, 640));

        Assert.Equal(WindowState.Normal, decision.WindowState);
        Assert.Equal(1318, decision.Bounds.Width);
        Assert.Equal(672, decision.Bounds.Height);
        Assert.True(workArea.Contains(decision.Bounds));
    }
}

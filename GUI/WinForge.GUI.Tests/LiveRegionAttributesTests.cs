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

using System.IO;
using System.Text.RegularExpressions;

namespace WinForge.GUI.Tests;

/// <summary>
/// Guards the hidden live region pattern used for screen-reader announcements.
/// </summary>
public class LiveRegionAttributesTests
{
    [Fact]
    public void ScreenReaderLiveRegion_IsNotCollapsed()
    {
        string element = ExtractScreenReaderLiveRegionElement();

        Assert.DoesNotContain(@"Visibility=""Collapsed""", element, StringComparison.Ordinal);
        Assert.DoesNotContain(@"Visibility=""Hidden""", element, StringComparison.Ordinal);
    }

    [Fact]
    public void ScreenReaderLiveRegion_HasZeroSize()
    {
        string element = ExtractScreenReaderLiveRegionElement();

        Assert.Contains(@"Width=""0""", element, StringComparison.Ordinal);
        Assert.Contains(@"Height=""0""", element, StringComparison.Ordinal);
    }

    [Fact]
    public void ScreenReaderLiveRegion_IsInvisibleWithoutLeavingAutomationTree()
    {
        string element = ExtractScreenReaderLiveRegionElement();

        Assert.Contains(@"Opacity=""0""", element, StringComparison.Ordinal);
        Assert.Contains(@"IsHitTestVisible=""False""", element, StringComparison.Ordinal);
        Assert.Contains(@"Focusable=""False""", element, StringComparison.Ordinal);
    }

    [Fact]
    public void ScreenReaderLiveRegion_PreservesPoliteLiveSetting()
    {
        string element = ExtractScreenReaderLiveRegionElement();

        Assert.Contains(@"AutomationProperties.LiveSetting=""Polite""", element, StringComparison.Ordinal);
    }

    [Fact]
    public void ScreenReaderLiveRegion_PreservesAccessibleName()
    {
        string element = ExtractScreenReaderLiveRegionElement();

        Assert.Contains(@"AutomationProperties.Name=""{loc:Loc Accessibility_LiveRegion}""", element, StringComparison.Ordinal);
    }

    private static string ExtractScreenReaderLiveRegionElement()
    {
        string content = File.ReadAllText(FindRepoFile("GUI", "WinForge.GUI", "MainWindow.xaml"));
        Match match = Regex.Match(
            content,
            @"<TextBlock\s+x:Name=""ScreenReaderLiveRegion""[\s\S]*?/>",
            RegexOptions.CultureInvariant);

        Assert.True(match.Success, "ScreenReaderLiveRegion TextBlock was not found in MainWindow.xaml.");
        return match.Value;
    }

    private static string FindRepoFile(params string[] relativeParts)
    {
        DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            string candidate = Path.Combine([directory.FullName, .. relativeParts]);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Could not locate {Path.Combine(relativeParts)} from {AppContext.BaseDirectory}.");
    }
}

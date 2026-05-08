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
using System.IO;
using System.Windows;
using System.Xml.Linq;
using Win11Forge.GUI.Resources;

namespace Win11Forge.GUI.Tests;

/// <summary>
/// Tests for visual design tokens shared across primary surfaces.
/// </summary>
public class VisualDesignTokenTests
{
    private static readonly string[] PrimaryPageViews =
    [
        "DashboardView.xaml",
        "AppsView.xaml",
        "AppCatalogView.xaml",
        "DeploymentView.xaml",
        "PrerequisitesView.xaml",
        "LogsView.xaml",
        "SettingsView.xaml"
    ];

    [Fact]
    public void AppXaml_DefinesPageTypographyTokens()
    {
        var appXaml = XDocument.Load(FindRepoFile("GUI", "Win11Forge.GUI", "App.xaml"));
        var styles = appXaml.Descendants()
            .Where(element => element.Name.LocalName == "Style")
            .ToList();

        AssertNamedStyleSetter(styles, "PageTitleTextStyle", "FontSize", "28");
        AssertNamedStyleSetter(styles, "PageTitleTextStyle", "FontWeight", "SemiBold");
        AssertNamedStyleSetter(styles, "PageTitleTextStyle", "Foreground", "{DynamicResource TextFillColorPrimaryBrush}");
        AssertNamedStyleSetter(styles, "PageSubtitleTextStyle", "FontSize", "14");
        AssertNamedStyleSetter(styles, "PageSubtitleTextStyle", "Foreground", "{DynamicResource TextFillColorSecondaryBrush}");
    }

    [Fact]
    public void PageAutomationTitles_UsePageTitleStyleWithoutInlineTypography()
    {
        foreach (var viewFileName in PrimaryPageViews)
        {
            var viewXaml = XDocument.Load(FindRepoFile("GUI", "Win11Forge.GUI", "Views", viewFileName));
            var pageTitleTextBlocks = viewXaml.Descendants()
                .Where(element =>
                    element.Name.LocalName == "TextBlock"
                    && element.Attributes().Any(attribute =>
                        IsAutomationIdAttribute(attribute)
                        && attribute.Value.StartsWith("Page", StringComparison.Ordinal)))
                .ToList();

            Assert.NotEmpty(pageTitleTextBlocks);

            foreach (var textBlock in pageTitleTextBlocks)
            {
                Assert.Equal("{StaticResource PageTitleTextStyle}", textBlock.Attribute("Style")?.Value);
                Assert.Null(textBlock.Attribute("FontSize"));
                Assert.Null(textBlock.Attribute("FontWeight"));
            }
        }
    }

    [Fact]
    public void SourceBadges_UseSharedStylesInAppsAndCatalog()
    {
        var appXaml = XDocument.Load(FindRepoFile("GUI", "Win11Forge.GUI", "App.xaml"));
        var styleKeys = appXaml.Descendants()
            .Where(element => element.Name.LocalName == "Style")
            .Select(element => element.Attribute(XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value)
            .Where(key => key is not null)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var expectedStyle in SourceBadgeStyles())
        {
            Assert.Contains(expectedStyle, styleKeys);
        }

        var appsXaml = File.ReadAllText(FindRepoFile("GUI", "Win11Forge.GUI", "Views", "AppsView.xaml"));
        var catalogXaml = File.ReadAllText(FindRepoFile("GUI", "Win11Forge.GUI", "Views", "AppCatalogView.xaml"));

        foreach (var expectedStyle in SourceBadgeStyles().Where(style => style != "SourceBadgeStyle" && style != "SourceBadgeTextStyle"))
        {
            Assert.Contains(expectedStyle, appsXaml, StringComparison.Ordinal);
            Assert.Contains(expectedStyle, catalogXaml, StringComparison.Ordinal);
        }

        Assert.Contains("SourceStringToVisibilityConverter", appsXaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Text=\"{Binding Sources}\"", appsXaml, StringComparison.Ordinal);
    }

    [Fact]
    public void DataGridStyles_UseSubtleHorizontalGridlines()
    {
        var appXaml = XDocument.Load(FindRepoFile("GUI", "Win11Forge.GUI", "App.xaml"));
        var styles = appXaml.Descendants()
            .Where(element => element.Name.LocalName == "Style")
            .ToList();

        AssertImplicitStyleSetter(styles, "DataGrid", "BorderBrush", "{DynamicResource SubtleDataGridLineBrush}");
        AssertImplicitStyleSetter(styles, "DataGrid", "GridLinesVisibility", "Horizontal");
        AssertImplicitStyleSetter(styles, "DataGrid", "HorizontalGridLinesBrush", "{DynamicResource SubtleDataGridLineBrush}");
        AssertImplicitStyleSetter(styles, "DataGrid", "VerticalGridLinesBrush", "Transparent");
        AssertImplicitStyleSetter(styles, "DataGridColumnHeader", "BorderBrush", "{DynamicResource SubtleDataGridLineBrush}");
        AssertImplicitStyleSetter(styles, "DataGridColumnHeader", "BorderThickness", "0,0,0,1");
    }

    [Fact]
    public void SourceStringToVisibilityConverter_MatchesCommaSeparatedTokens()
    {
        var converter = new SourceStringToVisibilityConverter();

        Assert.Equal(Visibility.Visible, converter.Convert("Winget, Chocolatey", typeof(Visibility), "Winget", CultureInfo.InvariantCulture));
        Assert.Equal(Visibility.Visible, converter.Convert("Winget, Chocolatey", typeof(Visibility), "Chocolatey", CultureInfo.InvariantCulture));
        Assert.Equal(Visibility.Visible, converter.Convert("Direct", typeof(Visibility), "direct", CultureInfo.InvariantCulture));
        Assert.Equal(Visibility.Collapsed, converter.Convert("Store", typeof(Visibility), "Winget", CultureInfo.InvariantCulture));
        Assert.Equal(Visibility.Collapsed, converter.Convert(string.Empty, typeof(Visibility), "Winget", CultureInfo.InvariantCulture));
        Assert.Equal(Visibility.Collapsed, converter.Convert(null!, typeof(Visibility), "Winget", CultureInfo.InvariantCulture));
        Assert.Equal(Visibility.Collapsed, converter.Convert("Winget", typeof(Visibility), null!, CultureInfo.InvariantCulture));
    }

    private static IEnumerable<string> SourceBadgeStyles()
    {
        yield return "SourceBadgeStyle";
        yield return "SourceBadgeTextStyle";
        yield return "SourceWingetBadgeStyle";
        yield return "SourceChocolateyBadgeStyle";
        yield return "SourceStoreBadgeStyle";
        yield return "SourceDirectBadgeStyle";
    }

    private static bool IsAutomationIdAttribute(XAttribute attribute)
    {
        return string.Equals(attribute.Name.LocalName, "AutomationId", StringComparison.Ordinal)
            || string.Equals(attribute.Name.LocalName, "AutomationProperties.AutomationId", StringComparison.Ordinal);
    }

    private static void AssertImplicitStyleSetter(
        IReadOnlyCollection<XElement> styles,
        string targetType,
        string property,
        string expectedValue)
    {
        var style = styles.Single(element =>
            string.Equals(element.Attribute("TargetType")?.Value, targetType, StringComparison.Ordinal)
            && element.Attribute(XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml")) is null);
        var setter = style.Elements()
            .SingleOrDefault(element =>
                element.Name.LocalName == "Setter"
                && string.Equals(element.Attribute("Property")?.Value, property, StringComparison.Ordinal));

        Assert.NotNull(setter);
        Assert.Equal(expectedValue, setter.Attribute("Value")?.Value);
    }

    private static void AssertNamedStyleSetter(
        IReadOnlyCollection<XElement> styles,
        string styleKey,
        string property,
        string expectedValue)
    {
        var style = styles.Single(element =>
            string.Equals(
                element.Attribute(XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value,
                styleKey,
                StringComparison.Ordinal));
        var setter = style.Elements()
            .SingleOrDefault(element =>
                element.Name.LocalName == "Setter"
                && string.Equals(element.Attribute("Property")?.Value, property, StringComparison.Ordinal));

        Assert.NotNull(setter);
        Assert.Equal(expectedValue, setter.Attribute("Value")?.Value);
    }

    private static string FindRepoFile(params string[] relativeParts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine([directory.FullName, .. relativeParts]);
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

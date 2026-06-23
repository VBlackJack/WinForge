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
using WinForge.GUI.Resources;

namespace WinForge.GUI.Tests;

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
        XDocument appXaml = XDocument.Load(FindRepoFile("GUI", "WinForge.GUI", "App.xaml"));
        List<XElement> styles = appXaml.Descendants()
            .Where(element => element.Name.LocalName == "Style")
            .ToList();

        AssertResourceValue(appXaml, "FontSizeMicro", "11");
        AssertResourceValue(appXaml, "FontSizeCaption", "12");
        AssertResourceValue(appXaml, "FontSizeBody", "14");
        AssertResourceValue(appXaml, "FontSizeBodyLarge", "16");
        AssertResourceValue(appXaml, "FontSizeLarge", "18");
        AssertResourceValue(appXaml, "FontSizeSubtitle", "20");
        AssertResourceValue(appXaml, "FontSizeTitle", "24");
        AssertResourceValue(appXaml, "FontSizeHeader", "28");
        AssertResourceValue(appXaml, "FontSizeDisplay", "32");
        AssertResourceValue(appXaml, "FontSizeHero", "48");

        AssertNamedStyleSetter(styles, "PageTitleTextStyle", "FontSize", "{StaticResource FontSizeHeader}");
        AssertNamedStyleSetter(styles, "PageTitleTextStyle", "FontWeight", "SemiBold");
        AssertNamedStyleSetter(styles, "PageTitleTextStyle", "Foreground", "{DynamicResource TextFillColorPrimaryBrush}");
        AssertNamedStyleSetter(styles, "PageSubtitleTextStyle", "FontSize", "{StaticResource FontSizeBody}");
        AssertNamedStyleSetter(styles, "PageSubtitleTextStyle", "Foreground", "{DynamicResource TextFillColorSecondaryBrush}");
        AssertNamedStyleSetter(styles, "SourceBadgeTextStyle", "FontSize", "{StaticResource FontSizeMicro}");
    }

    [Fact]
    public void AppXaml_DefinesCornerRadiusTokens()
    {
        XDocument appXaml = XDocument.Load(FindRepoFile("GUI", "WinForge.GUI", "App.xaml"));
        List<XElement> styles = appXaml.Descendants()
            .Where(element => element.Name.LocalName == "Style")
            .ToList();

        AssertResourceValue(appXaml, "RadiusSmall", "4");
        AssertResourceValue(appXaml, "RadiusMedium", "8");
        AssertResourceValue(appXaml, "RadiusLarge", "12");
        AssertResourceValue(appXaml, "RadiusXLarge", "16");

        AssertNamedStyleSetter(styles, "SourceBadgeStyle", "CornerRadius", "{StaticResource RadiusSmall}");
    }

    [Fact]
    public void SpacingDictionary_DefinesWinForgeLocalDensityTokens()
    {
        XDocument appXaml = XDocument.Load(FindRepoFile("GUI", "WinForge.GUI", "App.xaml"));
        List<string?> mergedDictionarySources = appXaml.Descendants()
            .Where(element => element.Name.LocalName == "ResourceDictionary")
            .Select(element => element.Attribute("Source")?.Value)
            .ToList();
        XDocument spacingXaml = XDocument.Load(FindRepoFile("GUI", "WinForge.GUI", "Resources", "Spacing.xaml"));

        Assert.Contains("Resources/Spacing.xaml", mergedDictionarySources);
        AssertResourceValue(spacingXaml, "HeroPadding", "24");
        AssertResourceValue(spacingXaml, "SectionPadding", "16");
        AssertResourceValue(spacingXaml, "SectionGap", "0,0,0,16");
        AssertResourceValue(spacingXaml, "BlockGap", "0,0,0,12");
    }

    [Fact]
    public void PageAutomationTitles_UsePageTitleStyleWithoutInlineTypography()
    {
        foreach (string viewFileName in PrimaryPageViews)
        {
            XDocument viewXaml = XDocument.Load(FindRepoFile("GUI", "WinForge.GUI", "Views", viewFileName));
            List<XElement> pageTitleTextBlocks = viewXaml.Descendants()
                .Where(element =>
                    element.Name.LocalName == "TextBlock"
                    && element.Attributes().Any(attribute =>
                        IsAutomationIdAttribute(attribute)
                        && attribute.Value.StartsWith("Page", StringComparison.Ordinal)))
                .ToList();

            Assert.NotEmpty(pageTitleTextBlocks);

            foreach (XElement? textBlock in pageTitleTextBlocks)
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
        XDocument appXaml = XDocument.Load(FindRepoFile("GUI", "WinForge.GUI", "App.xaml"));
        HashSet<string?> styleKeys = appXaml.Descendants()
            .Where(element => element.Name.LocalName == "Style")
            .Select(element => element.Attribute(XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value)
            .Where(key => key is not null)
            .ToHashSet(StringComparer.Ordinal);

        foreach (string expectedStyle in SourceBadgeStyles())
        {
            Assert.Contains(expectedStyle, styleKeys);
        }

        string appsXaml = File.ReadAllText(FindRepoFile("GUI", "WinForge.GUI", "Views", "AppsView.xaml"));
        string catalogXaml = File.ReadAllText(FindRepoFile("GUI", "WinForge.GUI", "Views", "AppCatalogView.xaml"));

        foreach (string? expectedStyle in SourceBadgeStyles().Where(style => style != "SourceBadgeStyle" && style != "SourceBadgeTextStyle"))
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
        XDocument appXaml = XDocument.Load(FindRepoFile("GUI", "WinForge.GUI", "App.xaml"));
        List<XElement> styles = appXaml.Descendants()
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
        SourceStringToVisibilityConverter converter = new SourceStringToVisibilityConverter();

        Assert.Equal(Visibility.Visible, converter.Convert("Winget, Chocolatey", typeof(Visibility), "Winget", CultureInfo.InvariantCulture));
        Assert.Equal(Visibility.Visible, converter.Convert("Winget, Chocolatey", typeof(Visibility), "Chocolatey", CultureInfo.InvariantCulture));
        Assert.Equal(Visibility.Visible, converter.Convert("Direct", typeof(Visibility), "direct", CultureInfo.InvariantCulture));
        Assert.Equal(Visibility.Collapsed, converter.Convert("Store", typeof(Visibility), "Winget", CultureInfo.InvariantCulture));
        Assert.Equal(Visibility.Collapsed, converter.Convert(string.Empty, typeof(Visibility), "Winget", CultureInfo.InvariantCulture));
        Assert.Equal(Visibility.Collapsed, converter.Convert(null!, typeof(Visibility), "Winget", CultureInfo.InvariantCulture));
        Assert.Equal(Visibility.Collapsed, converter.Convert("Winget", typeof(Visibility), null!, CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData("Views/AppCatalogView.xaml")]
    [InlineData("Views/AppsView.xaml")]
    [InlineData("Views/DashboardView.xaml")]
    [InlineData("Views/DeploymentView.xaml")]
    [InlineData("Views/LogsView.xaml")]
    [InlineData("Views/PrerequisitesView.xaml")]
    [InlineData("Views/SettingsView.xaml")]
    public void CardBorders_UseCardPaddingToken_AcrossAuditedViews(string relativePath)
    {
        string viewPath = FindRepoFile("GUI", "WinForge.GUI", relativePath);
        string viewXaml = File.ReadAllText(viewPath);
        XDocument viewDoc = XDocument.Load(viewPath);

        List<string?> literalCardPaddings = viewDoc.Descendants()
            .Where(element =>
                element.Name.LocalName == "Border"
                && element.Attribute("Padding")?.Value is "16" or "20")
            .Select(element => element.Attribute("Padding")?.Value)
            .ToList();

        Assert.Empty(literalCardPaddings);
        Assert.True(
            viewXaml.Contains("Padding=\"{StaticResource CardPadding}\"", StringComparison.Ordinal)
            || viewXaml.Contains("Style=\"{StaticResource CardBorderStyle}\"", StringComparison.Ordinal)
            || viewXaml.Contains("Style=\"{StaticResource SectionCardStyle}\"", StringComparison.Ordinal),
            $"{relativePath} should use shared card padding or a shared card style.");
    }

    [Fact]
    public void ButtonTaxonomy_DefinesStandardAndCompactVariants()
    {
        XDocument appXaml = XDocument.Load(FindRepoFile("GUI", "WinForge.GUI", "App.xaml"));
        List<XElement> styles = appXaml.Descendants()
            .Where(element => element.Name.LocalName == "Style")
            .ToList();

        AssertNamedStyleSetter(styles, "PrimaryButton", "MinHeight", "{StaticResource ButtonMinHeightStandard}");
        AssertNamedStyleSetter(styles, "PrimaryButton", "VerticalAlignment", "Center");
        AssertNamedStyleSetter(styles, "SecondaryButton", "MinHeight", "{StaticResource ButtonMinHeightStandard}");
        AssertNamedStyleSetter(styles, "SecondaryButton", "VerticalAlignment", "Center");
        AssertNamedStyleSetter(styles, "DestructiveSolidButton", "MinHeight", "{StaticResource ButtonMinHeightStandard}");
        AssertNamedStyleSetter(styles, "CompactPrimaryButton", "VerticalAlignment", "Center");
        AssertNamedStyleSetter(styles, "CompactSecondaryButton", "VerticalAlignment", "Center");
        AssertNamedStyleSetter(styles, "CompactSecondaryButton", "Padding", "{StaticResource ButtonPaddingCompact}");
        AssertNamedStyleSetter(styles, "CompactTransparentButton", "Appearance", "Transparent");
        AssertNamedStyleSetter(styles, "CompactTransparentButton", "VerticalAlignment", "Center");
        AssertNamedStyleSetter(styles, "CompactSecondaryDropDownButton", "Appearance", "Secondary");
        AssertNamedStyleSetter(styles, "CompactSecondaryDropDownButton", "VerticalAlignment", "Center");
        AssertNamedStyleSetter(styles, "CompactSecondaryDropDownButton", "CornerRadius", "{StaticResource ButtonCornerRadius}");
        AssertNamedStyleSetter(styles, "QuickActionButton", "HorizontalContentAlignment", "Left");
        AssertNamedStyleSetter(styles, "IconButton", "MinWidth", "{StaticResource ButtonMinHeightTouch}");
        AssertNamedStyleSetter(styles, "IconDropDownButton", "Appearance", "Transparent");
        AssertNamedStyleSetter(styles, "IconDropDownButton", "MinWidth", "{StaticResource ButtonMinHeightTouch}");
        AssertNamedStyleSetter(styles, "IconDropDownButton", "CornerRadius", "{StaticResource ButtonCornerRadius}");
    }

    [Fact]
    public void AppsColumnVisibilityDropDown_UsesSharedIconDropDownButtonStyle()
    {
        XDocument appsDoc = XDocument.Load(FindRepoFile("GUI", "WinForge.GUI", "Views", "AppsView.xaml"));

        XElement columnVisibilityButton = appsDoc.Descendants()
            .Single(element =>
                element.Name.LocalName == "DropDownButton"
                && string.Equals(
                    element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value,
                    "ColumnVisibilityButton",
                    StringComparison.Ordinal));

        Assert.Equal("{StaticResource IconDropDownButton}", columnVisibilityButton.Attribute("Style")?.Value);
        Assert.Null(columnVisibilityButton.Attribute("Appearance"));
        Assert.Null(columnVisibilityButton.Attribute("MinWidth"));
        Assert.Null(columnVisibilityButton.Attribute("MinHeight"));
        Assert.Null(columnVisibilityButton.Attribute("Padding"));
    }

    [Fact]
    public void AppsSelectionActionBar_UsesStatusAwarePrimaryAction()
    {
        string appsXaml = File.ReadAllText(FindRepoFile("GUI", "WinForge.GUI", "Views", "AppsView.xaml"));

        string selectionActionBar = ExtractXamlSection(
            appsXaml,
            "<!-- Selection Action Bar -->",
            "<!-- Batch Progress Panel");

        Assert.Contains("Command=\"{Binding InstallSelectedCommand}\"", selectionActionBar, StringComparison.Ordinal);
        Assert.Contains("SelectedPrimaryActionText", selectionActionBar, StringComparison.Ordinal);
        Assert.DoesNotContain("UpdateSelectedCommand", selectionActionBar, StringComparison.Ordinal);
        Assert.DoesNotContain("StatusFilterOption.HasUpdates", selectionActionBar, StringComparison.Ordinal);
    }

    [Fact]
    public void DeploymentCompletedSurface_StacksResultBannerAboveApplicationList()
    {
        string deploymentPath = FindRepoFile("GUI", "WinForge.GUI", "Views", "DeploymentView.xaml");
        string deploymentXaml = File.ReadAllText(deploymentPath);
        XDocument deploymentDoc = XDocument.Load(deploymentPath);

        Assert.DoesNotContain("0,80,0,0", deploymentXaml, StringComparison.Ordinal);

        XElement resultBanner = deploymentDoc.Descendants()
            .Single(element =>
                element.Name.LocalName == "Border"
                && string.Equals(
                    element.Attribute("AutomationProperties.LiveSetting")?.Value,
                    "Polite",
                    StringComparison.Ordinal));
        XElement applicationList = deploymentDoc.Descendants()
            .Single(element =>
                element.Name.LocalName == "Border"
                && element.Descendants().Any(child =>
                    child.Name.LocalName == "DataGrid"
                    && string.Equals(
                        child.Attribute("AutomationProperties.Name")?.Value,
                        "{loc:Loc A11y_Deployment_AppList}",
                        StringComparison.Ordinal)));

        Assert.Equal("0", resultBanner.Attribute("Grid.Row")?.Value);
        Assert.Equal("1", applicationList.Attribute("Grid.Row")?.Value);
    }

    [Fact]
    public void DeploymentResultTitle_UsesSemanticForegroundForEveryState()
    {
        string deploymentXaml = File.ReadAllText(FindRepoFile("GUI", "WinForge.GUI", "Views", "DeploymentView.xaml"));

        Assert.Contains(
            "<Setter Property=\"Foreground\" Value=\"{DynamicResource SuccessTextBrush}\"/>",
            deploymentXaml,
            StringComparison.Ordinal);
        Assert.Contains(
            "<Setter Property=\"Foreground\" Value=\"{DynamicResource WarningTextBrush}\"/>",
            deploymentXaml,
            StringComparison.Ordinal);
        Assert.Contains(
            "<Setter Property=\"Foreground\" Value=\"{DynamicResource ErrorTextBrush}\"/>",
            deploymentXaml,
            StringComparison.Ordinal);
        Assert.Contains(
            "<Setter Property=\"Foreground\" Value=\"{DynamicResource TextFillColorPrimaryBrush}\"/>",
            deploymentXaml,
            StringComparison.Ordinal);
    }

    [Fact]
    public void AppThemeFallback_UsesThemeForgeBridgeInsteadOfHardcodedPalette()
    {
        string appSource = File.ReadAllText(FindRepoFile("GUI", "WinForge.GUI", "App.xaml.cs"));

        Assert.Contains("ThemeService.ClearPaletteBridgeResources(app.Resources)", appSource, StringComparison.Ordinal);
        Assert.Contains("ThemeService.ApplyPaletteBridgeResources(app.Resources)", appSource, StringComparison.Ordinal);
        Assert.DoesNotContain("RestoreDarkThemeDefaults", appSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ApplyLightThemeEnhancements", appSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Color.FromRgb", appSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Color.FromArgb", appSource, StringComparison.Ordinal);
    }

    [Fact]
    public void FluentThemeBridge_SemanticFallbacksUseThemeForgeDarkPalette()
    {
        XDocument bridge = XDocument.Load(FindRepoFile("GUI", "WinForge.GUI", "Resources", "FluentThemeBridge.xaml"));

        AssertBridgeBrush(bridge, "StatusInstalledBrush", "#50FA7B");
        AssertBridgeBrush(bridge, "StatusInstallingBrush", "#8BE9FD");
        AssertBridgeBrush(bridge, "StatusFailedBrush", "#FF5555");
        AssertBridgeBrush(bridge, "StatusSkippedBrush", "#FFB86C");
        AssertBridgeBrush(bridge, "StatusPendingBrush", "#B3BBD6", "0.65");
        AssertBridgeBrush(bridge, "StatusAlreadyInstalledBrush", "#50FA7B", "0.75");
        AssertBridgeBrush(bridge, "RowInstalledBackground", "#50FA7B", "0.12");
        AssertBridgeBrush(bridge, "RowFailedBackground", "#FF5555", "0.12");
        AssertBridgeBrush(bridge, "RowUpdateAvailableBackground", "#FFB86C", "0.16");
        AssertBridgeBrush(bridge, "SuccessBackgroundBrush", "#50FA7B", "0.12");
        AssertBridgeBrush(bridge, "WarningBackgroundBrush", "#FFB86C", "0.16");
        AssertBridgeBrush(bridge, "ErrorBackgroundBrush", "#FF5555", "0.14");
        AssertBridgeBrush(bridge, "PrimaryHueMidBrush", "#BD93F9");
        AssertBridgeBrush(bridge, "SecondaryHueMidBrush", "#8BE9FD");
        AssertBridgeBrush(bridge, "ThemeAdaptiveAccentBrush", "#BD93F9");
        AssertBridgeBrush(bridge, "BadgePrimaryForegroundBrush", "#282A36");
        AssertBridgeBrush(bridge, "FocusIndicatorBrush", "#BD93F9");
    }

    [Fact]
    public void CSharpThemeFallbacks_AreCentralizedInThemeFallbackBrushes()
    {
        string convertersSource = File.ReadAllText(FindRepoFile("GUI", "WinForge.GUI", "Resources", "Converters.cs"));
        string severitySource = File.ReadAllText(FindRepoFile("GUI", "WinForge.GUI", "Controls", "SeverityIndicator.xaml.cs"));
        string fallbackSource = File.ReadAllText(FindRepoFile("GUI", "WinForge.GUI", "Resources", "ThemeFallbackBrushes.cs"));

        Assert.Contains("internal static class ThemeFallbackBrushes", fallbackSource, StringComparison.Ordinal);
        Assert.Contains("ThemeFallbackBrushes.", convertersSource, StringComparison.Ordinal);
        Assert.Contains("ThemeFallbackBrushes.", severitySource, StringComparison.Ordinal);
        Assert.DoesNotContain("Color.FromRgb", convertersSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Color.FromArgb", convertersSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Color.FromRgb", severitySource, StringComparison.Ordinal);
        Assert.DoesNotContain("Color.FromArgb", severitySource, StringComparison.Ordinal);
    }

    [Fact]
    public void DashboardQuickNavigation_UsesWpfUiButtonsToAvoidNativeHoverChrome()
    {
        string dashboardPath = FindRepoFile("GUI", "WinForge.GUI", "Views", "DashboardView.xaml");
        XDocument dashboardDoc = XDocument.Load(dashboardPath);

        List<XElement> quickActionButtons = dashboardDoc.Descendants()
            .Where(element =>
                element.Name.LocalName == "Button"
                && string.Equals(element.Attribute("Style")?.Value, "{StaticResource QuickActionButton}", StringComparison.Ordinal))
            .ToList();

        Assert.Equal(2, quickActionButtons.Count);
        Assert.All(
            quickActionButtons,
            button => Assert.Equal("http://schemas.lepo.co/wpfui/2022/xaml", button.Name.NamespaceName));
    }

    [Fact]
    public void AppCatalogToolbar_UsesCompactActionStyles()
    {
        string catalogPath = FindRepoFile("GUI", "WinForge.GUI", "Views", "AppCatalogView.xaml");
        string catalogXaml = File.ReadAllText(catalogPath);
        XDocument catalogDoc = XDocument.Load(catalogPath);

        Assert.Contains("Style=\"{StaticResource CompactPrimaryButton}\"", catalogXaml, StringComparison.Ordinal);
        Assert.Contains("Style=\"{StaticResource CompactSecondaryButton}\"", catalogXaml, StringComparison.Ordinal);
        Assert.Contains("Style=\"{StaticResource CompactSecondaryDropDownButton}\"", catalogXaml, StringComparison.Ordinal);
        Assert.DoesNotContain("<ui:DropDownButton ToolTip=\"{loc:Loc AppCatalog_ExportTooltip}\"", catalogXaml, StringComparison.Ordinal);

        XElement addButton = FindElementByCommand(
            catalogDoc,
            "{Binding AddCommand}",
            "{StaticResource CompactPrimaryButton}");
        XElement importButton = FindElementByCommand(
            catalogDoc,
            "{Binding ImportCommand}",
            "{StaticResource CompactSecondaryButton}");
        XElement verifyButton = FindElementByCommand(
            catalogDoc,
            "{Binding VerifyAllSourcesCommand}",
            "{StaticResource CompactSecondaryButton}");
        XElement cancelButton = FindElementByCommand(
            catalogDoc,
            "{Binding CancelVerificationCommand}",
            "{StaticResource CompactSecondaryButton}");
        XElement exportButton = catalogDoc.Descendants()
            .Single(element =>
                element.Name.LocalName == "DropDownButton"
                && string.Equals(
                    element.Attribute("Style")?.Value,
                    "{StaticResource CompactSecondaryDropDownButton}",
                    StringComparison.Ordinal));

        foreach (XElement button in new[] { addButton, importButton, verifyButton, cancelButton })
        {
            Assert.Null(button.Attribute("MinWidth"));
        }

        foreach (XElement icon in new[]
                 {
                     addButton.Descendants().Single(element => element.Name.LocalName == "SymbolIcon"),
                     importButton.Descendants().Single(element => element.Name.LocalName == "SymbolIcon"),
                     exportButton.Descendants().Single(element => element.Name.LocalName == "SymbolIcon"),
                     verifyButton.Descendants().Single(element => element.Name.LocalName == "SymbolIcon")
                 })
        {
            Assert.Equal("16", icon.Attribute("Width")?.Value);
            Assert.Equal("16", icon.Attribute("Height")?.Value);
            Assert.Equal("{StaticResource ButtonIconMarginCompact}", icon.Attribute("Margin")?.Value);
        }

        XElement cancelSpinner = cancelButton.Descendants()
            .Single(element => element.Name.LocalName == "ProgressRing");
        Assert.Equal("16", cancelSpinner.Attribute("Width")?.Value);
        Assert.Equal("16", cancelSpinner.Attribute("Height")?.Value);
        Assert.Equal("{StaticResource ButtonIconMarginCompact}", cancelSpinner.Attribute("Margin")?.Value);
    }

    [Fact]
    public void WpfUiButtons_UseNamedStylesInsteadOfInlineAppearance()
    {
        List<string> xamlFiles = Directory.EnumerateFiles(
            Path.GetDirectoryName(FindRepoFile("GUI", "WinForge.GUI", "App.xaml"))!,
            "*.xaml",
            SearchOption.AllDirectories)
            .Where(path => !path.EndsWith("App.xaml", StringComparison.OrdinalIgnoreCase))
            .ToList();

        List<string> offenders = new List<string>();
        foreach (string? path in xamlFiles)
        {
            XDocument doc = XDocument.Load(path);
            List<string> wpfUiButtonsWithInlineAppearance = doc.Descendants()
                .Where(element =>
                    element.Name.LocalName == "Button"
                    && string.Equals(
                        element.Name.NamespaceName,
                        "http://schemas.lepo.co/wpfui/2022/xaml",
                        StringComparison.Ordinal)
                    && element.Attribute("Appearance") is not null)
                .Select(element => Path.GetRelativePath(Path.GetDirectoryName(FindRepoFile("GUI", "WinForge.GUI", "App.xaml"))!, path))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            offenders.AddRange(wpfUiButtonsWithInlineAppearance);
        }

        Assert.Empty(offenders);
    }

    [Fact]
    public void MainWindow_TitleBarUsesAppNameAndRuntimeTitleUpdate()
    {
        string mainWindowPath = FindRepoFile("GUI", "WinForge.GUI", "MainWindow.xaml");
        string mainWindowXaml = File.ReadAllText(mainWindowPath);
        string mainWindowCode = File.ReadAllText(FindRepoFile("GUI", "WinForge.GUI", "MainWindow.xaml.cs"));

        Assert.Contains("Title=\"{loc:Loc App_Name}\"", mainWindowXaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Title=\"{loc:Loc App_Title}\"", mainWindowXaml, StringComparison.Ordinal);
        Assert.Contains("TitleBar.Title = title;", mainWindowCode, StringComparison.Ordinal);
    }

    [Fact]
    public void SettingsView_SectionHeadersDoNotRepeatTabIcons()
    {
        string settingsPath = FindRepoFile("GUI", "WinForge.GUI", "Views", "SettingsView.xaml");
        string settingsXaml = File.ReadAllText(settingsPath);
        XDocument settingsDoc = XDocument.Load(settingsPath);
        HashSet<string> removedSectionIcons = new HashSet<string>(StringComparer.Ordinal)
        {
            "Color24",
            "Translate24",
            "Gauge24",
            "Database24",
            "CalendarAdd24",
            "CalendarMultiple24"
        };
        List<string?> repeatedSectionIcons = settingsDoc.Descendants()
            .Where(element =>
                element.Name.LocalName == "SymbolIcon"
                && removedSectionIcons.Contains(element.Attribute("Symbol")?.Value ?? string.Empty)
                && string.Equals(element.Attribute("Width")?.Value, "24", StringComparison.Ordinal))
            .Select(element => element.Attribute("Symbol")?.Value)
            .ToList();

        Assert.Empty(repeatedSectionIcons);

        Assert.Contains("Symbol=\"Color24\" Width=\"18\"", settingsXaml, StringComparison.Ordinal);
        Assert.Contains("Symbol=\"Gauge24\" Width=\"18\"", settingsXaml, StringComparison.Ordinal);
        Assert.Contains("Symbol=\"Database24\" Width=\"18\"", settingsXaml, StringComparison.Ordinal);
        Assert.Contains("Symbol=\"CalendarClock24\" Width=\"18\"", settingsXaml, StringComparison.Ordinal);
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

    private static XElement FindElementByCommand(XDocument document, string command, string style)
    {
        return document.Descendants()
            .Single(element =>
                string.Equals(element.Attribute("Command")?.Value, command, StringComparison.Ordinal)
                && string.Equals(element.Attribute("Style")?.Value, style, StringComparison.Ordinal));
    }

    private static void AssertImplicitStyleSetter(
        IReadOnlyCollection<XElement> styles,
        string targetType,
        string property,
        string expectedValue)
    {
        XElement style = styles.Single(element =>
            string.Equals(element.Attribute("TargetType")?.Value, targetType, StringComparison.Ordinal)
            && element.Attribute(XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml")) is null);
        XElement? setter = style.Elements()
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
        XElement style = styles.Single(element =>
            string.Equals(
                element.Attribute(XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value,
                styleKey,
                StringComparison.Ordinal));
        XElement? setter = style.Elements()
            .SingleOrDefault(element =>
                element.Name.LocalName == "Setter"
                && string.Equals(element.Attribute("Property")?.Value, property, StringComparison.Ordinal));

        Assert.NotNull(setter);
        Assert.Equal(expectedValue, setter.Attribute("Value")?.Value);
    }

    private static void AssertResourceValue(XDocument appXaml, string key, string expectedValue)
    {
        XElement? resource = appXaml.Descendants()
            .SingleOrDefault(element =>
                string.Equals(
                    element.Attribute(XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value,
                    key,
                    StringComparison.Ordinal));

        Assert.NotNull(resource);
        Assert.Equal(expectedValue, resource.Value);
    }

    private static string ExtractXamlSection(string xaml, string startMarker, string endMarker)
    {
        int start = xaml.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Could not find start marker {startMarker}.");

        int end = xaml.IndexOf(endMarker, start, StringComparison.Ordinal);
        Assert.True(end > start, $"Could not find end marker {endMarker} after {startMarker}.");

        return xaml[start..end];
    }

    private static void AssertBridgeBrush(
        XDocument bridge,
        string key,
        string expectedColor,
        string? expectedOpacity = null)
    {
        XElement brush = bridge.Descendants()
            .Single(element =>
                element.Name.LocalName == "SolidColorBrush"
                && string.Equals(
                    element.Attribute(XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value,
                    key,
                    StringComparison.Ordinal));

        Assert.Equal(expectedColor, brush.Attribute("Color")?.Value);
        Assert.Equal(expectedOpacity, brush.Attribute("Opacity")?.Value);
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

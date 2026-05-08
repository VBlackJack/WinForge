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
using System.Windows;
using System.Xml.Linq;
using Win11Forge.GUI.Resources;
using Win11Forge.GUI.Services;

namespace Win11Forge.GUI.Tests;

/// <summary>
/// Tests for accessibility hardening resources and theme interactions.
/// </summary>
[Collection("WpfApplication")]
public class AccessibilityHardeningTests
{
    public static IEnumerable<object[]> RequiredA11yLocKeyCases =>
    [
        ["Views/AppsView.xaml", new[] { "LogViewer_Copy", "LogViewer_Close", "Summary_Close", "Apps_SaveProfile", "Apps_ResetColumns" }],
        ["Views/Dialogs/ApplicationEditorDialog.xaml", new[] { "AppEditor_SearchAction", "AppEditor_ApplySelection" }],
        ["Views/PrerequisitesView.xaml", new[] { "Prerequisites_Check" }],
        ["Views/SettingsView.xaml", new[] { "Settings_ReducedMotion_Desc", "Settings_HighContrast_Desc" }],
        ["Controls/ConfirmDialog.xaml", new[] { "Dialog_Cancel", "Dialog_Confirm" }],
        ["Controls/ErrorDialog.xaml", new[] { "Dialog_Help", "Dialog_Retry", "Dialog_OK" }]
    ];

    [Fact]
    public void ThemeServiceHc_HighContrastEnabled_ReappliesHighContrastResources()
    {
        var reapplyCount = 0;
        var settingsService = new MockAppSettingsService
        {
            SettingsToReturn = new AppSettings
            {
                ThemeName = ThemeNames.DraculaPro,
                IsHighContrastEnabled = true
            }
        };
        var themeService = new ThemeService(settingsService, () => reapplyCount++);

        themeService.ReapplyHighContrastIfEnabled(isHighContrastEnabled: true);

        Assert.Equal(1, reapplyCount);
    }

    [Fact]
    public void ReducedMotionPropagation_WhenEnabled_SetsMotionResourcesToZero()
    {
        var resources = new ResourceDictionary();

        App.ApplyAnimationResources(resources, reducedMotion: true);

        AssertDuration(TimeSpan.Zero, resources["AnimationFast"]);
        AssertDuration(TimeSpan.Zero, resources["AnimationNormal"]);
        AssertDuration(TimeSpan.Zero, resources["AnimationSlow"]);
        AssertDuration(TimeSpan.Zero, resources["AnimationMicro"]);
    }

    [Fact]
    public void ReducedMotionPropagation_WhenDisabled_RestoresMotionResources()
    {
        var resources = new ResourceDictionary();

        App.ApplyAnimationResources(resources, reducedMotion: false);

        AssertDuration(TimeSpan.FromMilliseconds(150), resources["AnimationFast"]);
        AssertDuration(TimeSpan.FromMilliseconds(300), resources["AnimationNormal"]);
        AssertDuration(TimeSpan.FromMilliseconds(500), resources["AnimationSlow"]);
        AssertDuration(TimeSpan.FromMilliseconds(50), resources["AnimationMicro"]);
    }

    [Fact]
    public void ReducedMotionPropagation_AppXamlDoesNotDefineGlobalMotionStoryboards()
    {
        var appXaml = XDocument.Load(FindRepoFile("GUI", "Win11Forge.GUI", "App.xaml"));
        var forbiddenElements = new[]
        {
            "BeginStoryboard",
            "Storyboard",
            "DoubleAnimation",
            "ThicknessAnimation",
            "ColorAnimation",
            "EventTrigger"
        };

        foreach (var elementName in forbiddenElements)
        {
            Assert.DoesNotContain(appXaml.Descendants(), element => element.Name.LocalName == elementName);
        }
    }

    [Fact]
    public void FocusVisualStyleApplied_AppResourcesDefineImplicitKeyboardFocusStyles()
    {
        var appXaml = XDocument.Load(FindRepoFile("GUI", "Win11Forge.GUI", "App.xaml"));
        var styles = appXaml.Descendants()
            .Where(element => element.Name.LocalName == "Style")
            .ToList();

        AssertImplicitFocusStyle(styles, "TextBox");
        AssertImplicitFocusStyle(styles, "ComboBox");
        AssertImplicitFocusStyle(styles, "CheckBox");
        AssertNamedFocusStyle(styles, "RequiredTextBoxStyle");
        AssertNamedFocusStyle(styles, "TouchFriendlyButton");
        AssertNamedFocusStyle(styles, "TouchFriendlyIconButton");
        AssertNamedFocusStyle(styles, "TouchFriendlyTextBox");
        AssertNamedFocusStyle(styles, "TouchFriendlyComboBox");
        AssertNamedFocusStyle(styles, "TouchFriendlyCheckBox");
        AssertNamedFocusStyle(styles, "HoverScaleIconButton");
        AssertNamedFocusStyle(styles, "AnimatedSelectionCheckBox");

        var focusVisualBorder = appXaml.Descendants()
            .Single(element =>
                element.Name.LocalName == "Border"
                && string.Equals(
                    element.Attribute("BorderThickness")?.Value,
                    "2",
                    StringComparison.Ordinal)
                && string.Equals(
                    element.Attribute("Margin")?.Value,
                    "-2",
                    StringComparison.Ordinal));

        Assert.Equal(
            "{DynamicResource FocusIndicatorBrush}",
            focusVisualBorder.Attribute("BorderBrush")?.Value);
    }

    [Fact]
    public void ThemeAwareFlyoutStyles_AppResourcesCoverNativeMenusAndComboBoxes()
    {
        var appXaml = XDocument.Load(FindRepoFile("GUI", "Win11Forge.GUI", "App.xaml"));
        var styles = appXaml.Descendants()
            .Where(element => element.Name.LocalName == "Style")
            .ToList();

        AssertImplicitStyleBasedOn(styles, "ComboBox", "{StaticResource ThemeAwareComboBoxStyle}");
        AssertNamedStyleSetter(styles, "ThemeAwareComboBoxStyle", "Background", "{DynamicResource ControlFillColorDefaultBrush}");
        AssertNamedStyleSetter(styles, "ThemeAwareComboBoxStyle", "Foreground", "{DynamicResource TextFillColorPrimaryBrush}");
        AssertNamedStyleSetter(styles, "TouchFriendlyComboBox", "MinHeight", "44");
        AssertImplicitStyleSetter(styles, "ComboBoxItem", "Background", "{DynamicResource ControlFillColorDefaultBrush}");
        AssertImplicitStyleSetter(styles, "ContextMenu", "Background", "{DynamicResource CardBackgroundFillColorDefaultBrush}");
        AssertImplicitStyleSetter(styles, "MenuItem", "Foreground", "{DynamicResource TextFillColorPrimaryBrush}");

        var menuItemStyle = FindImplicitStyle(styles, "MenuItem");
        Assert.Contains(
            menuItemStyle.Descendants(),
            element =>
                element.Name.LocalName == "ControlTemplate"
                && string.Equals(element.Attribute("TargetType")?.Value, "MenuItem", StringComparison.Ordinal));
        Assert.Contains(
            menuItemStyle.Descendants(),
            element =>
                element.Name.LocalName == "Border"
                && string.Equals(
                    element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value,
                    "MenuItemRoot",
                    StringComparison.Ordinal));

        var selectedComboBoxItemTrigger = FindImplicitStyle(styles, "ComboBoxItem")
            .Descendants()
            .Single(element =>
                element.Name.LocalName == "Trigger"
                && string.Equals(element.Attribute("Property")?.Value, "IsSelected", StringComparison.Ordinal)
                && string.Equals(element.Attribute("Value")?.Value, "True", StringComparison.Ordinal));

        Assert.Contains(
            selectedComboBoxItemTrigger.Elements(),
            element =>
                element.Name.LocalName == "Setter"
                && string.Equals(element.Attribute("Property")?.Value, "Foreground", StringComparison.Ordinal)
                && string.Equals(
                    element.Attribute("Value")?.Value,
                    "{DynamicResource TextOnAccentFillColorPrimaryBrush}",
                    StringComparison.Ordinal));
    }

    [Fact]
    public void ThemeAwareDataGridStyles_AppResourcesPreventSystemSelectionBrushBleed()
    {
        var appXaml = XDocument.Load(FindRepoFile("GUI", "Win11Forge.GUI", "App.xaml"));
        var styles = appXaml.Descendants()
            .Where(element => element.Name.LocalName == "Style")
            .ToList();

        AssertImplicitStyleSetter(styles, "DataGrid", "CellStyle", "{StaticResource EnhancedDataGridCellStyle}");
        AssertNamedStyleSetter(styles, "EnhancedDataGridCellStyle", "BorderThickness", "0");

        var selectedRowTrigger = FindNamedStyle(styles, "EnhancedDataGridRowStyle")
            .Descendants()
            .Single(element =>
                element.Name.LocalName == "Trigger"
                && string.Equals(element.Attribute("Property")?.Value, "IsSelected", StringComparison.Ordinal)
                && string.Equals(element.Attribute("Value")?.Value, "True", StringComparison.Ordinal));

        Assert.Contains(
            selectedRowTrigger.Elements(),
            element =>
                element.Name.LocalName == "Setter"
                && string.Equals(element.Attribute("Property")?.Value, "Background", StringComparison.Ordinal)
                && string.Equals(
                    element.Attribute("Value")?.Value,
                    "{DynamicResource ControlFillColorSecondaryBrush}",
                    StringComparison.Ordinal));

        var selectedCellTrigger = FindNamedStyle(styles, "EnhancedDataGridCellStyle")
            .Descendants()
            .Single(element =>
                element.Name.LocalName == "Trigger"
                && string.Equals(element.Attribute("Property")?.Value, "IsSelected", StringComparison.Ordinal)
                && string.Equals(element.Attribute("Value")?.Value, "True", StringComparison.Ordinal));

        Assert.Contains(
            selectedCellTrigger.Elements(),
            element =>
                element.Name.LocalName == "Setter"
                && string.Equals(element.Attribute("Property")?.Value, "Background", StringComparison.Ordinal)
                && string.Equals(element.Attribute("Value")?.Value, "Transparent", StringComparison.Ordinal));
        Assert.Contains(
            selectedCellTrigger.Elements(),
            element =>
                element.Name.LocalName == "Setter"
                && string.Equals(element.Attribute("Property")?.Value, "Foreground", StringComparison.Ordinal)
                && string.Equals(
                    element.Attribute("Value")?.Value,
                    "{DynamicResource TextFillColorPrimaryBrush}",
                    StringComparison.Ordinal));
    }

    [Fact]
    public void AppsDataGridRowStyle_BasesOnEnhancedRowStyleForSelectionContrast()
    {
        var appsXaml = XDocument.Load(FindRepoFile("GUI", "Win11Forge.GUI", "Views", "AppsView.xaml"));
        var rowStyle = appsXaml.Descendants()
            .Single(element => element.Name.LocalName == "DataGrid.RowStyle")
            .Elements()
            .Single(element => element.Name.LocalName == "Style");

        Assert.Equal(
            "{StaticResource EnhancedDataGridRowStyle}",
            rowStyle.Attribute("BasedOn")?.Value);
    }

    [Theory]
    [MemberData(nameof(RequiredA11yLocKeyCases))]
    public void RequiredA11yLocKeys_ArePresentInXaml(string relativePath, string[] requiredKeys)
    {
        var xaml = File.ReadAllText(FindRepoFile("GUI", "Win11Forge.GUI", relativePath));

        foreach (var key in requiredKeys)
        {
            Assert.Contains($"{{loc:Loc {key}}}", xaml, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void HighContrastMode_TextOnAccentBrushes_AreRemapped()
    {
        var src = File.ReadAllText(FindRepoFile("GUI", "Win11Forge.GUI", "App.xaml.cs"));

        Assert.Contains("SwapIfExists(app, \"TextOnAccentFillColorPrimaryBrush\"", src, StringComparison.Ordinal);
        Assert.Contains("SwapIfExists(app, \"TextOnAccentFillColorSecondaryBrush\"", src, StringComparison.Ordinal);
        Assert.Contains("SwapIfExists(app, \"TextOnAccentFillColorDisabledBrush\"", src, StringComparison.Ordinal);
    }

    [Fact]
    public void HighContrastTheme_ImplicitlyStylesWpfUiButton()
    {
        var xaml = XDocument.Load(FindRepoFile("GUI", "Win11Forge.GUI", "Resources", "HighContrastTheme.xaml"));
        var implicitUiButtonStyle = xaml.Descendants()
            .Where(element => element.Name.LocalName == "Style")
            .FirstOrDefault(element =>
            {
                var target = element.Attribute("TargetType")?.Value ?? string.Empty;
                var hasKey = element.Attribute(XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml")) is not null;
                return target.Contains("ui:Button", StringComparison.Ordinal) && !hasKey;
            });

        Assert.NotNull(implicitUiButtonStyle);
        Assert.Contains(
            "HighContrastButtonStyle",
            implicitUiButtonStyle.Attribute("BasedOn")?.Value ?? string.Empty,
            StringComparison.Ordinal);
    }

    [Fact]
    public void AppsView_HeaderUsesIconTitleSubtitlePattern()
    {
        var xaml = File.ReadAllText(FindRepoFile("GUI", "Win11Forge.GUI", "Views", "AppsView.xaml"));

        Assert.Contains("Symbol=\"Apps24\"", xaml, StringComparison.Ordinal);
        Assert.Contains("{loc:Loc Apps_Title}", xaml, StringComparison.Ordinal);
        Assert.Contains("{loc:Loc Apps_Subtitle}", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.AutomationId=\"PageApplications\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void AppsView_DoesNotForceHorizontalScrollOnFilterCards()
    {
        var xaml = File.ReadAllText(FindRepoFile("GUI", "Win11Forge.GUI", "Views", "AppsView.xaml"));
        var profileSelector = ExtractXamlSection(xaml, "<!-- Profile Selector Card -->", "<!-- Filter Bar Card -->");
        var filterBar = ExtractXamlSection(xaml, "<!-- Filter Bar Card -->", "<!-- Selection Action Bar -->");

        Assert.DoesNotContain("MinWidth=\"920\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("HorizontalScrollBarVisibility=\"Auto\"", profileSelector, StringComparison.Ordinal);
        Assert.DoesNotContain("HorizontalScrollBarVisibility=\"Auto\"", filterBar, StringComparison.Ordinal);
    }

    [Fact]
    public void AppCatalog_HidesUnavailableActionGroups()
    {
        var xaml = XDocument.Load(FindRepoFile("GUI", "Win11Forge.GUI", "Views", "AppCatalogView.xaml"));

        AssertButtonVisibilityBinding(xaml, "UndoCommand", "CanUndo");
        AssertButtonVisibilityBinding(xaml, "RedoCommand", "CanRedo");

        var selectionActions = xaml.Descendants()
            .Single(element =>
                element.Name.LocalName == "StackPanel"
                && string.Equals(element.Attribute("Grid.Column")?.Value, "3", StringComparison.Ordinal)
                && element.Descendants().Any(child =>
                    child.Name.LocalName == "Button"
                    && string.Equals(child.Attribute("Command")?.Value, "{Binding EditCommand}", StringComparison.Ordinal))
                && element.Descendants().Any(child =>
                    child.Name.LocalName == "Button"
                    && string.Equals(child.Attribute("Command")?.Value, "{Binding DuplicateCommand}", StringComparison.Ordinal))
                && element.Descendants().Any(child =>
                    child.Name.LocalName == "Button"
                    && string.Equals(child.Attribute("Command")?.Value, "{Binding DeleteCommand}", StringComparison.Ordinal)));

        Assert.Equal(
            "{Binding SelectedApplication, Converter={StaticResource NullableToVisibilityConverter}}",
            selectionActions.Attribute("Visibility")?.Value);
    }

    [Fact]
    public void AppsView_ProfileCardDoesNotDuplicateInstallSelected()
    {
        var xaml = File.ReadAllText(FindRepoFile("GUI", "Win11Forge.GUI", "Views", "AppsView.xaml"));
        var profileSelector = ExtractXamlSection(xaml, "<!-- Profile Selector Card -->", "<!-- Filter Bar Card -->");
        var selectionActionBar = ExtractXamlSection(xaml, "<!-- Selection Action Bar -->", "<!-- Applications DataGrid -->");

        Assert.DoesNotContain("InstallSelectedCommand", profileSelector, StringComparison.Ordinal);
        Assert.Contains("InstallSelectedCommand", selectionActionBar, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(xaml, "InstallSelectedCommand"));
    }

    [Fact]
    public void AppXaml_DefinesReinforcedTabItemStyleAsImplicit()
    {
        var xaml = XDocument.Load(FindRepoFile("GUI", "Win11Forge.GUI", "App.xaml"));
        var styles = xaml.Descendants()
            .Where(element => element.Name.LocalName == "Style")
            .ToList();
        var implicitTabItemStyle = styles.FirstOrDefault(element =>
        {
            var target = element.Attribute("TargetType")?.Value ?? string.Empty;
            var hasKey = element.Attribute(XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml")) is not null;
            return target.Contains("TabItem", StringComparison.Ordinal) && !hasKey;
        });

        Assert.NotNull(implicitTabItemStyle);
        Assert.Contains(
            "ReinforcedTabItemStyle",
            implicitTabItemStyle.Attribute("BasedOn")?.Value ?? string.Empty,
            StringComparison.Ordinal);
        AssertNamedStyleSetter(styles, "ReinforcedTabItemStyle", "BorderThickness", "0,0,0,3");
        AssertNamedStyleSetter(styles, "ReinforcedTabItemStyle", "BorderBrush", "Transparent");

        var selectedTrigger = FindNamedStyle(styles, "ReinforcedTabItemStyle")
            .Descendants()
            .Single(element =>
                element.Name.LocalName == "Trigger"
                && string.Equals(element.Attribute("Property")?.Value, "IsSelected", StringComparison.Ordinal)
                && string.Equals(element.Attribute("Value")?.Value, "True", StringComparison.Ordinal));

        Assert.Contains(
            selectedTrigger.Elements(),
            element =>
                element.Name.LocalName == "Setter"
                && string.Equals(element.Attribute("Property")?.Value, "BorderBrush", StringComparison.Ordinal)
                && string.Equals(element.Attribute("Value")?.Value, "{DynamicResource SystemAccentColorPrimaryBrush}", StringComparison.Ordinal));
        Assert.Contains(
            selectedTrigger.Elements(),
            element =>
                element.Name.LocalName == "Setter"
                && string.Equals(element.Attribute("Property")?.Value, "Background", StringComparison.Ordinal)
                && string.Equals(element.Attribute("Value")?.Value, "{DynamicResource SubtleFillColorSecondaryBrush}", StringComparison.Ordinal));
        Assert.Contains(
            selectedTrigger.Elements(),
            element =>
                element.Name.LocalName == "Setter"
                && string.Equals(element.Attribute("Property")?.Value, "FontWeight", StringComparison.Ordinal)
                && string.Equals(element.Attribute("Value")?.Value, "SemiBold", StringComparison.Ordinal));
    }

    [Fact]
    public void AppXaml_TabItemUsesThemeAwareTemplate()
    {
        var xaml = XDocument.Load(FindRepoFile("GUI", "Win11Forge.GUI", "App.xaml"));
        var styles = xaml.Descendants()
            .Where(element => element.Name.LocalName == "Style")
            .ToList();
        var tabItemStyle = FindNamedStyle(styles, "ReinforcedTabItemStyle");

        AssertNamedStyleSetter(styles, "ReinforcedTabItemStyle", "Background", "Transparent");
        AssertNamedStyleSetter(styles, "ReinforcedTabItemStyle", "Foreground", "{DynamicResource TextFillColorPrimaryBrush}");
        AssertNamedStyleSetter(styles, "ReinforcedTabItemStyle", "FocusVisualStyle", "{StaticResource HighVisibilityFocusVisual}");

        var template = tabItemStyle.Descendants()
            .Single(element =>
                element.Name.LocalName == "ControlTemplate"
                && string.Equals(element.Attribute("TargetType")?.Value, "{x:Type TabItem}", StringComparison.Ordinal));

        Assert.Contains(
            template.Descendants(),
            element =>
                element.Name.LocalName == "ContentPresenter"
                && string.Equals(
                    element.Attribute("ContentSource")?.Value,
                    "Header",
                    StringComparison.Ordinal)
                && string.Equals(
                    element.Attribute("TextElement.Foreground")?.Value,
                    "{TemplateBinding Foreground}",
                    StringComparison.Ordinal));

        Assert.Contains(
            template.Descendants(),
            element =>
                element.Name.LocalName == "Trigger"
                && string.Equals(element.Attribute("Property")?.Value, "IsEnabled", StringComparison.Ordinal)
                && string.Equals(element.Attribute("Value")?.Value, "False", StringComparison.Ordinal));

        Assert.Contains(
            tabItemStyle.Descendants(),
            element =>
                element.Name.LocalName == "Trigger"
                && string.Equals(element.Attribute("Property")?.Value, "IsKeyboardFocused", StringComparison.Ordinal)
                && string.Equals(element.Attribute("Value")?.Value, "True", StringComparison.Ordinal));

        var selectedTrigger = tabItemStyle.Descendants()
            .Single(element =>
                element.Name.LocalName == "Trigger"
                && string.Equals(element.Attribute("Property")?.Value, "IsSelected", StringComparison.Ordinal)
                && string.Equals(element.Attribute("Value")?.Value, "True", StringComparison.Ordinal));

        Assert.Contains(
            selectedTrigger.Elements(),
            element =>
                element.Name.LocalName == "Setter"
                && string.Equals(element.Attribute("Property")?.Value, "Foreground", StringComparison.Ordinal)
                && string.Equals(element.Attribute("Value")?.Value, "{DynamicResource TextFillColorPrimaryBrush}", StringComparison.Ordinal));
    }

    [Fact]
    public void MainWindow_NavSplitsWorkflowAndConfigClusters()
    {
        var xaml = File.ReadAllText(FindRepoFile("GUI", "Win11Forge.GUI", "MainWindow.xaml"));
        var sepIdx = xaml.IndexOf("<ui:NavigationViewItemSeparator", StringComparison.Ordinal);
        var appCatalogIdx = xaml.IndexOf("Tag=\"5\"", sepIdx, StringComparison.Ordinal);
        var settingsIdx = xaml.IndexOf("Tag=\"4\"", sepIdx, StringComparison.Ordinal);

        Assert.True(sepIdx > 0);
        Assert.True(appCatalogIdx > sepIdx);
        Assert.True(settingsIdx > appCatalogIdx);
    }

    private static string ExtractXamlSection(string xaml, string startMarker, string endMarker)
    {
        var start = xaml.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Could not find section start marker: {startMarker}");

        var end = xaml.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);
        Assert.True(end > start, $"Could not find section end marker: {endMarker}");

        return xaml[start..end];
    }

    private static void AssertButtonVisibilityBinding(XDocument xaml, string commandName, string visibilitySource)
    {
        var button = xaml.Descendants()
            .Single(element =>
                element.Name.LocalName == "Button"
                && string.Equals(element.Attribute("Command")?.Value, $"{{Binding {commandName}}}", StringComparison.Ordinal));

        Assert.Equal(
            $"{{Binding {visibilitySource}, Converter={{StaticResource BooleanToVisibilityConverter}}}}",
            button.Attribute("Visibility")?.Value);
    }

    private static int CountOccurrences(string value, string pattern)
    {
        var count = 0;
        var index = 0;
        while ((index = value.IndexOf(pattern, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += pattern.Length;
        }

        return count;
    }

    private static void AssertImplicitFocusStyle(IReadOnlyCollection<XElement> styles, string targetType)
    {
        var style = FindImplicitStyle(styles, targetType);
        var focusSetter = style.Elements()
            .SingleOrDefault(element =>
                element.Name.LocalName == "Setter"
                && string.Equals(element.Attribute("Property")?.Value, "FocusVisualStyle", StringComparison.Ordinal));

        Assert.NotNull(focusSetter);
        Assert.Equal(
            "{StaticResource HighVisibilityFocusVisual}",
            focusSetter.Attribute("Value")?.Value);
    }

    private static void AssertImplicitStyleSetter(
        IReadOnlyCollection<XElement> styles,
        string targetType,
        string property,
        string expectedValue)
    {
        var style = FindImplicitStyle(styles, targetType);
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
        var style = FindNamedStyle(styles, styleKey);
        var setter = style.Elements()
            .SingleOrDefault(element =>
                element.Name.LocalName == "Setter"
                && string.Equals(element.Attribute("Property")?.Value, property, StringComparison.Ordinal));

        Assert.NotNull(setter);
        Assert.Equal(expectedValue, setter.Attribute("Value")?.Value);
    }

    private static void AssertImplicitStyleBasedOn(
        IReadOnlyCollection<XElement> styles,
        string targetType,
        string expectedBasedOn)
    {
        var style = FindImplicitStyle(styles, targetType);

        Assert.Equal(expectedBasedOn, style.Attribute("BasedOn")?.Value);
    }

    private static XElement FindImplicitStyle(IReadOnlyCollection<XElement> styles, string targetType)
    {
        return styles.Single(element =>
            string.Equals(element.Attribute("TargetType")?.Value, targetType, StringComparison.Ordinal)
            && element.Attribute(XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml")) is null);
    }

    private static XElement FindNamedStyle(IReadOnlyCollection<XElement> styles, string styleKey)
    {
        var xKey = XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml");
        return styles.Single(element =>
            string.Equals(element.Attribute(xKey)?.Value, styleKey, StringComparison.Ordinal));
    }

    private static void AssertNamedFocusStyle(IReadOnlyCollection<XElement> styles, string styleKey)
    {
        var style = FindNamedStyle(styles, styleKey);
        var focusSetter = style.Elements()
            .SingleOrDefault(element =>
                element.Name.LocalName == "Setter"
                && string.Equals(element.Attribute("Property")?.Value, "FocusVisualStyle", StringComparison.Ordinal));

        Assert.NotNull(focusSetter);
        Assert.Equal(
            "{StaticResource HighVisibilityFocusVisual}",
            focusSetter.Attribute("Value")?.Value);
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

    private static void AssertDuration(TimeSpan expected, object? actual)
    {
        var duration = Assert.IsType<Duration>(actual);
        Assert.Equal(expected, duration.TimeSpan);
    }

}

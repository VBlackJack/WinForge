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
        [
            "Views/Dialogs/ApplicationEditorDialog.xaml",
            new[]
            {
                "A11y_AppEditor_SearchWinget",
                "A11y_AppEditor_SearchChocolatey",
                "A11y_AppEditor_SearchStore",
                "A11y_AppEditor_ApplyWinget",
                "A11y_AppEditor_ApplyChocolatey",
                "A11y_AppEditor_ApplyStore"
            }
        ],
        ["Views/PrerequisitesView.xaml", new[] { "Prerequisites_Check" }],
        ["Views/SettingsView.xaml", new[] { "Settings_AccentTint_Desc", "Settings_ReducedMotion_Desc", "Settings_HighContrast_Desc" }]
    ];

    [Fact]
    public void ThemeServiceHc_HighContrastEnabled_ReappliesHighContrastResources()
    {
        int reapplyCount = 0;
        MockAppSettingsService settingsService = new MockAppSettingsService
        {
            SettingsToReturn = new AppSettings
            {
                ThemeName = ThemeNames.Drakul,
                IsHighContrastEnabled = true
            }
        };
        ThemeService themeService = new ThemeService(settingsService, () => reapplyCount++);

        themeService.ReapplyHighContrastIfEnabled(isHighContrastEnabled: true);

        Assert.Equal(1, reapplyCount);
    }

    [Fact]
    public void ReducedMotionPropagation_WhenEnabled_SetsMotionResourcesToZero()
    {
        ResourceDictionary resources = new ResourceDictionary();

        App.ApplyAnimationResources(resources, reducedMotion: true);

        AssertDuration(TimeSpan.Zero, resources["AnimationFast"]);
        AssertDuration(TimeSpan.Zero, resources["AnimationNormal"]);
        AssertDuration(TimeSpan.Zero, resources["AnimationSlow"]);
        AssertDuration(TimeSpan.Zero, resources["AnimationMicro"]);
    }

    [Fact]
    public void ReducedMotionPropagation_WhenDisabled_RestoresMotionResources()
    {
        ResourceDictionary resources = new ResourceDictionary();

        App.ApplyAnimationResources(resources, reducedMotion: false);

        AssertDuration(TimeSpan.FromMilliseconds(150), resources["AnimationFast"]);
        AssertDuration(TimeSpan.FromMilliseconds(300), resources["AnimationNormal"]);
        AssertDuration(TimeSpan.FromMilliseconds(500), resources["AnimationSlow"]);
        AssertDuration(TimeSpan.FromMilliseconds(50), resources["AnimationMicro"]);
    }

    [Fact]
    public void ReducedMotionPropagation_AppXamlDoesNotDefineGlobalMotionStoryboards()
    {
        XDocument appXaml = XDocument.Load(FindRepoFile("GUI", "Win11Forge.GUI", "App.xaml"));
        string[] forbiddenElements = new[]
        {
            "BeginStoryboard",
            "Storyboard",
            "DoubleAnimation",
            "ThicknessAnimation",
            "ColorAnimation",
            "EventTrigger"
        };

        foreach (string? elementName in forbiddenElements)
        {
            Assert.DoesNotContain(appXaml.Descendants(), element => element.Name.LocalName == elementName);
        }
    }

    [Fact]
    public void FocusVisualStyleApplied_AppResourcesDefineImplicitKeyboardFocusStyles()
    {
        XDocument appXaml = XDocument.Load(FindRepoFile("GUI", "Win11Forge.GUI", "App.xaml"));
        List<XElement> styles = appXaml.Descendants()
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
        AssertNamedFocusStyle(styles, "AnimatedSelectionCheckBox");

        XElement focusVisualBorder = appXaml.Descendants()
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
        XDocument appXaml = XDocument.Load(FindRepoFile("GUI", "Win11Forge.GUI", "App.xaml"));
        List<XElement> styles = appXaml.Descendants()
            .Where(element => element.Name.LocalName == "Style")
            .ToList();

        AssertImplicitStyleBasedOn(styles, "ComboBox", "{StaticResource ThemeAwareComboBoxStyle}");
        AssertNamedStyleSetter(styles, "ThemeAwareComboBoxStyle", "Background", "{DynamicResource ControlFillColorDefaultBrush}");
        AssertNamedStyleSetter(styles, "ThemeAwareComboBoxStyle", "Foreground", "{DynamicResource TextFillColorPrimaryBrush}");
        AssertNamedStyleSetter(styles, "TouchFriendlyComboBox", "MinHeight", "44");
        AssertImplicitStyleSetter(styles, "ComboBoxItem", "Background", "{DynamicResource ControlFillColorDefaultBrush}");
        AssertImplicitStyleSetter(styles, "ContextMenu", "Background", "{DynamicResource CardBackgroundFillColorDefaultBrush}");
        AssertImplicitStyleSetter(styles, "MenuItem", "Foreground", "{DynamicResource TextFillColorPrimaryBrush}");

        XElement menuItemStyle = FindImplicitStyle(styles, "MenuItem");
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

        XElement selectedComboBoxItemTrigger = FindImplicitStyle(styles, "ComboBoxItem")
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
        XDocument appXaml = XDocument.Load(FindRepoFile("GUI", "Win11Forge.GUI", "App.xaml"));
        List<XElement> styles = appXaml.Descendants()
            .Where(element => element.Name.LocalName == "Style")
            .ToList();

        AssertImplicitStyleSetter(styles, "DataGrid", "CellStyle", "{StaticResource EnhancedDataGridCellStyle}");
        AssertNamedStyleSetter(styles, "EnhancedDataGridCellStyle", "BorderThickness", "0");

        XElement selectedRowTrigger = FindNamedStyle(styles, "EnhancedDataGridRowStyle")
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

        XElement selectedCellTrigger = FindNamedStyle(styles, "EnhancedDataGridCellStyle")
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
        XDocument appsXaml = XDocument.Load(FindRepoFile("GUI", "Win11Forge.GUI", "Views", "AppsView.xaml"));
        XElement rowStyle = appsXaml.Descendants()
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
        string xaml = File.ReadAllText(FindRepoFile("GUI", "Win11Forge.GUI", relativePath));

        foreach (string key in requiredKeys)
        {
            Assert.Contains($"{{loc:Loc {key}}}", xaml, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void HighContrastMode_TextOnAccentBrushes_AreRemapped()
    {
        string src = File.ReadAllText(FindRepoFile("GUI", "Win11Forge.GUI", "App.xaml.cs"));

        Assert.Contains("SwapIfExists(app, \"TextOnAccentFillColorPrimaryBrush\"", src, StringComparison.Ordinal);
        Assert.Contains("SwapIfExists(app, \"TextOnAccentFillColorSecondaryBrush\"", src, StringComparison.Ordinal);
        Assert.Contains("SwapIfExists(app, \"TextOnAccentFillColorDisabledBrush\"", src, StringComparison.Ordinal);
    }

    [Fact]
    public void DeadResourceCleanup_RemovesBulkDeleteKeysAndLogsFallbacks()
    {
        string enResx = File.ReadAllText(FindRepoFile("GUI", "Win11Forge.GUI", "Resources", "Resources.resx"));
        string frResx = File.ReadAllText(FindRepoFile("GUI", "Win11Forge.GUI", "Resources", "Resources.fr.resx"));
        string logsViewModel = File.ReadAllText(FindRepoFile("GUI", "Win11Forge.GUI", "ViewModels", "LogsViewModel.cs"));

        Assert.DoesNotContain("AppCatalog_DeleteMultipleConfirm", enResx, StringComparison.Ordinal);
        Assert.DoesNotContain("AppCatalog_DeleteMultipleTitle", enResx, StringComparison.Ordinal);
        Assert.DoesNotContain("AppCatalog_DeleteMultipleConfirm", frResx, StringComparison.Ordinal);
        Assert.DoesNotContain("AppCatalog_DeleteMultipleTitle", frResx, StringComparison.Ordinal);
        Assert.DoesNotContain("?? \"Clear Old Logs\"", logsViewModel, StringComparison.Ordinal);
        Assert.DoesNotContain("Delete log files older than 7 days?", logsViewModel, StringComparison.Ordinal);
    }

    [Fact]
    public void DeadResourceCleanup_RemovesDeadDialogKeys()
    {
        string enResx = File.ReadAllText(FindRepoFile("GUI", "Win11Forge.GUI", "Resources", "Resources.resx"));
        string frResx = File.ReadAllText(FindRepoFile("GUI", "Win11Forge.GUI", "Resources", "Resources.fr.resx"));
        string designer = File.ReadAllText(FindRepoFile("GUI", "Win11Forge.GUI", "Resources", "Resources.Designer.cs"));

        string[] removedKeys =
        [
            "A11y_Dialog_" + "Confirm",
            "A11y_Dialog_" + "Error",
            "Dialog_" + "Confirm",
            "Dialog_" + "Help",
            "Dialog_" + "Retry",
            "Dialog_" + "OK"
        ];

        foreach (string key in removedKeys)
        {
            Assert.DoesNotContain($"name=\"{key}\"", enResx, StringComparison.Ordinal);
            Assert.DoesNotContain($"name=\"{key}\"", frResx, StringComparison.Ordinal);
            Assert.DoesNotContain($"GetString(\"{key}\"", designer, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void DeadResourceCleanup_RemovesUnusedKeys2026May()
    {
        string enResx = File.ReadAllText(FindRepoFile("GUI", "Win11Forge.GUI", "Resources", "Resources.resx"));
        string frResx = File.ReadAllText(FindRepoFile("GUI", "Win11Forge.GUI", "Resources", "Resources.fr.resx"));

        string[] removedKeys =
        [
            "AppEditor_Category",
            "Apps_SelectWithUpdates",
            "Dashboard_Updates_Available",
            "Deploy_InheritedFrom",
            "Deploy_Installing",
            "Help_Shortcut_Actions",
            "Help_Shortcut_Navigation",
            "Recovery_NetworkTimeout",
            "SourceEditor_TestPlaceholder",
            "Toast_UninstallComplete",
            "Accessibility_Progress",
            "Accessibility_ProgressWithItem",
            "Accessibility_ProgressComplete",
            "Accessibility_DeploymentStarted"
        ];

        foreach (string key in removedKeys)
        {
            Assert.DoesNotContain($"name=\"{key}\"", enResx, StringComparison.Ordinal);
            Assert.DoesNotContain($"name=\"{key}\"", frResx, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Resume_LocalizationKeys_ArePresentInEnAndFrResx()
    {
        string enResx = File.ReadAllText(FindRepoFile("GUI", "Win11Forge.GUI", "Resources", "Resources.resx"));
        string frResx = File.ReadAllText(FindRepoFile("GUI", "Win11Forge.GUI", "Resources", "Resources.fr.resx"));

        string[] requiredKeys =
        [
            "Resume_Title",
            "Resume_Message_Install",
            "Resume_Message_Update",
            "Resume_Message_Uninstall",
            "Resume_Action_Resume",
            "Resume_Action_Discard",
            "Resume_Action_KeepForLater"
        ];

        foreach (string key in requiredKeys)
        {
            Assert.Contains($"name=\"{key}\"", enResx, StringComparison.Ordinal);
            Assert.Contains($"name=\"{key}\"", frResx, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void HighContrastTheme_ImplicitlyStylesWpfUiButton()
    {
        XDocument xaml = XDocument.Load(FindRepoFile("GUI", "Win11Forge.GUI", "Resources", "HighContrastTheme.xaml"));
        XElement? implicitUiButtonStyle = xaml.Descendants()
            .Where(element => element.Name.LocalName == "Style")
            .FirstOrDefault(element =>
            {
                string target = element.Attribute("TargetType")?.Value ?? string.Empty;
                bool hasKey = element.Attribute(XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml")) is not null;
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
        string xaml = File.ReadAllText(FindRepoFile("GUI", "Win11Forge.GUI", "Views", "AppsView.xaml"));

        Assert.Contains("Symbol=\"Apps24\"", xaml, StringComparison.Ordinal);
        Assert.Contains("{loc:Loc Apps_Title}", xaml, StringComparison.Ordinal);
        Assert.Contains("{loc:Loc Apps_Subtitle}", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.AutomationId=\"PageApplications\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void AppsView_DoesNotForceHorizontalScrollOnFilterCards()
    {
        string xaml = File.ReadAllText(FindRepoFile("GUI", "Win11Forge.GUI", "Views", "AppsView.xaml"));
        string profileSelector = ExtractXamlSection(xaml, "<!-- Profile Selector Card -->", "<!-- Filter Bar Card -->");
        string filterBar = ExtractXamlSection(xaml, "<!-- Filter Bar Card -->", "<!-- Selection Action Bar -->");

        Assert.DoesNotContain("MinWidth=\"920\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("HorizontalScrollBarVisibility=\"Auto\"", profileSelector, StringComparison.Ordinal);
        Assert.DoesNotContain("HorizontalScrollBarVisibility=\"Auto\"", filterBar, StringComparison.Ordinal);
    }

    [Fact]
    public void AppCatalog_HidesUnavailableActionGroups()
    {
        XDocument xaml = XDocument.Load(FindRepoFile("GUI", "Win11Forge.GUI", "Views", "AppCatalogView.xaml"));

        AssertButtonVisibilityBinding(xaml, "UndoCommand", "CanUndo");
        AssertButtonVisibilityBinding(xaml, "RedoCommand", "CanRedo");

        XElement selectionActions = xaml.Descendants()
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
        string xaml = File.ReadAllText(FindRepoFile("GUI", "Win11Forge.GUI", "Views", "AppsView.xaml"));
        string profileSelector = ExtractXamlSection(xaml, "<!-- Profile Selector Card -->", "<!-- Filter Bar Card -->");
        string selectionActionBar = ExtractXamlSection(xaml, "<!-- Selection Action Bar -->", "<!-- Applications DataGrid -->");

        Assert.DoesNotContain("InstallSelectedCommand", profileSelector, StringComparison.Ordinal);
        Assert.Contains("InstallSelectedCommand", selectionActionBar, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(xaml, "InstallSelectedCommand"));
    }

    [Fact]
    public void AppXaml_DefinesReinforcedTabItemStyleAsImplicit()
    {
        XDocument xaml = XDocument.Load(FindRepoFile("GUI", "Win11Forge.GUI", "App.xaml"));
        List<XElement> styles = xaml.Descendants()
            .Where(element => element.Name.LocalName == "Style")
            .ToList();
        XElement? implicitTabItemStyle = styles.FirstOrDefault(element =>
        {
            string target = element.Attribute("TargetType")?.Value ?? string.Empty;
            bool hasKey = element.Attribute(XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml")) is not null;
            return target.Contains("TabItem", StringComparison.Ordinal) && !hasKey;
        });

        Assert.NotNull(implicitTabItemStyle);
        Assert.Contains(
            "ReinforcedTabItemStyle",
            implicitTabItemStyle.Attribute("BasedOn")?.Value ?? string.Empty,
            StringComparison.Ordinal);
        AssertNamedStyleSetter(styles, "ReinforcedTabItemStyle", "BorderThickness", "0,0,0,3");
        AssertNamedStyleSetter(styles, "ReinforcedTabItemStyle", "BorderBrush", "Transparent");

        XElement selectedTrigger = FindNamedStyle(styles, "ReinforcedTabItemStyle")
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
        XDocument xaml = XDocument.Load(FindRepoFile("GUI", "Win11Forge.GUI", "App.xaml"));
        List<XElement> styles = xaml.Descendants()
            .Where(element => element.Name.LocalName == "Style")
            .ToList();
        XElement tabItemStyle = FindNamedStyle(styles, "ReinforcedTabItemStyle");

        AssertNamedStyleSetter(styles, "ReinforcedTabItemStyle", "Background", "Transparent");
        AssertNamedStyleSetter(styles, "ReinforcedTabItemStyle", "Foreground", "{DynamicResource TextFillColorPrimaryBrush}");
        AssertNamedStyleSetter(styles, "ReinforcedTabItemStyle", "FocusVisualStyle", "{StaticResource HighVisibilityFocusVisual}");

        XElement template = tabItemStyle.Descendants()
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

        XElement selectedTrigger = tabItemStyle.Descendants()
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
        string xaml = File.ReadAllText(FindRepoFile("GUI", "Win11Forge.GUI", "MainWindow.xaml"));
        int sepIdx = xaml.IndexOf("<ui:NavigationViewItemSeparator", StringComparison.Ordinal);
        int appCatalogIdx = xaml.IndexOf("Tag=\"5\"", sepIdx, StringComparison.Ordinal);
        int settingsIdx = xaml.IndexOf("Tag=\"4\"", sepIdx, StringComparison.Ordinal);

        Assert.True(sepIdx > 0);
        Assert.True(appCatalogIdx > sepIdx);
        Assert.True(settingsIdx > appCatalogIdx);
    }

    private static string ExtractXamlSection(string xaml, string startMarker, string endMarker)
    {
        int start = xaml.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Could not find section start marker: {startMarker}");

        int end = xaml.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);
        Assert.True(end > start, $"Could not find section end marker: {endMarker}");

        return xaml[start..end];
    }

    private static void AssertButtonVisibilityBinding(XDocument xaml, string commandName, string visibilitySource)
    {
        XElement button = xaml.Descendants()
            .Single(element =>
                element.Name.LocalName == "Button"
                && string.Equals(element.Attribute("Command")?.Value, $"{{Binding {commandName}}}", StringComparison.Ordinal));

        Assert.Equal(
            $"{{Binding {visibilitySource}, Converter={{StaticResource BooleanToVisibilityConverter}}}}",
            button.Attribute("Visibility")?.Value);
    }

    private static int CountOccurrences(string value, string pattern)
    {
        int count = 0;
        int index = 0;
        while ((index = value.IndexOf(pattern, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += pattern.Length;
        }

        return count;
    }

    private static void AssertImplicitFocusStyle(IReadOnlyCollection<XElement> styles, string targetType)
    {
        XElement style = FindImplicitStyle(styles, targetType);
        XElement? focusSetter = style.Elements()
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
        XElement style = FindImplicitStyle(styles, targetType);
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
        XElement style = FindNamedStyle(styles, styleKey);
        XElement? setter = style.Elements()
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
        XElement style = FindImplicitStyle(styles, targetType);

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
        XName xKey = XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml");
        return styles.Single(element =>
            string.Equals(element.Attribute(xKey)?.Value, styleKey, StringComparison.Ordinal));
    }

    private static void AssertNamedFocusStyle(IReadOnlyCollection<XElement> styles, string styleKey)
    {
        XElement style = FindNamedStyle(styles, styleKey);
        XElement? focusSetter = style.Elements()
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

    private static void AssertDuration(TimeSpan expected, object? actual)
    {
        Duration duration = Assert.IsType<Duration>(actual);
        Assert.Equal(expected, duration.TimeSpan);
    }

}

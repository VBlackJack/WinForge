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
using System.Xml.Linq;

namespace WinForge.GUI.Tests;

/// <summary>
/// Guards implicit focus-visual coverage for concrete WPF control types.
/// </summary>
public class FocusVisualCoverageTests
{
    private const string FocusVisualStyle = "{StaticResource HighVisibilityFocusVisual}";

    [Theory]
    [InlineData("Button", "{StaticResource {x:Type Button}}")]
    [InlineData("CheckBox", "{StaticResource {x:Type CheckBox}}")]
    [InlineData("RadioButton", "{StaticResource {x:Type RadioButton}}")]
    [InlineData("ToggleButton", "{StaticResource {x:Type ToggleButton}}")]
    [InlineData("ListBoxItem", "{StaticResource {x:Type ListBoxItem}}")]
    [InlineData("ui:Button", "{StaticResource {x:Type ui:Button}}")]
    public void AppResources_DefineImplicitFocusVisualStyleForConcreteTargetTypes(
        string targetType,
        string expectedBasedOn)
    {
        XElement style = FindImplicitStyle(targetType);

        Assert.Equal(expectedBasedOn, style.Attribute("BasedOn")?.Value);
        AssertSetter(style, "FocusVisualStyle", FocusVisualStyle);
    }

    [Fact]
    public void HighVisibilityFocusVisual_UsesThemeFocusIndicatorBrush()
    {
        XDocument appXaml = LoadAppXaml();
        XElement focusVisual = appXaml.Descendants()
            .Single(element =>
                element.Name.LocalName == "Style"
                && string.Equals(
                    element.Attribute(XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value,
                    "HighVisibilityFocusVisual",
                    StringComparison.Ordinal));

        XElement focusBorder = focusVisual.Descendants()
            .Single(element => element.Name.LocalName == "Border");

        Assert.Equal("{DynamicResource FocusIndicatorBrush}", focusBorder.Attribute("BorderBrush")?.Value);
        Assert.Equal("2", focusBorder.Attribute("BorderThickness")?.Value);
    }

    private static XElement FindImplicitStyle(string targetType)
    {
        XDocument appXaml = LoadAppXaml();
        return appXaml.Descendants()
            .Single(element =>
                element.Name.LocalName == "Style"
                && string.Equals(element.Attribute("TargetType")?.Value, targetType, StringComparison.Ordinal)
                && element.Attribute(XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml")) is null);
    }

    private static void AssertSetter(XElement style, string property, string expectedValue)
    {
        XElement? setter = style.Elements()
            .SingleOrDefault(element =>
                element.Name.LocalName == "Setter"
                && string.Equals(element.Attribute("Property")?.Value, property, StringComparison.Ordinal));

        Assert.NotNull(setter);
        Assert.Equal(expectedValue, setter.Attribute("Value")?.Value);
    }

    private static XDocument LoadAppXaml()
    {
        return XDocument.Load(FindRepoFile("GUI", "WinForge.GUI", "App.xaml"));
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
            $"Unable to find repository file '{Path.Combine(relativeParts)}' from '{AppContext.BaseDirectory}'.");
    }
}

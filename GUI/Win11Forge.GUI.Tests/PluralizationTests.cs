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
using Loc = Win11Forge.GUI.Resources.Resources;

namespace Win11Forge.GUI.Tests;

/// <summary>
/// Tests for confirmation dialog pluralization resources.
/// </summary>
public class PluralizationTests
{
    [Fact]
    public void UninstallConfirmation_English_UsesDedicatedSingularAndPluralResources()
    {
        Assert.DoesNotContain("{0}", Loc.Confirm_Uninstall_Title_Single);
        Assert.DoesNotContain("{0}", Loc.Confirm_Uninstall_Message_Single);
        Assert.Contains("{0}", Loc.Confirm_Uninstall_Title_Multiple);
        Assert.Contains("{0}", Loc.Confirm_Uninstall_Message_Multiple);
        Assert.DoesNotContain("(s)", Loc.Confirm_Uninstall_Title_Multiple);
        Assert.DoesNotContain("(s)", Loc.Confirm_Uninstall_Message_Multiple);
    }

    [Fact]
    public void UninstallConfirmation_French_UsesDedicatedSingularAndPluralResources()
    {
        CultureInfo culture = CultureInfo.GetCultureInfo("fr");
        string titleSingle = GetString("Confirm_Uninstall_Title_Single", culture);
        string titleMultiple = GetString("Confirm_Uninstall_Title_Multiple", culture);
        string messageSingle = GetString("Confirm_Uninstall_Message_Single", culture);
        string messageMultiple = GetString("Confirm_Uninstall_Message_Multiple", culture);

        Assert.DoesNotContain("{0}", titleSingle);
        Assert.DoesNotContain("{0}", messageSingle);
        Assert.Contains("{0}", titleMultiple);
        Assert.Contains("{0}", messageMultiple);
        Assert.DoesNotContain("(s)", titleMultiple);
        Assert.DoesNotContain("(s)", messageMultiple);
        Assert.Contains("Désinstaller", titleSingle);
        Assert.Contains("applications", titleMultiple);
    }

    [Fact]
    public void ImportConfirmationResources_DoNotDescribeGenericYesNoButtons()
    {
        CultureInfo frenchCulture = CultureInfo.GetCultureInfo("fr");
        string englishMessage = Loc.AppCatalog_ImportModeConfirm;
        string frenchMessage = GetString("AppCatalog_ImportModeConfirm", frenchCulture);

        Assert.DoesNotContain("Yes", englishMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("No", englishMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Oui", frenchMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Non", frenchMessage, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetString(string key, CultureInfo culture)
    {
        return Loc.ResourceManager.GetString(key, culture)
            ?? throw new InvalidOperationException($"Missing resource key '{key}' for '{culture.Name}'.");
    }
}

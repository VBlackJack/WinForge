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
using Xunit.Abstractions;

namespace Win11Forge.GUI.UITests;

public sealed class Win11ForgeUiaSmokeTests
{
    private readonly ITestOutputHelper _output;

    public Win11ForgeUiaSmokeTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [UiaFact]
    public void CanNavigateCoreScreensAndCaptureScreenshots()
    {
        using var app = Win11ForgeAppSession.Launch();
        app.WaitForElementByAutomationId("PageDashboard", TimeSpan.FromSeconds(10));

        var screenshots = new[]
        {
            app.CaptureWindow("01-dashboard"),
            CaptureAfterNavigation(app, "NavApplications", "PageApplications", "02-applications"),
            CaptureAfterNavigation(app, "NavAppCatalog", "PageAppCatalog", "03-app-catalog"),
            CaptureAfterNavigation(app, "NavSettings", "ThemePicker", "04-settings"),
            CaptureAfterNavigation(app, "NavDeployment", "PageDeployment", "05-deployment"),
            CaptureAfterNavigation(app, "NavPrerequisites", "PagePrerequisites", "06-prerequisites")
        };

        foreach (var screenshot in screenshots)
        {
            _output.WriteLine(screenshot);
            Assert.True(File.Exists(screenshot), $"Screenshot was not written: {screenshot}");
            Assert.True(new FileInfo(screenshot).Length > 4096, $"Screenshot appears empty: {screenshot}");
        }
    }

    [UiaFact]
    public void SettingsThemePicker_IsDiscoverable()
    {
        using var app = Win11ForgeAppSession.Launch();

        app.NavigateByAutomationId("NavSettings");
        var themePicker = app.WaitForElementByAutomationId("ThemePicker", TimeSpan.FromSeconds(10));
        var screenshot = app.CaptureWindow("settings-theme-picker");

        _output.WriteLine(screenshot);
        Assert.Equal("ThemePicker", themePicker.Current.AutomationId);
        Assert.True(new FileInfo(screenshot).Length > 4096, $"Screenshot appears empty: {screenshot}");
    }

    private static string CaptureAfterNavigation(
        Win11ForgeAppSession app,
        string automationId,
        string expectedPageAutomationId,
        string screenshotName)
    {
        app.NavigateByAutomationId(automationId);
        app.WaitForElementByAutomationId(expectedPageAutomationId, TimeSpan.FromSeconds(10));
        app.WaitForIdle();
        return app.CaptureWindow(screenshotName);
    }
}

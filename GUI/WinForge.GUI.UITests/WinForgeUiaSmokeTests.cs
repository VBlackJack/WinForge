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
using System.Windows.Automation;
using Xunit.Abstractions;

namespace WinForge.GUI.UITests;

public sealed class WinForgeUiaSmokeTests
{
    private readonly ITestOutputHelper _output;

    public WinForgeUiaSmokeTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [UiaFact]
    public void CanNavigateCoreScreensAndCaptureScreenshots()
    {
        using WinForgeAppSession app = WinForgeAppSession.Launch();
        app.WaitForElementByAutomationId("PageDashboard", TimeSpan.FromSeconds(10));

        string[] screenshots = new[]
        {
            app.CaptureWindow("01-dashboard"),
            CaptureAfterNavigation(app, "NavApplications", "PageApplications", "02-applications"),
            CaptureAfterNavigation(app, "NavAppCatalog", "PageAppCatalog", "03-app-catalog"),
            CaptureAfterNavigation(app, "NavSettings", "ThemePicker", "04-settings"),
            CaptureAfterNavigation(app, "NavDeployment", "PageDeployment", "05-deployment"),
            CaptureAfterNavigation(app, "NavPrerequisites", "PagePrerequisites", "06-prerequisites")
        };

        foreach (string? screenshot in screenshots)
        {
            _output.WriteLine(screenshot);
            Assert.True(File.Exists(screenshot), $"Screenshot was not written: {screenshot}");
            Assert.True(new FileInfo(screenshot).Length > 4096, $"Screenshot appears empty: {screenshot}");
        }
    }

    [UiaFact]
    public void SettingsThemePicker_IsDiscoverable()
    {
        using WinForgeAppSession app = WinForgeAppSession.Launch();
        app.WaitForElementByAutomationId("PageDashboard", TimeSpan.FromSeconds(10));

        app.NavigateByAutomationId("NavSettings");
        AutomationElement themePicker = app.WaitForElementByAutomationId("ThemePicker", TimeSpan.FromSeconds(10));
        string screenshot = app.CaptureWindow("settings-theme-picker");

        _output.WriteLine(screenshot);
        Assert.Equal("ThemePicker", themePicker.Current.AutomationId);
        Assert.True(new FileInfo(screenshot).Length > 4096, $"Screenshot appears empty: {screenshot}");
    }

    [UiaFact]
    public void AppCatalog_UndoRedoButtons_HaveNonEmptyBounds()
    {
        using WinForgeAppSession app = WinForgeAppSession.Launch();
        app.WaitForElementByAutomationId("PageDashboard", TimeSpan.FromSeconds(10));

        app.NavigateByAutomationId("NavAppCatalog");
        app.WaitForElementByAutomationId("PageAppCatalog", TimeSpan.FromSeconds(10));

        AutomationElement undoButton = app.WaitForElementByName("Undo last action", TimeSpan.FromSeconds(10));
        AutomationElement redoButton = app.WaitForElementByName("Redo last action", TimeSpan.FromSeconds(10));

        Assert.True(undoButton.Current.BoundingRectangle.Width > 0, "Undo button should render with visible width.");
        Assert.True(undoButton.Current.BoundingRectangle.Height > 0, "Undo button should render with visible height.");
        Assert.True(redoButton.Current.BoundingRectangle.Width > 0, "Redo button should render with visible width.");
        Assert.True(redoButton.Current.BoundingRectangle.Height > 0, "Redo button should render with visible height.");
    }

    private static string CaptureAfterNavigation(
        WinForgeAppSession app,
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

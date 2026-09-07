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
    public void AppCatalog_UnavailableUndoRedoActionsAreHidden()
    {
        using WinForgeAppSession app = WinForgeAppSession.Launch();
        app.WaitForElementByAutomationId("PageDashboard", TimeSpan.FromSeconds(10));

        app.NavigateByAutomationId("NavAppCatalog");
        app.WaitForElementByAutomationId("PageAppCatalog", TimeSpan.FromSeconds(10));

        AutomationElement? undoButton = app.MainWindow.FindFirst(TreeScope.Descendants,
            new PropertyCondition(AutomationElement.NameProperty, "Undo last action"));
        AutomationElement? redoButton = app.MainWindow.FindFirst(TreeScope.Descendants,
            new PropertyCondition(AutomationElement.NameProperty, "Redo last action"));
        Assert.Null(undoButton);
        Assert.Null(redoButton);
    }

    [UiaFact]
    public void ProfileDialog_InvalidNameDisablesSaveAndCancelCloses()
    {
        using WinForgeAppSession app = WinForgeAppSession.Launch();
        app.NavigateByAutomationId("NavApplications");
        AutomationElement selectAll = app.WaitForElementByName("Select All", TimeSpan.FromSeconds(10));
        ((InvokePattern)selectAll.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
        AutomationElement open = app.WaitForElementByName("Save Profile", TimeSpan.FromSeconds(10));
        ((InvokePattern)open.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
        AutomationElement createNew = app.WaitForElementByAutomationId("CreateNewRadio", TimeSpan.FromSeconds(10));
        ((SelectionItemPattern)createNew.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
        AutomationElement name = app.WaitForElementByAutomationId("SaveProfileName", TimeSpan.FromSeconds(10));
        ((ValuePattern)name.GetCurrentPattern(ValuePattern.Pattern)).SetValue("../outside");
        app.WaitForIdle();
        Assert.False(app.WaitForElementByAutomationId("SaveProfileSave", TimeSpan.FromSeconds(10)).Current.IsEnabled);
        app.CaptureWindow("profile-invalid-name");
        app.NavigateByAutomationId("SaveProfileCancel");
        AutomationElement? remaining = app.MainWindow.FindFirst(TreeScope.Descendants,
            new PropertyCondition(AutomationElement.AutomationIdProperty, "SaveProfileCancel"));
        Assert.Null(remaining);
    }

    [UiaFact]
    public void DeploymentPreview_IsReadableAndKeyboardFocusable()
    {
        using WinForgeAppSession app = WinForgeAppSession.Launch();
        ((WindowPattern)app.MainWindow.GetCurrentPattern(WindowPattern.Pattern)).SetWindowVisualState(WindowVisualState.Normal);
        ((TransformPattern)app.MainWindow.GetCurrentPattern(TransformPattern.Pattern)).Resize(1100, 800);
        app.NavigateByAutomationId("NavApplications");
        app.WaitForElementByAutomationId("PageApplications", TimeSpan.FromSeconds(10));
        AutomationElement clear = app.WaitForElementByName("Clear Selection", TimeSpan.FromSeconds(10));
        ((InvokePattern)clear.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
        AutomationElement selection = app.MainWindow.FindFirst(TreeScope.Descendants,
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.CheckBox));
        Assert.NotNull(selection);
        ((TogglePattern)selection.GetCurrentPattern(TogglePattern.Pattern)).Toggle();
        AutomationElement preview = app.WaitForElementByAutomationId("PreviewSelected", TimeSpan.FromSeconds(10));
        Assert.True(app.MainWindow.Current.BoundingRectangle.Contains(preview.Current.BoundingRectangle));
        app.NavigateByAutomationId("PreviewSelected");
        AutomationElement report = app.WaitForElementByAutomationId("DeploymentReportText", TimeSpan.FromSeconds(90));
        Assert.False(report.Current.IsOffscreen);
        Assert.True(report.Current.IsKeyboardFocusable);
        ValuePattern value = (ValuePattern)report.GetCurrentPattern(ValuePattern.Pattern);
        Assert.True(value.Current.IsReadOnly);
        Assert.Contains("Read-only preview", value.Current.Value);
        Assert.Contains("Installation tested", value.Current.Value);
        Assert.Contains("Not measured", value.Current.Value);
        report.SetFocus();
        app.CaptureWindow("deployment-preview");
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

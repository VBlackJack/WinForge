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
using Microsoft.Extensions.DependencyInjection;
using Win11Forge.GUI.Messages;
using Win11Forge.GUI.Services;
using Win11Forge.GUI.Tests.TestInfrastructure;
using Win11Forge.GUI.ViewModels;

namespace Win11Forge.GUI.Tests;

public class MainWindowLogsNavigationTests
{
    [Fact]
    public void LogsView_IsWiredIntoNavigationAndServices()
    {
        Assert.Equal(6, (int)ViewIndex.Logs);

        string navigateMessage = File.ReadAllText(RepositoryPathHelper.FindFile(
            "GUI", "Win11Forge.GUI", "Messages", "NavigateMessage.cs"));
        string mainWindowXaml = File.ReadAllText(RepositoryPathHelper.FindFile(
            "GUI", "Win11Forge.GUI", "MainWindow.xaml"));
        string mainWindowCode = File.ReadAllText(RepositoryPathHelper.FindFile(
            "GUI", "Win11Forge.GUI", "MainWindow.xaml.cs"));
        string mainWindowViewModelSource = File.ReadAllText(RepositoryPathHelper.FindFile(
            "GUI", "Win11Forge.GUI", "ViewModels", "MainWindowViewModel.cs"));
        string serviceRegistrations = File.ReadAllText(RepositoryPathHelper.FindFile(
            "GUI", "Win11Forge.GUI", "Services", "ServiceCollectionExtensions.cs"));

        Assert.Contains("public const int Logs = 6;", navigateMessage, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.AutomationId=\"NavLogs\"", mainWindowXaml, StringComparison.Ordinal);
        Assert.Contains("Tag=\"6\"", mainWindowXaml, StringComparison.Ordinal);
        Assert.Contains("CommandParameter=\"6\"", mainWindowXaml, StringComparison.Ordinal);
        Assert.Contains("ViewIndex.Logs => new LogsView { DataContext = _viewModel?.LogsViewModel }", mainWindowCode, StringComparison.Ordinal);
        Assert.Contains("TryGetNavigationIndex(item, out int itemIndex)", mainWindowCode, StringComparison.Ordinal);
        Assert.Contains("LogsViewModel logsViewModel", mainWindowViewModelSource, StringComparison.Ordinal);
        Assert.Contains("public LogsViewModel LogsViewModel { get; }", mainWindowViewModelSource, StringComparison.Ordinal);
        Assert.Contains("services.AddTransient<LogsViewModel>();", serviceRegistrations, StringComparison.Ordinal);

        using ServiceProvider provider = new ServiceCollection().AddWin11ForgeServices().BuildServiceProvider();
        MainWindowViewModel mainWindowViewModel = provider.GetRequiredService<MainWindowViewModel>();
        LogsViewModel logsViewModel = provider.GetRequiredService<LogsViewModel>();

        Assert.NotNull(mainWindowViewModel.LogsViewModel);
        Assert.NotNull(logsViewModel);
    }
}

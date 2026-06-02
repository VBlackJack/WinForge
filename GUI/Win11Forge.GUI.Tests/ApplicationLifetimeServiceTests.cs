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

using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Win11Forge.GUI.Services;

namespace Win11Forge.GUI.Tests;

/// <summary>
/// Tests for application lifetime service registration and contract shape.
/// </summary>
public class ApplicationLifetimeServiceTests
{
    /// <summary>
    /// Verifies the concrete lifetime service implements the expected interface.
    /// </summary>
    [Fact]
    public void ApplicationLifetimeService_ShouldImplementInterface()
    {
        // Arrange & act
        ApplicationLifetimeService service = new ApplicationLifetimeService();

        // Assert
        Assert.IsAssignableFrom<IApplicationLifetimeService>(service);
    }

    /// <summary>
    /// Verifies the lifetime service keeps the synchronous minimal API required by the ADR.
    /// </summary>
    [Fact]
    public void RequestShutdown_ShouldRemainSynchronousMinimalApi()
    {
        // Act
        MethodInfo? method = typeof(IApplicationLifetimeService).GetMethod(nameof(IApplicationLifetimeService.RequestShutdown));

        // Assert
        Assert.NotNull(method);
        Assert.Equal(typeof(void), method.ReturnType);
        ParameterInfo parameter = Assert.Single(method.GetParameters());
        Assert.Equal(typeof(int), parameter.ParameterType);
        Assert.True(parameter.HasDefaultValue);
        Assert.Equal(0, parameter.DefaultValue);
    }

    /// <summary>
    /// Verifies application lifetime services are registered as singleton services.
    /// </summary>
    [Fact]
    public void AddWin11ForgeServices_ShouldRegisterApplicationLifetimeSingleton()
    {
        // Arrange
        ServiceCollection services = new ServiceCollection();

        // Act
        using ServiceProvider provider = services.AddWin11ForgeServices().BuildServiceProvider();
        IApplicationLifetimeService first = provider.GetRequiredService<IApplicationLifetimeService>();
        IApplicationLifetimeService second = provider.GetRequiredService<IApplicationLifetimeService>();

        // Assert
        Assert.IsType<ApplicationLifetimeService>(first);
        Assert.Same(first, second);
    }

    /// <summary>
    /// Verifies the process launcher is also registered as a singleton dependency.
    /// </summary>
    [Fact]
    public void AddWin11ForgeServices_ShouldRegisterProcessLauncherSingleton()
    {
        // Arrange
        ServiceCollection services = new ServiceCollection();

        // Act
        using ServiceProvider provider = services.AddWin11ForgeServices().BuildServiceProvider();
        IProcessLauncher first = provider.GetRequiredService<IProcessLauncher>();
        IProcessLauncher second = provider.GetRequiredService<IProcessLauncher>();

        // Assert
        Assert.IsType<ProcessLauncher>(first);
        Assert.Same(first, second);
    }

    /// <summary>
    /// Verifies the process launcher exposes a single void Start method.
    /// </summary>
    [Fact]
    public void ProcessLauncher_ShouldExposeSingleVoidStartMethod()
    {
        // Act
        MethodInfo? method = typeof(IProcessLauncher).GetMethod(nameof(IProcessLauncher.Start));

        // Assert
        Assert.NotNull(method);
        Assert.Equal(typeof(void), method!.ReturnType);
        ParameterInfo parameter = Assert.Single(method.GetParameters());
        Assert.Equal(typeof(ProcessStartInfo), parameter.ParameterType);
    }

    /// <summary>
    /// Verifies the process launcher rejects null process start information.
    /// </summary>
    [Fact]
    public void ProcessLauncher_ShouldRejectNullStartInfo()
    {
        // Arrange
        ProcessLauncher launcher = new ProcessLauncher();

        // Act & assert
        Assert.Throws<ArgumentNullException>(() => launcher.Start(null!));
    }
}

/// <summary>
/// Test double for IApplicationLifetimeService.
/// </summary>
internal sealed class MockApplicationLifetimeService : IApplicationLifetimeService
{
    public int RequestShutdownCallCount { get; private set; }
    public int? LastExitCode { get; private set; }

    public void RequestShutdown(int exitCode = 0)
    {
        RequestShutdownCallCount++;
        LastExitCode = exitCode;
    }
}

/// <summary>
/// Test double for IProcessLauncher.
/// </summary>
internal sealed class MockProcessLauncher : IProcessLauncher
{
    public int StartCallCount { get; private set; }
    public ProcessStartInfo? LastStartInfo { get; private set; }

    public void Start(ProcessStartInfo startInfo)
    {
        StartCallCount++;
        LastStartInfo = startInfo;
    }
}

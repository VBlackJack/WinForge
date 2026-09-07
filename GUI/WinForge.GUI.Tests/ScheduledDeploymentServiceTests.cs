// Copyright 2026 Julien Bombled. Licensed under the Apache License, Version 2.0.
using Moq;
using WinForge.GUI.Models;
using WinForge.GUI.Services;
using WinForge.GUI.Services.PowerShell;

namespace WinForge.GUI.Tests;

public sealed class ScheduledDeploymentServiceTests
{
    [Fact]
    public async Task SchedulingPassesUserProfilesBeforePackagedDefaults()
    {
        Mock<IPowerShellBridge> bridge = new();
        Mock<IRepositoryPathService> paths = new();
        bridge.SetupGet(value => value.RepositoryRoot).Returns(@"C:\WinForge");
        paths.SetupGet(value => value.UserProfilesDirectory).Returns(@"C:\User's Data\Profiles");
        paths.SetupGet(value => value.DefaultProfilesDirectory).Returns(@"C:\WinForge\Profiles\Defaults");
        string? command = null;
        bridge.Setup(value => value.ExecuteCommandAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((script, _) => command = script)
            .ReturnsAsync("{\"Success\":true,\"Id\":\"fixture\"}");
        ScheduledDeploymentService service = new(bridge.Object, paths.Object);

        string? id = await service.CreateScheduledDeploymentAsync("Office", DateTime.Today.AddDays(1), ScheduledTriggerType.OneTime);

        Assert.Equal("fixture", id);
        Assert.Contains(@"-ProfileDirectories @('C:\User''s Data\Profiles', 'C:\WinForge\Profiles\Defaults')", command);
    }
}

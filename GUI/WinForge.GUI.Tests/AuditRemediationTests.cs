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
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Moq;
using WinForge.GUI.Helpers;
using WinForge.GUI.Services;
using WinForge.GUI.Services.Implementations;
using WinForge.GUI.Services.PowerShell;
using WinForge.GUI.ViewModels;

namespace WinForge.GUI.Tests;

public sealed class AuditRemediationTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "WinForge-tests-" + Guid.NewGuid().ToString("N"));

    public AuditRemediationTests() => Directory.CreateDirectory(_directory);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProfileEditorsPreserveSystemConfigAndUnknownProperties(bool applicationsEditor)
    {
        string path = Path.Combine(_directory, "Probe.json");
        const string source = """
            {"Name":"Probe","Description":"Keep","Version":"1","Inherits":[],"Applications":["old"],
             "SystemConfig":{"Privacy":{"DisableTelemetry":true}},"Custom":{"Nested":[1,null,"value"]}}
            """;
        await File.WriteAllTextAsync(path, source);
        Mock<IRepositoryPathService> paths = new Mock<IRepositoryPathService>();
        paths.SetupGet(service => service.UserProfilesDirectory).Returns(_directory);
        paths.SetupGet(service => service.DefaultProfilesDirectory).Returns(_directory);
        if (applicationsEditor)
        {
            AppsViewModel model = (AppsViewModel)RuntimeHelpers.GetUninitializedObject(typeof(AppsViewModel));
            typeof(AppsViewModel).GetField("_pathService", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(model, paths.Object);
            Task read = (Task)typeof(AppsViewModel).GetMethod("ReadProfileEditSnapshotAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(model, ["Probe"])!;
            await read;
            object snapshot = read.GetType().GetProperty("Result")!.GetValue(read)!;
            await (Task)typeof(AppsViewModel).GetMethod("WriteProfileEditSnapshotAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(model, [snapshot, new List<string> { "new" }])!;
        }
        else
        {
            Mock<IVersionService> versions = new Mock<IVersionService>();
            versions.Setup(service => service.GetWinForgeVersionAsync()).ReturnsAsync("2026081201");
            ProfileManagementServiceImpl service = new ProfileManagementServiceImpl(paths.Object,
                Mock.Of<IPowerShellExecutionService>(), Mock.Of<IApplicationCacheService>(), versions.Object);
            await service.SaveProfileAsync("Probe", "Updated", null, ["new"]);
        }
        using JsonDocument before = JsonDocument.Parse(source);
        using JsonDocument after = JsonDocument.Parse(await File.ReadAllTextAsync(path));
        foreach (string name in new[] { "SystemConfig", "Custom" })
        {
            Assert.True(JsonElement.DeepEquals(before.RootElement.GetProperty(name), after.RootElement.GetProperty(name)));
        }
        Assert.Equal("new", after.RootElement.GetProperty("Applications")[0].GetString());
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
    }

    [Fact]
    public void InvalidExistingJsonIsNotOverwritten()
    {
        string path = Path.Combine(_directory, "Probe.json");
        File.WriteAllText(path, "{invalid");
        Assert.ThrowsAny<JsonException>(() => ProfileJsonWriter.Write(path, new Dictionary<string, object> { ["Applications"] = Array.Empty<string>() }));
        Assert.Equal("{invalid", File.ReadAllText(path));
    }

    [Fact]
    public async Task VendorTimeoutInterruptsOpenOutputPipes()
    {
        Mock<IPowerShellExecutionService> execution = new Mock<IPowerShellExecutionService>();
        execution.SetupGet(service => service.InstallationTimeoutMs).Returns(300);
        VendorCommandRunner runner = new VendorCommandRunner(execution.Object, Mock.Of<IRepositoryPathService>(), Mock.Of<ILoggingService>());
        string shell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe");
        Stopwatch watch = Stopwatch.StartNew();
        VendorCommandResult result = await runner.RunAsync(shell,
            "-NoProfile -NonInteractive -Command Start-Sleep -Seconds 10", "Timeout regression", new StringBuilder());
        Assert.False(result.Success);
        Assert.Contains("timed out", result.Output);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), $"Timeout took {watch.Elapsed}.");
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}

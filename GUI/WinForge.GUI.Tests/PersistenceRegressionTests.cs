// Copyright 2026 Julien Bombled
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using System.IO;
using System.Text.Json;
using WinForge.GUI.Helpers;
using WinForge.GUI.Models;
using WinForge.GUI.Services;
using WinForge.GUI.Services.PowerShell;
using WinForge.GUI.Services.Resume;
using WinForge.GUI.ViewModels;

namespace WinForge.GUI.Tests;

public sealed class PersistenceRegressionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "WinForge.Persistence", Guid.NewGuid().ToString("N"));

    public PersistenceRegressionTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Theory]
    [InlineData("../outside")]
    [InlineData("C:\\outside")]
    [InlineData("..\\outside")]
    [InlineData("")]
    public void SaveDialog_RejectsInvalidNames(string name)
    {
        SaveProfileDialogViewModel dialog = new(null, [], 1) { NewProfileName = name };
        Assert.False(dialog.CanSave);
        Assert.Throws<InvalidOperationException>(() => dialog.GetResult());
        dialog.Close(true);
        Assert.False(dialog.Completion.IsCompleted);
    }

    [Fact]
    public async Task SaveDialog_CancelCannotBecomeSave()
    {
        SaveProfileDialogViewModel dialog = new(null, [], 1) { NewProfileName = "Valid" };
        dialog.Close(false);
        dialog.Close(true);
        Assert.Null(await dialog.Completion);
    }

    [Fact]
    public async Task SaveDialog_SaveReturnsValidatedSelection()
    {
        SaveProfileDialogViewModel dialog = new(null, ["Base"], 1) { NewProfileName = " Valid ", SelectedParent = "Base" };
        dialog.Close(true);
        SaveProfileResult? result = await dialog.Completion;
        Assert.Equal("Valid", result!.ProfileName);
        Assert.Equal("Base", result.ParentProfile);
    }

    [Theory]
    [InlineData("[{\"profileName\":\"Existing\"},")]
    [InlineData("null")]
    [InlineData("")]
    public async Task History_InvalidJsonIsPreserved(string original)
    {
        string path = Path.Combine(_root, "history.json");
        await File.WriteAllTextAsync(path, original);
        using DeploymentHistoryService service = new(path);
        await Assert.ThrowsAsync<JsonException>(() => service.AddEntryAsync(new() { ProfileName = "New" }));
        Assert.Equal(original, await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task History_LockedDestinationPreservesPreviousEntryAndReportsFailure()
    {
        string path = Path.Combine(_root, "history.json");
        using DeploymentHistoryService service = new(path);
        await service.AddEntryAsync(new() { ProfileName = "Existing" });
        byte[] original = await File.ReadAllBytesAsync(path);
        using (FileStream locked = new(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Exception? writeError = await Record.ExceptionAsync(() => service.AddEntryAsync(new() { ProfileName = "New" }));
            Assert.True(writeError is IOException or UnauthorizedAccessException);
            await Assert.ThrowsAnyAsync<IOException>(() => service.ClearHistoryAsync());
        }
        Assert.Equal(original, await File.ReadAllBytesAsync(path));
        Assert.Empty(Directory.GetFiles(_root, "*.tmp"));
        await service.AddEntryAsync(new() { ProfileName = "New" });
        Assert.Equal(2, (await service.GetHistoryAsync()).Count);
    }

    [Fact]
    public async Task Checkpoint_BlockedDirectoryReportsFailure()
    {
        string path = Path.Combine(_root, "state");
        await File.WriteAllTextAsync(path, "keep");
        BatchResumeService service = new(path, TimeSpan.FromDays(14), () => DateTimeOffset.UtcNow);
        await Assert.ThrowsAnyAsync<IOException>(() => service.BeginBatchAsync(BatchOperationKind.Install, ["App"], new(false)));
        Assert.Equal("keep", await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task Checkpoint_LockedFileCannotBeDiscardedOrUpdated()
    {
        BatchResumeService service = new(_root, TimeSpan.FromDays(14), () => DateTimeOffset.UtcNow);
        Guid id = await service.BeginBatchAsync(BatchOperationKind.Install, ["App"], new(false));
        string path = Path.Combine(_root, $"batch-{id:D}.json");
        byte[] original = await File.ReadAllBytesAsync(path);
        using (FileStream locked = new(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            await Assert.ThrowsAnyAsync<IOException>(() => service.DeleteCheckpointAsync(id));
            Exception? writeError = await Record.ExceptionAsync(() => service.MarkBatchCompletedAsync(id));
            Assert.True(writeError is IOException or UnauthorizedAccessException);
        }
        Assert.Equal(original, await File.ReadAllBytesAsync(path));
        Assert.Empty(Directory.GetFiles(_root, "*.tmp"));
        await service.DeleteCheckpointAsync(id);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void Profile_NewNameCollisionDoesNotOverwriteExistingConfiguration()
    {
        string path = Path.Combine(_root, "Existing.json");
        const string original = "{\"SystemConfig\":{\"Enabled\":true},\"Version\":\"7\"}";
        File.WriteAllText(path, original);
        Assert.Throws<IOException>(() => ProfileJsonWriter.Write(path, new Dictionary<string, object> { ["Name"] = "New" }, overwrite: false));
        Assert.Equal(original, File.ReadAllText(path));
        ProfileJsonWriter.Write(path, new Dictionary<string, object> { ["Name"] = "Existing" });
        using JsonDocument saved = JsonDocument.Parse(File.ReadAllText(path));
        Assert.True(saved.RootElement.GetProperty("SystemConfig").GetProperty("Enabled").GetBoolean());
        Assert.Equal("7", saved.RootElement.GetProperty("Version").GetString());
    }

    [Fact]
    public async Task PowerShell_SilentProcessCancellationDoesNotWaitForExit()
    {
        PowerShellExecutionService service = new(new RepositoryPathService());
        using CancellationTokenSource cancellation = new(TimeSpan.FromSeconds(1));
        Stopwatch timer = Stopwatch.StartNew();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ExecutePowerShellScriptAsync("Start-Sleep -Seconds 10", cancellation.Token));
        Assert.True(timer.Elapsed < TimeSpan.FromSeconds(6), $"Cancellation took {timer.Elapsed}.");
    }

    [Theory]
    [InlineData("../outside")]
    [InlineData("C:\\outside")]
    public void RepositoryPaths_RejectEscapingComponents(string component)
    {
        RepositoryPathService service = new(_root, [Path.Combine(_root, "data")]);
        Assert.Throws<ArgumentException>(() => service.GetPath(component));
        Assert.Throws<ArgumentException>(() => service.GetUserDataPath(component));
        Assert.Equal(Path.Combine(_root, "Config", "version.json"), service.GetPath("Config", "version.json"));
    }
}

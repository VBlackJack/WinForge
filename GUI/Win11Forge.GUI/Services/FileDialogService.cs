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

using Microsoft.Win32;

namespace Win11Forge.GUI.Services;

/// <summary>
/// Native Microsoft.Win32 file dialog service.
/// </summary>
public sealed class FileDialogService : IFileDialogService
{
    private readonly Func<IFileDialogAdapter> _openDialogFactory;
    private readonly Func<IFileDialogAdapter> _saveDialogFactory;

    /// <summary>
    /// Initializes a new instance of <see cref="FileDialogService"/>.
    /// </summary>
    public FileDialogService()
        : this(
            () => new OpenFileDialogAdapter(new OpenFileDialog()),
            () => new SaveFileDialogAdapter(new SaveFileDialog()))
    {
    }

    internal FileDialogService(
        Func<IFileDialogAdapter> openDialogFactory,
        Func<IFileDialogAdapter> saveDialogFactory)
    {
        _openDialogFactory = openDialogFactory ?? throw new ArgumentNullException(nameof(openDialogFactory));
        _saveDialogFactory = saveDialogFactory ?? throw new ArgumentNullException(nameof(saveDialogFactory));
    }

    /// <inheritdoc/>
    public Task<string?> ShowOpenAsync(FileDialogOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var dialog = _openDialogFactory();
        ApplyOptions(dialog, options);

        return Task.FromResult(dialog.ShowDialog() == true ? dialog.FileName : null);
    }

    /// <inheritdoc/>
    public Task<string?> ShowSaveAsync(FileDialogOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var dialog = _saveDialogFactory();
        ApplyOptions(dialog, options);

        return Task.FromResult(dialog.ShowDialog() == true ? dialog.FileName : null);
    }

    private static void ApplyOptions(IFileDialogAdapter dialog, FileDialogOptions options)
    {
        dialog.Title = options.Title;
        dialog.Filter = options.Filter;

        if (!string.IsNullOrEmpty(options.DefaultExtension))
        {
            dialog.DefaultExt = options.DefaultExtension;
        }

        if (!string.IsNullOrEmpty(options.DefaultFileName))
        {
            dialog.FileName = options.DefaultFileName;
        }

        if (!string.IsNullOrEmpty(options.InitialDirectory))
        {
            dialog.InitialDirectory = options.InitialDirectory;
        }
    }
}

internal interface IFileDialogAdapter
{
    string Title { get; set; }

    string Filter { get; set; }

    string DefaultExt { get; set; }

    string FileName { get; set; }

    string InitialDirectory { get; set; }

    bool? ShowDialog();
}

internal sealed class OpenFileDialogAdapter(OpenFileDialog dialog) : IFileDialogAdapter
{
    public string Title
    {
        get => dialog.Title;
        set => dialog.Title = value;
    }

    public string Filter
    {
        get => dialog.Filter;
        set => dialog.Filter = value;
    }

    public string DefaultExt
    {
        get => dialog.DefaultExt;
        set => dialog.DefaultExt = value;
    }

    public string FileName
    {
        get => dialog.FileName;
        set => dialog.FileName = value;
    }

    public string InitialDirectory
    {
        get => dialog.InitialDirectory;
        set => dialog.InitialDirectory = value;
    }

    public bool? ShowDialog() => dialog.ShowDialog();
}

internal sealed class SaveFileDialogAdapter(SaveFileDialog dialog) : IFileDialogAdapter
{
    public string Title
    {
        get => dialog.Title;
        set => dialog.Title = value;
    }

    public string Filter
    {
        get => dialog.Filter;
        set => dialog.Filter = value;
    }

    public string DefaultExt
    {
        get => dialog.DefaultExt;
        set => dialog.DefaultExt = value;
    }

    public string FileName
    {
        get => dialog.FileName;
        set => dialog.FileName = value;
    }

    public string InitialDirectory
    {
        get => dialog.InitialDirectory;
        set => dialog.InitialDirectory = value;
    }

    public bool? ShowDialog() => dialog.ShowDialog();
}

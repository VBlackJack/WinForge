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

using System.Windows;
using Win11Forge.GUI.Models;
using Win11Forge.GUI.Views.Dialogs;
using Loc = Win11Forge.GUI.Resources.Resources;

namespace Win11Forge.GUI.Services;

/// <summary>
/// WPF implementation of <see cref="IApplicationEditorDialogService"/>.
/// </summary>
public sealed class ApplicationEditorDialogService : IApplicationEditorDialogService
{
    private readonly Func<object?> _ownerProvider;
    private readonly Func<object, Task<EditableApplicationModel?>> _showAddDialogAsync;
    private readonly Func<object, EditableApplicationModel, Task<EditableApplicationModel?>> _showEditDialogAsync;

    /// <summary>
    /// Initializes a new instance of <see cref="ApplicationEditorDialogService"/>.
    /// </summary>
    public ApplicationEditorDialogService()
        : this(
            () => Application.Current?.MainWindow,
            owner => ApplicationEditorDialog.ShowAddDialogAsync((Window)owner),
            (owner, application) => ApplicationEditorDialog.ShowEditDialogAsync((Window)owner, application))
    {
    }

    internal ApplicationEditorDialogService(
        Func<object?> ownerProvider,
        Func<object, Task<EditableApplicationModel?>> showAddDialogAsync,
        Func<object, EditableApplicationModel, Task<EditableApplicationModel?>> showEditDialogAsync)
    {
        _ownerProvider = ownerProvider ?? throw new ArgumentNullException(nameof(ownerProvider));
        _showAddDialogAsync = showAddDialogAsync ?? throw new ArgumentNullException(nameof(showAddDialogAsync));
        _showEditDialogAsync = showEditDialogAsync ?? throw new ArgumentNullException(nameof(showEditDialogAsync));
    }

    /// <inheritdoc/>
    public Task<EditableApplicationModel?> ShowAddDialogAsync()
    {
        var owner = GetOwner();
        return _showAddDialogAsync(owner);
    }

    /// <inheritdoc/>
    public Task<EditableApplicationModel?> ShowEditDialogAsync(EditableApplicationModel application)
    {
        ArgumentNullException.ThrowIfNull(application);

        var owner = GetOwner();
        return _showEditDialogAsync(owner, application);
    }

    private object GetOwner()
    {
        return _ownerProvider() ?? throw new InvalidOperationException(Loc.AppDb_EditorOwnerNotFound);
    }
}

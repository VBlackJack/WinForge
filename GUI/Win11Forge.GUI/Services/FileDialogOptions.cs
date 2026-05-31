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

namespace Win11Forge.GUI.Services;

/// <summary>
/// Options used to configure native file dialogs.
/// </summary>
public sealed record FileDialogOptions(
    string Title,
    string Filter,
    string? DefaultFileName = null,
    string? InitialDirectory = null,
    string? DefaultExtension = null);

/// <summary>
/// Shared native file dialog filters.
/// </summary>
public static class FileDialogFilters
{
    public const string Json = "JSON files (*.json)|*.json|All files (*.*)|*.*";
    public const string JsonOnly = "JSON files (*.json)|*.json";
    public const string JsonDefaultExtension = ".json";
}

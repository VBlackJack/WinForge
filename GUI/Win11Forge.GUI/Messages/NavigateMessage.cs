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

using Win11Forge.GUI.ViewModels;

namespace Win11Forge.GUI.Messages;

/// <summary>
/// Navigation view indices.
/// </summary>
public enum ViewIndex
{
    Dashboard = 0,
    Prerequisites = 1,
    Apps = 2,
    Deployment = 3,
    Settings = 4,
    AppCatalog = 5
}

/// <summary>
/// Message to request navigation to a specific view.
/// </summary>
public class NavigateMessage
{
    /// <summary>
    /// Alias for ViewIndex enum for backward compatibility.
    /// </summary>
    [Obsolete("Use Win11Forge.GUI.Messages.ViewIndex enum directly")]
    public static class ViewIndex
    {
        public const int Dashboard = 0;
        public const int Prerequisites = 1;
        public const int Apps = 2;
        public const int Deployment = 3;
        public const int Settings = 4;
        public const int AppCatalog = 5;
    }

    /// <summary>
    /// The view index to navigate to.
    /// </summary>
    public int TargetViewIndex { get; }

    /// <summary>
    /// Initializes a new instance of NavigateMessage with enum.
    /// </summary>
    public NavigateMessage(Messages.ViewIndex targetView)
    {
        TargetViewIndex = (int)targetView;
    }

    /// <summary>
    /// Initializes a new instance of NavigateMessage with integer index.
    /// </summary>
    [Obsolete("Use NavigateMessage(ViewIndex) constructor instead")]
    public NavigateMessage(int targetViewIndex)
    {
        TargetViewIndex = targetViewIndex;
    }
}

/// <summary>
/// Message to request applying a status filter in the Apps view.
/// </summary>
public class ApplyFilterMessage
{
    /// <summary>
    /// The status filter to apply.
    /// </summary>
    public StatusFilterOption Filter { get; }

    /// <summary>
    /// Whether to trigger a scan after applying the filter.
    /// </summary>
    public bool TriggerScan { get; }

    /// <summary>
    /// Initializes a new instance of ApplyFilterMessage.
    /// </summary>
    public ApplyFilterMessage(StatusFilterOption filter, bool triggerScan = false)
    {
        Filter = filter;
        TriggerScan = triggerScan;
    }
}

/// <summary>
/// Message to request a scan in the Apps view from another view (e.g., Dashboard).
/// </summary>
public class TriggerScanMessage
{
    /// <summary>
    /// Callback to report scan progress.
    /// </summary>
    public Action<int, int>? ProgressCallback { get; }

    /// <summary>
    /// Callback to report scan completion with update count.
    /// </summary>
    public Action<int>? CompletionCallback { get; }

    /// <summary>
    /// Initializes a new instance of TriggerScanMessage.
    /// </summary>
    public TriggerScanMessage(Action<int, int>? progressCallback = null, Action<int>? completionCallback = null)
    {
        ProgressCallback = progressCallback;
        CompletionCallback = completionCallback;
    }
}

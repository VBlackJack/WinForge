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

#nullable enable

namespace WinForge.GUI.Models;

/// <summary>
/// Represents the current state of the Dashboard hero section.
/// Used to drive the UI display and available actions.
/// </summary>
public enum DashboardState
{
    /// <summary>
    /// Initial state while loading system information and checking prerequisites.
    /// Displays a spinner with progress message.
    /// </summary>
    Checking,

    /// <summary>
    /// One or more prerequisites are missing.
    /// Displays an alert with a button to navigate to Prerequisites view.
    /// </summary>
    NeedPrereqs,

    /// <summary>
    /// All prerequisites are installed and no updates are available.
    /// Displays success state with the main CTA button.
    /// </summary>
    Ready,

    /// <summary>
    /// All prerequisites are installed and updates are available.
    /// Displays info state with both CTA and View Updates buttons.
    /// </summary>
    HasUpdates
}

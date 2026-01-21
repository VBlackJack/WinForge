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

namespace Win11Forge.GUI.Services;

/// <summary>
/// Service for accessibility features including screen reader announcements.
/// </summary>
public interface IAccessibilityService
{
    /// <summary>
    /// Announces a message to screen readers.
    /// </summary>
    /// <param name="message">The message to announce</param>
    /// <param name="priority">Announcement priority</param>
    void Announce(string message, AnnouncementPriority priority = AnnouncementPriority.Polite);

    /// <summary>
    /// Announces a status change (e.g., "Installation complete").
    /// </summary>
    /// <param name="status">The status message</param>
    void AnnounceStatus(string status);

    /// <summary>
    /// Announces progress updates.
    /// </summary>
    /// <param name="current">Current progress value</param>
    /// <param name="total">Total value</param>
    /// <param name="itemName">Optional name of current item</param>
    void AnnounceProgress(int current, int total, string? itemName = null);
}

/// <summary>
/// Priority level for screen reader announcements.
/// </summary>
public enum AnnouncementPriority
{
    /// <summary>
    /// Polite announcement - waits for current speech to finish.
    /// Use for status updates that are not time-critical.
    /// </summary>
    Polite,

    /// <summary>
    /// Assertive announcement - interrupts current speech.
    /// Use for important updates that require immediate attention.
    /// </summary>
    Assertive
}

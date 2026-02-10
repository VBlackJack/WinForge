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

using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace Win11Forge.GUI.Services;

/// <summary>
/// Implementation of accessibility service using WPF UI Automation.
/// Provides screen reader announcements through a live region TextBlock.
/// </summary>
public class AccessibilityService : IAccessibilityService
{
    private TextBlock? _liveRegion;
    private int _lastProgressAnnouncement;
    private DateTime _lastProgressTime = DateTime.MinValue;

    /// <summary>
    /// Initializes the accessibility service with a live region element.
    /// Call this from the main window after it loads.
    /// </summary>
    /// <param name="liveRegion">The TextBlock to use for announcements</param>
    public void Initialize(TextBlock liveRegion)
    {
        _liveRegion = liveRegion;
    }

    /// <inheritdoc/>
    public void Announce(string message, AnnouncementPriority priority = AnnouncementPriority.Polite)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            if (_liveRegion == null)
                return;

            // Set the live setting based on priority
            var liveSetting = priority == AnnouncementPriority.Assertive
                ? AutomationLiveSetting.Assertive
                : AutomationLiveSetting.Polite;

            System.Windows.Automation.AutomationProperties.SetLiveSetting(_liveRegion, liveSetting);

            // Clear and set the text to trigger announcement
            _liveRegion.Text = string.Empty;
            _liveRegion.Text = message;

            // Raise automation event for screen readers
            var peer = UIElementAutomationPeer.FromElement(_liveRegion);
            peer?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
        });
    }

    /// <inheritdoc/>
    public void AnnounceStatus(string status)
    {
        Announce(status, AnnouncementPriority.Polite);
    }

    /// <inheritdoc/>
    public void AnnounceProgress(int current, int total, string? itemName = null)
    {
        if (total <= 0)
            return;

        // Only announce at 10% intervals to avoid spamming screen readers
        var percentComplete = (int)((double)current / total * 100);
        var interval = percentComplete / 10 * 10; // Round to nearest 10%

        // Throttle announcements to at most once per second and only at 10% intervals
        var now = DateTime.Now;
        if (interval == _lastProgressAnnouncement && (now - _lastProgressTime).TotalSeconds < 5)
            return;

        _lastProgressAnnouncement = interval;
        _lastProgressTime = now;

        string message;
        if (current >= total)
        {
            message = string.Format(
                Resources.Resources.Accessibility_ProgressComplete,
                total);
        }
        else if (!string.IsNullOrEmpty(itemName))
        {
            message = string.Format(
                Resources.Resources.Accessibility_ProgressWithItem,
                current,
                total,
                itemName);
        }
        else
        {
            message = string.Format(
                Resources.Resources.Accessibility_Progress,
                current,
                total,
                percentComplete);
        }

        Announce(message, AnnouncementPriority.Polite);
    }

    /// <summary>
    /// Resets progress announcement tracking.
    /// Call this when starting a new batch operation.
    /// </summary>
    public void ResetProgressTracking()
    {
        _lastProgressAnnouncement = -1;
        _lastProgressTime = DateTime.MinValue;
    }
}

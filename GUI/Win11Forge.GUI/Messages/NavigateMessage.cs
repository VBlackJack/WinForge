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

namespace Win11Forge.GUI.Messages;

/// <summary>
/// Message to request navigation to a specific view.
/// </summary>
public class NavigateMessage
{
    /// <summary>
    /// Navigation view indices.
    /// </summary>
    public static class ViewIndex
    {
        public const int Dashboard = 0;
        public const int Prerequisites = 1;
        public const int Apps = 2;
        public const int Deployment = 3;
        public const int Settings = 4;
    }

    /// <summary>
    /// The view index to navigate to.
    /// </summary>
    public int TargetViewIndex { get; }

    /// <summary>
    /// Initializes a new instance of NavigateMessage.
    /// </summary>
    public NavigateMessage(int targetViewIndex)
    {
        TargetViewIndex = targetViewIndex;
    }
}

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

namespace Win11Forge.GUI.Configuration;

/// <summary>
/// Single source of truth for clickable Win11Forge external links.
/// Display text without scheme (e.g., About dialog) lives in resx;
/// README markdown links remain canonical user-facing prose.
/// Anything launched via Process.Start reads from here.
/// </summary>
internal static class ProjectLinks
{
    public const string Repository = "https://github.com/VBlackJack/Win11Forge";

    public const string Issues = $"{Repository}/issues";

    public const string NewIssue = $"{Repository}/issues/new";
}

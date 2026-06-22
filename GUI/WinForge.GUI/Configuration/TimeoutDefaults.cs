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

namespace WinForge.GUI.Configuration;

/// <summary>
/// Centralized timeout defaults for GUI services.
/// PowerShell-side install timeouts live in Config/timeouts-settings.json;
/// these constants apply to in-process .NET HttpClient and synchronization paths only.
/// </summary>
internal static class TimeoutDefaults
{
    public static readonly TimeSpan HttpClient = TimeSpan.FromSeconds(15);

    public static readonly TimeSpan PackageOperation = TimeSpan.FromSeconds(30);

    public static readonly TimeSpan CacheWarmingShutdown = TimeSpan.FromSeconds(2);
}

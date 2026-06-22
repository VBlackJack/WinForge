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

namespace WinForge.GUI.Services.Coordinators;

/// <summary>
/// Result of an application scan operation.
/// </summary>
/// <param name="Total">Total number of applications considered.</param>
/// <param name="InstalledCount">Number of installed applications detected.</param>
/// <param name="UpdatesAvailableCount">Number of applications with updates available.</param>
/// <param name="WasCancelled">Whether the scan was cancelled before completion.</param>
public sealed record AppScanResult(
    int Total,
    int InstalledCount,
    int UpdatesAvailableCount,
    bool WasCancelled);

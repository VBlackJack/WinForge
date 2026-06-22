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

using WinForge.GUI.Models;

namespace WinForge.GUI.Services.Coordinators;

/// <summary>
/// Progress reported by application operation coordinators.
/// </summary>
/// <param name="Completed">Number of completed applications.</param>
/// <param name="Total">Total number of applications in the operation.</param>
/// <param name="Current">Application that just completed processing.</param>
public sealed record AppOperationProgress(int Completed, int Total, ApplicationModel? Current);

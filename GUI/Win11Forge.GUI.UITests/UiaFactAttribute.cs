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

namespace Win11Forge.GUI.UITests;

/// <summary>
/// Opt-in UI Automation test attribute. UIA tests open a real desktop window.
/// </summary>
public sealed class UiaFactAttribute : FactAttribute
{
    private const string RunUiaVariable = "WIN11FORGE_RUN_UIA";

    public UiaFactAttribute()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(RunUiaVariable), "1", StringComparison.Ordinal))
        {
            Skip = $"Set {RunUiaVariable}=1 to run desktop UI Automation tests.";
        }
    }
}

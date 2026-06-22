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

using System.Text.Json;
using WinForge.GUI.Models;

namespace WinForge.GUI.Tests;

public class DetectionConfigurationSerializationTests
{
    private const string WindowsSubsystemForLinuxFeature = "Microsoft-Windows-Subsystem-Linux";

    [Fact]
    public void Deserialize_FeatureKey_MapsToFeatureName()
    {
        string json = $$"""
            { "Method": "WindowsFeature", "Feature": "{{WindowsSubsystemForLinuxFeature}}" }
            """;

        DetectionConfiguration? configuration = JsonSerializer.Deserialize<DetectionConfiguration>(json);

        Assert.NotNull(configuration);
        Assert.Equal(WindowsSubsystemForLinuxFeature, configuration.FeatureName);
    }

    [Fact]
    public void Serialize_FeatureName_WritesFeatureKey()
    {
        DetectionConfiguration configuration = new DetectionConfiguration
        {
            Method = DetectionMethodStrings.WindowsFeature,
            FeatureName = WindowsSubsystemForLinuxFeature
        };

        string json = JsonSerializer.Serialize(configuration);

        Assert.Contains("\"Feature\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"FeatureName\"", json, StringComparison.Ordinal);
    }
}

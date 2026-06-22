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

using Win11Forge.GUI.Models;

namespace Win11Forge.GUI.Services;

/// <summary>
/// Executes a single detection configuration without catalog cache semantics.
/// </summary>
public interface IDetectionProbe
{
    /// <summary>
    /// Probes the local machine using the supplied detection configuration.
    /// </summary>
    Task<DetectionProbeResult> ProbeAsync(
        DetectionConfiguration config,
        PathValidationPolicy pathPolicy,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result category returned by a detection probe.
/// </summary>
public enum DetectionOutcome
{
    /// <summary>The configured target was detected.</summary>
    Found,

    /// <summary>The configured target was not detected.</summary>
    NotFound,

    /// <summary>The configured detection method is not supported by the probe.</summary>
    Unsupported,

    /// <summary>The configuration was rejected before probing.</summary>
    InvalidInput,

    /// <summary>The probe failed while executing.</summary>
    Error
}

/// <summary>
/// File path validation policy for detection probes.
/// </summary>
public enum PathValidationPolicy
{
    /// <summary>Catalog mode: enforce expected install roots.</summary>
    Strict,

    /// <summary>Editor mode: allow rooted ad-hoc paths while keeping injection and traversal guards.</summary>
    AdHoc
}

/// <summary>
/// Structured result from probing a detection configuration.
/// </summary>
public sealed class DetectionProbeResult
{
    /// <summary>Probe outcome.</summary>
    public DetectionOutcome Outcome { get; init; }

    /// <summary>Detection source when the target was found.</summary>
    public DetectionSource Source { get; init; } = DetectionSource.Unknown;

    /// <summary>Detected version when available.</summary>
    public string? Version { get; init; }

    /// <summary>Optional detail for UI or diagnostic mapping.</summary>
    public string? Detail { get; init; }

    /// <summary>
    /// Creates a successful probe result.
    /// </summary>
    public static DetectionProbeResult Found(DetectionSource source, string? version = null, string? detail = null)
    {
        return new DetectionProbeResult
        {
            Outcome = DetectionOutcome.Found,
            Source = source,
            Version = version,
            Detail = detail
        };
    }

    /// <summary>
    /// Creates a not-found probe result.
    /// </summary>
    public static DetectionProbeResult NotFound(string? detail = null)
    {
        return new DetectionProbeResult
        {
            Outcome = DetectionOutcome.NotFound,
            Detail = detail
        };
    }

    /// <summary>
    /// Creates an unsupported-method probe result.
    /// </summary>
    public static DetectionProbeResult Unsupported(string? detail = null)
    {
        return new DetectionProbeResult
        {
            Outcome = DetectionOutcome.Unsupported,
            Detail = detail
        };
    }

    /// <summary>
    /// Creates an invalid-input probe result.
    /// </summary>
    public static DetectionProbeResult InvalidInput(string? detail = null)
    {
        return new DetectionProbeResult
        {
            Outcome = DetectionOutcome.InvalidInput,
            Detail = detail
        };
    }

    /// <summary>
    /// Creates an error probe result.
    /// </summary>
    public static DetectionProbeResult Error(string? detail = null)
    {
        return new DetectionProbeResult
        {
            Outcome = DetectionOutcome.Error,
            Detail = detail
        };
    }
}

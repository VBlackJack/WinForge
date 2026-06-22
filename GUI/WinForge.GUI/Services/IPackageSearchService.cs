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
/// Service interface for searching packages across supported sources.
/// </summary>
public interface IPackageSearchService
{
    /// <summary>
    /// Checks if Winget is available on the system.
    /// </summary>
    bool IsWingetAvailable { get; }

    /// <summary>
    /// Checks if Chocolatey is available on the system.
    /// </summary>
    bool IsChocolateyAvailable { get; }

    /// <summary>
    /// Searches the Winget repository.
    /// </summary>
    /// <param name="query">Search query (package id or name).</param>
    /// <param name="maxResults">Maximum results to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<PackageSearchResult>> SearchWingetAsync(
        string query,
        int maxResults = 15,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches Chocolatey packages.
    /// </summary>
    /// <param name="query">Search query (package id or name).</param>
    /// <param name="maxResults">Maximum results to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<PackageSearchResult>> SearchChocolateyAsync(
        string query,
        int maxResults = 15,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches Microsoft Store packages via Winget's msstore source.
    /// </summary>
    /// <param name="query">Search query (store id or name).</param>
    /// <param name="maxResults">Maximum results to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<PackageSearchResult>> SearchStoreAsync(
        string query,
        int maxResults = 15,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a package search result for a specific source.
/// </summary>
/// <param name="PackageId">Package identifier to save in catalog source.</param>
/// <param name="DisplayName">Display name returned by source.</param>
/// <param name="Version">Latest version when available.</param>
/// <param name="Source">Source from which this package was discovered.</param>
public record PackageSearchResult(
    string PackageId,
    string DisplayName,
    string? Version,
    PackageSource Source
);

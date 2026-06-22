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

namespace WinForge.GUI.Services;

/// <summary>
/// Service interface for verifying package manager package existence.
/// Provides verification for Winget, Chocolatey, and Microsoft Store packages.
/// </summary>
public interface IPackageVerificationService
{
    /// <summary>
    /// Verifies a Winget package exists.
    /// </summary>
    /// <param name="packageId">Winget package identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Verification result.</returns>
    Task<PackageVerificationResult> VerifyWingetPackageAsync(string packageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies a Chocolatey package exists.
    /// </summary>
    /// <param name="packageName">Chocolatey package name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Verification result.</returns>
    Task<PackageVerificationResult> VerifyChocolateyPackageAsync(string packageName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies a Microsoft Store product exists.
    /// </summary>
    /// <param name="storeId">Microsoft Store product ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Verification result.</returns>
    Task<PackageVerificationResult> VerifyStoreProductAsync(string storeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies a direct download URL is accessible.
    /// </summary>
    /// <param name="url">Download URL to verify.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Verification result.</returns>
    Task<PackageVerificationResult> VerifyDirectUrlAsync(string url, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies all sources for an application.
    /// </summary>
    /// <param name="sources">Application sources to verify.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Complete verification result for all sources.</returns>
    Task<ApplicationSourcesVerificationResult> VerifyAllSourcesAsync(
        ApplicationSourcesForVerification sources,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if Winget is available on the system.
    /// </summary>
    bool IsWingetAvailable { get; }

    /// <summary>
    /// Checks if Chocolatey is available on the system.
    /// </summary>
    bool IsChocolateyAvailable { get; }
}

/// <summary>
/// Result of a package verification operation.
/// </summary>
public record PackageVerificationResult(
    bool Exists,
    string PackageId,
    PackageSource Source,
    string? Version = null,
    string? ErrorMessage = null
)
{
    /// <summary>Whether the verification was successful (no errors).</summary>
    public bool IsSuccess => ErrorMessage == null;

    /// <summary>Creates a success result.</summary>
    public static PackageVerificationResult Found(string packageId, PackageSource source, string? version = null) =>
        new(true, packageId, source, version);

    /// <summary>Creates a not found result.</summary>
    public static PackageVerificationResult NotFound(string packageId, PackageSource source) =>
        new(false, packageId, source);

    /// <summary>Creates an error result.</summary>
    public static PackageVerificationResult Error(string packageId, PackageSource source, string error) =>
        new(false, packageId, source, ErrorMessage: error);
}

/// <summary>
/// Package source type.
/// </summary>
public enum PackageSource
{
    /// <summary>Windows Package Manager (winget).</summary>
    Winget,

    /// <summary>Chocolatey package manager.</summary>
    Chocolatey,

    /// <summary>Microsoft Store.</summary>
    Store,

    /// <summary>Direct download URL.</summary>
    DirectUrl
}

/// <summary>
/// Application sources for verification.
/// </summary>
public record ApplicationSourcesForVerification(
    string? Winget,
    string? Chocolatey,
    string? Store,
    string? DirectUrl
);

/// <summary>
/// Complete verification result for all sources.
/// </summary>
public record ApplicationSourcesVerificationResult(
    PackageVerificationResult? WingetResult,
    PackageVerificationResult? ChocolateyResult,
    PackageVerificationResult? StoreResult,
    PackageVerificationResult? DirectUrlResult
)
{
    /// <summary>Whether at least one source is valid.</summary>
    public bool HasValidSource =>
        (WingetResult?.Exists ?? false) ||
        (ChocolateyResult?.Exists ?? false) ||
        (StoreResult?.Exists ?? false) ||
        (DirectUrlResult?.Exists ?? false);

    /// <summary>Count of valid sources.</summary>
    public int ValidSourceCount
    {
        get
        {
            int count = 0;
            if (WingetResult?.Exists == true) count++;
            if (ChocolateyResult?.Exists == true) count++;
            if (StoreResult?.Exists == true) count++;
            if (DirectUrlResult?.Exists == true) count++;
            return count;
        }
    }

    /// <summary>Count of sources that were checked.</summary>
    public int CheckedCount
    {
        get
        {
            int count = 0;
            if (WingetResult != null) count++;
            if (ChocolateyResult != null) count++;
            if (StoreResult != null) count++;
            if (DirectUrlResult != null) count++;
            return count;
        }
    }

    /// <summary>Whether all checked sources are valid.</summary>
    public bool AllSourcesValid => ValidSourceCount == CheckedCount && CheckedCount > 0;
}

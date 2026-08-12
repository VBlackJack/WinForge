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

using System.Text;
using WinForge.GUI.Services.PowerShell;

namespace WinForge.GUI.Services.Implementations;

/// <summary>
/// Decides which package manager an update runs through, and refuses ids that are unsafe
/// to hand to one.
/// </summary>
/// <remarks>
/// A catalog entry may name several sources and may state a preference. These rules are
/// gathered here because they are the part of the update flow that has nothing to do with
/// running processes: given a catalog entry, they answer "through which source, and is
/// this id safe to pass on".
/// </remarks>
internal static class UpdateSourcePolicy
{
    /// <summary>Canonical name of the Winget source.</summary>
    public const string SourceWinget = "Winget";

    /// <summary>Canonical name of the Chocolatey source.</summary>
    public const string SourceChocolatey = "Chocolatey";

    /// <summary>
    /// Maps a catalog-declared preferred source onto a canonical source name.
    /// </summary>
    /// <param name="source">The value read from the catalog.</param>
    /// <returns>
    /// The canonical source name, or <see langword="null"/> when the catalog named
    /// nothing recognizable — in which case the caller applies its normal source order.
    /// </returns>
    public static string? Normalize(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return null;
        }

        if (string.Equals(source, SourceWinget, StringComparison.OrdinalIgnoreCase))
        {
            return SourceWinget;
        }

        if (string.Equals(source, SourceChocolatey, StringComparison.OrdinalIgnoreCase))
        {
            return SourceChocolatey;
        }

        return null;
    }

    /// <summary>
    /// Determines whether the preferred source was named but cannot be used.
    /// </summary>
    /// <remarks>
    /// Distinguishes "no preference" from "preference impossible to honour". The second
    /// is reported to the user rather than silently falling back, because updating through
    /// a different package manager than the catalog specifies can install a different build.
    /// </remarks>
    /// <param name="preferredUpdateSource">The canonical preferred source, if any.</param>
    /// <param name="wingetId">The Winget package id declared by the entry, if any.</param>
    /// <param name="chocoPackage">The Chocolatey package name declared by the entry, if any.</param>
    /// <returns><see langword="true"/> when the preferred source has no usable package id.</returns>
    public static bool IsPreferredSourceUnavailable(
        string? preferredUpdateSource,
        string? wingetId,
        string? chocoPackage)
    {
        return string.Equals(preferredUpdateSource, SourceWinget, StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrEmpty(wingetId) ||
            string.Equals(preferredUpdateSource, SourceChocolatey, StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrEmpty(chocoPackage);
    }

    /// <summary>
    /// Rejects a package id that would be unsafe to pass to a package manager.
    /// </summary>
    /// <remarks>
    /// The id comes from a catalog that may have been imported, and it ends up on a
    /// command line. An empty id is not a rejection: it simply means the entry does not
    /// declare that source.
    /// </remarks>
    /// <param name="packageId">The package id from the catalog.</param>
    /// <param name="sourceName">Source name, used in the log message.</param>
    /// <param name="logBuilder">The deployment log being built.</param>
    /// <returns><see langword="true"/> when the id was rejected and must not be used.</returns>
    public static bool RejectInvalidPackageId(string? packageId, string sourceName, StringBuilder logBuilder)
    {
        if (string.IsNullOrEmpty(packageId))
        {
            return false;
        }

        if (PackageIdValidator.IsValidPackageId(packageId))
        {
            return false;
        }

        logBuilder.AppendLine($"Rejected invalid {sourceName} package id (failed safe-charset validation)");
        return true;
    }
}

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
using WinForge.GUI.Models;

namespace WinForge.GUI.Services.Implementations;

/// <summary>
/// Reconciles catalog applications with packages reported by detection.
/// </summary>
/// <remarks>
/// Detection reports whatever the package manager calls a program, which rarely matches
/// the catalog name exactly ("Microsoft Visual Studio Code" vs "Microsoft.VisualStudioCode"
/// vs "Visual Studio Code"). Matching is therefore heuristic and lives here as pure
/// functions so it can be reasoned about and tested without the surrounding service.
/// </remarks>
internal static class PackageMatcher
{
    /// <summary>
    /// Minimum length a normalized name must reach before substring matching is trusted.
    /// </summary>
    /// <remarks>
    /// Below this, substring matching produces nonsense: "Go" is contained in "Google
    /// Chrome", "R" in almost everything.
    /// </remarks>
    private const int MinimumSubstringMatchLength = 4;

    /// <summary>
    /// Minimum length for a token to be considered distinctive.
    /// </summary>
    private const int MinimumMeaningfulTokenLength = 4;

    /// <summary>
    /// Tokens that appear across unrelated products and therefore carry no evidence of a
    /// match. "Docker Desktop" and "Rancher Desktop" share only the noise.
    /// </summary>
    private static readonly HashSet<string> CommonPackageMatchTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "app",
        "apps",
        "client",
        "desktop",
        "shell",
        "studio",
        "tool",
        "tools"
    };

    /// <summary>
    /// Finds the detected package corresponding to a catalog application.
    /// </summary>
    /// <remarks>
    /// Ordered cheapest and most reliable first: exact app id, then normalized name, then
    /// the heuristic scan. The scan is last because it is both the slowest and the only
    /// step that can produce a false positive.
    /// </remarks>
    /// <param name="app">The catalog application.</param>
    /// <param name="detectionResult">The batch detection result to search.</param>
    /// <returns>The matching package, or <see langword="null"/> when none is found.</returns>
    public static InstalledPackageInfo? FindDetectedPackage(
        ApplicationModel app,
        BatchDetectionResult detectionResult)
    {
        if (!string.IsNullOrEmpty(app.AppId))
        {
            InstalledPackageInfo? packageInfo = detectionResult.GetPackage(app.AppId);
            if (packageInfo != null)
            {
                return packageInfo;
            }
        }

        if (!string.IsNullOrEmpty(app.Name))
        {
            string normalizedName = NormalizeLookupKey(app.Name);
            InstalledPackageInfo? packageInfo = detectionResult.GetPackage(normalizedName);
            if (packageInfo != null)
            {
                return packageInfo;
            }
        }

        foreach (InstalledPackageInfo? packageInfo in detectionResult.Packages.Values.DistinctBy(p => $"{p.Id}|{p.Name}"))
        {
            if (IsMatch(app, packageInfo))
            {
                return packageInfo;
            }
        }

        return null;
    }

    /// <summary>
    /// Determines whether a detected package plausibly is the given application.
    /// </summary>
    /// <param name="app">The catalog application.</param>
    /// <param name="packageInfo">The detected package.</param>
    /// <returns><see langword="true"/> when the two are considered the same product.</returns>
    public static bool IsMatch(ApplicationModel app, InstalledPackageInfo packageInfo)
    {
        if (string.IsNullOrWhiteSpace(app.Name))
        {
            return false;
        }

        foreach (string? candidate in new[] { packageInfo.Name, packageInfo.Id }.Where(static c => !string.IsNullOrWhiteSpace(c)))
        {
            string normalizedAppName = NormalizeLookupKey(app.Name);
            string normalizedCandidate = NormalizeLookupKey(candidate);

            if (normalizedAppName.Length >= MinimumSubstringMatchLength &&
                normalizedCandidate.Length >= MinimumSubstringMatchLength &&
                (normalizedCandidate.Contains(normalizedAppName, StringComparison.OrdinalIgnoreCase) ||
                 normalizedAppName.Contains(normalizedCandidate, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            if (HasMeaningfulTokenOverlap(app.Name, candidate))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Reduces a product name to letters and digits, lower-cased.
    /// </summary>
    /// <remarks>
    /// Vendors punctuate the same product inconsistently ("Node.js", "NodeJS", "Node JS"),
    /// so punctuation and spacing carry no signal and are dropped before comparison.
    /// </remarks>
    /// <param name="value">The value to normalize.</param>
    /// <returns>The normalized lookup key, or an empty string for blank input.</returns>
    public static string NormalizeLookupKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        StringBuilder builder = new StringBuilder(value.Length);
        foreach (char ch in value)
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(char.ToLowerInvariant(ch));
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// Determines whether two names share at least one distinctive token.
    /// </summary>
    private static bool HasMeaningfulTokenOverlap(string appName, string packageName)
    {
        HashSet<string> appTokens = GetMeaningfulTokens(appName);
        if (appTokens.Count == 0)
        {
            return false;
        }

        HashSet<string> packageTokens = GetMeaningfulTokens(packageName);
        return packageTokens.Any(appTokens.Contains);
    }

    /// <summary>
    /// Splits a name into distinctive tokens, dropping short and common ones.
    /// </summary>
    private static HashSet<string> GetMeaningfulTokens(string value)
    {
        HashSet<string> tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        StringBuilder builder = new StringBuilder();

        foreach (char ch in value)
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(char.ToLowerInvariant(ch));
            }
            else
            {
                AddToken(builder, tokens);
            }
        }

        AddToken(builder, tokens);
        return tokens;
    }

    /// <summary>
    /// Adds the accumulated token to the set when it is distinctive, then resets the buffer.
    /// </summary>
    private static void AddToken(StringBuilder builder, HashSet<string> tokens)
    {
        if (builder.Length == 0)
        {
            return;
        }

        string token = builder.ToString();
        builder.Clear();

        if (token.Length < MinimumMeaningfulTokenLength || CommonPackageMatchTokens.Contains(token))
        {
            return;
        }

        tokens.Add(token);
    }
}

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

using System.Globalization;
using System.Text;

namespace WinForge.GUI.Services.Implementations;

/// <summary>
/// Builds the deployment log text that is persisted and shown as an operation result.
/// </summary>
/// <remarks>
/// This surface is intentionally English-only, enforced by LocalizationAuditTests: results
/// and persisted logs must stay parseable regardless of the UI culture. The literal text
/// still lives in resources, to avoid hardcoding rather than to localize it — which is why
/// every lookup goes through the English resolver here instead of the ambient culture.
/// </remarks>
internal static class DeploymentLog
{
    private static readonly CultureInfo LogCulture = CultureInfo.GetCultureInfo("en");

    /// <summary>
    /// Resolves a log resource in English, falling back to the resource name.
    /// </summary>
    /// <param name="resourceName">The resource key.</param>
    /// <returns>The English resource text.</returns>
    public static string GetResource(string resourceName)
        => Resources.Resources.ResourceManager.GetString(resourceName, LogCulture) ?? resourceName;

    /// <summary>
    /// Formats a deployment progress/result string using the English log resolver.
    /// </summary>
    /// <param name="resourceName">The resource key.</param>
    /// <param name="args">Format arguments.</param>
    /// <returns>The formatted English text.</returns>
    public static string Format(string resourceName, params object?[] args)
        => string.Format(LogCulture, GetResource(resourceName), args);

    /// <summary>
    /// Records that vendor output was produced, without copying it into the main log.
    /// </summary>
    /// <remarks>
    /// Winget and Chocolatey localize their raw output to the host OS. Copying it into the
    /// deployment log would make the log unparseable on non-English machines, so only a
    /// WinForge-owned English summary is written and the sizes are kept for diagnostics.
    /// </remarks>
    /// <param name="logBuilder">The log being built.</param>
    /// <param name="output">Raw stdout from the vendor command.</param>
    /// <param name="error">Raw stderr from the vendor command.</param>
    public static void AppendVendorOutputSummary(StringBuilder logBuilder, string output, string error)
    {
        int outputLength = string.IsNullOrEmpty(output) ? 0 : output.Length;
        int errorLength = string.IsNullOrEmpty(error) ? 0 : error.Length;
        if (outputLength == 0 && errorLength == 0)
        {
            return;
        }

        logBuilder.AppendLine(
            $"Raw vendor output omitted from main log (stdout chars: {outputLength}, stderr chars: {errorLength})");
    }
}

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

using System.Diagnostics;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace Win11Forge.GUI.Services;

/// <summary>
/// Regex pattern for validating package identifiers to prevent command injection.
/// Allows alphanumeric characters, dots, hyphens, underscores, and forward slashes (for scoped packages).
/// </summary>
internal static partial class PackageIdValidator
{
    [GeneratedRegex(@"^[a-zA-Z0-9._\-/]+$", RegexOptions.Compiled)]
    public static partial Regex SafePackageIdPattern();

    public static bool IsValidPackageId(string? packageId)
    {
        if (string.IsNullOrWhiteSpace(packageId)) return false;
        return SafePackageIdPattern().IsMatch(packageId);
    }
}

/// <summary>
/// Service implementation for verifying package manager package existence.
/// </summary>
public partial class PackageVerificationService : IPackageVerificationService
{
    private static readonly HttpClient SharedHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    /// <summary>
    /// Compiled regex for validating Microsoft Store ID format (9-12 alphanumeric characters).
    /// </summary>
    [GeneratedRegex(@"^[A-Za-z0-9]{9,12}$", RegexOptions.Compiled)]
    private static partial Regex StoreIdPattern();

    /// <summary>
    /// Compiled regex for extracting version numbers from winget output.
    /// Matches patterns like 1.2, 1.2.3, 1.2.3.4.
    /// </summary>
    [GeneratedRegex(@"\b(\d+\.\d+(?:\.\d+)?(?:\.\d+)?)\b", RegexOptions.Compiled)]
    private static partial Regex VersionPattern();

    private readonly Lazy<bool> _wingetAvailable;
    private readonly Lazy<bool> _chocoAvailable;

    /// <summary>
    /// Initializes a new instance of PackageVerificationService.
    /// </summary>
    public PackageVerificationService()
    {
        _wingetAvailable = new Lazy<bool>(CheckWingetAvailable);
        _chocoAvailable = new Lazy<bool>(CheckChocoAvailable);
    }

    /// <inheritdoc/>
    public bool IsWingetAvailable => _wingetAvailable.Value;

    /// <inheritdoc/>
    public bool IsChocolateyAvailable => _chocoAvailable.Value;

    /// <inheritdoc/>
    public async Task<PackageVerificationResult> VerifyWingetPackageAsync(
        string packageId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(packageId))
        {
            return PackageVerificationResult.Error(packageId, PackageSource.Winget, "Package ID is required");
        }

        // Validate package ID to prevent command injection
        if (!PackageIdValidator.IsValidPackageId(packageId))
        {
            return PackageVerificationResult.Error(packageId, PackageSource.Winget, "Invalid package ID format");
        }

        if (!IsWingetAvailable)
        {
            return PackageVerificationResult.Error(packageId, PackageSource.Winget, "Winget is not available");
        }

        try
        {
            var result = await RunProcessAsync(
                "winget",
                $"search --id \"{packageId}\" --exact --accept-source-agreements",
                cancellationToken);

            if (result.ExitCode == 0 && result.Output.Contains(packageId, StringComparison.OrdinalIgnoreCase))
            {
                // Try to extract version from output
                var version = ExtractVersionFromWingetOutput(result.Output, packageId);
                return PackageVerificationResult.Found(packageId, PackageSource.Winget, version);
            }

            return PackageVerificationResult.NotFound(packageId, PackageSource.Winget);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Winget verification failed for {packageId}: {ex.Message}");
            return PackageVerificationResult.Error(packageId, PackageSource.Winget, ex.Message);
        }
    }

    /// <inheritdoc/>
    public async Task<PackageVerificationResult> VerifyChocolateyPackageAsync(
        string packageName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(packageName))
        {
            return PackageVerificationResult.Error(packageName, PackageSource.Chocolatey, "Package name is required");
        }

        // Validate package name to prevent command injection
        if (!PackageIdValidator.IsValidPackageId(packageName))
        {
            return PackageVerificationResult.Error(packageName, PackageSource.Chocolatey, "Invalid package name format");
        }

        if (!IsChocolateyAvailable)
        {
            return PackageVerificationResult.Error(packageName, PackageSource.Chocolatey, "Chocolatey is not available");
        }

        try
        {
            var result = await RunProcessAsync(
                "choco",
                $"search \"{packageName}\" --exact --limit-output",
                cancellationToken);

            if (result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.Output))
            {
                // Chocolatey --limit-output format: packageName|version
                var lines = result.Output.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                var matchLine = lines.FirstOrDefault(l =>
                    l.StartsWith(packageName + "|", StringComparison.OrdinalIgnoreCase));

                if (matchLine != null)
                {
                    var parts = matchLine.Split('|');
                    var version = parts.Length > 1 ? parts[1].Trim() : null;
                    return PackageVerificationResult.Found(packageName, PackageSource.Chocolatey, version);
                }
            }

            return PackageVerificationResult.NotFound(packageName, PackageSource.Chocolatey);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Chocolatey verification failed for {packageName}: {ex.Message}");
            return PackageVerificationResult.Error(packageName, PackageSource.Chocolatey, ex.Message);
        }
    }

    /// <inheritdoc/>
    public async Task<PackageVerificationResult> VerifyStoreProductAsync(
        string storeId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(storeId))
        {
            return PackageVerificationResult.Error(storeId, PackageSource.Store, "Store ID is required");
        }

        // Validate Store ID format (typically 12 alphanumeric characters)
        if (!StoreIdPattern().IsMatch(storeId))
        {
            return PackageVerificationResult.Error(storeId, PackageSource.Store, "Invalid Store ID format");
        }

        try
        {
            // Try to access the Microsoft Store product page
            var url = $"https://apps.microsoft.com/detail/{storeId}";

            using var request = new HttpRequestMessage(HttpMethod.Head, url);
            request.Headers.Add("User-Agent", "Win11Forge/3.5.2");

            using var response = await SharedHttpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return PackageVerificationResult.Found(storeId, PackageSource.Store);
            }

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return PackageVerificationResult.NotFound(storeId, PackageSource.Store);
            }

            return PackageVerificationResult.Error(storeId, PackageSource.Store, $"HTTP {(int)response.StatusCode}");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            Debug.WriteLine($"Store verification HTTP error for {storeId}: {ex.Message}");
            return PackageVerificationResult.Error(storeId, PackageSource.Store, ex.Message);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Store verification failed for {storeId}: {ex.Message}");
            return PackageVerificationResult.Error(storeId, PackageSource.Store, ex.Message);
        }
    }

    /// <inheritdoc/>
    public async Task<PackageVerificationResult> VerifyDirectUrlAsync(
        string url,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return PackageVerificationResult.Error(url, PackageSource.DirectUrl, "URL is required");
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return PackageVerificationResult.Error(url, PackageSource.DirectUrl, "Invalid URL format");
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, url);
            request.Headers.Add("User-Agent", "Win11Forge/3.5.2");

            using var response = await SharedHttpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return PackageVerificationResult.Found(url, PackageSource.DirectUrl);
            }

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return PackageVerificationResult.NotFound(url, PackageSource.DirectUrl);
            }

            // Some servers don't support HEAD, try GET with small range
            if (response.StatusCode == System.Net.HttpStatusCode.MethodNotAllowed)
            {
                using var getRequest = new HttpRequestMessage(HttpMethod.Get, url);
                getRequest.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(0, 0);
                getRequest.Headers.Add("User-Agent", "Win11Forge/3.5.2");

                using var getResponse = await SharedHttpClient.SendAsync(getRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                if (getResponse.IsSuccessStatusCode || getResponse.StatusCode == System.Net.HttpStatusCode.PartialContent)
                {
                    return PackageVerificationResult.Found(url, PackageSource.DirectUrl);
                }
            }

            return PackageVerificationResult.Error(url, PackageSource.DirectUrl, $"HTTP {(int)response.StatusCode}");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            Debug.WriteLine($"Direct URL verification HTTP error for {url}: {ex.Message}");
            return PackageVerificationResult.Error(url, PackageSource.DirectUrl, ex.Message);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Direct URL verification failed for {url}: {ex.Message}");
            return PackageVerificationResult.Error(url, PackageSource.DirectUrl, ex.Message);
        }
    }

    /// <inheritdoc/>
    public async Task<ApplicationSourcesVerificationResult> VerifyAllSourcesAsync(
        ApplicationSourcesForVerification sources,
        CancellationToken cancellationToken = default)
    {
        // Start all verification tasks in parallel
        var wingetTask = !string.IsNullOrWhiteSpace(sources.Winget)
            ? VerifyWingetPackageAsync(sources.Winget, cancellationToken)
            : Task.FromResult<PackageVerificationResult?>(null);

        var chocoTask = !string.IsNullOrWhiteSpace(sources.Chocolatey)
            ? VerifyChocolateyPackageAsync(sources.Chocolatey, cancellationToken)
            : Task.FromResult<PackageVerificationResult?>(null);

        var storeTask = !string.IsNullOrWhiteSpace(sources.Store)
            ? VerifyStoreProductAsync(sources.Store, cancellationToken)
            : Task.FromResult<PackageVerificationResult?>(null);

        var directTask = !string.IsNullOrWhiteSpace(sources.DirectUrl)
            ? VerifyDirectUrlAsync(sources.DirectUrl, cancellationToken)
            : Task.FromResult<PackageVerificationResult?>(null);

        // Wait for all tasks using proper async/await pattern
        await Task.WhenAll(wingetTask, chocoTask, storeTask, directTask);

        // Access results after awaiting - this is safe and doesn't block
        return new ApplicationSourcesVerificationResult(
            await wingetTask,
            await chocoTask,
            await storeTask,
            await directTask
        );
    }

    private static bool CheckWingetAvailable()
    {
        try
        {
            var result = RunProcessSync("winget", "--version");
            return result.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool CheckChocoAvailable()
    {
        try
        {
            var result = RunProcessSync("choco", "--version");
            return result.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static (int ExitCode, string Output) RunProcessSync(string fileName, string arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit(5000);

        return (process.ExitCode, output);
    }

    private static async Task<(int ExitCode, string Output)> RunProcessAsync(
        string fileName,
        string arguments,
        CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();

        // Read both streams concurrently to prevent deadlocks
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(30));

        try
        {
            await Task.WhenAll(outputTask, errorTask, process.WaitForExitAsync(cts.Token));
            return (process.ExitCode, await outputTask);
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to kill process {fileName}: {ex.Message}");
            }
            throw;
        }
    }

    private static string? ExtractVersionFromWingetOutput(string output, string packageId)
    {
        // Winget output has a table format with Version column
        // Try to extract version using regex
        var lines = output.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            if (line.Contains(packageId, StringComparison.OrdinalIgnoreCase))
            {
                // Try to find version pattern (e.g., 1.2.3, 1.2.3.4, v1.2.3)
                var versionMatch = VersionPattern().Match(line);
                if (versionMatch.Success)
                {
                    return versionMatch.Groups[1].Value;
                }
            }
        }

        return null;
    }
}

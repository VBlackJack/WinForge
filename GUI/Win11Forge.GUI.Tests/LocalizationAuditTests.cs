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

using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Win11Forge.GUI.Tests;

/// <summary>
/// Automated tests to enforce the "Zero Hardcoding" policy.
/// Scans XAML files for hardcoded user-facing strings that should be localized.
/// </summary>
public class LocalizationAuditTests
{
    /// <summary>
    /// Attributes that should contain localized or bound values, not hardcoded strings.
    /// </summary>
    private static readonly string[] LocalizableAttributes =
    [
        "Text",
        "Content",
        "Title",
        "Header",
        "ToolTip",
        "Watermark",
        "Placeholder"
    ];

    /// <summary>
    /// Patterns that indicate proper localization or binding.
    /// </summary>
    private static readonly string[] AllowedPatterns =
    [
        "{Binding",
        "{loc:Loc",
        "{StaticResource",
        "{DynamicResource",
        "{x:Static",
        "{TemplateBinding"
    ];

    /// <summary>
    /// Characters/strings that are acceptable as hardcoded values.
    /// These are formatting elements, not user-facing text.
    /// </summary>
    private static readonly string[] AllowedLiterals =
    [
        "",           // Empty string
        " ",          // Single space
        "/",          // Separator
        " - ",        // Dash separator
        " | ",        // Pipe separator
        "(",          // Parenthesis
        ")",          // Parenthesis
        " (",         // Space + parenthesis (formatting)
        ") ",         // Parenthesis + space (formatting)
        "...",        // Ellipsis
        ":",          // Colon
        ",",          // Comma
        "•",          // Bullet
        "-",          // Hyphen
        "*",          // Asterisk
        " *",         // Required field indicator (space + asterisk)
        "×",          // Multiplication sign
        "+",          // Plus sign
        "v",          // Version prefix (e.g., "v3.2.3")
        " / ~",       // Time separator (elapsed / ~estimated)
        "Win11Forge", // Brand name (not localized)
        "Win11Forge v", // Brand name with version prefix
        "\"",         // Quote character
        " \"",        // Space + quote (formatting)
        "&quot;",     // HTML entity quote
        " &quot;"     // Space + HTML entity quote
    ];

    /// <summary>
    /// French stems that should use diacritics in localized resource values.
    /// </summary>
    private static readonly (string Stem, Regex Pattern)[] FrenchDiacriticsStems =
    [
        ("Echec", new Regex(@"\b[Ee]chec(?:s)?\b")),
        ("echoue", new Regex(@"\b[Ee]choue(?:e|es|s)?\b")),
        ("Parametre", new Regex(@"\b[Pp]arametre(?:s)?\b")),
        ("Selection", new Regex(@"\b[Ss]election(?:ner|nez|ne|nes|nee|nees|s)?\b")),
        ("Desinstall", new Regex(@"\b[Dd]esinstall(?:er|e|es|ee|ees|ation)?\b")),
        ("Detection", new Regex(@"\b[Dd]etection\b")),
        ("detecter", new Regex(@"\b[Dd]etecter\b")),
        ("Verification", new Regex(@"\b[Vv]erification\b")),
        ("verifier", new Regex(@"\b[Vv]erifier\b")),
        ("Operation", new Regex(@"\b[Oo]peration(?:s)?\b")),
        ("Reessayer", new Regex(@"\b[Rr]eessayer\b")),
        ("Activite", new Regex(@"\b[Aa]ctivite\b")),
        ("recent", new Regex(@"\b[Rr]ecent(?:e|es|s)?\b")),
        ("Editeur", new Regex(@"\b[Ee]diteur\b")),
        ("Deploiement", new Regex(@"\b[Dd]eploiement(?:s)?\b")),
        ("Prerequis", new Regex(@"\b[Pp]rerequis\b")),
        ("succes", new Regex(@"\b[Ss]ucces\b")),
        ("a jour", new Regex(@"\ba jour\b")),
        ("etre", new Regex(@"\b[Ee]tre\b")),
        ("ete", new Regex(@"\b[Ee]te\b")),
        ("resultat", new Regex(@"\b[Rr]esultat(?:s)?\b")),
        ("element", new Regex(@"\b[Ee]lement(?:s)?\b")),
        ("securite", new Regex(@"\b[Ss]ecurite\b")),
        ("delai", new Regex(@"\b[Dd]elai(?:s)?\b")),
        ("fleche", new Regex(@"\b[Ff]leche(?:s)?\b")),
        ("Entree", new Regex(@"\b[Ee]ntree(?:s)?\b")),
        ("Reference", new Regex(@"\b[Rr]eference(?:s)?\b")),
        ("methode", new Regex(@"\b[Mm]ethode(?:s)?\b")),
        ("deja", new Regex(@"\b[Dd]eja\b")),
        ("irreversible", new Regex(@"\b[Ii]rreversible\b")),
        ("systeme", new Regex(@"\b[Ss]ysteme(?:s)?\b")),
        ("planifie", new Regex(@"\b[Pp]lanifie(?:e|es|s)?\b")),
        ("cree", new Regex(@"\b[Cc]ree(?:e|es|s)?\b")),
        ("demarr", new Regex(@"\b[Dd]emarr(?:er|age|e|es|ee|ees)?\b")),
        ("categorie", new Regex(@"\b[Cc]ategorie(?:s)?\b")),
        ("telechargement", new Regex(@"\b[Tt]elechargement\b")),
        ("depot", new Regex(@"\b[Dd]epot\b")),
        ("peut-etre", new Regex(@"\b[Pp]eut-etre\b"))
    ];

    /// <summary>
    /// Gets the Views directory path by walking up from the test assembly location.
    /// </summary>
    private static string GetViewsDirectory()
    {
        // Start from the test assembly location
        var assemblyLocation = typeof(LocalizationAuditTests).Assembly.Location;
        var currentDir = new DirectoryInfo(Path.GetDirectoryName(assemblyLocation)!);

        // Walk up to find the GUI folder structure
        while (currentDir != null)
        {
            var viewsPath = Path.Combine(currentDir.FullName, "Views");
            if (Directory.Exists(viewsPath))
            {
                return viewsPath;
            }

            // Check for Win11Forge.GUI/Views pattern
            var guiViewsPath = Path.Combine(currentDir.FullName, "Win11Forge.GUI", "Views");
            if (Directory.Exists(guiViewsPath))
            {
                return guiViewsPath;
            }

            // Check in GUI folder
            var guiFolderPath = Path.Combine(currentDir.FullName, "GUI", "Win11Forge.GUI", "Views");
            if (Directory.Exists(guiFolderPath))
            {
                return guiFolderPath;
            }

            currentDir = currentDir.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate Views directory. Started from: " + assemblyLocation);
    }

    /// <summary>
    /// Gets the Resources directory path by walking up from the test assembly location.
    /// </summary>
    private static string GetResourcesDirectory()
    {
        var assemblyLocation = typeof(LocalizationAuditTests).Assembly.Location;
        var currentDir = new DirectoryInfo(Path.GetDirectoryName(assemblyLocation)!);

        while (currentDir != null)
        {
            var resourcesPath = Path.Combine(currentDir.FullName, "Resources");
            if (Directory.Exists(resourcesPath))
            {
                return resourcesPath;
            }

            var guiResourcesPath = Path.Combine(currentDir.FullName, "Win11Forge.GUI", "Resources");
            if (Directory.Exists(guiResourcesPath))
            {
                return guiResourcesPath;
            }

            var guiFolderPath = Path.Combine(currentDir.FullName, "GUI", "Win11Forge.GUI", "Resources");
            if (Directory.Exists(guiFolderPath))
            {
                return guiFolderPath;
            }

            currentDir = currentDir.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate Resources directory. Started from: " + assemblyLocation);
    }

    /// <summary>
    /// Scans all XAML files in the Views directory for hardcoded strings.
    /// Fails if any violations are found.
    /// </summary>
    [Fact]
    public void AllXamlFiles_ShouldNotContainHardcodedStrings()
    {
        // Arrange
        var viewsDirectory = GetViewsDirectory();
        var xamlFiles = Directory.GetFiles(viewsDirectory, "*.xaml", SearchOption.AllDirectories);
        var violations = new List<string>();

        // Act - Scan each XAML file
        foreach (var xamlFile in xamlFiles)
        {
            var fileViolations = ScanXamlFile(xamlFile);
            violations.AddRange(fileViolations);
        }

        // Assert
        if (violations.Count > 0)
        {
            var message = $"Found {violations.Count} hardcoded string violation(s):\n" +
                          string.Join("\n", violations.Take(20)); // Limit output

            if (violations.Count > 20)
            {
                message += $"\n... and {violations.Count - 20} more violations.";
            }

            Assert.Fail(message);
        }
    }

    /// <summary>
    /// Scans a single XAML file for hardcoded strings.
    /// </summary>
    /// <param name="filePath">Path to the XAML file</param>
    /// <returns>List of violation messages</returns>
    private static List<string> ScanXamlFile(string filePath)
    {
        var violations = new List<string>();
        var fileName = Path.GetFileName(filePath);
        var lines = File.ReadAllLines(filePath);

        for (var lineNumber = 0; lineNumber < lines.Length; lineNumber++)
        {
            var line = lines[lineNumber];

            foreach (var attribute in LocalizableAttributes)
            {
                // Pattern: Attribute="value" where value doesn't start with {
                // Use word boundary \b to avoid matching HelpText when looking for Text
                var pattern = $@"\b{attribute}=""([^""]+)""";
                var matches = Regex.Matches(line, pattern);

                foreach (Match match in matches)
                {
                    var value = match.Groups[1].Value;

                    // Skip if the value is an allowed pattern (binding, localization, etc.)
                    if (AllowedPatterns.Any(p => value.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    // Skip if the value is an allowed literal (punctuation, formatting)
                    if (AllowedLiterals.Contains(value))
                    {
                        continue;
                    }

                    // Skip pure whitespace
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        continue;
                    }

                    // Skip if value is only digits (numbers don't need localization)
                    if (Regex.IsMatch(value, @"^[\d.,]+$"))
                    {
                        continue;
                    }

                    // Skip single special characters
                    if (value.Length == 1 && !char.IsLetter(value[0]))
                    {
                        continue;
                    }

                    // This is a violation - hardcoded text
                    violations.Add(
                        $"  [{fileName}:{lineNumber + 1}] {attribute}=\"{value}\" " +
                        $"-> Should use {{loc:Loc ...}} or {{Binding ...}}");
                }
            }
        }

        return violations;
    }

    /// <summary>
    /// Verifies that the Views directory can be found.
    /// </summary>
    [Fact]
    public void ViewsDirectory_ShouldExist()
    {
        // Act
        var viewsDirectory = GetViewsDirectory();

        // Assert
        Assert.True(Directory.Exists(viewsDirectory),
            $"Views directory should exist: {viewsDirectory}");
    }

    /// <summary>
    /// Verifies that XAML files exist in the Views directory.
    /// </summary>
    [Fact]
    public void ViewsDirectory_ShouldContainXamlFiles()
    {
        // Arrange
        var viewsDirectory = GetViewsDirectory();

        // Act
        var xamlFiles = Directory.GetFiles(viewsDirectory, "*.xaml", SearchOption.AllDirectories);

        // Assert
        Assert.NotEmpty(xamlFiles);
        Assert.True(xamlFiles.Length >= 5,
            $"Expected at least 5 XAML view files, found {xamlFiles.Length}");
    }

    /// <summary>
    /// Verifies that French resource values keep expected diacritics on common stems.
    /// </summary>
    [Fact]
    public void FrenchResources_ShouldNotContainUnaccentedFrenchStems()
    {
        // Arrange
        var resourcesDirectory = GetResourcesDirectory();
        var resourcePath = Path.Combine(resourcesDirectory, "Resources.fr.resx");
        var document = XDocument.Load(resourcePath);
        var violations = new List<string>();

        // Act
        foreach (var data in document.Root?.Elements("data") ?? Enumerable.Empty<XElement>())
        {
            var key = data.Attribute("name")?.Value ?? "<unknown>";
            var value = data.Element("value")?.Value;

            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            foreach (var stem in FrenchDiacriticsStems)
            {
                if (stem.Pattern.IsMatch(value))
                {
                    violations.Add($"  [{key}] {stem.Stem}: \"{value}\"");
                }
            }
        }

        // Assert
        if (violations.Count > 0)
        {
            var message = $"Found {violations.Count} French diacritics issue(s):\n" +
                          string.Join("\n", violations.Take(20));

            if (violations.Count > 20)
            {
                message += $"\n... and {violations.Count - 20} more violations.";
            }

            Assert.Fail(message);
        }
    }
}

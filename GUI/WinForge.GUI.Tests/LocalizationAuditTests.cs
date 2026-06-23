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
using WinForge.GUI.Tests.TestInfrastructure;

namespace WinForge.GUI.Tests;

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
        "WinForge", // Brand name (not localized)
        "WinForge v", // Brand name with version prefix
        "\"",         // Quote character
        " \"",        // Space + quote (formatting)
        "&quot;",     // HTML entity quote
        " &quot;"     // Space + HTML entity quote
    ];

    /// <summary>
    /// Regex options aligned with PowerShell's case-insensitive -match behavior
    /// in Tools/lint-fr-diacritics.ps1.
    /// </summary>
    private const RegexOptions FrenchDiacriticsRegexOptions = RegexOptions.IgnoreCase;

    /// <summary>
    /// French stems that should use diacritics in localized resource values.
    /// </summary>
    private static readonly (string Stem, Regex Pattern)[] FrenchDiacriticsStems =
    [
        ("Echec", new Regex(@"\b[Ee]chec(?:s)?\b", FrenchDiacriticsRegexOptions)),
        ("echoue", new Regex(@"\b[Ee]choue(?:e|es|s)?\b", FrenchDiacriticsRegexOptions)),
        ("Parametre", new Regex(@"\b[Pp]arametre(?:s)?\b", FrenchDiacriticsRegexOptions)),
        ("Selection", new Regex(@"\b[Ss]election(?:ner|nez|ne|nes|nee|nees|s)?\b", FrenchDiacriticsRegexOptions)),
        ("Desinstall", new Regex(@"\b[Dd]esinstall(?:er|e|es|ee|ees|ation)?\b", FrenchDiacriticsRegexOptions)),
        ("Detection", new Regex(@"\b[Dd]etection\b", FrenchDiacriticsRegexOptions)),
        ("detecter", new Regex(@"\b[Dd]etecter\b", FrenchDiacriticsRegexOptions)),
        ("Verification", new Regex(@"\b[Vv]erification\b", FrenchDiacriticsRegexOptions)),
        ("verifier", new Regex(@"\b[Vv]erifier\b", FrenchDiacriticsRegexOptions)),
        ("Operation", new Regex(@"\b[Oo]peration(?:s)?\b", FrenchDiacriticsRegexOptions)),
        ("Reessayer", new Regex(@"\b[Rr]eessayer\b", FrenchDiacriticsRegexOptions)),
        ("Activite", new Regex(@"\b[Aa]ctivite\b", FrenchDiacriticsRegexOptions)),
        ("recent", new Regex(@"\b[Rr]ecent(?:e|es|s)?\b", FrenchDiacriticsRegexOptions)),
        ("Editeur", new Regex(@"\b[Ee]diteur\b", FrenchDiacriticsRegexOptions)),
        ("Deploiement", new Regex(@"\b[Dd]eploiement(?:s)?\b", FrenchDiacriticsRegexOptions)),
        ("Prerequis", new Regex(@"\b[Pp]rerequis\b", FrenchDiacriticsRegexOptions)),
        ("succes", new Regex(@"\b[Ss]ucces\b", FrenchDiacriticsRegexOptions)),
        ("a jour", new Regex(@"\ba jour\b", FrenchDiacriticsRegexOptions)),
        ("etre", new Regex(@"\b[Ee]tre\b", FrenchDiacriticsRegexOptions)),
        ("ete", new Regex(@"\b[Ee]te\b", FrenchDiacriticsRegexOptions)),
        ("resultat", new Regex(@"\b[Rr]esultat(?:s)?\b", FrenchDiacriticsRegexOptions)),
        ("element", new Regex(@"\b[Ee]lement(?:s)?\b", FrenchDiacriticsRegexOptions)),
        ("securite", new Regex(@"\b[Ss]ecurite\b", FrenchDiacriticsRegexOptions)),
        ("delai", new Regex(@"\b[Dd]elai(?:s)?\b", FrenchDiacriticsRegexOptions)),
        ("fleche", new Regex(@"\b[Ff]leche(?:s)?\b", FrenchDiacriticsRegexOptions)),
        ("Entree", new Regex(@"\b[Ee]ntree(?:s)?\b", FrenchDiacriticsRegexOptions)),
        ("Reference", new Regex(@"\b[Rr]eference(?:s)?\b", FrenchDiacriticsRegexOptions)),
        ("methode", new Regex(@"\b[Mm]ethode(?:s)?\b", FrenchDiacriticsRegexOptions)),
        ("deja", new Regex(@"\b[Dd]eja\b", FrenchDiacriticsRegexOptions)),
        ("irreversible", new Regex(@"\b[Ii]rreversible\b", FrenchDiacriticsRegexOptions)),
        ("systeme", new Regex(@"\b[Ss]ysteme(?:s)?\b", FrenchDiacriticsRegexOptions)),
        ("planifie", new Regex(@"\b[Pp]lanifie(?:e|es|s)?\b", FrenchDiacriticsRegexOptions)),
        ("cree", new Regex(@"\b[Cc]ree(?:e|es|s)?\b", FrenchDiacriticsRegexOptions)),
        ("demarr", new Regex(@"\b[Dd]emarr(?:er|age|e|es|ee|ees)?\b", FrenchDiacriticsRegexOptions)),
        ("categorie", new Regex(@"\b[Cc]ategorie(?:s)?\b", FrenchDiacriticsRegexOptions)),
        ("telechargement", new Regex(@"\b[Tt]elechargement\b", FrenchDiacriticsRegexOptions)),
        ("depot", new Regex(@"\b[Dd]epot\b", FrenchDiacriticsRegexOptions)),
        ("peut-etre", new Regex(@"\b[Pp]eut-etre\b", FrenchDiacriticsRegexOptions)),
        ("applique", new Regex(@"\b[Aa]pplique(?:e|es|s)?\b", FrenchDiacriticsRegexOptions)),
        ("enregistre", new Regex(@"\b[Ee]nregistre(?:e|es|s)?\b", FrenchDiacriticsRegexOptions)),
        ("chargee", new Regex(@"\b[Cc]hargee(?:s)?\b", FrenchDiacriticsRegexOptions)),
        ("verifie", new Regex(@"\b[Vv]erifie(?:e|es|s)?\b", FrenchDiacriticsRegexOptions)),
        ("reseau", new Regex(@"\b[Rr]eseau(?:x)?\b", FrenchDiacriticsRegexOptions)),
        ("necessaire", new Regex(@"\b[Nn]ecessaire(?:s)?\b", FrenchDiacriticsRegexOptions)),
        ("decouvert", new Regex(@"\b[Dd]ecouvert(?:e|es|s)?\b", FrenchDiacriticsRegexOptions)),
        ("ignore", new Regex(@"\b[Ii]gnore(?:e|es|s)?\b", FrenchDiacriticsRegexOptions)),
        ("donnee", new Regex(@"\b[Dd]onnee(?:s)?\b", FrenchDiacriticsRegexOptions))
    ];

    /// <summary>
    /// Gets the Views directory path by walking up from the test assembly location.
    /// </summary>
    private static string GetViewsDirectory()
        => RepositoryPathHelper.FindDirectory("GUI", "WinForge.GUI", "Views");

    /// <summary>
    /// Gets the Resources directory path by walking up from the test assembly location.
    /// </summary>
    private static string GetResourcesDirectory()
        => RepositoryPathHelper.FindDirectory("GUI", "WinForge.GUI", "Resources");

    /// <summary>
    /// Scans all XAML files in the Views directory for hardcoded strings.
    /// Fails if any violations are found.
    /// </summary>
    [Fact]
    public void AllXamlFiles_ShouldNotContainHardcodedStrings()
    {
        // Arrange
        string viewsDirectory = GetViewsDirectory();
        string[] xamlFiles = Directory.GetFiles(viewsDirectory, "*.xaml", SearchOption.AllDirectories);
        List<string> violations = new List<string>();

        // Act - Scan each XAML file
        foreach (string xamlFile in xamlFiles)
        {
            List<string> fileViolations = ScanXamlFile(xamlFile);
            violations.AddRange(fileViolations);
        }

        // Assert
        if (violations.Count > 0)
        {
            string message = $"Found {violations.Count} hardcoded string violation(s):\n" +
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
        List<string> violations = new List<string>();
        string fileName = Path.GetFileName(filePath);
        string[] lines = File.ReadAllLines(filePath);

        for (int lineNumber = 0; lineNumber < lines.Length; lineNumber++)
        {
            string line = lines[lineNumber];

            foreach (string attribute in LocalizableAttributes)
            {
                // Pattern: Attribute="value" where value doesn't start with {
                // Use word boundary \b to avoid matching HelpText when looking for Text
                string pattern = $@"\b{attribute}=""([^""]+)""";
                MatchCollection matches = Regex.Matches(line, pattern);

                foreach (Match match in matches)
                {
                    string value = match.Groups[1].Value;

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
        string viewsDirectory = GetViewsDirectory();

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
        string viewsDirectory = GetViewsDirectory();

        // Act
        string[] xamlFiles = Directory.GetFiles(viewsDirectory, "*.xaml", SearchOption.AllDirectories);

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
        string resourcesDirectory = GetResourcesDirectory();
        string resourcePath = Path.Combine(resourcesDirectory, "Resources.fr.resx");
        XDocument document = XDocument.Load(resourcePath);
        List<string> violations = new List<string>();

        // Act
        foreach (XElement data in document.Root?.Elements("data") ?? Enumerable.Empty<XElement>())
        {
            string key = data.Attribute("name")?.Value ?? "<unknown>";
            string? value = data.Element("value")?.Value;

            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            foreach ((string Stem, Regex Pattern) stem in FrenchDiacriticsStems)
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
            string message = $"Found {violations.Count} French diacritics issue(s):\n" +
                          string.Join("\n", violations.Take(20));

            if (violations.Count > 20)
            {
                message += $"\n... and {violations.Count - 20} more violations.";
            }

            Assert.Fail(message);
        }
    }

    [Fact]
    public void PowerShellOperationalLogs_ShouldUseEnglishLogStringResolver()
    {
        List<string> violations = new List<string>();
        string coreDirectory = RepositoryPathHelper.FindDirectory("Core");
        string modulesDirectory = RepositoryPathHelper.FindDirectory("Modules");
        string repositoryRoot = Directory.GetParent(coreDirectory)?.FullName
            ?? throw new DirectoryNotFoundException($"Could not resolve repository root from {coreDirectory}.");
        HashSet<string> excludedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Path.Combine(coreDirectory, "Localization.psm1"),
            Path.Combine(modulesDirectory, "WinForgeGUI.psm1")
        };

        string[] files = Directory.GetFiles(coreDirectory, "*.psm1")
            .Concat(Directory.GetFiles(modulesDirectory, "*.psm1"))
            .Where(file => !excludedFiles.Contains(file))
            .ToArray();

        foreach (string filePath in files)
        {
            string[] lines = File.ReadAllLines(filePath);
            string displayPath = Path.GetRelativePath(repositoryRoot, filePath);

            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                if (lines[lineIndex].Contains("Get-LocalizedString", StringComparison.Ordinal) ||
                    Regex.IsMatch(lines[lineIndex], @"\bt\s+'", RegexOptions.CultureInvariant))
                {
                    if (Path.GetFileName(filePath).Equals("ModuleLoader.psm1", StringComparison.OrdinalIgnoreCase) &&
                        lines[lineIndex].Contains("'Get-LocalizedString'", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    violations.Add($"{displayPath}:{lineIndex + 1}: {lines[lineIndex].Trim()}");
                }
            }
        }

        if (violations.Count > 0)
        {
            Assert.Fail(
                "Operational PowerShell logs and result messages must use Get-LogString so persisted logs stay English:\n" +
                string.Join("\n", violations.Take(20)));
        }
    }

    [Fact]
    public void ApplicationManagementLogResults_ShouldUseEnglishResourceResolver()
    {
        string filePath = RepositoryPathHelper.FindFile(
            "GUI",
            "WinForge.GUI",
            "Services",
            "Implementations",
            "ApplicationManagementServiceImpl.cs");
        string source = File.ReadAllText(filePath);

        Assert.DoesNotMatch(
            new Regex(@"(?<!nameof\()Resources\.Resources\.AppManagement_", RegexOptions.Multiline),
            source);
        Assert.Contains("GetLogResource", source, StringComparison.Ordinal);
    }
}

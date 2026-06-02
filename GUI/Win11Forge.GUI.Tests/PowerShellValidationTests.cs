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
 *
 * Author: Julien Bombled
 */

using System.IO;
using Win11Forge.GUI.Services.PowerShell;

namespace Win11Forge.GUI.Tests;

public class PowerShellValidationTests
{
    [Theory]
    [InlineData("Microsoft.VisualStudioCode")]
    [InlineData("7zip.7zip")]
    [InlineData("VideoLAN.VLC")]
    [InlineData("Google Chrome")]
    [InlineData("a_b-c.d")]
    public void ValidateAppId_WithValidId_ReturnsInput(string appId)
    {
        string result = PowerShellValidation.ValidateAppId(appId);

        Assert.Equal(appId, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateAppId_WithEmptyId_ThrowsArgumentException(string? appId)
    {
        Assert.Throws<ArgumentException>(() => PowerShellValidation.ValidateAppId(appId!));
    }

    [Theory]
    [InlineData("`")]
    [InlineData("$")]
    [InlineData("(")]
    [InlineData(")")]
    [InlineData(";")]
    [InlineData("|")]
    [InlineData("'")]
    [InlineData("\"")]
    [InlineData("&")]
    [InlineData("<")]
    [InlineData(">")]
    [InlineData("\n")]
    public void ValidateAppId_WithInjectionCharacter_ThrowsArgumentException(string invalidCharacter)
    {
        string appId = "safe" + invalidCharacter + "id";

        Assert.Throws<ArgumentException>(() => PowerShellValidation.ValidateAppId(appId));
    }

    [Fact]
    public void ValidateAppId_WithLengthGreaterThanMaximum_ThrowsArgumentException()
    {
        string appId = new string('a', 201);

        Assert.Throws<ArgumentException>(() => PowerShellValidation.ValidateAppId(appId));
    }

    [Fact]
    public void ValidateAppId_WithMaximumLength_ReturnsInput()
    {
        string appId = new string('a', 200);

        string result = PowerShellValidation.ValidateAppId(appId);

        Assert.Equal(appId, result);
    }

    [Theory]
    [InlineData("Developer")]
    [InlineData("My Profile 1")]
    public void ValidateProfileName_WithValidName_ReturnsInput(string profileName)
    {
        string result = PowerShellValidation.ValidateProfileName(profileName);

        Assert.Equal(profileName, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateProfileName_WithEmptyName_ThrowsArgumentException(string? profileName)
    {
        Assert.Throws<ArgumentException>(() => PowerShellValidation.ValidateProfileName(profileName!));
    }

    [Theory]
    [InlineData("..")]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("../x")]
    public void ValidateProfileName_WithPathTraversal_ThrowsArgumentException(string profileName)
    {
        Assert.Throws<ArgumentException>(() => PowerShellValidation.ValidateProfileName(profileName));
    }

    [Fact]
    public void ValidateProfileName_WithInvalidFileNameCharacter_ThrowsArgumentException()
    {
        char[] invalidChars = Path.GetInvalidFileNameChars();
        char invalidChar = invalidChars.Contains(':') ? ':' : invalidChars[0];
        string profileName = "Bad" + invalidChar + "Name";

        Assert.Throws<ArgumentException>(() => PowerShellValidation.ValidateProfileName(profileName));
    }

    [Fact]
    public void ValidateProfileName_WithLengthGreaterThanMaximum_ThrowsArgumentException()
    {
        string profileName = new string('a', 101);

        Assert.Throws<ArgumentException>(() => PowerShellValidation.ValidateProfileName(profileName));
    }

    [Fact]
    public void ValidateProfileName_WithMaximumLength_ReturnsInput()
    {
        string profileName = new string('a', 100);

        string result = PowerShellValidation.ValidateProfileName(profileName);

        Assert.Equal(profileName, result);
    }

    [Fact]
    public void ValidatePathWithinDirectory_WithPathInsideBaseDirectory_ReturnsFullPath()
    {
        string baseDir = Path.Combine(Path.GetTempPath(), "Win11ForgeValidationTests", "Data");
        string candidate = Path.Combine(baseDir, "sub", "f.txt");
        string expectedPath = Path.GetFullPath(candidate);

        string result = PowerShellValidation.ValidatePathWithinDirectory(candidate, baseDir);

        Assert.Equal(expectedPath, result);
    }

    [Fact]
    public void ValidatePathWithinDirectory_WithParentTraversal_ThrowsArgumentException()
    {
        string baseDir = Path.Combine(Path.GetTempPath(), "Win11ForgeValidationTests", "Data");
        string candidate = Path.Combine(baseDir, "..", "evil.txt");

        Assert.Throws<ArgumentException>(() => PowerShellValidation.ValidatePathWithinDirectory(candidate, baseDir));
    }

    [Fact]
    public void ValidatePathWithinDirectory_WithSiblingPrefixAttack_ThrowsArgumentException()
    {
        string root = Path.GetPathRoot(Environment.CurrentDirectory)
            ?? Path.GetPathRoot(Path.GetTempPath())
            ?? string.Concat("C:", Path.DirectorySeparatorChar);
        string baseDir = Path.Combine(root, "app", "data");
        string candidate = Path.Combine(root, "app", "data-evil", "x");

        Assert.Throws<ArgumentException>(() => PowerShellValidation.ValidatePathWithinDirectory(candidate, baseDir));
    }

    [Fact]
    public void ValidatePathWithinDirectory_WithDifferentCasing_ReturnsFullPath()
    {
        string baseDir = Path.Combine(Path.GetTempPath(), "Win11ForgeValidationTests", "CaseTest");
        string candidate = Path.Combine(baseDir.ToUpperInvariant(), "Sub", "file.txt");
        string expectedPath = Path.GetFullPath(candidate);

        string result = PowerShellValidation.ValidatePathWithinDirectory(candidate, baseDir.ToLowerInvariant());

        Assert.Equal(expectedPath, result);
    }

    [Fact]
    public void EscapeForPowerShell_WithNull_ReturnsNull()
    {
        string? result = PowerShellValidation.EscapeForPowerShell(null!);

        Assert.Null(result);
    }

    [Fact]
    public void EscapeForPowerShell_WithEmptyString_ReturnsEmptyString()
    {
        string result = PowerShellValidation.EscapeForPowerShell(string.Empty);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void EscapeForPowerShell_WithSingleQuote_DoublesQuote()
    {
        string result = PowerShellValidation.EscapeForPowerShell("it's");

        Assert.Equal("it''s", result);
    }

    [Fact]
    public void EscapeForPowerShell_WithDangerousCharacters_RemovesCharacters()
    {
        string result = PowerShellValidation.EscapeForPowerShell("a`b$c(d)e");

        Assert.Equal("abcde", result);
    }

    [Fact]
    public void EscapeForPowerShell_WithCleanString_ReturnsInput()
    {
        const string input = "Microsoft.VisualStudioCode";

        string result = PowerShellValidation.EscapeForPowerShell(input);

        Assert.Equal(input, result);
    }
}

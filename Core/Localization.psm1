<#
.SYNOPSIS
    WinForge - Localization v3.7.2

.DESCRIPTION
    Provides internationalization (i18n) functionality for the WinForge framework:
    - JSON-based translation files
    - Automatic locale detection
    - Fallback to English for missing translations
    - Parameter substitution support

.NOTES
    Author: Julien Bombled
    v3.7.2

.EXAMPLE
    # Initialize with auto-detected locale
    Initialize-Localization

    # Get a translated string
    Get-LocalizedString -Key 'install.starting' -Parameters @{ AppName = 'Firefox' }

.LINK
    https://github.com/VBlackJack/WinForge
#>

#
# Copyright 2026 Julien Bombled
#
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at
#
#     http://www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.
#

Set-StrictMode -Version Latest

# === MODULE VARIABLES ===
$script:CurrentLocale = 'en'
$script:Translations = @{}
$script:FallbackTranslations = @{}
$script:LocalesPath = $null
$script:IsInitialized = $false

# === INITIALIZATION ===

function Initialize-Localization {
    <#
    .SYNOPSIS
        Initializes the localization system.
    .DESCRIPTION
        Loads translation files from the locales directory, auto-detecting the system language
        if no locale is specified. Always loads English as a fallback, then overlays the requested
        locale translations.

    .PARAMETER Locale
        Locale code (e.g., 'en', 'fr'). If not specified, auto-detects from system.

    .PARAMETER LocalesPath
        Path to the locales directory. Defaults to Config/Locales relative to repository root.

    .EXAMPLE
        Initialize-Localization -Locale 'fr'
    #>
    [CmdletBinding()]
    param(
        [Parameter()]
        [ValidatePattern('^[a-z]{2}(-[A-Z]{2})?$')]
        [string]$Locale,

        [Parameter()]
        [string]$LocalesPath
    )

    # Determine locales path
    if ([string]::IsNullOrWhiteSpace($LocalesPath)) {
        $scriptRoot = $PSScriptRoot
        if ($scriptRoot) {
            $repoRoot = Split-Path -Parent $scriptRoot
            $script:LocalesPath = Join-Path -Path $repoRoot -ChildPath 'Config\Locales'
        } else {
            $script:LocalesPath = Join-Path -Path $PWD -ChildPath 'Config\Locales'
        }
    } else {
        $script:LocalesPath = $LocalesPath
    }

    # Auto-detect locale if not specified
    if ([string]::IsNullOrWhiteSpace($Locale)) {
        $systemLocale = (Get-Culture).TwoLetterISOLanguageName
        $Locale = if ($systemLocale -in @('en', 'fr')) { $systemLocale } else { 'en' }
    }

    # Load fallback (English) translations first
    $fallbackPath = Join-Path -Path $script:LocalesPath -ChildPath 'en.json'
    if (Test-Path -Path $fallbackPath) {
        try {
            $content = Get-Content -Path $fallbackPath -Raw -Encoding UTF8
            # PS5.1 compatible: ConvertFrom-Json returns PSCustomObject, not hashtable
            $script:FallbackTranslations = ConvertFrom-Json -InputObject $content -ErrorAction Stop
        }
        catch {
            Write-Warning "Failed to load fallback translations: $($_.Exception.Message)"
            $script:FallbackTranslations = $null
        }
    }

    # Load requested locale translations
    $script:CurrentLocale = $Locale
    $localePath = Join-Path -Path $script:LocalesPath -ChildPath "$Locale.json"

    if (Test-Path -Path $localePath) {
        try {
            $content = Get-Content -Path $localePath -Raw -Encoding UTF8
            # PS5.1 compatible: ConvertFrom-Json returns PSCustomObject, not hashtable
            $script:Translations = ConvertFrom-Json -InputObject $content -ErrorAction Stop
        }
        catch {
            Write-Warning "Failed to load $Locale translations: $($_.Exception.Message)"
            $script:Translations = $script:FallbackTranslations
        }
    } else {
        # Fall back to English if locale file not found
        $script:Translations = $script:FallbackTranslations
    }

    $script:IsInitialized = $true
}

# === TRANSLATION FUNCTIONS ===

function Get-LocalizedString {
    <#
    .SYNOPSIS
        Gets a localized string by key.
    .DESCRIPTION
        Resolves a dot-notation translation key against the current locale, falling back to
        English if the key is missing. Supports parameter substitution using {ParamName} placeholders
        in translation values.

    .PARAMETER Key
        Translation key using dot notation (e.g., 'install.starting')

    .PARAMETER Parameters
        Hashtable of parameters to substitute in the string.
        Use {ParamName} in translation strings.

    .PARAMETER DefaultValue
        Value to return if key is not found. Defaults to the key itself.

    .OUTPUTS
        [string] The localized string with parameters substituted.

    .EXAMPLE
        Get-LocalizedString -Key 'install.starting' -Parameters @{ AppName = 'Firefox' }
        # Returns: "Starting installation of Firefox..."
    #>
    [CmdletBinding()]
    [OutputType([string])]
    [Alias('t', 'Get-String', '__')]
    param(
        [Parameter(Mandatory, Position = 0)]
        [string]$Key,

        [Parameter(Position = 1)]
        [hashtable]$Parameters = @{},

        [Parameter()]
        [string]$DefaultValue
    )

    # Auto-initialize if needed
    if (-not $script:IsInitialized) {
        Initialize-Localization
    }

    # Navigate nested keys
    $value = Get-NestedValue -Object $script:Translations -Key $Key

    # Fall back to English if not found
    if ($null -eq $value) {
        $value = Get-NestedValue -Object $script:FallbackTranslations -Key $Key
    }

    # Fall back to default or key
    if ($null -eq $value) {
        $value = if ($DefaultValue) { $DefaultValue } else { "[$Key]" }
    }

    # Substitute parameters
    if ($Parameters.Count -gt 0) {
        foreach ($param in $Parameters.GetEnumerator()) {
            $value = $value -replace "\{$($param.Key)\}", $param.Value
        }
    }

    return $value
}

function Get-LogString {
    <#
    .SYNOPSIS
        Gets an English string for persisted logs and operation result messages.
    .DESCRIPTION
        Resolves a dot-notation translation key against the English fallback translations only.
        UI strings should continue to use Get-LocalizedString so they follow the user language,
        while logs stay stable and language-neutral for support and automation.

    .PARAMETER Key
        Translation key using dot notation (e.g., 'install.starting')

    .PARAMETER Parameters
        Hashtable of parameters to substitute in the string.

    .PARAMETER DefaultValue
        Value to return if key is not found. Defaults to the key itself.

    .OUTPUTS
        [string] The English string with parameters substituted.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory, Position = 0)]
        [string]$Key,

        [Parameter(Position = 1)]
        [hashtable]$Parameters = @{},

        [Parameter()]
        [string]$DefaultValue
    )

    if (-not $script:IsInitialized) {
        Initialize-Localization
    }

    $value = Get-NestedValue -Object $script:FallbackTranslations -Key $Key

    if ($null -eq $value) {
        $value = if ($DefaultValue) { $DefaultValue } else { "[$Key]" }
    }

    if ($Parameters.Count -gt 0) {
        foreach ($param in $Parameters.GetEnumerator()) {
            $value = $value -replace "\{$($param.Key)\}", $param.Value
        }
    }

    return $value
}

function Get-NestedValue {
    <#
    .SYNOPSIS
        Gets a value from a nested object using dot notation.
    .DESCRIPTION
        Traverses a nested hashtable or PSCustomObject hierarchy by splitting a dot-separated
        key into segments and resolving each level. Returns null if any segment is missing.
        Compatible with both PowerShell 5.1 PSCustomObjects and PowerShell 7+ hashtables.

    .PARAMETER Object
        The hashtable or PSCustomObject to search

    .PARAMETER Key
        Dot-separated key path (e.g., 'install.parallel.starting')

    .OUTPUTS
        The value at the specified path, or $null if not found
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        $Object,

        [Parameter(Mandatory)]
        [string]$Key
    )

    $parts = $Key -split '\.'
    $current = $Object

    foreach ($part in $parts) {
        if ($null -eq $current) {
            return $null
        }

        # Support both hashtables and PSCustomObjects (PS5.1 compatibility)
        if ($current -is [hashtable]) {
            if ($current.ContainsKey($part)) {
                $current = $current[$part]
            } else {
                return $null
            }
        } elseif ($current -is [PSCustomObject]) {
            $prop = $current.PSObject.Properties[$part]
            if ($null -ne $prop) {
                $current = $prop.Value
            } else {
                return $null
            }
        } else {
            return $null
        }
    }

    return $current
}

# === UTILITY FUNCTIONS ===

function Get-CurrentLocale {
    <#
    .SYNOPSIS
        Gets the current locale code.
    .DESCRIPTION
        Returns the two-letter locale code that is currently active in the localization system
        (e.g., 'en' or 'fr').

    .OUTPUTS
        [string] Current locale code (e.g., 'en', 'fr')
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param()

    return $script:CurrentLocale
}

function Set-CurrentLocale {
    <#
    .SYNOPSIS
        Changes the current locale at runtime.
    .DESCRIPTION
        Reinitializes the localization system with a new locale code, reloading translation
        files from disk while preserving the current locales directory path.

    .PARAMETER Locale
        New locale code to use

    .EXAMPLE
        Set-CurrentLocale -Locale 'fr'
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [ValidatePattern('^[a-z]{2}(-[A-Z]{2})?$')]
        [string]$Locale
    )

    Initialize-Localization -Locale $Locale -LocalesPath $script:LocalesPath
}

function Get-AvailableLocales {
    <#
    .SYNOPSIS
        Gets list of available locale codes.
    .DESCRIPTION
        Scans the locales directory for JSON translation files and returns an array of their
        base names as available locale codes. Returns only 'en' if the directory is not found.

    .OUTPUTS
        [string[]] Array of available locale codes
    #>
    [CmdletBinding()]
    [OutputType([string[]])]
    param()

    if (-not $script:LocalesPath -or -not (Test-Path -Path $script:LocalesPath)) {
        return @('en')
    }

    $locales = Get-ChildItem -Path $script:LocalesPath -Filter '*.json' -File |
               ForEach-Object { $_.BaseName }

    return $locales
}

function Test-TranslationKey {
    <#
    .SYNOPSIS
        Tests if a translation key exists.
    .DESCRIPTION
        Checks whether a dot-notation translation key is present in the current locale or the
        English fallback translations. Does not return the value itself.

    .PARAMETER Key
        Translation key to test

    .OUTPUTS
        [bool] True if key exists in current or fallback translations
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [string]$Key
    )

    if (-not $script:IsInitialized) {
        Initialize-Localization
    }

    $value = Get-NestedValue -Object $script:Translations -Key $Key
    if ($null -ne $value) {
        return $true
    }

    $value = Get-NestedValue -Object $script:FallbackTranslations -Key $Key
    return $null -ne $value
}

# === MODULE EXPORTS ===

Export-ModuleMember -Function @(
    'Initialize-Localization',
    'Get-LocalizedString',
    'Get-LogString',
    'Get-CurrentLocale',
    'Set-CurrentLocale',
    'Get-AvailableLocales',
    'Test-TranslationKey'
) -Alias @('t', 'Get-String', '__')

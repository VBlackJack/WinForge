<#
.SYNOPSIS
    WinForge - JSON Schema Validation Module v3.7.2

.DESCRIPTION
    Module for validating JSON files against JSON Schema definitions:
    - Profile validation against deployment-profile.schema.json
    - Application database validation against applications-database.schema.json
    - Custom schema validation support
    - Detailed error reporting

.NOTES
    Author: Julien Bombled
    v3.7.2
    Requires: PowerShell 5.1+
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

# === MODULE INITIALIZATION ===
$script:ModuleRoot = Split-Path -Parent $PSCommandPath
$script:RepositoryRoot = Split-Path $script:ModuleRoot -Parent
$script:SchemasDirectory = Join-Path $script:RepositoryRoot 'Schemas'
$script:CoreModulePath = Join-Path $script:RepositoryRoot 'Core\Core.psm1'

if (-not (Get-Command -Name Write-Status -ErrorAction SilentlyContinue)) {
    if (Test-Path -Path $script:CoreModulePath) {
        Import-Module -Name $script:CoreModulePath -Force
    }
}

# Import Localization module for i18n support
$script:LocalizationModulePath = Join-Path $script:RepositoryRoot 'Core\Localization.psm1'
if (-not (Get-Command -Name Get-LogString -ErrorAction SilentlyContinue)) {
    if (Test-Path -Path $script:LocalizationModulePath) {
        Import-Module -Name $script:LocalizationModulePath -Force
    }
}

# === SCHEMA PATHS ===
$script:ProfileSchemaPath = Join-Path $script:SchemasDirectory 'deployment-profile.schema.json'
$script:DatabaseSchemaPath = Join-Path $script:SchemasDirectory 'applications-database.schema.json'

# === SECURITY LIMITS ===
# Maximum JSON file size to prevent DoS via large payloads (10 MB)
$script:MaxJsonFileSizeBytes = 10 * 1024 * 1024

# === VALIDATION RESULT CLASS ===

class JsonValidationResult {
    [bool]$IsValid
    [string]$FilePath
    [string]$SchemaPath
    [System.Collections.Generic.List[string]]$Errors
    [System.Collections.Generic.List[string]]$Warnings

    JsonValidationResult() {
        $this.IsValid = $true
        $this.Errors = [System.Collections.Generic.List[string]]::new()
        $this.Warnings = [System.Collections.Generic.List[string]]::new()
    }

    [void] AddError([string]$Message) {
        $this.IsValid = $false
        $this.Errors.Add($Message)
    }

    [void] AddWarning([string]$Message) {
        $this.Warnings.Add($Message)
    }
}

# === CORE VALIDATION FUNCTIONS ===

function Test-JsonSyntax {
    <#
    .SYNOPSIS
        Tests if a file contains valid JSON syntax.
    .DESCRIPTION
        Attempts to parse the specified file as JSON and reports whether the syntax is valid.
        Includes security checks for file existence and maximum file size to prevent denial
        of service from oversized inputs.

    .PARAMETER Path
        Path to the JSON file to validate.

    .OUTPUTS
        [JsonValidationResult] Validation result.
    #>
    [CmdletBinding()]
    [OutputType([JsonValidationResult])]
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    $result = [JsonValidationResult]::new()
    $result.FilePath = $Path

    if (-not (Test-Path $Path)) {
        $result.AddError("File not found: $Path")
        return $result
    }

    # Security: Check file size before reading to prevent DoS
    $fileInfo = Get-Item -Path $Path -ErrorAction SilentlyContinue
    if ($fileInfo -and $fileInfo.Length -gt $script:MaxJsonFileSizeBytes) {
        $result.AddError("File exceeds maximum allowed size of $($script:MaxJsonFileSizeBytes / 1MB) MB: $Path")
        return $result
    }

    try {
        $content = Get-Content -Path $Path -Raw -ErrorAction Stop
        $null = $content | ConvertFrom-Json -ErrorAction Stop
    }
    catch {
        $result.AddError("JSON syntax error: $($_.Exception.Message)")
    }

    return $result
}

function Test-JsonAgainstSchema {
    <#
    .SYNOPSIS
        Validates a JSON file against a JSON Schema.

    .DESCRIPTION
        Performs structural validation of a JSON file against the specified
        JSON Schema. Validates types, required properties, patterns, and
        additional constraints defined in the schema.

    .PARAMETER JsonPath
        Path to the JSON file to validate.

    .PARAMETER SchemaPath
        Path to the JSON Schema file.

    .OUTPUTS
        [JsonValidationResult] Validation result with errors and warnings.

    .EXAMPLE
        Test-JsonAgainstSchema -JsonPath 'Profiles/Base.json' -SchemaPath 'Schemas/deployment-profile.schema.json'
    #>
    [CmdletBinding()]
    [OutputType([JsonValidationResult])]
    param(
        [Parameter(Mandatory)]
        [string]$JsonPath,

        [Parameter(Mandatory)]
        [string]$SchemaPath
    )

    $result = [JsonValidationResult]::new()
    $result.FilePath = $JsonPath
    $result.SchemaPath = $SchemaPath

    # Validate file exists
    if (-not (Test-Path $JsonPath)) {
        $result.AddError("JSON file not found: $JsonPath")
        return $result
    }

    if (-not (Test-Path $SchemaPath)) {
        $result.AddError((Get-LogString 'validation.schema.file_not_found' @{ Path = $SchemaPath }))
        return $result
    }

    # Security: Check file sizes before reading to prevent DoS
    $jsonFileInfo = Get-Item -Path $JsonPath -ErrorAction SilentlyContinue
    if ($jsonFileInfo -and $jsonFileInfo.Length -gt $script:MaxJsonFileSizeBytes) {
        $result.AddError("JSON file exceeds maximum allowed size of $($script:MaxJsonFileSizeBytes / 1MB) MB: $JsonPath")
        return $result
    }

    $schemaFileInfo = Get-Item -Path $SchemaPath -ErrorAction SilentlyContinue
    if ($schemaFileInfo -and $schemaFileInfo.Length -gt $script:MaxJsonFileSizeBytes) {
        $result.AddError("Schema file exceeds maximum allowed size of $($script:MaxJsonFileSizeBytes / 1MB) MB: $SchemaPath")
        return $result
    }

    try {
        # Parse JSON and Schema
        $jsonContent = Get-Content -Path $JsonPath -Raw -ErrorAction Stop
        $json = $jsonContent | ConvertFrom-Json -ErrorAction Stop

        $schemaContent = Get-Content -Path $SchemaPath -Raw -ErrorAction Stop
        $schema = $schemaContent | ConvertFrom-Json -ErrorAction Stop

        # Perform validation
        $validationErrors = Test-ObjectAgainstSchema -Object $json -Schema $schema -Path '$'

        foreach ($error in $validationErrors) {
            $result.AddError($error)
        }
    }
    catch {
        $result.AddError("Validation error: $($_.Exception.Message)")
    }

    return $result
}

function script:Get-SchemaProperty {
    <#
    .SYNOPSIS
        Safely gets a property from a schema object.
    .DESCRIPTION
        Retrieves a named property from a schema that may be either a PSCustomObject or a
        hashtable, returning null if the property does not exist or the schema is null.
    #>
    param($Schema, [string]$PropertyName)

    if ($null -eq $Schema) { return $null }
    if ($Schema -is [PSCustomObject] -and $Schema.PSObject.Properties.Name -contains $PropertyName) {
        return $Schema.$PropertyName
    }
    if ($Schema -is [hashtable] -and $Schema.ContainsKey($PropertyName)) {
        return $Schema[$PropertyName]
    }
    return $null
}

function Test-ObjectAgainstSchema {
    <#
    .SYNOPSIS
        Recursively validates an object against a JSON Schema.
    .DESCRIPTION
        Performs recursive validation of a PowerShell object against a JSON Schema Draft 7
        definition, checking type constraints, required properties, additional properties,
        array items, enum values, pattern matching, and oneOf combinators.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        $Object,

        [Parameter(Mandatory)]
        $Schema,

        [Parameter(Mandatory)]
        [string]$Path
    )

    $errors = New-Object 'System.Collections.Generic.List[object]'

    function Add-ValidationError {
        param([object]$ErrorItem)
        $errors.Add($ErrorItem) | Out-Null
    }

    function Add-ValidationErrors {
        param([object[]]$ErrorItems)
        foreach ($errorItem in $ErrorItems) {
            $errors.Add($errorItem) | Out-Null
        }
    }

    # Handle null object
    if ($null -eq $Object) {
        $schemaType = Get-SchemaProperty -Schema $Schema -PropertyName 'type'
        if ($schemaType -and $schemaType -ne 'null') {
            Add-ValidationError (Get-LogString 'validation.schema.type_mismatch' @{ Expected = $schemaType; Actual = 'null'; Path = $Path })
        }
        return $errors.ToArray()
    }

    # Type validation
    $schemaType = Get-SchemaProperty -Schema $Schema -PropertyName 'type'
    if ($schemaType) {
        $actualType = Get-JsonType -Value $Object
        $expectedTypes = if ($schemaType -is [array]) { $schemaType } else { @($schemaType) }

        if ($actualType -notin $expectedTypes) {
            Add-ValidationError (Get-LogString 'validation.schema.type_mismatch' @{ Expected = ($expectedTypes -join ' or '); Actual = $actualType; Path = $Path })
            return $errors.ToArray()
        }
    }

    # Object validation
    if ($schemaType -eq 'object' -or ($null -eq $schemaType -and $Object -is [PSCustomObject])) {
        # Required properties
        $requiredProps = Get-SchemaProperty -Schema $Schema -PropertyName 'required'
        if ($requiredProps) {
            foreach ($requiredProp in $requiredProps) {
                if (-not ($Object.PSObject.Properties.Name -contains $requiredProp)) {
                    Add-ValidationError (Get-LogString 'validation.schema.required_missing' @{ Property = $requiredProp; Path = $Path })
                }
            }
        }

        # Property validation
        $schemaProps = Get-SchemaProperty -Schema $Schema -PropertyName 'properties'
        if ($schemaProps) {
            foreach ($prop in $Object.PSObject.Properties) {
                $propPath = "$Path.$($prop.Name)"

                if ($schemaProps.PSObject.Properties.Name -contains $prop.Name) {
                    $propSchema = $schemaProps.($prop.Name)
                    Add-ValidationErrors (Test-ObjectAgainstSchema -Object $prop.Value -Schema $propSchema -Path $propPath)
                }
                elseif ((Get-SchemaProperty -Schema $Schema -PropertyName 'additionalProperties') -eq $false) {
                    Add-ValidationError "[$propPath] Additional property not allowed"
                }
            }
        }
    }

    # Array validation
    if ($schemaType -eq 'array' -and $Object -is [array]) {
        $arrayObj = @($Object)
        $schemaItems = Get-SchemaProperty -Schema $Schema -PropertyName 'items'
        if ($schemaItems) {
            for ($i = 0; $i -lt $arrayObj.Count; $i++) {
                $itemPath = "$Path[$i]"

                # Handle oneOf in items
                $oneOf = Get-SchemaProperty -Schema $schemaItems -PropertyName 'oneOf'
                if ($oneOf) {
                    $validForAny = $false
                    foreach ($subSchema in $oneOf) {
                        $subErrors = @(Test-ObjectAgainstSchema -Object $arrayObj[$i] -Schema $subSchema -Path $itemPath)
                        if ($subErrors.Count -eq 0) {
                            $validForAny = $true
                            break
                        }
                    }
                    if (-not $validForAny) {
                        Add-ValidationError "[$itemPath] Value does not match any of the allowed schemas"
                    }
                }
                else {
                    Add-ValidationErrors (Test-ObjectAgainstSchema -Object $arrayObj[$i] -Schema $schemaItems -Path $itemPath)
                }
            }
        }

        $uniqueItems = Get-SchemaProperty -Schema $Schema -PropertyName 'uniqueItems'
        if ($uniqueItems -and $arrayObj.Count -ne @($arrayObj | Select-Object -Unique).Count) {
            Add-ValidationError "[$Path] Array items must be unique"
        }

        $minItems = Get-SchemaProperty -Schema $Schema -PropertyName 'minItems'
        if ($minItems -and $arrayObj.Count -lt $minItems) {
            Add-ValidationError "[$Path] Array must have at least $minItems items, has $($arrayObj.Count)"
        }

        $maxItems = Get-SchemaProperty -Schema $Schema -PropertyName 'maxItems'
        if ($maxItems -and $arrayObj.Count -gt $maxItems) {
            Add-ValidationError "[$Path] Array must have at most $maxItems items, has $($arrayObj.Count)"
        }
    }

    # String validation
    if ($schemaType -eq 'string' -and $Object -is [string]) {
        $minLength = Get-SchemaProperty -Schema $Schema -PropertyName 'minLength'
        if ($minLength -and $Object.Length -lt $minLength) {
            Add-ValidationError "[$Path] String length $($Object.Length) is less than minimum $minLength"
        }

        $maxLength = Get-SchemaProperty -Schema $Schema -PropertyName 'maxLength'
        if ($maxLength -and $Object.Length -gt $maxLength) {
            Add-ValidationError "[$Path] String length $($Object.Length) exceeds maximum $maxLength"
        }

        $pattern = Get-SchemaProperty -Schema $Schema -PropertyName 'pattern'
        if ($pattern) {
            if ($Object -notmatch $pattern) {
                Add-ValidationError "[$Path] String '$Object' does not match pattern '$pattern'"
            }
        }

        $enum = Get-SchemaProperty -Schema $Schema -PropertyName 'enum'
        if ($enum) {
            if ($Object -notin $enum) {
                Add-ValidationError "[$Path] Value '$Object' is not one of allowed values: $($enum -join ', ')"
            }
        }
    }

    # Number validation
    if (($schemaType -eq 'integer' -or $schemaType -eq 'number') -and $Object -is [ValueType]) {
        $minimum = Get-SchemaProperty -Schema $Schema -PropertyName 'minimum'
        if ($null -ne $minimum -and $Object -lt $minimum) {
            Add-ValidationError "[$Path] Value $Object is less than minimum $minimum"
        }

        $maximum = Get-SchemaProperty -Schema $Schema -PropertyName 'maximum'
        if ($null -ne $maximum -and $Object -gt $maximum) {
            Add-ValidationError "[$Path] Value $Object exceeds maximum $maximum"
        }
    }

    return $errors.ToArray()
}

function Get-JsonType {
    <#
    .SYNOPSIS
        Gets the JSON type name for a PowerShell value.
    .DESCRIPTION
        Maps a PowerShell value to its corresponding JSON Schema type name (null, boolean,
        string, array, integer, number, or object) for use during schema validation.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        $Value
    )

    if ($null -eq $Value) {
        return 'null'
    }

    if ($Value -is [bool]) {
        return 'boolean'
    }

    if ($Value -is [string]) {
        return 'string'
    }

    if ($Value -is [array]) {
        return 'array'
    }

    if ($Value -is [int] -or $Value -is [long]) {
        return 'integer'
    }

    if ($Value -is [double] -or $Value -is [decimal] -or $Value -is [float]) {
        return 'number'
    }

    if ($Value -is [PSCustomObject] -or $Value -is [hashtable]) {
        return 'object'
    }

    return 'unknown'
}

# === HIGH-LEVEL VALIDATION FUNCTIONS ===

function Test-DeploymentProfile {
    <#
    .SYNOPSIS
        Validates a deployment profile against the schema.
    .DESCRIPTION
        Validates a deployment profile JSON file against the profile JSON Schema definition.
        Returns a validation result indicating whether the profile conforms to the expected
        structure and constraints.

    .PARAMETER ProfilePath
        Path to the profile JSON file.

    .OUTPUTS
        [JsonValidationResult] Validation result.

    .EXAMPLE
        Test-DeploymentProfile -ProfilePath 'Profiles/Base.json'
    #>
    [CmdletBinding()]
    [OutputType([JsonValidationResult])]
    param(
        [Parameter(Mandatory)]
        [string]$ProfilePath
    )

    if (-not (Test-Path $script:ProfileSchemaPath)) {
        $result = [JsonValidationResult]::new()
        $result.FilePath = $ProfilePath
        $result.AddError((Get-LogString 'validation.schema.file_not_found' @{ Path = $script:ProfileSchemaPath }))
        return $result
    }

    return Test-JsonAgainstSchema -JsonPath $ProfilePath -SchemaPath $script:ProfileSchemaPath
}

function Test-ApplicationsDatabase {
    <#
    .SYNOPSIS
        Validates the applications database against the schema.
    .DESCRIPTION
        Validates the applications database JSON file against the database JSON Schema
        definition. Defaults to the standard database location if no path is provided.

    .PARAMETER DatabasePath
        Path to the applications database JSON file. Defaults to standard location.

    .OUTPUTS
        [JsonValidationResult] Validation result.

    .EXAMPLE
        Test-ApplicationsDatabase
    #>
    [CmdletBinding()]
    [OutputType([JsonValidationResult])]
    param(
        [Parameter()]
        [string]$DatabasePath
    )

    if (-not $DatabasePath) {
        $DatabasePath = Join-Path $script:RepositoryRoot 'Apps\Database\applications.json'
    }

    if (-not (Test-Path $script:DatabaseSchemaPath)) {
        $result = [JsonValidationResult]::new()
        $result.FilePath = $DatabasePath
        $result.AddError((Get-LogString 'validation.schema.file_not_found' @{ Path = $script:DatabaseSchemaPath }))
        return $result
    }

    return Test-JsonAgainstSchema -JsonPath $DatabasePath -SchemaPath $script:DatabaseSchemaPath
}

function Test-AllProfiles {
    <#
    .SYNOPSIS
        Validates all profiles in the Profiles directory.
    .DESCRIPTION
        Iterates over all JSON files in the specified profiles directory and validates each
        one against the profile schema, returning an array of validation results that can
        be filtered to identify invalid profiles.

    .PARAMETER ProfilesDirectory
        Directory containing profile JSON files. Defaults to standard location.

    .OUTPUTS
        [JsonValidationResult[]] Array of validation results.

    .EXAMPLE
        Test-AllProfiles | Where-Object { -not $_.IsValid }
    #>
    [CmdletBinding()]
    [OutputType([JsonValidationResult[]])]
    param(
        [Parameter()]
        [string]$ProfilesDirectory
    )

    if (-not $ProfilesDirectory) {
        $ProfilesDirectory = Join-Path $script:RepositoryRoot 'Profiles'
    }

    $results = @()

    $profiles = Get-ChildItem -Path $ProfilesDirectory -Filter '*.json' -ErrorAction SilentlyContinue

    foreach ($profile in $profiles) {
        Write-Status -Message "Validating profile: $($profile.Name)" -Level 'Verbose'
        $results += Test-DeploymentProfile -ProfilePath $profile.FullName
    }

    return $results
}

function Invoke-JsonSchemaValidation {
    <#
    .SYNOPSIS
        Validates all WinForge JSON configuration files.

    .DESCRIPTION
        Performs comprehensive validation of all JSON configuration files
        including profiles and the applications database.

    .PARAMETER FailOnWarning
        If specified, treat warnings as failures.

    .OUTPUTS
        [hashtable] Summary of validation results.

    .EXAMPLE
        Invoke-JsonSchemaValidation
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [Parameter()]
        [switch]$FailOnWarning
    )

    $summary = @{
        TotalFiles = 0
        ValidFiles = 0
        InvalidFiles = 0
        TotalErrors = 0
        TotalWarnings = 0
        Results = @()
    }

    Write-Status -Message 'Starting JSON Schema validation...' -Level 'Info'

    # Validate profiles
    Write-Status -Message 'Validating deployment profiles...' -Level 'Info'
    $profileResults = Test-AllProfiles

    foreach ($result in $profileResults) {
        $summary.TotalFiles++
        $summary.TotalErrors += $result.Errors.Count
        $summary.TotalWarnings += $result.Warnings.Count

        if ($result.IsValid -and (-not $FailOnWarning -or $result.Warnings.Count -eq 0)) {
            $summary.ValidFiles++
            Write-Status -Message "  [OK] $($result.FilePath | Split-Path -Leaf)" -Level 'Success'
        }
        else {
            $summary.InvalidFiles++
            Write-Status -Message "  [FAIL] $($result.FilePath | Split-Path -Leaf)" -Level 'Error'
            foreach ($error in $result.Errors) {
                Write-Status -Message "    - $error" -Level 'Error'
            }
        }

        $summary.Results += $result
    }

    # Validate applications database
    Write-Status -Message 'Validating applications database...' -Level 'Info'
    $dbResult = Test-ApplicationsDatabase

    $summary.TotalFiles++
    $summary.TotalErrors += $dbResult.Errors.Count
    $summary.TotalWarnings += $dbResult.Warnings.Count

    if ($dbResult.IsValid -and (-not $FailOnWarning -or $dbResult.Warnings.Count -eq 0)) {
        $summary.ValidFiles++
        Write-Status -Message '  [OK] applications.json' -Level 'Success'
    }
    else {
        $summary.InvalidFiles++
        Write-Status -Message '  [FAIL] applications.json' -Level 'Error'
        foreach ($error in $dbResult.Errors) {
            Write-Status -Message "    - $error" -Level 'Error'
        }
    }

    $summary.Results += $dbResult

    # Summary
    Write-Status -Message '=== Validation Summary ===' -Level 'Info'
    Write-Status -Message "  Total files: $($summary.TotalFiles)" -Level 'Info'
    Write-Status -Message "  Valid: $($summary.ValidFiles)" -Level 'Success'
    Write-Status -Message "  Invalid: $($summary.InvalidFiles)" -Level $(if ($summary.InvalidFiles -gt 0) { 'Error' } else { 'Info' })
    Write-Status -Message "  Total errors: $($summary.TotalErrors)" -Level $(if ($summary.TotalErrors -gt 0) { 'Error' } else { 'Info' })
    Write-Status -Message "  Total warnings: $($summary.TotalWarnings)" -Level $(if ($summary.TotalWarnings -gt 0) { 'Warning' } else { 'Info' })

    return $summary
}

# === MODULE EXPORTS ===

Export-ModuleMember -Function @(
    'Test-JsonSyntax',
    'Test-JsonAgainstSchema',
    'Test-DeploymentProfile',
    'Test-ApplicationsDatabase',
    'Test-AllProfiles',
    'Invoke-JsonSchemaValidation'
)

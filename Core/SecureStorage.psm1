<#
.SYNOPSIS
    WinForge - Secure Storage v3.7.2

.DESCRIPTION
    Provides secure storage capabilities using Windows DPAPI:
    - Encryption/decryption of sensitive data
    - Secure API key storage
    - Protection scope: CurrentUser (machine-bound)

.NOTES
    Author: Julien Bombled
    v3.7.2
    Uses Windows Data Protection API (DPAPI) for encryption
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

# Load System.Security assembly for DPAPI types (required for PowerShell 5.1)
Add-Type -AssemblyName System.Security

# === MODULE INITIALIZATION ===
$script:ModuleRoot = Split-Path -Parent $PSCommandPath
$script:RepositoryRoot = Split-Path $script:ModuleRoot -Parent
# Import DirectoryConstants for path management
$script:DirectoryConstantsPath = Join-Path $script:ModuleRoot 'DirectoryConstants.psm1'
if (-not (Get-Command -Name Get-WinForgeDirectory -ErrorAction SilentlyContinue)) {
    if (Test-Path -Path $script:DirectoryConstantsPath) {
        Import-Module -Name $script:DirectoryConstantsPath -Force
    }
}

$script:SecureStoragePath = Get-StatePath -PathKey 'SecureStorage'
$script:SecureApiKeysPath = Get-StatePath -PathKey 'ApiKeys'

# Ensure directory exists
$secureDir = Split-Path $script:SecureStoragePath -Parent
if (-not (Test-Path $secureDir)) {
    New-Item -Path $secureDir -ItemType Directory -Force | Out-Null
}

# Security: Per-user entropy file path
$script:EntropyPath = Get-StatePath -PathKey 'Entropy'
# Mutex name for cross-process synchronization (per-user to avoid privilege issues)
$script:EntropyMutexName = "Local\WinForge_Entropy_$($env:USERNAME)"
# In-memory entropy cache to ensure consistency within a session
$script:CachedEntropy = $null

# Import Localization module for i18n support
$script:LocalizationModulePath = Join-Path $script:ModuleRoot 'Localization.psm1'
if (-not (Get-Command -Name Get-LogString -ErrorAction SilentlyContinue)) {
    if (Test-Path -Path $script:LocalizationModulePath) {
        try {
            Import-Module -Name $script:LocalizationModulePath -Force -ErrorAction SilentlyContinue
        } catch { Write-Debug "Localization module not available: $($_.Exception.Message)" }
    }
}

# Import WinForgeExceptions for custom exception types
$script:ExceptionsPath = Join-Path $script:ModuleRoot 'WinForgeExceptions.psm1'
if (-not (Get-Command -Name New-SecurityException -ErrorAction SilentlyContinue)) {
    if (Test-Path -Path $script:ExceptionsPath) {
        Import-Module -Name $script:ExceptionsPath -Force
    }
}

# === ENTROPY MANAGEMENT ===

function Set-SecureFileAcl {
    <#
    .SYNOPSIS
        Sets restrictive ACL on a file (user-only access).
    .DESCRIPTION
        Removes inherited permissions and grants only the current user full control.
        This prevents other processes running as the same user from reading the file
        through inherited directory permissions.
    .PARAMETER Path
        The file path to secure.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    if (-not (Test-Path $Path)) {
        return
    }

    try {
        $acl = Get-Acl -Path $Path
        # Disable inheritance and remove inherited rules
        $acl.SetAccessRuleProtection($true, $false)
        # Clear all existing rules
        $acl.Access | ForEach-Object { $acl.RemoveAccessRule($_) } | Out-Null
        # Add only current user with FullControl
        $currentUser = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name
        $rule = New-Object System.Security.AccessControl.FileSystemAccessRule(
            $currentUser,
            [System.Security.AccessControl.FileSystemRights]::FullControl,
            [System.Security.AccessControl.AccessControlType]::Allow
        )
        $acl.AddAccessRule($rule)
        Set-Acl -Path $Path -AclObject $acl -ErrorAction Stop
        Write-Verbose (Get-LogString 'security.storage.acl_set' @{ Path = $Path })
    } catch {
        Write-Verbose (Get-LogString 'security.storage.acl_failed' @{ Path = $Path; Error = $_.Exception.Message })
    }
}

function Assert-SecureStoragePermissions {
    <#
    .SYNOPSIS
        Ensures all secure storage files have proper restrictive permissions.
    .DESCRIPTION
        Called at module load to verify and fix permissions on existing secure files.
    #>
    [CmdletBinding()]
    param()

    # Secure entropy file
    if (Test-Path $script:EntropyPath) {
        Set-SecureFileAcl -Path $script:EntropyPath
    }

    # Secure API keys file
    if (Test-Path $script:SecureApiKeysPath) {
        Set-SecureFileAcl -Path $script:SecureApiKeysPath
    }

    # Secure generic storage file
    if (Test-Path $script:SecureStoragePath) {
        Set-SecureFileAcl -Path $script:SecureStoragePath
    }
}

# Ensure permissions on module load
Assert-SecureStoragePermissions

function Get-DpapiEntropy {
    <#
    .SYNOPSIS
        Gets or creates per-user random entropy for DPAPI.
    .DESCRIPTION
        Generates a random 32-byte entropy value on first use and stores it
        securely in the user's LOCALAPPDATA. This provides an additional
        layer of security beyond DPAPI's default protection.

        Uses a named mutex to prevent race conditions (TOCTOU) when multiple
        processes access the entropy file simultaneously.
    .OUTPUTS
        [byte[]] The entropy bytes for DPAPI operations.
    #>
    [CmdletBinding()]
    [OutputType([byte[]])]
    param()

    # Return cached entropy if available (avoids file I/O on every call)
    if ($null -ne $script:CachedEntropy) {
        return $script:CachedEntropy
    }

    $mutex = $null
    $mutexAcquired = $false

    try {
        # Create or open the named mutex for cross-process synchronization
        $mutex = [System.Threading.Mutex]::new($false, $script:EntropyMutexName)

        # Wait for exclusive access (2 second timeout to mitigate attack surface)
        $mutexAcquired = $mutex.WaitOne(2000)
        if (-not $mutexAcquired) {
            Write-Warning (Get-LogString 'security.storage.mutex_timeout')
        }

        # Try to read existing entropy file atomically (no separate Test-Path check)
        try {
            $entropyBytes = [System.IO.File]::ReadAllBytes($script:EntropyPath)
            if ($entropyBytes.Length -eq 32) {
                $script:CachedEntropy = $entropyBytes
                return $entropyBytes
            }
            # Invalid entropy file, will regenerate below
            Write-Verbose (Get-LogString 'security.storage.entropy_regenerating' @{ Length = $entropyBytes.Length })
        } catch [System.IO.FileNotFoundException] {
            # File doesn't exist, will create below
            Write-Verbose (Get-LogString 'security.storage.entropy_not_found')
        } catch [System.IO.DirectoryNotFoundException] {
            # Directory doesn't exist, will create below
            Write-Verbose (Get-LogString 'security.storage.entropy_dir_not_found')
        } catch {
            Write-Verbose (Get-LogString 'security.storage.entropy_read_failed' @{ Error = $_.Exception.Message })
        }

        # Generate new random entropy (32 bytes = 256 bits)
        $entropyBytes = [byte[]]::new(32)
        $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
        try {
            $rng.GetBytes($entropyBytes)
        } finally {
            $rng.Dispose()
        }

        # Store entropy securely with atomic write
        try {
            $entropyDir = Split-Path $script:EntropyPath -Parent

            # Create directory if needed (idempotent operation)
            if (-not (Test-Path $entropyDir)) {
                New-Item -Path $entropyDir -ItemType Directory -Force | Out-Null
            }

            # Use a temporary file and atomic rename for safe writes
            $tempPath = "$script:EntropyPath.tmp.$PID"
            try {
                $fileStream = $null
                try {
                    # Write entropy to temp file (compatible with both .NET Framework and .NET Core)
                    $fileStream = [System.IO.FileStream]::new(
                        $tempPath,
                        [System.IO.FileMode]::Create,
                        [System.IO.FileAccess]::Write,
                        [System.IO.FileShare]::None,
                        4096,
                        [System.IO.FileOptions]::WriteThrough
                    )
                    $fileStream.Write($entropyBytes, 0, $entropyBytes.Length)
                    $fileStream.Flush()
                } finally {
                    if ($fileStream) { $fileStream.Dispose() }
                }

                # Apply restrictive ACL before moving to final path
                Set-SecureFileAcl -Path $tempPath

                # Atomic move (rename) to final path
                if ($PSVersionTable.PSVersion.Major -ge 7) {
                    [System.IO.File]::Move($tempPath, $script:EntropyPath, $true)
                } else {
                    # .NET Framework File.Move does not support overwrite parameter
                    if (Test-Path $script:EntropyPath) {
                        Remove-Item -Path $script:EntropyPath -Force
                    }
                    [System.IO.File]::Move($tempPath, $script:EntropyPath)
                }
            } finally {
                # Clean up temp file if it still exists (move failed)
                if (Test-Path $tempPath) {
                    Remove-Item -Path $tempPath -Force -ErrorAction SilentlyContinue
                }
            }
        } catch {
            Write-Verbose (Get-LogString 'security.storage.entropy_persist_failed' @{ Error = $_.Exception.Message })
        }

        $script:CachedEntropy = $entropyBytes
        return $entropyBytes
    } finally {
        # Always release the mutex
        if ($mutexAcquired -and $mutex) {
            $mutex.ReleaseMutex()
        }
        if ($mutex) {
            $mutex.Dispose()
        }
    }
}

# === DPAPI FUNCTIONS ===

function Protect-Data {
    <#
    .SYNOPSIS
        Encrypts data using Windows DPAPI.

    .DESCRIPTION
        Uses Windows Data Protection API to encrypt sensitive data.
        The encrypted data can only be decrypted by the same user on the same machine.

    .PARAMETER PlainText
        The text to encrypt.

    .PARAMETER Scope
        Protection scope: CurrentUser (default) or LocalMachine.

    .OUTPUTS
        [string] Base64-encoded encrypted data.

    .EXAMPLE
        $encrypted = Protect-Data -PlainText "my-secret-api-key"
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory, ValueFromPipeline)]
        [string]$PlainText,

        [Parameter()]
        [ValidateSet('CurrentUser', 'LocalMachine')]
        [string]$Scope = 'CurrentUser'
    )

    process {
        try {
            $bytes = [System.Text.Encoding]::UTF8.GetBytes($PlainText)
            # Security: Use per-user random entropy instead of hardcoded value
            $entropy = Get-DpapiEntropy

            $protectionScope = switch ($Scope) {
                'LocalMachine' { [System.Security.Cryptography.DataProtectionScope]::LocalMachine }
                default { [System.Security.Cryptography.DataProtectionScope]::CurrentUser }
            }

            $encryptedBytes = [System.Security.Cryptography.ProtectedData]::Protect(
                $bytes,
                $entropy,
                $protectionScope
            )

            return [Convert]::ToBase64String($encryptedBytes)
        } catch {
            throw (New-SecurityException -Message (Get-LogString 'security.storage.encrypt_failed' @{ Error = $_.Exception.Message }))
        }
    }
}

function Unprotect-Data {
    <#
    .SYNOPSIS
        Decrypts data that was encrypted using Windows DPAPI.

    .DESCRIPTION
        Uses Windows Data Protection API to decrypt data.
        The data must have been encrypted by the same user on the same machine.

    .PARAMETER EncryptedData
        Base64-encoded encrypted data from Protect-Data.

    .PARAMETER Scope
        Protection scope: CurrentUser (default) or LocalMachine.

    .OUTPUTS
        [string] The decrypted plain text.

    .EXAMPLE
        $plainText = Unprotect-Data -EncryptedData $encryptedString
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory, ValueFromPipeline)]
        [string]$EncryptedData,

        [Parameter()]
        [ValidateSet('CurrentUser', 'LocalMachine')]
        [string]$Scope = 'CurrentUser'
    )

    process {
        try {
            $encryptedBytes = [Convert]::FromBase64String($EncryptedData)
            # Security: Use per-user random entropy instead of hardcoded value
            $entropy = Get-DpapiEntropy

            $protectionScope = switch ($Scope) {
                'LocalMachine' { [System.Security.Cryptography.DataProtectionScope]::LocalMachine }
                default { [System.Security.Cryptography.DataProtectionScope]::CurrentUser }
            }

            $decryptedBytes = [System.Security.Cryptography.ProtectedData]::Unprotect(
                $encryptedBytes,
                $entropy,
                $protectionScope
            )

            return [System.Text.Encoding]::UTF8.GetString($decryptedBytes)
        } catch {
            throw (New-SecurityException -Message (Get-LogString 'security.storage.decrypt_failed' @{ Error = $_.Exception.Message }))
        }
    }
}

# === SECURE API KEY STORAGE ===

function Save-SecureApiKey {
    <#
    .SYNOPSIS
        Saves an API key securely using DPAPI encryption.

    .DESCRIPTION
        Encrypts and stores an API key with its metadata.
        Keys are stored in %LOCALAPPDATA%\WinForge\api-keys.secure

    .PARAMETER KeyId
        Unique identifier for the key.

    .PARAMETER ApiKey
        The API key to encrypt and store.

    .PARAMETER Description
        Optional description for the key.

    .PARAMETER Permissions
        Array of permissions for the key.

    .PARAMETER ExpiresAt
        Optional expiration date (ISO format).

    .EXAMPLE
        Save-SecureApiKey -KeyId 'automation' -ApiKey 'w11f_abc123' -Permissions @('read', 'deploy')
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$KeyId,

        [Parameter(Mandatory)]
        [string]$ApiKey,

        [Parameter()]
        [string]$Description = '',

        [Parameter()]
        [string[]]$Permissions = @('read'),

        [Parameter()]
        [string]$ExpiresAt = $null
    )

    try {
        # Load existing keys
        $keys = Get-SecureApiKeys -IncludeDecrypted

        # Encrypt the API key
        $encryptedKey = Protect-Data -PlainText $ApiKey

        # Add or update key
        $keyEntry = @{
            Id = $KeyId
            EncryptedKey = $encryptedKey
            Description = $Description
            Permissions = $Permissions
            CreatedAt = (Get-Date).ToString('yyyy-MM-dd')
            ExpiresAt = $ExpiresAt
            Enabled = $true
        }

        $keys[$KeyId] = $keyEntry

        # Save to secure storage
        $json = $keys | ConvertTo-Json -Depth 5
        $encryptedStorage = Protect-Data -PlainText $json
        Set-Content -Path $script:SecureApiKeysPath -Value $encryptedStorage -Force

        Write-Verbose (Get-LogString 'security.storage.api_key_saved' @{ KeyId = $KeyId })
        return $true
    } catch {
        Write-Error (Get-LogString 'security.storage.api_key_save_failed' @{ Error = $_.Exception.Message })
        return $false
    }
}

function Get-SecureApiKeys {
    <#
    .SYNOPSIS
        Retrieves securely stored API keys.

    .DESCRIPTION
        Loads API keys from secure storage and optionally decrypts them.

    .PARAMETER IncludeDecrypted
        If specified, includes decrypted API keys in the output.
        Use with caution - only when keys are needed for authentication.

    .PARAMETER KeyId
        Optional filter to get a specific key by ID.

    .OUTPUTS
        [hashtable] API keys (with or without decrypted values).

    .EXAMPLE
        $keys = Get-SecureApiKeys
        $allKeys = Get-SecureApiKeys -IncludeDecrypted
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [Parameter()]
        [switch]$IncludeDecrypted,

        [Parameter()]
        [string]$KeyId
    )

    try {
        if (-not (Test-Path $script:SecureApiKeysPath)) {
            return @{}
        }

        $encryptedStorage = Get-Content -Path $script:SecureApiKeysPath -Raw
        $json = Unprotect-Data -EncryptedData $encryptedStorage
        $keys = $json | ConvertFrom-Json -AsHashtable

        if ($KeyId -and $keys.ContainsKey($KeyId)) {
            $keys = @{ $KeyId = $keys[$KeyId] }
        }

        if ($IncludeDecrypted) {
            foreach ($id in $keys.Keys) {
                $keyEntry = $keys[$id]
                if ($keyEntry.EncryptedKey) {
                    $keyEntry['DecryptedKey'] = Unprotect-Data -EncryptedData $keyEntry.EncryptedKey
                }
            }
        }

        return $keys
    } catch {
        Write-Verbose (Get-LogString 'security.storage.api_keys_load_failed' @{ Error = $_.Exception.Message })
        return @{}
    }
}

function Remove-SecureApiKey {
    <#
    .SYNOPSIS
        Removes an API key from secure storage.
    .DESCRIPTION
        Deletes the specified API key entry from the DPAPI-encrypted key store. If no keys remain
        after removal, the entire secure API keys file is deleted.

    .PARAMETER KeyId
        The ID of the key to remove.

    .EXAMPLE
        Remove-SecureApiKey -KeyId 'automation'
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$KeyId
    )

    try {
        $keys = Get-SecureApiKeys -IncludeDecrypted

        if ($keys.ContainsKey($KeyId)) {
            $keys.Remove($KeyId)

            if ($keys.Count -eq 0) {
                Remove-Item -Path $script:SecureApiKeysPath -Force -ErrorAction SilentlyContinue
            } else {
                $json = $keys | ConvertTo-Json -Depth 5
                $encryptedStorage = Protect-Data -PlainText $json
                Set-Content -Path $script:SecureApiKeysPath -Value $encryptedStorage -Force
            }

            Write-Verbose (Get-LogString 'security.storage.api_key_removed' @{ KeyId = $KeyId })
            return $true
        }

        return $false
    } catch {
        Write-Error (Get-LogString 'security.storage.api_key_remove_failed' @{ Error = $_.Exception.Message })
        return $false
    }
}

function Get-SecureApiKeysForAuth {
    <#
    .SYNOPSIS
        Gets API keys in the format expected by RestApiServer authentication.

    .DESCRIPTION
        Returns a hashtable keyed by the actual API key value for fast lookup.
        This is used internally by the REST API server for authentication.

    .OUTPUTS
        [hashtable] Keys indexed by decrypted API key value.

    .EXAMPLE
        $authKeys = Get-SecureApiKeysForAuth
    #>
    [CmdletBinding()]
    [OutputType([hashtable])]
    param()

    $authKeys = @{}

    try {
        $keys = Get-SecureApiKeys -IncludeDecrypted

        foreach ($id in $keys.Keys) {
            $keyEntry = $keys[$id]
            if ($keyEntry.Enabled -and $keyEntry.DecryptedKey) {
                $authKeys[$keyEntry.DecryptedKey] = @{
                    Id = $keyEntry.Id
                    Description = $keyEntry.Description
                    Permissions = $keyEntry.Permissions
                    CreatedAt = $keyEntry.CreatedAt
                    ExpiresAt = $keyEntry.ExpiresAt
                }
            }
        }
    } catch {
        Write-Verbose (Get-LogString 'security.storage.api_keys_auth_failed' @{ Error = $_.Exception.Message })
    }

    return $authKeys
}

# === GENERIC SECURE STORAGE ===

function Save-SecureData {
    <#
    .SYNOPSIS
        Saves arbitrary data securely using DPAPI encryption.
    .DESCRIPTION
        Encrypts a string value with DPAPI and stores it in the generic secure storage file,
        indexed by a unique key name. Existing entries with the same key are overwritten.

    .PARAMETER Key
        Unique key name for the data.

    .PARAMETER Value
        The value to encrypt and store.

    .EXAMPLE
        Save-SecureData -Key 'database-password' -Value 'secret123'
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Key,

        [Parameter(Mandatory)]
        [string]$Value
    )

    try {
        # Load existing storage
        $storage = @{}
        if (Test-Path $script:SecureStoragePath) {
            try {
                $encryptedStorage = Get-Content -Path $script:SecureStoragePath -Raw
                $json = Unprotect-Data -EncryptedData $encryptedStorage
                $storage = $json | ConvertFrom-Json -AsHashtable
            } catch {
                Write-Verbose (Get-LogString 'security.storage.read_failed' @{ Error = $_.Exception.Message })
            }
        }

        # Encrypt and store value
        $storage[$Key] = Protect-Data -PlainText $Value

        # Save storage
        $json = $storage | ConvertTo-Json -Depth 5
        $encryptedStorage = Protect-Data -PlainText $json
        Set-Content -Path $script:SecureStoragePath -Value $encryptedStorage -Force

        Write-Verbose (Get-LogString 'security.storage.data_saved' @{ Key = $Key })
        return $true
    } catch {
        Write-Error (Get-LogString 'security.storage.data_save_failed' @{ Error = $_.Exception.Message })
        return $false
    }
}

function Get-SecureData {
    <#
    .SYNOPSIS
        Retrieves securely stored data.
    .DESCRIPTION
        Decrypts and returns a value from the generic secure storage file by its key name.
        Returns null if the key does not exist or the storage file is not found.

    .PARAMETER Key
        The key name of the data to retrieve.

    .OUTPUTS
        [string] The decrypted value, or $null if not found.

    .EXAMPLE
        $password = Get-SecureData -Key 'database-password'
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)]
        [string]$Key
    )

    try {
        if (-not (Test-Path $script:SecureStoragePath)) {
            return $null
        }

        $encryptedStorage = Get-Content -Path $script:SecureStoragePath -Raw
        $json = Unprotect-Data -EncryptedData $encryptedStorage
        $storage = $json | ConvertFrom-Json -AsHashtable

        if ($storage.ContainsKey($Key)) {
            return Unprotect-Data -EncryptedData $storage[$Key]
        }

        return $null
    } catch {
        Write-Verbose (Get-LogString 'security.storage.data_retrieve_failed' @{ Error = $_.Exception.Message })
        return $null
    }
}

function Remove-SecureData {
    <#
    .SYNOPSIS
        Removes data from secure storage.
    .DESCRIPTION
        Deletes a value from the generic secure storage file by its key name. If no entries remain
        after removal, the entire secure storage file is deleted.

    .PARAMETER Key
        The key name of the data to remove.

    .EXAMPLE
        Remove-SecureData -Key 'database-password'
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Key
    )

    try {
        if (-not (Test-Path $script:SecureStoragePath)) {
            return $false
        }

        $encryptedStorage = Get-Content -Path $script:SecureStoragePath -Raw
        $json = Unprotect-Data -EncryptedData $encryptedStorage
        $storage = $json | ConvertFrom-Json -AsHashtable

        if ($storage.ContainsKey($Key)) {
            $storage.Remove($Key)

            if ($storage.Count -eq 0) {
                Remove-Item -Path $script:SecureStoragePath -Force -ErrorAction SilentlyContinue
            } else {
                $json = $storage | ConvertTo-Json -Depth 5
                $encryptedStorage = Protect-Data -PlainText $json
                Set-Content -Path $script:SecureStoragePath -Value $encryptedStorage -Force
            }

            Write-Verbose (Get-LogString 'security.storage.data_removed' @{ Key = $Key })
            return $true
        }

        return $false
    } catch {
        Write-Error (Get-LogString 'security.storage.data_remove_failed' @{ Error = $_.Exception.Message })
        return $false
    }
}

function Test-SecureStorageAvailable {
    <#
    .SYNOPSIS
        Tests if secure storage (DPAPI) is available on this system.
    .DESCRIPTION
        Performs a round-trip encrypt/decrypt test with a known value to verify that the Windows
        Data Protection API is functional on the current machine and user context.

    .OUTPUTS
        [bool] True if DPAPI is available and working.

    .EXAMPLE
        if (Test-SecureStorageAvailable) { Save-SecureApiKey ... }
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param()

    try {
        $testData = 'WinForge-DPAPI-Test'
        $encrypted = Protect-Data -PlainText $testData
        $decrypted = Unprotect-Data -EncryptedData $encrypted
        return $decrypted -eq $testData
    } catch {
        return $false
    }
}

# === MODULE EXPORTS ===
Export-ModuleMember -Function @(
    # Core DPAPI
    'Protect-Data',
    'Unprotect-Data',
    # API Key Storage
    'Save-SecureApiKey',
    'Get-SecureApiKeys',
    'Remove-SecureApiKey',
    'Get-SecureApiKeysForAuth',
    # Generic Secure Storage
    'Save-SecureData',
    'Get-SecureData',
    'Remove-SecureData',
    # Utility
    'Test-SecureStorageAvailable'
)


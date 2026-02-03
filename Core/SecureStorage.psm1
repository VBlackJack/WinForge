<#
.SYNOPSIS
    Win11Forge - Secure Storage Module v1.0.0

.DESCRIPTION
    Provides secure storage capabilities using Windows DPAPI:
    - Encryption/decryption of sensitive data
    - Secure API key storage
    - Protection scope: CurrentUser (machine-bound)

.NOTES
    Author: Julien Bombled
    Version: 1.0.0
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

# === MODULE INITIALIZATION ===
$script:ModuleRoot = Split-Path -Parent $PSCommandPath
$script:RepositoryRoot = Split-Path $script:ModuleRoot -Parent
$script:SecureStoragePath = Join-Path $env:LOCALAPPDATA 'Win11Forge\secure-storage.dat'
$script:SecureApiKeysPath = Join-Path $env:LOCALAPPDATA 'Win11Forge\api-keys.secure'

# Ensure directory exists
$secureDir = Split-Path $script:SecureStoragePath -Parent
if (-not (Test-Path $secureDir)) {
    New-Item -Path $secureDir -ItemType Directory -Force | Out-Null
}

# Security: Per-user entropy file path
$script:EntropyPath = Join-Path $env:LOCALAPPDATA 'Win11Forge\entropy.dat'
# Mutex name for cross-process synchronization (per-user to avoid privilege issues)
$script:EntropyMutexName = "Local\Win11Forge_Entropy_$($env:USERNAME)"

# === ENTROPY MANAGEMENT ===

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

    $mutex = $null
    $mutexAcquired = $false

    try {
        # Create or open the named mutex for cross-process synchronization
        $mutex = [System.Threading.Mutex]::new($false, $script:EntropyMutexName)

        # Wait for exclusive access (2 second timeout to mitigate attack surface)
        $mutexAcquired = $mutex.WaitOne(2000)
        if (-not $mutexAcquired) {
            Write-Warning "Could not acquire entropy mutex within timeout, proceeding without lock"
        }

        # Try to read existing entropy file atomically (no separate Test-Path check)
        try {
            $entropyBytes = [System.IO.File]::ReadAllBytes($script:EntropyPath)
            if ($entropyBytes.Length -eq 32) {
                return $entropyBytes
            }
            # Invalid entropy file, will regenerate below
            Write-Verbose "Regenerating entropy file (invalid length: $($entropyBytes.Length))"
        } catch [System.IO.FileNotFoundException] {
            # File doesn't exist, will create below
            Write-Verbose "Entropy file not found, creating new one"
        } catch [System.IO.DirectoryNotFoundException] {
            # Directory doesn't exist, will create below
            Write-Verbose "Entropy directory not found, creating new one"
        } catch {
            Write-Verbose "Failed to read entropy file, regenerating: $($_.Exception.Message)"
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
                # Write to temp file first
                [System.IO.File]::WriteAllBytes($tempPath, $entropyBytes)

                # Set restrictive permissions on temp file before renaming
                $acl = Get-Acl -Path $tempPath
                $acl.SetAccessRuleProtection($true, $false)
                $currentUser = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name
                $rule = New-Object System.Security.AccessControl.FileSystemAccessRule(
                    $currentUser,
                    [System.Security.AccessControl.FileSystemRights]::FullControl,
                    [System.Security.AccessControl.AccessControlType]::Allow
                )
                $acl.AddAccessRule($rule)
                Set-Acl -Path $tempPath -AclObject $acl -ErrorAction SilentlyContinue

                # Atomic move (rename) to final path
                [System.IO.File]::Move($tempPath, $script:EntropyPath, $true)
            } finally {
                # Clean up temp file if it still exists (move failed)
                if (Test-Path $tempPath) {
                    Remove-Item -Path $tempPath -Force -ErrorAction SilentlyContinue
                }
            }
        } catch {
            Write-Verbose "Could not persist entropy file: $($_.Exception.Message)"
        }

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
            throw "Failed to encrypt data: $($_.Exception.Message)"
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
            throw "Failed to decrypt data: $($_.Exception.Message)"
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
        Keys are stored in %LOCALAPPDATA%\Win11Forge\api-keys.secure

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

        Write-Verbose "API key '$KeyId' saved securely"
        return $true
    } catch {
        Write-Error "Failed to save API key: $($_.Exception.Message)"
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
        Write-Verbose "Could not load secure API keys: $($_.Exception.Message)"
        return @{}
    }
}

function Remove-SecureApiKey {
    <#
    .SYNOPSIS
        Removes an API key from secure storage.

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

            Write-Verbose "API key '$KeyId' removed"
            return $true
        }

        return $false
    } catch {
        Write-Error "Failed to remove API key: $($_.Exception.Message)"
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
        Write-Verbose "Could not load API keys for auth: $($_.Exception.Message)"
    }

    return $authKeys
}

# === GENERIC SECURE STORAGE ===

function Save-SecureData {
    <#
    .SYNOPSIS
        Saves arbitrary data securely using DPAPI encryption.

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
                Write-Verbose "Failed to read secure storage (will create new): $($_.Exception.Message)"
            }
        }

        # Encrypt and store value
        $storage[$Key] = Protect-Data -PlainText $Value

        # Save storage
        $json = $storage | ConvertTo-Json -Depth 5
        $encryptedStorage = Protect-Data -PlainText $json
        Set-Content -Path $script:SecureStoragePath -Value $encryptedStorage -Force

        Write-Verbose "Secure data '$Key' saved"
        return $true
    } catch {
        Write-Error "Failed to save secure data: $($_.Exception.Message)"
        return $false
    }
}

function Get-SecureData {
    <#
    .SYNOPSIS
        Retrieves securely stored data.

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
        Write-Verbose "Could not retrieve secure data: $($_.Exception.Message)"
        return $null
    }
}

function Remove-SecureData {
    <#
    .SYNOPSIS
        Removes data from secure storage.

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

            Write-Verbose "Secure data '$Key' removed"
            return $true
        }

        return $false
    } catch {
        Write-Error "Failed to remove secure data: $($_.Exception.Message)"
        return $false
    }
}

function Test-SecureStorageAvailable {
    <#
    .SYNOPSIS
        Tests if secure storage (DPAPI) is available on this system.

    .OUTPUTS
        [bool] True if DPAPI is available and working.

    .EXAMPLE
        if (Test-SecureStorageAvailable) { Save-SecureApiKey ... }
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param()

    try {
        $testData = 'Win11Forge-DPAPI-Test'
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

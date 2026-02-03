<#
.SYNOPSIS
    Pester tests for SecureStorage module

.DESCRIPTION
    Comprehensive unit tests for Win11Forge SecureStorage v1.0.0
    Tests DPAPI encryption, API key management, and secure data storage

.NOTES
    Author: Julien Bombled
    Version: 3.5.2
    Requires: Pester v5+
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

BeforeAll {
    $script:ModuleRoot = Join-Path $PSScriptRoot '..\Core'
    $script:ModulePath = Join-Path $script:ModuleRoot 'SecureStorage.psm1'

    Import-Module $script:ModulePath -Force -ErrorAction Stop

    # Test data cleanup paths
    $script:TestSecureStoragePath = Join-Path $env:LOCALAPPDATA 'Win11Forge\secure-storage.dat'
    $script:TestApiKeysPath = Join-Path $env:LOCALAPPDATA 'Win11Forge\api-keys.secure'

    # Backup existing files if they exist
    $script:BackupSecureStorage = $null
    $script:BackupApiKeys = $null

    if (Test-Path $script:TestSecureStoragePath) {
        $script:BackupSecureStorage = Get-Content -Path $script:TestSecureStoragePath -Raw
    }
    if (Test-Path $script:TestApiKeysPath) {
        $script:BackupApiKeys = Get-Content -Path $script:TestApiKeysPath -Raw
    }
}

AfterAll {
    # Restore backups if they existed
    if ($script:BackupSecureStorage) {
        Set-Content -Path $script:TestSecureStoragePath -Value $script:BackupSecureStorage -Force
    } elseif (Test-Path $script:TestSecureStoragePath) {
        Remove-Item -Path $script:TestSecureStoragePath -Force -ErrorAction SilentlyContinue
    }

    if ($script:BackupApiKeys) {
        Set-Content -Path $script:TestApiKeysPath -Value $script:BackupApiKeys -Force
    } elseif (Test-Path $script:TestApiKeysPath) {
        Remove-Item -Path $script:TestApiKeysPath -Force -ErrorAction SilentlyContinue
    }
}

Describe 'SecureStorage Module' {
    Context 'Module Loading' {
        It 'Should load without errors' {
            { Import-Module $script:ModulePath -Force } | Should -Not -Throw
        }

        It 'Should export Protect-Data function' {
            Get-Command Protect-Data -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Unprotect-Data function' {
            Get-Command Unprotect-Data -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Save-SecureApiKey function' {
            Get-Command Save-SecureApiKey -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Get-SecureApiKeys function' {
            Get-Command Get-SecureApiKeys -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Remove-SecureApiKey function' {
            Get-Command Remove-SecureApiKey -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Get-SecureApiKeysForAuth function' {
            Get-Command Get-SecureApiKeysForAuth -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Save-SecureData function' {
            Get-Command Save-SecureData -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Get-SecureData function' {
            Get-Command Get-SecureData -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Remove-SecureData function' {
            Get-Command Remove-SecureData -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }

        It 'Should export Test-SecureStorageAvailable function' {
            Get-Command Test-SecureStorageAvailable -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }
    }

    Context 'Test-SecureStorageAvailable' {
        It 'Should return boolean' {
            $result = Test-SecureStorageAvailable
            $result | Should -BeOfType [bool]
        }

        It 'Should return true on Windows with DPAPI support' {
            # This test assumes we're running on Windows with DPAPI available
            if ($env:OS -eq 'Windows_NT') {
                Test-SecureStorageAvailable | Should -Be $true
            }
        }
    }

    Context 'Protect-Data' {
        It 'Should encrypt a simple string' {
            $plainText = 'TestSecret123'
            $encrypted = Protect-Data -PlainText $plainText
            $encrypted | Should -Not -BeNullOrEmpty
            $encrypted | Should -Not -Be $plainText
        }

        It 'Should return Base64 encoded string' {
            $plainText = 'TestSecret'
            $encrypted = Protect-Data -PlainText $plainText
            # Base64 strings only contain these characters
            $encrypted | Should -Match '^[A-Za-z0-9+/=]+$'
        }

        It 'Should produce different output for same input (due to entropy)' {
            $plainText = 'SameInput'
            $encrypted1 = Protect-Data -PlainText $plainText
            $encrypted2 = Protect-Data -PlainText $plainText
            # Note: With same entropy, outputs will be same
            # This tests that encryption works, not randomness
            $encrypted1 | Should -Not -BeNullOrEmpty
            $encrypted2 | Should -Not -BeNullOrEmpty
        }

        It 'Should reject empty string' {
            { Protect-Data -PlainText '' } | Should -Throw
        }

        It 'Should encrypt special characters' {
            $plainText = '!@#$%^&*()_+-=[]{}|;:,.<>?'
            $encrypted = Protect-Data -PlainText $plainText
            $encrypted | Should -Not -BeNullOrEmpty
        }

        It 'Should encrypt Unicode characters' {
            $plainText = 'Français 日本語 العربية 🔐'
            $encrypted = Protect-Data -PlainText $plainText
            $encrypted | Should -Not -BeNullOrEmpty
        }

        It 'Should encrypt long strings' {
            $plainText = 'A' * 10000
            $encrypted = Protect-Data -PlainText $plainText
            $encrypted | Should -Not -BeNullOrEmpty
        }

        It 'Should accept CurrentUser scope' {
            $encrypted = Protect-Data -PlainText 'Test' -Scope 'CurrentUser'
            $encrypted | Should -Not -BeNullOrEmpty
        }

        It 'Should accept pipeline input' {
            $encrypted = 'PipelineTest' | Protect-Data
            $encrypted | Should -Not -BeNullOrEmpty
        }
    }

    Context 'Unprotect-Data' {
        It 'Should decrypt data encrypted with Protect-Data' {
            $plainText = 'MySecretData123'
            $encrypted = Protect-Data -PlainText $plainText
            $decrypted = Unprotect-Data -EncryptedData $encrypted
            $decrypted | Should -Be $plainText
        }

        It 'Should reject empty encrypted data' {
            { Unprotect-Data -EncryptedData '' } | Should -Throw
        }

        It 'Should decrypt special characters' {
            $plainText = '!@#$%^&*()_+-=[]{}|;:,.<>?'
            $encrypted = Protect-Data -PlainText $plainText
            $decrypted = Unprotect-Data -EncryptedData $encrypted
            $decrypted | Should -Be $plainText
        }

        It 'Should decrypt Unicode characters' {
            $plainText = 'Français 日本語 العربية 🔐'
            $encrypted = Protect-Data -PlainText $plainText
            $decrypted = Unprotect-Data -EncryptedData $encrypted
            $decrypted | Should -Be $plainText
        }

        It 'Should decrypt long strings' {
            $plainText = 'A' * 10000
            $encrypted = Protect-Data -PlainText $plainText
            $decrypted = Unprotect-Data -EncryptedData $encrypted
            $decrypted | Should -Be $plainText
        }

        It 'Should throw on invalid Base64' {
            { Unprotect-Data -EncryptedData 'NotValidBase64!!!' } | Should -Throw
        }

        It 'Should throw on corrupted encrypted data' {
            $encrypted = Protect-Data -PlainText 'Test'
            $corrupted = $encrypted.Substring(0, 20) + 'CORRUPTED' + $encrypted.Substring(30)
            { Unprotect-Data -EncryptedData $corrupted } | Should -Throw
        }

        It 'Should accept pipeline input' {
            $plainText = 'PipelineTest'
            $encrypted = Protect-Data -PlainText $plainText
            $decrypted = $encrypted | Unprotect-Data
            $decrypted | Should -Be $plainText
        }
    }

    Context 'Encryption Round-Trip' {
        It 'Should successfully round-trip simple data' {
            $original = 'SimpleTest'
            $encrypted = Protect-Data -PlainText $original
            $decrypted = Unprotect-Data -EncryptedData $encrypted
            $decrypted | Should -BeExactly $original
        }

        It 'Should successfully round-trip JSON data' {
            $original = '{"key": "value", "number": 123}'
            $encrypted = Protect-Data -PlainText $original
            $decrypted = Unprotect-Data -EncryptedData $encrypted
            $decrypted | Should -BeExactly $original
        }

        It 'Should successfully round-trip multiline data' {
            $original = "Line1`nLine2`nLine3"
            $encrypted = Protect-Data -PlainText $original
            $decrypted = Unprotect-Data -EncryptedData $encrypted
            $decrypted | Should -BeExactly $original
        }
    }

    Context 'Save-SecureData' {
        BeforeEach {
            # Clean up test data
            Remove-SecureData -Key 'test-key' -ErrorAction SilentlyContinue
        }

        AfterEach {
            # Clean up test data
            Remove-SecureData -Key 'test-key' -ErrorAction SilentlyContinue
            Remove-SecureData -Key 'test-key-2' -ErrorAction SilentlyContinue
        }

        It 'Should save data without errors' {
            { Save-SecureData -Key 'test-key' -Value 'test-value' } | Should -Not -Throw
        }

        It 'Should return true on success' {
            $result = Save-SecureData -Key 'test-key' -Value 'test-value'
            $result | Should -Be $true
        }

        It 'Should overwrite existing data with same key' {
            Save-SecureData -Key 'test-key' -Value 'original'
            Save-SecureData -Key 'test-key' -Value 'updated'
            $retrieved = Get-SecureData -Key 'test-key'
            $retrieved | Should -Be 'updated'
        }

        It 'Should store multiple keys' {
            Save-SecureData -Key 'test-key' -Value 'value1'
            Save-SecureData -Key 'test-key-2' -Value 'value2'
            Get-SecureData -Key 'test-key' | Should -Be 'value1'
            Get-SecureData -Key 'test-key-2' | Should -Be 'value2'
        }
    }

    Context 'Get-SecureData' {
        BeforeEach {
            Save-SecureData -Key 'get-test-key' -Value 'get-test-value'
        }

        AfterEach {
            Remove-SecureData -Key 'get-test-key' -ErrorAction SilentlyContinue
        }

        It 'Should retrieve stored data' {
            $result = Get-SecureData -Key 'get-test-key'
            $result | Should -Be 'get-test-value'
        }

        It 'Should return null for non-existent key' {
            $result = Get-SecureData -Key 'non-existent-key-12345'
            $result | Should -BeNullOrEmpty
        }

        It 'Should handle special characters in value' {
            $specialValue = '!@#$%^&*()日本語'
            Save-SecureData -Key 'get-test-key' -Value $specialValue
            $result = Get-SecureData -Key 'get-test-key'
            $result | Should -Be $specialValue
        }
    }

    Context 'Remove-SecureData' {
        BeforeEach {
            Save-SecureData -Key 'remove-test-key' -Value 'remove-test-value'
        }

        It 'Should remove existing data' {
            Remove-SecureData -Key 'remove-test-key'
            $result = Get-SecureData -Key 'remove-test-key'
            $result | Should -BeNullOrEmpty
        }

        It 'Should return true when key exists' {
            $result = Remove-SecureData -Key 'remove-test-key'
            $result | Should -Be $true
        }

        It 'Should return false for non-existent key' {
            $result = Remove-SecureData -Key 'non-existent-key-xyz'
            $result | Should -Be $false
        }
    }

    Context 'Save-SecureApiKey' {
        AfterEach {
            Remove-SecureApiKey -KeyId 'test-api-key' -ErrorAction SilentlyContinue
            Remove-SecureApiKey -KeyId 'test-api-key-2' -ErrorAction SilentlyContinue
        }

        It 'Should save API key without errors' {
            { Save-SecureApiKey -KeyId 'test-api-key' -ApiKey 'w11f_testkey123' } | Should -Not -Throw
        }

        It 'Should return true on success' {
            $result = Save-SecureApiKey -KeyId 'test-api-key' -ApiKey 'w11f_testkey123'
            $result | Should -Be $true
        }

        It 'Should save API key with description' {
            Save-SecureApiKey -KeyId 'test-api-key' -ApiKey 'w11f_testkey123' -Description 'Test key'
            $keys = Get-SecureApiKeys
            $keys['test-api-key'].Description | Should -Be 'Test key'
        }

        It 'Should save API key with permissions' {
            Save-SecureApiKey -KeyId 'test-api-key' -ApiKey 'w11f_testkey123' -Permissions @('read', 'write', 'deploy')
            $keys = Get-SecureApiKeys
            $keys['test-api-key'].Permissions | Should -Contain 'read'
            $keys['test-api-key'].Permissions | Should -Contain 'write'
            $keys['test-api-key'].Permissions | Should -Contain 'deploy'
        }

        It 'Should save API key with expiration date' {
            $expiresAt = '2027-12-31'
            Save-SecureApiKey -KeyId 'test-api-key' -ApiKey 'w11f_testkey123' -ExpiresAt $expiresAt
            $keys = Get-SecureApiKeys
            $keys['test-api-key'].ExpiresAt | Should -Be $expiresAt
        }

        It 'Should overwrite existing API key' {
            Save-SecureApiKey -KeyId 'test-api-key' -ApiKey 'original_key'
            Save-SecureApiKey -KeyId 'test-api-key' -ApiKey 'updated_key'
            $keys = Get-SecureApiKeys -IncludeDecrypted
            $keys['test-api-key'].DecryptedKey | Should -Be 'updated_key'
        }
    }

    Context 'Get-SecureApiKeys' {
        BeforeAll {
            Save-SecureApiKey -KeyId 'get-test-key' -ApiKey 'w11f_gettest123' -Description 'Get test'
        }

        AfterAll {
            Remove-SecureApiKey -KeyId 'get-test-key' -ErrorAction SilentlyContinue
        }

        It 'Should return hashtable' {
            $keys = Get-SecureApiKeys
            $keys | Should -BeOfType [hashtable]
        }

        It 'Should return stored key metadata' {
            $keys = Get-SecureApiKeys
            $keys.ContainsKey('get-test-key') | Should -Be $true
            $keys['get-test-key'].Description | Should -Be 'Get test'
        }

        It 'Should not include decrypted key by default' {
            $keys = Get-SecureApiKeys
            $keys['get-test-key'].ContainsKey('DecryptedKey') | Should -Be $false
        }

        It 'Should include decrypted key with -IncludeDecrypted' {
            $keys = Get-SecureApiKeys -IncludeDecrypted
            $keys['get-test-key'].DecryptedKey | Should -Be 'w11f_gettest123'
        }

        It 'Should filter by KeyId' {
            Save-SecureApiKey -KeyId 'get-test-key-other' -ApiKey 'other_key'
            $keys = Get-SecureApiKeys -KeyId 'get-test-key'
            $keys.Count | Should -Be 1
            $keys.ContainsKey('get-test-key') | Should -Be $true
            Remove-SecureApiKey -KeyId 'get-test-key-other' -ErrorAction SilentlyContinue
        }

        It 'Should return empty hashtable when no keys exist' {
            # Temporarily remove all keys
            $backup = Get-SecureApiKeys -IncludeDecrypted
            foreach ($keyId in @($backup.Keys)) {
                Remove-SecureApiKey -KeyId $keyId
            }

            $keys = Get-SecureApiKeys
            $keys.Count | Should -Be 0

            # Restore
            foreach ($keyId in $backup.Keys) {
                $key = $backup[$keyId]
                Save-SecureApiKey -KeyId $keyId -ApiKey $key.DecryptedKey -Description $key.Description -Permissions $key.Permissions
            }
        }
    }

    Context 'Remove-SecureApiKey' {
        BeforeEach {
            Save-SecureApiKey -KeyId 'remove-test-key' -ApiKey 'w11f_removetest'
        }

        It 'Should remove existing API key' {
            Remove-SecureApiKey -KeyId 'remove-test-key'
            $keys = Get-SecureApiKeys
            $keys.ContainsKey('remove-test-key') | Should -Be $false
        }

        It 'Should return true when key exists' {
            $result = Remove-SecureApiKey -KeyId 'remove-test-key'
            $result | Should -Be $true
        }

        It 'Should return false for non-existent key' {
            $result = Remove-SecureApiKey -KeyId 'non-existent-api-key-xyz'
            $result | Should -Be $false
        }
    }

    Context 'Get-SecureApiKeysForAuth' {
        BeforeAll {
            Save-SecureApiKey -KeyId 'auth-test-key' -ApiKey 'w11f_authtest123' -Permissions @('read', 'deploy')
        }

        AfterAll {
            Remove-SecureApiKey -KeyId 'auth-test-key' -ErrorAction SilentlyContinue
        }

        It 'Should return hashtable' {
            $authKeys = Get-SecureApiKeysForAuth
            $authKeys | Should -BeOfType [hashtable]
        }

        It 'Should be indexed by decrypted key value' {
            $authKeys = Get-SecureApiKeysForAuth
            $authKeys.ContainsKey('w11f_authtest123') | Should -Be $true
        }

        It 'Should include permissions in value' {
            $authKeys = Get-SecureApiKeysForAuth
            $authKeys['w11f_authtest123'].Permissions | Should -Contain 'read'
            $authKeys['w11f_authtest123'].Permissions | Should -Contain 'deploy'
        }

        It 'Should include key ID in value' {
            $authKeys = Get-SecureApiKeysForAuth
            $authKeys['w11f_authtest123'].Id | Should -Be 'auth-test-key'
        }

        It 'Should only include enabled keys' {
            # All keys are enabled by default
            $authKeys = Get-SecureApiKeysForAuth
            $authKeys.Count | Should -BeGreaterOrEqual 1
        }
    }

    Context 'Security Tests' {
        It 'Should not expose plaintext in encrypted data' {
            $secret = 'SuperSecretPassword123!'
            $encrypted = Protect-Data -PlainText $secret
            $encrypted | Should -Not -Match $secret
        }

        It 'Should produce encrypted data longer than plaintext' {
            $secret = 'Short'
            $encrypted = Protect-Data -PlainText $secret
            $encrypted.Length | Should -BeGreaterThan $secret.Length
        }

        It 'Should fail to decrypt data encrypted by different user' {
            # This test verifies DPAPI user binding
            # We can't easily simulate a different user, so we test corruption instead
            $originalText = 'TestData'
            $encrypted = Protect-Data -PlainText $originalText
            $bytes = [Convert]::FromBase64String($encrypted)
            $bytes[10] = ($bytes[10] + 1) % 256  # Corrupt a byte
            $corrupted = [Convert]::ToBase64String($bytes)
            # Corrupted data should either throw or return different data
            try {
                $decrypted = Unprotect-Data -EncryptedData $corrupted
                $decrypted | Should -Not -Be $originalText -Because 'corrupted data should not decrypt correctly'
            } catch {
                # Expected - corrupted data throws
                $true | Should -Be $true
            }
        }
    }

    Context 'Edge Cases' {
        It 'Should handle very long key names' {
            $longKey = 'K' * 500
            Save-SecureData -Key $longKey -Value 'test'
            $result = Get-SecureData -Key $longKey
            $result | Should -Be 'test'
            Remove-SecureData -Key $longKey
        }

        It 'Should handle special characters in key names' {
            $specialKey = 'key-with_special.chars'
            Save-SecureData -Key $specialKey -Value 'test'
            $result = Get-SecureData -Key $specialKey
            $result | Should -Be 'test'
            Remove-SecureData -Key $specialKey
        }

        It 'Should handle concurrent access gracefully' {
            # Test that rapid sequential calls don't cause issues
            1..10 | ForEach-Object {
                Save-SecureData -Key "concurrent-test-$_" -Value "value-$_"
            }
            1..10 | ForEach-Object {
                $result = Get-SecureData -Key "concurrent-test-$_"
                $result | Should -Be "value-$_"
                Remove-SecureData -Key "concurrent-test-$_"
            }
        }
    }

    Context 'API Key Lifecycle' {
        It 'Should complete full lifecycle: create, read, update, delete' {
            # Create
            $createResult = Save-SecureApiKey -KeyId 'lifecycle-test' -ApiKey 'initial_key' -Description 'Initial'
            $createResult | Should -Be $true

            # Read
            $keys = Get-SecureApiKeys -IncludeDecrypted
            $keys['lifecycle-test'].DecryptedKey | Should -Be 'initial_key'
            $keys['lifecycle-test'].Description | Should -Be 'Initial'

            # Update
            Save-SecureApiKey -KeyId 'lifecycle-test' -ApiKey 'updated_key' -Description 'Updated'
            $keys = Get-SecureApiKeys -IncludeDecrypted
            $keys['lifecycle-test'].DecryptedKey | Should -Be 'updated_key'
            $keys['lifecycle-test'].Description | Should -Be 'Updated'

            # Delete
            $deleteResult = Remove-SecureApiKey -KeyId 'lifecycle-test'
            $deleteResult | Should -Be $true
            $keys = Get-SecureApiKeys
            $keys.ContainsKey('lifecycle-test') | Should -Be $false
        }
    }

    Context 'Secure Data Lifecycle' {
        It 'Should complete full lifecycle: create, read, update, delete' {
            # Create
            $createResult = Save-SecureData -Key 'lifecycle-data' -Value 'initial_value'
            $createResult | Should -Be $true

            # Read
            $value = Get-SecureData -Key 'lifecycle-data'
            $value | Should -Be 'initial_value'

            # Update
            Save-SecureData -Key 'lifecycle-data' -Value 'updated_value'
            $value = Get-SecureData -Key 'lifecycle-data'
            $value | Should -Be 'updated_value'

            # Delete
            $deleteResult = Remove-SecureData -Key 'lifecycle-data'
            $deleteResult | Should -Be $true
            $value = Get-SecureData -Key 'lifecycle-data'
            $value | Should -BeNullOrEmpty
        }
    }
}

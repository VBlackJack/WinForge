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
    $script:DownloadValidationPath = Join-Path $PSScriptRoot '..\Modules\DownloadValidation.psm1'
    Import-Module $script:DownloadValidationPath -Force -ErrorAction Stop
}

Describe 'DownloadValidation - Resolve-DirectDownloadValidationMode' {
    Context 'Module loading' {
        It 'Should export Resolve-DirectDownloadValidationMode' {
            Get-Command Resolve-DirectDownloadValidationMode -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }
    }

    Context 'Validation policy matrix' {
        It 'Returns None when no control and no opt-out is configured' {
            Resolve-DirectDownloadValidationMode | Should -Be 'None'
        }

        It 'Returns SkipValidation on an explicit opt-out token' {
            Resolve-DirectDownloadValidationMode -ExpectedSHA256 'SKIP_VALIDATION' | Should -Be 'SkipValidation'
        }

        It 'Returns Validated with a publisher only' {
            Resolve-DirectDownloadValidationMode -ExpectedPublisher 'CN=Test Publisher' | Should -Be 'Validated'
        }

        It 'Returns Validated with a real checksum only' {
            Resolve-DirectDownloadValidationMode -ExpectedSHA256 ('a' * 64) | Should -Be 'Validated'
        }

        It 'Returns Validated with both a publisher and a checksum' {
            Resolve-DirectDownloadValidationMode -ExpectedSHA256 ('a' * 64) -ExpectedPublisher 'CN=Test Publisher' | Should -Be 'Validated'
        }

        It 'Returns Validated (never SkipValidation) when a publisher is set with a SKIP_VALIDATION checksum' {
            Resolve-DirectDownloadValidationMode -ExpectedSHA256 'SKIP_VALIDATION' -ExpectedPublisher 'CN=Test Publisher' | Should -Be 'Validated'
        }
    }
}

Describe 'DownloadValidation - Get-ExpectedChecksum' {
    It 'Returns the canonical ExpectedSHA256 value' {
        $sources = [PSCustomObject]@{ ExpectedSHA256 = ('a' * 64) }
        Get-ExpectedChecksum -Sources $sources | Should -Be ('a' * 64)
    }

    It 'Returns the SKIP_VALIDATION token when that is the configured value' {
        $sources = [PSCustomObject]@{ ExpectedSHA256 = 'SKIP_VALIDATION' }
        Get-ExpectedChecksum -Sources $sources | Should -Be 'SKIP_VALIDATION'
    }

    It 'Ignores a legacy bare SHA256 field (no fallback)' {
        $sources = [PSCustomObject]@{ SHA256 = ('b' * 64) }
        Get-ExpectedChecksum -Sources $sources | Should -BeNullOrEmpty
    }

    It 'Returns null when no checksum field is present' {
        $sources = [PSCustomObject]@{ Winget = 'Some.App' }
        Get-ExpectedChecksum -Sources $sources | Should -BeNullOrEmpty
    }

    It 'Returns null for a null Sources object' {
        Get-ExpectedChecksum -Sources $null | Should -BeNullOrEmpty
    }
}

Describe 'DownloadValidation - Assert-FileChecksum' {
    BeforeAll {
        $script:ChecksumFile = Join-Path $TestDrive 'payload.bin'
        Set-Content -Path $script:ChecksumFile -Value 'Win11Forge checksum fixture' -NoNewline -Encoding UTF8
        $script:RealHash = (Get-FileHash -Path $script:ChecksumFile -Algorithm SHA256).Hash
    }

    It 'Returns true when the file hash matches the expected checksum' {
        Assert-FileChecksum -Path $script:ChecksumFile -ExpectedSHA256 $script:RealHash | Should -BeTrue
    }

    It 'Returns true regardless of expected-hash casing (case-insensitive compare)' {
        Assert-FileChecksum -Path $script:ChecksumFile -ExpectedSHA256 $script:RealHash.ToLower() | Should -BeTrue
    }

    It 'Returns false on a checksum mismatch (fail-closed)' {
        Assert-FileChecksum -Path $script:ChecksumFile -ExpectedSHA256 ('f' * 64) | Should -BeFalse
    }

    It 'Returns true for the SKIP_VALIDATION opt-out token (nothing to enforce)' {
        Assert-FileChecksum -Path $script:ChecksumFile -ExpectedSHA256 'SKIP_VALIDATION' | Should -BeTrue
    }

    It 'Returns true for an empty expected checksum (nothing to enforce)' {
        Assert-FileChecksum -Path $script:ChecksumFile -ExpectedSHA256 '' | Should -BeTrue
    }
}

Describe 'DownloadValidation - Test-DirectDownloadChecksumGate' {
    BeforeAll {
        $script:GateChecksumFile = Join-Path $TestDrive 'gate-payload.bin'
        Set-Content -Path $script:GateChecksumFile -Value 'Win11Forge checksum gate fixture' -NoNewline -Encoding UTF8
        $script:GateRealHash = (Get-FileHash -Path $script:GateChecksumFile -Algorithm SHA256).Hash
    }

    Context 'Module loading' {
        It 'Should export Test-DirectDownloadChecksumGate' {
            Get-Command Test-DirectDownloadChecksumGate -ErrorAction SilentlyContinue | Should -Not -BeNullOrEmpty
        }
    }

    Context 'Checksum verdict matrix' {
        It 'Returns a stop verdict and actual hash on checksum mismatch' {
            $verdict = Test-DirectDownloadChecksumGate -Path $script:GateChecksumFile -ExpectedSHA256 ('f' * 64)

            $verdict.Enforced | Should -BeTrue
            $verdict.Proceed | Should -BeFalse
            $verdict.ActualHash | Should -Be $script:GateRealHash
        }

        It 'Returns a proceed verdict and actual hash on checksum match' {
            $verdict = Test-DirectDownloadChecksumGate -Path $script:GateChecksumFile -ExpectedSHA256 $script:GateRealHash

            $verdict.Enforced | Should -BeTrue
            $verdict.Proceed | Should -BeTrue
            $verdict.ActualHash | Should -Be $script:GateRealHash
        }

        It 'Returns a non-enforced proceed verdict for an empty checksum' {
            $verdict = Test-DirectDownloadChecksumGate -Path $script:GateChecksumFile -ExpectedSHA256 ''

            $verdict.Enforced | Should -BeFalse
            $verdict.Proceed | Should -BeTrue
            $verdict.ActualHash | Should -BeNullOrEmpty
        }

        It 'Returns a non-enforced proceed verdict for the SKIP_VALIDATION token' {
            $verdict = Test-DirectDownloadChecksumGate -Path $script:GateChecksumFile -ExpectedSHA256 'SKIP_VALIDATION'

            $verdict.Enforced | Should -BeFalse
            $verdict.Proceed | Should -BeTrue
            $verdict.ActualHash | Should -BeNullOrEmpty
        }
    }
}

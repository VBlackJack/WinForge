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

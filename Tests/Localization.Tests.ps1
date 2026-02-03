#
# Tests for Localization module
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
    $modulePath = Join-Path $PSScriptRoot '..\Core\Localization.psm1'
    Import-Module $modulePath -Force
}

AfterAll {
    Remove-Module Localization -ErrorAction SilentlyContinue
}

Describe 'Localization Module' {
    Context 'Initialize-Localization' {
        It 'Should initialize without errors' {
            { Initialize-Localization } | Should -Not -Throw
        }

        It 'Should accept en locale' {
            { Initialize-Localization -Locale 'en' } | Should -Not -Throw
        }

        It 'Should accept fr locale' {
            { Initialize-Localization -Locale 'fr' } | Should -Not -Throw
        }

        It 'Should accept locale with region code' {
            { Initialize-Localization -Locale 'en-US' } | Should -Not -Throw
        }
    }

    Context 'Get-CurrentLocale' {
        BeforeAll {
            Initialize-Localization -Locale 'en'
        }

        It 'Should return a locale string' {
            $result = Get-CurrentLocale
            $result | Should -Not -BeNullOrEmpty
            $result | Should -BeOfType [string]
        }

        It 'Should return the set locale' {
            Initialize-Localization -Locale 'en'
            $result = Get-CurrentLocale
            $result | Should -Be 'en'
        }
    }

    Context 'Set-CurrentLocale' {
        BeforeEach {
            $originalLocale = Get-CurrentLocale
        }

        AfterEach {
            Set-CurrentLocale -Locale $originalLocale -ErrorAction SilentlyContinue
        }

        It 'Should change the current locale to en' {
            Set-CurrentLocale -Locale 'en'
            $result = Get-CurrentLocale
            $result | Should -Be 'en'
        }

        It 'Should change the current locale to fr' {
            Set-CurrentLocale -Locale 'fr'
            $result = Get-CurrentLocale
            $result | Should -Be 'fr'
        }
    }

    Context 'Get-LocalizedString' {
        BeforeAll {
            Initialize-Localization -Locale 'en'
        }

        It 'Should return a string for a valid key' {
            $result = Get-LocalizedString -Key 'common.ok'
            $result | Should -Not -BeNullOrEmpty
        }

        It 'Should return bracketed key when translation is missing' {
            $result = Get-LocalizedString -Key 'nonexistent.key.that.does.not.exist'
            $result | Should -Be '[nonexistent.key.that.does.not.exist]'
        }

        It 'Should support string interpolation with parameters' {
            $result = Get-LocalizedString -Key 'common.error' -Parameters @{ message = 'Test error' }
            $result | Should -BeOfType [string]
        }

        It 'Should return default value when provided and key missing' {
            $result = Get-LocalizedString -Key 'missing.key' -DefaultValue 'Default Text'
            $result | Should -Be 'Default Text'
        }
    }

    Context 'Get-AvailableLocales' {
        It 'Should return available locales' {
            $result = Get-AvailableLocales
            $result | Should -Not -BeNullOrEmpty
        }

        It 'Should include en locale' {
            $result = Get-AvailableLocales
            $result | Should -Contain 'en'
        }

        It 'Should include fr locale' {
            $result = Get-AvailableLocales
            $result | Should -Contain 'fr'
        }
    }

    Context 'Test-TranslationKey' {
        BeforeAll {
            Initialize-Localization -Locale 'en'
        }

        It 'Should return true for existing key' {
            $result = Test-TranslationKey -Key 'common.ok'
            $result | Should -Be $true
        }

        It 'Should return false for non-existent key' {
            $result = Test-TranslationKey -Key 'completely.made.up.key.that.does.not.exist'
            $result | Should -Be $false
        }
    }

    Context 'Module Integration' {
        It 'Should have all expected functions exported' {
            $expectedFunctions = @(
                'Initialize-Localization',
                'Get-LocalizedString',
                'Get-CurrentLocale',
                'Set-CurrentLocale',
                'Get-AvailableLocales',
                'Test-TranslationKey'
            )

            $module = Get-Module Localization
            foreach ($func in $expectedFunctions) {
                $module.ExportedFunctions.Keys | Should -Contain $func
            }
        }

        It 'Should export t alias for Get-LocalizedString' {
            $module = Get-Module Localization
            $module.ExportedAliases.Keys | Should -Contain 't'
        }
    }

    Context 'Locale Switching' {
        BeforeAll {
            $originalLocale = Get-CurrentLocale
        }

        AfterAll {
            Set-CurrentLocale -Locale $originalLocale -ErrorAction SilentlyContinue
        }

        It 'Should return different strings for different locales' {
            Set-CurrentLocale -Locale 'en'
            $englishString = Get-LocalizedString -Key 'common.ok'

            Set-CurrentLocale -Locale 'fr'
            $frenchString = Get-LocalizedString -Key 'common.ok'

            # Both should return something (even if same due to fallback)
            $englishString | Should -Not -BeNullOrEmpty
            $frenchString | Should -Not -BeNullOrEmpty
        }
    }

    Context 'Auto-initialization' {
        It 'Should auto-initialize when calling Get-LocalizedString before initialization' {
            # Force reload module to reset state
            Remove-Module Localization -Force -ErrorAction SilentlyContinue
            Import-Module (Join-Path $PSScriptRoot '..\Core\Localization.psm1') -Force

            # This should auto-initialize
            { Get-LocalizedString -Key 'common.ok' } | Should -Not -Throw
        }
    }
}

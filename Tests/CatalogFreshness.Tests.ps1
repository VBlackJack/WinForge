<#
.SYNOPSIS
    Pester tests for Tools/Test-CatalogFreshness.ps1.

.DESCRIPTION
    Unit tests for the catalog freshness checker. External CLI calls
    (winget/choco) and HTTP requests are mocked — these tests do not hit
    the network.

.NOTES
    Author: Julien Bombled
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
    $script:ScriptPath  = Join-Path $PSScriptRoot '..\Tools\Test-CatalogFreshness.ps1'
    $script:FixturePath = Join-Path $PSScriptRoot 'Fixtures\freshness-sample.json'

    # Dot-source the script — the entry-point guard skips the main run on dot-source.
    . $script:ScriptPath

    function script:Get-FixtureApplications {
        $json = Get-Content -LiteralPath $script:FixturePath -Raw -Encoding UTF8
        $db = $json | ConvertFrom-Json
        $apps = @()
        foreach ($prop in $db.Applications.PSObject.Properties) {
            $entry = $prop.Value
            $entry | Add-Member -NotePropertyName 'AppId' -NotePropertyValue $prop.Name -Force
            $apps += $entry
        }
        return ,$apps
    }
}

Describe 'Get-FreshnessCheckPlan' {
    BeforeAll {
        $script:Apps = script:Get-FixtureApplications
    }

    It 'plans Probe for Winget when -Checks Winget' {
        $plan = Get-FreshnessCheckPlan -Applications $script:Apps -Selected @('Winget')
        $wingetEntries = @($plan | Where-Object { $_.Source -eq 'Winget' })
        $wingetEntries | Should -Not -BeNullOrEmpty
        ($wingetEntries | Where-Object { $_.Action -ne 'Probe' }) | Should -BeNullOrEmpty
    }

    It 'plans Probe for Chocolatey only when -Checks Chocolatey' {
        $plan = Get-FreshnessCheckPlan -Applications $script:Apps -Selected @('Chocolatey')
        ($plan | Where-Object { $_.Source -eq 'Winget' -and $_.Action -eq 'Probe' }) | Should -BeNullOrEmpty
        ($plan | Where-Object { $_.Source -eq 'Chocolatey' -and $_.Action -eq 'Probe' }) | Should -Not -BeNullOrEmpty
    }

    It 'always marks Store as Skipped (v1)' {
        $plan = Get-FreshnessCheckPlan -Applications $script:Apps -Selected @('All')
        $storeEntries = @($plan | Where-Object { $_.Source -eq 'Store' })
        $storeEntries.Count | Should -BeGreaterThan 0
        foreach ($e in $storeEntries) {
            $e.Action | Should -Be 'Skip'
            $e.Reason | Should -Be 'store-not-validated-v1'
        }
    }

    It 'marks all-null Sources as Skipped with reason "windows-feature" when InstallMethod=WindowsFeature' {
        $plan = Get-FreshnessCheckPlan -Applications $script:Apps -Selected @('All')
        $wf = @($plan | Where-Object { $_.AppId -eq 'WindowsFeatureOnly' })
        $wf.Count | Should -Be 1
        $wf[0].Action | Should -Be 'Skip'
        $wf[0].Reason | Should -Be 'windows-feature'
    }

    It 'plans DirectUrl Probe for DirectUrl-only apps' {
        $plan = Get-FreshnessCheckPlan -Applications $script:Apps -Selected @('All')
        $du = @($plan | Where-Object { $_.AppId -eq 'DirectUrlOnly' -and $_.Source -eq 'DirectUrl' })
        $du.Count | Should -Be 1
        $du[0].Action | Should -Be 'Probe'
    }
}

Describe 'Get-DirectUrlStatusFromCode' {
    It 'maps 200 to Ok' {
        (Get-DirectUrlStatusFromCode -StatusCode 200).Status | Should -Be 'Ok'
    }
    It 'maps 301/302/307/308 to Ok' {
        (Get-DirectUrlStatusFromCode -StatusCode 301).Status | Should -Be 'Ok'
        (Get-DirectUrlStatusFromCode -StatusCode 302).Status | Should -Be 'Ok'
        (Get-DirectUrlStatusFromCode -StatusCode 307).Status | Should -Be 'Ok'
        (Get-DirectUrlStatusFromCode -StatusCode 308).Status | Should -Be 'Ok'
    }
    It 'maps 401/403 to Suspect (CDN protected)' {
        (Get-DirectUrlStatusFromCode -StatusCode 401).Status | Should -Be 'Suspect'
        (Get-DirectUrlStatusFromCode -StatusCode 403).Status | Should -Be 'Suspect'
    }
    It 'maps 404/410 to Broken' {
        (Get-DirectUrlStatusFromCode -StatusCode 404).Status | Should -Be 'Broken'
        (Get-DirectUrlStatusFromCode -StatusCode 410).Status | Should -Be 'Broken'
    }
    It 'maps unknown codes to Suspect' {
        (Get-DirectUrlStatusFromCode -StatusCode 599).Status | Should -Be 'Suspect'
    }
}

Describe 'Invoke-SchemaLint' {
    BeforeAll {
        $script:Apps = script:Get-FixtureApplications
        $script:Findings = Invoke-SchemaLint -Applications $script:Apps
    }

    It 'flags StoreApp detection without Store source' {
        $hit = @($script:Findings | Where-Object { $_.AppId -eq 'StoreAppMissingStoreId' -and $_.Reason -eq 'storeapp-detection-without-store-source' })
        $hit.Count | Should -Be 1
        $hit[0].Status | Should -Be 'Suspect'
    }

    It 'flags stale LastVerified for Verified apps' {
        $hit = @($script:Findings | Where-Object { $_.AppId -eq 'StaleVerification' -and $_.Reason -eq 'lastverified-stale' })
        $hit.Count | Should -Be 1
    }

    It 'does not flag fresh, well-formed entries' {
        $clean = @($script:Findings | Where-Object { $_.AppId -eq 'WingetOnly' })
        $clean.Count | Should -Be 0
    }
}

Describe 'Cache TTL' {
    It 'considers a recent entry fresh' {
        $entry = [PSCustomObject]@{
            Status    = 'Ok'
            Reason    = 'found'
            CheckedAt = (Get-Date).ToUniversalTime().AddHours(-1).ToString('yyyy-MM-ddTHH:mm:ssZ')
        }
        Test-CacheEntryFresh -Entry $entry -TtlHours 24 | Should -BeTrue
    }

    It 'considers an old entry stale' {
        $entry = [PSCustomObject]@{
            Status    = 'Ok'
            Reason    = 'found'
            CheckedAt = (Get-Date).ToUniversalTime().AddHours(-48).ToString('yyyy-MM-ddTHH:mm:ssZ')
        }
        Test-CacheEntryFresh -Entry $entry -TtlHours 24 | Should -BeFalse
    }

    It 'considers entries with missing CheckedAt stale' {
        $entry = [PSCustomObject]@{ Status = 'Ok'; Reason = 'found' }
        Test-CacheEntryFresh -Entry $entry -TtlHours 24 | Should -BeFalse
    }
}

Describe 'Cache file round-trip' {
    BeforeAll {
        $script:TempCache = Join-Path $TestDrive 'freshness-cache.json'
    }

    It 'writes and reads back the cache contents' {
        $entries = @{
            'Winget|Foo.Bar' = [PSCustomObject]@{
                Status    = 'Ok'
                Reason    = 'found'
                Detail    = 'winget search ...'
                CheckedAt = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
            }
        }
        Save-FreshnessCache -Path $script:TempCache -Entries $entries

        $loaded = Read-FreshnessCache -Path $script:TempCache
        $loaded.Keys | Should -Contain 'Winget|Foo.Bar'
        $loaded['Winget|Foo.Bar'].Status | Should -Be 'Ok'
    }

    It 'returns empty hashtable when file is missing' {
        $missing = Join-Path $TestDrive 'nonexistent.json'
        $loaded = Read-FreshnessCache -Path $missing
        $loaded.Count | Should -Be 0
    }
}

Describe 'New-FreshnessReport' {
    It 'aggregates counts correctly' {
        $results = @(
            [PSCustomObject]@{ AppId='A'; Source='Winget'; Status='Ok';      Reason='found';        Identifier='A.Id'; FromCache=$false; CheckedAt='2026-05-10T00:00:00Z' }
            [PSCustomObject]@{ AppId='B'; Source='Winget'; Status='Broken';  Reason='not-found';    Identifier='B.Id'; FromCache=$false; CheckedAt='2026-05-10T00:00:00Z' }
            [PSCustomObject]@{ AppId='C'; Source='Winget'; Status='Suspect'; Reason='exit-non-zero';Identifier='C.Id'; FromCache=$false; CheckedAt='2026-05-10T00:00:00Z' }
            [PSCustomObject]@{ AppId='D'; Source='Store';  Status='Skipped'; Reason='store-not-validated-v1'; Identifier='9N0'; FromCache=$false; CheckedAt='2026-05-10T00:00:00Z' }
        )
        $report = New-FreshnessReport -DatabaseVersion '1.2.3' -AppCount 4 -Results $results

        $report.DatabaseVersion | Should -Be '1.2.3'
        $report.Summary.Apps    | Should -Be 4
        $report.Summary.Checks  | Should -Be 4
        $report.Summary.Ok      | Should -Be 1
        $report.Summary.Broken  | Should -Be 1
        $report.Summary.Suspect | Should -Be 1
        $report.Summary.Skipped | Should -Be 1
    }
}

Describe 'ConvertTo-AnnotationSafeText' {
    It 'escapes commas, colons, percents, and newlines' {
        $out = ConvertTo-AnnotationSafeText -Text "a,b:c%d`r`ne"
        $out | Should -Be 'a%2Cb%3Ac%25d e'
    }
}

Describe 'Test-DirectUrlReachable HEAD probe' {
    It 'returns Ok when HEAD returns 200' {
        Mock -CommandName Invoke-WebRequest -ParameterFilter { $Method -eq 'Head' } -MockWith {
            [PSCustomObject]@{ StatusCode = 200 }
        }
        $r = Test-DirectUrlReachable -Url 'https://example.invalid/installer.exe'
        $r.Status | Should -Be 'Ok'
    }

    It 'returns Broken when HEAD returns 404 via WebException' {
        Mock -CommandName Invoke-WebRequest -ParameterFilter { $Method -eq 'Head' } -MockWith {
            $resp = [PSCustomObject]@{ StatusCode = 404 }
            $exc = New-Object System.Net.WebException('Not Found')
            $exc | Add-Member -NotePropertyName Response -NotePropertyValue $resp -Force
            throw $exc
        }
        $r = Test-DirectUrlReachable -Url 'https://example.invalid/missing.exe'
        $r.Status | Should -Be 'Broken'
        $r.Reason | Should -Match '^http-404'
    }

    It 'returns Suspect with reason network-error when no status code is available' {
        Mock -CommandName Invoke-WebRequest -ParameterFilter { $Method -eq 'Head' } -MockWith {
            throw (New-Object System.Net.WebException('Connection refused'))
        }
        $r = Test-DirectUrlReachable -Url 'https://example.invalid/timeout.exe'
        $r.Status | Should -Be 'Suspect'
        $r.Reason | Should -Be 'network-error'
    }
}

Describe 'Test-WingetIdentifier exit-code mapping' {
    BeforeAll {
        # Stub winget so Get-Command sees it as available.
        function global:winget { }
    }
    AfterAll {
        Remove-Item -Path Function:\winget -ErrorAction SilentlyContinue
    }

    It 'maps APPINSTALLER_CLI_ERROR_NO_APPLICATIONS_FOUND to Broken/not-found' {
        Mock -CommandName winget -MockWith {
            $script:LASTEXITCODE = -1978335212
            'No package matches the criteria.'
        }
        $r = Test-WingetIdentifier -Identifier 'Contoso.NotReal'
        $r.Status | Should -Be 'Broken'
        $r.Reason | Should -Be 'not-found'
    }

    It 'maps other non-zero exit codes to Suspect/exit-non-zero' {
        Mock -CommandName winget -MockWith {
            $script:LASTEXITCODE = -1978335211   # arbitrary non-NoMatch error
            'Network failure or other transient issue.'
        }
        $r = Test-WingetIdentifier -Identifier 'Contoso.Other'
        $r.Status | Should -Be 'Suspect'
        $r.Reason | Should -Be 'exit-non-zero'
    }
}

Describe 'Get-CoverageGap' {
    It 'reports a gap when winget probes were skipped because of missing CLI' {
        $results = @(
            [PSCustomObject]@{ AppId='A'; Source='Winget'; Identifier='X.Y'; Status='Skipped'; Reason='winget-cli-missing'; Detail=''; FromCache=$false; CheckedAt='2026-05-10T00:00:00Z' }
            [PSCustomObject]@{ AppId='B'; Source='Winget'; Identifier='X.Z'; Status='Skipped'; Reason='winget-cli-missing'; Detail=''; FromCache=$false; CheckedAt='2026-05-10T00:00:00Z' }
            [PSCustomObject]@{ AppId='C'; Source='Chocolatey'; Identifier='c'; Status='Ok'; Reason='found'; Detail=''; FromCache=$false; CheckedAt='2026-05-10T00:00:00Z' }
        )
        $gaps = Get-CoverageGap -Results $results
        $gaps.Count | Should -Be 1
        $gaps[0].Source | Should -Be 'Winget'
        $gaps[0].SkippedCount | Should -Be 2
        $gaps[0].Reason | Should -Be 'winget-cli-missing'
    }

    It 'reports both winget and choco gaps when both CLIs missing' {
        $results = @(
            [PSCustomObject]@{ AppId='A'; Source='Winget';     Identifier='X.Y'; Status='Skipped'; Reason='winget-cli-missing'; Detail=''; FromCache=$false; CheckedAt='2026-05-10T00:00:00Z' }
            [PSCustomObject]@{ AppId='B'; Source='Chocolatey'; Identifier='c1';  Status='Skipped'; Reason='choco-cli-missing';  Detail=''; FromCache=$false; CheckedAt='2026-05-10T00:00:00Z' }
        )
        $gaps = Get-CoverageGap -Results $results
        $gaps.Count | Should -Be 2
        ($gaps | Where-Object { $_.Source -eq 'Winget' }).Count     | Should -Be 1
        ($gaps | Where-Object { $_.Source -eq 'Chocolatey' }).Count | Should -Be 1
    }

    It 'returns empty array when Skipped reasons are by-design (not CLI-missing)' {
        $results = @(
            [PSCustomObject]@{ AppId='A'; Source='Store';  Identifier='9N0'; Status='Skipped'; Reason='store-not-validated-v1'; Detail=''; FromCache=$false; CheckedAt='2026-05-10T00:00:00Z' }
            [PSCustomObject]@{ AppId='B'; Source='None';   Identifier=$null; Status='Skipped'; Reason='windows-feature';        Detail=''; FromCache=$false; CheckedAt='2026-05-10T00:00:00Z' }
        )
        $gaps = Get-CoverageGap -Results $results
        $gaps.Count | Should -Be 0
    }
}

Describe 'New-FreshnessReport with ProbeEnvironment and CoverageGap' {
    It 'embeds ProbeEnvironment, AppIdFilter, and CoverageGap in the report' {
        $results = @(
            [PSCustomObject]@{ AppId='A'; Source='Winget'; Identifier='X.Y'; Status='Skipped'; Reason='winget-cli-missing'; Detail=''; FromCache=$false; CheckedAt='2026-05-10T00:00:00Z' }
        )
        $env = [PSCustomObject]@{ WingetAvailable = $false; ChocolateyAvailable = $true }
        $report = New-FreshnessReport -DatabaseVersion '1.0.0' -AppCount 1 -Results $results -ProbeEnvironment $env -AppIdFilter @('A')

        $report.ProbeEnvironment.WingetAvailable     | Should -BeFalse
        $report.ProbeEnvironment.ChocolateyAvailable | Should -BeTrue
        $report.AppIdFilter | Should -Be @('A')
        $report.CoverageGap.Count | Should -Be 1
        $report.CoverageGap[0].Source | Should -Be 'Winget'
    }

    It 'leaves CoverageGap empty when probes ran successfully' {
        $results = @(
            [PSCustomObject]@{ AppId='A'; Source='Winget'; Identifier='X.Y'; Status='Ok'; Reason='found'; Detail=''; FromCache=$false; CheckedAt='2026-05-10T00:00:00Z' }
        )
        $env = [PSCustomObject]@{ WingetAvailable = $true; ChocolateyAvailable = $true }
        $report = New-FreshnessReport -DatabaseVersion '1.0.0' -AppCount 1 -Results $results -ProbeEnvironment $env
        $report.CoverageGap.Count | Should -Be 0
    }
}

Describe 'Invoke-CatalogFreshness -AppIdFilter end-to-end' {
    BeforeAll {
        # Mock the actual probes so the test never hits the network/CLIs.
        Mock -CommandName Test-WingetIdentifier     -MockWith { [PSCustomObject]@{ Status='Ok'; Reason='found'; Detail='mocked' } }
        Mock -CommandName Test-ChocolateyIdentifier -MockWith { [PSCustomObject]@{ Status='Ok'; Reason='found'; Detail='mocked' } }
        Mock -CommandName Test-DirectUrlReachable   -MockWith { [PSCustomObject]@{ Status='Ok'; Reason='http-ok'; Detail='mocked' } }
    }

    It 'restricts probing to a single AppId and produces results only for it' {
        $tmpReport = Join-Path $TestDrive 'filtered-report.json'
        $outcome = Invoke-CatalogFreshness `
            -DatabasePath $script:FixturePath `
            -Checks @('Winget','Chocolatey','DirectUrl') `
            -AppIdFilter @('WingetOnly') `
            -JsonReportPath $tmpReport `
            -CacheTtlHours 168 `
            -ThrottleMs 0

        $outcome.ExitCode | Should -Be 0
        $report = Get-Content -LiteralPath $tmpReport -Raw -Encoding UTF8 | ConvertFrom-Json
        $report.AppIdFilter | Should -Be @('WingetOnly')
        $appIds = @($report.Results | ForEach-Object { $_.AppId } | Sort-Object -Unique)
        $appIds | Should -Be @('WingetOnly')
    }

    It 'warns and continues when filter contains unknown AppId' {
        $warnings = @()
        $outcome = Invoke-CatalogFreshness `
            -DatabasePath $script:FixturePath `
            -Checks @('Winget') `
            -AppIdFilter @('WingetOnly','DoesNotExist') `
            -CacheTtlHours 168 `
            -ThrottleMs 0 `
            -WarningVariable warnings `
            -WarningAction SilentlyContinue

        $outcome.ExitCode | Should -Be 0
        ($warnings | Out-String) | Should -Match 'DoesNotExist'
    }
}

# Copyright 2026 Julien Bombled. Licensed under the Apache License, Version 2.0.
BeforeAll { Import-Module "$PSScriptRoot/../Core/Core.psm1" -Force }
Describe 'Native execution across PowerShell runtimes' {
    It 'preserves spaces, embedded quotes and trailing backslashes with a timeout' {
        $child=Join-Path $TestDrive 'argument fixture.ps1'
        'ConvertTo-Json -InputObject @($args) -Compress' | Set-Content -LiteralPath $child
        $expected=@('two words','embedded"quote','trailing\')
        $result=Invoke-NativeCommandUtf8 -FilePath "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe" -ArgumentList (@('-NoProfile','-File',$child)+$expected) -TimeoutSeconds 15
        $result.ExitCode | Should -Be 0
        $actual=ConvertFrom-Json -InputObject $result.Output
        $actual.Count | Should -Be $expected.Count
        for($index=0;$index -lt $expected.Count;$index++) { $actual[$index] | Should -BeExactly $expected[$index] }
    }
    It 'terminates a timed-out native process' {
        $result=Invoke-NativeCommandUtf8 -FilePath "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe" -ArgumentList @('-NoProfile','-Command','Start-Sleep -Seconds 30') -TimeoutSeconds 1
        $result.ExitCode | Should -Be -1
        $result.Output | Should -Match 'timed out'
    }
}


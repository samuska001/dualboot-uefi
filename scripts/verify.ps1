$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'test.ps1')
& (Join-Path $PSScriptRoot 'build.ps1')
$scenarios = @('Menu','Profiles','Assets','Invalid','Diagnostic','Chain','Return','Self','SelfAlias','Timer','ModeReturn','Report')
foreach ($scenario in $scenarios) { & (Join-Path $PSScriptRoot 'smoke-qemu.ps1') -Scenario $scenario }
Write-Output 'PASS: native tests, EFI build and 12 QEMU/OVMF scenarios. Hardware compatibility requires physical testing.'

param([string]$Python='python')
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    & $Python tests/display_choice_test.py
    if($LASTEXITCODE -ne 0){throw 'SteamOS display helper tests failed'}
    & $Python scripts/test-steam-collector.py
    if($LASTEXITCODE -ne 0){throw 'Secure Boot collector tests failed'}
} finally {Pop-Location}

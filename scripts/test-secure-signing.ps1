param([Parameter(Mandatory=$true)][string]$SignedFixture)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
 New-Item -ItemType Directory build -Force | Out-Null
 & "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /r:System.Security.dll /out:build\secure-signing-tests.exe installer\SecureSigning.cs installer\SecureSigningTests.cs
 if($LASTEXITCODE -ne 0){throw 'Secure signing tests compilation failed'}
 & .\build\secure-signing-tests.exe $SignedFixture
 if($LASTEXITCODE -ne 0){throw 'Secure signing tests failed'}
} finally {Pop-Location}

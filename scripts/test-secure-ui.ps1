$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
Push-Location $root
try{
 New-Item -ItemType Directory build\installer -Force | Out-Null
 $sources=@(Get-ChildItem installer -Filter '*.cs' -File | Where-Object Name -ne 'Program.cs' | ForEach-Object {$_.FullName})
 & "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /codepage:65001 /warnaserror /target:exe /main:DualBoot.Installer.SecureBootFormTests /out:build\installer\secure-ui-tests.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Security.dll /r:System.Web.Extensions.dll /r:System.IO.Compression.dll /r:System.Management.dll @sources
 if($LASTEXITCODE -ne 0){throw 'Secure UI tests compilation failed'}
 & .\build\installer\secure-ui-tests.exe
 if($LASTEXITCODE -ne 0){throw 'Secure UI recovery tests failed'}
}finally{Pop-Location}

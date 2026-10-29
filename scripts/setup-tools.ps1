[CmdletBinding()]
param([switch]$WithQemu)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$toolsRoot = Join-Path $projectRoot 'tools'
$downloads = Join-Path $toolsRoot 'downloads'
New-Item -ItemType Directory -Path $downloads -Force | Out-Null
$sevenZip = (Get-Command 7z.exe -ErrorAction SilentlyContinue).Source; if (-not $sevenZip -and (Test-Path -LiteralPath "$env:ProgramFiles\7-Zip\7z.exe")) { $sevenZip = "$env:ProgramFiles\7-Zip\7z.exe" }
function Get-CheckedArchive([string]$Url, [string]$Name, [string]$Algorithm, [string]$Expected) {
    $archivePath = Join-Path $downloads $Name
    if (-not (Test-Path -LiteralPath $archivePath)) { Invoke-WebRequest -Uri $Url -OutFile $archivePath }
    if ((Get-FileHash -LiteralPath $archivePath -Algorithm $Algorithm).Hash -ne $Expected) { throw "Hash incorreto: $Name. Remova o download incompleto e tente novamente." }
    return $archivePath
}
$zigDir = Join-Path $toolsRoot 'zig-x86_64-windows-0.15.2'
if (-not (Test-Path -LiteralPath (Join-Path $zigDir 'zig.exe'))) {
    $archive = Get-CheckedArchive 'https://ziglang.org/download/0.15.2/zig-x86_64-windows-0.15.2.zip' 'zig-0.15.2.zip' 'SHA256' '3a0ed1e8799a2f8ce2a6e6290a9ff22e6906f8227865911fb7ddedc3cc14cb0c'
    if ($sevenZip) { & $sevenZip x $archive "-o$toolsRoot" -y | Out-Null; if ($LASTEXITCODE -ne 0) { throw 'Falha ao extrair Zig' } }
    else { Expand-Archive -LiteralPath $archive -DestinationPath $toolsRoot -Force }
}
if ($WithQemu -and -not (Test-Path -LiteralPath (Join-Path $toolsRoot 'qemu\qemu-system-x86_64.exe'))) {
    if (-not $sevenZip) { throw 'A extração portátil de QEMU requer 7z.exe. O instalador não será executado.' }
    $qemuBase = 'https://qemu.weilnetz.de/w64/qemu-w64-setup-20260811'
    $checksumText = (Invoke-WebRequest -Uri "$qemuBase.sha512").Content
    if ($checksumText -is [byte[]]) { $checksumText = [Text.Encoding]::ASCII.GetString($checksumText) }
    $expectedHash = [regex]::Match([string]$checksumText, '(?i)\b[a-f0-9]{128}\b').Value
    if (-not $expectedHash) { throw 'Checksum oficial de QEMU não reconhecido' }
    $archive = Get-CheckedArchive "$qemuBase.exe" 'qemu-20260811.exe' 'SHA512' $expectedHash
    $qemuDir = Join-Path $toolsRoot 'qemu'
    & $sevenZip x $archive "-o$qemuDir" -y | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Falha ao extrair QEMU' }
}
& (Join-Path $zigDir 'zig.exe') version
if ($LASTEXITCODE -ne 0) { throw 'Zig não executou' }
Write-Output 'Ferramentas locais prontas; nenhuma instalação global.'

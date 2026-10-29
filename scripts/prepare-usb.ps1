[CmdletBinding(SupportsShouldProcess)]
param([Parameter(Mandatory)][ValidatePattern('^[A-Za-z]$')][string]$DriveLetter,[ValidateSet('Diagnostic','Menu')][string]$Mode='Diagnostic')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$partition = Get-Partition -DriveLetter $DriveLetter
$disk = $partition | Get-Disk
$volume = Get-Volume -DriveLetter $DriveLetter
if ($disk.BusType -ne 'USB' -or $disk.IsBoot -or $disk.IsSystem) { throw 'Destino recusado: exige USB externo sem função de boot/sistema. Nenhum arquivo foi copiado.' }
if ($volume.FileSystem -ne 'FAT32') { throw 'O USB precisa já estar em FAT32. Este script não formata.' }
$usbRoot = $DriveLetter.ToUpperInvariant() + ':\'
$bootFolder = Join-Path $usbRoot 'EFI\BOOT'
if (Test-Path -LiteralPath $bootFolder) { throw 'O USB já contém EFI/BOOT. Use uma mídia sem outro carregador; este script não sobrescreve essa pasta.' }
$source = Join-Path $projectRoot 'dist\DualBoot-0.1.0\EFI'
if (-not (Test-Path -LiteralPath $source)) { throw 'Gere o pacote com scripts/package.ps1 antes de preparar o USB.' }
if ($PSCmdlet.ShouldProcess($usbRoot,"Copiar DualBoot ($Mode), sem formatar")) {
    Copy-Item -LiteralPath $source -Destination $usbRoot -Recurse
    if ($Mode -eq 'Menu') { Copy-Item -LiteralPath (Join-Path $bootFolder 'DualBoot.efi') -Destination (Join-Path $bootFolder 'BOOTX64.EFI') -Force }
    Write-Output "USB preparado em $usbRoot. Inicialize pela entrada UEFI do USB."
}

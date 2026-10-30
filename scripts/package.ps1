$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$package = Join-Path $root 'dist\DualBoot-0.1.0'
if (Test-Path -LiteralPath $package) { throw 'Package directory already exists. Move it aside before packaging again.' }
$efi = Join-Path $package 'EFI\BOOT'
New-Item -ItemType Directory -Path $efi -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $root 'build\Diagnostic.efi') -Destination (Join-Path $efi 'BOOTX64.EFI')
Copy-Item -LiteralPath (Join-Path $root 'build\DualBoot.efi') -Destination $efi
Copy-Item -LiteralPath (Join-Path $root 'config\dualboot.ini') -Destination $efi
Copy-Item -LiteralPath (Join-Path $root 'themes') -Destination $efi -Recurse
foreach ($name in @('README.md','LICENSE','THIRD_PARTY.md','CHANGELOG.md')) { Copy-Item -LiteralPath (Join-Path $root $name) -Destination $package }
New-Item -ItemType Directory -Path (Join-Path $package 'docs') | Out-Null
foreach ($name in @('MANUAL.md','INSTALADOR.md','VALIDACAO.md','menu.png','installer.png')) { Copy-Item -LiteralPath (Join-Path $root ('docs\'+$name)) -Destination (Join-Path $package 'docs') }
$hashes = Get-ChildItem -LiteralPath $package -Recurse -File | Sort-Object FullName | ForEach-Object {
    (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + $_.FullName.Substring($package.Length+1).Replace('\','/')
}
$hashes | Set-Content -LiteralPath (Join-Path $package 'SHA256SUMS.txt') -Encoding ascii
$zip = Join-Path $root 'dist\DualBoot-0.1.0.zip'
Compress-Archive -LiteralPath $package -DestinationPath $zip -Force
((Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()+'  DualBoot-0.1.0.zip') | Set-Content -LiteralPath (Join-Path $root 'dist\SHA256SUMS.txt') -Encoding ascii
Get-Item -LiteralPath $zip | Select-Object FullName,Length

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$zig = Join-Path $projectRoot 'tools\zig-x86_64-windows-0.15.2\zig.exe'
New-Item -ItemType Directory -Path (Join-Path $projectRoot 'build') -Force | Out-Null
Push-Location $projectRoot
try {
    $flags = @('build-exe','-target','x86_64-uefi','-O','ReleaseSmall','-fno-compiler-rt','-fno-stack-check','-fno-stack-protector')
    $cflags = @('-cflags','-std=c11','-O2','-Wall','-Wextra','-Werror','-ffreestanding','-fshort-wchar','-mno-red-zone','-mno-stack-arg-probe','-fno-stack-protector','-fno-builtin')
    $sources = @('src/core.c','src/platform.c','src/menu.c','src/main.c')
    & $zig @flags @cflags -- @sources '-femit-bin=build/DualBoot.efi'
    if ($LASTEXITCODE -ne 0) { throw 'Compilação EFI falhou' }
    & $zig @flags @cflags -DDB_DIAGNOSTIC=1 -- @sources '-femit-bin=build/Diagnostic.efi'
    if ($LASTEXITCODE -ne 0) { throw 'Compilação do diagnóstico falhou' }
    & $zig @flags @cflags -DDB_TEST_IO=1 -- @sources '-femit-bin=build/Smoke.efi'
    if ($LASTEXITCODE -ne 0) { throw 'Compilação para emulador falhou' }
    & $zig @flags @cflags -DDB_TEST_IO=1 -DDB_DIAGNOSTIC=1 -- @sources '-femit-bin=build/SmokeDiagnostic.efi'
    if ($LASTEXITCODE -ne 0) { throw 'Compilação de diagnóstico para emulador falhou' }
    & $zig @flags @cflags -- tests/efi_child.c '-femit-bin=build/child.efi'
    if ($LASTEXITCODE -ne 0) { throw 'Compilação da imagem de teste falhou' }
    & $zig @flags @cflags -- tests/efi_return.c '-femit-bin=build/return.efi'
    if ($LASTEXITCODE -ne 0) { throw 'Compilação da imagem de retorno falhou' }
    & $zig @flags @cflags -- tests/efi_mode_return.c '-femit-bin=build/mode-return.efi'
    if ($LASTEXITCODE -ne 0) { throw 'Compilação da imagem que troca modo de vídeo falhou' }
    Write-Output 'EFI x86-64 compilados.'
} finally { Pop-Location }

param([switch]$Test)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$zig = Join-Path $projectRoot 'tools\zig-x86_64-windows-0.15.2\zig.exe'
$out = Join-Path $projectRoot 'build\secureboot-payload'
New-Item -ItemType Directory -Path $out -Force | Out-Null
Push-Location $projectRoot
try {
    if ($Test) {
        & $zig cc -DDB_SB_HOST_TEST -std=c11 -Wall -Wextra -Werror tests/secureboot_report_test.c src/secureboot_report.c -o build/secureboot-payload/report-test.exe
        if ($LASTEXITCODE -ne 0) { throw 'Falha ao compilar testes de binding/relatório' }
        & "$out\report-test.exe"
        if ($LASTEXITCODE -ne 0) { throw 'Testes de binding/relatório falharam' }
    }
    $flags = @('build-exe','-target','x86_64-uefi','-O','ReleaseSmall','-fno-compiler-rt','-fno-stack-check','-fno-stack-protector')
    $cflags = @('-cflags','-std=c11','-O2','-Wall','-Wextra','-Werror','-ffreestanding','-fshort-wchar','-mno-red-zone','-mno-stack-arg-probe','-fno-stack-protector','-fno-builtin','-DDB_SECURE_BOOT=1','-DDB_MOK_REPORT=1')
    $sources = @('src/core.c','src/platform.c','src/menu.c','src/main.c','src/secureboot_report.c')
    $variants = @(
        @{Name='DualBoot';Defines=@()},
        @{Name='Diagnostic';Defines=@('-DDB_DIAGNOSTIC=1')},
        @{Name='Smoke';Defines=@('-DDB_TEST_IO=1')},
        @{Name='SmokeDiagnostic';Defines=@('-DDB_TEST_IO=1','-DDB_DIAGNOSTIC=1')}
    )
    foreach ($variant in $variants) {
        $defines = $variant.Defines
        $target = 'build/secureboot-payload/' + $variant.Name + '.template.efi'
        & $zig @flags @cflags @defines -- @sources "-femit-bin=$target"
        if ($LASTEXITCODE -ne 0) { throw "Falha ao compilar $target" }
        # Inspect the dedicated PE section, never search arbitrary code for a patch marker.
        $bytes = [IO.File]::ReadAllBytes((Join-Path $projectRoot $target))
        $pe = [BitConverter]::ToUInt32($bytes,0x3c)
        $count = [BitConverter]::ToUInt16($bytes,$pe+6)
        $section = $pe+24+[BitConverter]::ToUInt16($bytes,$pe+20)
        $bindings = 0
        $sbat = $false
        for ($i=0; $i -lt $count; $i++) {
            $at=$section+40*$i
            $name=[Text.Encoding]::ASCII.GetString($bytes,$at,8).TrimEnd([char]0)
            if ($name -eq '.sbat') { $sbat=$true }
            if ($name -ne '.dbbind') { continue }
            $bindings++
            $raw=[BitConverter]::ToUInt32($bytes,$at+20)
            $rawSize=[BitConverter]::ToUInt32($bytes,$at+16)
            $virtualSize=[BitConverter]::ToUInt32($bytes,$at+8)
            if ($rawSize -lt 4228 -or $virtualSize -lt 4228 -or $raw+4228 -gt $bytes.Length) { throw 'Região de binding truncada' }
            if ([Text.Encoding]::ASCII.GetString($bytes,$raw,32) -cne 'DUALBOOT_SB_BINDING_V2_20261030!') { throw 'Magic de binding inválido' }
            for ($j=32; $j -lt 4228; $j++) { if ($bytes[$raw+$j] -ne 0) { throw 'Template contém binding concreto inesperado' } }
        }
        if ($bindings -ne 1 -or -not $sbat) { throw 'Template sem região única .dbbind ou SBAT' }
    }
    Write-Output "Templates EFI públicos sem assinatura em $out. Preencha .dbbind antes da assinatura."
} finally { Pop-Location }

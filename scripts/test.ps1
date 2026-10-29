$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$zig = Join-Path $root 'tools\zig-x86_64-windows-0.15.2\zig.exe'
New-Item -ItemType Directory -Path (Join-Path $root 'build') -Force | Out-Null
Push-Location $root
try {
    $suites = @(
        @{Name='core_test'; Sources=@('tests/core_test.c','src/core.c')},
        @{Name='platform_test'; Sources=@('tests/platform_test.c','src/platform.c','src/core.c')},
        @{Name='menu_test'; Sources=@('tests/menu_test.c','src/menu.c','src/core.c')},
        @{Name='view_fallback_test'; Sources=@('tests/view_fallback_test.c','src/platform.c','src/menu.c','src/core.c')}
    )
    foreach ($suite in $suites) {
        $sources = $suite.Sources
        & $zig cc -std=c11 -Wall -Wextra -Werror -O1 -fno-builtin @sources -o ('build/'+$suite.Name+'.exe')
        if ($LASTEXITCODE -ne 0) { throw ('Compilation failed: '+$suite.Name) }
        & (Join-Path $root ('build/'+$suite.Name+'.exe'))
        if ($LASTEXITCODE -ne 0) { throw ('Tests failed: '+$suite.Name) }
    }
} finally { Pop-Location }

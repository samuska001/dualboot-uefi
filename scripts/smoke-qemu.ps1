[CmdletBinding()]
param([ValidateSet('Menu','Profiles','Assets','Invalid','Chain','Diagnostic','Report','Self','SelfAlias','Return','ModeReturn','Timer')][string]$Scenario = 'Menu')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$qemuRoot = Join-Path $projectRoot 'tools\qemu'
$runRoot = Join-Path $projectRoot ('build\qemu-' + $Scenario.ToLowerInvariant())
$efiFolder = Join-Path $runRoot 'disk\EFI\BOOT'
New-Item -ItemType Directory -Path $efiFolder -Force | Out-Null
if ($Scenario -ne 'Report') { Copy-Item -LiteralPath (Join-Path $projectRoot 'themes') -Destination $efiFolder -Recurse -Force }
Copy-Item -LiteralPath (Join-Path $projectRoot 'build\Smoke.efi') -Destination (Join-Path $efiFolder 'BOOTX64.EFI') -Force
if ($Scenario -eq 'Diagnostic') { Copy-Item -LiteralPath (Join-Path $projectRoot 'build\SmokeDiagnostic.efi') -Destination (Join-Path $efiFolder 'BOOTX64.EFI') -Force }
Copy-Item -LiteralPath (Join-Path $projectRoot 'build\child.efi') -Destination (Join-Path $efiFolder 'child.efi') -Force
$config = "[menu]`ntimeout=0`n[windows]`nsource=file`npath=\EFI\BOOT\child.efi`n[steamos]`nenabled=false`n"
if ($Scenario -eq 'Profiles') { $config = "[menu]`ntimeout=0`n[windows]`nsource=file`npath=\EFI\BOOT\child.efi`n[steamos]`nsource=file`npath=\EFI\BOOT\child.efi`n" }
if ($Scenario -eq 'Assets') { $config = "[menu]`ntimeout=0`ntheme=missing-assets`n" }
if ($Scenario -eq 'Invalid') { $config = "[menu]`ntimeout=-1`n" }
if ($Scenario -eq 'Diagnostic') { $config = "[menu]`ntimeout=0`n" }
if ($Scenario -eq 'Self') { $config = "[menu]`ntimeout=0`n[windows]`nsource=file`npath=\EFI\BOOT\BOOTX64.EFI`n" }
if ($Scenario -eq 'SelfAlias') { $config = "[menu]`ntimeout=0`n[windows]`nsource=file`npath=\EFI\BOOT\.\BOOTX64.EFI`n" }
if ($Scenario -eq 'Timer') { $config = $config.Replace('timeout=0','timeout=1') }
if ($Scenario -eq 'Return') { Copy-Item -LiteralPath (Join-Path $projectRoot 'build\return.efi') -Destination (Join-Path $efiFolder 'child.efi') -Force }
if ($Scenario -eq 'ModeReturn') { Copy-Item -LiteralPath (Join-Path $projectRoot 'build\mode-return.efi') -Destination (Join-Path $efiFolder 'child.efi') -Force }
if ($Scenario -eq 'Report') { [IO.File]::WriteAllText((Join-Path $efiFolder 'diagnostic.txt'),('STALE-TAIL '*2000),[Text.Encoding]::ASCII) }
[IO.File]::WriteAllText((Join-Path $efiFolder 'dualboot.ini'), $config, [Text.Encoding]::ASCII)
$varsFile = Join-Path $runRoot 'vars.fd'
Copy-Item -LiteralPath (Join-Path $qemuRoot 'share\edk2-i386-vars.fd') -Destination $varsFile -Force
$debugLog = Join-Path $runRoot 'debug.log'
if (Test-Path -LiteralPath $debugLog) { Remove-Item -LiteralPath $debugLog }
$listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback,0)
$listener.Start(); $port = $listener.LocalEndpoint.Port; $listener.Stop()
$processInfo = [Diagnostics.ProcessStartInfo]::new()
$processInfo.FileName = Join-Path $qemuRoot 'qemu-system-x86_64.exe'
$processInfo.UseShellExecute = $false
$processInfo.CreateNoWindow = $true
$virtualDrive = 'format=raw,file=fat:rw:' + (Join-Path $runRoot 'disk')
if ($Scenario -eq 'Report') {
    $reportDisk = Join-Path $runRoot 'report-disk.img'
    & (Join-Path $qemuRoot 'qemu-img.exe') convert -f vvfat -O raw ('fat:floppy:' + (Join-Path $runRoot 'disk')) $reportDisk
    if ($LASTEXITCODE -ne 0) { throw 'Falha ao preparar FAT virtual de teste' }
    $virtualDrive = 'format=raw,file=' + $reportDisk
}
$argsList = @('-machine','q35','-accel','tcg','-m','256','-display','none','-no-reboot','-net','none',
 '-drive',('if=pflash,format=raw,readonly=on,file='+(Join-Path $qemuRoot 'share\edk2-x86_64-code.fd')),
 '-drive',('if=pflash,format=raw,file='+$varsFile),
 '-drive',$virtualDrive,
 '-debugcon',('file:'+$debugLog),'-global','isa-debugcon.iobase=0xe9',
 '-device','isa-debug-exit,iobase=0xf4,iosize=0x04','-qmp',"tcp:127.0.0.1:$port,server=on,wait=off")
foreach ($arg in $argsList) { $processInfo.ArgumentList.Add($arg) }
$qemu = [Diagnostics.Process]::Start($processInfo)
$client = [Net.Sockets.TcpClient]::new()
function Read-DebugLog {
    if (-not (Test-Path -LiteralPath $debugLog)) { return '' }
    $fileStream = [IO.File]::Open($debugLog,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::ReadWrite)
    $logReader = [IO.StreamReader]::new($fileStream)
    try { return $logReader.ReadToEnd() } finally { $logReader.Dispose() }
}
try {
    $deadline = [DateTime]::UtcNow.AddSeconds(45)
    while (-not $client.Connected -and [DateTime]::UtcNow -lt $deadline) {
        try { $client.Connect('127.0.0.1',$port) } catch { Start-Sleep -Milliseconds 100 }
    }
    if (-not $client.Connected) { throw 'QMP não iniciou' }
    $stream = $client.GetStream(); $stream.ReadTimeout = 3000
    $reader = [IO.StreamReader]::new($stream); $writer = [IO.StreamWriter]::new($stream); $writer.AutoFlush = $true
    $null = $reader.ReadLine()
    function Send-Qmp([hashtable]$Command) {
        $writer.WriteLine(($Command | ConvertTo-Json -Compress -Depth 8))
        do { $line = $reader.ReadLine(); if (-not $line) { throw 'QMP desconectado' }; $response = $line | ConvertFrom-Json } while ($response.PSObject.Properties.Name -contains 'event')
        if ($response.PSObject.Properties.Name -contains 'error') { throw ($response.error | ConvertTo-Json -Compress) }
        return $response
    }
    $null = Send-Qmp @{execute='qmp_capabilities'}
    $ready = $false
    while ([DateTime]::UtcNow -lt $deadline -and -not $qemu.HasExited) {
        $log = Read-DebugLog
        if ($log.Contains('DB:MENU')) { $ready = $true; break }
        Start-Sleep -Milliseconds 200
    }
    if (-not $ready) { throw "O menu UEFI não iniciou. Consulte $debugLog" }
    if ($Scenario -eq 'Invalid' -and -not $log.Contains('DB:CONFIG_INVALID')) { throw 'Configuração inválida não foi detectada' }
    if ($Scenario -in @('Diagnostic','Report')) {
        $null = Send-Qmp @{execute='human-monitor-command';arguments=@{'command-line'='sendkey d'}}
        Start-Sleep -Milliseconds 300
        $log = Read-DebugLog
        if (-not $log.Contains('DB:DIAGNOSTIC')) { throw 'Diagnóstico não abriu' }
    }
    $screen = Join-Path $runRoot 'screen.png'
    $null = Send-Qmp @{execute='screendump';arguments=@{filename=$screen;format='png'}}
    if ($Scenario -eq 'Assets' -and -not $log.Contains('DB:ASSET_INVALID')) { throw 'Assets ausentes nao abriram recuperacao.' }
    if ($Scenario -eq 'Profiles') {
        if ($log.Contains('DB:ASSET_INVALID') -or $log.Contains('DB:DIAGNOSTIC')) { throw 'Tema nao iniciou no menu grafico.' }
        $null = Send-Qmp @{execute='human-monitor-command';arguments=@{'command-line'='sendkey right'}}
        Start-Sleep -Milliseconds 400
        $selectedScreen=Join-Path $runRoot 'selected-steamos.png'
        $null = Send-Qmp @{execute='screendump';arguments=@{filename=$selectedScreen;format='png'}}
        if ((Get-FileHash -LiteralPath $screen).Hash -eq (Get-FileHash -LiteralPath $selectedScreen).Hash) { throw 'Foco nao mudou visualmente.' }
        $null = Send-Qmp @{execute='human-monitor-command';arguments=@{'command-line'='sendkey d'}}
        Start-Sleep -Milliseconds 250
        $null = Send-Qmp @{execute='human-monitor-command';arguments=@{'command-line'='sendkey m'}}
        Start-Sleep -Milliseconds 400
        $restoredScreen=Join-Path $runRoot 'after-diagnostic.png'
        $null = Send-Qmp @{execute='screendump';arguments=@{filename=$restoredScreen;format='png'}}
        if ((Get-FileHash -LiteralPath $selectedScreen).Hash -ne (Get-FileHash -LiteralPath $restoredScreen).Hash) { throw 'Retorno do diagnostico nao restaurou menu/foco.' }
    }
    if ($Scenario -in @('Chain','Timer')) {
        if ($Scenario -eq 'Chain') { $null = Send-Qmp @{execute='human-monitor-command';arguments=@{'command-line'='sendkey ret'}} }
        $exitDeadline = [DateTime]::UtcNow.AddSeconds(10)
        while (-not $qemu.HasExited -and [DateTime]::UtcNow -lt $exitDeadline) { Start-Sleep -Milliseconds 100 }
        $log = Read-DebugLog
        if (-not $log.Contains('DB:CHILD_OK') -or -not $log.Contains('DB:HANDOFF_BLACK') -or -not $qemu.HasExited -or $qemu.ExitCode -ne 33) { throw 'A imagem EFI filha não confirmou a transferência com tela limpa' }
    }
    if ($Scenario -in @('Self','SelfAlias')) {
        $null = Send-Qmp @{execute='human-monitor-command';arguments=@{'command-line'='sendkey m'}}
        Start-Sleep -Milliseconds 150
        $null = Send-Qmp @{execute='human-monitor-command';arguments=@{'command-line'='sendkey ret'}}
        Start-Sleep -Milliseconds 300
        $log = Read-DebugLog
        if ($log.Contains('DB:CHILD_START')) { throw 'Recursão: o gerenciador tentou iniciar a si mesmo' }
        if (-not $log.Contains('DB:TARGET_UNAVAILABLE')) { throw 'Destino recursivo não foi sinalizado como indisponível' }
    }
    if ($Scenario -in @('Return','ModeReturn')) {
        foreach ($attempt in 1..2) {
            $null = Send-Qmp @{execute='human-monitor-command';arguments=@{'command-line'='sendkey ret'}}
            Start-Sleep -Milliseconds 500
            $null = Send-Qmp @{execute='human-monitor-command';arguments=@{'command-line'='sendkey m'}}
            Start-Sleep -Milliseconds 200
        }
        $log = Read-DebugLog
        if ([regex]::Matches($log,'DB:TEST_RETURN').Count -ne 2) { throw 'O menu não recuperou a interação após retorno do carregador' }
        if ($Scenario -eq 'ModeReturn') {
            if ([regex]::Matches($log,'DB:MODE_CHANGED').Count -ne 2) { throw 'A imagem filha não executou a troca de modo' }
            $after = Join-Path $runRoot 'after-return.png'
            $null = Send-Qmp @{execute='screendump';arguments=@{filename=$after;format='png'}}
            $originalPng = [IO.File]::ReadAllBytes($screen); $afterPng = [IO.File]::ReadAllBytes($after)
            if (-not [Linq.Enumerable]::SequenceEqual([byte[]]$originalPng[16..23],[byte[]]$afterPng[16..23])) { throw 'A resolução do menu não foi restaurada após retorno' }
        }
    }
    if ($Scenario -eq 'Report') {
        $null = Send-Qmp @{execute='human-monitor-command';arguments=@{'command-line'='sendkey f2'}}
        Start-Sleep -Milliseconds 500
        $null = Send-Qmp @{execute='quit'}
        $qemu.WaitForExit()
        $sevenZip = (Get-Command 7z.exe -ErrorAction SilentlyContinue).Source; if (-not $sevenZip -and (Test-Path -LiteralPath "$env:ProgramFiles\7-Zip\7z.exe")) { $sevenZip = "$env:ProgramFiles\7-Zip\7z.exe" }
        $readback = Join-Path $runRoot 'readback'
        if($sevenZip){& $sevenZip x $reportDisk 'EFI\BOOT\diagnostic.txt' "-o$readback" -y | Out-Null}
        else {
            $python=(Get-Command python.exe -ErrorAction SilentlyContinue).Source
            if(-not $python){$python=Join-Path $env:USERPROFILE '.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'}
            if(-not (Test-Path -LiteralPath $python)){throw '7-Zip ou Python com pyfatfs necessário para a leitura independente do teste'}
            $oldPythonPath=$env:PYTHONPATH
            try{$env:PYTHONPATH=(Join-Path $projectRoot 'tools\fat-test')+[IO.Path]::PathSeparator+$oldPythonPath;& $python (Join-Path $PSScriptRoot 'read-fat-report.py') $reportDisk (Join-Path $readback 'EFI\BOOT\diagnostic.txt')}
            finally{$env:PYTHONPATH=$oldPythonPath}
        }
        if ($LASTEXITCODE -ne 0) { throw 'Falha na leitura independente do FAT gravado' }
        $report = [IO.File]::ReadAllText((Join-Path $readback 'EFI\BOOT\diagnostic.txt'))
        if (-not $report.Contains('USB 045E:028E') -or -not $report.TrimEnd().EndsWith('--- END OF CURRENT INVENTORY ---') -or $report.Contains('STALE-TAIL')) { throw 'Relatório incompleto ou com conteúdo antigo após a gravação' }
    }
    Write-Output "PASS QEMU $Scenario : $screen"
} finally {
    $client.Dispose()
    if (-not $qemu.HasExited) { $qemu.Kill(); $qemu.WaitForExit() }
    $qemu.Dispose()
}

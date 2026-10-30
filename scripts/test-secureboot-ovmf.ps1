# Isolated OVMF laboratory: accepts PUBLIC laboratory fixtures only. No key/store access.
# All mutable VM state lives below build/qemu-secureboot-installer. Guest has no NIC.
param(
 [string]$Python = '',
 [ValidateRange(10,120)][int]$TimeoutSeconds = 35,
 [switch]$PrepareOnly
)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$run=[IO.Path]::GetFullPath((Join-Path $root 'build\qemu-secureboot-installer'))
$lab=Join-Path $root 'build\secureboot-lab'
if (-not $Python) {
 $candidate=Join-Path $env:USERPROFILE '.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
 if(Test-Path -LiteralPath $candidate){$Python=$candidate}else{$Python=(Get-Command python.exe -ErrorAction Stop).Source}
}
# Resolve every existing input before making VM output; never use physical drives.
$shim=Join-Path $root 'tools\shim-fedora-16.1-5\usr\lib\efi\shim\16.1-5\EFI\fedora\shimx64.efi'
$qemu=Join-Path $root 'tools\qemu\qemu-system-x86_64.exe'
$code=Join-Path $root 'tools\qemu\share\edk2-x86_64-secure-code.fd'
$varsTemplate=Join-Path $root 'tools\qemu\share\edk2-i386-vars.fd'
$inputs=@($shim,$qemu,$code,$varsTemplate,(Join-Path $lab 'lab.cer'),(Join-Path $lab 'lab-signed-menu.efi'),(Join-Path $lab 'lab-signed-smoke.efi'),(Join-Path $lab 'lab-signed-diag.efi'),(Join-Path $lab 'lab-signed-probe.efi'),(Join-Path $lab 'wrong-cert.efi'),(Join-Path $root 'build\secureboot-payload\Smoke.template.efi'))
foreach($inputPath in $inputs){if(-not (Test-Path -LiteralPath $inputPath -PathType Leaf)){throw "Fixture/ferramenta ausente: $inputPath"}}
if((Get-FileHash -LiteralPath $shim -Algorithm SHA256).Hash -ne '571EA56B855DCF73BEC6ACB63C5DED44C2A191138BCA0D8CFA5AA93F60F46FFF'){throw 'Shim diverge da origem fixada'}
# Refuse linked output directories, including existing ancestors.
$ancestor=$run
while($ancestor -and $ancestor.Length -ge $root.Length){if((Test-Path -LiteralPath $ancestor) -and ((Get-Item -LiteralPath $ancestor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)){throw "Diretório vinculado recusado: $ancestor"};$ancestor=Split-Path -Parent $ancestor}
New-Item -ItemType Directory -Path $run -Force | Out-Null
$cert=[Security.Cryptography.X509Certificates.X509Certificate2]::new([IO.File]::ReadAllBytes((Join-Path $lab 'lab.cer')))
$certificateHash=([BitConverter]::ToString([Security.Cryptography.SHA256]::Create().ComputeHash($cert.RawData))).Replace('-','').ToLowerInvariant()
$testedFiles=@()
foreach($name in @('lab-signed-menu.efi','lab-signed-smoke.efi','lab-signed-diag.efi','lab-signed-probe.efi','wrong-cert.efi')){
 $path=Join-Path $lab $name
 $signer=(Get-AuthenticodeSignature -LiteralPath $path).SignerCertificate
 if(-not $signer){throw "Fixture sem certificado de assinatura: $name"}
 $same=[Convert]::ToBase64String($signer.RawData) -ceq [Convert]::ToBase64String($cert.RawData)
 if(($name -eq 'wrong-cert.efi' -and $same) -or ($name -ne 'wrong-cert.efi' -and -not $same)){throw "Identidade da fixture incorreta: $name"}
 $testedFiles+=@{Name=$name;SHA256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()}
}
$shimCert=(Get-AuthenticodeSignature -LiteralPath $shim).SignerCertificate
if(-not $shimCert){throw 'Certificado público do shim indisponível'}
$shimPem=Join-Path $run 'shim-signer.pem'
[IO.File]::WriteAllText($shimPem,"-----BEGIN CERTIFICATE-----`n"+[Convert]::ToBase64String($shimCert.RawData,[Base64FormattingOptions]::InsertLineBreaks)+"`n-----END CERTIFICATE-----`n")
$vars=Join-Path $run 'base-vars.fd'
$oldPythonPath=$env:PYTHONPATH
$oldBytecode=$env:PYTHONDONTWRITEBYTECODE
try {
 $env:PYTHONPATH=Join-Path $root 'tools\secureboot-test'
 $env:PYTHONDONTWRITEBYTECODE='1'
 $guid='812ea427-e0b9-4892-94d0-de01b79541b4'
 & $Python -m virt.firmware.vars --input $varsTemplate --output $vars --set-pk $guid (Join-Path $lab 'lab.cer') --add-kek $guid (Join-Path $lab 'lab.cer') --add-db $guid $shimPem --add-mok $guid (Join-Path $lab 'lab.cer') --sb
 if($LASTEXITCODE -ne 0){throw 'Falha ao criar NVRAM somente virtual'}
} finally {$env:PYTHONPATH=$oldPythonPath;$env:PYTHONDONTWRITEBYTECODE=$oldBytecode}
function Read-SharedLog([string]$Path) {
 $stream=[IO.File]::Open($Path,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::ReadWrite)
 try {$reader=[IO.StreamReader]::new($stream);try{return $reader.ReadToEnd()}finally{$reader.Dispose()}}finally{$stream.Dispose()}
}
# Use a real FAT image: vvfat rw can alias new host files to old sectors.
$fatHelper=Join-Path $run 'fat-helper.py'
@'
import pathlib,sys
from pyfatfs.PyFat import PyFat
from pyfatfs.PyFatFS import PyFatFS
mode,image,tree=sys.argv[1:]
image=pathlib.Path(image);tree=pathlib.Path(tree)
if mode=='create':
    with image.open('wb') as f:f.truncate(64*1024*1024)
    fat=PyFat();fat.mkfs(str(image),PyFat.FAT_TYPE_FAT16,size=64*1024*1024,label='DBLAB');fat.close()
    with PyFatFS(str(image),preserve_case=True) as fs:
        for entry in sorted(tree.rglob('*')):
            name=entry.relative_to(tree).as_posix()
            if entry.is_dir():fs.makedirs(name,recreate=True)
            else:fs.writebytes(name,entry.read_bytes())
elif mode=='extract':
    with PyFatFS(str(image),read_only=True) as fs:
        (tree/'secureboot-status.txt').write_bytes(fs.readbytes('EFI/BOOT/secureboot-status.txt'))
else:raise ValueError(mode)
'@ | Set-Content -LiteralPath $fatHelper -Encoding utf8
function Invoke-FatHelper($Mode,$Image,$Tree){
 $previous=$env:PYTHONPATH;$previousBytecode=$env:PYTHONDONTWRITEBYTECODE
 try{$env:PYTHONPATH=Join-Path $root 'tools\fat-test';$env:PYTHONDONTWRITEBYTECODE='1';& $Python $fatHelper $Mode $Image $Tree;if($LASTEXITCODE -ne 0){throw "Falha FAT helper: $Mode"}}finally{$env:PYTHONPATH=$previous;$env:PYTHONDONTWRITEBYTECODE=$previousBytecode}
}
$results=@()
foreach($mode in @('policy','smoke')){
 $case=Join-Path $run $mode
 $disk=Join-Path $case 'disk'
 $boot=Join-Path $disk 'EFI\BOOT'
 # Fresh per-run directories prevent stale reports from passing.
 if(Test-Path -LiteralPath $case){
  $resolved=[IO.Path]::GetFullPath($case)
  if(-not $resolved.StartsWith($run+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Diretório de teste fora do escopo'}
  if((Get-Item -LiteralPath $case -Force).Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Diretório de teste vinculado'}
  # Do not recursively remove links or traverse a pre-existing case tree. Use a new run instead.
  $case=Join-Path $run ($mode+'-'+[Guid]::NewGuid().ToString('N'));$disk=Join-Path $case 'disk';$boot=Join-Path $disk 'EFI\BOOT'
 }
 New-Item -ItemType Directory -Path $boot -Force | Out-Null
 Copy-Item -LiteralPath $shim -Destination (Join-Path $boot 'BOOTX64.EFI')
 $loader=if($mode -eq 'policy'){'lab-signed-probe.efi'}else{'lab-signed-smoke.efi'}
 Copy-Item -LiteralPath (Join-Path $lab $loader) -Destination (Join-Path $boot 'grubx64.efi')
 foreach($pair in @(@('lab-signed-menu.efi','menu.efi'),@('lab-signed-smoke.efi','smoke.efi'),@('lab-signed-diag.efi','diag.efi'),@('wrong-cert.efi','wrong-cert.efi'))){Copy-Item -LiteralPath (Join-Path $lab $pair[0]) -Destination (Join-Path $boot $pair[1])}
 Copy-Item -LiteralPath (Join-Path $root 'build\secureboot-payload\Smoke.template.efi') -Destination (Join-Path $boot 'unsigned.efi')
 $tampered=[IO.File]::ReadAllBytes((Join-Path $lab 'lab-signed-smoke.efi'))
 $pe=[BitConverter]::ToInt32($tampered,60);$section=$pe+24+[BitConverter]::ToUInt16($tampered,$pe+20);$raw=[BitConverter]::ToInt32($tampered,$section+20)
 $tampered[$raw+32]=$tampered[$raw+32] -bxor 1
 [IO.File]::WriteAllBytes((Join-Path $boot 'tampered.efi'),$tampered)
 $image=Join-Path $case 'disk.img';Invoke-FatHelper create $image $disk
 $caseVars=Join-Path $case 'vars.fd';Copy-Item -LiteralPath $vars -Destination $caseVars
 if($PrepareOnly){$results+=@{Case=$mode;State='Prepared';Directory=$case};continue}
 $debug=Join-Path $case 'debug.log'
 $start=[Diagnostics.ProcessStartInfo]::new();$start.FileName=$qemu;$start.UseShellExecute=$false;$start.CreateNoWindow=$true
 $start.RedirectStandardInput=$true;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
 # QMP uses redirected stdio, not a TCP listener. Firmware writes target a private file only.
 $arguments=@('-machine','q35,smm=on','-accel','tcg','-cpu','max','-m','512','-display','none','-no-reboot','-net','none','-global','driver=cfi.pflash01,property=secure,value=on',
 '-drive',('if=pflash,format=raw,readonly=on,file='+$code),'-drive',('if=pflash,format=raw,file='+$caseVars),'-drive',('format=raw,file='+$image),'-debugcon',('file:'+$debug),'-global','isa-debugcon.iobase=0xe9','-device','isa-debug-exit,iobase=0xf4,iosize=0x04','-qmp','stdio')
 foreach($argument in $arguments){$start.ArgumentList.Add($argument)}
 $process=[Diagnostics.Process]::Start($start)
 $stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
 try {
  $process.StandardInput.WriteLine('{"execute":"qmp_capabilities"}');$process.StandardInput.Flush()
  $deadline=[DateTime]::UtcNow.AddSeconds($TimeoutSeconds);$passed=$false
  while([DateTime]::UtcNow -lt $deadline -and -not $process.HasExited){
   if(Test-Path -LiteralPath $debug){$log=(Read-SharedLog $debug);if($mode -eq 'smoke' -and $log.Contains('DB:SECURE_STATUS_SAVED') -and $log.Contains('DB:MENU')){$passed=$true;break}}
   Start-Sleep -Milliseconds 200
  }
  if(-not $process.HasExited){
   $shot=Join-Path $case 'screen.ppm';$process.StandardInput.WriteLine((@{execute='screendump';arguments=@{filename=$shot}} | ConvertTo-Json -Compress -Depth 4));$process.StandardInput.Flush();Start-Sleep -Milliseconds 300;$process.StandardInput.WriteLine('{"execute":"quit"}');$process.StandardInput.Flush()
   if(-not $process.WaitForExit(5000)){$process.Kill();$process.WaitForExit()}
  }
  [IO.File]::WriteAllText((Join-Path $case 'qmp.log'),$stdout.Result)
  [IO.File]::WriteAllText((Join-Path $case 'qemu-error.log'),$stderr.Result)
  $log=if(Test-Path -LiteralPath $debug){(Read-SharedLog $debug)}else{''}
  if($mode -eq 'policy'){
   foreach($marker in @('DB:SIGNED_ACCEPTED','DB:UNSIGNED_REJECTED','DB:TAMPERED_REJECTED','DB:WRONG_CERT_REJECTED','DB:SHIM_VALIDATION_ENABLED','DB:INSTALLER_POLICY_PASS')){if(-not $log.Contains($marker)){throw "Teste policy sem $marker; consulte $case"}}
   if($process.ExitCode -ne 33){throw "Probe retornou $($process.ExitCode); consulte $case"}
  }else{
   if(-not $passed){throw "Smoke não concluiu relatório/menu; consulte $case"}
   if($process.ExitCode -ne 0){throw "Smoke não encerrou via QMP normalmente; consulte $case"}
   Invoke-FatHelper extract $image $case
   $report=Join-Path $case 'secureboot-status.txt'
   if(-not (Test-Path -LiteralPath $report)){throw 'Relatório smoke ausente após fechar FAT virtual'}
   $lines=[IO.File]::ReadAllLines($report);$fields=@{}
   foreach($line in $lines){if($line -notmatch '^([A-Za-z0-9]+)=(.*)$' -or $fields.ContainsKey($Matches[1])){throw 'Relatório V2 malformado/duplicado'};$fields[$Matches[1]]=$Matches[2]}
   $expected=@{Format='DualBootSecureBootStatusV2';SessionId='11111111111111111111111111111111';CertificateSHA256=$certificateHash;SecureBoot='1';SetupMode='0';LocalCertificateInMokListRT='1';ShimProtocolPresent='1';ShimValidationDisabled='0'}
   if($fields.Count -ne $expected.Count){throw 'Campos inesperados no relatório V2'}
   foreach($key in $expected.Keys){if($fields[$key] -cne $expected[$key]){throw "Relatório $key diverge: $($fields[$key])"}}
  }
  $results+=@{Case=$mode;State='Passed';Directory=$case;ExitCode=$process.ExitCode}
 } finally {if(-not $process.HasExited){$process.Kill();$process.WaitForExit()};$process.Dispose()}
}
$summary=[ordered]@{State=$(if($PrepareOnly){'Prepared'}else{'Passed'});TestedUtc=[DateTime]::UtcNow.ToString('o');PhysicalFirmwareWritten=$false;PhysicalDisksAttached=$false;GuestNetwork=$false;LaboratoryCertificateSHA256=$certificateHash;SessionId='11111111111111111111111111111111';TestedFiles=$testedFiles;Cases=$results}
$summary | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $run 'result.json') -Encoding utf8
$summary | ConvertTo-Json -Depth 6



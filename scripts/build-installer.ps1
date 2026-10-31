[CmdletBinding()]
param([switch]$SkipEfiBuild)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if(-not (Test-Path -LiteralPath $compiler)){throw '.NET Framework 4.8 x64 compiler not found.'}
New-Item -ItemType Directory -Path (Join-Path $root 'build\installer'),(Join-Path $root 'dist') -Force | Out-Null
Push-Location $root
try {
    if(-not $SkipEfiBuild){& (Join-Path $PSScriptRoot 'build.ps1')}
    & (Join-Path $PSScriptRoot 'build-secureboot-payload.ps1') -Test
    & (Join-Path $PSScriptRoot 'setup-secureboot-tools.ps1')
    foreach($efi in @('DualBoot.efi','Diagnostic.efi')){
        $bytes=[IO.File]::ReadAllBytes((Join-Path $root ('build\'+$efi)))
        $offset=[BitConverter]::ToInt32($bytes,60)
        if([BitConverter]::ToUInt16($bytes,$offset+4) -ne 0x8664 -or [BitConverter]::ToUInt16($bytes,$offset+24+68) -ne 10){throw 'Unexpected EFI architecture/subsystem.'}
    }
    $files=[ordered]@{
        'DualBoot.efi'='build\DualBoot.efi';'Diagnostic.efi'='build\Diagnostic.efi'
        'themes/night/theme.ini'='themes\night\theme.ini';'themes/night/background.bmp'='themes\night\background.bmp'
        'themes/night/windows.bmp'='themes\night\windows.bmp';'themes/night/steamos.bmp'='themes\night\steamos.bmp'
    }
    Add-Type -AssemblyName System.IO.Compression
    $payloadPath=Join-Path $root 'build\installer\payload.zip'
    $stream=[IO.File]::Open($payloadPath,[IO.FileMode]::Create)
    $zip=[IO.Compression.ZipArchive]::new($stream,[IO.Compression.ZipArchiveMode]::Create)
    $hashes=@()
    try{foreach($item in $files.GetEnumerator()){
        $inputPath=Join-Path $root $item.Value
        $hashes+=((Get-FileHash -LiteralPath $inputPath -Algorithm SHA256).Hash.ToLowerInvariant()+'  '+$item.Key)
        $entry=$zip.CreateEntry($item.Key,[IO.Compression.CompressionLevel]::Optimal)
        $output=$entry.Open();$inputStream=[IO.File]::OpenRead($inputPath)
        try{$inputStream.CopyTo($output)}finally{$inputStream.Dispose();$output.Dispose()}
    }}finally{$zip.Dispose();$stream.Dispose()}
    $hashes | Set-Content -LiteralPath 'build\installer\payload.hashes' -Encoding ascii
    $sources=@('installer\Core.cs','installer\BootTransaction.cs','installer\FirmwareNative.cs','installer\Deployment.cs','installer\Native.cs','installer\Tests.cs','installer\DeploymentTests.cs','installer\SecureSigning.cs','installer\SteamCollection.cs','installer\SteamBundle.cs','installer\SteamCollectionTests.cs','installer\SecureBootWorkflow.cs','installer\SecureBootDeployment.cs','installer\SecureBootNative.cs','installer\SecureBootTests.cs','installer\SecureInstallerTests.cs')
    $refs=@('/r:System.Web.Extensions.dll','/r:System.IO.Compression.dll','/r:System.Management.dll','/r:System.Security.dll')
    $shim='tools\shim-fedora-16.1-5\usr\lib\efi\shim\16.1-5\EFI\fedora'
    $resources=@('/resource:build\installer\payload.zip,payload.zip','/resource:build\installer\payload.hashes,payload.hashes','/resource:docs\INSTALADOR.md,installer.manual',('/resource:'+$shim+'\shimx64.efi,secure.shim'),('/resource:'+$shim+'\mmx64.efi,secure.mokmanager'),'/resource:build\secureboot-payload\DualBoot.template.efi,secure.menu','/resource:build\secureboot-payload\Diagnostic.template.efi,secure.diagnostic','/resource:scripts\collect-secureboot-steamos.sh,secure.collector','/resource:docs\SECUREBOOT.md,secure.manual','/resource:licenses\shim-COPYRIGHT.txt,secure.license')
    & $compiler /nologo /codepage:65001 /warnaserror /optimize+ /platform:x64 /target:exe /main:DualBoot.Installer.SecureInstallerTests /out:build\installer\tests.exe @refs @resources @sources
    if($LASTEXITCODE -ne 0){throw 'Installer test compilation failed.'}
    & '.\build\installer\tests.exe'
    if($LASTEXITCODE -ne 0){throw 'Installer tests failed.'}
    & (Join-Path $PSScriptRoot 'test-secure-ui.ps1')
    $outputPath='dist\DualBoot-Setup-0.4.0.exe'
    & $compiler /nologo /codepage:65001 /warnaserror /optimize+ /platform:x64 /target:winexe /main:DualBoot.Installer.Program /win32manifest:installer\app.manifest "/out:$outputPath" /r:System.Windows.Forms.dll /r:System.Drawing.dll @refs @resources @sources installer\MainForm.cs installer\SecureBootForm.cs installer\Program.cs
    if($LASTEXITCODE -ne 0){throw 'Installer GUI compilation failed.'}
    $bundle=Join-Path $root 'build\installer\DualBoot-Setup-0.4.0.zip'
    Compress-Archive -LiteralPath $outputPath,'docs\INSTALADOR.md','docs\SECUREBOOT.md','licenses','assets','LICENSE','THIRD_PARTY.md' -DestinationPath $bundle -Force
    Copy-Item -LiteralPath $bundle -Destination 'dist\DualBoot-Setup-0.4.0.zip' -Force
    $extraRoot=Join-Path $root 'extras\steamos\display-choice'
    $extraHashes=[ordered]@{}
    foreach($name in @('telas.py','gamescope-shim','instalar.sh','LEIA-ME.md')){$extraHashes[$name]=(Get-FileHash -LiteralPath (Join-Path $extraRoot $name)).Hash.ToLowerInvariant()}
    $extraManifest=[ordered]@{version=1;session_sha256='e1499b725efd8afcf5693b5dc240d7311d2bbc389b92df1bb9eea8dd5688d23e';sha256=$extraHashes}
    [IO.File]::WriteAllText((Join-Path $extraRoot 'pacote.json'),(($extraManifest | ConvertTo-Json -Depth 3).Replace("`r`n","`n")+"`n"),[Text.UTF8Encoding]::new($false))
    $extraFiles=@('README.md','collect-display-routing.sh','collect-startup-animation.sh','measure-steamos-startup.sh','display-choice/telas.py','display-choice/gamescope-shim','display-choice/instalar.sh','display-choice/LEIA-ME.md','display-choice/pacote.json')
    $extraStream=[IO.File]::Open((Join-Path $root 'dist\DualBoot-SteamOS-Tools-0.4.0.zip'),[IO.FileMode]::Create)
    $extraZip=[IO.Compression.ZipArchive]::new($extraStream,[IO.Compression.ZipArchiveMode]::Create)
    try{foreach($name in $extraFiles){
        $entry=$extraZip.CreateEntry(('steamos/'+$name),[IO.Compression.CompressionLevel]::Optimal)
        $output=$entry.Open();$inputStream=[IO.File]::OpenRead((Join-Path $root ('extras/steamos/'+$name)))
        try{$inputStream.CopyTo($output)}finally{$inputStream.Dispose();$output.Dispose()}
    }}finally{$extraZip.Dispose();$extraStream.Dispose()}
    Get-Item -LiteralPath 'dist\DualBoot-Setup-0.4.0.exe','dist\DualBoot-Setup-0.4.0.zip','dist\DualBoot-SteamOS-Tools-0.4.0.zip' | ForEach-Object {
        (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()+'  '+$_.Name
    } | Set-Content -LiteralPath 'dist\SHA256SUMS-0.4.0.txt' -Encoding ascii
    Get-Item -LiteralPath $outputPath | Select-Object FullName,Length
}finally{Pop-Location}

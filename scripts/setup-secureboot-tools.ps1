# Downloads/extracts public Fedora shim binaries for development, never installs them.
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$directory=Join-Path $root 'tools\shim-fedora-16.1-5'
$source=Join-Path $directory 'usr\lib\efi\shim\16.1-5\EFI\fedora'
$expected=@{'shimx64.efi'='571EA56B855DCF73BEC6ACB63C5DED44C2A191138BCA0D8CFA5AA93F60F46FFF';'mmx64.efi'='F8AF592759C8AB33B69C4B0E772DA5A8E2AA6D09C7DBD5E24C62C89FA5FDBD05'}
if(-not (Test-Path -LiteralPath (Join-Path $source 'shimx64.efi'))){
 $archive=Join-Path $root 'tools\downloads\shim-x64-16.1-5.x86_64.rpm'
 [IO.Directory]::CreateDirectory((Split-Path -Parent $archive)) | Out-Null
 if(-not (Test-Path -LiteralPath $archive)){Invoke-WebRequest -Uri 'https://mirrors.kernel.org/fedora/releases/44/Everything/x86_64/os/Packages/s/shim-x64-16.1-5.x86_64.rpm' -OutFile $archive}
 if((Get-FileHash -LiteralPath $archive).Hash -ne 'A1BBABACA8E4398B2483C678240F4BE4803E91390B512A7B618DA3BC88E49917'){throw 'Fedora RPM hash mismatch'}
 $seven=Get-Command 7z.exe -ErrorAction SilentlyContinue
 $exe=if($seven){$seven.Source}else{Join-Path $env:ProgramFiles '7-Zip\7z.exe'}
 if(-not (Test-Path -LiteralPath $exe)){throw 'Install 7-Zip or place 7z.exe in PATH to extract the pinned public RPM.'}
 $work=Join-Path $root ('build\shim-extract-'+[guid]::NewGuid().ToString('N'));[IO.Directory]::CreateDirectory($work) | Out-Null
 & $exe x $archive ('-o'+$work) -y | Out-Null
 if($LASTEXITCODE -ne 0){throw 'RPM extraction failed'}
 foreach($file in @(Get-ChildItem -LiteralPath $work -File)){& $exe x $file.FullName ('-o'+$directory) -y | Out-Null;if($LASTEXITCODE -ne 0){throw 'CPIO extraction failed'}}
}
foreach($name in $expected.Keys){if((Get-FileHash -LiteralPath (Join-Path $source $name)).Hash -ne $expected[$name]){throw ('Shim payload hash mismatch: '+$name)}}
Write-Output 'Pinned public shim/MokManager verified; no ESP or firmware writes.'

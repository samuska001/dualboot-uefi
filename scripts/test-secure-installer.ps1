$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
Push-Location $root
try{
 New-Item -ItemType Directory build\installer -Force | Out-Null
 $compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
 $sources=@('installer\Core.cs','installer\BootTransaction.cs','installer\FirmwareNative.cs','installer\Deployment.cs','installer\Native.cs','installer\Tests.cs','installer\DeploymentTests.cs','installer\SecureSigning.cs','installer\SteamCollection.cs','installer\SteamBundle.cs','installer\SteamCollectionTests.cs','installer\SecureBootWorkflow.cs','installer\SecureBootDeployment.cs','installer\SecureBootNative.cs','installer\SecureBootTests.cs','installer\SecureInstallerTests.cs')
 & $compiler /nologo /warnaserror /target:exe /main:DualBoot.Installer.SecureInstallerTests /out:build\installer\secure-installer-tests.exe /r:System.Security.dll /r:System.Web.Extensions.dll /r:System.IO.Compression.dll /r:System.Management.dll @sources
 if($LASTEXITCODE -ne 0){throw 'Secure installer tests compilation failed'}
 & .\build\installer\secure-installer-tests.exe
 if($LASTEXITCODE -ne 0){throw 'Secure installer tests failed'}
}finally{Pop-Location}

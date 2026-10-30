# Disposable LAB identity only; no root trust, ESP or firmware changes.
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$testId='DualBootSigningLab-'+[Guid]::NewGuid().ToString('N')
$temp=Join-Path ([IO.Path]::GetTempPath()) $testId
$output=Join-Path $root 'build\secureboot-lab'
New-Item -ItemType Directory -Path $temp,$output -Force | Out-Null
$baseline=@{};Get-ChildItem Cert:\CurrentUser\My | ForEach-Object {$baseline[$_.Thumbprint]=$true}
$started=Get-Date
$runner=@'
using System;
using System.IO;
using System.Text;
using System.Security.Cryptography;
using DualBoot.Installer;
public static class SigningLab {
 static byte[] Patch(byte[] image,SigningIdentity id){
  byte[] der=Convert.FromBase64String(id.CertificateBase64);int pe=BitConverter.ToInt32(image,60),table=pe+24+BitConverter.ToUInt16(image,pe+20),count=BitConverter.ToUInt16(image,pe+6),found=0;
  for(int i=0;i<count;i++){int h=table+40*i;if(Encoding.ASCII.GetString(image,h,8).TrimEnd('\0')!=".dbbind")continue;found++;int raw=BitConverter.ToInt32(image,h+20);if(BitConverter.ToInt32(image,h+16)<4228||raw<0||raw>image.Length-4228||der.Length>4096)throw new Exception("Bad binding bounds");
   if(Encoding.ASCII.GetString(image,raw,32)!="DUALBOOT_SB_BINDING_V2_20261030!")throw new Exception("Bad binding marker");for(int j=32;j<4228;j++)if(image[raw+j]!=0)throw new Exception("Template already bound");
   Buffer.BlockCopy(BitConverter.GetBytes(der.Length),0,image,raw+32,4);Buffer.BlockCopy(Encoding.ASCII.GetBytes(new string('1',32)),0,image,raw+36,32);Buffer.BlockCopy(Encoding.ASCII.GetBytes(id.CertificateSha256),0,image,raw+68,64);Buffer.BlockCopy(der,0,image,raw+132,der.Length);
  }if(found!=1)throw new Exception("Expected one binding");return image;
 }
 public static int Main(string[] a){try{
  string temp=a[0],root=a[1],output=a[2];SigningIdentity id=LocalSigner.CreateOrOpen(temp);File.WriteAllText(Path.Combine(temp,"lab-thumbprint.txt"),id.Thumbprint);
  SigningIdentity again=LocalSigner.CreateOrOpen(temp);if(again.Thumbprint!=id.Thumbprint||again.CertificateBase64!=id.CertificateBase64||again.CertificateSha256!=id.CertificateSha256||again.Store!=id.Store)throw new Exception("Identity changed on reopen");
  File.WriteAllBytes(Path.Combine(output,"lab.cer"),Convert.FromBase64String(id.CertificateBase64));string[] variants={"DualBoot","Smoke","Diagnostic"},names={"menu","smoke","diag"};
  for(int i=0;i<variants.Length;i++){string source=Path.Combine(temp,"unsigned ' "+names[i]+".efi");File.WriteAllBytes(source,Patch(File.ReadAllBytes(Path.Combine(root,"build/secureboot-payload/"+variants[i]+".template.efi")),id));string target=Path.Combine(output,"lab-signed-"+names[i]+".efi");LocalSigner.Sign(source,target,id);LocalSigner.Verify(target,id.CertificateBase64);}
  string probe=Path.Combine(root,"build/secureboot-lab/lab-probe-unsigned.efi");LocalSigner.Sign(probe,Path.Combine(output,"lab-signed-probe.efi"),id);LocalSigner.Verify(Path.Combine(output,"lab-signed-probe.efi"),id.CertificateBase64);
  SigningIdentity wrong=LocalSigner.CreateOrOpen(Path.Combine(temp,"wrong-identity"));File.WriteAllText(Path.Combine(temp,"wrong-thumbprint.txt"),wrong.Thumbprint);File.WriteAllBytes(Path.Combine(output,"wrong-lab.cer"),Convert.FromBase64String(wrong.CertificateBase64));
  LocalSigner.Sign(probe,Path.Combine(output,"wrong-cert.efi"),wrong);LocalSigner.Verify(Path.Combine(output,"wrong-cert.efi"),wrong.CertificateBase64);bool wrongRejected=false;try{LocalSigner.Verify(Path.Combine(output,"wrong-cert.efi"),id.CertificateBase64);}catch{wrongRejected=true;}if(!wrongRejected)throw new Exception("Wrong signer accepted");
  byte[] bad=File.ReadAllBytes(Path.Combine(output,"lab-signed-menu.efi"));int pe=BitConverter.ToInt32(bad,60),table=pe+24+BitConverter.ToUInt16(bad,pe+20);bad[BitConverter.ToInt32(bad,table+20)]^=1;string tamper=Path.Combine(temp,"tampered.efi");File.WriteAllBytes(tamper,bad);bool rejected=false;try{LocalSigner.Verify(tamper,id.CertificateBase64);}catch{rejected=true;}if(!rejected)throw new Exception("Tampered PE accepted");
  Console.WriteLine("PASS LAB: create nonexportable CNG, reopen exact identity, sign three bound templates plus probe, reject wrong signer and tampering");return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
'@
$runnerPath=Join-Path $temp 'SigningLab.cs';$exe=Join-Path $temp 'SigningLab.exe'
Set-Content -LiteralPath $runnerPath -Value $runner
$failure=$null;$removed=0;$thumb=$null;$wrongThumb=$null
$staged=Join-Path $temp 'public';New-Item -ItemType Directory $staged | Out-Null
try {
 if(-not (Test-Path (Join-Path $output 'lab-probe-unsigned.efi'))){throw 'Compile the unsigned LAB probe before running lifecycle'}
 & "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /r:System.Security.dll "/out:$exe" (Join-Path $root 'installer\SecureSigning.cs') $runnerPath
 if($LASTEXITCODE -ne 0){throw 'LAB compilation failed'}
 & $exe $temp $root $staged
 if($LASTEXITCODE -ne 0){throw 'LAB lifecycle failed; see runner error'}
 foreach($name in @('lab-signed-menu.efi','lab-signed-smoke.efi','lab-signed-diag.efi','lab-signed-probe.efi','wrong-cert.efi','lab.cer','wrong-lab.cer')){Copy-Item -LiteralPath (Join-Path $staged $name) -Destination (Join-Path $output $name) -Force}
} catch {$failure=$_} finally {
 $candidates=@(Get-ChildItem Cert:\CurrentUser\My | Where-Object {-not $baseline.ContainsKey($_.Thumbprint) -and $_.Subject -match '^CN=DualBoot Local [0-9a-f]{32}$' -and $_.NotBefore -ge $started.AddMinutes(-11) -and @($_.Extensions | Where-Object {$_.Oid.Value -eq '1.3.6.1.4.1.57264.2.1'}).Count -eq 1})
 $known=@();$marker=Join-Path $temp 'lab-thumbprint.txt';$wrongMarker=Join-Path $temp 'wrong-thumbprint.txt'
 if(Test-Path $marker){$thumb=(Get-Content $marker -Raw).Trim();$known+=$thumb}
 if(Test-Path $wrongMarker){$wrongThumb=(Get-Content $wrongMarker -Raw).Trim();$known+=$wrongThumb}
 if(@($candidates | Where-Object {$known -notcontains $_.Thumbprint}).Count -gt 0){throw 'Unclaimed identity preserved: cleanup requires an exact LAB thumbprint marker; no certificates removed'}
 foreach($labCert in $candidates){$current=$labCert.Thumbprint;$labCert.FriendlyName=$testId; $rsa=[Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPrivateKey($labCert);$keyName=$rsa.Key.KeyName;$provider=$rsa.Key.Provider;$rsa.Dispose();Remove-Item -LiteralPath ("Cert:\CurrentUser\My\"+$current) -DeleteKey -Force;if((Test-Path ("Cert:\CurrentUser\My\"+$current)) -or [Security.Cryptography.CngKey]::Exists($keyName,$provider)){throw 'LAB certificate/key cleanup failed'};$removed++;Write-Output ('PASS LAB cleanup: exact disposable certificate and CNG key absent: '+$current)}
 $resolved=[IO.Path]::GetFullPath($temp)
 if([IO.Path]::GetFileName($resolved) -cne $testId -or -not $resolved.StartsWith([IO.Path]::GetTempPath(),[StringComparison]::OrdinalIgnoreCase)){throw 'Invalid LAB cleanup directory'}
 Remove-Item -LiteralPath $resolved -Recurse -Force
}
if($failure){throw $failure}
if($removed -ne 2){throw 'Both LAB identity cleanups were not confirmed'}
@{LabOnly=$true;CertificateRemovedAfterTest=$true;WrongCertificateRemovedAfterTest=$true;TestId=$testId;SessionId=('1'*32);Thumbprint=$thumb;WrongThumbprint=$wrongThumb;CreatedUtc=[DateTime]::UtcNow.ToString('o')} | ConvertTo-Json | Set-Content (Join-Path $output 'lab-metadata.json')


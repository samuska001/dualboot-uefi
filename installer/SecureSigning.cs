using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml.Serialization;
namespace DualBoot.Installer {
 public sealed class SigningIdentity {
  public string Thumbprint, CertificateBase64, CertificateSha256, Store;
 }
 public static class LocalSigner {
  const string OwnerOid="1.3.6.1.4.1.57264.2.1";
  public static SigningIdentity CreateOrOpen(string sessionRoot) {
   string root=Path.GetFullPath(sessionRoot); Directory.CreateDirectory(root);
   if((File.GetAttributes(root)&FileAttributes.ReparsePoint)!=0) throw new IOException("Session directory cannot be a link");
   string path=Path.Combine(root,"signing-identity.xml");
   // Never silently replace a journal identity or search by a friendly name.
   if(File.Exists(path)) { SigningIdentity old; using(var f=File.OpenRead(path)) old=(SigningIdentity)new XmlSerializer(typeof(SigningIdentity)).Deserialize(f); ValidateIdentity(old); return old; }
   string token=Guid.NewGuid().ToString("N");
   string script="$c=New-SelfSignedCertificate -Type CodeSigningCert -Subject 'CN=DualBoot Local "+token+"' -FriendlyName 'DualBoot local signing' -CertStoreLocation 'Cert:\\CurrentUser\\My' -Provider 'Microsoft Software Key Storage Provider' -KeyAlgorithm RSA -KeyLength 3072 -HashAlgorithm SHA256 -KeyExportPolicy NonExportable -KeyUsage DigitalSignature -NotAfter (Get-Date).AddYears(10) -TextExtension @('"+OwnerOid+"={hex}0c20"+Hex(Encoding.ASCII.GetBytes(token))+"'); [Convert]::ToBase64String($c.RawData)";
   string encoded=Run(script).Trim(); var cert=new X509Certificate2(Convert.FromBase64String(encoded));
   var id=new SigningIdentity {Thumbprint=cert.Thumbprint,CertificateBase64=encoded,CertificateSha256=Hex(SHA256.Create().ComputeHash(cert.RawData)),Store="CurrentUser\\My"};
   ValidateIdentity(id);
   using(var f=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None)) {new XmlSerializer(typeof(SigningIdentity)).Serialize(f,id); f.Flush(true);} return id;
  }
  static void ValidateIdentity(SigningIdentity id) {
   if(id==null||id.Store!="CurrentUser\\My") throw new InvalidDataException("Invalid signing store");
   var expected=new X509Certificate2(Convert.FromBase64String(id.CertificateBase64));
   if(expected.Thumbprint!=id.Thumbprint||Hex(SHA256.Create().ComputeHash(expected.RawData))!=id.CertificateSha256) throw new CryptographicException("Identity certificate mismatch");
   using(var store=new X509Store(StoreName.My,StoreLocation.CurrentUser)) {store.Open(OpenFlags.ReadOnly); var found=store.Certificates.Find(X509FindType.FindByThumbprint,id.Thumbprint,false); if(found.Count!=1) throw new CryptographicException("Local identity missing");
    var cert=found[0]; bool owned=false;foreach(X509Extension ex in cert.Extensions)if(ex.Oid.Value==OwnerOid)owned=true;
    if(!owned||!Equal(cert.RawData,expected.RawData)||!cert.HasPrivateKey||DateTime.Now<cert.NotBefore||DateTime.Now>cert.NotAfter) throw new CryptographicException("Invalid local identity");
    using(RSA rsa=cert.GetRSAPrivateKey()) {var cng=rsa as RSACng; if(cng==null||cng.Key.IsEphemeral||cng.Key.ExportPolicy!=CngExportPolicies.None||cng.Key.Provider.Provider!="Microsoft Software Key Storage Provider")throw new CryptographicException("A persisted non-exportable CNG key is required");}
   }
  }
  public static void Sign(string input,string output,SigningIdentity identity) {
   ValidateIdentity(identity); string src=Path.GetFullPath(input), dst=Path.GetFullPath(output);
   if(String.Equals(src,dst,StringComparison.OrdinalIgnoreCase)||File.Exists(dst)) throw new IOException("Signing requires a new output copy");
   File.Copy(src,dst,false);
   try {Run("$c=Get-Item -LiteralPath "+Quote("Cert:\\CurrentUser\\My\\"+identity.Thumbprint)+"; $s=Set-AuthenticodeSignature -LiteralPath "+Quote(dst)+" -Certificate $c -HashAlgorithm SHA256; if($null -eq $s.SignerCertificate){throw 'Signing failed'}"); Verify(dst,identity.CertificateBase64);}
   catch {File.Delete(dst);throw;}
  }
  static string Quote(string s){return "'"+s.Replace("'","''")+"'";}
  static string Run(string script) {
   var start=new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"System32\\WindowsPowerShell\\v1.0\\powershell.exe"),"-NoProfile -NonInteractive -EncodedCommand "+Convert.ToBase64String(Encoding.Unicode.GetBytes("$ErrorActionPreference='Stop';"+script)));
   start.UseShellExecute=false;start.CreateNoWindow=true;start.RedirectStandardOutput=true;start.RedirectStandardError=true;
   using(var p=Process.Start(start)){var err=p.StandardError.ReadToEndAsync();var stdout=p.StandardOutput.ReadToEndAsync();if(!p.WaitForExit(120000)){p.Kill();throw new TimeoutException("Signing command timed out");}string result=stdout.Result; if(p.ExitCode!=0)throw new CryptographicException("Windows signing failed: "+err.Result);return result;}
  }
  public static void Verify(string path,string certificateBase64) {
   var expected=new X509Certificate2(Convert.FromBase64String(certificateBase64));
   byte[] a;using(var f=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read)){if(f.Length>1024L*1024*1024||f.Length<128)throw new InvalidDataException("PE size invalid");a=new byte[(int)f.Length];int n=0;while(n<a.Length){int r=f.Read(a,n,a.Length-n);if(r==0)throw new EndOfStreamException();n+=r;}}
   if(U16(a,0)!=0x5a4d)throw new InvalidDataException("Missing MZ");int pe=I32(a,60);if(U32(a,pe)!=0x4550)throw new InvalidDataException("Missing PE");
   int op=checked(pe+24), optSize=U16(a,pe+20), magic=U16(a,op), dd=magic==0x20b?112:magic==0x10b?96:0;
   if(dd==0||optSize<dd+40||U32(a,op+dd-4)<5)throw new InvalidDataException("Unsupported PE optional header");
   int check=checked(op+64), secdir=checked(op+dd+32), certOff=I32(a,secdir), certSize=I32(a,secdir+4), headers=I32(a,op+60);
   if(certOff<=0||certSize<8||(certOff&7)!=0||checked((long)certOff+certSize)!=a.Length||headers<op+optSize||headers>certOff)throw new InvalidDataException("Invalid PE certificate layout");
   int length=I32(a,certOff);if(length<8||length>certSize||((length+7)&~7)!=certSize||U16(a,certOff+4)!=0x200||U16(a,certOff+6)!=2)throw new InvalidDataException("Unsupported WIN_CERTIFICATE");
   for(int i=certOff+length;i<a.Length;i++)if(a[i]!=0)throw new InvalidDataException("Nonzero certificate padding");
   byte[] cmsBytes=new byte[length-8];Buffer.BlockCopy(a,certOff+8,cmsBytes,0,cmsBytes.Length);int cmsEnd=0;Der(cmsBytes,ref cmsEnd,0x30);for(int i=cmsEnd;i<cmsBytes.Length;i++)if(cmsBytes[i]!=0)throw new InvalidDataException("Trailing CMS data");var cms=new SignedCms();cms.Decode(cmsBytes);
   if(cms.ContentInfo.ContentType.Value!="1.3.6.1.4.1.311.2.1.4"||cms.SignerInfos.Count!=1)throw new CryptographicException("Expected one Authenticode signer");
   cms.CheckSignature(true);var signer=cms.SignerInfos[0];if(signer.Certificate==null||!Equal(signer.Certificate.RawData,expected.RawData))throw new CryptographicException("Unexpected signing certificate");
   byte[] digest=ReadSha256Digest(cms.ContentInfo.Content);
   var sections=new List<int[]>();int count=U16(a,pe+6);if(count<1||count>96||checked((long)op+optSize+count*40)>headers)throw new InvalidDataException("Invalid section table");
   for(int i=0;i<count;i++){int s=op+optSize+i*40;int size=I32(a,s+16),offset=I32(a,s+20);if(size==0)continue;if(offset<headers||size<0||(long)offset+size>certOff)throw new InvalidDataException("Section bounds");sections.Add(new int[]{offset,size});}
   sections.Sort(delegate(int[] x,int[] y){return x[0].CompareTo(y[0]);});
   using(var h=SHA256.Create()) {
    Hash(h,a,0,check);Hash(h,a,check+4,secdir-check-4);Hash(h,a,secdir+8,headers-secdir-8);
    int end=headers;foreach(var s in sections){if(s[0]!=end)throw new InvalidDataException("Noncontiguous PE sections unsupported");Hash(h,a,s[0],s[1]);end=checked(end+s[1]);}
    Hash(h,a,end,certOff-end);h.TransformFinalBlock(new byte[0],0,0);if(!Equal(h.Hash,digest))throw new CryptographicException("Authenticode PE digest mismatch");
   }
  }
  // Minimal strict DER reader: SpcIndirectDataContent ::= SEQUENCE { data, DigestInfo }.
  static byte[] ReadSha256Digest(byte[] b){int p=0;byte[] outer=Der(b,ref p,0x30);if(p!=b.Length)throw new InvalidDataException("Trailing indirect data");p=0;Der(outer,ref p,0x30);byte[] info=Der(outer,ref p,0x30);if(p!=outer.Length)throw new InvalidDataException("Trailing digest info");p=0;byte[] alg=Der(info,ref p,0x30);byte[] digest=Der(info,ref p,4);if(p!=info.Length||digest.Length!=32)throw new InvalidDataException("Invalid SHA256 digest");int q=0;byte[] oid=Der(alg,ref q,6);byte[] sha={0x60,0x86,0x48,1,0x65,3,4,2,1};if(!Equal(oid,sha))throw new CryptographicException("Only SHA256 PE digests supported");if(q<alg.Length){byte[] nil=Der(alg,ref q,5);if(nil.Length!=0)throw new InvalidDataException();}if(q!=alg.Length)throw new InvalidDataException();return digest;}
  static byte[] Der(byte[] b,ref int p,int tag){if(p>=b.Length||b[p++]!=tag||p>=b.Length)throw new InvalidDataException("Invalid DER");int n=b[p++];if((n&128)!=0){int count=n&127;if(count==0||count>4||p+count>b.Length||b[p]==0)throw new InvalidDataException("Invalid DER length");n=0;for(int i=0;i<count;i++)n=checked(n*256+b[p++]);if(n<128)throw new InvalidDataException("Noncanonical DER");}if(n<0||(long)p+n>b.Length)throw new InvalidDataException("DER bounds");byte[] r=new byte[n];Buffer.BlockCopy(b,p,r,0,n);p+=n;return r;}
  static void Hash(HashAlgorithm h,byte[] a,int offset,int count){if(offset<0||count<0||(long)offset+count>a.Length)throw new InvalidDataException("PE hash bounds");h.TransformBlock(a,offset,count,null,0);}
  static int U16(byte[] a,int o){if(o<0||o>a.Length-2)throw new InvalidDataException("PE bounds");return BitConverter.ToUInt16(a,o);}
  static uint U32(byte[] a,int o){if(o<0||o>a.Length-4)throw new InvalidDataException("PE bounds");return BitConverter.ToUInt32(a,o);}
  static int I32(byte[] a,int o){uint n=U32(a,o);if(n>Int32.MaxValue)throw new InvalidDataException("PE value too large");return (int)n;}
  static bool Equal(byte[] a,byte[] b){if(a.Length!=b.Length)return false;int d=0;for(int i=0;i<a.Length;i++)d|=a[i]^b[i];return d==0;}
  static string Hex(byte[] b){return BitConverter.ToString(b).Replace("-","").ToLowerInvariant();}
 }
}



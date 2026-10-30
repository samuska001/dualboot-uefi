using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
namespace DualBoot.Installer {
public static class SecureSigningTests {
 public static int Main(string[] args) {
  try {
   if(args.Length!=1) throw new Exception("Provide a read-only signed PE fixture");
   string p=args[0]; var cert=X509Certificate.CreateFromSignedFile(p); string b=Convert.ToBase64String(cert.Export(X509ContentType.Cert));
   LocalSigner.Verify(p,b);
   string t=Path.GetTempFileName(); try {byte[] a=File.ReadAllBytes(p); int pe=BitConverter.ToInt32(a,60); int op=pe+24; int sect=op+BitConverter.ToUInt16(a,pe+20); int raw=(int)BitConverter.ToUInt32(a,sect+20); a[raw]^=1; File.WriteAllBytes(t,a); Reject(delegate{LocalSigner.Verify(t,b);},"tampered code");
   byte[] brokenCms=File.ReadAllBytes(p); int optional=BitConverter.ToInt32(brokenCms,60)+24; int directory=optional+(BitConverter.ToUInt16(brokenCms,optional)==0x20b?112:96)+32; int certificateOffset=BitConverter.ToInt32(brokenCms,directory); int certificateLength=BitConverter.ToInt32(brokenCms,certificateOffset); brokenCms[certificateOffset+certificateLength-1]^=1; File.WriteAllBytes(t,brokenCms); Reject(delegate{LocalSigner.Verify(t,b);},"invalid CMS trailing data"); brokenCms=File.ReadAllBytes(p);brokenCms[certificateOffset+certificateLength-64]^=1;File.WriteAllBytes(t,brokenCms);Reject(delegate{LocalSigner.Verify(t,b);},"invalid CMS cryptography");
   File.WriteAllBytes(t,new byte[64]); Reject(delegate{LocalSigner.Verify(t,b);},"bad PE");
   Reject(delegate{LocalSigner.Verify(p,Convert.ToBase64String(new byte[20]));},"bad certificate");
   byte[] changed=cert.Export(X509ContentType.Cert);changed[changed.Length-1]^=1;Reject(delegate{LocalSigner.Verify(p,Convert.ToBase64String(changed));},"different certificate");
   } finally {File.Delete(t);}
   Console.WriteLine("PASS secure signing: valid fixture, tampered PE, malformed PE, invalid and mismatched certificate");return 0;
  } catch(Exception e){Console.Error.WriteLine(e);return 1;}
 }
 static void Reject(Action a,string label){try{a();}catch{return;}throw new Exception("Accepted "+label);}
}
}



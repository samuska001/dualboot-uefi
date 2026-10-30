using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using DualBoot.Setup;
namespace DualBoot.Installer {
 public static class SecureBootTests {
  static int count;
  static void Check(bool ok,string label){if(!ok)throw new Exception("FAIL secure workflow: "+label);count++;}
  static void Reject(Action action,string label){bool rejected=false;try{action();}catch{rejected=true;}Check(rejected,label);}
  static SecureBootSession Session(){return new SecureBootSession{Id="12345678123442348234123456789abc",CreatedUtc=DateTime.UtcNow.ToString("o"),PreparedUtc=DateTime.UtcNow.AddMinutes(-1).ToString("o"),State="AwaitingUsbTest",Identity=new SigningIdentity{CertificateSha256=new string('a',64),CertificateBase64=Convert.ToBase64String(new byte[]{1,2,3}),Thumbprint=new string('b',40)},Files=new List<SecureFileRecord>{new SecureFileRecord{Path="grubx64.efi",Hash=new string('c',64),Bytes=64}}};}
  static string Report(SecureBootSession s){return "Format=DualBootSecureBootStatusV2\nSessionId="+s.Id+"\nCertificateSHA256="+s.Identity.CertificateSha256+"\nSecureBoot=1\nSetupMode=0\nLocalCertificateInMokListRT=1\nShimProtocolPresent=1\nShimValidationDisabled=0\n";}
  public static int Run(){
   var s=Session();SecureBootWorkflow.AcceptFirmwareReport(s,Report(s),DateTime.UtcNow);Check(s.FirmwareTestVerified,"current session-bound report accepted");
   var stale=Session();Reject(delegate{SecureBootWorkflow.AcceptFirmwareReport(stale,Report(stale),DateTime.UtcNow.AddDays(-1));},"old report timestamp rejected");
   Reject(delegate{SecureBootWorkflow.AcceptFirmwareReport(s,Report(s).Replace(s.Id,new string('0',32)),DateTime.UtcNow);},"other session rejected");
   Reject(delegate{SecureBootWorkflow.AcceptFirmwareReport(s,Report(s)+"SecureBoot=1\n",DateTime.UtcNow);},"duplicate field rejected");
   Reject(delegate{SecureBootWorkflow.AcceptFirmwareReport(s,Report(s).Replace("ShimValidationDisabled=0","ShimValidationDisabled=1"),DateTime.UtcNow);},"disabled shim validation rejected");
   Reject(delegate{SecureBootWorkflow.AcceptFirmwareReport(s,Report(s).Replace("SecureBoot=1","SecureBoot=0"),DateTime.UtcNow);},"Secure Boot off rejected");
   Reject(delegate{SecureBootWorkflow.AcceptFirmwareReport(s,Report(s).Replace("LocalCertificateInMokListRT=1","LocalCertificateInMokListRT=0"),DateTime.UtcNow);},"missing MOK rejected");
   Check(!SecureBootWorkflow.Ready(s),"menu report alone never means Windows tested");s.WindowsUserConfirmed=true;Check(SecureBootWorkflow.Ready(s),"Windows-only preparation has explicit scope");
   s.State="NeedsRecovery";SecureBootWorkflow.ReconcileState(s,false,false);Check(SecureBootWorkflow.Ready(s)&&s.State=="ReadyToInstall","resolved recovery accepts complete evidence without a second validation");SecureBootWorkflow.ReconcileState(s,true,true);Check(!SecureBootWorkflow.Ready(s)&&s.State=="NeedsRecovery","unfinished transaction takes precedence over installed transaction");SecureBootWorkflow.ReconcileState(s,false,false);
   s.IncludesSteam=true;s.RootPartitionGuid="12345678-1234-4234-8234-123456789abc";Check(!SecureBootWorkflow.Ready(s),"Steam requires Linux report too");
   string linux="Format=DualBootLinuxStatusV1\nSessionId="+s.Id+"\nRootPartitionGuid="+s.RootPartitionGuid+"\nSecureBoot=1\nLockdownIntegrity=1\nModuleSignaturesEnforced=1\nBootedSignedBundle=1\n";
   SecureBootWorkflow.AcceptLinuxReport(s,linux,DateTime.UtcNow);Check(SecureBootWorkflow.Ready(s),"matching Linux enforcement report accepted");
   Reject(delegate{SecureBootWorkflow.AcceptLinuxReport(s,linux.Replace("ModuleSignaturesEnforced=1","ModuleSignaturesEnforced=0"),DateTime.UtcNow);},"unenforced modules rejected");
   Reject(delegate{SecureBootWorkflow.AcceptLinuxReport(s,linux.Replace(s.RootPartitionGuid,"87654321-4321-4321-8321-cba987654321"),DateTime.UtcNow);},"different root partition rejected");
   var firmware=new FakeFirmware();var winGuid=new Guid("12345678-1234-4234-8234-123456789abc");firmware.Values["BootOrder"]=new Variable(7,new byte[]{1,0});firmware.Values["Boot0001"]=new Variable(7,InstallerTests.Option(winGuid,@"\EFI\Microsoft\Boot\bootmgfw.efi","Windows"));
   var win=FirmwareEntry.Parse("Boot0001",firmware.Read("Boot0001"));var state=new ComputerState{SecureBoot=true,EncryptionClear=true,Order=firmware.Read("BootOrder")};state.Entries.Add(win);
   var temp=Path.Combine(Path.GetTempPath(),"DualBoot-secure-tests-"+Guid.NewGuid().ToString("N"));var volume=Path.Combine(temp,"volume");Directory.CreateDirectory(volume);
   state.Partitions.Add(new PartitionInfo{Guid=winGuid,Root=volume,IsEsp=true,IsSystem=true,FileSystem="FAT32",FreeBytes=1000000000});var request=new InstallRequest{Windows=win};
   var session=Session();session.Windows=win;session.WindowsPartitionGuid=winGuid.ToString();session.FirmwareTestVerified=true;session.WindowsUserConfirmed=true;
   var payload=new Dictionary<string,byte[]>(StringComparer.OrdinalIgnoreCase);foreach(var name in SecureBootWorkflow.RequiredNames(false))payload.Add(name,Encoding.ASCII.GetBytes("SIGNED TEST "+name));
   session.Files=SecureBootWorkflow.Records(payload);
   var engine=new SecureInstallerEngine(firmware,Path.Combine(temp,"backups"),delegate(SecureBootSession a,Dictionary<string,byte[]> b){SecureBootWorkflow.MatchRecords(a,b);});
   engine.JournalPrepared=delegate(SecureDeploymentJournal j){throw new IOException("injected session link failure");};Reject(delegate{engine.Install(state,request,session,payload,false,null);},"session metadata failure occurs before any EFI mutation");Check(!Directory.Exists(Path.Combine(volume,"EFI")),"metadata failure leaves volume untouched");
   string link=Path.Combine(temp,"session-link.txt");engine.JournalPrepared=delegate(SecureDeploymentJournal j){File.WriteAllText(link,j.Id);};
   engine.AfterFile=delegate(string n){Check(File.Exists(link)&&engine.Load(File.ReadAllText(link)).SessionId==session.Id,"durable session link precedes EFI files");throw new IOException("injected copy failure");};Reject(delegate{engine.Install(state,request,session,payload,false,null);},"copy failure is rolled back");
   Check(!Directory.Exists(Path.Combine(volume,@"EFI\DualBoot")),"no partial promoted installation");Check(Transaction.Equal(firmware.Read("BootOrder"),state.Order),"original boot order preserved on failure");
   engine.AfterFile=null;var journal=engine.Install(state,request,session,payload,false,null);Check(journal.State=="Installed","signed SSD transaction committed");
   File.WriteAllText(Path.Combine(volume,@"EFI\DualBoot\foreign.txt"),"KEEP");Reject(delegate{engine.Restore(journal,volume);},"foreign content blocks restoration before firmware rollback");
   Check(Transaction.Equal(firmware.Read("BootOrder"),journal.FirmwarePlan.AfterOrder),"foreign file refusal preserves active boot entry");
   File.Delete(Path.Combine(volume,@"EFI\DualBoot\foreign.txt"));engine.Restore(journal,volume);Check(!Directory.Exists(Path.Combine(volume,@"EFI\DualBoot")),"owned files removed after verified restoration");Check(Transaction.Equal(firmware.Read("BootOrder"),state.Order),"original boot order restored");
   state.SecureBoot=null;Reject(delegate{engine.Install(state,request,session,payload,false,null);},"unknown Secure Boot never installs");
   state.SecureBoot=true;state.EncryptionClear=null;Reject(delegate{engine.Install(state,request,session,payload,false,null);},"unknown encryption never installs SSD");
   Console.WriteLine("Secure workflow/deployment: "+count+" checks passed (simulated firmware, real temporary files).");return count;
  }
 }
}

using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using DualBoot.Setup;
namespace DualBoot.Installer {
    public static class DeploymentTests {
        static int count;
        static void Check(bool ok,string label){if(!ok)throw new Exception("FAIL deployment: "+label);count++;}
        static void Reject(Action action,string label){bool failed=false;try{action();}catch{failed=true;}Check(failed,label);}
        sealed class Fixture {
            public string Root,Backup;public FakeFirmware Firmware;public ComputerState State;public InstallRequest Request;public InstallerEngine Engine;
            public Fixture(){
                string temp=Path.Combine(Path.GetTempPath(),"DualBoot-tests-"+Guid.NewGuid().ToString("N"));Root=Path.Combine(temp,"volume");Backup=Path.Combine(temp,"backups");
                Directory.CreateDirectory(Path.Combine(Root,@"EFI\Microsoft\Boot"));File.WriteAllText(Path.Combine(Root,@"EFI\Microsoft\Boot\bootmgfw.efi"),"ORIGINAL WINDOWS");
                Guid guid=new Guid("12345678-1234-4234-8234-123456789abc");
                Firmware=new FakeFirmware();Firmware.Values["BootOrder"]=new Variable(7,new byte[]{1,0});Firmware.Values["Boot0001"]=new Variable(7,InstallerTests.Option(guid,@"\EFI\Microsoft\Boot\bootmgfw.efi","Windows"));
                var entry=FirmwareEntry.Parse("Boot0001",Firmware.Read("Boot0001"));
                State=new ComputerState{SecureBoot=false,EncryptionClear=true,Order=Firmware.Read("BootOrder")};State.Entries.Add(entry);
                State.Partitions.Add(new PartitionInfo{Guid=guid,Root=Root,IsEsp=true,IsSystem=true,FileSystem="FAT32",FreeBytes=100000000});Request=new InstallRequest{Windows=entry};
                var files=new Dictionary<string,byte[]>();foreach(string name in Payload.Names)files[name]=Encoding.ASCII.GetBytes("PAYLOAD-"+name);
                Engine=new InstallerEngine(Firmware,files,Backup);
            }
        }
        public static int Run(){
            var f=new Fixture();var journal=f.Engine.Install(f.State,f.Request,false);
            Check(journal.State=="Installed" && File.Exists(Path.Combine(f.Root,@"EFI\DualBoot\DualBoot.efi")),"install creates own files");
            Check(File.ReadAllText(Path.Combine(f.Root,@"EFI\Microsoft\Boot\bootmgfw.efi"))=="ORIGINAL WINDOWS","original Windows file preserved");
            string saved=Path.Combine(f.Backup,journal.Id,"journal.json");var loaded=JournalStore.Load(saved);
            f.Engine.Update(loaded,f.Root,false,15,true);
            Check(File.ReadAllText(Path.Combine(f.Root,@"EFI\DualBoot\dualboot.ini")).Contains("timeout=15"),"managed configuration update");
            f.Engine.Restore(loaded,f.Root);
            Check(!Directory.Exists(Path.Combine(f.Root,@"EFI\DualBoot")) && f.Firmware.Read(journal.FirmwarePlan.EntryName)==null,"serialized journal restores files and firmware");
            f.Engine.Restore(JournalStore.Load(saved),f.Root);Check(true,"restoration idempotent");
            var failure=new Fixture();failure.Firmware.FailOrder=true;Reject(delegate{failure.Engine.Install(failure.State,failure.Request,false);},"failed firmware commit rejected");
            Check(!Directory.Exists(Path.Combine(failure.Root,@"EFI\DualBoot")) && Transaction.Equal(failure.Firmware.Read("BootOrder"),failure.State.Order),"failed firmware commit cleans only own files");
            var foreign=new Fixture();Directory.CreateDirectory(Path.Combine(foreign.Root,@"EFI\DualBoot"));File.WriteAllText(Path.Combine(foreign.Root,@"EFI\DualBoot\other.efi"),"KEEP");
            Reject(delegate{foreign.Engine.Install(foreign.State,foreign.Request,false);},"unmanaged directory refused");Check(foreign.Firmware.Writes==0,"no firmware writes on unmanaged installation");
            var changed=new Fixture();var j=changed.Engine.Install(changed.State,changed.Request,false);File.WriteAllText(Path.Combine(changed.Root,@"EFI\DualBoot\DualBoot.efi"),"MODIFIED");int writes=changed.Firmware.Writes;
            Reject(delegate{changed.Engine.Restore(j,changed.Root);},"modified own file preserved");Check(changed.Firmware.Writes==writes,"restore checks files before firmware writes");
            var extra=new Fixture();j=extra.Engine.Install(extra.State,extra.Request,false);File.WriteAllText(Path.Combine(extra.Root,@"EFI\DualBoot\unknown.txt"),"KEEP");
            Reject(delegate{extra.Engine.Restore(j,extra.Root);},"unrecognized extra content preserved");
            var interrupted=new Fixture();interrupted.Engine.AfterFile=delegate(string name){throw new IOException("injected copy interruption");};
            Reject(delegate{interrupted.Engine.Install(interrupted.State,interrupted.Request,false);},"partial file deployment fails safely");Check(interrupted.Firmware.Writes==0,"copy interruption never changes firmware");
            var missing=new Fixture();j=missing.Engine.Install(missing.State,missing.Request,false);File.Delete(Path.Combine(missing.Root,@"EFI\DualBoot\DualBoot.efi"));missing.Engine.Restore(j,missing.Root);
            Check(!Directory.Exists(Path.Combine(missing.Root,@"EFI\DualBoot")),"recovery tolerates missing owned file");
            var usb=new Fixture();string usbRoot=Path.Combine(Path.GetTempPath(),"DualBoot-usb-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(usbRoot);
            usb.Request.UsbPartition=new PartitionInfo{Root=usbRoot,IsUsb=true,IsInternal=false,FileSystem="FAT32",FreeBytes=100000000};usb.State.Partitions.Add(usb.Request.UsbPartition);
            j=usb.Engine.Install(usb.State,usb.Request,true);Check(File.Exists(Path.Combine(usbRoot,@"EFI\BOOT\BOOTX64.EFI")) && usb.Firmware.Writes==0,"USB menu install never changes firmware");
            usb.Engine.Restore(j,usbRoot);Check(!Directory.Exists(Path.Combine(usbRoot,@"EFI\BOOT")),"USB restoration");
            var stale=new Fixture();stale.Firmware.Values["BootOrder"]=new Variable(7,new byte[]{2,0});Reject(delegate{stale.Engine.Install(stale.State,stale.Request,false);},"stale preview rejected");Check(stale.Firmware.Writes==0,"stale preview no writes");
            var pending=new Fixture();j=pending.Engine.Install(pending.State,pending.Request,false);j.PendingConfig=Convert.ToBase64String(Encoding.ASCII.GetBytes(Configuration.Build(pending.Request.Windows,null,false,30)));
            j.State="Updating";JournalStore.Save(j,saved=Path.Combine(pending.Backup,j.Id,"journal.json"));File.WriteAllBytes(Path.Combine(pending.Root,@"EFI\DualBoot\dualboot.ini"),Convert.FromBase64String(j.PendingConfig));
            pending.Engine.Restore(JournalStore.Load(saved),pending.Root);Check(!Directory.Exists(Path.Combine(pending.Root,@"EFI\DualBoot")),"pending configuration recovered before restore");
            Check(JournalStore.Load(saved).Timeout==30,"recovered configuration metadata reflects committed bytes");
            var invalid=new Fixture();j=invalid.Engine.Install(invalid.State,invalid.Request,false);j.Files[0].Path=@"..\Microsoft\Boot\bootmgfw.efi";
            Reject(delegate{invalid.Engine.Restore(j,invalid.Root);},"forged journal path rejected");
            var identity=new Fixture();j=identity.Engine.Install(identity.State,identity.Request,false);j.PartitionGuid="87654321-4321-4321-8321-cba987654321";
            Reject(delegate{JournalStore.Validate(j);},"journal ESP must match firmware transaction");
            var empty=new Fixture();j=empty.Engine.Install(empty.State,empty.Request,false);Directory.CreateDirectory(Path.Combine(empty.Root,@"EFI\DualBoot\foreign-empty"));
            Reject(delegate{empty.Engine.Restore(j,empty.Root);},"unknown empty directory preserved");
            var race=new Fixture();bool inserted=false;
            race.Engine.AfterFile=delegate(string name){if(inserted)return;inserted=true;string own=Path.Combine(race.Root,@"EFI\DualBoot");Directory.CreateDirectory(own);File.WriteAllText(Path.Combine(own,"DualBoot.efi"),"PAYLOAD-DualBoot.efi");};
            Reject(delegate{race.Engine.Install(race.State,race.Request,false);},"concurrent foreign destination blocks promotion");
            Check(File.Exists(Path.Combine(race.Root,@"EFI\DualBoot\DualBoot.efi")),"unpromoted foreign directory never removed");
            var noOwner=new Fixture();j=noOwner.Engine.Install(noOwner.State,noOwner.Request,false);j.State="Prepared";File.Delete(Path.Combine(noOwner.Root,@"EFI\DualBoot\setup-owner.id"));writes=noOwner.Firmware.Writes;
            Reject(delegate{noOwner.Engine.Restore(j,noOwner.Root);},"Prepared recovery requires own unique marker");Check(noOwner.Firmware.Writes==writes,"missing marker blocks before firmware writes");
            var huge=new Fixture();var order=new byte[12];order[0]=1;for(int k=2;k<=6;k++){order[(k-1)*2]=(byte)k;var large=new byte[65536];for(int i=0;i<large.Length;i++)large[i]=255;huge.Firmware.Values["Boot"+k.ToString("X4")]=new Variable(7,large);}huge.Firmware.Values["BootOrder"]=new Variable(7,order);huge.State.Order=huge.Firmware.Read("BootOrder");
            Reject(delegate{huge.Engine.Install(huge.State,huge.Request,false);},"unrestorable journal size refused before install");Check(huge.Firmware.Writes==0 && !Directory.Exists(Path.Combine(huge.Root,@"EFI\DualBoot")),"oversized journal never writes EFI or NVRAM");
            Console.WriteLine("PASS: "+count+" deployment checks");return 0;
        }
    }
}

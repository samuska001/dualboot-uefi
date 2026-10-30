using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using DualBoot.Setup;

namespace DualBoot.Installer {
    public sealed class FakeFirmware : IFirmware {
        public Dictionary<string, Variable> Values = new Dictionary<string, Variable>();
        public int Writes; public bool FailOrder;
        public Variable Read(string name) { Variable v; return Values.TryGetValue(name,out v) ? new Variable(v.Attributes,(byte[])v.Data.Clone()) : null; }
        public void Write(string name,Variable value) {
            Writes++; if(name=="BootOrder" && FailOrder){FailOrder=false;throw new IOException("Injected firmware failure");}
            if(value==null) Values.Remove(name); else Values[name]=value;
        }
    }
    public static class InstallerTests {
        static int count;
        static void Check(bool ok,string name){if(!ok)throw new Exception("FAIL: "+name); count++;}
        static void Reject(Action operation,string name){bool failed=false;try{operation();}catch{failed=true;}Check(failed,name);}
        public static byte[] Option(Guid guid,string path,string description) {
            byte[] hd=new byte[42];hd[0]=4;hd[1]=1;hd[2]=42;hd[4]=1;hd[8]=0;hd[9]=8;hd[16]=0;hd[17]=64;
            Array.Copy(guid.ToByteArray(),0,hd,24,16);hd[40]=2;hd[41]=2;
            byte[] file=Encoding.Unicode.GetBytes(path+"\0"), desc=Encoding.Unicode.GetBytes(description+"\0");
            using(var m=new MemoryStream())using(var w=new BinaryWriter(m)){
                w.Write((uint)1);w.Write((ushort)(42+4+file.Length+4));w.Write(desc);w.Write(hd);
                w.Write((byte)4);w.Write((byte)4);w.Write((ushort)(4+file.Length));w.Write(file);w.Write(new byte[]{127,255,4,0});w.Write(new byte[]{99,88});return m.ToArray();
            }
        }
        public static int Run() {
            try {
                var guid=new Guid("12345678-1234-4234-8234-123456789abc");
                var raw=new Variable(7,Option(guid,@"\EFI\Microsoft\Boot\bootmgfw.efi","Windows"));
                var entry=FirmwareEntry.Parse("Boot0001",raw);
                Check(entry!=null && entry.PartitionGuid==guid && entry.Path==@"\EFI\Microsoft\Boot\bootmgfw.efi","parse original GPT option");
                Reject(delegate{FirmwareEntry.Parse("Boot0001",new Variable(7,new byte[]{1,2}));},"truncated option rejected");
                Reject(delegate{FirmwareEntry.Parse("Boot0001",new Variable(7,Option(guid,@"\EFI\..\evil.efi","Bad")));},"path traversal rejected");
                Reject(delegate{FirmwareEntry.Parse("Boot0001",new Variable(7,Option(guid,@"\EFI\BOOT\.\evil.efi","Bad")));},"path alias rejected");
                var extra=(byte[])raw.Data.Clone();extra[6]=0;extra[7]=0;
                Reject(delegate{FirmwareEntry.Parse("Boot0001",new Variable(7,extra));},"malformed device path rejected");
                var f=new FakeFirmware(); f.Values["BootOrder"]=new Variable(7,new byte[]{1,0});f.Values["Boot0001"]=raw;
                var plan=Transaction.Prepare(f,"Boot0001",guid);Transaction.Apply(f,plan);
                Check(Transaction.Equal(f.Read("Boot0001"),raw),"Windows optional data preserved");Transaction.Rollback(f,plan);
                Check(f.Read(plan.EntryName)==null,"firmware restoration");
                Check(Configuration.Build(entry,null,false,0).Contains("enabled=false"),"missing SteamOS disabled");
                Check(Configuration.Build(entry,null,false,0).Contains(guid.ToString()),"GUID generated automatically");
                Reject(delegate{Configuration.Build(entry,null,false,301);},"invalid timeout rejected");
                var state=new ComputerState{SecureBoot=false,EncryptionClear=true};
                var part=new PartitionInfo{Guid=guid,Root=Path.GetTempPath(),IsEsp=true,IsSystem=true,FileSystem="FAT32",FreeBytes=100000000};
                state.Entries.Add(entry);state.Partitions.Add(part); var request=new InstallRequest{Windows=entry,Timeout=0};
                Check(Policy.Compatibility(state,request,false)==null,"known safe policy accepted");
                state.SecureBoot=true;Check(Policy.Compatibility(state,request,false)!=null,"Secure Boot blocks install");state.SecureBoot=null;
                Check(Policy.Compatibility(state,request,false)!=null,"unknown Secure Boot blocks");state.SecureBoot=false;
                state.EncryptionClear=null;Check(Policy.Compatibility(state,request,false)!=null,"unknown encryption blocks SSD");state.EncryptionClear=true;
                part.IsSystem=false;Check(Policy.Compatibility(state,request,false)!=null,"other Windows ESP blocks SSD");part.IsSystem=true;
                part.IsUsb=true;Check(Policy.Compatibility(state,request,false)!=null,"external Windows ESP blocked");part.IsUsb=false;
                state.BootNextPending=true;Check(Policy.Compatibility(state,request,false)!=null,"pending BootNext blocked");state.BootNextPending=false;
                Reject(delegate{SafeFiles.Relative(@"..\Microsoft\Boot\bootmgfw.efi");},"deployment escape rejected");
                Reject(delegate{SafeFiles.Relative(@"themes\night\x:y.bmp");},"alternate stream rejected");
                Check(Policy.EncryptionState(0,0,0)==true,"known decrypted Windows accepted");
                Check(Policy.EncryptionState(1,1,0)==false,"encrypted Windows recognized");
                Check(Policy.EncryptionState(null,0,0)==null && Policy.EncryptionState(0,null,0)==null && Policy.EncryptionState(0,0,null)==null,"missing encryption fields never mean off");
                Check(Policy.EncryptionState(0,2,0)==null && Policy.EncryptionState(0,0,1)==null,"unknown encryption status and failed WMI method rejected");
                var steamGuid=new Guid("87654321-4321-4321-8321-cba987654321");
                var steamEntry=FirmwareEntry.Parse("Boot0002",new Variable(7,Option(steamGuid,@"\EFI\steamos\steamcl.efi","SteamOS")));
                state.Entries.Add(steamEntry);state.Partitions.Add(new PartitionInfo{Guid=steamGuid,Root=Path.GetTempPath(),IsEsp=true,FileSystem="FAT",FreeBytes=100000000});request.Steam=steamEntry;
                Check(Policy.Compatibility(state,request,false)==null,"read-only SteamOS FAT target supported");
                part.FileSystem="FAT";Check(Policy.Compatibility(state,request,false)!=null,"SSD write target still requires FAT32");part.FileSystem="FAT32";
                if(typeof(InstallerTests).Assembly.GetManifestResourceInfo("payload.zip")!=null)Check(Payload.ReadEmbedded().Count==6,"embedded payload hashes verified");
                Check(Selection.WindowsDefault(state)==entry,"unique current Windows selected");
                var second=FirmwareEntry.Parse("Boot0003",new Variable(7,Option(guid,@"\EFI\Microsoft\Boot\bootmgfw.efi","Another Windows")));state.Entries.Add(second);
                Check(Selection.WindowsDefault(state)==null,"ambiguous current Windows requires explicit choice");
                Console.WriteLine("PASS: "+count+" installer core checks");return DeploymentTests.Run();
            } catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
        }
        public static int Main(){return Run();}
    }
}

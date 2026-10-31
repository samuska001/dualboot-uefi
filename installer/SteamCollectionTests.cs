using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace DualBoot.Installer {
 public static class SteamCollectionTests {
  static int checks;
  static void Check(bool b,string name){if(!b)throw new Exception(name);checks++;}
  static void Reject(Action a,string name){try{a();}catch(InvalidDataException){checks++;return;}throw new Exception("Accepted: "+name);}
  static void Put(byte[] b,int p,uint n){Buffer.BlockCopy(BitConverter.GetBytes(n),0,b,p,4);}
  static void Word(byte[] b,int p,ushort n){Buffer.BlockCopy(BitConverter.GetBytes(n),0,b,p,2);}
  static byte[] HashsumElf(){
   byte[] b=new byte[384];b[0]=0x7f;b[1]=69;b[2]=76;b[3]=70;b[4]=2;b[5]=1;Put(b,40,192);Word(b,58,64);Word(b,60,3);Word(b,62,2);
   byte[] names=Encoding.ASCII.GetBytes("\0.rodata\0.shstrtab\0");Buffer.BlockCopy(names,0,b,64,names.Length);
   byte[] messages=Encoding.ASCII.GetBytes("%s: OK\n\0%s: HASH MISMATCH\n\0%s: READ ERROR\n\0");Buffer.BlockCopy(messages,0,b,96,messages.Length);
   Put(b,256,1);Put(b,260,1);Put(b,264,2);Put(b,280,96);Put(b,288,80);
   Put(b,320,9);Put(b,324,3);Put(b,344,64);Put(b,352,(uint)names.Length);return b;
  }
  sealed class AdversarialStream:MemoryStream {
   readonly long advertised; readonly bool changeLength; public int Reads;
   public AdversarialStream(byte[] contents,long length,bool mutate):base(contents){advertised=length;changeLength=mutate;}
   public override long Length { get { return advertised+(changeLength&&Reads>0?1:0); } }
   public override int Read(byte[] buffer,int offset,int count){Reads++;return base.Read(buffer,offset,count);}
  }
  static byte[] Core(){
   byte[] b=new byte[3072];Word(b,0,0x5a4d);Put(b,60,128);Put(b,128,0x4550);Word(b,132,0x8664);Word(b,134,4);Word(b,148,240);int o=152;Word(b,o,0x20b);Put(b,o+8,1536);Put(b,o+32,4096);Put(b,o+36,512);Word(b,o+68,10);Put(b,o+108,16);Put(b,o+152,16384);Put(b,o+156,12);
   string[] names={".text",".data","mods",".reloc"};for(int x=0;x<4;x++){int h=392+x*40;Buffer.BlockCopy(Encoding.ASCII.GetBytes(names[x]),0,b,h,names[x].Length);Put(b,h+8,512);Put(b,h+12,(uint)((x+1)*4096));Put(b,h+16,512);Put(b,h+20,(uint)(1024+x*512));}
   int m=2048;Put(b,m,0x676d696d);Put(b,m+8,24);int p=m+24;Put(b,p,0);Put(b,p+4,16);Put(b,p+8,0x464c457f);p+=16;Put(b,p,1);Put(b,p+4,16);p+=16;Put(b,p,3);Put(b,p+4,32);Buffer.BlockCopy(Encoding.ASCII.GetBytes("(memdisk)/boot/grub"),0,b,p+8,19);p+=32;Put(b,m+16,(uint)(p-m));Put(b,2560,4096);Put(b,2564,12);Word(b,2568,0xa000);return b;
  }
  static SteamFileRecord FileRecord(string root,string name,string original,byte[] b){File.WriteAllBytes(Path.Combine(root,"files",name),b);return new SteamFileRecord{Path="files/"+name,OriginalPath=original,Bytes=b.Length,Hash=SteamCollection.Sha256(b)};}
  public static int Main(string[] args){string root=Path.Combine(Path.GetTempPath(),"dualboot-collection-tests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(Path.Combine(root,"files"));try{
   byte[] original=HashsumElf(),quiet=SteamBundle.QuietHashsum(original);Check(quiet[96]==0 && original[96]==37,"quiet success format only in copied module");int differences=0;for(int x=0;x<original.Length;x++)if(original[x]!=quiet[x])differences++;Check(differences==1,"quiet module preserves code, errors and all other bytes");
   byte[] executable=HashsumElf();Put(executable,264,6);Reject(delegate{SteamBundle.QuietHashsum(executable);},"quiet refuses executable section");
   byte[] duplicate=HashsumElf();Buffer.BlockCopy(Encoding.ASCII.GetBytes("%s: OK\n\0"),0,duplicate,150,8);Reject(delegate{SteamBundle.QuietHashsum(duplicate);},"quiet refuses ambiguous success format");
   byte[] malformed=HashsumElf();Put(malformed,40,999999);Reject(delegate{SteamBundle.QuietHashsum(malformed);},"quiet refuses truncated ELF table");
   byte[] overlap=new byte[448],baseElf=HashsumElf();Buffer.BlockCopy(baseElf,0,overlap,0,baseElf.Length);Buffer.BlockCopy(baseElf,320,overlap,384,64);Buffer.BlockCopy(baseElf,256,overlap,320,64);Word(overlap,60,4);Word(overlap,62,3);Put(overlap,264,6);Reject(delegate{SteamBundle.QuietHashsum(overlap);},"quiet refuses overlapping executable section");
   AdversarialStream huge=new AdversarialStream(new byte[]{1},(long)Int32.MaxValue+1,false);Reject(delegate{SteamCollection.ReadBounded(huge,-1,65536);},"handle oversized before allocation");Check(huge.Reads==0,"oversize never read");
   Reject(delegate{SteamCollection.ReadBounded(new AdversarialStream(new byte[]{1,2,3},3,false),2,65536);},"opened handle differs from manifest");
   Reject(delegate{SteamCollection.ReadBounded(new AdversarialStream(new byte[]{1,2,3},2,false),2,65536);},"growth beyond snapshot EOF");
   Reject(delegate{SteamCollection.ReadBounded(new AdversarialStream(new byte[]{1},2,false),2,65536);},"truncation during read");
   Reject(delegate{SteamCollection.ReadBounded(new AdversarialStream(new byte[]{1,2},2,true),2,65536);},"length changed during read");
   byte[] kernel=new byte[512];Word(kernel,0,0x5a4d);
   SteamInventory i=new SteamInventory{SourceRoot=root,RootPartitionGuid=Guid.NewGuid().ToString(),EfiPartitionGuid=Guid.NewGuid().ToString(),Version="3.9.5",Slot="A",KernelArguments="console=tty1 quiet rootflags=subvol=/",Kernel=FileRecord(root,"kernel.efi","/boot/vmlinuz-test",kernel),Core=FileRecord(root,"core.efi","generated:grub-core",Core()),Config=FileRecord(root,"grub.cfg","/EFI/steamos/grub.cfg",Encoding.ASCII.GetBytes("never executed")),Initrds=new SteamFileRecord[]{FileRecord(root,"initrd.img","/boot/initramfs.img",new byte[]{1,2,3})}};
   Dictionary<string,object> manifest=new Dictionary<string,object>{{"Format","DualBootSteamCollectionV1"},{"RootPartitionGuid",i.RootPartitionGuid},{"EfiPartitionGuid",i.EfiPartitionGuid},{"Version",i.Version},{"Slot",i.Slot},{"Variant","steamdeck"},{"KernelArguments",i.KernelArguments},{"Kernel",i.Kernel},{"Initrds",i.Initrds},{"Core",i.Core},{"Config",i.Config}};
   string json=new JavaScriptSerializer().Serialize(manifest),path=Path.Combine(root,"collection.json");File.WriteAllText(path,json);SteamInventory loaded=SteamCollection.Load(root);Check(loaded.Initrds.Length==1,"load");
   byte[] output=SteamBundle.Build(loaded,delegate(byte[] k){return k;});Check(output.Length>3072,"bundle grows");Check(Encoding.ASCII.GetString(output).Contains("lockdown=integrity module.sig_enforce=1"),"policy args");Check(Encoding.ASCII.GetString(output).Contains(i.Kernel.Hash+"  /boot/vmlinuz-test"),"original guard");Check(output[1024]==0&&BitConverter.ToUInt16(output,output.Length-504)==0xa000,"relocation retained");
   File.WriteAllText(path,json.Replace("\"Format\":", "\"Format\":\"x\",\"Format\":"));Reject(delegate{SteamCollection.Load(root);},"duplicate JSON");File.WriteAllText(path,json.Replace("steamdeck","steamdeck-oobe"));Reject(delegate{SteamCollection.Load(root);},"oobe");File.WriteAllText(path,json);
   foreach(string bad in new string[]{"quiet; halt","quiet\nboot","x=$root","x='foo'","x=\\foo","lockdown=none","module.sig_enforce=0","init=/bin/sh","quiet --","dualboot.session=old"}){string copy=bad;Reject(delegate{SteamCollection.ValidateArguments(copy);},"arguments "+bad);}
   byte[] bound=SteamBundle.Build(i,delegate(byte[] k){return k;},"0123456789abcdef0123456789abcdef");Check(Encoding.ASCII.GetString(bound).Contains("dualboot.session=0123456789abcdef0123456789abcdef"),"session commandline");Check(Encoding.ASCII.GetString(bound).Contains("Format=DualBootBundleSessionV1"),"session resource");Reject(delegate{SteamBundle.Build(i,delegate(byte[] k){return k;},"invalid; boot");},"session injection");
   i.Kernel.Path="../kernel.efi";Reject(delegate{SteamCollection.Validate(i);},"traversal");i.Kernel.Path="files/kernel.efi";
   byte[] changed=(byte[])kernel.Clone();changed[99]=1;File.WriteAllBytes(Path.Combine(root,"files/kernel.efi"),changed);Reject(delegate{SteamBundle.Build(i,delegate(byte[] k){return k;});},"hash change");File.WriteAllBytes(Path.Combine(root,"files/kernel.efi"),kernel);
   byte[] core=Core();Put(core,152+144,1234);Reject(delegate{SteamBundle.Rebuild(core,new byte[1024]);},"signed core");core=Core();Put(core,2048+16,9999);Reject(delegate{SteamBundle.Rebuild(core,new byte[1024]);},"module overflow");core=Core();Word(core,2568,0xa000+8192);Put(core,2560,12288);Reject(delegate{SteamBundle.Rebuild(core,new byte[1024]);},"reloc resource");
   SortedDictionary<string,byte[]> tar=new SortedDictionary<string,byte[]>();tar.Add("boot/a",new byte[]{1,2,3});byte[] archive=SteamBundle.Tar(tar);Check(archive.Length==2048&&archive[512]==1&&archive[515]==0,"tar padding");int checksum=0;for(int x=0;x<512;x++)checksum+=(x>=148&&x<156)?32:archive[x];Check(Convert.ToInt32(Encoding.ASCII.GetString(archive,148,6),8)==checksum,"tar checksum");tar.Add("boot/../escape",new byte[]{1});Reject(delegate{SteamBundle.Tar(tar);},"tar escape");
   if(args.Length>0){byte[] actual=File.ReadAllBytes(args[0]);byte[] rebuilt=SteamBundle.Rebuild(actual,archive);Check(rebuilt.Length>0,"real GRUB fixture readonly");}
   Console.WriteLine("Steam collection/bundle checks: "+checks);return 0;
  }catch(Exception e){Console.Error.WriteLine(e);return 1;}finally{Directory.Delete(root,true);}}
 }
}

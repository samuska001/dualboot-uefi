using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

namespace DualBoot.Installer {
 public static class SteamBundle {
  static void Need(bool b,string why){if(!b)throw new InvalidDataException(why);}
  static uint U(byte[] b,int p){Need(p>=0&&p<=b.Length-4,"PE truncado.");return BitConverter.ToUInt32(b,p);}
  static ushort W(byte[] b,int p){Need(p>=0&&p<=b.Length-2,"PE truncado.");return BitConverter.ToUInt16(b,p);}
  static ulong Q(byte[] b,int p){Need(p>=0&&p<=b.Length-8,"GRUB truncado.");return BitConverter.ToUInt64(b,p);}
  static void Put(byte[] b,int p,uint n){Buffer.BlockCopy(BitConverter.GetBytes(n),0,b,p,4);}
  static int Int(uint n){Need(n<=Int32.MaxValue,"PE excessivo.");return (int)n;}
  static int Align(int n,int a){Need(a>0&&(a&(a-1))==0&&a<=65536,"Alinhamento PE invalido.");return checked((n+a-1)/a*a);}
  // Modify only the known success format in a copied, read-only ELF data section.
  // Verification instructions, return values, relocations and error strings remain identical.
  public static byte[] QuietHashsum(byte[] data){
   Need(data!=null,"Modulo ausente.");string bytes=Encoding.ASCII.GetString(data);const string format="%s: OK\n\0";
   int at=bytes.IndexOf(format,StringComparison.Ordinal);if(at<0)return data;
   Need(bytes.IndexOf(format,at+1,StringComparison.Ordinal)<0,"Formato de sucesso ambiguo.");
   Need(data.Length>=64&&U(data,0)==0x464c457f&&data[4]==2&&data[5]==1,"ELF64 little-endian esperado.");
   Need(bytes.Contains("%s: HASH MISMATCH\n\0")&&bytes.Contains("%s: READ ERROR\n\0"),"Mensagens de verificacao desconhecidas.");
   ulong table64=Q(data,40);int count=W(data,60),strings=W(data,62);
   Need(table64<=(ulong)data.Length&&W(data,58)==64&&count>0&&strings<count&&table64+(ulong)count*64<=(ulong)data.Length,"Tabela ELF invalida.");
   int table=(int)table64,sh=table+strings*64;ulong namesAt=Q(data,sh+24),namesSize=Q(data,sh+32);
   Need(namesAt<=(ulong)data.Length&&namesSize<=(ulong)data.Length-namesAt,"Nomes de secoes truncados.");
   byte[] names=Slice(data,(int)namesAt,(int)namesSize);int matching=0;
   for(int i=0;i<count;i++){
    int section=table+i*64;uint nameAt=U(data,section);Need(nameAt<(uint)names.Length,"Nome ELF invalido.");int stop=Array.IndexOf(names,(byte)0,(int)nameAt);Need(stop>=(int)nameAt,"Nome ELF sem terminador.");
    string name=Encoding.ASCII.GetString(names,(int)nameAt,stop-(int)nameAt);ulong offset=Q(data,section+24),size=Q(data,section+32);
    if(offset<=(ulong)at && size>=(ulong)at-offset+8){Need(offset<=(ulong)data.Length&&size<=(ulong)data.Length-offset,"Secao ELF truncada.");Need(name.StartsWith(".rodata",StringComparison.Ordinal)&&U(data,section+4)==1&&(Q(data,section+8)&7)==2,"Formato sobreposto a secao executavel ou gravavel.");matching++;}
   }
   Need(matching==1,"Formato de sucesso fora de uma unica secao de dados somente leitura.");byte[] result=(byte[])data.Clone();result[at]=0;return result;
  }
  static byte[] Slice(byte[] b,int p,int n){Need(p>=0&&n>=0&&p<=b.Length-n,"Recurso truncado.");byte[] r=new byte[n];Buffer.BlockCopy(b,p,r,0,n);return r;}
  public static byte[] Build(SteamInventory inventory,Func<byte[],byte[]> signKernel){return Build(inventory,signKernel,null);}
  public static byte[] Build(SteamInventory inventory,Func<byte[],byte[]> signKernel,string sessionId){
   Need(sessionId==null||System.Text.RegularExpressions.Regex.IsMatch(sessionId,@"^[0-9a-f]{32}$"),"Sessao invalida.");
   SteamCollection.Validate(inventory);Need(signKernel!=null,"Assinador ausente.");
   byte[] kernel=SteamCollection.ReadVerified(inventory,inventory.Kernel);Need(kernel.Length>256&&W(kernel,0)==0x5a4d,"Kernel deve ser EFI PE.");
   byte[] signed=signKernel(kernel);Need(signed!=null&&signed.Length>=kernel.Length&&W(signed,0)==0x5a4d,"Assinador retornou kernel invalido.");
   SortedDictionary<string,byte[]> files=new SortedDictionary<string,byte[]>(StringComparer.Ordinal);
   files.Add("boot/kernel.efi",signed);StringBuilder guard=new StringBuilder();guard.Append(inventory.Kernel.Hash).Append("  ").Append(inventory.Kernel.OriginalPath).Append('\n');
   StringBuilder initrd=new StringBuilder();int index=0;foreach(SteamFileRecord f in inventory.Initrds){string path="boot/initrd-"+(index++)+".img";files.Add(path,SteamCollection.ReadVerified(inventory,f));initrd.Append(" (memdisk)/").Append(path);guard.Append(f.Hash).Append("  ").Append(f.OriginalPath).Append('\n');}
   files.Add("boot/rootfs.sha256",Encoding.ASCII.GetBytes(guard.ToString()));
   string sessionArg=sessionId==null?"":" dualboot.session="+sessionId;
   string cfg="set superusers=dualboot-maintenance\nset timeout=0\nset timeout_style=menu\nset steamenv_loader_mode=auto\nset steamenv_kernel_mode=keep\nsteamenv_init\nmenuentry 'SteamOS assinado' --unrestricted {\n search --no-floppy --part-uuid --set=root "+inventory.RootPartitionGuid.ToLowerInvariant()+"\n if hashsum --hash sha256 --check (memdisk)/boot/rootfs.sha256; then\n  if steamenv_boot linux (memdisk)/boot/kernel.efi "+inventory.KernelArguments+" lockdown=integrity module.sig_enforce=1"+sessionArg+"; then\n   if initrd"+initrd+"; then\n    boot\n   fi\n  fi\n else\n  echo 'Pacote desatualizado: prepare nova coleta no Windows.'\n  sleep 10\n fi\n}\n";
   if(sessionId!=null)files.Add("boot/session.txt",Encoding.ASCII.GetBytes("Format=DualBootBundleSessionV1\nSessionId="+sessionId+"\nRootPartitionGuid="+inventory.RootPartitionGuid.ToLowerInvariant()+"\n"));
   files.Add("boot/grub/grub.cfg",Encoding.ASCII.GetBytes(cfg));
   return Rebuild(SteamCollection.ReadVerified(inventory,inventory.Core),Tar(files));
  }
  static byte[] Module(uint kind,byte[] data){byte[] b=new byte[Align(checked(data.Length+8),8)];Put(b,0,kind);Put(b,4,(uint)b.Length);Buffer.BlockCopy(data,0,b,8,data.Length);return b;}
  // GNU GRUB x86_64-efi layout only. Preserve code/data and every relocation byte.
  public static byte[] Rebuild(byte[] image,byte[] archive){
   Need(image!=null&&image.Length>=256&&W(image,0)==0x5a4d,"MZ invalido.");int pe=Int(U(image,0x3c));Need(U(image,pe)==0x4550&&W(image,pe+4)==0x8664&&W(image,pe+6)==4,"PE x64 GRUB esperado.");
   int opt=checked(pe+24),optSize=W(image,pe+20);Need(optSize>=240&&W(image,opt)==0x20b&&W(image,opt+68)==10,"PE EFI PE32+ esperado.");Need(U(image,opt+108)>=6,"Diretorios PE ausentes.");Need(U(image,opt+144)==0&&U(image,opt+148)==0,"Core GRUB deve ser unsigned.");
   int sa=Int(U(image,opt+32)),fa=Int(U(image,opt+36));Align(1,sa);Align(1,fa);Need(sa>=fa&&fa>=512,"Alinhamentos PE desconhecidos.");
   int table=checked(opt+optSize);string[] names={".text",".data","mods",".reloc"};int[] raw=new int[4],size=new int[4],rva=new int[4],vs=new int[4];
   for(int x=0;x<4;x++){int h=checked(table+x*40);Need(h<=image.Length-40,"Secao truncada.");Need(Encoding.ASCII.GetString(image,h,8).TrimEnd('\0')==names[x],"Layout GRUB desconhecido.");vs[x]=Int(U(image,h+8));rva[x]=Int(U(image,h+12));size[x]=Int(U(image,h+16));raw[x]=Int(U(image,h+20));Need(raw[x]>=table+160&&raw[x]%fa==0&&rva[x]%sa==0&&size[x]%fa==0&&raw[x]<=image.Length-size[x],"Secao PE invalida.");if(x>0)Need(raw[x]>=raw[x-1]+size[x-1]&&rva[x]>=rva[x-1]+vs[x-1],"Secoes sobrepostas.");}
   Need(raw[3]+size[3]==image.Length,"Overlay PE nao suportado.");Need(U(image,opt+152)==(uint)rva[3]&&U(image,opt+156)<=(uint)size[3],"Diretorio reloc invalido.");
   int relocLen=Int(U(image,opt+156)),pos=0;while(pos<relocLen){Need(pos<=relocLen-8,"Reloc truncada.");int p=raw[3]+pos;uint page=U(image,p);int block=Int(U(image,p+4));Need(block>=8&&block%2==0&&block<=relocLen-pos,"Bloco reloc invalido.");for(int j=8;j<block;j+=2){ushort entry=W(image,p+j);int type=entry>>12;Need(type==0||type==10,"Tipo reloc desconhecido.");if(type!=0)Need((ulong)page+(uint)(entry&4095)+8<=(ulong)rva[2],"Reloc aponta para recurso movido.");}pos+=block;}
   Need(U(image,raw[2])==0x676d696d&&U(image,raw[2]+4)==0&&Q(image,raw[2]+8)==24,"Cabecalho modules GRUB invalido.");ulong end64=Q(image,raw[2]+16);Need(end64>=24&&end64<=(ulong)size[2],"Modules truncados.");int end=(int)end64;pos=24;byte[] prefix=null;List<byte[]> entries=new List<byte[]>();int elfCount=0,memdiskCount=0;
   while(pos<end){Need(pos<=end-8,"Modulo truncado.");int p=raw[2]+pos;uint type=U(image,p);int n=Int(U(image,p+4));Need(n>=8&&n%8==0&&n<=end-pos,"Modulo invalido.");byte[] entry=Slice(image,p,n);if(type==0){Need(n>=12&&U(entry,8)==0x464c457f,"Modulo ELF esperado.");byte[] elf=Slice(entry,8,n-8),quiet=QuietHashsum(elf);if(!Object.ReferenceEquals(elf,quiet))Buffer.BlockCopy(quiet,0,entry,8,quiet.Length);entries.Add(entry);elfCount++;}else if(type==3){Need(prefix==null,"Prefix duplicado.");prefix=entry;Need(Encoding.ASCII.GetString(entry,8,n-8).TrimEnd('\0')=="(memdisk)/boot/grub","Prefix nao suportado.");}else if(type==1){memdiskCount++;}else throw new InvalidDataException("Tipo modulo GRUB desconhecido.");pos+=n;}
   Need(elfCount>0&&prefix!=null&&memdiskCount==1,"Core GRUB incompleto.");entries.Add(Module(1,archive));entries.Add(prefix);
   byte[] blob;using(MemoryStream m=new MemoryStream()){m.Write(new byte[24],0,24);foreach(byte[] e in entries)m.Write(e,0,e.Length);blob=m.ToArray();}Put(blob,0,0x676d696d);Buffer.BlockCopy(BitConverter.GetBytes((ulong)24),0,blob,8,8);Buffer.BlockCopy(BitConverter.GetBytes((ulong)blob.Length),0,blob,16,8);
   int newRaw=Align(blob.Length,fa),newVirtual=Align(blob.Length,sa),relocRaw=checked(raw[2]+newRaw),relocRva=checked(rva[2]+newVirtual);byte[] output=new byte[checked(relocRaw+size[3])];Buffer.BlockCopy(image,0,output,0,raw[2]);Buffer.BlockCopy(blob,0,output,raw[2],blob.Length);Buffer.BlockCopy(image,raw[3],output,relocRaw,size[3]);
   Put(output,table+80+8,(uint)newVirtual);Put(output,table+80+16,(uint)newRaw);Put(output,table+120+12,(uint)relocRva);Put(output,table+120+20,(uint)relocRaw);
   long initialized=(long)U(image,opt+8)+newRaw-size[2];Need(initialized>=0&&initialized<=uint.MaxValue,"PE initialized overflow.");Put(output,opt+8,(uint)initialized);Put(output,opt+56,(uint)Align(checked(relocRva+vs[3]),sa));Put(output,opt+152,(uint)relocRva);Put(output,opt+64,0);return output;
  }
  public static byte[] Tar(SortedDictionary<string,byte[]> files){using(MemoryStream m=new MemoryStream()){foreach(KeyValuePair<string,byte[]> f in files){Need(f.Key.Length<100&&System.Text.RegularExpressions.Regex.IsMatch(f.Key,@"^boot/[A-Za-z0-9_./-]+$")&&!f.Key.Contains(".."),"Nome TAR inseguro.");byte[] h=new byte[512];Ascii(h,0,f.Key);Ascii(h,100,"0000644");Ascii(h,108,"0000000");Ascii(h,116,"0000000");Ascii(h,124,Convert.ToString(f.Value.Length,8).PadLeft(11,'0'));Ascii(h,136,"00000000000");for(int x=148;x<156;x++)h[x]=32;h[156]=(byte)'0';Ascii(h,257,"ustar");Ascii(h,263,"00");int sum=0;foreach(byte b in h)sum+=b;Ascii(h,148,Convert.ToString(sum,8).PadLeft(6,'0'));h[154]=0;h[155]=32;m.Write(h,0,512);m.Write(f.Value,0,f.Value.Length);int pad=(512-f.Value.Length%512)%512;m.Write(new byte[pad],0,pad);}m.Write(new byte[1024],0,1024);return m.ToArray();}}
  static void Ascii(byte[] b,int p,string s){byte[] bytes=Encoding.ASCII.GetBytes(s);Buffer.BlockCopy(bytes,0,b,p,bytes.Length);}
 }
}

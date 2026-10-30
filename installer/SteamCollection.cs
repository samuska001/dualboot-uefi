using System;
using System.IO;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace DualBoot.Installer {
 public sealed class SteamFileRecord { public string Path, OriginalPath, Hash; public long Bytes; }
 public sealed class SteamInventory {
  public string RootPartitionGuid, EfiPartitionGuid, Version, Slot, SourceRoot, KernelArguments;
  public SteamFileRecord Kernel, Core, Config;
  public SteamFileRecord[] Initrds;
 }
 public static class SteamCollection {
  public const long MaximumFileBytes = 536870912;
  static void Require(bool ok, string why) { if (!ok) throw new InvalidDataException(why); }
  public static string Sha256(byte[] data) { using (SHA256 s = SHA256.Create()) return BitConverter.ToString(s.ComputeHash(data)).Replace("-", "").ToLowerInvariant(); }
  static string Str(Dictionary<string,object> d,string k) { object v; Require(d.TryGetValue(k,out v) && v is string,"Campo ausente: "+k); return (string)v; }
  static Dictionary<string,object> Obj(object v) { Require(v is Dictionary<string,object>,"Objeto esperado."); return (Dictionary<string,object>)v; }
  static void Keys(Dictionary<string,object> d, params string[] keys) { Require(d.Count==keys.Length,"Campos desconhecidos ou ausentes."); foreach(string k in keys) Require(d.ContainsKey(k),"Campo ausente: "+k); }
  static SteamFileRecord Record(object value) {
   Dictionary<string,object> d=Obj(value); Keys(d,"Path","OriginalPath","Hash","Bytes");
   object n=d["Bytes"]; Require(n is long,"Tamanho inteiro esperado.");
   return new SteamFileRecord {Path=Str(d,"Path"),OriginalPath=Str(d,"OriginalPath"),Hash=Str(d,"Hash"),Bytes=(long)n};
  }
  public static SteamInventory Load(string directory) {
   string root=System.IO.Path.GetFullPath(directory); CheckDirectory(root);
   byte[] manifest=ReadFileBounded(root,"collection.json",-1,65536);
   string json=new UTF8Encoding(false,true).GetString(manifest);
   if(json.Length>0&&json[0]=='\ufeff')json=json.Substring(1);
   Dictionary<string,object> d=Obj(new StrictJson(json).Read());
   Keys(d,"Format","RootPartitionGuid","EfiPartitionGuid","Version","Slot","Variant","KernelArguments","Kernel","Initrds","Core","Config");
   Require(Str(d,"Format")=="DualBootSteamCollectionV1","Formato desconhecido.");
   Require(Str(d,"Variant")=="steamdeck","Variante SteamOS nao suportada.");
   SteamInventory i=new SteamInventory {SourceRoot=root,RootPartitionGuid=Str(d,"RootPartitionGuid"),EfiPartitionGuid=Str(d,"EfiPartitionGuid"),Version=Str(d,"Version"),Slot=Str(d,"Slot"),KernelArguments=Str(d,"KernelArguments"),Kernel=Record(d["Kernel"]),Core=Record(d["Core"]),Config=Record(d["Config"])};
   List<object> a=d["Initrds"] as List<object>; Require(a!=null && a.Count>0 && a.Count<=4,"Initramfs ausente ou excessivo.");
   i.Initrds=new SteamFileRecord[a.Count]; for(int x=0;x<a.Count;x++) i.Initrds[x]=Record(a[x]); Validate(i); return i;
  }
  public static void Validate(SteamInventory i) {
   Require(i!=null,"Inventario ausente."); Guid g;
   Require(Guid.TryParseExact(i.RootPartitionGuid,"D",out g)&&g!=Guid.Empty,"GUID raiz invalido.");
   Require(Guid.TryParseExact(i.EfiPartitionGuid,"D",out g)&&g!=Guid.Empty && !String.Equals(i.RootPartitionGuid,i.EfiPartitionGuid,StringComparison.OrdinalIgnoreCase),"GUID EFI invalido.");
   Require(i.Slot=="A"||i.Slot=="B","Slot desconhecido.");
   Require(i.Version!=null && Regex.IsMatch(i.Version,@"^3\.9\.[0-9]{1,3}$"),"Versao fora do perfil experimental 3.9.x.");
   ValidateArguments(i.KernelArguments);
   Require(i.Initrds!=null&&i.Initrds.Length>0&&i.Initrds.Length<=4,"Initramfs invalido.");
   HashSet<string> paths=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   List<SteamFileRecord> files=new List<SteamFileRecord>(i.Initrds); files.Add(i.Kernel); files.Add(i.Core); files.Add(i.Config);
   long total=0; foreach(SteamFileRecord f in files) { Require(f!=null,"Arquivo ausente."); Require(paths.Add(f.Path??""),"Arquivo duplicado."); Require(f.Bytes>0&&f.Bytes<=MaximumFileBytes,"Tamanho invalido."); total+=f.Bytes; Require(total<=1073741824,"Coleta excessiva."); ReadVerified(i,f); }
   Require(i.Kernel.OriginalPath!=null&&Regex.IsMatch(i.Kernel.OriginalPath,@"^/boot/[A-Za-z0-9._+-]+$"),"Caminho kernel invalido.");
   HashSet<string> originals=new HashSet<string>(StringComparer.Ordinal); originals.Add(i.Kernel.OriginalPath);
   foreach(SteamFileRecord f in i.Initrds) Require(f.OriginalPath!=null&&Regex.IsMatch(f.OriginalPath,@"^/boot/[A-Za-z0-9._+-]+$")&&originals.Add(f.OriginalPath),"Caminho initramfs invalido.");
   Require(i.Core.OriginalPath=="generated:grub-core"&&i.Config.OriginalPath=="/EFI/steamos/grub.cfg","Origem core/config invalida.");
  }
  public static void ValidateArguments(string args) {
   Require(args!=null&&args.Length>0&&args.Length<=4096,"Argumentos ausentes/excessivos.");
   Require(Regex.IsMatch(args,@"^[A-Za-z0-9_./,:=+@%~-]+(?: [A-Za-z0-9_./,:=+@%~-]+)*$"),"Argumentos contêm sintaxe GRUB nao permitida.");
   foreach(string t in args.Split(' ')) Require(t!="--"&&!t.StartsWith("init=")&&!t.StartsWith("rdinit=")&&!t.StartsWith("lockdown=")&&!t.StartsWith("module.sig_enforce=")&&!t.StartsWith("enforcemodulesig=")&&!t.StartsWith("dualboot.session=")&&t!="single"&&t!="break"&&!t.StartsWith("rd.break"),"Argumento de politica nao permitido.");
  }
  static void CheckDirectory(string path) { Require(Directory.Exists(path),"Diretorio ausente."); DirectoryInfo d=new DirectoryInfo(path); while(d!=null) { Require((d.Attributes&FileAttributes.ReparsePoint)==0,"Links nao permitidos."); d=d.Parent; } }
  static string SafePath(string root,string relative) {
   Require(relative!=null&&Regex.IsMatch(relative,@"^[A-Za-z0-9_.+-]+(?:/[A-Za-z0-9_.+-]+)*$")&&!relative.Contains(".."),"Caminho inseguro.");
   // collection.json is the only top-level dotted filename permitted.
   string full=System.IO.Path.GetFullPath(System.IO.Path.Combine(root,relative.Replace('/',System.IO.Path.DirectorySeparatorChar)));
   Require(full.StartsWith(root.TrimEnd('\\','/')+System.IO.Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase),"Escape de diretorio."); CheckDirectory(System.IO.Path.GetDirectoryName(full));
   Require(File.Exists(full)&&(File.GetAttributes(full)&FileAttributes.ReparsePoint)==0,"Arquivo ausente ou link."); return full;
  }
  public static byte[] ReadVerified(SteamInventory i,SteamFileRecord f) {
   Require(f.Hash!=null&&Regex.IsMatch(f.Hash,@"^[0-9a-f]{64}$"),"SHA256 invalido.");
   Require(f.Bytes>0&&f.Bytes<=MaximumFileBytes,"Tamanho invalido.");
   byte[] b=ReadFileBounded(System.IO.Path.GetFullPath(i.SourceRoot),f.Path,f.Bytes,MaximumFileBytes);
   Require(Sha256(b)==f.Hash,"Arquivo alterado: "+f.Path); return b;
  }
  static byte[] ReadFileBounded(string root,string relative,long expected,long maximum) {
   string p=SafePath(root,relative);
   // FileShare.Read prevents writes/deletes while this handle is held on Windows.
   // Recheck the entire path after opening, before allocating from the handle length.
   using(FileStream s=new FileStream(p,FileMode.Open,FileAccess.Read,FileShare.Read)) {
    SafePath(root,relative);
    byte[] result=ReadBounded(s,expected,maximum);
    SafePath(root,relative);
    return result;
   }
  }
  internal static byte[] ReadBounded(Stream s,long expected,long maximum) {
   Require(s!=null&&s.CanRead&&s.CanSeek&&s.Position==0,"Stream invalido.");
   long length=s.Length;
   Require(maximum>0&&length>0&&length<=maximum&&length<=Int32.MaxValue,"Arquivo excessivo/vazio.");
   Require(expected==-1||length==expected,"Tamanho alterado no handle.");
   // Allocate only the validated snapshot, never a second uncontrolled Length value.
   byte[] b=new byte[(int)length]; int at=0,n;
   while(at<b.Length&&(n=s.Read(b,at,b.Length-at))>0)at+=n;
   Require(at==b.Length,"Leitura incompleta.");
   Require(s.ReadByte()==-1&&s.Length==length&&s.Position==length,"Arquivo mudou durante leitura.");
   return b;
  }
  // Minimal strict JSON reader: rejects duplicate fields, floats, escapes outside strings,
  // trailing values and excessive nesting before the manifest becomes an object.
  sealed class StrictJson {
   readonly string s; int p,depth; public StrictJson(string value){s=value;}
   void Ws(){while(p<s.Length&&" \r\n\t".IndexOf(s[p])>=0)p++;}
   char Take(){Require(p<s.Length,"JSON truncado."); return s[p++];}
   public object Read(){object v=Value();Ws();Require(p==s.Length,"JSON extra.");return v;}
   string String(){int start=p;Require(Take()=='"',"String esperada.");bool esc=false;while(p<s.Length){char c=Take();if(c=='"'&&!esc)return new JavaScriptSerializer().Deserialize<string>(s.Substring(start,p-start));if(c=='\\'&&!esc)esc=true;else esc=false;}throw new InvalidDataException("String truncada.");}
   object Value(){Ws();Require(++depth<=8,"JSON profundo.");object result;char c=p<s.Length?s[p]:'\0';
    if(c=='{'){p++;Dictionary<string,object>d=new Dictionary<string,object>(StringComparer.Ordinal);Ws();if(p<s.Length&&s[p]=='}'){p++;result=d;}else{while(true){Ws();string k=String();Require(!d.ContainsKey(k),"Campo JSON duplicado.");Ws();Require(Take()==':',"JSON invalido.");d.Add(k,Value());Ws();char sep=Take();if(sep=='}')break;Require(sep==',',"JSON invalido.");}result=d;}}
    else if(c=='['){p++;List<object>a=new List<object>();Ws();if(p<s.Length&&s[p]==']')p++;else while(true){Require(a.Count<32,"Lista excessiva.");a.Add(Value());Ws();char sep=Take();if(sep==']')break;Require(sep==',',"JSON invalido.");}result=a;}
    else if(c=='"')result=String();else{int start=p;while(p<s.Length&&s[p]>='0'&&s[p]<='9')p++;long n=0;Require(p>start&&long.TryParse(s.Substring(start,p-start),out n),"Numero JSON invalido.");Require(p-start==1||s[start]!='0',"Numero nao canonico.");result=n;}
    depth--;return result;
   }
  }
 }
}

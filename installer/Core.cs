using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.ComponentModel;
using System.Runtime.InteropServices;
using DualBoot.Setup;

namespace DualBoot.Installer {
    public sealed class FirmwareEntry {
        public string Name,Path,Description; public Guid PartitionGuid; public Variable Raw; public bool Active;
        public bool IsWindows { get { return String.Equals(Path,@"\EFI\Microsoft\Boot\bootmgfw.efi",StringComparison.OrdinalIgnoreCase); } }
        public bool IsSteam { get { return String.Equals(Path,@"\EFI\steamos\steamcl.efi",StringComparison.OrdinalIgnoreCase); } }
        public override string ToString(){return Description+" — "+Name;}
        static void Need(bool ok){if(!ok)throw new InvalidDataException("Entrada EFI não compatível: esperado HD GPT e caminho EFI curto, sem atalhos.");}
        public static FirmwareEntry Parse(string name,Variable raw) {
            Need(name!=null && Regex.IsMatch(name,@"\ABoot[0-9A-F]{4}\z") && raw!=null && raw.Data!=null && raw.Attributes==7);
            byte[] d=raw.Data;Need(d.Length>=8 && d.Length<=65535);
            int n=BitConverter.ToUInt16(d,4),at=6;
            while(at+1<d.Length && (d[at]!=0 || d[at+1]!=0))at+=2;
            Need(at+1<d.Length);
            string description=Encoding.Unicode.GetString(d,6,at-6);at+=2;
            Need(n>=46 && n<=d.Length-at);int end=at+n;
            Need(at+42<=end && d[at]==4 && d[at+1]==1 && BitConverter.ToUInt16(d,at+2)==42 && d[at+40]==2 && d[at+41]==2);
            byte[] guid=new byte[16];Array.Copy(d,at+24,guid,0,16);at+=42;
            Need(at+4<=end && d[at]==4 && d[at+1]==4);
            int fileLength=BitConverter.ToUInt16(d,at+2);
            Need(fileLength>=6 && (fileLength%2)==0 && fileLength<=end-at && d[at+fileLength-2]==0 && d[at+fileLength-1]==0);
            string path=Encoding.Unicode.GetString(d,at+4,fileLength-6);at+=fileLength;
            Need(at+4==end && d[at]==127 && d[at+1]==255 && d[at+2]==4 && d[at+3]==0);
            Need(path.Length<240 && path.StartsWith(@"\EFI\",StringComparison.OrdinalIgnoreCase) && path.EndsWith(".efi",StringComparison.OrdinalIgnoreCase));
            SafeFiles.Relative(path.Substring(1)); Need(new Guid(guid)!=Guid.Empty);
            return new FirmwareEntry{Name=name,Description=description,Path=path,PartitionGuid=new Guid(guid),Raw=raw,Active=(BitConverter.ToUInt32(d,0)&1)!=0};
        }
    }
    public sealed class PartitionInfo {
        public Guid Guid; public string Root,FileSystem,Label; public int DiskNumber,PartitionNumber;
        public bool IsEsp,IsSystem,IsUsb,IsBoot; public bool IsInternal=true; public long FreeBytes;
        public override string ToString(){return String.IsNullOrEmpty(Label)?"Disco "+DiskNumber+" / partição "+PartitionNumber:Label;}
    }
    public sealed class ComputerState {
        public bool? SecureBoot,EncryptionClear; public bool BootNextPending;
        public List<FirmwareEntry> Entries=new List<FirmwareEntry>(); public List<PartitionInfo> Partitions=new List<PartitionInfo>();
        public List<string> Notes=new List<string>(); public Variable Order;
        public PartitionInfo Find(Guid guid){PartitionInfo found=null;foreach(var p in Partitions)if(p.Guid==guid){if(found!=null)throw new InvalidDataException("GUID de partição duplicado.");found=p;}return found;}
    }
    public sealed class InstallRequest {
        public FirmwareEntry Windows,Steam; public PartitionInfo UsbPartition; public int Timeout; public bool SteamDefault,SteamFirst;
    }
    public static class Selection {
        public static FirmwareEntry WindowsDefault(ComputerState state){
            FirmwareEntry only=null,current=null;int count=0,currentCount=0;
            foreach(var entry in state.Entries)if(entry.Active && entry.IsWindows){only=entry;count++;var part=state.Find(entry.PartitionGuid);if(part!=null&&part.IsSystem){current=entry;currentCount++;}}
            return currentCount==1?current:currentCount==0 && count==1?only:null;
        }
    }
    public static class Configuration {
        public sealed class Options {public bool SteamDefault,SteamFirst;public int Timeout;}
        public static Options ReadOptions(byte[] data){
            foreach(byte b in data)if(b>127 || b==0)throw new InvalidDataException("Configuração pendente não é ASCII.");string text=Encoding.ASCII.GetString(data);
            var standard=Regex.Matches(text,@"^default=(windows|steamos)\r?$",RegexOptions.Multiline);var order=Regex.Matches(text,@"^order=(windows,steamos|steamos,windows)\r?$",RegexOptions.Multiline);var time=Regex.Matches(text,@"^timeout=([0-9]{1,3})\r?$",RegexOptions.Multiline);
            if(standard.Count!=1 || order.Count!=1 || time.Count!=1 || Int32.Parse(time[0].Groups[1].Value)>300)throw new InvalidDataException("Opções pendentes inválidas.");
            return new Options{SteamDefault=standard[0].Groups[1].Value=="steamos",SteamFirst=order[0].Groups[1].Value=="steamos,windows",Timeout=Int32.Parse(time[0].Groups[1].Value)};
        }
        public static string Build(FirmwareEntry windows,FirmwareEntry steam,bool steamDefault,int timeout){return Build(windows,steam,steamDefault,timeout,false);}
        public static string Build(FirmwareEntry windows,FirmwareEntry steam,bool steamDefault,int timeout,bool steamFirst) {
            if(windows==null || !windows.IsWindows || timeout<0 || timeout>300 || (steam!=null && !steam.IsSteam))throw new InvalidDataException("Seleção de sistemas ou tempo inválido.");
            if(steamDefault && steam==null)throw new InvalidDataException("SteamOS não foi selecionado.");
            return "; Generated by DualBoot Setup 0.2.0. ASCII.\r\n[menu]\r\ntitle=DUALBOOT\r\ntheme=night\r\ndefault="+(steamDefault?"steamos":"windows")+
                "\r\norder="+(steamFirst?"steamos,windows":"windows,steamos")+"\r\ntimeout="+timeout+"\r\n\r\n"+
                Section("windows","Windows",windows)+"\r\n"+Section("steamos","SteamOS",steam);
        }
        static string Section(string name,string label,FirmwareEntry entry) {
            return "["+name+"]\r\nlabel="+label+"\r\npartition_guid="+(entry==null?Guid.Empty:entry.PartitionGuid)+"\r\npath="+
                (entry==null?@"\EFI\steamos\steamcl.efi":entry.Path)+"\r\nsource=firmware\r\nenabled="+(entry==null?"false":"true")+"\r\n";
        }
    }
    public static class Policy {
        public static bool? EncryptionState(int? conversion,int? protection,int? result){
            if(result!=0 || !conversion.HasValue || conversion<0 || conversion>5 || !protection.HasValue || protection<0 || protection>1)return null;
            return conversion==0 && protection==0;
        }
        public static string Compatibility(ComputerState state,InstallRequest request,bool usb) {
            if(state.SecureBoot!=false)return "Secure Boot está ativo ou não pôde ser consultado. O pacote público não tem assinatura para esse modo. Sua configuração atual será preservada.";
            if(request.Windows==null || !request.Windows.Active || !request.Windows.IsWindows)return "Selecione uma entrada original e ativa do Windows.";
            if(request.Timeout<0 || request.Timeout>300 || (request.SteamDefault && request.Steam==null))return "Selecione um padrão disponível e tempo entre 0 e 300 segundos.";
            foreach(var entry in new[]{request.Windows,request.Steam}) {
                if(entry==null)continue;
                bool fresh=false;foreach(var actual in state.Entries)if(actual.Name==entry.Name && Transaction.Equal(actual.Raw,entry.Raw))fresh=true;
                if(!fresh || !entry.Active || (!entry.IsWindows && !entry.IsSteam))return "A seleção mudou ou não corresponde a um carregador original compatível. Verifique novamente.";
                var p=state.Find(entry.PartitionGuid);
                bool fat=p!=null && Array.Exists(new[]{"FAT","FAT16","FAT32"},delegate(string fs){return String.Equals(p.FileSystem,fs,StringComparison.OrdinalIgnoreCase);});
                if(p==null || !p.IsEsp || !p.IsInternal || p.IsUsb || String.IsNullOrEmpty(p.Root) || !fat)return "A partição EFI do sistema não pôde ser validada em um disco interno com sistema de arquivos FAT compatível.";
            }
            if(usb) {
                var p=request.UsbPartition;
                if(p==null || !p.IsUsb || p.IsSystem || p.IsBoot || String.IsNullOrEmpty(p.Root) || !String.Equals(p.FileSystem,"FAT32",StringComparison.OrdinalIgnoreCase))return "Escolha um pendrive USB FAT32 que não seja o disco de sistema. O assistente não formata mídias.";
                if(p.FreeBytes<32*1024*1024)return "O pendrive precisa de pelo menos 32 MiB livres.";
            } else {
                if(state.EncryptionClear!=true)return "O instalador automático no SSD exige que o Windows atual esteja totalmente sem criptografia. O estado está ativo ou não pôde ser confirmado; nenhuma proteção será alterada.";
                if(state.BootNextPending)return "Há um BootNext pendente. Conclua o reinício planejado antes de instalar.";
                var p=state.Find(request.Windows.PartitionGuid);
                if(!String.Equals(p.FileSystem,"FAT32",StringComparison.OrdinalIgnoreCase))return "A partição EFI de instalação do Windows precisa estar em FAT32. O assistente não converte nem formata partições.";
                if(!p.IsSystem)return "Selecione a entrada Windows da partição EFI usada pelo Windows atual; outras instalações não são gerenciadas automaticamente.";
                if(p.FreeBytes<32*1024*1024)return "A partição EFI precisa de pelo menos 32 MiB livres.";
            }
            return null;
        }
    }
    public static class SafeFiles {
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true,ExactSpelling=true)]static extern bool CreateDirectoryW(string path,IntPtr securityAttributes);
        public static void CreateDirectoryExclusive(string path){Plain(Path.GetDirectoryName(path));if(!CreateDirectoryW(path,IntPtr.Zero))throw new Win32Exception(Marshal.GetLastWin32Error(),"Não foi possível criar uma nova pasta exclusiva de instalação.");}
        public static string Relative(string path) {
            if(String.IsNullOrEmpty(path) || path.Length>240 || !Regex.IsMatch(path,@"\A[A-Za-z0-9_-]+(?:\.[A-Za-z0-9_-]+)?(?:\\[A-Za-z0-9_-]+(?:\.[A-Za-z0-9_-]+)?)*\z"))throw new InvalidDataException("Caminho de arquivo recusado.");
            return path;
        }
        public static string Under(string root,string relative) {
            Relative(relative);string absolute=System.IO.Path.GetFullPath(System.IO.Path.Combine(root,relative));
            string prefix=System.IO.Path.GetFullPath(root).TrimEnd('\\')+"\\";
            if(!absolute.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Caminho fora da pasta própria.");
            Plain(root);string cursor=absolute;
            while(cursor!=null && cursor.Length>=prefix.Length){Plain(cursor);cursor=System.IO.Path.GetDirectoryName(cursor);}
            return absolute;
        }
        public static void Plain(string path){if((File.Exists(path)||Directory.Exists(path)) && (File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new IOException("Link/reparse point não permitido: "+path);}
        public static string Hash(byte[] data){using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(data)).Replace("-","").ToLowerInvariant();}
        public static string HashFile(string path){using(var hash=SHA256.Create())using(var stream=File.OpenRead(path))return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","").ToLowerInvariant();}
        public static void WriteNew(string path,byte[] data) {
            using(var f=new FileStream(path,FileMode.CreateNew,FileAccess.ReadWrite,FileShare.None)){f.Write(data,0,data.Length);f.Flush(true);}
            if(HashFile(path)!=Hash(data))throw new IOException("A releitura do arquivo não confere.");
        }
    }
}

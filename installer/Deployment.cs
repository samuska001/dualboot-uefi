using System;
using System.IO;
using System.Text;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using System.IO.Compression;
using DualBoot.Setup;
namespace DualBoot.Installer {
    public sealed class FileRecord { public string Path,Hash; public long Bytes; }
    public sealed class Journal {
        public int Version=2;public string State,Id,Kind,PartitionGuid,VolumeIdentity,CreatedUtc,WindowsName,SteamName,PendingConfig,ConfigBefore,Error;
        public bool SteamDefault,SteamFirst;public int Timeout;
        public Plan FirmwarePlan;public List<FileRecord> Files=new List<FileRecord>();
        public override string ToString(){return (Kind=="SSD"?"SSD":"USB")+" · "+State+" · "+CreatedUtc;}
    }
    public static class Payload {
        public static readonly string[] Names={"DualBoot.efi","Diagnostic.efi",@"themes\night\theme.ini",@"themes\night\background.bmp",@"themes\night\windows.bmp",@"themes\night\steamos.bmp"};
        public static Dictionary<string,byte[]> ReadEmbedded() {
            var result=new Dictionary<string,byte[]>(StringComparer.OrdinalIgnoreCase);var assembly=Assembly.GetExecutingAssembly();
            string manifest;using(var reader=new StreamReader(assembly.GetManifestResourceStream("payload.hashes")))manifest=reader.ReadToEnd();
            var hashes=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            foreach(string row in manifest.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries)){
                if(row.Length<67 || row.Substring(64,2)!="  ")throw new InvalidDataException("Manifesto do instalador inválido.");
                string path=row.Substring(66).Replace('/','\\');SafeFiles.Relative(path);hashes.Add(path,row.Substring(0,64));
            }
            using(var stream=assembly.GetManifestResourceStream("payload.zip"))using(var zip=new ZipArchive(stream,ZipArchiveMode.Read)){
                if(zip.Entries.Count!=Names.Length || hashes.Count!=Names.Length)throw new InvalidDataException("Conjunto do pacote inválido.");
                foreach(var entry in zip.Entries){
                    string name=entry.FullName.Replace('/','\\');bool allowed=Array.Exists(Names,delegate(string x){return x==name;});
                    if(!allowed || entry.Length<1 || entry.Length>32*1024*1024 || !hashes.ContainsKey(name))throw new InvalidDataException("Arquivo inesperado no pacote.");
                    using(var input=entry.Open())using(var output=new MemoryStream()){input.CopyTo(output);byte[] data=output.ToArray();if(data.LongLength!=entry.Length || SafeFiles.Hash(data)!=hashes[name])throw new InvalidDataException("O pacote embutido não confere: "+name);result.Add(name,data);}
                }
            }
            return result;
        }
    }
    public static class JournalStore {
        public const int MaxBytes=1024*1024;
        public static void Save(Journal journal,string path) {
            byte[] bytes=new UTF8Encoding(false).GetBytes(new JavaScriptSerializer().Serialize(journal));
            if(bytes.Length>MaxBytes)throw new InvalidDataException("O registro de firmware excede o limite seguro de restauração; instalação automática recusada.");
            string directory=Path.GetDirectoryName(path);SafeFiles.Plain(directory);Directory.CreateDirectory(directory);SafeFiles.Plain(path);
            string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            SafeFiles.WriteNew(temp,bytes);
            if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);
        }
        public static Journal Load(string path){
            SafeFiles.Plain(path);if(new FileInfo(path).Length>MaxBytes)throw new InvalidDataException("Registro excede limite.");
            var j=new JavaScriptSerializer().Deserialize<Journal>(File.ReadAllText(path,Encoding.UTF8));Validate(j);return j;
        }
        public static void Validate(Journal j){
            if(j==null || j.Version!=2 || j.Id==null || !Regex.IsMatch(j.Id,@"\A[0-9a-f]{32}\z") || (j.Kind!="SSD" && j.Kind!="USB") ||
                !Array.Exists(new[]{"Prepared","FilesReady","Installed","Updating","Restored","FailedRolledBack","NeedsRecovery"},delegate(string s){return s==j.State;}))throw new InvalidDataException("Registro de instalação não reconhecido.");
            Guid guid;if(!Guid.TryParse(j.PartitionGuid,out guid) || (j.Kind=="SSD" && guid==Guid.Empty) || String.IsNullOrEmpty(j.VolumeIdentity))throw new InvalidDataException("Identidade do volume ausente no registro.");
            var names=new HashSet<string>(Payload.Names,StringComparer.OrdinalIgnoreCase);names.Add("dualboot.ini");names.Add("setup-owner.id");if(j.Kind=="USB")names.Add("BOOTX64.EFI");
            if(j.Files==null || j.Files.Count!=names.Count)throw new InvalidDataException("Lista de arquivos do registro inválida.");
            foreach(var r in j.Files){SafeFiles.Relative(r.Path);if(!names.Remove(r.Path) || r.Bytes<1 || r.Bytes>32*1024*1024 || r.Hash==null || !Regex.IsMatch(r.Hash,@"\A[0-9a-f]{64}\z"))throw new InvalidDataException("Arquivo inválido no registro.");}
            var owner=j.Files.Find(delegate(FileRecord r){return r.Path=="setup-owner.id";});if(owner==null || owner.Bytes!=32 || owner.Hash!=SafeFiles.Hash(Encoding.ASCII.GetBytes(j.Id)))throw new InvalidDataException("Marcador de propriedade inválido no registro.");
            if(j.Kind=="SSD" && j.FirmwarePlan==null)throw new InvalidDataException("Plano de firmware ausente.");
            if(j.Kind=="SSD"){
                Transaction.ValidatePlan(j.FirmwarePlan);
                if(j.FirmwarePlan.Esp!=guid || j.WindowsName!=j.FirmwarePlan.WindowsName)throw new InvalidDataException("A identidade do volume não corresponde ao plano de firmware.");
                if(j.SteamName!=null && (!j.FirmwarePlan.Preserved.ContainsKey(j.SteamName) || !FirmwareEntry.Parse(j.SteamName,j.FirmwarePlan.Preserved[j.SteamName]).IsSteam))throw new InvalidDataException("Entrada SteamOS inválida no registro.");
            }
        }
    }
    public sealed class InstallerEngine {
        readonly IFirmware firmware;readonly Dictionary<string,byte[]> payload;readonly string backup;
        public Action<string> AfterFile;public Action RecheckEnvironment;
        public InstallerEngine(IFirmware firmware,Dictionary<string,byte[]> payload,string backup){this.firmware=firmware;this.payload=payload;this.backup=backup;}
        string JournalPath(Journal j){return SafeFiles.Under(backup,j.Id+@"\journal.json");}
        void Save(Journal j){JournalStore.Save(j,JournalPath(j));}
        static string Own(string root,Journal j){return SafeFiles.Under(root,j.Kind=="SSD"?@"EFI\DualBoot":@"EFI\BOOT");}
        static string Stage(string root,Journal j){return SafeFiles.Under(root,(j.Kind=="SSD"?@"EFI\DualBoot-setup-":@"EFI\BOOT-setup-")+j.Id);}
        static void Snapshot(IFirmware firmware,ComputerState state,InstallRequest request){
            if(!Transaction.Equal(firmware.Read("BootOrder"),state.Order))throw new IOException("A ordem de boot mudou desde a verificação. Verifique novamente.");
            foreach(var e in new[]{request.Windows,request.Steam})if(e!=null && !Transaction.Equal(firmware.Read(e.Name),e.Raw))throw new IOException("Uma entrada original mudou desde a verificação.");
        }
        public Journal Install(ComputerState state,InstallRequest request,bool usb){
            string issue=Policy.Compatibility(state,request,usb);if(issue!=null)throw new InvalidOperationException(issue);
            Snapshot(firmware,state,request);if(RecheckEnvironment!=null)RecheckEnvironment();
            PartitionInfo partition=usb?request.UsbPartition:state.Find(request.Windows.PartitionGuid);string root=partition.Root;
            var files=new Dictionary<string,byte[]>(StringComparer.OrdinalIgnoreCase);
            foreach(string name in Payload.Names){byte[] bytes;if(!payload.TryGetValue(name,out bytes) || bytes==null || bytes.Length==0 || bytes.Length>32*1024*1024)throw new InvalidDataException("Pacote de instalação incompleto.");files.Add(name,bytes);}
            files.Add("dualboot.ini",Encoding.ASCII.GetBytes(Configuration.Build(request.Windows,request.Steam,request.SteamDefault,request.Timeout,request.SteamFirst)));
            if(usb)files.Add("BOOTX64.EFI",files["DualBoot.efi"]);
            var j=new Journal{Id=Guid.NewGuid().ToString("N"),Kind=usb?"USB":"SSD",State="Prepared",PartitionGuid=partition.Guid.ToString(),VolumeIdentity=partition.Root,
                CreatedUtc=DateTime.UtcNow.ToString("u"),WindowsName=request.Windows.Name,SteamName=request.Steam==null?null:request.Steam.Name,
                SteamDefault=request.SteamDefault,SteamFirst=request.SteamFirst,Timeout=request.Timeout,ConfigBefore=Convert.ToBase64String(files["dualboot.ini"])};
            files.Add("setup-owner.id",Encoding.ASCII.GetBytes(j.Id));
            foreach(var pair in files)j.Files.Add(new FileRecord{Path=pair.Key,Bytes=pair.Value.LongLength,Hash=SafeFiles.Hash(pair.Value)});
            string own=Own(root,j),stage=Stage(root,j);
            if(Directory.Exists(own)||File.Exists(own))throw new IOException("Já existe "+(usb?"EFI/BOOT":"EFI/DualBoot")+". A instalação existente será preservada; não há migração automática.");
            if(!usb){if(firmware.Read("BootNext")!=null)throw new IOException("BootNext pendente.");j.FirmwarePlan=Transaction.Prepare(firmware,request.Windows.Name,partition.Guid);}
            Save(j); // Durable ownership/rollback journal precedes every EFI file and NVRAM write.
            bool stageCreated=false,ownPromoted=false;
            try {
                Directory.CreateDirectory(Path.GetDirectoryName(stage));SafeFiles.CreateDirectoryExclusive(stage);stageCreated=true;
                SafeFiles.WriteNew(SafeFiles.Under(stage,"setup-owner.id"),files["setup-owner.id"]);
                foreach(var pair in files){if(pair.Key=="setup-owner.id")continue;string path=SafeFiles.Under(stage,pair.Key);Directory.CreateDirectory(Path.GetDirectoryName(path));SafeFiles.WriteNew(path,pair.Value);if(AfterFile!=null)AfterFile(pair.Key);}
                AssertTree(stage,j,false);if(RecheckEnvironment!=null)RecheckEnvironment();Snapshot(firmware,state,request);
                Directory.Move(stage,own);ownPromoted=true;j.State="FilesReady";Save(j);AssertTree(own,j,false);
                if(!usb){if(firmware.Read("BootNext")!=null)throw new IOException("BootNext mudou durante a instalação.");Transaction.Apply(firmware,j.FirmwarePlan);}
                j.State="Installed";Save(j);return j;
            } catch(Exception original){
                try {
                    if(!usb)Transaction.Rollback(firmware,j.FirmwarePlan);
                    if(ownPromoted)RemoveTree(own,j);if(stageCreated)RemoveTree(stage,j);j.State="FailedRolledBack";j.Error=original.Message;Save(j);
                } catch(Exception recovery){j.State="NeedsRecovery";j.Error=original.Message+" / "+recovery.Message;try{Save(j);}catch{}throw new AggregateException("Instalação interrompida. Preserve o registro em "+JournalPath(j)+". A recuperação precisa de revisão.",original,recovery);}
                throw new IOException("Instalação não concluída; alterações próprias revertidas. "+original.Message,original);
            }
        }
        static void AssertTree(string own,Journal j,bool allowMissing){
            if(!Directory.Exists(own)){if(allowMissing)return;throw new IOException("Pasta da instalação ausente.");}
            SafeFiles.Plain(own);string owner=SafeFiles.Under(own,"setup-owner.id");if(!File.Exists(owner) || SafeFiles.HashFile(owner)!=SafeFiles.Hash(Encoding.ASCII.GetBytes(j.Id)))throw new IOException("A pasta não tem o marcador exclusivo desta instalação. Seu conteúdo será preservado.");
            var expected=new Dictionary<string,FileRecord>(StringComparer.OrdinalIgnoreCase);
            foreach(var r in j.Files)expected.Add(SafeFiles.Under(own,r.Path),r);
            foreach(string path in Tree(own,AllowedDirectories(own,j))){
                FileRecord r;if(!expected.TryGetValue(path,out r))throw new IOException("Há conteúdo não gerenciado na pasta. Preserve-o antes de restaurar: "+Path.GetFileName(path));
                if(new FileInfo(path).Length!=r.Bytes || SafeFiles.HashFile(path)!=r.Hash)throw new IOException("Um arquivo foi modificado. Restauração automática interrompida para preservá-lo: "+r.Path);
                expected.Remove(path);
            }
            if(!allowMissing && expected.Count!=0)throw new IOException("Um arquivo da instalação está ausente.");
        }
        static HashSet<string> AllowedDirectories(string own,Journal j){
            var result=new HashSet<string>(StringComparer.OrdinalIgnoreCase);result.Add(own);
            foreach(var r in j.Files){string cursor=Path.GetDirectoryName(SafeFiles.Under(own,r.Path));while(cursor!=null && cursor.Length>=own.Length){result.Add(cursor);if(String.Equals(cursor,own,StringComparison.OrdinalIgnoreCase))break;cursor=Path.GetDirectoryName(cursor);}}
            return result;
        }
        static IEnumerable<string> Tree(string root,HashSet<string> allowed){
            SafeFiles.Plain(root);foreach(string file in Directory.GetFiles(root)){SafeFiles.Plain(file);yield return file;}
            foreach(string directory in Directory.GetDirectories(root)){SafeFiles.Plain(directory);if(!allowed.Contains(directory))throw new IOException("Uma pasta não gerenciada foi adicionada; ela será preservada.");foreach(string file in Tree(directory,allowed))yield return file;}
        }
        static void RemoveEmpty(string root,HashSet<string> allowed){foreach(string d in Directory.GetDirectories(root)){SafeFiles.Plain(d);if(!allowed.Contains(d))throw new IOException("Uma pasta não gerenciada foi adicionada durante a operação.");RemoveEmpty(d,allowed);}if(Directory.GetFileSystemEntries(root).Length==0)Directory.Delete(root,false);}
        static void RemoveTree(string own,Journal j){
            if(!Directory.Exists(own))return;AssertTree(own,j,true);
            foreach(var r in j.Files){string path=SafeFiles.Under(own,r.Path);if(File.Exists(path)){if(SafeFiles.HashFile(path)!=r.Hash)throw new IOException("Arquivo mudou antes da remoção.");File.Delete(path);}}
            RemoveEmpty(own,AllowedDirectories(own,j));
        }
        void ResolveUpdate(Journal j,string root){
            if(j.State!="Updating")return;
            string own=Own(root,j),path=SafeFiles.Under(own,"dualboot.ini"),temp=SafeFiles.Under(own,"dualboot.pending");
            var record=j.Files.Find(delegate(FileRecord f){return f.Path=="dualboot.ini";});byte[] next=Convert.FromBase64String(j.PendingConfig);
            string nextHash=SafeFiles.Hash(next);
            if(File.Exists(temp) && SafeFiles.HashFile(temp)!=nextHash)throw new IOException("Configuração temporária divergente; arquivos preservados.");
            if(!File.Exists(path)){
                byte[] previous=Convert.FromBase64String(j.ConfigBefore);if(SafeFiles.Hash(previous)!=record.Hash)throw new InvalidDataException("Backup da configuração inválido.");SafeFiles.WriteNew(path,previous);
            }
            string actual=SafeFiles.HashFile(path);
            if(actual==nextHash){record.Hash=nextHash;record.Bytes=next.Length;AdoptOptions(j,next);}else if(actual!=record.Hash)throw new IOException("Configuração mudou fora do assistente; preservar.");
            if(File.Exists(temp))File.Delete(temp);j.PendingConfig=null;j.State="Installed";Save(j);
        }
        static void AdoptOptions(Journal j,byte[] data){var options=Configuration.ReadOptions(data);j.SteamDefault=options.SteamDefault;j.SteamFirst=options.SteamFirst;j.Timeout=options.Timeout;}
        public static void PreviewPendingSettings(Journal j,string root){
            if(j.State!="Updating" || j.PendingConfig==null)return;byte[] next=Convert.FromBase64String(j.PendingConfig);string path=SafeFiles.Under(Own(root,j),"dualboot.ini");
            if(File.Exists(path) && SafeFiles.HashFile(path)==SafeFiles.Hash(next))AdoptOptions(j,next);
        }
        public void Restore(Journal j,string root){
            JournalStore.Validate(j);if(j.State=="Restored")return;
            ResolveUpdate(j,root);string own=Own(root,j),stage=Stage(root,j);AssertIfPresent(own,j);AssertIfPresent(stage,j);
            if(j.Kind=="SSD"){
                if(firmware.Read("BootNext")!=null)throw new IOException("Há um BootNext pendente. Conclua o reinício antes da restauração.");
                Transaction.Rollback(firmware,j.FirmwarePlan);
            }
            RemoveTree(own,j);RemoveTree(stage,j);j.State="Restored";Save(j);
        }
        static void AssertIfPresent(string root,Journal j){if(Directory.Exists(root))AssertTree(root,j,true);}
        public void Update(Journal j,string root,bool steamDefault,int timeout,bool steamFirst){
            JournalStore.Validate(j);if(j.Kind!="SSD" || (j.State!="Installed" && j.State!="Updating"))throw new InvalidOperationException("Somente instalações SSD gerenciadas podem ser reconfiguradas.");
            ResolveUpdate(j,root);string own=Own(root,j);AssertTree(own,j,false);
            if(!Transaction.Equal(firmware.Read("BootOrder"),j.FirmwarePlan.AfterOrder) || !Transaction.Equal(firmware.Read(j.FirmwarePlan.EntryName),j.FirmwarePlan.Entry))throw new IOException("A instalação de firmware foi alterada. Preserve a configuração atual.");
            foreach(var pair in j.FirmwarePlan.Preserved)if(!Transaction.Equal(firmware.Read(pair.Key),pair.Value))throw new IOException("Uma entrada original foi modificada.");
            FirmwareEntry windows=FirmwareEntry.Parse(j.WindowsName,j.FirmwarePlan.Preserved[j.WindowsName]);
            FirmwareEntry steam=j.SteamName==null?null:FirmwareEntry.Parse(j.SteamName,j.FirmwarePlan.Preserved[j.SteamName]);
            byte[] next=Encoding.ASCII.GetBytes(Configuration.Build(windows,steam,steamDefault,timeout,steamFirst));
            string config=SafeFiles.Under(own,"dualboot.ini"),temp=SafeFiles.Under(own,"dualboot.pending");
            if(File.Exists(temp))throw new IOException("Arquivo temporário já existe. Preserve-o e revise a instalação.");
            j.ConfigBefore=Convert.ToBase64String(File.ReadAllBytes(config));j.PendingConfig=Convert.ToBase64String(next);j.State="Updating";Save(j);
            SafeFiles.WriteNew(temp,next);File.Replace(temp,config,null);
            if(SafeFiles.HashFile(config)!=SafeFiles.Hash(next))throw new IOException("Releitura da nova configuração divergiu; registro preservado.");
            var r=j.Files.Find(delegate(FileRecord x){return x.Path=="dualboot.ini";});r.Hash=SafeFiles.Hash(next);r.Bytes=next.Length;
            j.SteamDefault=steamDefault;j.SteamFirst=steamFirst;j.Timeout=timeout;j.PendingConfig=null;j.State="Installed";Save(j);
        }
    }
}

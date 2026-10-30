using System;
using System.IO;
using System.Management;
using System.Collections.Generic;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Runtime.InteropServices;
using System.Text;
using DualBoot.Setup;
namespace DualBoot.Installer {
    public sealed class NativeServices {
        readonly NativeFirmware firmware=new NativeFirmware();
        public static string BackupRoot { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),@"DualBoot\Backups"); } }
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern uint GetFirmwareEnvironmentVariableExW(string name,string guid,byte[] buffer,uint count,out uint attributes);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool GetVolumeInformationW(string root,StringBuilder label,uint labelSize,out uint serial,out uint componentLength,out uint flags,StringBuilder fileSystem,uint fsSize);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool GetDiskFreeSpaceExW(string root,out ulong available,out ulong total,out ulong free);
        static bool ValidVolumeRoot(string root){return root!=null && System.Text.RegularExpressions.Regex.IsMatch(root,@"\A\\\\\?\\Volume\{[0-9a-fA-F-]{36}\}\\\z");}
        static void ReadVolume(PartitionInfo part,string root){
            if(!ValidVolumeRoot(root))return;
            if(part.Root!=null && !String.Equals(part.Root,root,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Mais de um volume para a partição.");
            part.Root=root;var label=new StringBuilder(260);var fs=new StringBuilder(40);uint serial,componentLength,flags;ulong available,total,free;
            if(GetVolumeInformationW(root,label,260,out serial,out componentLength,out flags,fs,40)){part.FileSystem=fs.ToString();part.Label=(part.IsUsb?"USB ":"Disco ")+part.DiskNumber+" · partição "+part.PartitionNumber+" · "+label;}
            if(GetDiskFreeSpaceExW(root,out available,out total,out free))part.FreeBytes=(long)Math.Min(available,(ulong)long.MaxValue);
        }
        static object Value(ManagementBaseObject item,string name){try{return item[name];}catch{return null;}}
        static bool Flag(ManagementBaseObject item,string name){object value=Value(item,name);return value!=null && Convert.ToBoolean(value);}
        static long Number(ManagementBaseObject item,string name){object value=Value(item,name);return value==null?0:Convert.ToInt64(value);}
        static int? OptionalNumber(ManagementBaseObject item,string name){object value=Value(item,name);return value==null?(int?)null:Convert.ToInt32(value);}
        static string Text(ManagementBaseObject item,string name){return Convert.ToString(Value(item,name));}
        static bool? SecureBootFlag(){byte[] data=new byte[1];uint attrs;uint size=GetFirmwareEnvironmentVariableExW("SecureBoot","{8BE4DF61-93CA-11D2-AA0D-00E098032B8C}",data,1,out attrs);return size==1 && data[0]<=1?(bool?)(data[0]==1):null;}
        static bool? EncryptionClear(){
            string drive=Environment.GetEnvironmentVariable("SystemDrive");
            if(drive==null || !System.Text.RegularExpressions.Regex.IsMatch(drive,@"\A[A-Za-z]:\z"))return null;
            using(var search=new ManagementObjectSearcher(@"root\CIMV2\Security\MicrosoftVolumeEncryption","SELECT * FROM Win32_EncryptableVolume WHERE DriveLetter='"+drive+"'")){
                int count=0;bool? clear=null;
                foreach(ManagementObject item in search.Get())using(item){count++;using(var status=item.InvokeMethod("GetConversionStatus",null,null)){
                    if(status==null)return null;
                    clear=Policy.EncryptionState(OptionalNumber(status,"ConversionStatus"),OptionalNumber(item,"ProtectionStatus"),OptionalNumber(status,"ReturnValue"));
                }}return count==1?clear:null;
            }
        }
        public ComputerState Scan(){
            var state=new ComputerState();
            try {
                if(!Environment.Is64BitOperatingSystem || !Environment.Is64BitProcess)throw new InvalidOperationException("É necessário Windows x64.");
                firmware.Initialize();state.SecureBoot=SecureBootFlag();state.Order=firmware.Read("BootOrder");state.BootNextPending=firmware.Read("BootNext")!=null;
                if(state.Order==null || state.Order.Data.Length<2 || state.Order.Data.Length>4096 || state.Order.Data.Length%2!=0)throw new InvalidDataException("BootOrder inválido.");
                var seen=new HashSet<ushort>();
                for(int i=0;i<state.Order.Data.Length;i+=2){ushort id=BitConverter.ToUInt16(state.Order.Data,i);if(!seen.Add(id))throw new InvalidDataException("BootOrder com duplicatas.");string name="Boot"+id.ToString("X4");
                    try{var entry=FirmwareEntry.Parse(name,firmware.Read(name));if(entry.Active && (entry.IsWindows||entry.IsSteam))state.Entries.Add(entry);}catch(Exception ex){state.Notes.Add(name+": "+ex.Message);}
                }
            }catch(Exception ex){state.Notes.Add("Leitura UEFI: "+ex.Message);}
            try{ReadPartitions(state);}catch(Exception ex){state.Notes.Add("Armazenamento: "+ex.Message);}
            try{state.EncryptionClear=EncryptionClear();}catch(Exception ex){state.Notes.Add("Criptografia não confirmada: "+ex.Message);}
            int steam=0;foreach(var entry in state.Entries)if(entry.IsSteam)steam++;
            if(steam>1)state.Notes.Add("Há várias entradas SteamOS. Selecione explicitamente a entrada original desejada.");
            foreach(var p in state.Partitions)if(p.IsSystem && !String.IsNullOrEmpty(p.Root))try{
                if(Directory.Exists(SafeFiles.Under(p.Root,@"EFI\DualBoot")))state.Notes.Add("Já existe uma instalação DualBoot. Apenas registros criados por este assistente podem ser alterados; instalações personalizadas são preservadas.");
            }catch(Exception ex){state.Notes.Add(ex.Message);}
            return state;
        }
        static void ReadPartitions(ComputerState state){
            var disks=new Dictionary<int,ManagementBaseObject>();
            using(var query=new ManagementObjectSearcher(@"root\Microsoft\Windows\Storage","SELECT * FROM MSFT_Disk"))foreach(ManagementObject disk in query.Get())disks.Add((int)Number(disk,"Number"),disk);
            try {
                using(var query=new ManagementObjectSearcher(@"root\Microsoft\Windows\Storage","SELECT * FROM MSFT_Partition"))foreach(ManagementObject row in query.Get())using(row){
                    int diskNumber=(int)Number(row,"DiskNumber");ManagementBaseObject disk;if(!disks.TryGetValue(diskNumber,out disk))continue;
                    int bus=(int)Number(disk,"BusType");Guid guid;Guid.TryParse(Text(row,"Guid"),out guid);
                    Guid type;Guid.TryParse(Text(row,"GptType"),out type);bool esp=type==new Guid("c12a7328-f81f-11d2-ba4b-00a0c93ec93b");
                    if(!esp && bus!=7)continue;
                    var part=new PartitionInfo{Guid=guid,IsEsp=esp,DiskNumber=diskNumber,PartitionNumber=(int)Number(row,"PartitionNumber"),IsUsb=bus==7,
                        IsInternal=Array.IndexOf(new[]{1,3,8,10,11,17},bus)>=0,IsSystem=Flag(row,"IsSystem")||Flag(disk,"IsSystem"),IsBoot=Flag(row,"IsBoot")||Flag(disk,"IsBoot")};
                    // IsSystem on disk is used only to exclude USB; for the Windows ESP use the partition's own flag.
                    if(esp && !part.IsUsb)part.IsSystem=Flag(row,"IsSystem");
                    using(var related=row.GetRelated("MSFT_Volume"))foreach(ManagementObject volume in related)using(volume){
                        string root=Text(volume,"Path");
                        if(ValidVolumeRoot(root)){
                            if(part.Root!=null)throw new InvalidDataException("Mais de um volume para a partição.");part.Root=root;part.FileSystem=Text(volume,"FileSystem");part.FreeBytes=Number(volume,"SizeRemaining");
                            string letter=Text(volume,"DriveLetter");part.Label=(bus==7?"USB ":"Disco ")+diskNumber+" · partição "+part.PartitionNumber+" · "+Text(volume,"FileSystemLabel")+" "+(String.IsNullOrEmpty(letter)?"":letter+":");
                        }
                    }
                    var accessPaths=Value(row,"AccessPaths") as string[];
                    if(accessPaths!=null)foreach(string root in accessPaths)if(ValidVolumeRoot(root))ReadVolume(part,root);
                    state.Partitions.Add(part);
                }
            } finally {foreach(var disk in disks.Values)disk.Dispose();}
        }
        static void ProtectBackupRoot(){
            string parent=Path.GetDirectoryName(BackupRoot);SafeFiles.Plain(parent);SafeFiles.Plain(BackupRoot);Directory.CreateDirectory(BackupRoot);
            var acl=new DirectorySecurity();acl.SetAccessRuleProtection(true,false);
            foreach(var sid in new[]{WellKnownSidType.BuiltinAdministratorsSid,WellKnownSidType.LocalSystemSid})acl.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(sid,null),FileSystemRights.FullControl,InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit,PropagationFlags.None,AccessControlType.Allow));
            acl.SetOwner(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid,null));Directory.SetAccessControl(BackupRoot,acl);
        }
        static void ValidateTargets(ComputerState state,InstallRequest request){
            foreach(var entry in new[]{request.Windows,request.Steam})if(entry!=null){var p=state.Find(entry.PartitionGuid);string file=SafeFiles.Under(p.Root,entry.Path.Substring(1));if(!File.Exists(file))throw new IOException("O carregador original não foi encontrado: "+entry.Description);}
        }
        public Journal Install(InstallRequest request,bool usb){
            var state=Scan();string issue=Policy.Compatibility(state,request,usb);if(issue!=null)throw new InvalidOperationException(issue);ValidateTargets(state,request);
            if(usb){PartitionInfo actual=null;foreach(var p in state.Partitions)if(String.Equals(p.Root,request.UsbPartition.Root,StringComparison.OrdinalIgnoreCase)){if(actual!=null)throw new IOException("Volume USB ambíguo.");actual=p;}
                if(actual==null || !actual.IsUsb || actual.IsSystem || actual.IsBoot)throw new IOException("O USB mudou desde a verificação.");request.UsbPartition=actual;}
            ProtectBackupRoot();var payload=Payload.ReadEmbedded();var engine=new InstallerEngine(firmware,payload,BackupRoot);
            string expectedRoot=usb?request.UsbPartition.Root:state.Find(request.Windows.PartitionGuid).Root;
            engine.RecheckEnvironment=delegate{
                var current=Scan();string problem=Policy.Compatibility(current,request,usb);if(problem!=null)throw new IOException(problem);ValidateTargets(current,request);
                if(usb){bool found=false;foreach(var p in current.Partitions)if(p.IsUsb && !p.IsSystem && !p.IsBoot && String.Equals(p.Root,expectedRoot,StringComparison.OrdinalIgnoreCase) && String.Equals(p.FileSystem,"FAT32",StringComparison.OrdinalIgnoreCase))found=true;if(!found)throw new IOException("O USB foi removido ou mudou.");}
                else if(!String.Equals(current.Find(request.Windows.PartitionGuid).Root,expectedRoot,StringComparison.OrdinalIgnoreCase))throw new IOException("O volume de destino mudou.");
            };
            return engine.Install(state,request,usb);
        }
        public List<Journal> Journals(List<string> notes,ComputerState state){
            var result=new List<Journal>();if(!Directory.Exists(BackupRoot))return result;SafeFiles.Plain(BackupRoot);
            foreach(string folder in Directory.GetDirectories(BackupRoot))try{
                SafeFiles.Plain(folder);string path=Path.Combine(folder,"journal.json");if(!File.Exists(path))continue;var j=JournalStore.Load(path);
                if(Path.GetFileName(folder)!=j.Id)throw new InvalidDataException("ID do backup divergente.");
                if(j.State=="Updating")try{InstallerEngine.PreviewPendingSettings(j,ResolveRoot(j,state));}catch(Exception ex){notes.Add("Configuração pendente preservada: "+ex.Message);}
                if(j.State!="Restored" && j.State!="FailedRolledBack")result.Add(j);
            }catch(Exception ex){notes.Add("Registro preservado, não carregado: "+ex.Message);}
            result.Sort(delegate(Journal a,Journal b){return String.CompareOrdinal(b.CreatedUtc,a.CreatedUtc);});return result;
        }
        string ResolveRoot(Journal j,ComputerState state){
            JournalStore.Validate(j);PartitionInfo found=null;
            foreach(var p in state.Partitions){bool match=j.Kind=="SSD"?p.Guid.ToString()==j.PartitionGuid:String.Equals(p.Root,j.VolumeIdentity,StringComparison.OrdinalIgnoreCase);
                if(match){if(found!=null)throw new IOException("Identidade de volume ambígua.");found=p;}}
            if(found==null || String.IsNullOrEmpty(found.Root) || !String.Equals(found.FileSystem,"FAT32",StringComparison.OrdinalIgnoreCase) ||
                (j.Kind=="SSD" && (!found.IsEsp || found.IsUsb || !found.IsInternal)) || (j.Kind=="USB" && (!found.IsUsb || found.IsBoot || found.IsSystem)))throw new IOException("O volume registrado não está disponível de forma compatível. Nada será removido.");
            return found.Root;
        }
        Journal Reload(Journal selected){JournalStore.Validate(selected);return JournalStore.Load(SafeFiles.Under(BackupRoot,selected.Id+@"\journal.json"));}
        public void Restore(Journal selected){var j=Reload(selected);var state=Scan();firmware.Initialize();string root=ResolveRoot(j,state);new InstallerEngine(firmware,null,BackupRoot).Restore(j,root);}
        public void Update(Journal selected,bool steamDefault,int timeout,bool steamFirst){var j=Reload(selected);var state=Scan();firmware.Initialize();string root=ResolveRoot(j,state);new InstallerEngine(firmware,null,BackupRoot).Update(j,root,steamDefault,timeout,steamFirst);}
    }
}

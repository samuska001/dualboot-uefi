using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using DualBoot.Setup;
namespace DualBoot.Installer {
 public sealed class SecureDeploymentJournal {
  public int Version=1;public string Id,SessionId,CertificateSha256,State,Kind,PartitionGuid,VolumeIdentity,CreatedUtc,Error;public bool IncludesSteam;
  public Plan FirmwarePlan;public List<SecureFileRecord> Files=new List<SecureFileRecord>();
 }
 public sealed class SecureInstallerEngine {
  readonly IFirmware firmware;readonly string backup;readonly Action<SecureBootSession,Dictionary<string,byte[]>> validatePayload;
  public Action<string> AfterFile;public Action<SecureDeploymentJournal> JournalPrepared;
  public SecureInstallerEngine(IFirmware firmware,string backup,Action<SecureBootSession,Dictionary<string,byte[]>> validator){if(validator==null)throw new ArgumentNullException("validator");this.firmware=firmware;this.backup=backup;validatePayload=validator;}
  static string Own(string root,SecureDeploymentJournal j){return SafeFiles.Under(root,j.Kind=="USB"?@"EFI\BOOT":@"EFI\DualBoot");}
  static string Stage(string root,SecureDeploymentJournal j){return SafeFiles.Under(root,(j.Kind=="USB"?@"EFI\BOOT-signed-":@"EFI\DualBoot-signed-")+j.Id);}
  string JournalPath(SecureDeploymentJournal j){return SafeFiles.Under(backup,j.Id+@"\secure-journal.json");}
  public void Save(SecureDeploymentJournal j){SaveJson(j,JournalPath(j));}
  public static void SaveJson(object value,string path){
   byte[] data=new UTF8Encoding(false).GetBytes(new JavaScriptSerializer().Serialize(value));if(data.Length>2*1024*1024)throw new InvalidDataException("Registro excede limite.");SafeFiles.Plain(Path.GetDirectoryName(path));Directory.CreateDirectory(Path.GetDirectoryName(path));SafeFiles.Plain(path);string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";SafeFiles.WriteNew(temp,data);if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);
  }
  public SecureDeploymentJournal Load(string id){if(!SecureBootWorkflow.Hex(id,32))throw new InvalidDataException("ID inválido.");string path=SafeFiles.Under(backup,id+@"\secure-journal.json");SafeFiles.Plain(path);using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read)){if(stream.Length<1||stream.Length>2*1024*1024)throw new InvalidDataException("Journal excessivo.");using(var reader=new StreamReader(stream,new UTF8Encoding(false,true))){var j=new JavaScriptSerializer{MaxJsonLength=2*1024*1024}.Deserialize<SecureDeploymentJournal>(reader.ReadToEnd());Validate(j);if(j.Id!=id)throw new InvalidDataException("ID do journal diverge.");return j;}}}
  public static void Validate(SecureDeploymentJournal j){
   Guid guid;if(j==null||j.Version!=1||!SecureBootWorkflow.Hex(j.Id,32)||!SecureBootWorkflow.Hex(j.SessionId,32)||!SecureBootWorkflow.Hex(j.CertificateSha256,64)||(j.Kind!="USB"&&j.Kind!="SSD")||!Guid.TryParse(j.PartitionGuid,out guid)||String.IsNullOrEmpty(j.VolumeIdentity))throw new InvalidDataException("Registro assinado inválido.");
   if(!Array.Exists(new[]{"Prepared","FilesReady","Installed","Restored","FailedRolledBack","NeedsRecovery"},delegate(string s){return s==j.State;}))throw new InvalidDataException("Estado do journal inválido.");
   var names=new HashSet<string>(SecureBootWorkflow.RequiredNames(j.IncludesSteam),StringComparer.OrdinalIgnoreCase);names.Add("dualboot.ini");names.Add("setup-owner.id");if(j.Kind=="USB")names.Add("BOOTX64.EFI");if(j.Files==null||j.Files.Count!=names.Count)throw new InvalidDataException("Manifesto assinado incompleto.");
   foreach(var file in j.Files){SafeFiles.Relative(file.Path);if(!names.Remove(file.Path)||!SecureBootWorkflow.Hex(file.Hash,64)||file.Bytes<1||file.Bytes>768L*1024*1024)throw new InvalidDataException("Arquivo fora do conjunto autorizado.");}
   var owner=j.Files.Find(delegate(SecureFileRecord r){return r.Path=="setup-owner.id";});if(owner.Bytes!=32||owner.Hash!=SafeFiles.Hash(Encoding.ASCII.GetBytes(j.Id)))throw new InvalidDataException("Propriedade inválida.");
   if(j.Kind=="SSD"){Transaction.ValidatePlan(j.FirmwarePlan);if(j.FirmwarePlan.Esp!=guid||guid==Guid.Empty)throw new InvalidDataException("ESP do journal diverge.");}
  }
  static void CheckEnvironment(ComputerState state,InstallRequest request,SecureBootSession session,bool usb){
   if(state.SecureBoot==null)throw new InvalidOperationException("Não foi possível confirmar Secure Boot.");if(request.Windows==null||!request.Windows.IsWindows||!request.Windows.Active||request.Windows.PartitionGuid.ToString()!=session.WindowsPartitionGuid||session.Windows.Name!=request.Windows.Name||!Transaction.Equal(session.Windows.Raw,request.Windows.Raw))throw new InvalidOperationException("Seleção Windows diverge da sessão assinada.");
   var windows=state.Find(request.Windows.PartitionGuid);if(windows==null||!windows.IsEsp||!windows.IsInternal||windows.IsUsb||windows.Root==null||!windows.IsSystem||!String.Equals(windows.FileSystem,"FAT32",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("ESP Windows incompatível.");
   bool found=false;foreach(var e in state.Entries)if(e.Name==request.Windows.Name&&e.Active&&Transaction.Equal(e.Raw,request.Windows.Raw))found=true;if(!found)throw new InvalidOperationException("Entrada Windows mudou.");
   if(usb){var p=request.UsbPartition;if(p==null||!p.IsUsb||p.IsBoot||p.IsSystem||p.Root==null||!String.Equals(p.FileSystem,"FAT32",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Selecione USB FAT32 que não seja sistema.");bool current=false;foreach(var actual in state.Partitions)if(actual.IsUsb&&!actual.IsBoot&&!actual.IsSystem&&actual.Root==p.Root&&actual.Guid==p.Guid)current=true;if(!current)throw new IOException("USB mudou desde a verificação.");}
   else if(state.SecureBoot!=true||state.EncryptionClear!=true||state.BootNextPending||!SecureBootWorkflow.Ready(session))throw new InvalidOperationException("SSD exige teste USB desta sessão, Secure Boot ativo, Windows sem criptografia e ausência de BootNext.");
  }
  void Snapshot(ComputerState state,InstallRequest request){if(!Transaction.Equal(firmware.Read("BootOrder"),state.Order)||!Transaction.Equal(firmware.Read(request.Windows.Name),request.Windows.Raw))throw new IOException("Firmware mudou desde a verificação.");}
  public SecureDeploymentJournal Install(ComputerState state,InstallRequest request,SecureBootSession session,Dictionary<string,byte[]> payload,bool usb,Action recheck){
   SecureBootWorkflow.Validate(session);CheckEnvironment(state,request,session,usb);validatePayload(session,payload);Snapshot(state,request);if(recheck!=null)recheck();
   var partition=usb?request.UsbPartition:state.Find(request.Windows.PartitionGuid);string root=partition.Root;var files=new Dictionary<string,byte[]>(payload,StringComparer.OrdinalIgnoreCase);files.Add("dualboot.ini",Encoding.ASCII.GetBytes(SecureBootWorkflow.Configuration(session,usb)));if(usb)files.Add("BOOTX64.EFI",payload["DualBoot.efi"]);
   var j=new SecureDeploymentJournal{Id=Guid.NewGuid().ToString("N"),SessionId=session.Id,CertificateSha256=session.Identity.CertificateSha256,Kind=usb?"USB":"SSD",IncludesSteam=session.IncludesSteam,State="Prepared",PartitionGuid=partition.Guid.ToString(),VolumeIdentity=root,CreatedUtc=DateTime.UtcNow.ToString("o")};files.Add("setup-owner.id",Encoding.ASCII.GetBytes(j.Id));j.Files=SecureBootWorkflow.Records(files);
   long total=0;foreach(var f in files.Values)total=checked(total+f.LongLength);if(partition.FreeBytes<total+16*1024*1024)throw new IOException("Espaço insuficiente para o pacote assinado.");string own=Own(root,j),stage=Stage(root,j);if(Directory.Exists(own)||File.Exists(own))throw new IOException("Já existe uma instalação neste destino. Instalações personalizadas são preservadas; restaure somente a instalação gerenciada antes de substituí-la.");
   if(!usb){if(firmware.Read("BootNext")!=null)throw new IOException("BootNext pendente.");j.FirmwarePlan=Transaction.Prepare(firmware,request.Windows.Name,partition.Guid);}Validate(j);Save(j);if(JournalPrepared!=null)JournalPrepared(j);bool promoted=false,created=false;
   try{Directory.CreateDirectory(Path.GetDirectoryName(stage));SafeFiles.CreateDirectoryExclusive(stage);created=true;SafeFiles.WriteNew(SafeFiles.Under(stage,"setup-owner.id"),files["setup-owner.id"]);
    foreach(var pair in files){if(pair.Key=="setup-owner.id")continue;string path=SafeFiles.Under(stage,pair.Key);Directory.CreateDirectory(Path.GetDirectoryName(path));SafeFiles.WriteNew(path,pair.Value);if(AfterFile!=null)AfterFile(pair.Key);}
    VerifyTree(stage,j,false);if(recheck!=null)recheck();Snapshot(state,request);Directory.Move(stage,own);promoted=true;j.State="FilesReady";Save(j);VerifyTree(own,j,false);
    if(!usb){if(firmware.Read("BootNext")!=null)throw new IOException("BootNext mudou durante a instalação.");Transaction.Apply(firmware,j.FirmwarePlan);}j.State="Installed";Save(j);return j;
   }catch(Exception error){try{if(!usb)Transaction.Rollback(firmware,j.FirmwarePlan);if(promoted)RemoveTree(own,j);if(created)RemoveTree(stage,j);j.State="FailedRolledBack";j.Error=error.Message;Save(j);}catch(Exception restore){j.State="NeedsRecovery";j.Error=error.Message+" / "+restore.Message;try{Save(j);}catch{}throw new AggregateException("Recuperação necessária; preserve "+JournalPath(j),error,restore);}throw;}
  }
  static void VerifyReport(string path,SecureDeploymentJournal j){
   using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read)){if(stream.Length>8192)throw new IOException("Relatório excessivo; preservar.");using(var reader=new StreamReader(stream,Encoding.ASCII)){var f=SecureBootWorkflow.Fields(reader.ReadToEnd(),new[]{"Format","SessionId","CertificateSHA256","SecureBoot","SetupMode","LocalCertificateInMokListRT","ShimProtocolPresent","ShimValidationDisabled"});if(f["Format"]!="DualBootSecureBootStatusV2"||f["SessionId"]!=j.SessionId||f["CertificateSHA256"]!=j.CertificateSha256)throw new IOException("Relatório pertence a outra sessão; preservar.");}}
  }
  static HashSet<string> Directories(string own,SecureDeploymentJournal j){var dirs=new HashSet<string>(StringComparer.OrdinalIgnoreCase);dirs.Add(own);foreach(var file in j.Files){string cursor=Path.GetDirectoryName(SafeFiles.Under(own,file.Path));while(cursor!=null&&cursor.Length>=own.Length){dirs.Add(cursor);if(cursor==own)break;cursor=Path.GetDirectoryName(cursor);}}return dirs;}
  static IEnumerable<string> Tree(string own,HashSet<string> allowed){SafeFiles.Plain(own);foreach(var file in Directory.GetFiles(own)){SafeFiles.Plain(file);yield return file;}foreach(var dir in Directory.GetDirectories(own)){SafeFiles.Plain(dir);if(!allowed.Contains(dir))throw new IOException("Diretório alheio encontrado; preservar.");foreach(var file in Tree(dir,allowed))yield return file;}}
  public static void VerifyTree(string own,SecureDeploymentJournal j,bool allowMissing){
   if(!Directory.Exists(own)){if(allowMissing)return;throw new IOException("Instalação ausente.");}SafeFiles.Plain(own);string owner=SafeFiles.Under(own,"setup-owner.id");if(!File.Exists(owner)||SafeFiles.HashFile(owner)!=SafeFiles.Hash(Encoding.ASCII.GetBytes(j.Id)))throw new IOException("Marcador de propriedade diverge; preservar.");
   var expected=new Dictionary<string,SecureFileRecord>(StringComparer.OrdinalIgnoreCase);foreach(var r in j.Files)expected.Add(SafeFiles.Under(own,r.Path),r);
   foreach(var file in Tree(own,Directories(own,j))){SecureFileRecord r;if(expected.TryGetValue(file,out r)){if(new FileInfo(file).Length!=r.Bytes||SafeFiles.HashFile(file)!=r.Hash)throw new IOException("Arquivo alterado; preservar: "+r.Path);expected.Remove(file);}else if(file==SafeFiles.Under(own,"secureboot-status.txt"))VerifyReport(file,j);else throw new IOException("Arquivo alheio encontrado; preservar: "+Path.GetFileName(file));}
   if(!allowMissing&&expected.Count!=0)throw new IOException("Arquivos gerenciados ausentes.");
  }
  static void RemoveEmpty(string root,HashSet<string> dirs){foreach(var d in Directory.GetDirectories(root)){SafeFiles.Plain(d);if(!dirs.Contains(d))throw new IOException("Diretório mudou durante a restauração.");RemoveEmpty(d,dirs);}if(Directory.GetFileSystemEntries(root).Length==0)Directory.Delete(root,false);}
  static void RemoveTree(string own,SecureDeploymentJournal j){if(!Directory.Exists(own))return;VerifyTree(own,j,true);foreach(var f in j.Files){string path=SafeFiles.Under(own,f.Path);if(File.Exists(path)){if(SafeFiles.HashFile(path)!=f.Hash)throw new IOException("Arquivo mudou antes da remoção.");File.Delete(path);}}string report=SafeFiles.Under(own,"secureboot-status.txt");if(File.Exists(report)){VerifyReport(report,j);File.Delete(report);}RemoveEmpty(own,Directories(own,j));}
  public void Verify(SecureDeploymentJournal j,string root){Validate(j);VerifyTree(Own(root,j),j,false);if(j.Kind=="SSD"){if(!Transaction.Equal(firmware.Read("BootOrder"),j.FirmwarePlan.AfterOrder)||!Transaction.Equal(firmware.Read(j.FirmwarePlan.EntryName),j.FirmwarePlan.Entry))throw new IOException("Firmware diverge da instalação.");foreach(var pair in j.FirmwarePlan.Preserved)if(!Transaction.Equal(firmware.Read(pair.Key),pair.Value))throw new IOException("Entrada original mudou.");}}
  public void Restore(SecureDeploymentJournal j,string root){
   Validate(j);if(j.State=="Restored")return;string own=Own(root,j),stage=Stage(root,j);VerifyTree(own,j,true);VerifyTree(stage,j,true);if(j.Kind=="SSD"){if(firmware.Read("BootNext")!=null)throw new IOException("BootNext pendente.");Transaction.Rollback(firmware,j.FirmwarePlan);}RemoveTree(own,j);RemoveTree(stage,j);j.State="Restored";Save(j);
  }
 }
}

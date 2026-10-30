using System;
using System.IO;
using System.Text;
using System.Reflection;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
namespace DualBoot.Installer {
 public sealed class SecureFileRecord {public string Path,Hash;public long Bytes;}
 public sealed class SecureBootSession {
  public int Version=1;public string Id,State,CreatedUtc,PreparedUtc,WindowsPartitionGuid,RootPartitionGuid,CollectionDirectory,UsbJournalId,SsdJournalId;
  public string WindowsLoaderHash,SteamLoaderHash,FirmwareReportHash,LinuxReportHash,SlotPartitionGuid,CollectionManifestHash;
  public bool IncludesSteam,FirmwareTestVerified,WindowsUserConfirmed,LinuxTestVerified;
  public FirmwareEntry Windows,Steam;public int Timeout;public bool SteamDefault,SteamFirst;
  public SigningIdentity Identity;public List<SecureFileRecord> Files=new List<SecureFileRecord>();
  public override string ToString(){return Id.Substring(0,8)+" · "+State+" · "+(IncludesSteam?"Windows e SteamOS":"somente Windows");}
 }
 public static class SecureBootWorkflow {
  public static readonly string[] BaseNames={"DualBoot.efi","mmx64.efi","grubx64.efi","Diagnostic.efi","DualBoot.cer",@"themes\night\theme.ini",@"themes\night\background.bmp",@"themes\night\windows.bmp",@"themes\night\steamos.bmp"};
  public static string[] RequiredNames(bool steam){var result=new List<string>(BaseNames);if(steam)result.Add("steam-slot.efi");return result.ToArray();}
  public static bool Hex(string value,int count){return value!=null && Regex.IsMatch(value,"\\A[0-9a-f]{"+count+"}\\z");}
  public static bool Ready(SecureBootSession s){return s!=null && s.State!="Preparing" && s.State!="Restored" && s.State!="NeedsRecovery" && s.FirmwareTestVerified && s.WindowsUserConfirmed && (!s.IncludesSteam || s.LinuxTestVerified);}
  public static void ReconcileState(SecureBootSession s,bool installed,bool recovering){if(recovering){s.State="NeedsRecovery";return;}if(installed){s.State="Installed";return;}if(s.State=="Preparing"&&s.Files.Count==0)return;s.State="AwaitingUsbTest";if(Ready(s))s.State="ReadyToInstall";}
  public static List<SecureFileRecord> Records(Dictionary<string,byte[]> payload){var result=new List<SecureFileRecord>();foreach(var p in payload)result.Add(new SecureFileRecord{Path=p.Key,Bytes=p.Value.LongLength,Hash=SafeFiles.Hash(p.Value)});return result;}
  public static void MatchRecords(SecureBootSession s,Dictionary<string,byte[]> payload){
   var allowed=new HashSet<string>(RequiredNames(s.IncludesSteam),StringComparer.OrdinalIgnoreCase);if(s.Files==null || s.Files.Count!=allowed.Count || payload.Count!=allowed.Count)throw new InvalidDataException("Pacote assinado incompleto.");
   foreach(var r in s.Files){SafeFiles.Relative(r.Path);byte[] data;if(!allowed.Remove(r.Path)||!payload.TryGetValue(r.Path,out data)||data.LongLength!=r.Bytes||SafeFiles.Hash(data)!=r.Hash)throw new InvalidDataException("O pacote mudou desde a preparação: "+r.Path);}
  }
  public static Dictionary<string,string> Fields(string text,string[] names){
   if(text==null||text.Length>8192)throw new InvalidDataException("Relatório ausente ou excessivo.");var allowed=new HashSet<string>(names,StringComparer.Ordinal);var values=new Dictionary<string,string>(StringComparer.Ordinal);
   foreach(char c in text)if(c>127||c==0)throw new InvalidDataException("Relatório não é ASCII.");
   foreach(var line in text.Split(new[]{'\n'},StringSplitOptions.RemoveEmptyEntries)){string row=line.TrimEnd('\r');int eq=row.IndexOf('=');if(eq<1)throw new InvalidDataException("Linha de relatório inválida.");string key=row.Substring(0,eq);if(!allowed.Remove(key))throw new InvalidDataException("Campo duplicado ou desconhecido: "+key);values.Add(key,row.Substring(eq+1));}
   if(allowed.Count!=0)throw new InvalidDataException("Relatório incompleto.");return values;
  }
  static void Fresh(SecureBootSession s,DateTime stamp){DateTime prepared;if(!DateTime.TryParse(s.PreparedUtc,null,System.Globalization.DateTimeStyles.RoundtripKind,out prepared)||stamp.ToUniversalTime()<prepared.ToUniversalTime()||stamp.ToUniversalTime()>DateTime.UtcNow.AddMinutes(5))throw new InvalidDataException("Relatório antigo ou com data inválida; execute o teste deste pacote.");}
  public static void AcceptFirmwareReport(SecureBootSession s,string text,DateTime stamp){
   Fresh(s,stamp);var f=Fields(text,new[]{"Format","SessionId","CertificateSHA256","SecureBoot","SetupMode","LocalCertificateInMokListRT","ShimProtocolPresent","ShimValidationDisabled"});
   if(f["Format"]!="DualBootSecureBootStatusV2"||f["SessionId"]!=s.Id||f["CertificateSHA256"]!=s.Identity.CertificateSha256||f["SecureBoot"]!="1"||f["SetupMode"]!="0"||f["LocalCertificateInMokListRT"]!="1"||f["ShimProtocolPresent"]!="1"||f["ShimValidationDisabled"]!="0")throw new InvalidDataException("Relatório não confirma sessão, MOK, shim e Secure Boot esperados.");
   s.FirmwareTestVerified=true;s.FirmwareReportHash=SafeFiles.Hash(Encoding.ASCII.GetBytes(text));
  }
  public static void AcceptLinuxReport(SecureBootSession s,string text,DateTime stamp){
   Fresh(s,stamp);var f=Fields(text,new[]{"Format","SessionId","RootPartitionGuid","SecureBoot","LockdownIntegrity","ModuleSignaturesEnforced","BootedSignedBundle"});
   if(!s.IncludesSteam||f["Format"]!="DualBootLinuxStatusV1"||f["SessionId"]!=s.Id||!String.Equals(f["RootPartitionGuid"],s.RootPartitionGuid,StringComparison.OrdinalIgnoreCase)||f["SecureBoot"]!="1"||f["LockdownIntegrity"]!="1"||f["ModuleSignaturesEnforced"]!="1"||f["BootedSignedBundle"]!="1")throw new InvalidDataException("O SteamOS não confirmou este pacote com lockdown e assinaturas obrigatórias.");
   s.LinuxTestVerified=true;s.LinuxReportHash=SafeFiles.Hash(Encoding.ASCII.GetBytes(text));
  }
  public static string Configuration(SecureBootSession s,bool usb){
   string config=global::DualBoot.Installer.Configuration.Build(s.Windows,null,false,s.Timeout,s.SteamFirst);int start=config.IndexOf("[steamos]",StringComparison.Ordinal);config=config.Substring(0,start);
   if(s.SteamDefault&&s.IncludesSteam)config=config.Replace("default=windows","default=steamos");
   return config+"[steamos]\r\nlabel=SteamOS\r\nsource=file\r\npath=\\EFI\\"+(usb?"BOOT":"DualBoot")+"\\steam-slot.efi\r\nenabled="+(s.IncludesSteam?"true":"false")+"\r\n";
  }
  public static void Validate(SecureBootSession s){
   Guid guid;if(s==null||s.Version!=1||!Hex(s.Id,32)||s.Id==new string('0',32)||!Guid.TryParse(s.WindowsPartitionGuid,out guid)||guid==Guid.Empty||s.Windows==null||!s.Windows.IsWindows||s.Windows.PartitionGuid!=guid||s.Identity==null||!Hex(s.Identity.CertificateSha256,64)||s.Identity.CertificateBase64==null||s.Identity.CertificateBase64.Length>6000)throw new InvalidDataException("Sessão assinada inválida.");
   if(!Array.Exists(new[]{"Preparing","AwaitingUsbTest","ReadyToInstall","Installed","Restored","NeedsRecovery"},delegate(string v){return v==s.State;}))throw new InvalidDataException("Estado da sessão inválido.");
   if(s.IncludesSteam && (s.Steam==null||!s.Steam.IsSteam||!Guid.TryParse(s.RootPartitionGuid,out guid)||guid==Guid.Empty))throw new InvalidDataException("Identidade SteamOS ausente.");
   if(s.Timeout<0||s.Timeout>300||(!s.IncludesSteam && s.SteamDefault))throw new InvalidDataException("Opções inválidas.");
   foreach(var id in new[]{s.UsbJournalId,s.SsdJournalId})if(id!=null&&!Hex(id,32))throw new InvalidDataException("Registro de transação inválido.");
   var names=new HashSet<string>(RequiredNames(s.IncludesSteam),StringComparer.OrdinalIgnoreCase);if(s.State=="Preparing"&&s.Files!=null&&s.Files.Count==0)return;if(s.Files==null||s.Files.Count!=names.Count)throw new InvalidDataException("Manifesto de sessão incompleto.");
   foreach(var file in s.Files){SafeFiles.Relative(file.Path);if(!names.Remove(file.Path)||!Hex(file.Hash,64)||file.Bytes<1||file.Bytes>768L*1024*1024)throw new InvalidDataException("Arquivo inválido na sessão.");}
  }
 }
 public static class PeBinding {
  const string Magic="DUALBOOT_SB_BINDING_V2_20261030!";
  static int Offset(byte[] pe,string name){
   if(pe.Length<256||pe[0]!='M'||pe[1]!='Z')throw new InvalidDataException("Template PE inválido.");int p=BitConverter.ToInt32(pe,60);if(p<64||p>pe.Length-256||BitConverter.ToUInt32(pe,p)!=0x4550||BitConverter.ToUInt16(pe,p+4)!=0x8664)throw new InvalidDataException("PE x64 inválido.");int count=BitConverter.ToUInt16(pe,p+6),table=p+24+BitConverter.ToUInt16(pe,p+20),found=-1;
   if(count<1||count>32||table>pe.Length-count*40)throw new InvalidDataException("Seções PE inválidas.");for(int i=0;i<count;i++){int at=table+i*40;if(Encoding.ASCII.GetString(pe,at,8).TrimEnd('\0')!=name)continue;int offset=BitConverter.ToInt32(pe,at+20),raw=BitConverter.ToInt32(pe,at+16);if(found>=0||offset<0||raw<4228||offset>pe.Length-raw)throw new InvalidDataException("Binding ausente ou ambíguo.");found=offset;}
   if(found<0)throw new InvalidDataException("Template sem binding.");return found;
  }
  public static byte[] Patch(byte[] template,string session,string certificateBase64){
   byte[] cert=Convert.FromBase64String(certificateBase64);if(cert.Length<1||cert.Length>4096||!SecureBootWorkflow.Hex(session,32)||session==new string('0',32))throw new InvalidDataException("Certificado ou sessão inválidos.");byte[] output=(byte[])template.Clone();int at=Offset(output,".dbbind");if(Encoding.ASCII.GetString(output,at,32)!=Magic)throw new InvalidDataException("Magic do binding inválido.");for(int i=32;i<4228;i++)if(output[at+i]!=0)throw new InvalidDataException("Template já personalizado.");
   Array.Copy(BitConverter.GetBytes(cert.Length),0,output,at+32,4);Array.Copy(Encoding.ASCII.GetBytes(session),0,output,at+36,32);Array.Copy(Encoding.ASCII.GetBytes(SafeFiles.Hash(cert)),0,output,at+68,64);Array.Copy(cert,0,output,at+132,cert.Length);return output;
  }
  public static byte[] EnsureSbat(byte[] input){
   int pe=BitConverter.ToInt32(input,60),count=BitConverter.ToUInt16(input,pe+6),opt=pe+24,table=opt+BitConverter.ToUInt16(input,pe+20);int rawStart=input.Length,rawEnd=0,virtualEnd=0;
   for(int i=0;i<count;i++){int at=table+i*40;if(Encoding.ASCII.GetString(input,at,8).TrimEnd('\0')==".sbat")return input;int size=BitConverter.ToInt32(input,at+16),raw=BitConverter.ToInt32(input,at+20),v=BitConverter.ToInt32(input,at+12),vs=BitConverter.ToInt32(input,at+8);if(size>0)rawStart=Math.Min(rawStart,raw);rawEnd=Math.Max(rawEnd,checked(raw+size));virtualEnd=Math.Max(virtualEnd,checked(v+Math.Max(vs,size)));}
   int sectionAlign=BitConverter.ToInt32(input,opt+32),fileAlign=BitConverter.ToInt32(input,opt+36);if(count<1||count>=32||table+(count+1)*40>rawStart||fileAlign<512||fileAlign>65536||sectionAlign<fileAlign||sectionAlign>1048576||input.Length!=rawEnd)throw new InvalidDataException("Sem espaço seguro para SBAT no candidato GRUB.");
   byte[] data=Encoding.ASCII.GetBytes("sbat,1,SBAT Version,sbat,1,https://github.com/rhboot/shim/blob/main/SBAT.md\ndualboot-grub,1,DualBoot,dualboot-grub,0.3.0,https://github.com/samuska001/dualboot-uefi\n");int pos=Align(rawEnd,fileAlign),rawSize=Align(data.Length,fileAlign),va=Align(virtualEnd,sectionAlign);byte[] output=new byte[checked(pos+rawSize)];Array.Copy(input,output,input.Length);int header=table+count*40;Array.Clear(output,header,40);Array.Copy(Encoding.ASCII.GetBytes(".sbat"),0,output,header,5);Write(output,header+8,data.Length);Write(output,header+12,va);Write(output,header+16,rawSize);Write(output,header+20,pos);Write(output,header+36,0x40000040);Array.Copy(BitConverter.GetBytes((ushort)(count+1)),0,output,pe+6,2);Write(output,opt+56,Align(va+data.Length,sectionAlign));Write(output,opt+8,checked(BitConverter.ToInt32(output,opt+8)+rawSize));Array.Copy(data,0,output,pos,data.Length);return output;
  }
  static int Align(int value,int align){return checked((value+align-1)/align*align);}static void Write(byte[] output,int at,int value){Array.Copy(BitConverter.GetBytes(value),0,output,at,4);}
 }
 public static class SecureResources {
  public static byte[] Read(string name){using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("secure."+name)){if(stream==null)throw new InvalidDataException("Recurso Secure Boot ausente: "+name);using(var output=new MemoryStream()){stream.CopyTo(output);if(output.Length>8*1024*1024)throw new InvalidDataException("Recurso excessivo.");return output.ToArray();}}}
 }
}

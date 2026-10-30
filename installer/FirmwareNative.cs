namespace DualBoot.Setup {
    using System;
    using System.ComponentModel;
    using System.Runtime.InteropServices;
    using System.Text.RegularExpressions;
    public sealed class NativeFirmware : IFirmware {
        const string GlobalGuid="{8BE4DF61-93CA-11D2-AA0D-00E098032B8C}";
        [StructLayout(LayoutKind.Sequential)] struct Luid { public uint Low; public int High; }
        [StructLayout(LayoutKind.Sequential)] struct Privilege { public uint Count; public Luid Id; public uint Attributes; }
        [DllImport("kernel32.dll")] static extern IntPtr GetCurrentProcess();
        [DllImport("kernel32.dll",SetLastError=true)] static extern bool CloseHandle(IntPtr handle);
        [DllImport("advapi32.dll",SetLastError=true)] static extern bool OpenProcessToken(IntPtr process,uint access,out IntPtr token);
        [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool LookupPrivilegeValue(string system,string name,out Luid id);
        [DllImport("advapi32.dll",SetLastError=true)] static extern bool AdjustTokenPrivileges(IntPtr token,bool disable,ref Privilege state,uint size,IntPtr previous,IntPtr required);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern uint GetFirmwareEnvironmentVariableExW(string name,string guid,byte[] data,uint size,out uint attributes);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool SetFirmwareEnvironmentVariableExW(string name,string guid,byte[] data,uint size,uint attributes);
        static bool IsEntry(string name) { return name!=null && Regex.IsMatch(name,@"\ABoot[0-9A-F]{4}\z"); }
        public void Initialize() {
            IntPtr token;
            if (!OpenProcessToken(GetCurrentProcess(),0x28,out token)) throw new Win32Exception(Marshal.GetLastWin32Error());
            try {
                Luid id;
                if (!LookupPrivilegeValue(null,"SeSystemEnvironmentPrivilege",out id)) throw new Win32Exception(Marshal.GetLastWin32Error());
                Privilege p=new Privilege{Count=1,Id=id,Attributes=2};
                if (!AdjustTokenPrivileges(token,false,ref p,0,IntPtr.Zero,IntPtr.Zero)) throw new Win32Exception(Marshal.GetLastWin32Error());
                int error=Marshal.GetLastWin32Error(); if (error!=0) throw new Win32Exception(error);
            } finally { CloseHandle(token); }
        }
        public Variable Read(string name) {
            if (!IsEntry(name) && name!="BootOrder" && name!="BootCurrent" && name!="BootNext") throw new ArgumentException("Variavel fora do escopo");
            byte[] data=new byte[65536]; uint attributes;
            uint size=GetFirmwareEnvironmentVariableExW(name,GlobalGuid,data,(uint)data.Length,out attributes);
            if (size==0) {
                int error=Marshal.GetLastWin32Error();
                if (error==203) return null;
                throw new Win32Exception(error,"Leitura de "+name);
            }
            Array.Resize(ref data,(int)size); return new Variable(attributes,data);
        }
        public void Write(string name,Variable value) {
            if (!IsEntry(name) && name!="BootOrder") throw new ArgumentException("Escrita fora do escopo");
            if (value==null && name=="BootOrder") throw new ArgumentException("Remocao de BootOrder proibida");
            if (value!=null && (value.Attributes!=7 || value.Data==null || value.Data.Length==0 || value.Data.Length>65536)) throw new ArgumentException("Atributos ou tamanho invalidos");
            byte[] data=value==null?null:value.Data;
            if (!SetFirmwareEnvironmentVariableExW(name,GlobalGuid,data,data==null?0:(uint)data.Length,7)) throw new Win32Exception(Marshal.GetLastWin32Error(),"Escrita de "+name);
        }
    }
}

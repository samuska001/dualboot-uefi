namespace DualBoot.Setup {
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    public sealed class Variable {
        public uint Attributes;
        public byte[] Data;
        public Variable() {} public Variable(uint attributes, byte[] data) { Attributes=attributes; Data=data; }
    }
    public interface IFirmware {
        Variable Read(string name);
        void Write(string name, Variable value);
    }
    public sealed class Plan {
        public string EntryName;
        public string WindowsName;
        public Guid Esp;
        public Variable Entry, BeforeOrder, AfterOrder;
        public Dictionary<string, Variable> Preserved = new Dictionary<string, Variable>();
    }
    public static class Transaction {
        public static bool Equal(Variable a, Variable b) {
            if (a==null || b==null) return a==b;
            if (a.Attributes!=b.Attributes || a.Data.Length!=b.Data.Length) return false;
            for (int i=0;i<a.Data.Length;i++) if (a.Data[i]!=b.Data[i]) return false;
            return true;
        }
        static void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
        public static byte[] BuildOption(byte[] windowsOption, Guid esp) {
            Require(windowsOption!=null && windowsOption.Length>=8,"Load option truncada");
            Require((BitConverter.ToUInt32(windowsOption,0)&1)!=0,"Windows inativo");
            int length=BitConverter.ToUInt16(windowsOption,4), start=6;
            while (start+1<windowsOption.Length && (windowsOption[start]!=0 || windowsOption[start+1]!=0)) start+=2;
            Require(start+1<windowsOption.Length,"Descricao sem terminador");
            start+=2;
            Require(length>=46 && length<=windowsOption.Length-start,"Caminho truncado");
            int end=start+length, cursor=start;
            Require(windowsOption[cursor]==4 && windowsOption[cursor+1]==1 && BitConverter.ToUInt16(windowsOption,cursor+2)==42,"Esperado caminho HD GPT curto");
            byte[] hd=new byte[42]; Array.Copy(windowsOption,cursor,hd,0,42); cursor+=42;
            Require(hd[40]==2 && hd[41]==2,"Assinatura GPT ausente");
            byte[] signature=new byte[16]; Array.Copy(hd,24,signature,0,16);
            Require(new Guid(signature)==esp,"ESP diferente da autorizada");
            Require(cursor+4<=end && windowsOption[cursor]==4 && windowsOption[cursor+1]==4,"Esperado FILEPATH");
            int fileLength=BitConverter.ToUInt16(windowsOption,cursor+2);
            Require(fileLength>=6 && (fileLength&1)==0 && fileLength<=end-cursor,"FILEPATH invalido");
            Require(windowsOption[cursor+fileLength-2]==0 && windowsOption[cursor+fileLength-1]==0,"FILEPATH sem terminador");
            string path=Encoding.Unicode.GetString(windowsOption,cursor+4,fileLength-6);
            Require(path.Equals(@"\EFI\Microsoft\Boot\bootmgfw.efi",StringComparison.OrdinalIgnoreCase),"Carregador original diferente do Windows");
            cursor+=fileLength;
            Require(cursor+4==end && windowsOption[cursor]==127 && windowsOption[cursor+1]==255 && BitConverter.ToUInt16(windowsOption,cursor+2)==4,"Fim de caminho invalido");
            byte[] description=Encoding.Unicode.GetBytes("DualBoot\0"), file=Encoding.Unicode.GetBytes("\\EFI\\DualBoot\\DualBoot.efi\0");
            using (var stream=new MemoryStream()) using (var writer=new BinaryWriter(stream)) {
                writer.Write((uint)1); writer.Write((ushort)(42+4+file.Length+4)); writer.Write(description);
                writer.Write(hd); writer.Write((byte)4); writer.Write((byte)4); writer.Write((ushort)(4+file.Length)); writer.Write(file);
                writer.Write(new byte[]{127,255,4,0});
                return stream.ToArray();
            }
        }
        static ushort[] DecodeOrder(Variable value) {
            Require(value!=null && value.Attributes==7 && value.Data.Length>0 && value.Data.Length<=4096 && (value.Data.Length&1)==0,"BootOrder invalido");
            var result=new ushort[value.Data.Length/2]; var seen=new HashSet<ushort>();
            for (int i=0;i<result.Length;i++) {
                result[i]=BitConverter.ToUInt16(value.Data,i*2);
                Require(seen.Add(result[i]),"BootOrder com duplicata");
            }
            return result;
        }
        public static Plan Prepare(IFirmware firmware, string windowsName, Guid esp) {
            var plan=new Plan(); plan.BeforeOrder=firmware.Read("BootOrder"); plan.WindowsName=windowsName; plan.Esp=esp;
            var order=DecodeOrder(plan.BeforeOrder);
            foreach (ushort id in order) {
                string name="Boot"+id.ToString("X4"); var value=firmware.Read(name);
                Require(value!=null,"Entrada da ordem ausente: "+name); plan.Preserved.Add(name,value);
            }
            Require(plan.Preserved.ContainsKey(windowsName),"Windows fora da ordem");
            byte[] entry=BuildOption(plan.Preserved[windowsName].Data,esp);
            int free=-1;
            for (int i=0;i<=65535;i++) {
                string name="Boot"+i.ToString("X4");
                if (!plan.Preserved.ContainsKey(name) && firmware.Read(name)==null) { free=i; break; }
            }
            Require(free>=0,"Nenhuma entrada livre"); plan.EntryName="Boot"+free.ToString("X4");
            plan.Entry=new Variable(7,entry);
            byte[] after=new byte[plan.BeforeOrder.Data.Length+2];
            Array.Copy(BitConverter.GetBytes((ushort)free),after,2); Array.Copy(plan.BeforeOrder.Data,0,after,2,plan.BeforeOrder.Data.Length);
            plan.AfterOrder=new Variable(plan.BeforeOrder.Attributes,after);
            ValidatePlan(plan);
            return plan;
        }
        public static void ValidatePlan(Plan plan) {
            Require(plan!=null && plan.EntryName!=null && System.Text.RegularExpressions.Regex.IsMatch(plan.EntryName,@"\ABoot[0-9A-F]{4}\z"),"Plano/entrada invalido");
            var before=DecodeOrder(plan.BeforeOrder); var after=DecodeOrder(plan.AfterOrder);
            ushort id=Convert.ToUInt16(plan.EntryName.Substring(4),16);
            Require(after.Length==before.Length+1 && after[0]==id,"Ordem instalada inconsistente");
            Require(plan.Preserved!=null && plan.Preserved.Count==before.Length && !plan.Preserved.ContainsKey(plan.EntryName),"Conjunto preservado inconsistente");
            for (int i=0;i<before.Length;i++) {
                string name="Boot"+before[i].ToString("X4");
                Require(before[i]!=id && after[i+1]==before[i],"Ordem original inconsistente");
                Require(plan.Preserved.ContainsKey(name) && plan.Preserved[name]!=null && plan.Preserved[name].Attributes==7,"Entrada original ausente/invalida no plano");
            }
            Require(plan.WindowsName!=null && plan.Preserved.ContainsKey(plan.WindowsName) && plan.Esp!=Guid.Empty,"Origem Windows/ESP ausente no plano");
            var expected=new Variable(7,BuildOption(plan.Preserved[plan.WindowsName].Data,plan.Esp));
            Require(Equal(plan.Entry,expected),"Destino DualBoot inconsistente no plano");
        }
        static void CheckPreserved(IFirmware firmware, Plan plan) {
            foreach (var pair in plan.Preserved) Require(Equal(firmware.Read(pair.Key),pair.Value),"Entrada original mudou: "+pair.Key);
        }
        static void VerifiedWrite(IFirmware firmware, string name, Variable value) {
            firmware.Write(name,value);
            Require(Equal(firmware.Read(name),value),"Releitura divergente: "+name);
        }
        public static void Apply(IFirmware firmware, Plan plan) {
            ValidatePlan(plan);
            CheckPreserved(firmware,plan);
            Require(Equal(firmware.Read("BootOrder"),plan.BeforeOrder),"Ordem mudou antes da instalacao");
            Require(firmware.Read(plan.EntryName)==null,"Entrada deixou de estar livre");
            try {
                VerifiedWrite(firmware,plan.EntryName,plan.Entry);
                CheckPreserved(firmware,plan);
                Require(Equal(firmware.Read("BootOrder"),plan.BeforeOrder),"Ordem mudou durante a instalacao");
                VerifiedWrite(firmware,"BootOrder",plan.AfterOrder);
                CheckPreserved(firmware,plan);
            } catch (Exception failure) {
                try { Rollback(firmware,plan); }
                catch (Exception rollback) { throw new AggregateException("Falha; reversao exige revisao pelo registro salvo",failure,rollback); }
                throw new InvalidOperationException("Instalacao nao concluida; firmware revertido",failure);
            }
        }
        public static void Rollback(IFirmware firmware, Plan plan) {
            ValidatePlan(plan);
            CheckPreserved(firmware,plan);
            var entry=firmware.Read(plan.EntryName); var order=firmware.Read("BootOrder");
            Require(entry==null || Equal(entry,plan.Entry),"Entrada propria foi alterada; preservar");
            Require(Equal(order,plan.BeforeOrder) || Equal(order,plan.AfterOrder),"Ordem sofreu alteracao posterior; preservar");
            if (!Equal(order,plan.BeforeOrder)) VerifiedWrite(firmware,"BootOrder",plan.BeforeOrder);
            if (entry!=null) VerifiedWrite(firmware,plan.EntryName,null);
            CheckPreserved(firmware,plan);
        }
    }
}

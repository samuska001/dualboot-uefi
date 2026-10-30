using System;
using System.IO;
using System.Diagnostics;
using System.Windows.Forms;
using System.Security.Principal;
using System.Runtime.Versioning;
using System.Web.Script.Serialization;
[assembly:TargetFramework(".NETFramework,Version=v4.8",FrameworkDisplayName=".NET Framework 4.8")]
[assembly:System.Reflection.AssemblyTitle("DualBoot Setup")]
[assembly:System.Reflection.AssemblyVersion("0.3.0.0")]
[assembly:System.Reflection.AssemblyProduct("DualBoot UEFI")]
namespace DualBoot.Installer {
    public static class Program {
        [STAThread]public static int Main(string[] args){
            AppContext.SetSwitch("Switch.System.IO.UseLegacyPathHandling",false);AppContext.SetSwitch("Switch.System.IO.BlockLongPaths",false);
            if(args.Length==1 && args[0]=="--self-test")return SecureInstallerTests.Run();
            if(args.Length==2 && args[0]=="--render-secure-demo"){
                try{Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);using(var form=new SecureBootForm(true,null,null))form.RenderDemo(args[1]);return 0;}catch{return 1;}
            }
            if(args.Length==2 && args[0]=="--render-demo"){
                try{Payload.ReadEmbedded();Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);using(var form=new MainForm(true))form.RenderDemo(args[1]);return 0;}catch{return 1;}
            }
            if(args.Length==2 && args[0]=="--inspect"){
                try{var state=new NativeServices().Scan();File.WriteAllText(args[1],new JavaScriptSerializer().Serialize(state));return 0;}catch(Exception ex){File.WriteAllText(args[1],ex.ToString());return 1;}
            }
            bool demo=args.Length==1 && args[0]=="--demo";
            if(args.Length>0 && !demo)return 2;
            if(!demo && !new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator)){
                try{Process.Start(new ProcessStartInfo(Application.ExecutablePath){UseShellExecute=true,Verb="runas"});return 0;}
                catch(System.ComponentModel.Win32Exception){MessageBox.Show("A verificação UEFI precisa de permissão de administrador. Nenhum arquivo ou entrada de boot foi alterado.","DualBoot Setup");return 1;}
            }
            bool acquired;using(var mutex=new System.Threading.Mutex(true,demo?"Local\\DualBootSetupDemo":"Global\\DualBootSetup",out acquired)){
                if(!acquired){MessageBox.Show("O assistente já está aberto.","DualBoot Setup");return 1;}
                try{Payload.ReadEmbedded();Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.Run(new MainForm(demo));return 0;}
                catch(Exception ex){MessageBox.Show(ex.Message,"DualBoot Setup",MessageBoxButtons.OK,MessageBoxIcon.Error);return 1;}
            }
        }
    }
}

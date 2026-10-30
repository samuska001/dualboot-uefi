using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows.Forms;
namespace DualBoot.Installer {
 internal static class SecureBootFormTests {
  static void Check(bool value,string label){if(!value)throw new Exception(label);}
  static object Field(object form,string name){return form.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(form);}
  static void Apply(object form,List<SecureBootSession> list,string id){form.GetType().GetMethod("ApplySessions",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(form,new object[]{list,id});}
  [STAThread] public static void Main(){
   var old=new SecureBootSession{Id=new string('a',32),State="ReadyToInstall"};
   var fresh=new SecureBootSession{Id=old.Id,State="NeedsRecovery",UsbJournalId="usb-journal",SsdJournalId="ssd-journal",FirmwareTestVerified=true,WindowsUserConfirmed=true,CollectionDirectory="full-session",Files=new List<SecureFileRecord>{new SecureFileRecord{Path="file"}}};
   Check(Object.ReferenceEquals(SecureBootForm.ReconcileSession(old,null,new List<SecureBootSession>{fresh}),fresh),"adopt entire reloaded object");
   Check(Object.ReferenceEquals(SecureBootForm.ReconcileSession(old,null,new List<SecureBootSession>()),old),"retain fallback when missing");
   using(var form=new SecureBootForm(true,null,null)){
    Apply(form,new List<SecureBootSession>{old},old.Id);
    ((TextBox)Field(form,"firmware")).Text="report";((TextBox)Field(form,"linux")).Text="linux-report";
    Apply(form,new List<SecureBootSession>{fresh},old.Id);
    Check(Object.ReferenceEquals(Field(form,"session"),fresh),"recovery current without reopening");
    Check(((TextBox)Field(form,"firmware")).Text=="report","same session preserves report");
    Check(((CheckBox)Field(form,"windowsConfirmed")).Checked,"resume synchronizes Windows confirmation");
    form.GetType().GetField("demo",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(form,false);
    form.GetType().GetMethod("RefreshView",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(form,null);
    Check(((Button)Field(form,"restoreUsb")).Enabled&&((Button)Field(form,"restoreSsd")).Enabled,"recovery actions enabled without reopening");
    Check(!((Button)Field(form,"install")).Enabled,"recovery blocks install");
    form.GetType().GetField("demo",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(form,true);
    Check(fresh.UsbJournalId=="usb-journal"&&fresh.SsdJournalId=="ssd-journal"&&fresh.Files.Count==1,"complete recovery fields");
    var next=new SecureBootSession{Id=new string('b',32)};Apply(form,new List<SecureBootSession>{next},next.Id);
    Check(((TextBox)Field(form,"firmware")).Text==""&&((TextBox)Field(form,"linux")).Text=="","different session clears reports");
    Check(!((CheckBox)Field(form,"windowsConfirmed")).Checked,"different session resets confirmation");
    var run=form.GetType().GetMethod("Run",BindingFlags.Instance|BindingFlags.NonPublic);bool invoked=false;
    ((System.Threading.Tasks.Task)run.Invoke(form,new object[]{(Action)delegate{invoked=true;},null,null})).GetAwaiter().GetResult();Check(!invoked,"demo never executes native operation");
   }
   Console.WriteLine("Secure Boot form reconciliation tests passed.");
  }
 }
}

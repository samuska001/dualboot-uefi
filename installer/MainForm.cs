using System;
using System.IO;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.Collections.Generic;
using DualBoot.Setup;
namespace DualBoot.Installer {
    public sealed class MainForm : Form {
        readonly bool demo; readonly NativeServices services=new NativeServices();ComputerState state;
        readonly ComboBox windows=new ComboBox(),steam=new ComboBox(),systemDefault=new ComboBox(),usb=new ComboBox(),backups=new ComboBox();
        readonly NumericUpDown timeout=new NumericUpDown();readonly CheckBox steamFirst=new CheckBox();
        readonly Label summary=new Label(),compatibility=new Label();readonly TextBox details=new TextBox();
        readonly Button scan=new Button(),install=new Button(),prepare=new Button(),restore=new Button(),configure=new Button(),manual=new Button(),folder=new Button(),secure=new Button();
        readonly ProgressBar progress=new ProgressBar();bool busy;
        static readonly Color background=Color.FromArgb(17,21,29),card=Color.FromArgb(27,33,44),foreground=Color.FromArgb(234,239,247),muted=Color.FromArgb(173,186,206),accent=Color.FromArgb(65,126,238);
        public MainForm(bool demonstration){
            demo=demonstration;Text="DualBoot Setup 0.3.0";ClientSize=new Size(960,760);MinimumSize=new Size(940,740);StartPosition=FormStartPosition.CenterScreen;
            BackColor=background;ForeColor=foreground;Font=new Font("Segoe UI",10);AutoScaleMode=AutoScaleMode.Dpi;
            var layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(26),ColumnCount=1,RowCount=7};
            foreach(var style in new[]{new RowStyle(SizeType.Absolute,82),new RowStyle(SizeType.Absolute,80),new RowStyle(SizeType.Absolute,245),new RowStyle(SizeType.Absolute,68),new RowStyle(SizeType.Absolute,88),new RowStyle(SizeType.Percent,100),new RowStyle(SizeType.Absolute,12)})layout.RowStyles.Add(style);
            Controls.Add(layout);
            var header=new Panel{Dock=DockStyle.Fill};header.Controls.Add(new Label{Text="DualBoot",Font=new Font("Segoe UI",27,FontStyle.Bold),AutoSize=true,Location=new Point(0,0)});
            header.Controls.Add(new Label{Text=demo?"DEMONSTRAÇÃO · nenhuma alteração em discos ou firmware":"Instale e configure seu menu de boot",ForeColor=muted,AutoSize=true,Location=new Point(3,51)});
            header.Controls.Add(new Label{Text="0.3.0 · EXPERIMENTAL",Width=215,Dock=DockStyle.Right,TextAlign=ContentAlignment.MiddleRight,ForeColor=muted});layout.Controls.Add(header);
            summary.Dock=DockStyle.Fill;summary.BackColor=card;summary.Padding=new Padding(16,10,10,10);summary.Text="Verificando seu computador…";layout.Controls.Add(summary);
            var columns=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,Padding=new Padding(0,16,0,0)};columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,53));columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,47));layout.Controls.Add(columns);
            var systems=Section("1   Sistemas encontrados");var settings=Section("2   Sua configuração");columns.Controls.Add(systems);columns.Controls.Add(settings);
            AddChoice(systems,"Windows original",windows,40);AddChoice(systems,"SteamOS original (opcional)",steam,113);
            AddChoice(settings,"Sistema selecionado ao abrir",systemDefault,40);systemDefault.Items.AddRange(new object[]{"Windows","SteamOS"});systemDefault.SelectedIndex=0;
            settings.Controls.Add(new Label{Text="Iniciar automaticamente após",AutoSize=true,ForeColor=muted,Location=new Point(16,113)});
            timeout.Minimum=0;timeout.Maximum=300;timeout.Value=0;timeout.Location=new Point(18,139);timeout.Width=88;timeout.BackColor=card;timeout.ForeColor=foreground;settings.Controls.Add(timeout);
            settings.Controls.Add(new Label{Text="segundos · 0 aguarda sua escolha",AutoSize=true,ForeColor=muted,Location=new Point(116,142)});
            steamFirst.Text="Mostrar SteamOS primeiro";steamFirst.AutoSize=true;steamFirst.Location=new Point(16,180);settings.Controls.Add(steamFirst);
            compatibility.Dock=DockStyle.Fill;compatibility.Padding=new Padding(2,8,0,0);compatibility.ForeColor=muted;layout.Controls.Add(compatibility);
            var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.LeftToRight,WrapContents=true};
            SetupButton(install,"Instalar no SSD",160,true);SetupButton(scan,"Verificar novamente",173,false);SetupButton(manual,"Como usar",120,false);
            actions.Controls.Add(install);actions.Controls.Add(scan);actions.Controls.Add(manual);SetupButton(secure,"Preparar Secure Boot",205,true);actions.Controls.Add(secure);
            var usbRow=new FlowLayoutPanel{Width=880,Height=38,WrapContents=false,Margin=new Padding(0,5,0,0)};
            usbRow.Controls.Add(new Label{Text="Pendrive",AutoSize=true,Margin=new Padding(0,8,12,0)});StyleCombo(usb);usb.Width=435;usbRow.Controls.Add(usb);SetupButton(prepare,"Preparar pendrive",173,false);usbRow.Controls.Add(prepare);actions.Controls.Add(usbRow);layout.Controls.Add(actions);
            var bottom=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=3,ColumnCount=1};bottom.RowStyles.Add(new RowStyle(SizeType.Absolute,32));bottom.RowStyles.Add(new RowStyle(SizeType.Absolute,40));bottom.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            StyleCombo(backups);backups.Dock=DockStyle.Fill;bottom.Controls.Add(backups);
            var recovery=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false};SetupButton(configure,"Salvar configuração",175,false);SetupButton(restore,"Restaurar boot anterior",210,false);SetupButton(folder,"Abrir backups",150,false);
            recovery.Controls.Add(configure);recovery.Controls.Add(restore);recovery.Controls.Add(folder);bottom.Controls.Add(recovery);
            details.Multiline=true;details.ReadOnly=true;details.ScrollBars=ScrollBars.Vertical;details.Dock=DockStyle.Fill;details.BackColor=background;details.ForeColor=muted;details.BorderStyle=BorderStyle.None;details.Font=new Font("Segoe UI",9);bottom.Controls.Add(details);layout.Controls.Add(bottom);
            progress.Dock=DockStyle.Fill;progress.Style=ProgressBarStyle.Marquee;progress.MarqueeAnimationSpeed=25;progress.Visible=false;layout.Controls.Add(progress);
            scan.Click+=async delegate{await Scan();};install.Click+=async delegate{await Install(false);};prepare.Click+=async delegate{await Install(true);};restore.Click+=async delegate{await Restore();};configure.Click+=async delegate{await Configure();};manual.Click+=delegate{ShowManual();};
            secure.Click+=delegate{if(busy)return;using(var form=new SecureBootForm(demo,state,Request()))form.ShowDialog(this);};
            folder.Click+=delegate{try{if(!Directory.Exists(NativeServices.BackupRoot))throw new IOException("Ainda não há backups deste assistente.");System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(NativeServices.BackupRoot){UseShellExecute=true});}catch(Exception ex){ShowError(ex);}};
            windows.SelectedIndexChanged+=delegate{RefreshCompatibility();};steam.SelectedIndexChanged+=delegate{RefreshCompatibility();};usb.SelectedIndexChanged+=delegate{RefreshCompatibility();};systemDefault.SelectedIndexChanged+=delegate{RefreshCompatibility();};timeout.ValueChanged+=delegate{RefreshCompatibility();};
            backups.SelectedIndexChanged+=delegate{var j=backups.SelectedItem as Journal;if(j!=null){systemDefault.SelectedIndex=j.SteamDefault?1:0;timeout.Value=Math.Max(0,Math.Min(300,j.Timeout));steamFirst.Checked=j.SteamFirst;}RefreshCompatibility();};
            Shown+=async delegate{await Scan();};FormClosing+=delegate(object sender,FormClosingEventArgs e){if(busy)e.Cancel=true;};
        }
        static Panel Section(string text){var p=new Panel{Dock=DockStyle.Fill,Margin=new Padding(0,0,12,0),BackColor=card};p.Controls.Add(new Label{Text=text,AutoSize=true,Font=new Font("Segoe UI",11,FontStyle.Bold),Location=new Point(16,9)});return p;}
        static void StyleCombo(ComboBox c){c.DropDownStyle=ComboBoxStyle.DropDownList;c.FlatStyle=FlatStyle.Flat;c.BackColor=card;c.ForeColor=foreground;}
        static void AddChoice(Panel panel,string text,ComboBox combo,int y){panel.Controls.Add(new Label{Text=text,ForeColor=muted,AutoSize=true,Location=new Point(16,y)});StyleCombo(combo);combo.Location=new Point(16,y+27);combo.Width=panel.Width-32;combo.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;panel.Controls.Add(combo);}
        static void SetupButton(Button b,string text,int width,bool primary){b.Text=text;b.Width=width;b.Height=34;b.FlatStyle=FlatStyle.Flat;b.FlatAppearance.BorderColor=Color.FromArgb(63,77,98);b.BackColor=primary?accent:card;b.ForeColor=foreground;b.Margin=new Padding(0,0,10,4);b.Cursor=Cursors.Hand;}
        InstallRequest Request(){return new InstallRequest{Windows=windows.SelectedItem as FirmwareEntry,Steam=steam.SelectedItem as FirmwareEntry,UsbPartition=usb.SelectedItem as PartitionInfo,SteamDefault=systemDefault.SelectedIndex==1,Timeout=(int)timeout.Value,SteamFirst=steamFirst.Checked};}
        void SetBusy(bool value){busy=value;progress.Visible=value;scan.Enabled=!value;windows.Enabled=!value;steam.Enabled=!value;systemDefault.Enabled=!value;timeout.Enabled=!value;steamFirst.Enabled=!value;usb.Enabled=!value;backups.Enabled=!value;manual.Enabled=!value;folder.Enabled=!value&&!demo;secure.Enabled=!value&&state!=null;RefreshCompatibility();}
        void RefreshCompatibility(){
            install.Enabled=false;prepare.Enabled=false;restore.Enabled=false;configure.Enabled=false;if(busy||state==null)return;
            var request=Request();string issue=Policy.Compatibility(state,request,false);string usbIssue=Policy.Compatibility(state,request,true);
            bool exists=false;try{var part=request.Windows==null?null:state.Find(request.Windows.PartitionGuid);exists=part!=null && part.Root!=null && (Directory.Exists(SafeFiles.Under(part.Root,@"EFI\DualBoot"))||File.Exists(SafeFiles.Under(part.Root,@"EFI\DualBoot")));}catch{exists=true;}
            var journal=backups.SelectedItem as Journal;
            install.Enabled=!demo && issue==null && !exists;prepare.Enabled=!demo && usbIssue==null;
            restore.Enabled=!demo && journal!=null;configure.Enabled=!demo && journal!=null && journal.Kind=="SSD" && (journal.State=="Installed"||journal.State=="Updating");
            compatibility.Text=demo?"Demonstração: sistemas fictícios. Os botões que alteram arquivos ou boot ficam desativados.":exists?"DualBoot já existe. Use um backup gerenciado abaixo; instalações manuais ou com Secure Boot personalizado não serão substituídas.":issue??"Pronto para instalar. Será criada uma entrada DualBoot antes das entradas originais; seus carregadores serão preservados.";
            if(!demo && usbIssue!=null && request.UsbPartition!=null)compatibility.Text+="  USB: "+usbIssue;
        }
        async Task Scan(){
            SetBusy(true);try{
                state=demo?Demo():await Task.Run(delegate{return services.Scan();});windows.Items.Clear();steam.Items.Clear();usb.Items.Clear();backups.Items.Clear();
                windows.Items.Add("Selecione o Windows");steam.Items.Add("Não usar SteamOS");usb.Items.Add("Conecte e selecione um pendrive FAT32");
                int winCount=0,steamCount=0;
                foreach(var e in state.Entries){if(e.IsWindows){windows.Items.Add(e);winCount++;}if(e.IsSteam){steam.Items.Add(e);steamCount++;}}
                windows.SelectedIndex=0;var automatic=Selection.WindowsDefault(state);if(automatic!=null)windows.SelectedItem=automatic;steam.SelectedIndex=steamCount==1?1:0;
                foreach(var p in state.Partitions)if(p.IsUsb && !p.IsBoot && !p.IsSystem && String.Equals(p.FileSystem,"FAT32",StringComparison.OrdinalIgnoreCase))usb.Items.Add(p);usb.SelectedIndex=usb.Items.Count==2?1:0;
                var notes=new List<string>(state.Notes);if(!demo)foreach(var j in services.Journals(notes,state))backups.Items.Add(j);if(backups.Items.Count==0)backups.Items.Add("Instalações e backups deste assistente aparecerão aqui");backups.SelectedIndex=0;
                summary.Text="UEFI: "+(state.Order!=null?"detectado":"não confirmado")+"     ·     Secure Boot: "+(state.SecureBoot==true?"ativo":state.SecureBoot==false?"desligado":"não confirmado")+"\r\n"+
                    "Windows: "+winCount+" entrada(s)     ·     SteamOS: "+steamCount+" entrada(s)     ·     Criptografia do Windows: "+(state.EncryptionClear==true?"desligada":state.EncryptionClear==false?"ativa":"não confirmada");
                details.Text=notes.Count==0?"Verificação concluída. Escolha seu padrão e clique em Instalar no SSD. Para começar pelo pendrive, selecione a mídia acima.\r\nO aplicativo não formata, não reinicia sozinho e não muda Secure Boot ou criptografia.":String.Join(Environment.NewLine,notes.ToArray());
            }catch(Exception ex){ShowError(ex);}finally{SetBusy(false);}
        }
        async Task Install(bool isUsb){
            if(demo)return;var request=Request();string issue=Policy.Compatibility(state,request,isUsb);if(issue!=null){MessageBox.Show(this,issue,"Verificação necessária",MessageBoxButtons.OK,MessageBoxIcon.Information);return;}
            string destination=isUsb?request.UsbPartition.ToString():state.Find(request.Windows.PartitionGuid).ToString();
            string body="Destino: "+destination+"\r\nWindows: "+request.Windows.Description+"\r\nSteamOS: "+(request.Steam==null?"desativado":request.Steam.Description)+"\r\nPadrão: "+(request.SteamDefault?"SteamOS":"Windows")+"\r\nTempo: "+(request.Timeout==0?"aguardar sua escolha":request.Timeout+" segundos")+"\r\n\r\n"+
                (isUsb?"Será criada a pasta EFI/BOOT no pendrive, sem formatar e sem alterar a ordem de boot do PC.":"Será criada EFI/DualBoot na partição EFI do Windows e uma entrada DualBoot em primeiro. As entradas originais serão mantidas. Um registro de restauração será salvo antes das alterações.");
            if(MessageBox.Show(this,body,"Confirmar "+(isUsb?"pendrive":"instalação"),MessageBoxButtons.OKCancel,MessageBoxIcon.Information)!=DialogResult.OK)return;
            await Action(isUsb?"Pendrive preparado. Escolha sua entrada UEFI no menu de boot; D abre o diagnóstico.":"DualBoot instalado. Você pode reiniciar quando quiser; o assistente não reiniciará o computador.",delegate{services.Install(request,isUsb);});
        }
        async Task Restore(){
            if(demo)return;var journal=backups.SelectedItem as Journal;if(journal==null)return;
            if(MessageBox.Show(this,"Restaurar o estado anterior deste registro?\r\n\r\n"+journal+"\r\n\r\nA ordem original será restaurada (SSD) e somente arquivos próprios conferidos serão removidos. Se houver alterações externas, a operação será interrompida para preservá-las.","Restaurar boot anterior",MessageBoxButtons.OKCancel,MessageBoxIcon.Information)!=DialogResult.OK)return;
            await Action("Restauração concluída. As entradas originais estão preservadas.",delegate{services.Restore(journal);});
        }
        async Task Configure(){
            if(demo)return;var journal=backups.SelectedItem as Journal;if(journal==null)return;
            bool steamDefault=systemDefault.SelectedIndex==1;int seconds=(int)timeout.Value;bool first=steamFirst.Checked;
            if(MessageBox.Show(this,"Salvar padrão, tempo e ordem visual no registro selecionado?\r\n\r\nOs destinos Windows/SteamOS já instalados serão mantidos. Esta ação não altera a ordem de boot do firmware.","Salvar configuração",MessageBoxButtons.OKCancel,MessageBoxIcon.Information)!=DialogResult.OK)return;
            await Action("Configuração salva e conferida.",delegate{services.Update(journal,steamDefault,seconds,first);});
        }
        async Task Action(string success,Action operation){SetBusy(true);try{await Task.Run(operation);MessageBox.Show(this,success,"DualBoot",MessageBoxButtons.OK,MessageBoxIcon.Information);}catch(Exception ex){ShowError(ex);}finally{SetBusy(false);}await Scan();}
        void ShowError(Exception error){details.Text=error.Message;MessageBox.Show(this,error.Message,"Operação não concluída",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
        void ShowManual(){
            var dialog=new Form{Text="Como usar o DualBoot Setup",Size=new Size(810,640),StartPosition=FormStartPosition.CenterParent,BackColor=background,ForeColor=foreground};
            string text;using(var reader=new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("installer.manual")))text=reader.ReadToEnd();
            dialog.Controls.Add(new TextBox{Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,Dock=DockStyle.Fill,Text=text,BackColor=background,ForeColor=foreground,Font=new Font("Segoe UI",11),BorderStyle=BorderStyle.None,Margin=new Padding(20)});dialog.ShowDialog(this);dialog.Dispose();
        }
        public void RenderDemo(string path){
            if(!demo)throw new InvalidOperationException("Captura permitida somente na demonstração.");
            StartPosition=FormStartPosition.Manual;Location=new Point(-20000,-20000);ShowInTaskbar=false;Show();Application.DoEvents();
            using(var bitmap=new Bitmap(Width,Height)){DrawToBitmap(bitmap,new Rectangle(0,0,Width,Height));bitmap.Save(path,System.Drawing.Imaging.ImageFormat.Png);}Close();
        }
        static ComputerState Demo(){
            var s=new ComputerState{SecureBoot=false,EncryptionClear=true,Order=new Variable(7,new byte[]{1,0,2,0})};
            Guid win=new Guid("12345678-1234-4234-8234-123456789abc"),linux=new Guid("87654321-4321-4321-8321-cba987654321");
            s.Entries.Add(FirmwareEntry.Parse("Boot0001",new Variable(7,InstallerTests.Option(win,@"\EFI\Microsoft\Boot\bootmgfw.efi","Windows Boot Manager"))));
            s.Entries.Add(FirmwareEntry.Parse("Boot0002",new Variable(7,InstallerTests.Option(linux,@"\EFI\steamos\steamcl.efi","SteamOS"))));
            s.Partitions.Add(new PartitionInfo{Guid=win,Root=Path.GetTempPath(),IsEsp=true,IsSystem=true,FileSystem="FAT32",FreeBytes=100000000,Label="SSD do Windows"});
            s.Partitions.Add(new PartitionInfo{Guid=linux,Root=Path.GetTempPath(),IsEsp=true,FileSystem="FAT32",FreeBytes=100000000,Label="SSD do SteamOS"});return s;
        }
    }
}

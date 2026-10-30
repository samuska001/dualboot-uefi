using System;
using System.IO;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using DualBoot.Setup;

namespace DualBoot.Installer {
    public sealed class SecureBootForm : Form {
        readonly bool demo; readonly InstallRequest request; ComputerState state;
        readonly SecureBootNative native; SecureBootSession session; bool busy;
        readonly ComboBox sessions=new ComboBox(),usb=new ComboBox();
        readonly TextBox collection=new TextBox(),firmware=new TextBox(),linux=new TextBox(),details=new TextBox();
        readonly CheckBox windowsConfirmed=new CheckBox(); readonly Label status=new Label();
        readonly System.Collections.Generic.List<Button> selectors=new System.Collections.Generic.List<Button>();
        readonly FlowLayoutPanel actions=new FlowLayoutPanel(); readonly ProgressBar progress=new ProgressBar();
        Button create,update,collector,prepare,verify,install,installed,restoreUsb,restoreSsd,folder,resume;
        static readonly Color bg=Color.FromArgb(17,21,29),card=Color.FromArgb(27,33,44),fg=Color.FromArgb(234,239,247),muted=Color.FromArgb(173,186,206);
        public SecureBootForm(bool demonstration,ComputerState computer,InstallRequest selection){
            demo=demonstration;state=computer;request=selection;native=demo?null:new SecureBootNative();
            Text="DualBoot 0.3.0 · Preparar Secure Boot";ClientSize=new Size(1000,900);MinimumSize=new Size(960,850);StartPosition=FormStartPosition.CenterParent;
            BackColor=bg;ForeColor=fg;Font=new Font("Segoe UI",10);AutoScaleMode=AutoScaleMode.Dpi;
            var layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(24),ColumnCount=1,RowCount=9};
            foreach(int height in new[]{66,64,44,90,80,110,125})layout.RowStyles.Add(new RowStyle(SizeType.Absolute,height));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,10));Controls.Add(layout);
            layout.Controls.Add(new Label{Dock=DockStyle.Fill,Text="Preparar Secure Boot\r\n0.3.0 EXPERIMENTAL · "+(demo?"DEMONSTRAÇÃO — dados fictícios":"Windows e SteamOS selecionados na janela principal"),Font=new Font("Segoe UI",13,FontStyle.Bold)});
            status.Dock=DockStyle.Fill;status.BackColor=card;status.Padding=new Padding(12);layout.Controls.Add(status);
            var resumeRow=Row();Style(sessions);sessions.Width=680;resumeRow.Controls.Add(sessions);resume=Button(resumeRow,"Retomar sessão",async delegate{var chosen=sessions.SelectedItem as SecureBootSession;if(chosen==null)return;await Run(delegate{},null,chosen.Id);});layout.Controls.Add(resumeRow);
            var collectPanel=new Panel{Dock=DockStyle.Fill};collectPanel.Controls.Add(new Label{Text="1   Coleta nova do SteamOS (obrigatória quando selecionado)",AutoSize=true,Location=new Point(0,0)});
            collection.SetBounds(0,29,710,29);StyleText(collection);collectPanel.Controls.Add(collection);var browse=new Button{Text="Importar pasta…",Location=new Point(725,27),Size=new Size(205,34)};StyleButton(browse);selectors.Add(browse);browse.Click+=delegate{ChooseFolder(collection);};collectPanel.Controls.Add(browse);
            collectPanel.Controls.Add(new Label{Text="Use o coletor no SteamOS ativo; importe a pasta gerada. O assistente não reinicia nem coleta via WSL.",ForeColor=muted,AutoSize=true,Location=new Point(0,65)});layout.Controls.Add(collectPanel);
            var usbPanel=new Panel{Dock=DockStyle.Fill};usbPanel.Controls.Add(new Label{Text="2   Teste primeiro no pendrive FAT32",AutoSize=true});Style(usb);usb.SetBounds(0,30,930,30);usbPanel.Controls.Add(usb);layout.Controls.Add(usbPanel);
            var reportPanel=new Panel{Dock=DockStyle.Fill};reportPanel.Controls.Add(new Label{Text="3   Após iniciar pelo USB: importe os relatórios desta sessão",AutoSize=true});
            ReportRow(reportPanel,"UEFI",firmware,27);ReportRow(reportPanel,"Linux",linux,65);layout.Controls.Add(reportPanel);
            actions.Dock=DockStyle.Fill;actions.WrapContents=true;layout.Controls.Add(actions);
            collector=Button(actions,"Exportar coletor",async delegate{using(var d=new FolderBrowserDialog()){if(d.ShowDialog(this)==DialogResult.OK){string path=d.SelectedPath;await Run(delegate{native.ExportCollector(path);},"Coletor exportado. Execute-o no SteamOS conforme as instruções incluídas.");}}});
            create=Button(actions,"Criar certificado e preparar",async delegate{await Create(false);});
            update=Button(actions,"Preparar atualização",async delegate{await Create(true);});
            prepare=Button(actions,"Preparar USB",async delegate{var p=usb.SelectedItem as PartitionInfo;if(p==null)return;if(Confirm("Preparar este pendrive, sem formatar?\r\n"+p+"\r\nSerá gravada a cadeia de teste assinada; os arquivos próprios terão registro de restauração."))await Run(delegate{native.PrepareUsb(session,p);},"USB preparado. Inicie manualmente por ele e siga as etapas MOK/BIOS abaixo.");});
            verify=Button(actions,"Verificar teste USB",async delegate{string f=firmware.Text,l=session!=null&&session.IncludesSteam?linux.Text:null;bool w=windowsConfirmed.Checked;await Run(delegate{native.VerifyTest(session,f,l,w);},"Relatórios conferidos. Consulte o estado da sessão.");});
            install=Button(actions,"Instalar no SSD",async delegate{if(Confirm("Instalar a cadeia assinada desta sessão no SSD?\r\nOs carregadores originais serão preservados. O controlador verificará novamente as condições antes de gravar."))await Run(delegate{native.InstallSsd(session);},"Instalação concluída. Reinicie e faça a verificação manual dos sistemas.");});
            installed=Button(actions,"Verificar instalado",async delegate{await Run(delegate{native.VerifyInstalled(session);},"Arquivos instalados conferidos; isso não substitui iniciar os sistemas no hardware.");});
            restoreUsb=Button(actions,"Restaurar USB",async delegate{await Restore(true);});restoreSsd=Button(actions,"Restaurar SSD",async delegate{await Restore(false);});
            folder=Button(actions,"Abrir pasta da sessão",delegate{try{System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(native.SessionDirectory(session)){UseShellExecute=true});}catch(Exception ex){Error(ex);}});
            var bottom=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3};bottom.RowStyles.Add(new RowStyle(SizeType.Absolute,30));bottom.RowStyles.Add(new RowStyle(SizeType.Absolute,83));bottom.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            windowsConfirmed.Text="Confirmei manualmente que o Windows iniciou pelo menu assinado do USB";windowsConfirmed.AutoSize=true;windowsConfirmed.ForeColor=fg;windowsConfirmed.CheckedChanged+=delegate{RefreshView();};bottom.Controls.Add(windowsConfirmed);
            bottom.Controls.Add(new Label{Dock=DockStyle.Fill,ForeColor=muted,Text="Etapas manuais: no primeiro boot pelo USB, use o MokManager para cadastrar somente o certificado desta sessão e conferir sua identificação. Depois, ative Secure Boot na BIOS e teste Windows e SteamOS (se incluído).\r\nNão apague PK/KEK/db/dbx. O assistente não altera BIOS, TPM, BitLocker ou BCD. MOK/BIOS e início dos sistemas exigem sua confirmação física."});
            details.Multiline=true;details.ReadOnly=true;details.ScrollBars=ScrollBars.Vertical;details.Dock=DockStyle.Fill;StyleText(details);bottom.Controls.Add(details);layout.Controls.Add(bottom);
            progress.Dock=DockStyle.Fill;progress.Style=ProgressBarStyle.Marquee;progress.Visible=false;layout.Controls.Add(progress);
            if(demo){collection.ReadOnly=true;firmware.ReadOnly=true;linux.ReadOnly=true;}
            Shown+=async delegate{if(demo){sessions.Items.Add("Sessão fictícia · preparada para teste USB");sessions.SelectedIndex=0;usb.Items.Add("Pendrive de demonstração · FAT32 · 16 GB");usb.SelectedIndex=0;collection.Text=@"D:\Coleta-SteamOS-exemplo";RefreshView();}else await LoadSessions();};
            sessions.Format+=delegate(object sender,ListControlConvertEventArgs e){var s=e.ListItem as SecureBootSession;if(s!=null)e.Value=s.Id+" · "+s.State;};sessions.FormattingEnabled=true;
            usb.SelectedIndexChanged+=delegate{RefreshView();};
            FormClosing+=delegate(object sender,FormClosingEventArgs e){if(busy)e.Cancel=true;};RefreshView();
        }
        static FlowLayoutPanel Row(){return new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false};}
        static void Style(ComboBox c){c.DropDownStyle=ComboBoxStyle.DropDownList;c.BackColor=card;c.ForeColor=fg;c.FlatStyle=FlatStyle.Flat;}
        static void StyleText(TextBox c){c.BackColor=card;c.ForeColor=fg;c.BorderStyle=BorderStyle.FixedSingle;}
        static void StyleButton(Button b){b.FlatStyle=FlatStyle.Flat;b.BackColor=card;b.ForeColor=fg;b.FlatAppearance.BorderColor=Color.FromArgb(63,77,98);}
        Button Button(FlowLayoutPanel row,string text,EventHandler click){var b=new Button{Text=text,AutoSize=true,Height=34,Margin=new Padding(0,0,8,7)};StyleButton(b);b.Click+=click;row.Controls.Add(b);return b;}
        void ReportRow(Panel panel,string name,TextBox box,int y){panel.Controls.Add(new Label{Text=name,Location=new Point(0,y+4),Width=55});StyleText(box);box.SetBounds(60,y,650,29);panel.Controls.Add(box);var b=new Button{Text="Selecionar relatório…",Location=new Point(725,y-2),Size=new Size(205,34)};StyleButton(b);selectors.Add(b);b.Click+=delegate{if(busy||demo)return;using(var d=new OpenFileDialog{Filter="Relatórios|*.txt;*.json;*.ini;*.log|Todos os arquivos|*.*"})if(d.ShowDialog(this)==DialogResult.OK)box.Text=d.FileName;};panel.Controls.Add(b);}
        void ChooseFolder(TextBox box){if(busy||demo)return;using(var d=new FolderBrowserDialog())if(d.ShowDialog(this)==DialogResult.OK)box.Text=d.SelectedPath;}
        bool Confirm(string text){return MessageBox.Show(this,text,"Confirmar Secure Boot",MessageBoxButtons.OKCancel,MessageBoxIcon.Information)==DialogResult.OK;}
        async Task Create(bool updating){
            if(busy||demo)return;if(request==null||request.Windows==null){Error(new InvalidOperationException("Selecione o Windows na janela principal antes de preparar."));return;}
            if(updating&&request.Steam!=null){using(var d=new FolderBrowserDialog{Description="Selecione uma coleta nova do SteamOS para esta atualização"}){if(d.ShowDialog(this)!=DialogResult.OK)return;collection.Text=d.SelectedPath;}}
            string path=collection.Text.Trim();if(request.Steam!=null&&path.Length==0){Error(new InvalidOperationException("Importe uma coleta nova do SteamOS selecionado."));return;}
            if(!Confirm((updating?"Será criada uma nova sessão com coleta nova. A instalação anterior continua disponível até a instalação explícita da atualização.\r\n\r\n":"")+"Criar explicitamente uma identidade de assinatura local e preparar esta sessão?\r\nWindows: "+request.Windows.Description+"\r\nSteamOS: "+(request.Steam==null?"não incluído":request.Steam.Description)+"\r\nNenhum disco de boot será instalado nesta etapa."))return;
            firmware.Clear();linux.Clear();windowsConfirmed.Checked=false;
            await Run(delegate{session=native.Prepare(request,path.Length==0?null:path);},"Sessão preparada. Exporte/confira o certificado e teste pelo USB antes de instalar no SSD.");
        }
        async Task Restore(bool isUsb){if(Confirm("Restaurar somente os arquivos e metadados gerenciados pelo registro "+(isUsb?"USB":"SSD")+" desta sessão? Alterações externas serão recusadas."))await Run(delegate{native.Restore(session,isUsb);},"Restauração concluída.");}
        void SelectSession(SecureBootSession current){
            bool changed=(session==null?null:session.Id)!=(current==null?null:current.Id);
            session=current;if(changed){firmware.Clear();linux.Clear();}
            windowsConfirmed.Checked=session!=null&&session.WindowsUserConfirmed;
        }
        internal static SecureBootSession ReconcileSession(SecureBootSession previous,string preferredId,System.Collections.Generic.List<SecureBootSession> list){
            string id=preferredId??(previous==null?null:previous.Id);
            foreach(var item in list)if(item.Id==id)return item;
            return previous;
        }
        void ApplySessions(System.Collections.Generic.List<SecureBootSession> list,string preferredId){
            var current=ReconcileSession(session,preferredId,list);
            sessions.Items.Clear();foreach(var item in list)sessions.Items.Add(item);
            if(current!=null){if(!sessions.Items.Contains(current))sessions.Items.Add(current);sessions.SelectedItem=current;}
            else if(sessions.Items.Count>0)sessions.SelectedIndex=0;
            SelectSession(current);
        }
        async Task LoadSessions(){await Run(delegate{state=new NativeServices().Scan();},null);if(demo||state==null)return;usb.Items.Clear();foreach(var p in state.Partitions)if(p.IsUsb&&!p.IsBoot&&!p.IsSystem&&String.Equals(p.FileSystem,"FAT32",StringComparison.OrdinalIgnoreCase))usb.Items.Add(p);if(usb.Items.Count>0)usb.SelectedIndex=0;}
        async Task Run(Action operation,string success,string preferredId=null){
            if(busy||demo)return;busy=true;RefreshView();Exception failure=null;
            try{
                try{await Task.Run(operation);}catch(Exception ex){failure=ex;}
                try{var list=await Task.Run(delegate{return native.ListSessions();});ApplySessions(list,preferredId);}
                catch(Exception reloadError){if(failure==null)failure=reloadError;else failure=new InvalidOperationException(failure.Message+"\r\nNão foi possível atualizar as sessões: "+reloadError.Message,failure);}
            }finally{busy=false;RefreshView();}
            if(failure!=null)Error(failure);else if(success!=null)MessageBox.Show(this,success,"DualBoot",MessageBoxButtons.OK,MessageBoxIcon.Information);
        }
        void Error(Exception ex){details.Text=ex.Message;MessageBox.Show(this,ex.Message,"Operação não concluída",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
        void RefreshView(){
            if(create==null)return;bool active=!busy&&!demo,has=session!=null;progress.Visible=busy;
            foreach(Button b in selectors)b.Enabled=active;foreach(Control c in actions.Controls)c.Enabled=active;collector.Enabled=active;create.Enabled=active;update.Enabled=active&&has;resume.Enabled=active&&sessions.Items.Count>0;
            prepare.Enabled=active&&has&&usb.SelectedItem is PartitionInfo;verify.Enabled=active&&has;install.Enabled=active&&has&&SecureBootWorkflow.Ready(session);installed.Enabled=active&&has&&!String.IsNullOrEmpty(session.SsdJournalId);
            restoreUsb.Enabled=active&&has&&!String.IsNullOrEmpty(session.UsbJournalId);restoreSsd.Enabled=active&&has&&!String.IsNullOrEmpty(session.SsdJournalId);folder.Enabled=active&&has;
            sessions.Enabled=active;usb.Enabled=active;collection.Enabled=active||demo;firmware.Enabled=active||demo;linux.Enabled=demo||(active&&has&&session.IncludesSteam);windowsConfirmed.Enabled=active&&has;
            status.Text=demo?"DEMONSTRAÇÃO · Sessão fictícia 0123456789abcdef0123456789abcdef\r\nPreparada → USB → MOK/BIOS → testes confirmados → SSD":busy?"Operação em andamento. Aguarde; esta janela permanecerá aberta.":has?"Sessão: "+session.Id+" · Estado: "+session.State+"\r\nTeste UEFI: "+session.FirmwareTestVerified+" · Windows confirmado: "+session.WindowsUserConfirmed+" · Linux: "+(session.IncludesSteam?session.LinuxTestVerified.ToString():"não incluído"):"Nenhuma sessão retomada. Importe a coleta (se usar SteamOS) e crie uma sessão explicitamente.";
            if(!busy)details.Text=demo?"Certificado fictício · SHA-256: A1B2… (exemplo)\r\nTodos os controles de operação estão desativados. Nenhuma varredura, assinatura ou acesso a discos foi realizado.":has?"Certificado SHA-256: "+(session.Identity==null?"não disponível":session.Identity.CertificateSha256)+"\r\nPreparar → testar USB → cadastrar MOK e ativar Secure Boot manualmente → conferir relatórios desta sessão → instalar SSD.\r\nRelatórios antigos ou de outra sessão não autorizam instalação.":"As sessões existentes são somente listadas ao abrir. Retomar mantém o estado e os registros da sessão completa.";
        }
        public void RenderDemo(string path){if(!demo)throw new InvalidOperationException("Somente demonstração.");StartPosition=FormStartPosition.Manual;Location=new Point(-20000,-20000);ShowInTaskbar=false;Show();Application.DoEvents();using(var bitmap=new Bitmap(Width,Height)){DrawToBitmap(bitmap,new Rectangle(0,0,Width,Height));bitmap.Save(path,System.Drawing.Imaging.ImageFormat.Png);}Close();}
    }
}

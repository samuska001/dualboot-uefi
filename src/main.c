#include "menu.h"
#ifdef DB_MOK_REPORT
#include "secureboot_status.h"
#endif
#ifdef DB_SECURE_BOOT
static const volatile char sbat_data[] __attribute__((section(".sbat"),used))=
 "sbat,1,SBAT Version,sbat,1,https://github.com/rhboot/shim/blob/main/SBAT.md\n"
 "dualboot,1,DualBoot,dualboot,0.1.0,https://example.invalid/dualboot\n";
#endif
static DbPlatform machine;
static DbConfig config;
static size_t length(const char*s){size_t n=0;while(s[n])n++;return n;}
static void hex_text(char*out,uint64_t v,unsigned digits){const char*hx="0123456789ABCDEF";for(unsigned i=0;i<digits;i++)out[i]=hx[(v>>((digits-i-1)*4))&15];out[digits]=0;}
static void fit_text(DbPlatform*p,int x,int y,int max_width,const char*text,int scale,uint32_t color){
 char out[DB_LABEL];size_t cap=(size_t)(max_width/(6*scale));if(cap>=sizeof out)cap=sizeof out-1;size_t n=length(text);if(n>cap)n=cap;for(size_t i=0;i<n;i++)out[i]=text[i];out[n]=0;if(length(text)>cap&&n>=3)out[n-1]=out[n-2]=out[n-3]='.';db_text(p,x,y,out,scale,color);
}
static void *asset(DbPlatform*p,const char*theme,const char*name,size_t*len){char path[DB_PATH]="themes\\";size_t n=7;for(size_t i=0;theme[i]&&n+1<sizeof path;i++)path[n++]=theme[i];path[n++]='\\';for(size_t i=0;name[i]&&n+1<sizeof path;i++)path[n++]=name[i];path[n]=0;return platform_read_local(p,path,DB_FILE_MAX,len);}
static void load_config(DbPlatform*p,DbConfig*c,DbMenuAssets*a){
 db_defaults(c);c->timeout=0;size_t n=0;char*data=platform_read_local(p,"dualboot.ini",DB_CONFIG_MAX,&n);
 if(data){if(!db_config_parse(c,data,n)){p->config_error=1;platform_log("DB:CONFIG_INVALID\n");}platform_free(p,data);}
 size_t theme_len=0;char*theme=asset(p,c->theme,"theme.ini",&theme_len);if(theme){if(!db_theme_parse(c,theme,theme_len))p->config_error=1;platform_free(p,theme);}
 a->background=asset(p,c->theme,"background.bmp",&a->background_len);a->icons[0]=asset(p,c->theme,"windows.bmp",&a->icon_len[0]);a->icons[1]=asset(p,c->theme,"steamos.bmp",&a->icon_len[1]);
}
static int draw_menu(DbPlatform*p,const DbConfig*c,DbMenuAssets*a,unsigned selected,uint32_t remaining,int timer,const char*message){
 if(!p->canvas){p->st->ConOut->SetCursorPosition(p->st->ConOut,0,0);platform_console(p,"DUALBOOT 0.1.0\r\n\r\n");for(unsigned pos=0;pos<2;pos++){unsigned i=pos^c->first;platform_console(p,selected==i?"> ":"  ");platform_console(p,c->entries[i].label);platform_console(p,p->targets[i].available?" [OK]          \r\n":" [INDISPONIVEL]\r\n");}platform_console(p,"\r\nSetas: escolher | Enter: iniciar | D: diagnostico | Esc: sair\r\n");platform_console(p,message);return 1;}
 if(!db_menu_draw(p,c,a,selected,remaining,timer))return 0;
 platform_present(p);if(!p->canvas)return draw_menu(p,c,a,selected,remaining,timer,message);return 1;
}
static void draw_diagnostic(DbPlatform*p,const char*report_status){
 if(!p->canvas){p->st->ConOut->SetCursorPosition(p->st->ConOut,0,0);platform_console(p,"DUALBOOT - DIAGNOSTICO\r\nF2 salva diagnostic.txt | M abre menu | Esc sai\r\n");platform_console(p,p->usb.active?"Receptor ativo\r\n":"Receptor nao inicializado\r\n");platform_console(p,report_status);platform_console(p,"\r\n");platform_console(p,p->inventory);return;}
 int w=(int)p->width,h=(int)p->height,m=w/16;db_rect(p,0,0,w,h,0x0b101a);db_text(p,m,35,"DIAGNOSTICO UEFI",2,0x6be0b5);
 db_text(p,m,75,"FLYDIGI DIREWOLF 4 - USB 045E:028E - FF/5D/01",1,0xe9eef5);
 db_text(p,m,105,p->usb.failed?"ERRO USB - ENCERRAMENTO PENDENTE":(p->usb.active?"TRANSFERENCIA USB ATIVA":"AGUARDANDO RECEPTOR COMPATIVEL"),2,p->usb.active&&!p->usb.failed?0x6be0b5:0xf5c879);
 char value[20];db_text(p,m,150,"PACOTES",1,0x8f9dad);hex_text(value,p->usb.packets,8);db_text(p,m+150,150,value,2,0xe9eef5);
 db_text(p,m,185,"BOTOES (HEX)",1,0x8f9dad);hex_text(value,p->usb.pad.buttons,4);db_text(p,m+150,185,value,2,0x6be0b5);
 db_text(p,m,220,"ANALOGICO X / Y",1,0x8f9dad);hex_text(value,(uint16_t)p->usb.pad.lx,4);db_text(p,m+150,220,value,2,0xe9eef5);hex_text(value,(uint16_t)p->usb.pad.ly,4);db_text(p,m+230,220,value,2,0xe9eef5);
 db_text(p,m,255,"ERROS USB",1,0x8f9dad);hex_text(value,p->usb.errors,8);db_text(p,m+150,255,value,2,0xe9eef5);
 fit_text(p,m,300,w-2*m,"DIRECIONAL E A/B: OBSERVE OS VALORES, SEM INICIAR SISTEMAS",1,0x8f9dad);
 int y=340;const char*s=p->inventory;for(int lines=0;*s&&y<h-95&&lines<24;lines++,y+=15){char line[DB_LABEL];size_t n=0;while(*s&&*s!='\n'){if(*s!='\r'&&n+1<sizeof line)line[n++]=*s;s++;}if(*s=='\n')s++;line[n]=0;fit_text(p,m,y,w-2*m,line,1,0xa7b4c7);}
 fit_text(p,m,h-80,w-2*m,report_status,1,0xf5c879);fit_text(p,m,h-60,w-2*m,"F2: SALVAR RELATORIO   M: MENU   B/ESC: VOLTAR OU SAIR",1,0x6be0b5);platform_present(p);if(!p->canvas)draw_diagnostic(p,report_status);
}
EFI_STATUS EFIAPI EfiMain(EFI_HANDLE image,EFI_SYSTEM_TABLE*st){
#ifdef DB_SECURE_BOOT
 if(sbat_data[0]!='s')return EFI_ABORTED;
#endif
 DbPlatform*p=&machine;if(!platform_init(p,image,st))return EFI_ABORTED;DbMenuAssets assets={0};load_config(p,&config,&assets);platform_scan_boot(p,&config);
#ifdef DB_MOK_REPORT
 db_secureboot_report(p);
#endif
 unsigned selected=config.selected;int diagnostic=0,standalone=0;
#ifdef DB_DIAGNOSTIC
 diagnostic=1;standalone=1;
#endif
 int timer=config.timeout>0&&!p->config_error&&p->targets[selected].available&&!diagnostic;uint64_t deadline=(uint64_t)config.timeout*1000;
 char message[96]="";const char*report_status="";if(p->config_error){db_copy(message,sizeof message,"CONFIGURACAO INVALIDA - CONTAGEM DESATIVADA");diagnostic=1;report_status=message;}
 EFI_EVENT tick=0;if(EFI_ERROR(p->bs->CreateEvent(0x80000000u,0,0,0,&tick))||EFI_ERROR(p->bs->SetTimer(tick,1,500000))){if(tick)p->bs->CloseEvent(tick);platform_shutdown(p);return EFI_ABORTED;}
 int first=1;platform_usb_scan(p);
 for(;;){
  char shortcut=0;unsigned actions=platform_poll_input(p,&shortcut);int redraw=first||diagnostic||actions||shortcut||timer;if(actions&DB_ACTIVITY)timer=0;
  if(shortcut=='d'){diagnostic=1;timer=0;platform_log("DB:DIAGNOSTIC\n");}
  if(shortcut=='m'){diagnostic=0;standalone=0;timer=0;message[0]=0;report_status="";}
  if(diagnostic){
   if(shortcut=='f')report_status=platform_save_diagnostic(p)?"RELATORIO SALVO EM DIAGNOSTIC.TXT":"FALHA AO GRAVAR - CONFIRA A MIDIA USB";
   if(actions&DB_BACK){if(standalone){if(platform_stop_input(p))break;report_status="FALHA AO ENCERRAR USB - SAIDA BLOQUEADA";}else diagnostic=0;}
  }else{
   if(actions&DB_BACK){if(platform_stop_input(p))break;db_copy(message,sizeof message,"FALHA AO ENCERRAR USB - SAIDA BLOQUEADA");diagnostic=1;report_status=message;}if(actions&(DB_LEFT|DB_RIGHT))selected=1-selected;if(shortcut=='w')selected=0;if(shortcut=='s')selected=1;
   if((actions&DB_CONFIRM)||(timer&&p->ticks>=deadline)){
    timer=0;if(p->targets[selected].available){p->bs->SetTimer(tick,0,0);EFI_STATUS s=platform_launch(p,selected);p->bs->SetTimer(tick,1,500000);char code[17];hex_text(code,s,16);db_copy(message,sizeof message,"CARREGADOR RETORNOU: ");size_t n=length(message);db_copy(message+n,sizeof(message)-n,code);}
    else {db_copy(message,sizeof message,p->targets[selected].status);platform_log("DB:TARGET_UNAVAILABLE\n");}
    diagnostic=1;standalone=0;report_status=message;
   }
  }
  if(redraw){if(diagnostic)draw_diagnostic(p,report_status);else if(!draw_menu(p,&config,&assets,selected,timer?(uint32_t)((deadline-p->ticks+999)/1000):0,timer,message)){diagnostic=1;standalone=0;report_status="IMAGENS DO TEMA AUSENTES OU INVALIDAS - CONFIRA THEMES";draw_diagnostic(p,report_status);platform_log("DB:ASSET_INVALID\n");}}
  if(first){platform_log("DB:MENU\n");if(diagnostic)platform_log("DB:DIAGNOSTIC\n");first=0;}
  size_t event_index=0;p->bs->WaitForEvent(1,&tick,&event_index);p->ticks+=50;
 }
 p->bs->SetTimer(tick,0,0);p->bs->CloseEvent(tick);platform_free(p,assets.background);for(int i=0;i<2;i++)platform_free(p,assets.icons[i]);platform_shutdown(p);return EFI_SUCCESS;
}

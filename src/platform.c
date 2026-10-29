#include "platform.h"
#include "font.h"

void *memcpy(void*d,const void*s,size_t n){volatile uint8_t*a=d;const uint8_t*b=s;while(n--)*a++=*b++;return d;}
void *memset(void*d,int v,size_t n){volatile uint8_t*a=d;while(n--)*a++=(uint8_t)v;return d;}
int memcmp(const void*a,const void*b,size_t n){const uint8_t*x=a,*y=b;while(n--){if(*x!=*y)return *x-*y;x++;y++;}return 0;}
static EFI_GUID loaded_guid={0x5b1b31a1,0x9562,0x11d2,{0x8e,0x3f,0,0xa0,0xc9,0x69,0x72,0x3b}};
static EFI_GUID fs_guid={0x964e5b22,0x6459,0x11d2,{0x8e,0x39,0,0xa0,0xc9,0x69,0x72,0x3b}};
static EFI_GUID path_guid={0x09576e91,0x6d3f,0x11d2,{0x8e,0x39,0,0xa0,0xc9,0x69,0x72,0x3b}};
static EFI_GUID info_guid={0x09576e92,0x6d3f,0x11d2,{0x8e,0x39,0,0xa0,0xc9,0x69,0x72,0x3b}};
static EFI_GUID gop_guid={0x9042a9de,0x23dc,0x4a38,{0x96,0xfb,0x7a,0xde,0xd0,0x80,0x51,0x6a}};
static EFI_GUID usb_guid={0x2b2f68d6,0x0cd2,0x44cf,{0x8e,0x8b,0xbb,0xa2,0x0b,0x1b,0x5b,0x75}};
static EFI_GUID global_guid={0x8be4df61,0x93ca,0x11d2,{0xaa,0x0d,0,0xe0,0x98,0x03,0x2b,0x8c}};

static size_t slen(const char*s){size_t n=0;while(s[n])n++;return n;}
static void wide(CHAR16*d,size_t cap,const char*s){size_t n=0;while(s[n]&&n+1<cap){d[n]=(uint8_t)s[n];n++;}d[n]=0;}
static int join(char*out,size_t cap,const char*dir,const char*name){size_t a=slen(dir),b=slen(name);if(a+b+2>cap)return 0;memcpy(out,dir,a);out[a++]='\\';memcpy(out+a,name,b+1);return 1;}
static void*alloc(DbPlatform*p,size_t n){void*out=0;if(!n||n>64u*1024u*1024u)return 0;if(EFI_ERROR(p->bs->AllocatePool(2,n,&out)))return 0;return out;}
void platform_free(DbPlatform*p,void*m){if(m)p->bs->FreePool(m);}
void platform_log(const char*s){
#ifdef DB_TEST_IO
 for(;*s;s++)__asm__ volatile("outb %0,%1"::"a"((uint8_t)*s),"Nd"((uint16_t)0xe9));
#else
 (void)s;
#endif
}
void platform_console(DbPlatform*p,const char*s){CHAR16 b[256];wide(b,256,s);if(p->st->ConOut)p->st->ConOut->OutputString(p->st->ConOut,b);}
static size_t dp_length(const uint8_t*d){size_t n=0;while(n+4<=65535){size_t k=db_u16(d+n+2);if(k<4||k>65535-n)return 0;if(d[n]==0x7f&&d[n+1]==0xff)return k==4?n+4:0;n+=k;}return 0;}
static EFI_FILE*root_open(DbPlatform*p,EFI_HANDLE h){EFI_SIMPLE_FS*fs=0;EFI_FILE*r=0;if(EFI_ERROR(p->bs->HandleProtocol(h,&fs_guid,(void**)&fs)))return 0;if(EFI_ERROR(fs->OpenVolume(fs,&r)))return 0;return r;}
void *platform_read_file(DbPlatform*p,EFI_HANDLE volume,const char*path,size_t limit,size_t*length){
 *length=0;EFI_FILE*r=root_open(p,volume);if(!r)return 0;CHAR16 w[DB_PATH];wide(w,DB_PATH,path);EFI_FILE*f=0;
 EFI_STATUS status=r->Open(r,&f,w,1,0);r->Close(r);if(EFI_ERROR(status))return 0;
 uint8_t info[1024];size_t info_size=sizeof info;status=f->GetInfo(f,&info_guid,&info_size,info);
 if(EFI_ERROR(status)||info_size<80){f->Close(f);return 0;}
 uint64_t size=(uint64_t)db_u32(info+8)|((uint64_t)db_u32(info+12)<<32);
 if(size>limit||size>=DB_FILE_MAX||(db_u32(info+72)&0x10)){f->Close(f);return 0;}
 uint8_t*data=alloc(p,(size_t)size+1);if(!data){f->Close(f);return 0;}
 size_t got=(size_t)size;status=f->Read(f,&got,data);f->Close(f);
 if(EFI_ERROR(status)||got!=size){platform_free(p,data);return 0;}data[got]=0;*length=got;return data;
}
void *platform_read_local(DbPlatform*p,const char*name,size_t limit,size_t*len){char path[DB_PATH];*len=0;if(!join(path,sizeof path,p->directory,name))return 0;return platform_read_file(p,p->loaded->DeviceHandle,path,limit,len);}

int platform_init(DbPlatform*p,EFI_HANDLE image,EFI_SYSTEM_TABLE*st){
 memset(p,0,sizeof(*p));p->image=image;p->st=st;p->bs=st->BootServices;p->bs->SetWatchdogTimer(0,0,0,0);
 if(EFI_ERROR(p->bs->HandleProtocol(image,&loaded_guid,(void**)&p->loaded)))return 0;
 DbBootOption own={0};size_t n=dp_length((const uint8_t*)p->loaded->FilePath);
 if(n&&db_device_path(&own,(uint8_t*)p->loaded->FilePath,n))db_copy(p->own_path,DB_PATH,own.path);
 if(!p->own_path[0])db_copy(p->own_path,DB_PATH,"\\EFI\\BOOT\\BOOTX64.EFI");
 db_copy(p->directory,DB_PATH,p->own_path);size_t end=slen(p->directory);while(end&&p->directory[end]!='\\')end--;p->directory[end]=0;
 p->root=root_open(p,p->loaded->DeviceHandle);
 if(!EFI_ERROR(p->bs->LocateProtocol(&gop_guid,0,(void**)&p->gop))&&p->gop->Mode&&p->gop->Mode->Info){
  p->old_mode=p->gop->Mode->Mode;p->width=p->gop->Mode->Info->HorizontalResolution;p->height=p->gop->Mode->Info->VerticalResolution;
  if(p->width>=640&&p->height>=400&&p->width<=3840&&p->height<=2160)p->canvas=alloc(p,(size_t)p->width*p->height*4);
 }
 if(st->ConIn)st->ConIn->Reset(st->ConIn,0);
 platform_log("DB:START\n");return 1;
}

static void append(DbPlatform*p,const char*s){while(*s&&p->inventory_len+1<sizeof p->inventory)p->inventory[p->inventory_len++]=*s++;p->inventory[p->inventory_len]=0;}
static void hex_append(DbPlatform*p,uint64_t v,unsigned digits){static const char hx[]="0123456789ABCDEF";char b[17];if(digits>16)digits=16;for(unsigned i=0;i<digits;i++)b[i]=hx[(v>>((digits-i-1)*4))&15];b[digits]=0;append(p,b);}
static void guid_append(DbPlatform*p,const uint8_t*g){static const uint8_t order[16]={3,2,1,0,5,4,7,6,8,9,10,11,12,13,14,15};for(int i=0;i<16;i++){if(i==4||i==6||i==8||i==10)append(p,"-");hex_append(p,g[order[i]],2);}}
static int same_guid(const uint8_t*g,const char*text){uint8_t expected[16];return db_guid_parse(text,expected)&&memcmp(g,expected,16)==0;}
static int volume_guid(DbPlatform*p,EFI_HANDLE h,uint8_t*g){
 uint8_t*d=0;if(EFI_ERROR(p->bs->HandleProtocol(h,&path_guid,(void**)&d)))return 0;size_t n=dp_length(d),pos=0;
 while(pos+4<=n){size_t k=db_u16(d+pos+2);if(d[pos]==4&&d[pos+1]==1&&k==42&&d[pos+41]==2){memcpy(g,d+pos+24,16);return 1;}pos+=k;}return 0;
}
static EFI_HANDLE find_volume(DbPlatform*p,const uint8_t*g){
 EFI_HANDLE*handles=0,result=0;size_t count=0;if(EFI_ERROR(p->bs->LocateHandleBuffer(2,&fs_guid,0,&count,&handles)))return 0;
 for(size_t i=0;i<count;i++){uint8_t v[16];if(volume_guid(p,handles[i],v)&&!memcmp(v,g,16)){result=handles[i];break;}}
 platform_free(p,handles);return result;
}
static int exists(DbPlatform*p,EFI_HANDLE volume,const char*path){EFI_FILE*r=root_open(p,volume);if(!r)return 0;EFI_FILE*f=0;CHAR16 w[DB_PATH];wide(w,DB_PATH,path);EFI_STATUS s=r->Open(r,&f,w,1,0);r->Close(r);if(!EFI_ERROR(s)){f->Close(f);return 1;}return 0;}
static uint8_t*file_path(DbPlatform*p,EFI_HANDLE volume,const char*path,size_t*size){
 uint8_t*base=0;if(EFI_ERROR(p->bs->HandleProtocol(volume,&path_guid,(void**)&base)))return 0;size_t n=dp_length(base),chars=slen(path);if(!n)return 0;
 size_t fsize=4+(chars+1)*2,total=n-4+fsize+4;uint8_t*out=alloc(p,total);if(!out)return 0;memcpy(out,base,n-4);
 size_t pos=n-4;out[pos]=4;out[pos+1]=4;out[pos+2]=(uint8_t)fsize;out[pos+3]=(uint8_t)(fsize>>8);
 for(size_t i=0;i<=chars;i++){out[pos+4+i*2]=(uint8_t)path[i];out[pos+5+i*2]=0;}
 pos+=fsize;out[pos]=0x7f;out[pos+1]=0xff;out[pos+2]=4;out[pos+3]=0;*size=total;return out;
}
static void release_targets(DbPlatform*p){for(int i=0;i<2;i++){platform_free(p,p->targets[i].storage);memset(&p->targets[i],0,sizeof(DbTarget));}}
void platform_scan_boot(DbPlatform*p,const DbConfig*c){
 release_targets(p);p->inventory_len=0;append(p,"DualBoot 0.1.0 -- inventario UEFI em leitura\r\n\r\n");
 uint16_t order[256];size_t order_size=sizeof order;EFI_STATUS s=p->st->RuntimeServices->GetVariable((CHAR16*)L"BootOrder",&global_guid,0,&order_size,order);
 if(!EFI_ERROR(s)&&order_size<=sizeof order&&!(order_size%2)){
  size_t count=order_size/2;if(count>DB_BOOT_MAX)count=DB_BOOT_MAX;
  for(size_t i=0;i<count;i++){
   CHAR16 var[9]={'B','o','o','t',0,0,0,0,0};const char*hx="0123456789ABCDEF";for(int k=0;k<4;k++)var[4+k]=(CHAR16)hx[(order[i]>>((3-k)*4))&15];
   size_t size=0;s=p->st->RuntimeServices->GetVariable(var,&global_guid,0,&size,0);if(s!=EFI_BUFFER_TOO_SMALL||size>65535||size<10)continue;
   uint8_t*data=alloc(p,size);if(!data)continue;s=p->st->RuntimeServices->GetVariable(var,&global_guid,0,&size,data);DbBootOption b;
   if(EFI_ERROR(s)||!db_boot_option_parse(&b,data,size)){platform_free(p,data);continue;}
   append(p,"Boot");hex_append(p,order[i],4);append(p," ");append(p,b.label);append(p,"\r\n  ");append(p,b.path);append(p,"\r\n  GPT: ");if(b.has_guid)guid_append(p,b.guid);else append(p,"ausente");append(p,"  OptionalData bytes=0x");hex_append(p,b.options_bytes,4);append(p,"\r\n");
   int retained=0;
   for(int j=0;j<2;j++){
    const DbEntry*e=&c->entries[j];DbTarget*t=&p->targets[j];if(t->available||!e->enabled||!e->firmware||!b.has_guid||!db_equal(e->path,b.path))continue;
    if(e->guid[0]&&!same_guid(b.guid,e->guid))continue;
    EFI_HANDLE volume=find_volume(p,b.guid);if(!volume||!exists(p,volume,b.path))continue;
    if(volume==p->loaded->DeviceHandle&&db_equal(b.path,p->own_path))continue;
    t->volume=volume;t->storage=data;t->option=b;t->boot_id=order[i];t->available=1;db_copy(t->status,sizeof t->status,"Entrada UEFI validada");retained=1;break;
   }if(!retained)platform_free(p,data);
  }
 }else append(p,"BootOrder nao disponivel ou excede limite.\r\n");
 for(int j=0;j<2;j++){
  DbTarget*t=&p->targets[j];const DbEntry*e=&c->entries[j];
  if(!e->firmware&&e->enabled){
   EFI_HANDLE h=0;if(e->guid[0]){uint8_t g[16];if(db_guid_parse(e->guid,g))h=find_volume(p,g);}else h=p->loaded->DeviceHandle;
   if(h&&exists(p,h,e->path)&&!(h==p->loaded->DeviceHandle&&db_equal(e->path,p->own_path))){t->volume=h;t->available=1;t->boot_id=0xffff;db_copy(t->option.path,DB_PATH,e->path);db_copy(t->status,sizeof t->status,"Arquivo explicito validado");}
  }
  if(!t->available)db_copy(t->status,sizeof t->status,e->enabled?"Entrada original nao localizada":"Destino desativado");
 }
 platform_log("DB:SCAN_DONE\n");
}

static EFI_STATUS EFIAPI usb_callback(void*data,size_t length,void*context,uint32_t result){
 DbUsb*u=context;DbPad pad;if(result){u->errors++;u->failed=1;DbPad empty={0};u->pad=empty;return EFI_SUCCESS;}
 if(db_pad_decode(&pad,data,length)){u->pad=pad;u->packets++;}return EFI_SUCCESS;
}
static int usb_stop(DbPlatform*p){
 if(p->usb.active){EFI_USB_IO*current=0;if(!EFI_ERROR(p->bs->HandleProtocol(p->usb.handle,&usb_guid,(void**)&current))&&current==p->usb.io){
  EFI_STATUS s=current->UsbAsyncInterruptTransfer(current,p->usb.endpoint,0,0,0,0,0);
  if(EFI_ERROR(s)){p->usb.failed=1;p->usb.errors++;return 0;}
 }}
 uint32_t errors=p->usb.errors;memset(&p->usb,0,sizeof p->usb);p->usb.errors=errors;memset(&p->nav,0,sizeof p->nav);
 return 1;
}
int platform_stop_input(DbPlatform*p){return usb_stop(p);}
void platform_usb_scan(DbPlatform*p){
 if(p->usb.active){EFI_USB_IO*current=0;if(!p->usb.failed&&!EFI_ERROR(p->bs->HandleProtocol(p->usb.handle,&usb_guid,(void**)&current))&&current==p->usb.io)return;if(!usb_stop(p))return;}
 EFI_HANDLE*handles=0;size_t count=0;if(EFI_ERROR(p->bs->LocateHandleBuffer(2,&usb_guid,0,&count,&handles)))return;
 for(size_t i=0;i<count;i++){
  EFI_USB_IO*io=0;EFI_USB_DEVICE_DESC dev;EFI_USB_INTERFACE_DESC iface;
  if(EFI_ERROR(p->bs->HandleProtocol(handles[i],&usb_guid,(void**)&io))||EFI_ERROR(io->UsbGetDeviceDescriptor(io,&dev))||dev.IdVendor!=0x045e||dev.IdProduct!=0x028e||EFI_ERROR(io->UsbGetInterfaceDescriptor(io,&iface)))continue;
  if(iface.InterfaceClass!=0xff||iface.InterfaceSubClass!=0x5d||iface.InterfaceProtocol!=1)continue;
  p->usb.seen=1;
  for(uint8_t k=0;k<iface.NumEndpoints;k++){
   EFI_USB_ENDPOINT_DESC ep;if(EFI_ERROR(io->UsbGetEndpointDescriptor(io,k,&ep))||!(ep.EndpointAddress&0x80)||(ep.Attributes&3)!=3||ep.MaxPacketSize<20||ep.MaxPacketSize>1024)continue;
   p->usb.handle=handles[i];p->usb.io=io;p->usb.endpoint=ep.EndpointAddress;
   EFI_STATUS status=io->UsbAsyncInterruptTransfer(io,ep.EndpointAddress,1,8,ep.MaxPacketSize,usb_callback,&p->usb);
   if(!EFI_ERROR(status)){p->usb.active=1;memset(&p->nav,0,sizeof p->nav);platform_log("DB:USB_ACTIVE\n");}
   else p->usb.errors++;break;
  }if(p->usb.active)break;
 }platform_free(p,handles);
}
unsigned platform_poll_input(DbPlatform*p,char*shortcut){
 unsigned actions=0;*shortcut=0;if(p->ticks%2000==0)platform_usb_scan(p);
 if(p->usb.active&&!p->usb.failed&&p->usb.packets){size_t tpl=p->bs->RaiseTPL(16);DbPad pad=p->usb.pad;p->bs->RestoreTPL(tpl);actions|=db_nav_update(&p->nav,&pad,p->ticks);}
 if(p->st->ConIn){EFI_INPUT_KEY key;for(int n=0;n<16&&!EFI_ERROR(p->st->ConIn->ReadKeyStroke(p->st->ConIn,&key));n++){
  actions|=DB_ACTIVITY;if(key.ScanCode==1||key.ScanCode==4)actions|=DB_LEFT;else if(key.ScanCode==2||key.ScanCode==3)actions|=DB_RIGHT;
  if(key.UnicodeChar==13)actions|=DB_CONFIRM;if(key.ScanCode==23||key.UnicodeChar==27)actions|=DB_BACK;
  if(key.UnicodeChar=='w'||key.UnicodeChar=='W')*shortcut='w';if(key.UnicodeChar=='s'||key.UnicodeChar=='S')*shortcut='s';if(key.UnicodeChar=='d'||key.UnicodeChar=='D')*shortcut='d';if(key.UnicodeChar=='m'||key.UnicodeChar=='M')*shortcut='m';if(key.ScanCode==12)*shortcut='f';
 }}return actions;
}
int platform_save_diagnostic(DbPlatform*p){
 if(!p->root)return 0;char path[DB_PATH];if(!join(path,sizeof path,p->directory,"diagnostic.txt"))return 0;CHAR16 w[DB_PATH];wide(w,DB_PATH,path);EFI_FILE*f=0;
 if(EFI_ERROR(p->root->Open(p->root,&f,w,0x8000000000000003ULL,0)))return 0;
 size_t original=p->inventory_len;int success=0;uint8_t info[1024];size_t info_len=sizeof info;
 if(EFI_ERROR(f->GetInfo(f,&info_guid,&info_len,info))||info_len<80)goto done;
 for(unsigned i=8;i<16;i++)info[i]=0;
 if(EFI_ERROR(f->SetInfo(f,&info_guid,info_len,info))||EFI_ERROR(f->SetPosition(f,0)))goto done;
 append(p,"\r\nUSB 045E:028E  FF/5D/01\r\n  Receptor inicializado: ");append(p,p->usb.active?"sim":"nao");append(p,"  Pacotes: 0x");hex_append(p,p->usb.packets,8);append(p,"  Erros: 0x");hex_append(p,p->usb.errors,8);append(p,"\r\n  Botoes: 0x");hex_append(p,p->usb.pad.buttons,4);append(p,"\r\n\r\n--- END OF CURRENT INVENTORY ---\r\n");
 size_t n=p->inventory_len;
 if(EFI_ERROR(f->Write(f,&n,p->inventory))||n!=p->inventory_len||EFI_ERROR(f->Flush(f)))goto done;
 uint8_t check[8];size_t checked=sizeof check;
 if(EFI_ERROR(f->SetPosition(f,0))||EFI_ERROR(f->Read(f,&checked,check))||checked!=sizeof check||memcmp(check,p->inventory,sizeof check))goto done;
 platform_log("DB:REPORT_READBACK_OK\n");
 success=1;platform_log("DB:REPORT_SAVED\n");
done:
 p->inventory_len=original;p->inventory[original]=0;if(EFI_ERROR(f->Close(f)))success=0;return success;
}
static void text_fallback(DbPlatform*p){
 platform_free(p,p->canvas);p->canvas=0;p->gop=0;
 if(p->st&&p->st->ConOut){p->st->ConOut->Reset(p->st->ConOut,0);p->st->ConOut->ClearScreen(p->st->ConOut);}
}
static void restore_graphics(DbPlatform*p){
 if(!p->gop||!p->canvas)return;
 if(!p->gop->Mode||!p->gop->Mode->Info){text_fallback(p);return;}
 if(p->gop->Mode->Mode!=p->old_mode||p->gop->Mode->Info->HorizontalResolution!=p->width||p->gop->Mode->Info->VerticalResolution!=p->height){
  EFI_STATUS status=p->gop->SetMode(p->gop,p->old_mode);
  if(EFI_ERROR(status)||!p->gop->Mode||!p->gop->Mode->Info||p->gop->Mode->Info->HorizontalResolution!=p->width||p->gop->Mode->Info->VerticalResolution!=p->height)text_fallback(p);
 }
}
static void clear_handoff_screen(DbPlatform*p){
 /* Windows retains the GOP framebuffer while drawing its boot animation. */
 if(p->gop&&p->gop->Mode&&p->gop->Mode->Info){
  uint32_t black=0;EFI_GOP_INFO*info=p->gop->Mode->Info;
  EFI_STATUS s=p->gop->Blt(p->gop,&black,0,0,0,0,0,info->HorizontalResolution,info->VerticalResolution,0);
  if(EFI_ERROR(s))s=p->gop->SetMode(p->gop,p->gop->Mode->Mode);
  if(EFI_ERROR(s))platform_log("DB:HANDOFF_CLEAR_FAILED\n");
 }else if(p->st&&p->st->ConOut)p->st->ConOut->ClearScreen(p->st->ConOut);
}
EFI_STATUS platform_launch(DbPlatform*p,unsigned index){
 if(index>1||!p->targets[index].available)return EFI_NOT_FOUND;DbTarget*t=&p->targets[index];size_t length=0;uint8_t*path=file_path(p,t->volume,t->option.path,&length);if(!path)return EFI_NOT_FOUND;
 EFI_HANDLE child=0;EFI_STATUS s=p->bs->LoadImage(0,p->image,(EFI_DEVICE_PATH*)path,0,0,&child);platform_free(p,path);
 if(EFI_ERROR(s)){if(child)p->bs->UnloadImage(child);platform_log("DB:LOAD_ERROR\n");return s;}
 EFI_LOADED_IMAGE*loaded=0;s=p->bs->HandleProtocol(child,&loaded_guid,(void**)&loaded);if(EFI_ERROR(s)){p->bs->UnloadImage(child);return s;}
 loaded->LoadOptionsSize=(uint32_t)t->option.options_bytes;loaded->LoadOptions=(void*)t->option.options;
 if(!usb_stop(p)){p->bs->UnloadImage(child);return EFI_ABORTED;}
 clear_handoff_screen(p);
 p->bs->SetWatchdogTimer(300,0,0,0);platform_log("DB:CHILD_START\n");size_t exit_size=0;CHAR16*exit_data=0;s=p->bs->StartImage(child,&exit_size,&exit_data);
 p->bs->SetWatchdogTimer(0,0,0,0);platform_free(p,exit_data);p->bs->UnloadImage(child);restore_graphics(p);platform_usb_scan(p);return s;
}
int platform_shutdown(DbPlatform*p){if(!usb_stop(p))return 0;release_targets(p);platform_free(p,p->canvas);p->canvas=0;if(p->root){p->root->Close(p->root);p->root=0;}return 1;}

void db_rect(DbPlatform*p,int x,int y,int w,int h,uint32_t color){
 if(!p->canvas||w<=0||h<=0)return;int right=x+w,bottom=y+h;if(x<0)x=0;if(y<0)y=0;if(right>(int)p->width)right=(int)p->width;if(bottom>(int)p->height)bottom=(int)p->height;
 for(int row=y;row<bottom;row++)for(int col=x;col<right;col++)p->canvas[(size_t)row*p->width+(size_t)col]=color;
}
void db_text(DbPlatform*p,int x,int y,const char*text,int scale,uint32_t color){
 if(scale<1)scale=1;for(;*text;text++,x+=6*scale){char c=*text;if(c>='a'&&c<='z')c=(char)(c-32);const uint8_t*rows=0;
  for(size_t i=0;i<sizeof db_font/sizeof db_font[0];i++)if(db_font[i].c==c){rows=db_font[i].rows;break;}
  if(!rows)continue;for(int yy=0;yy<7;yy++)for(int xx=0;xx<5;xx++)if(rows[yy]&(16>>xx))db_rect(p,x+xx*scale,y+yy*scale,scale,scale,color);
 }
}
void db_image(DbPlatform*p,const uint8_t*d,size_t len,int x,int y,int width,int height){
 DbBmp b;if(!p->canvas||width<=0||height<=0||!db_bmp_info(&b,d,len))return;
 for(int yy=0;yy<height;yy++){int dy=y+yy;if(dy<0||dy>=(int)p->height)continue;uint32_t sy=(uint32_t)((uint64_t)(unsigned)yy*b.height/(unsigned)height);if(!b.top_down)sy=b.height-sy-1;
  for(int xx=0;xx<width;xx++){int dx=x+xx;if(dx<0||dx>=(int)p->width)continue;uint32_t sx=(uint32_t)((uint64_t)(unsigned)xx*b.width/(unsigned)width);const uint8_t*pixel=d+b.offset+(size_t)sy*b.stride+(size_t)sx*(b.bpp/8);p->canvas[(size_t)dy*p->width+(size_t)dx]=(uint32_t)pixel[0]|((uint32_t)pixel[1]<<8)|((uint32_t)pixel[2]<<16);}
 }
}
void platform_present(DbPlatform*p){if(p->canvas&&p->gop&&EFI_ERROR(p->gop->Blt(p->gop,p->canvas,2,0,0,0,0,p->width,p->height,0)))text_fallback(p);}

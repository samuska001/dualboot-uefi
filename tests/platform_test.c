#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include "../src/platform.h"
static int starts,cancels,loaded_verified,watchdog;
static int cancel_fails,change_mode,restore_fails,restores;
static EFI_GOP_INFO mode_info={.HorizontalResolution=1280,.VerticalResolution=800};
static EFI_GOP_MODE mode={.Mode=2,.Info=&mode_info};
static EFI_GOP gop;
static int clears,fill_fails,require_clear;
static EFI_USB_CALLBACK receiver;static void*receiver_context;
static EFI_USB_IO io;static EFI_LOADED_IMAGE child;
static uint8_t device_path[4]={0x7f,0xff,4,0};
static const uint8_t options[6]={1,3,5,7,9,11};
#define CHECK(x) do { if(!(x)){fprintf(stderr,"PLATFORM FAIL %d: %s\n",__LINE__,#x);exit(1);} }while(0)
static EFI_STATUS EFIAPI pool(uint32_t type,size_t n,void**out){(void)type;*out=malloc(n);return *out?0:EFI_ABORTED;}
static EFI_STATUS EFIAPI release(void*p){free(p);return 0;}
static EFI_STATUS EFIAPI descriptor(EFI_USB_IO*self,EFI_USB_DEVICE_DESC*d){(void)self;memset(d,0,sizeof(*d));d->IdVendor=0x045e;d->IdProduct=0x028e;return 0;}
static EFI_STATUS EFIAPI interface(EFI_USB_IO*self,EFI_USB_INTERFACE_DESC*d){(void)self;memset(d,0,sizeof(*d));d->NumEndpoints=1;d->InterfaceClass=0xff;d->InterfaceSubClass=0x5d;d->InterfaceProtocol=1;return 0;}
static EFI_STATUS EFIAPI endpoint(EFI_USB_IO*self,uint8_t index,EFI_USB_ENDPOINT_DESC*d){(void)self;(void)index;memset(d,0,sizeof(*d));d->EndpointAddress=0x81;d->Attributes=3;d->MaxPacketSize=32;return 0;}
static EFI_STATUS EFIAPI transfer(EFI_USB_IO*self,uint8_t ep,uint8_t begin,size_t interval,size_t bytes,EFI_USB_CALLBACK cb,void*ctx){(void)self;(void)ep;(void)interval;(void)bytes;if(begin){starts++;receiver=cb;receiver_context=ctx;}else {if(cancel_fails)return EFI_ABORTED;cancels++;}return 0;}
static EFI_STATUS EFIAPI protocol(EFI_HANDLE handle,EFI_GUID*g,void**out){if(g->a==0x2b2f68d6&&handle==(void*)1){*out=&io;return 0;}if(g->a==0x09576e91){*out=device_path;return 0;}if(g->a==0x5b1b31a1&&handle==(void*)2){*out=&child;return 0;}return EFI_NOT_FOUND;}
static EFI_STATUS EFIAPI locate(uint32_t method,EFI_GUID*g,void*search,size_t*n,EFI_HANDLE**h){(void)method;(void)g;(void)search;*h=malloc(sizeof(EFI_HANDLE));(*h)[0]=(void*)1;*n=1;return 0;}
static EFI_STATUS EFIAPI load(uint8_t policy,EFI_HANDLE parent,EFI_DEVICE_PATH*path,void*buffer,size_t bytes,EFI_HANDLE*out){(void)policy;(void)parent;(void)buffer;(void)bytes;CHECK(path->Type==4&&path->SubType==4);*out=(void*)2;return 0;}
static EFI_STATUS EFIAPI watch(size_t timeout,uint64_t code,size_t n,CHAR16*data){(void)code;(void)n;(void)data;watchdog=(int)timeout;return 0;}
static EFI_STATUS EFIAPI start(EFI_HANDLE h,size_t*n,CHAR16**out){(void)h;(void)n;(void)out;CHECK(cancels>0);CHECK(watchdog==300);CHECK(child.LoadOptionsSize==6);CHECK(!memcmp(child.LoadOptions,options,6));if(require_clear)CHECK(clears>0);if(change_mode){mode.Mode=0;mode_info.HorizontalResolution=640;mode_info.VerticalResolution=480;}loaded_verified++;return EFI_ABORTED;}
static EFI_STATUS EFIAPI blt(EFI_GOP*self,void*pixel,uint32_t op,size_t sx,size_t sy,size_t dx,size_t dy,size_t w,size_t h,size_t delta){CHECK(self==&gop);CHECK(op==0&&*(uint32_t*)pixel==0);CHECK(sx==0&&sy==0&&dx==0&&dy==0&&delta==0);CHECK(w==mode_info.HorizontalResolution&&h==mode_info.VerticalResolution);clears++;return fill_fails?EFI_ABORTED:EFI_SUCCESS;}
static EFI_STATUS EFIAPI set_mode(EFI_GOP*self,uint32_t target){(void)self;restores++;if(restore_fails)return EFI_ABORTED;mode.Mode=target;mode_info.HorizontalResolution=1280;mode_info.VerticalResolution=800;return 0;}
static EFI_STATUS EFIAPI unload(EFI_HANDLE h){(void)h;return 0;}
static int partial_blits,full_blits,reject_partial;
static EFI_STATUS EFIAPI region_blt(EFI_GOP*self,void*buffer,uint32_t op,size_t sx,size_t sy,size_t dx,size_t dy,size_t w,size_t h,size_t delta){
 CHECK(self==&gop&&buffer&&op==2);if(!delta){CHECK(sx==0&&sy==0&&dx==0&&dy==0&&w==1280&&h==800);full_blits++;return 0;}
 CHECK(sx==100&&sy==200&&dx==100&&dy==200&&w==300&&h==250&&delta==1280*4);partial_blits++;return reject_partial?EFI_ABORTED:EFI_SUCCESS;
}
int main(int argc,char**argv){
 EFI_BOOT_SERVICES bs={0};bs.AllocatePool=pool;bs.FreePool=release;bs.HandleProtocol=protocol;bs.LocateHandleBuffer=locate;bs.LoadImage=load;bs.StartImage=start;bs.UnloadImage=unload;bs.SetWatchdogTimer=watch;
 io.UsbGetDeviceDescriptor=descriptor;io.UsbGetInterfaceDescriptor=interface;io.UsbGetEndpointDescriptor=endpoint;io.UsbAsyncInterruptTransfer=transfer;
 DbPlatform p={0};p.bs=&bs;platform_usb_scan(&p);CHECK(starts==1);
 receiver(0,0,receiver_context,32);platform_usb_scan(&p);CHECK(starts==2);CHECK(cancels==1);
 p.targets[0].available=1;p.targets[0].volume=(void*)3;db_copy(p.targets[0].option.path,DB_PATH,"\\EFI\\TEST\\child.efi");p.targets[0].option.options=options;p.targets[0].option.options_bytes=6;
 CHECK(platform_launch(&p,0)==EFI_ABORTED);CHECK(loaded_verified==1);CHECK(watchdog==0);CHECK(starts==3);
 if(argc==1||!strcmp(argv[1],"cancel")){
  cancel_fails=1;int before=loaded_verified;CHECK(platform_launch(&p,0)==EFI_ABORTED);CHECK(loaded_verified==before);CHECK(p.usb.active);platform_shutdown(&p);CHECK(p.usb.active);cancel_fails=0;
 }
 if(argc==1||!strcmp(argv[1],"graphics")){
  gop.Mode=&mode;gop.Blt=blt;gop.SetMode=set_mode;p.gop=&gop;p.old_mode=2;p.width=1280;p.height=800;require_clear=1;
  CHECK(platform_launch(&p,0)==EFI_ABORTED);CHECK(clears==1&&restores==0);
  p.targets[1]=p.targets[0];CHECK(platform_launch(&p,1)==EFI_ABORTED);CHECK(clears==2);
  cancel_fails=1;CHECK(platform_launch(&p,0)==EFI_ABORTED);CHECK(clears==2);cancel_fails=0;
  fill_fails=1;CHECK(platform_launch(&p,0)==EFI_ABORTED);CHECK(restores==1);fill_fails=0;restores=0;
  gop.Mode=&mode;gop.SetMode=set_mode;p.gop=&gop;p.old_mode=2;p.width=1280;p.height=800;p.canvas=malloc(16);change_mode=1;CHECK(platform_launch(&p,0)==EFI_ABORTED);CHECK(mode.Mode==2&&restores==1);
  restore_fails=1;CHECK(platform_launch(&p,0)==EFI_ABORTED);CHECK(p.canvas==0&&p.gop==0);restore_fails=0;
 }
 {DbPlatform display={0};display.width=1280;display.height=800;display.canvas=calloc(1280u*800u,4);CHECK(display.canvas);display.gop=&gop;gop.Blt=region_blt;
  platform_present_region(&display,100,200,300,250);CHECK(partial_blits==1&&full_blits==0);
  reject_partial=1;platform_present_region(&display,100,200,300,250);CHECK(partial_blits==2&&full_blits==1&&display.canvas);
  platform_present_region(&display,-1,0,300,250);CHECK(full_blits==2);free(display.canvas);}
 platform_shutdown(&p);puts("PASS: USB recovery, EFI handoff and partial GOP transfer/fallback");return 0;
}

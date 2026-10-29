#ifndef DB_PLATFORM_H
#define DB_PLATFORM_H
#include "core.h"
#include "uefi.h"
#define DB_BOOT_MAX 64
typedef struct { EFI_HANDLE volume; uint8_t *storage; DbBootOption option; uint16_t boot_id; int available; char status[96]; } DbTarget;
typedef struct { EFI_HANDLE handle; EFI_USB_IO*io; uint8_t endpoint; int active,seen; volatile int failed; volatile uint32_t packets,errors; volatile DbPad pad; } DbUsb;
typedef struct { EFI_HANDLE image;EFI_SYSTEM_TABLE*st;EFI_BOOT_SERVICES*bs;EFI_LOADED_IMAGE*loaded;EFI_FILE*root;char directory[DB_PATH], own_path[DB_PATH];EFI_GOP*gop;uint32_t*canvas;uint32_t width,height,old_mode;DbUsb usb;DbNav nav;uint64_t ticks;DbTarget targets[2];char inventory[12288];size_t inventory_len;int config_error; } DbPlatform;
int platform_init(DbPlatform*p,EFI_HANDLE image,EFI_SYSTEM_TABLE*st);
int platform_shutdown(DbPlatform*p);
int platform_stop_input(DbPlatform*p);
void *platform_read_file(DbPlatform*p,EFI_HANDLE volume,const char*path,size_t limit,size_t*length);
void *platform_read_local(DbPlatform*p,const char*name,size_t limit,size_t*length);
void platform_free(DbPlatform*p,void*memory);
void platform_scan_boot(DbPlatform*p,const DbConfig*c);
unsigned platform_poll_input(DbPlatform*p,char*shortcut);
EFI_STATUS platform_launch(DbPlatform*p,unsigned index);
void platform_log(const char*s);
void platform_console(DbPlatform*p,const char*s);
void platform_usb_scan(DbPlatform*p);
void platform_present(DbPlatform*p);
int platform_save_diagnostic(DbPlatform*p);
void db_rect(DbPlatform*p,int x,int y,int w,int h,uint32_t color);
void db_text(DbPlatform*p,int x,int y,const char*text,int scale,uint32_t color);
void db_image(DbPlatform*p,const uint8_t*data,size_t length,int x,int y,int width,int height);
#endif

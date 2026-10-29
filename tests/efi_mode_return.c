#include "../src/uefi.h"
static void log_text(const char*s){while(*s)__asm__ volatile("outb %0,%1"::"a"((uint8_t)*s++),"Nd"((uint16_t)0xe9));}
EFI_STATUS EFIAPI EfiMain(EFI_HANDLE image,EFI_SYSTEM_TABLE*st){
 (void)image;EFI_GUID guid={0x9042a9de,0x23dc,0x4a38,{0x96,0xfb,0x7a,0xde,0xd0,0x80,0x51,0x6a}};EFI_GOP*gop=0;
 if(EFI_ERROR(st->BootServices->LocateProtocol(&guid,0,(void**)&gop)))return EFI_ABORTED;
 uint32_t original_w=gop->Mode->Info->HorizontalResolution,original_h=gop->Mode->Info->VerticalResolution;
 for(uint32_t i=0;i<gop->Mode->MaxMode;i++){
  size_t bytes=0;EFI_GOP_INFO*info=0;if(EFI_ERROR(gop->QueryMode(gop,i,&bytes,&info)))continue;
  int smaller=info->HorizontalResolution<original_w&&info->VerticalResolution<original_h;st->BootServices->FreePool(info);
  if(smaller&&!EFI_ERROR(gop->SetMode(gop,i))){log_text("DB:MODE_CHANGED\nDB:TEST_RETURN\n");return EFI_ABORTED;}
 }
 log_text("DB:NO_SMALLER_MODE\n");return EFI_ABORTED;
}

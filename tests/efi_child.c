#include "../src/uefi.h"
static void finish(const char*s,uint32_t code){
 while(*s)__asm__ volatile("outb %0,%1"::"a"((uint8_t)*s++),"Nd"((uint16_t)0xe9));
 __asm__ volatile("outl %0,%1"::"a"(code),"Nd"((uint16_t)0xf4));
}
EFI_STATUS EFIAPI EfiMain(EFI_HANDLE image,EFI_SYSTEM_TABLE*st){
 (void)image;EFI_GUID guid={0x9042a9de,0x23dc,0x4a38,{0x96,0xfb,0x7a,0xde,0xd0,0x80,0x51,0x6a}};EFI_GOP*gop=0;
 if(EFI_ERROR(st->BootServices->LocateProtocol(&guid,0,(void**)&gop))||!gop->Mode||!gop->Mode->Info){finish("DB:HANDOFF_NO_GOP\n",0x11);return EFI_ABORTED;}
 size_t w=gop->Mode->Info->HorizontalResolution,h=gop->Mode->Info->VerticalResolution;uint32_t*pixels=0;
 if(!w||!h||w>3840||h>2160||EFI_ERROR(st->BootServices->AllocatePool(2,w*h*4,(void**)&pixels))){finish("DB:HANDOFF_READ_FAILED\n",0x11);return EFI_ABORTED;}
 int black=!EFI_ERROR(gop->Blt(gop,pixels,1,0,0,0,0,w,h,0));
 if(black)for(size_t i=0;i<w*h;i++)if(pixels[i]&0xffffff){black=0;break;}
 st->BootServices->FreePool(pixels);
 finish(black?"DB:HANDOFF_BLACK\nDB:CHILD_OK\n":"DB:HANDOFF_DIRTY\n",black?0x10:0x11);return EFI_SUCCESS;
}

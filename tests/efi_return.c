#include "../src/uefi.h"
EFI_STATUS EFIAPI EfiMain(EFI_HANDLE image,EFI_SYSTEM_TABLE*st){
 (void)image;(void)st;const char*s="DB:TEST_RETURN\n";while(*s)__asm__ volatile("outb %0,%1"::"a"((uint8_t)*s++),"Nd"((uint16_t)0xe9));return EFI_ABORTED;
}

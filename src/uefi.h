/* Minimal UEFI 2.x ABI declarations used by DualBoot. Names/layouts from the UEFI specification.
 * This is not a motherboard firmware image and contains no firmware flashing routines. */
#ifndef DB_UEFI_H
#define DB_UEFI_H
#include <stdint.h>
#include <stddef.h>
#define EFIAPI __attribute__((ms_abi))
typedef uint64_t EFI_STATUS;
typedef void *EFI_HANDLE,*EFI_EVENT;
typedef uint16_t CHAR16;
typedef struct {uint32_t a;uint16_t b,c;uint8_t d[8];} EFI_GUID;
typedef struct {uint64_t Signature;uint32_t Revision,HeaderSize,CRC32,Reserved;} EFI_TABLE_HEADER;
typedef struct {uint8_t Type,SubType;uint16_t Length;} EFI_DEVICE_PATH;
#define EFI_ERROR(x) (((uint64_t)(x)>>63)!=0)
#define EFI_SUCCESS 0
#define EFI_BUFFER_TOO_SMALL (0x8000000000000000ULL|5)
#define EFI_NOT_FOUND (0x8000000000000000ULL|14)
#define EFI_ABORTED (0x8000000000000000ULL|21)
typedef struct {uint16_t ScanCode;CHAR16 UnicodeChar;} EFI_INPUT_KEY;
typedef struct EFI_TEXT_INPUT EFI_TEXT_INPUT;
struct EFI_TEXT_INPUT {EFI_STATUS(EFIAPI*Reset)(EFI_TEXT_INPUT*,uint8_t);EFI_STATUS(EFIAPI*ReadKeyStroke)(EFI_TEXT_INPUT*,EFI_INPUT_KEY*);EFI_EVENT WaitForKey;};
typedef struct EFI_TEXT_OUTPUT EFI_TEXT_OUTPUT;
struct EFI_TEXT_OUTPUT {
 EFI_STATUS(EFIAPI*Reset)(EFI_TEXT_OUTPUT*,uint8_t);
 EFI_STATUS(EFIAPI*OutputString)(EFI_TEXT_OUTPUT*,const CHAR16*);
 void*TestString;void*QueryMode;void*SetMode;
 EFI_STATUS(EFIAPI*SetAttribute)(EFI_TEXT_OUTPUT*,size_t);
 EFI_STATUS(EFIAPI*ClearScreen)(EFI_TEXT_OUTPUT*);
 EFI_STATUS(EFIAPI*SetCursorPosition)(EFI_TEXT_OUTPUT*,size_t,size_t);
 EFI_STATUS(EFIAPI*EnableCursor)(EFI_TEXT_OUTPUT*,uint8_t);void*Mode;
};
typedef struct EFI_BOOT_SERVICES {
 EFI_TABLE_HEADER Hdr;
 size_t(EFIAPI*RaiseTPL)(size_t);void(EFIAPI*RestoreTPL)(size_t);
 void*AllocatePages;void*FreePages;void*GetMemoryMap;
 EFI_STATUS(EFIAPI*AllocatePool)(uint32_t,size_t,void**);EFI_STATUS(EFIAPI*FreePool)(void*);
 EFI_STATUS(EFIAPI*CreateEvent)(uint32_t,size_t,void*,void*,EFI_EVENT*);
 EFI_STATUS(EFIAPI*SetTimer)(EFI_EVENT,uint32_t,uint64_t);
 EFI_STATUS(EFIAPI*WaitForEvent)(size_t,EFI_EVENT*,size_t*);
 void*SignalEvent;EFI_STATUS(EFIAPI*CloseEvent)(EFI_EVENT);void*CheckEvent;
 void*InstallProtocolInterface;void*ReinstallProtocolInterface;void*UninstallProtocolInterface;
 EFI_STATUS(EFIAPI*HandleProtocol)(EFI_HANDLE,EFI_GUID*,void**);
 void*Reserved;void*RegisterProtocolNotify;void*LocateHandle;void*LocateDevicePath;void*InstallConfigurationTable;
 EFI_STATUS(EFIAPI*LoadImage)(uint8_t,EFI_HANDLE,EFI_DEVICE_PATH*,void*,size_t,EFI_HANDLE*);
 EFI_STATUS(EFIAPI*StartImage)(EFI_HANDLE,size_t*,CHAR16**);
 EFI_STATUS(EFIAPI*Exit)(EFI_HANDLE,EFI_STATUS,size_t,CHAR16*);
 EFI_STATUS(EFIAPI*UnloadImage)(EFI_HANDLE);void*ExitBootServices;void*GetNextMonotonicCount;
 EFI_STATUS(EFIAPI*Stall)(size_t);
 EFI_STATUS(EFIAPI*SetWatchdogTimer)(size_t,uint64_t,size_t,CHAR16*);
 EFI_STATUS(EFIAPI*ConnectController)(EFI_HANDLE,EFI_HANDLE*,EFI_DEVICE_PATH*,uint8_t);
 void*DisconnectController;void*OpenProtocol;void*CloseProtocol;void*OpenProtocolInformation;void*ProtocolsPerHandle;
 EFI_STATUS(EFIAPI*LocateHandleBuffer)(uint32_t,EFI_GUID*,void*,size_t*,EFI_HANDLE**);
 EFI_STATUS(EFIAPI*LocateProtocol)(EFI_GUID*,void*,void**);
 void*InstallMultipleProtocolInterfaces;void*UninstallMultipleProtocolInterfaces;void*CalculateCrc32;void*CopyMem;void*SetMem;void*CreateEventEx;
} EFI_BOOT_SERVICES;
typedef struct {EFI_TABLE_HEADER Hdr;void*GetTime;void*SetTime;void*GetWakeupTime;void*SetWakeupTime;void*SetVirtualAddressMap;void*ConvertPointer;
 EFI_STATUS(EFIAPI*GetVariable)(const CHAR16*,EFI_GUID*,uint32_t*,size_t*,void*);
 void*GetNextVariableName;void*SetVariable;void*GetNextHighMonotonicCount;void*ResetSystem;
} EFI_RUNTIME_SERVICES;
typedef struct {EFI_TABLE_HEADER Hdr;CHAR16*FirmwareVendor;uint32_t FirmwareRevision;EFI_HANDLE ConsoleInHandle;EFI_TEXT_INPUT*ConIn;EFI_HANDLE ConsoleOutHandle;EFI_TEXT_OUTPUT*ConOut;EFI_HANDLE StandardErrorHandle;EFI_TEXT_OUTPUT*StdErr;EFI_RUNTIME_SERVICES*RuntimeServices;EFI_BOOT_SERVICES*BootServices;size_t NumberOfTableEntries;void*ConfigurationTable;} EFI_SYSTEM_TABLE;
typedef struct {uint32_t Revision;EFI_HANDLE ParentHandle;EFI_SYSTEM_TABLE*SystemTable;EFI_HANDLE DeviceHandle;EFI_DEVICE_PATH*FilePath;void*Reserved;uint32_t LoadOptionsSize;void*LoadOptions;void*ImageBase;uint64_t ImageSize;uint32_t ImageCodeType,ImageDataType;void*Unload;} EFI_LOADED_IMAGE;
typedef struct EFI_FILE EFI_FILE;
struct EFI_FILE {uint64_t Revision;EFI_STATUS(EFIAPI*Open)(EFI_FILE*,EFI_FILE**,const CHAR16*,uint64_t,uint64_t);EFI_STATUS(EFIAPI*Close)(EFI_FILE*);void*Delete;EFI_STATUS(EFIAPI*Read)(EFI_FILE*,size_t*,void*);EFI_STATUS(EFIAPI*Write)(EFI_FILE*,size_t*,void*);void*GetPosition;EFI_STATUS(EFIAPI*SetPosition)(EFI_FILE*,uint64_t);EFI_STATUS(EFIAPI*GetInfo)(EFI_FILE*,EFI_GUID*,size_t*,void*);EFI_STATUS(EFIAPI*SetInfo)(EFI_FILE*,EFI_GUID*,size_t,void*);EFI_STATUS(EFIAPI*Flush)(EFI_FILE*);};
typedef struct EFI_SIMPLE_FS EFI_SIMPLE_FS;
struct EFI_SIMPLE_FS {uint64_t Revision;EFI_STATUS(EFIAPI*OpenVolume)(EFI_SIMPLE_FS*,EFI_FILE**);};
typedef struct {uint32_t Version,HorizontalResolution,VerticalResolution,PixelFormat;uint32_t RedMask,GreenMask,BlueMask,ReservedMask;uint32_t PixelsPerScanLine;} EFI_GOP_INFO;
typedef struct {uint32_t MaxMode,Mode;EFI_GOP_INFO*Info;size_t SizeOfInfo;uint64_t FrameBufferBase;size_t FrameBufferSize;} EFI_GOP_MODE;
typedef struct EFI_GOP EFI_GOP;
struct EFI_GOP {EFI_STATUS(EFIAPI*QueryMode)(EFI_GOP*,uint32_t,size_t*,EFI_GOP_INFO**);EFI_STATUS(EFIAPI*SetMode)(EFI_GOP*,uint32_t);EFI_STATUS(EFIAPI*Blt)(EFI_GOP*,void*,uint32_t,size_t,size_t,size_t,size_t,size_t,size_t,size_t);EFI_GOP_MODE*Mode;};
#pragma pack(push,1)
typedef struct {uint8_t Length,DescriptorType;uint16_t BcdUSB;uint8_t DeviceClass,DeviceSubClass,DeviceProtocol,MaxPacketSize0;uint16_t IdVendor,IdProduct,BcdDevice;uint8_t StrManufacturer,StrProduct,StrSerialNumber,NumConfigurations;} EFI_USB_DEVICE_DESC;
typedef struct {uint8_t Length,DescriptorType,InterfaceNumber,AlternateSetting,NumEndpoints,InterfaceClass,InterfaceSubClass,InterfaceProtocol,Interface;} EFI_USB_INTERFACE_DESC;
typedef struct {uint8_t Length,DescriptorType,EndpointAddress,Attributes;uint16_t MaxPacketSize;uint8_t Interval;} EFI_USB_ENDPOINT_DESC;
#pragma pack(pop)
typedef struct EFI_USB_IO EFI_USB_IO;
typedef EFI_STATUS(EFIAPI*EFI_USB_CALLBACK)(void*,size_t,void*,uint32_t);
struct EFI_USB_IO {
 void*UsbControlTransfer;void*UsbBulkTransfer;
 EFI_STATUS(EFIAPI*UsbAsyncInterruptTransfer)(EFI_USB_IO*,uint8_t,uint8_t,size_t,size_t,EFI_USB_CALLBACK,void*);
 void*UsbSyncInterruptTransfer;void*UsbIsochronousTransfer;void*UsbAsyncIsochronousTransfer;
 EFI_STATUS(EFIAPI*UsbGetDeviceDescriptor)(EFI_USB_IO*,EFI_USB_DEVICE_DESC*);
 void*UsbGetConfigDescriptor;
 EFI_STATUS(EFIAPI*UsbGetInterfaceDescriptor)(EFI_USB_IO*,EFI_USB_INTERFACE_DESC*);
 EFI_STATUS(EFIAPI*UsbGetEndpointDescriptor)(EFI_USB_IO*,uint8_t,EFI_USB_ENDPOINT_DESC*);
 void*UsbGetStringDescriptor;void*UsbGetSupportedLanguages;void*UsbPortReset;
};
_Static_assert(sizeof(void*)==8,"x86-64 only");
_Static_assert(offsetof(EFI_SYSTEM_TABLE,BootServices)==96,"system table ABI");
_Static_assert(offsetof(EFI_BOOT_SERVICES,LoadImage)==200,"boot services ABI");
_Static_assert(offsetof(EFI_BOOT_SERVICES,LocateProtocol)==320,"boot services ABI");
_Static_assert(offsetof(EFI_LOADED_IMAGE,LoadOptions)==56,"image ABI");
_Static_assert(sizeof(EFI_USB_DEVICE_DESC)==18,"USB descriptor ABI");
_Static_assert(offsetof(EFI_RUNTIME_SERVICES,GetVariable)==72,"runtime ABI");
#endif

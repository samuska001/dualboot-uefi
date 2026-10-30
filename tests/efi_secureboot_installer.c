/* OVMF laboratory probe. No OS launch and no runtime-variable writes. */
#include "../src/platform.c"
static const volatile char test_sbat[] __attribute__((section(".sbat"),used))=
 "sbat,1,SBAT Version,sbat,1,https://github.com/rhboot/shim/blob/main/SBAT.md\n"
 "dualboot-test,1,DualBoot,dualboot-test,0.3.0,https://github.com/samuska001/dualboot-uefi\n";
typedef EFI_STATUS (__attribute__((sysv_abi))*ShimVerify)(void*,uint32_t);
typedef struct {ShimVerify Verify;void*Hash;void*Context;} ShimLock;
static void finish(int ok){platform_log(ok?"DB:INSTALLER_POLICY_PASS\n":"DB:INSTALLER_POLICY_FAIL\n");__asm__ volatile("outl %0,%1"::"a"((uint32_t)(ok?0x10:0x11)),"Nd"((uint16_t)0xf4));}
static int verify_file(DbPlatform*p,ShimLock*shim,const char*name,int valid){
 size_t n=0;void*data=platform_read_file(p,p->loaded->DeviceHandle,name,DB_FILE_MAX-1,&n);
 if(!data){platform_log("DB:POLICY_FIXTURE_MISSING\n");return 0;}
 EFI_STATUS s=shim->Verify(data,(uint32_t)n);platform_free(p,data);
 if(valid)return s==EFI_SUCCESS;
 return s==(0x8000000000000000ull|15)||s==(0x8000000000000000ull|26);
}
EFI_STATUS EFIAPI EfiMain(EFI_HANDLE image,EFI_SYSTEM_TABLE*st){
 DbPlatform p;if(test_sbat[0]!='s'||!platform_init(&p,image,st)){finish(0);return EFI_ABORTED;}
 uint8_t secure=0,setup=1;size_t n=1;EFI_STATUS s=st->RuntimeServices->GetVariable((CHAR16*)L"SecureBoot",&global_guid,0,&n,&secure);n=1;
 if(EFI_ERROR(s)||EFI_ERROR(st->RuntimeServices->GetVariable((CHAR16*)L"SetupMode",&global_guid,0,&n,&setup))||secure!=1||setup!=0){platform_log("DB:SECURE_NOT_ENFORCING\n");finish(0);return EFI_ABORTED;}
 EFI_GUID guid={0x605dab50,0xe046,0x4300,{0xab,0xb6,0x3d,0xd8,0x10,0xdd,0x8b,0x23}};ShimLock*shim=0;
 if(EFI_ERROR(p.bs->LocateProtocol(&guid,0,(void**)&shim))||!shim||!shim->Verify){finish(0);return EFI_ABORTED;}
 uint8_t disabled=0;n=1;s=st->RuntimeServices->GetVariable((CHAR16*)L"MokSBStateRT",&guid,0,&n,&disabled);
 if(s!=EFI_NOT_FOUND&&(EFI_ERROR(s)||n!=1||disabled!=0)){platform_log("DB:SHIM_VALIDATION_DISABLED\n");finish(0);return EFI_ABORTED;}
 const char*valid[]={"\\EFI\\BOOT\\menu.efi","\\EFI\\BOOT\\smoke.efi","\\EFI\\BOOT\\diag.efi"};
 for(unsigned i=0;i<3;i++)if(!verify_file(&p,shim,valid[i],1)){platform_log("DB:SIGNED_REJECTED\n");finish(0);return EFI_ABORTED;}
 platform_log("DB:SIGNED_ACCEPTED\n");
 const char*invalid[]={"\\EFI\\BOOT\\unsigned.efi","\\EFI\\BOOT\\tampered.efi","\\EFI\\BOOT\\wrong-cert.efi"};
 const char*markers[]={"DB:UNSIGNED_REJECTED\n","DB:TAMPERED_REJECTED\n","DB:WRONG_CERT_REJECTED\n"};
 for(unsigned i=0;i<3;i++){if(!verify_file(&p,shim,invalid[i],0)){platform_log("DB:INVALID_ACCEPTED\n");finish(0);return EFI_ABORTED;}platform_log(markers[i]);}
 platform_log("DB:SHIM_VALIDATION_ENABLED\n");finish(1);return EFI_SUCCESS;
}

/* Portable public binding and read-only firmware diagnostics. No SetVariable. */
#include "secureboot_report.h"
static const volatile DbSecureBootBinding binding __attribute__((section(".dbbind"),used))={DB_SB_BINDING_MAGIC,0,{0},{0},{0}};
static int equal(const uint8_t*a,const uint8_t*b,size_t n){for(size_t i=0;i<n;i++)if(a[i]!=b[i])return 0;return 1;}
static uint32_t le32(const uint8_t*p){return (uint32_t)p[0]|(uint32_t)p[1]<<8|(uint32_t)p[2]<<16|(uint32_t)p[3]<<24;}
static uint32_t rotr(uint32_t x,unsigned n){return x>>n|x<<(32-n);}
static void sha_block(uint32_t*h,const uint8_t*p){
 static const uint32_t k[64]={
 0x428a2f98,0x71374491,0xb5c0fbcf,0xe9b5dba5,0x3956c25b,0x59f111f1,0x923f82a4,0xab1c5ed5,
 0xd807aa98,0x12835b01,0x243185be,0x550c7dc3,0x72be5d74,0x80deb1fe,0x9bdc06a7,0xc19bf174,
 0xe49b69c1,0xefbe4786,0x0fc19dc6,0x240ca1cc,0x2de92c6f,0x4a7484aa,0x5cb0a9dc,0x76f988da,
 0x983e5152,0xa831c66d,0xb00327c8,0xbf597fc7,0xc6e00bf3,0xd5a79147,0x06ca6351,0x14292967,
 0x27b70a85,0x2e1b2138,0x4d2c6dfc,0x53380d13,0x650a7354,0x766a0abb,0x81c2c92e,0x92722c85,
 0xa2bfe8a1,0xa81a664b,0xc24b8b70,0xc76c51a3,0xd192e819,0xd6990624,0xf40e3585,0x106aa070,
 0x19a4c116,0x1e376c08,0x2748774c,0x34b0bcb5,0x391c0cb3,0x4ed8aa4a,0x5b9cca4f,0x682e6ff3,
 0x748f82ee,0x78a5636f,0x84c87814,0x8cc70208,0x90befffa,0xa4506ceb,0xbef9a3f7,0xc67178f2};
 uint32_t w[64];for(unsigned i=0;i<16;i++)w[i]=(uint32_t)p[i*4]<<24|(uint32_t)p[i*4+1]<<16|(uint32_t)p[i*4+2]<<8|p[i*4+3];
 for(unsigned i=16;i<64;i++){uint32_t x=w[i-15],y=w[i-2];w[i]=w[i-16]+(rotr(x,7)^rotr(x,18)^(x>>3))+w[i-7]+(rotr(y,17)^rotr(y,19)^(y>>10));}
 uint32_t a=h[0],b=h[1],c=h[2],d=h[3],e=h[4],f=h[5],g=h[6],v=h[7];
 for(unsigned i=0;i<64;i++){uint32_t t=v+(rotr(e,6)^rotr(e,11)^rotr(e,25))+((e&f)^(~e&g))+k[i]+w[i];uint32_t u=(rotr(a,2)^rotr(a,13)^rotr(a,22))+((a&b)^(a&c)^(b&c));v=g;g=f;f=e;e=d+t;d=c;c=b;b=a;a=t+u;}
 h[0]+=a;h[1]+=b;h[2]+=c;h[3]+=d;h[4]+=e;h[5]+=f;h[6]+=g;h[7]+=v;
}
static void sha256(const uint8_t*p,size_t n,char*out){
 uint32_t h[8]={0x6a09e667,0xbb67ae85,0x3c6ef372,0xa54ff53a,0x510e527f,0x9b05688c,0x1f83d9ab,0x5be0cd19};
 uint64_t bits=(uint64_t)n*8;while(n>=64){sha_block(h,p);p+=64;n-=64;}
 uint8_t tail[128]={0};for(size_t i=0;i<n;i++)tail[i]=p[i];tail[n]=0x80;size_t blocks=n<56?64:128;
 for(unsigned i=0;i<8;i++)tail[blocks-1-i]=(uint8_t)(bits>>(8*i));sha_block(h,tail);if(blocks==128)sha_block(h,tail+64);
 const char*hex="0123456789abcdef";for(unsigned i=0;i<64;i++)out[i]=hex[(h[i/8]>>(28-4*(i%8)))&15];
}
static int lowerhex(const char*p,size_t n){for(size_t i=0;i<n;i++)if(!((p[i]>='0'&&p[i]<='9')||(p[i]>='a'&&p[i]<='f')))return 0;return 1;}
int db_sb_binding_valid(const DbSecureBootBinding*b){
 if(!b||!equal(b->magic,(const uint8_t*)DB_SB_BINDING_MAGIC,32)||!b->certificate_length||b->certificate_length>DB_SB_CERT_MAX||!lowerhex(b->session_id,32)||!lowerhex(b->certificate_sha256,64))return 0;
 unsigned nonzero=0;for(unsigned i=0;i<32;i++)nonzero|=b->session_id[i]!='0';if(!nonzero)return 0;
 for(size_t i=b->certificate_length;i<DB_SB_CERT_MAX;i++)if(b->certificate[i])return 0;
 char hash[64];sha256(b->certificate,b->certificate_length,hash);return equal((const uint8_t*)hash,(const uint8_t*)b->certificate_sha256,64);
}
int db_sb_mok_contains(const uint8_t*data,size_t n,const uint8_t*cert,size_t certn){
 static const uint8_t x509[16]={0xa1,0x59,0xc0,0xa5,0xe4,0x94,0xa7,0x4a,0x87,0xb5,0xab,0x15,0x5c,0x2b,0xf0,0x72};
 if((n&&!data)||!cert||!certn||certn>DB_SB_CERT_MAX)return -1;int found=0;
 while(n){if(n<28)return -1;uint32_t list=le32(data+16),header=le32(data+20),sig=le32(data+24);
  if(list<28||list>n||header>list-28||sig<16)return -1;size_t entries=list-28-header;if(entries%sig)return -1;
  if(equal(data,x509,16)&&sig-16==certn){for(size_t at=28+header;at<list;at+=sig)if(equal(data+at+16,cert,certn))found=1;}
  data+=list;n-=list;
 }return found;
}
static int append(char*out,size_t cap,size_t*at,const char*s,size_t n){if(*at>=cap||n>=cap-*at)return 0;for(size_t i=0;i<n;i++)out[(*at)++]=s[i];out[*at]=0;return 1;}
static int field(char*out,size_t cap,size_t*at,const char*key,const char*value,size_t n){size_t k=0;while(key[k])k++;return append(out,cap,at,key,k)&&append(out,cap,at,value,n)&&append(out,cap,at,"\r\n",2);}
size_t db_sb_format(char*out,size_t cap,const DbSecureBootBinding*b,char secure,char setup,char mok,char shim,char disabled){
 if(!out||!db_sb_binding_valid(b))return 0;char flags[5]={secure,setup,mok,shim,disabled};for(unsigned i=0;i<5;i++)if(flags[i]!='0'&&flags[i]!='1'&&flags[i]!='?')return 0;
 size_t at=0;if(!field(out,cap,&at,"Format=","DualBootSecureBootStatusV2",26)||!field(out,cap,&at,"SessionId=",b->session_id,32)||!field(out,cap,&at,"CertificateSHA256=",b->certificate_sha256,64))return 0;
 const char*keys[5]={"SecureBoot=","SetupMode=","LocalCertificateInMokListRT=","ShimProtocolPresent=","ShimValidationDisabled="};
 for(unsigned i=0;i<5;i++)if(!field(out,cap,&at,keys[i],flags+i,1))return 0;return at;
}
#ifndef DB_SB_HOST_TEST
static EFI_GUID global={0x8be4df61,0x93ca,0x11d2,{0xaa,0x0d,0x00,0xe0,0x98,0x03,0x2b,0x8c}};
static EFI_GUID shim={0x605dab50,0xe046,0x4300,{0xab,0xb6,0x3d,0xd8,0x10,0xdd,0x8b,0x23}};
static EFI_GUID file_info={0x09576e92,0x6d3f,0x11d2,{0x8e,0x39,0x00,0xa0,0xc9,0x69,0x72,0x3b}};
static char flag(DbPlatform*p,const CHAR16*name,EFI_GUID*guid,int absent_zero){
 uint8_t value=0;size_t n=1;EFI_STATUS s=p->st->RuntimeServices->GetVariable(name,guid,0,&n,&value);
 if(s==EFI_NOT_FOUND&&absent_zero)return '0';return !EFI_ERROR(s)&&n==1&&value<=1?(char)('0'+value):'?';
}
static char mok_present(DbPlatform*p,const DbSecureBootBinding*b){
 size_t n=0;EFI_STATUS s=p->st->RuntimeServices->GetVariable((CHAR16*)L"MokListRT",&shim,0,&n,0);
 if(s==EFI_NOT_FOUND)return '0';if(s!=EFI_BUFFER_TOO_SMALL||n<28||n>1024*1024)return '?';
 size_t capacity=n;uint8_t*data=0;if(EFI_ERROR(p->bs->AllocatePool(2,n,(void**)&data))||!data)return '?';
 s=p->st->RuntimeServices->GetVariable((CHAR16*)L"MokListRT",&shim,0,&n,data);int result=-1;
 if(!EFI_ERROR(s)&&n<=capacity)result=db_sb_mok_contains(data,n,b->certificate,b->certificate_length);p->bs->FreePool(data);return result<0?'?':(char)('0'+result);
}
void db_secureboot_report(DbPlatform*p){
 /* volatile copy is essential: compiler must not fold the unsigned template's zero length. */
 DbSecureBootBinding b;for(size_t i=0;i<sizeof b;i++)((uint8_t*)&b)[i]=((const volatile uint8_t*)&binding)[i];
 if(!db_sb_binding_valid(&b)){platform_log("DB:SECURE_BINDING_INVALID\n");return;}
 void*protocol=0;char text[512];size_t size=db_sb_format(text,sizeof text,&b,flag(p,(CHAR16*)L"SecureBoot",&global,0),flag(p,(CHAR16*)L"SetupMode",&global,0),mok_present(p,&b),!EFI_ERROR(p->bs->LocateProtocol(&shim,0,&protocol))&&protocol?'1':'0',flag(p,(CHAR16*)L"MokSBStateRT",&shim,1));
 if(!size||!p->root)return;CHAR16 path[DB_PATH];size_t at=0;
 while(p->directory[at]){if(at+1>=DB_PATH)return;path[at]=(uint8_t)p->directory[at];at++;}
 if(at&&path[at-1]!='\\'){if(at+1>=DB_PATH)return;path[at++]='\\';}
 const char*name="secureboot-status.txt";for(size_t i=0;name[i];i++){if(at+1>=DB_PATH)return;path[at++]=(CHAR16)name[i];}path[at]=0;
 EFI_FILE*f=0;if(EFI_ERROR(p->root->Open(p->root,&f,path,0x8000000000000003ULL,0)))return;
 uint8_t info[1024];size_t n=sizeof info;int ok=0;
 if(EFI_ERROR(f->GetInfo(f,&file_info,&n,info))||n<80||n>sizeof info)goto done;
 for(unsigned i=8;i<16;i++)info[i]=0;
 if(EFI_ERROR(f->SetInfo(f,&file_info,n,info))||EFI_ERROR(f->SetPosition(f,0)))goto done;
 n=size;if(EFI_ERROR(f->Write(f,&n,text))||n!=size||EFI_ERROR(f->Flush(f)))goto done;
 char check[513];n=size+1;
 if(EFI_ERROR(f->SetPosition(f,0))||EFI_ERROR(f->Read(f,&n,check))||n!=size||!equal((uint8_t*)check,(uint8_t*)text,size))goto done;
 ok=1;
 done:if(EFI_ERROR(f->Close(f)))ok=0;platform_log(ok?"DB:SECURE_STATUS_SAVED\n":"DB:SECURE_STATUS_SAVE_FAILED\n");
}
#endif

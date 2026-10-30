#include <assert.h>
#include <string.h>
#include <stdio.h>
#include "../src/secureboot_report.h"
static void u32(unsigned char*p,unsigned v){p[0]=v;p[1]=v>>8;p[2]=v>>16;p[3]=v>>24;}
int main(void){
 DbSecureBootBinding b={0}; memcpy(b.magic,DB_SB_BINDING_MAGIC,32);
 b.certificate_length=3; memcpy(b.certificate,"abc",3);
 memcpy(b.session_id,"1234567890abcdef1234567890abcdef",32);
 memcpy(b.certificate_sha256,"ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad",64);
 assert(db_sb_binding_valid(&b));
 b.certificate[0]='x';assert(!db_sb_binding_valid(&b));b.certificate[0]='a';
 b.certificate_length=4097;assert(!db_sb_binding_valid(&b));b.certificate_length=3;
 b.session_id[0]='G';assert(!db_sb_binding_valid(&b));b.session_id[0]='1';
 memset(b.session_id,'0',32);assert(!db_sb_binding_valid(&b));memcpy(b.session_id,"1234567890abcdef1234567890abcdef",32);
 b.certificate[4095]=1;assert(!db_sb_binding_valid(&b));b.certificate[4095]=0;
 unsigned char list[66]={0};
 const unsigned char x509[]={0xa1,0x59,0xc0,0xa5,0xe4,0x94,0xa7,0x4a,0x87,0xb5,0xab,0x15,0x5c,0x2b,0xf0,0x72};
 memcpy(list,x509,16);u32(list+16,47);u32(list+24,19);memcpy(list+44,"abc",3);
 assert(db_sb_mok_contains(list,47,b.certificate,3)==1);
 assert(db_sb_mok_contains(list,46,b.certificate,3)==-1);
 list[0]=0;assert(db_sb_mok_contains(list,47,b.certificate,3)==0);list[0]=0xa1;
 u32(list+24,0);assert(db_sb_mok_contains(list,47,b.certificate,3)==-1);u32(list+24,19);
 u32(list+20,1);assert(db_sb_mok_contains(list,47,b.certificate,3)==-1);u32(list+20,0);
 /* A matching first list cannot hide malformed trailing data. */
 assert(db_sb_mok_contains(list,48,b.certificate,3)==-1);
 assert(db_sb_mok_contains(0,0,b.certificate,3)==0);
 char text[512];assert(db_sb_format(text,sizeof text,&b,'1','0','1','1','0')>0);
 assert(strstr(text,"Format=DualBootSecureBootStatusV2\r\nSessionId=1234567890abcdef1234567890abcdef\r\n"));
 assert(strstr(text,"CertificateSHA256=ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad\r\n"));
 assert(!db_sb_format(text,12,&b,'1','0','1','1','0'));
 assert(!db_sb_format(text,sizeof text,&b,'9','0','1','1','0'));
 assert(db_sb_format(text,sizeof text,&b,'?','?','?','0','?'));
 assert(strstr(text,"SecureBoot=?\r\n"));
 unsigned lengths[]={55,56,64,65,4096};
 const char*hashes[]={"463eb28e72f82e0a96c0a4cc53690c571281131f672aa229e0d45ae59b598b59","da2ae4d6b36748f2a318f23e7ab1dfdf45acdc9d049bd80e59de82a60895f562","fdeab9acf3710362bd2658cdc9a29e8f9c757fcf9811603a8c447cd1d9151108","4bfd2c8b6f1eec7a2afeb48b934ee4b2694182027e6d0fc075074f2fabb31781","d67c656e01756650d77717b0839985a056ec28ffe174601d690fc407a2ceffca"};
 for(unsigned j=0;j<5;j++){
  memset(b.certificate,0,sizeof b.certificate);b.certificate_length=lengths[j];
  for(unsigned i=0;i<lengths[j];i++)b.certificate[i]=(unsigned char)(i%251);
  memcpy(b.certificate_sha256,hashes[j],64);assert(db_sb_binding_valid(&b));
 }
 /* GUID/header/owner bytes are not certificate entries. */
 memset(list,0,sizeof list);memcpy(list,x509,16);u32(list+16,50);u32(list+20,3);u32(list+24,19);
 memcpy(list+28,"abc",3);memcpy(list+31,"abc",3);
 assert(db_sb_mok_contains(list,50,(const unsigned char*)"abc",3)==0);
 memcpy(list+47,"abc",3);assert(db_sb_mok_contains(list,50,(const unsigned char*)"abc",3)==1);
 for(unsigned i=0;i<50;i++)assert(db_sb_mok_contains(list,i,(const unsigned char*)"abc",3)==(i==0?0:-1));
 puts("secureboot report tests passed");return 0;
}


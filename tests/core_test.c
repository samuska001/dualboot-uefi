#include <stdio.h>
#include <string.h>
#include <stdlib.h>
#include "../src/core.h"
static unsigned checks;
#define CHECK(x) do { ++checks; if (!(x)) { fprintf(stderr,"FAIL line %d: %s\n",__LINE__,#x); exit(1); } } while(0)
static void config_tests(void) {
 DbConfig c; db_defaults(&c);
 CHECK(c.timeout==15 && c.selected==0);
 const char *ok="[menu]\ntitle=Meu PC\ntimeout=0\ndefault=steamos\ntheme=night\n[windows]\nlabel=Trabalho\npartition_guid=12345678-1234-4234-8234-123456789abc\npath=\\EFI\\Microsoft\\Boot\\bootmgfw.efi\nsource=firmware\n";
 CHECK(db_config_parse(&c,ok,strlen(ok)));
 CHECK(c.timeout==0 && c.selected==1 && !strcmp(c.entries[0].label,"Trabalho"));
 const char*order="[menu]\norder=steamos,windows\n";CHECK(db_config_parse(&c,order,strlen(order)));CHECK(c.first==1);
 const char*theme="[theme]\naccent=FF8844\n"; CHECK(db_theme_parse(&c,theme,strlen(theme))); CHECK(c.accent==0xff8844);
 const char*bad_theme="[windows]\nsource=file\n"; CHECK(!db_theme_parse(&c,bad_theme,strlen(bad_theme)));
 CHECK(!db_config_parse(&c,"[menu]\ntimeout=-1",strlen("[menu]\ntimeout=-1")));
 CHECK(!db_config_parse(&c,"[menu]\ntimeout=999999999999",strlen("[menu]\ntimeout=999999999999")));
 CHECK(!db_config_parse(&c,"[menu]\ntheme=../escape",strlen("[menu]\ntheme=../escape")));
 CHECK(!db_config_parse(&c,"[windows]\npath=\\EFI\\..\\boot.efi",strlen("[windows]\npath=\\EFI\\..\\boot.efi")));
 CHECK(!db_config_parse(&c,"[menu]\ndefault=other",strlen("[menu]\ndefault=other")));
 CHECK(!db_config_parse(&c,"[menu]\ntiemout=2",strlen("[menu]\ntiemout=2")));
 CHECK(!db_config_parse(&c,"[windows]\npartition_guid=garbage",strlen("[windows]\npartition_guid=garbage")));
 const char nul[]="[menu]\ntitle=bad\0hidden"; CHECK(!db_config_parse(&c,nul,sizeof(nul)-1));
 CHECK(!db_safe_path("\\EFI\\test.exe") && !db_safe_path("EFI\\test.efi"));
 CHECK(!db_safe_path("\\EFI\\BOOT\\.\\BOOTX64.EFI"));
 CHECK(!db_safe_path("\\EFI\\BOOT\\\\BOOTX64.EFI"));
 CHECK(!db_safe_path("\\EFI\\BOOT.\\BOOTX64.EFI"));
 uint8_t g[16]; CHECK(db_guid_parse("12345678-1234-4234-8234-123456789abc",g)); CHECK(g[0]==0x78 && g[1]==0x56 && g[15]==0xbc);
}
static void pad_tests(void) {
 uint8_t report[20]={0,20,8,16,0,0,0xff,0x7f}; DbPad p={0};
 CHECK(!db_pad_decode(&p,report,3)); CHECK(!db_pad_decode(&p,report,19));
 CHECK(db_pad_decode(&p,report,20)); CHECK(p.buttons==0x1008 && p.lx==32767);
 report[0]=1; CHECK(!db_pad_decode(&p,report,20)); report[0]=0; report[1]=19; CHECK(!db_pad_decode(&p,report,20));
 DbNav n={0}; p=(DbPad){.buttons=0x1000}; CHECK(!db_nav_update(&n,&p,0)); CHECK(!(db_nav_update(&n,&p,100)&DB_CONFIRM));
 p.buttons=0; db_nav_update(&n,&p,200); p.buttons=0x1000; CHECK(db_nav_update(&n,&p,300)&DB_CONFIRM);
 p.buttons=0x3000; CHECK(!(db_nav_update(&n,&p,400)&DB_CONFIRM)); CHECK(db_nav_update(&n,&p,401)==0);
 n=(DbNav){0}; p=(DbPad){0}; db_nav_update(&n,&p,0); p.lx=7800; CHECK(!db_nav_update(&n,&p,10));
 p.lx=20000; CHECK(db_nav_update(&n,&p,20)&DB_RIGHT); CHECK(!db_nav_update(&n,&p,100)); CHECK(db_nav_update(&n,&p,471)&DB_RIGHT);
 p=(DbPad){0}; db_nav_update(&n,&p,480); p.buttons=4; CHECK(db_nav_update(&n,&p,500)&DB_LEFT);
}
static void bmp_tests(void) {
 uint8_t b[70]={0}; DbBmp bmp;
 b[0]='B';b[1]='M';b[2]=70;b[10]=54;b[14]=40;b[18]=2;b[22]=2;b[26]=1;b[28]=24;
 CHECK(db_bmp_info(&bmp,b,sizeof b)); CHECK(bmp.stride==8 && bmp.height==2 && !bmp.top_down);
 CHECK(!db_bmp_info(&bmp,b,69)); b[30]=1; CHECK(!db_bmp_info(&bmp,b,sizeof b)); b[30]=0;
 b[10]=10;CHECK(!db_bmp_info(&bmp,b,sizeof b));b[10]=54;b[18]=0xff;b[19]=0xff;b[20]=0xff;b[21]=0x7f;CHECK(!db_bmp_info(&bmp,b,sizeof b));
}
static void boot_tests(void) {
 uint8_t b[512]={1,0,0,0}; size_t path=10; b[6]='O'; b[8]=0;
 uint8_t *d=b+path; d[0]=4;d[1]=1;d[2]=42;d[40]=2;d[41]=2;d[24]=0x76;
 const char *file="\\EFI\\TEST\\child.efi"; size_t length=strlen(file), fsize=4+2*(length+1), plen=42+fsize+4;
 d[42]=4;d[43]=4;d[44]=(uint8_t)fsize;
 for(size_t i=0;i<length;i++) d[46+i*2]=(uint8_t)file[i];
 d[42+fsize]=0x7f;d[43+fsize]=0xff;d[44+fsize]=4;
 b[4]=(uint8_t)plen;b[5]=(uint8_t)(plen>>8);b[path+plen]=0xab;
 DbBootOption o; CHECK(db_boot_option_parse(&o,b,path+plen+1)); CHECK(o.options_bytes==1 && o.options[0]==0xab && o.has_guid && o.guid[0]==0x76 && !strcmp(o.path,file));
 CHECK(!db_boot_option_parse(&o,b,7)); d[2]=0;CHECK(!db_boot_option_parse(&o,b,path+plen+1));d[2]=42;
 b[4]=0xff;b[5]=0xff;CHECK(!db_boot_option_parse(&o,b,path+plen+1));
}
int main(void) { config_tests();pad_tests();bmp_tests();boot_tests(); printf("PASS: %u assertions\n",checks);return 0; }

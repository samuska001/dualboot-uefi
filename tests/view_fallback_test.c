/* Exercise actual view entry points with a failing firmware graphics service. */
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include "../src/main.c"
static char output[8192];static size_t output_len;
static EFI_STATUS EFIAPI output_string(EFI_TEXT_OUTPUT*self,const CHAR16*s){(void)self;while(*s&&output_len+1<sizeof output)output[output_len++]=(char)*s++;output[output_len]=0;return 0;}
static EFI_STATUS EFIAPI reset_console(EFI_TEXT_OUTPUT*self,uint8_t extended){(void)self;(void)extended;output_len=0;output[0]=0;return 0;}
static EFI_STATUS EFIAPI clear_console(EFI_TEXT_OUTPUT*self){(void)self;output_len=0;output[0]=0;return 0;}
static EFI_STATUS EFIAPI cursor(EFI_TEXT_OUTPUT*self,size_t x,size_t y){(void)self;(void)x;(void)y;return 0;}
static EFI_STATUS EFIAPI release_pool(void*p){free(p);return 0;}
static EFI_STATUS EFIAPI failed_blt(EFI_GOP*g,void*b,uint32_t operation,size_t sx,size_t sy,size_t dx,size_t dy,size_t width,size_t height,size_t delta){(void)g;(void)b;(void)operation;(void)sx;(void)sy;(void)dx;(void)dy;(void)width;(void)height;(void)delta;return EFI_ABORTED;}
#define CHECK(x) do{if(!(x)){fprintf(stderr,"VIEW FAIL %d: %s\n",__LINE__,#x);return 1;}}while(0)
int main(void){
 EFI_TEXT_OUTPUT console={0};console.OutputString=output_string;console.Reset=reset_console;console.ClearScreen=clear_console;console.SetCursorPosition=cursor;
 EFI_SYSTEM_TABLE st={0};st.ConOut=&console;EFI_BOOT_SERVICES bs={0};bs.FreePool=release_pool;EFI_GOP gop={0};gop.Blt=failed_blt;
 DbPlatform p={0};p.st=&st;p.bs=&bs;p.gop=&gop;p.width=640;p.height=400;p.canvas=calloc(640u*400u,4);CHECK(p.canvas);p.targets[0].available=p.targets[1].available=1;
 uint8_t b[58]={0};b[0]='B';b[1]='M';b[2]=58;b[10]=54;b[14]=40;b[18]=1;b[22]=1;b[26]=1;b[28]=24;b[56]=255;
 DbMenuAssets a={0};a.icons[0]=a.icons[1]=b;a.icon_len[0]=a.icon_len[1]=58;DbConfig c;db_defaults(&c);
 CHECK(draw_menu(&p,&c,&a,0,0,0,""));CHECK(!p.canvas);CHECK(strstr(output,"Windows")&&strstr(output,"SteamOS"));
 output_len=0;output[0]=0;draw_diagnostic(&p,"CARREGADOR RETORNOU: TEST ERROR");CHECK(strstr(output,"TEST ERROR"));
 puts("PASS: failed graphics immediately renders keyboard fallback and preserves diagnostic error");return 0;
}

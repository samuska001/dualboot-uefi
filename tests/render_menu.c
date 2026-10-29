/* Developer capture utility: production rasterizer, synthetic available targets, no EFI launch. */
#include "../src/menu.h"
#include <stdio.h>
#include <stdlib.h>
static uint8_t*read_asset(const char*name,size_t*length){char path[256];snprintf(path,sizeof path,"themes/night/%s",name);FILE*f=fopen(path,"rb");if(!f)return 0;fseek(f,0,SEEK_END);long n=ftell(f);rewind(f);if(n<=0||(unsigned long)n>DB_FILE_MAX){fclose(f);return 0;}uint8_t*data=malloc((size_t)n);if(!data||fread(data,1,(size_t)n,f)!=(size_t)n){free(data);fclose(f);return 0;}fclose(f);*length=(size_t)n;return data;}
int main(int argc,char**argv){
 if(argc!=5)return 2;DbPlatform p={0};p.width=(uint32_t)atoi(argv[1]);p.height=(uint32_t)atoi(argv[2]);unsigned selected=(unsigned)atoi(argv[3]);DbMenuLayout layout;if(!db_menu_layout(p.width,p.height,0,selected,&layout))return 2;
 p.canvas=malloc((size_t)p.width*p.height*4);if(!p.canvas)return 2;p.targets[0].available=p.targets[1].available=1;
 DbConfig c;db_defaults(&c);c.background=0x080b12;c.accent=0xe0e7ef;DbMenuAssets a={0};a.background=read_asset("background.bmp",&a.background_len);a.icons[0]=read_asset("windows.bmp",&a.icon_len[0]);a.icons[1]=read_asset("steamos.bmp",&a.icon_len[1]);
 if(!db_menu_draw(&p,&c,&a,selected,0,0))return 3;FILE*out=fopen(argv[4],"wb");if(!out)return 3;
 for(size_t i=0;i<(size_t)p.width*p.height;i++){uint8_t rgb[3]={(uint8_t)(p.canvas[i]>>16),(uint8_t)(p.canvas[i]>>8),(uint8_t)p.canvas[i]};if(fwrite(rgb,1,3,out)!=3)return 3;}
 fclose(out);free(a.background);free(a.icons[0]);free(a.icons[1]);free(p.canvas);printf("Captured production rasterizer %ux%u, selected=%u (no OS boot)\n",p.width,p.height,selected);return 0;
}

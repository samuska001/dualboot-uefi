/* Native CPU raster benchmark. Excludes GOP and is not a physical FPS measurement. */
#define main capture_main
#include "render_menu.c"
#undef main
#include <time.h>
int main(void){
 DbConfig c;db_defaults(&c);DbMenuAssets a={0};
 a.background=read_asset("background.bmp",&a.background_len);a.icons[0]=read_asset("windows.bmp",&a.icon_len[0]);a.icons[1]=read_asset("steamos.bmp",&a.icon_len[1]);
 const unsigned sizes[][2]={{1920,1080},{3840,2160}};
 for(unsigned n=0;n<2;n++){
  DbPlatform p={0};p.width=sizes[n][0];p.height=sizes[n][1];p.canvas=malloc((size_t)p.width*p.height*4);p.targets[0].available=p.targets[1].available=1;
  DbMenuCache cache={0};cache.capacity=db_menu_cache_bytes(p.width,p.height);cache.pixels=malloc(cache.capacity);a.cache=&cache;
  if(!p.canvas||!cache.pixels||!db_menu_draw(&p,&c,&a,0,0,0))return 2;
  clock_t start=clock();unsigned distinct=0;int previous=-1;
  for(unsigned f=0;f<60;f++){DbMenuLayout l;unsigned ms=(f%25)*16;db_menu_layout_frame(p.width,p.height,0,1,ms,&l);if(l.icons[1].width!=previous){distinct++;previous=l.icons[1].width;}if(!db_menu_draw_frame(&p,&c,&a,1,0,0,ms))return 3;}
  double elapsed=(double)(clock()-start)*1000.0/CLOCKS_PER_SEC;
  printf("%ux%u: CPU draw %.3f ms/frame (%u size changes / 60 samples); excludes firmware display\n",p.width,p.height,elapsed/60.0,distinct);
  free(cache.pixels);free(p.canvas);
 }
 free(a.background);free(a.icons[0]);free(a.icons[1]);return 0;
}

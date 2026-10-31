#include "../src/menu.h"
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#define CHECK(x) do{if(!(x)){fprintf(stderr,"FAIL menu line %d: %s\n",__LINE__,#x);return 1;}}while(0)
static int text_calls;
static void u32(uint8_t*p,uint32_t n){p[0]=(uint8_t)n;p[1]=(uint8_t)(n>>8);p[2]=(uint8_t)(n>>16);p[3]=(uint8_t)(n>>24);}
void db_text(DbPlatform*p,int x,int y,const char*s,int scale,uint32_t color){(void)p;(void)x;(void)y;(void)s;(void)scale;(void)color;text_calls++;}
static void bmp(uint8_t b[62],uint8_t r,uint8_t g,uint8_t blue){
 memset(b,0,62);b[0]='B';b[1]='M';b[2]=62;b[10]=54;b[14]=40;b[18]=2;b[22]=1;b[26]=1;b[28]=24;
 for(int i=0;i<2;i++){b[54+i*3]=blue;b[55+i*3]=g;b[56+i*3]=r;}
}
int main(void){
 DbMenuLayout l;CHECK(db_menu_layout(1920,1080,0,0,&l));
 CHECK(l.cards[0].width==256&&l.cards[1].width==236);
 CHECK(l.cards[0].x+l.cards[0].width/2==800&&l.cards[1].x+l.cards[1].width/2==1120);
 CHECK(db_menu_layout(1920,1080,1,1,&l));CHECK(l.cards[1].x<l.cards[0].x&&l.cards[1].width>l.cards[0].width);
 CHECK(!db_menu_layout(0,1080,0,0,&l));CHECK(!db_menu_layout(640,400,0,2,&l));
 const uint32_t sizes[][2]={{640,400},{1280,800},{1920,1080},{3840,2160},{1024,768},{640,1024}};
 uint8_t red[62],green[62];bmp(red,255,0,0);bmp(green,0,255,0);
 DbMenuAssets a={0};a.icons[0]=red;a.icons[1]=green;a.icon_len[0]=a.icon_len[1]=62;
 DbConfig c;db_defaults(&c);c.accent=0xe0e7ef;c.background=0x080b12;
 for(size_t n=0;n<sizeof sizes/sizeof sizes[0];n++)for(unsigned selected=0;selected<2;selected++){
  DbPlatform p={0};p.width=sizes[n][0];p.height=sizes[n][1];size_t pixels=(size_t)p.width*p.height;
  uint32_t *allocation=malloc((pixels+16)*sizeof(uint32_t));CHECK(allocation);for(size_t i=0;i<pixels+16;i++)allocation[i]=0xcafebabe;p.canvas=allocation+8;
  p.targets[0].available=p.targets[1].available=1;
  CHECK(db_menu_draw(&p,&c,&a,selected,0,0));CHECK(db_menu_layout(p.width,p.height,0,selected,&l));
  CHECK(p.canvas[0]==c.background);CHECK(p.canvas[(size_t)(p.height/2+p.height*90/1080)*p.width+p.width/2-p.width*160/1920]==c.background); /* no floor shadow */
  for(int i=0;i<2;i++){
   DbBox card=l.cards[i],icon=l.icons[i];CHECK(card.x>=0&&card.y>=0&&card.x+card.width<=(int)p.width&&card.y+card.height<=(int)p.height);
   CHECK(p.canvas[(size_t)(icon.y+icon.height/2)*p.width+(size_t)(icon.x+icon.width/2)]==(i?0x00ff00u:0xff0000u));
   CHECK(p.canvas[(size_t)(icon.y+icon.height/8)*p.width+(size_t)(icon.x+icon.width/2)]!=0xff0000u); /* wide source stays wide */
   CHECK(p.canvas[(size_t)(card.y+3)*p.width+(size_t)(card.x+card.width/2)]==c.background); /* no card or border */
  }
  for(size_t i=0;i<8;i++){CHECK(allocation[i]==0xcafebabe);CHECK(allocation[pixels+8+i]==0xcafebabe);}
  free(allocation);
 }
 {size_t bytes=54+476u*476u*3u;uint8_t*stripes=calloc(1,bytes);CHECK(stripes);stripes[0]='B';stripes[1]='M';u32(stripes+2,(uint32_t)bytes);stripes[10]=54;stripes[14]=40;u32(stripes+18,476);u32(stripes+22,476);stripes[26]=1;stripes[28]=24;
  for(unsigned y=0;y<476;y++)for(unsigned x=0;x<476;x++)for(unsigned ch=0;ch<3;ch++)stripes[54+(y*476+x)*3+ch]=(x&1)?255:0;
  DbPlatform p={0};p.width=1920;p.height=1080;p.targets[0].available=p.targets[1].available=1;p.canvas=malloc(1920u*1080u*4u);CHECK(p.canvas);a.icons[0]=stripes;a.icon_len[0]=bytes;
  CHECK(db_menu_draw(&p,&c,&a,0,0,0));uint32_t mix=p.canvas[540u*1920u+800u];CHECK((mix&255)==((mix>>8)&255)&&((mix>>8)&255)==((mix>>16)&255)&&(mix&255)>=120&&(mix&255)<=136); /* centered bilinear sampling remains neutral at the midpoint */
  free(p.canvas);free(stripes);
 }
 {DbMenuLayout start,peak,rest;CHECK(db_menu_layout_frame(1920,1080,0,1,0,&start));CHECK(db_menu_layout_frame(1920,1080,0,1,DB_FOCUS_MS/2,&peak));CHECK(db_menu_layout_frame(1920,1080,0,1,DB_FOCUS_MS,&rest));
  CHECK(start.icons[1].width<peak.icons[1].width&&peak.icons[1].width<rest.icons[1].width);
  CHECK(start.icons[0].width>rest.icons[0].width);
  for(unsigned ms=0;ms<=DB_FOCUS_MS;ms+=50){CHECK(db_menu_layout_frame(1920,1080,0,1,ms,&l));for(int i=0;i<2;i++){CHECK(l.icons[i].x+l.icons[i].width/2==(i?1120:800));CHECK(l.icons[i].y+l.icons[i].height/2==540);}}
  CHECK(db_menu_layout_frame(1920,1080,0,1,UINT64_MAX,&l));CHECK(l.icons[1].width==rest.icons[1].width);}
 {int last[2]={10000,0};for(unsigned ms=0;ms<=DB_FOCUS_MS;ms++){CHECK(db_menu_layout_frame(1920,1080,0,1,ms,&l));CHECK(l.icons[0].width<=last[0]&&l.icons[1].width>=last[1]);for(int i=0;i<2;i++){CHECK(2*l.icons[i].x+l.icons[i].width==(i?2240:1600));CHECK(2*l.icons[i].y+l.icons[i].height==1080);last[i]=l.icons[i].width;}}}
 {int previous=-1;unsigned changes=0,samples=0;for(unsigned ms=0;ms<=DB_FOCUS_MS;ms+=16){samples++;CHECK(db_menu_layout_frame(3840,2160,0,1,ms,&l));if(l.icons[1].width!=previous){changes++;previous=l.icons[1].width;}}CHECK(changes>=samples*3/4); /* no 50ms staircase at a 60Hz cadence */}
 {uint8_t alpha[62];bmp(alpha,255,0,0);alpha[28]=32;alpha[54]=0;alpha[55]=0;alpha[56]=255;alpha[57]=0;alpha[58]=255;alpha[59]=0;alpha[60]=0;alpha[61]=255;
  DbPlatform transparent={0};transparent.width=1920;transparent.height=1080;transparent.targets[0].available=transparent.targets[1].available=1;transparent.canvas=malloc(1920u*1080u*4u);CHECK(transparent.canvas);
  a.icons[0]=alpha;a.icon_len[0]=62;CHECK(db_menu_draw(&transparent,&c,&a,0,0,0));
  CHECK(((transparent.canvas[540u*1920u+775u]>>16)&255)<=8);CHECK(transparent.canvas[540u*1920u+900u]==0x0000ffu);free(transparent.canvas);}
 {DbMenuMotion response={0};db_menu_motion_update(&response,0,0);CHECK(db_menu_motion_update(&response,1,0));db_menu_motion_update(&response,1,60);CHECK(response.scale[1]>=65536u+(81265u-65536u)*65/100);CHECK(!db_menu_motion_update(&response,1,180));CHECK(response.scale[1]==81265u);}
 {DbMenuMotion m={0};CHECK(!db_menu_motion_update(&m,0,0));uint32_t left=m.scale[0],right=m.scale[1];CHECK(db_menu_motion_update(&m,1,50));CHECK(m.scale[0]==left&&m.scale[1]==right);
  CHECK(db_menu_motion_update(&m,1,200));left=m.scale[0];right=m.scale[1];CHECK(db_menu_motion_update(&m,0,200));CHECK(m.scale[0]==left&&m.scale[1]==right);
  for(unsigned now=201;now<=600;now++){db_menu_motion_update(&m,0,now);CHECK(m.scale[0]>=left&&m.scale[1]<=right);left=m.scale[0];right=m.scale[1];}CHECK(!m.active);CHECK(m.scale[0]==81265u&&m.scale[1]==65536u);
  db_menu_motion_update(&m,0,UINT64_MAX);CHECK(m.scale[0]==left&&m.scale[1]==right);}
 { /* Cached frames must match complete redraws, including focus reversal and timer. */
  a.icons[0]=red;a.icons[1]=green;a.icon_len[0]=a.icon_len[1]=62;
  for(size_t n=0;n<sizeof sizes/sizeof sizes[0];n++){
   DbPlatform cached={0},full={0};cached.width=full.width=sizes[n][0];cached.height=full.height=sizes[n][1];size_t bytes=(size_t)cached.width*cached.height*4;
   cached.canvas=malloc(bytes);full.canvas=malloc(bytes);CHECK(cached.canvas&&full.canvas);cached.targets[0].available=full.targets[0].available=1;cached.targets[1].available=full.targets[1].available=1;
   DbMenuCache cache={0};cache.capacity=db_menu_cache_bytes(cached.width,cached.height);cache.pixels=malloc(cache.capacity);CHECK(cache.pixels);DbMenuAssets ca=a;ca.cache=&cache;
   for(unsigned f=0;f<27;f++){if(f==13){cache.valid=0;memset(cached.canvas,0xab,bytes);}c.timeout=10;c.first=f>=20;
    CHECK(db_menu_draw_frame(&cached,&c,&ca,(f/7)%2,10-f%10,1,(f%7)*16));CHECK(db_menu_draw_frame(&full,&c,&a,(f/7)%2,10-f%10,1,(f%7)*16));CHECK(!memcmp(cached.canvas,full.canvas,bytes));CHECK(cache.restored==(f!=0&&f!=13));
   }
   cache.capacity=0;CHECK(db_menu_draw(&cached,&c,&ca,0,0,0));CHECK(!cache.restored);free(cache.pixels);free(cached.canvas);free(full.canvas);
  }c.first=0;c.timeout=0;}
 DbPlatform p={0};uint32_t pixel=0x12345678;p.width=640;p.height=400;p.canvas=&pixel;a.icon_len[0]=1;
 CHECK(!db_menu_draw(&p,&c,&a,0,0,0));CHECK(pixel==0x12345678);CHECK(text_calls==0);
 puts("PASS: centered focus expansion/contraction, no floor shadow, alpha, aspect ratio, missing assets and framebuffer guards");return 0;
}

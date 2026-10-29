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
  CHECK(p.canvas[0]==c.background);
  for(int i=0;i<2;i++){
   DbBox card=l.cards[i],icon=l.icons[i];CHECK(card.x>=0&&card.y>=0&&card.x+card.width<=(int)p.width&&card.y+card.height<=(int)p.height);
   CHECK(p.canvas[(size_t)(icon.y+icon.height/2)*p.width+(size_t)(icon.x+icon.width/2)]==(i?0x00ff00u:0xff0000u));
   CHECK(p.canvas[(size_t)(icon.y+icon.height/8)*p.width+(size_t)(icon.x+icon.width/2)]==0x171c25u); /* wide source stays wide */
  }
  for(size_t i=0;i<8;i++){CHECK(allocation[i]==0xcafebabe);CHECK(allocation[pixels+8+i]==0xcafebabe);}
  free(allocation);
 }
 {size_t bytes=54+224u*224u*3u;uint8_t*stripes=calloc(1,bytes);CHECK(stripes);stripes[0]='B';stripes[1]='M';u32(stripes+2,(uint32_t)bytes);stripes[10]=54;stripes[14]=40;stripes[18]=224;stripes[22]=224;stripes[26]=1;stripes[28]=24;
  for(unsigned y=0;y<224;y++)for(unsigned x=0;x<224;x++)for(unsigned ch=0;ch<3;ch++)stripes[54+(y*224+x)*3+ch]=(x&1)?255:0;
  DbPlatform p={0};p.width=1920;p.height=1080;p.targets[0].available=p.targets[1].available=1;p.canvas=malloc(1920u*1080u*4u);CHECK(p.canvas);a.icons[0]=stripes;a.icon_len[0]=bytes;
  CHECK(db_menu_draw(&p,&c,&a,0,0,0));CHECK(p.canvas[540u*1920u+800u]==0x808080u); /* area averaging avoids aliased official logos */
  free(p.canvas);free(stripes);
 }
 DbPlatform p={0};uint32_t pixel=0x12345678;p.width=640;p.height=400;p.canvas=&pixel;a.icon_len[0]=1;
 CHECK(!db_menu_draw(&p,&c,&a,0,0,0));CHECK(pixel==0x12345678);CHECK(text_calls==0);
 puts("PASS: profile menu layout, real pixels, aspect ratio, missing assets and framebuffer guards");return 0;
}

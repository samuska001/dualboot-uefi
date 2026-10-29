#include "menu.h"
static int scaled(int value,uint32_t unit){return (int)(((uint32_t)value*unit+32768u)/65536u);}
static uint32_t unit_for(uint32_t w,uint32_t h){uint32_t a=w*65536u/1920u,b=h*65536u/1080u;uint32_t u=a<b?a:b;return u<26214u?26214u:u;}
int db_menu_layout(uint32_t w,uint32_t h,unsigned first,unsigned selected,DbMenuLayout*l){
 if(!l||w<320||h<240||w>3840||h>2160||first>1||selected>1)return 0;
 uint32_t u=unit_for(w,h);l->radius=scaled(17,u);l->border=scaled(3,u);if(l->border<2)l->border=2;
 for(unsigned pos=0;pos<2;pos++){
  unsigned i=pos^first;int cx=(int)w/2+(pos?1:-1)*scaled(160,u),cy=(int)h/2,size=scaled(i==selected?256:236,u),icon=scaled(i?130:112,u);
  l->cards[i]=(DbBox){cx-size/2,cy-size/2,size,size};l->icons[i]=(DbBox){cx-icon/2,cy-icon/2,icon,icon};
 }
 return 1;
}
static uint32_t blend(uint32_t dst,uint32_t src,unsigned a){
 if(a==255)return src;if(!a)return dst;unsigned b=255-a;
 unsigned r=(((dst>>16)&255)*b+((src>>16)&255)*a+127)/255,g=(((dst>>8)&255)*b+((src>>8)&255)*a+127)/255,blue=((dst&255)*b+(src&255)*a+127)/255;
 return (r<<16)|(g<<8)|blue;
}
static unsigned coverage(int x,int y,int w,int h,int r){
 if(x<0||y<0||x>=w||y>=h)return 0;
 if(!r||(x>=r&&x<w-r)||(y>=r&&y<h-r))return 255;
 int cx=x<r?r:w-r,cy=y<r?r:h-r,count=0;
 for(int sy=1;sy<4;sy+=2)for(int sx=1;sx<4;sx+=2){int dx=4*x+sx-4*cx,dy=4*y+sy-4*cy;if(dx*dx+dy*dy<=16*r*r)count++;}
 return (unsigned)(count*255+2)/4;
}
static void rounded(DbPlatform*p,DbBox box,int radius,uint32_t color,unsigned alpha){
 int x0=box.x<0?0:box.x,y0=box.y<0?0:box.y,x1=box.x+box.width,y1=box.y+box.height;
 if(x1>(int)p->width)x1=(int)p->width;if(y1>(int)p->height)y1=(int)p->height;
 if(radius>box.width/2)radius=box.width/2;if(radius>box.height/2)radius=box.height/2;
 for(int y=y0;y<y1;y++)for(int x=x0;x<x1;x++){
  unsigned a=(coverage(x-box.x,y-box.y,box.width,box.height,radius)*alpha+127)/255;
  size_t index=(size_t)y*p->width+(size_t)x;p->canvas[index]=blend(p->canvas[index],color,a);
 }
}
static void image(DbPlatform*p,const uint8_t*d,const DbBmp*b,DbBox box,int cover){
 uint32_t sx0=0,sy0=0,sw=b->width,sh=b->height;
 if(cover){
  if((uint64_t)p->width*b->height>(uint64_t)p->height*b->width){sh=(uint32_t)((uint64_t)b->width*p->height/p->width);if(!sh)sh=1;sy0=(b->height-sh)/2;}
  else {sw=(uint32_t)((uint64_t)b->height*p->width/p->height);if(!sw)sw=1;sx0=(b->width-sw)/2;}
 }else{
  int width=box.width,height=box.height;
  if((uint64_t)(unsigned)width*b->height>(uint64_t)(unsigned)height*b->width)width=(int)((uint64_t)b->width*(unsigned)height/b->height);
  else height=(int)((uint64_t)b->height*(unsigned)width/b->width);
  if(width<1)width=1;if(height<1)height=1;box.x+=(box.width-width)/2;box.y+=(box.height-height)/2;box.width=width;box.height=height;
 }
 for(int y=0;y<box.height;y++){
  int dy=box.y+y;if(dy<0||dy>=(int)p->height)continue;uint32_t sy=sy0+(uint32_t)((uint64_t)(unsigned)y*sh/(unsigned)box.height);if(!b->top_down)sy=b->height-sy-1;
  for(int x=0;x<box.width;x++){
   int dx=box.x+x;if(dx<0||dx>=(int)p->width)continue;uint32_t sx=sx0+(uint32_t)((uint64_t)(unsigned)x*sw/(unsigned)box.width);
   uint32_t color;
   if(!cover&&(sw>(unsigned)box.width||sh>(unsigned)box.height)){
    uint32_t xa=(uint32_t)((uint64_t)(unsigned)x*sw/(unsigned)box.width),xb=(uint32_t)((uint64_t)((unsigned)x+1)*sw/(unsigned)box.width);
    uint32_t ya=(uint32_t)((uint64_t)(unsigned)y*sh/(unsigned)box.height),yb=(uint32_t)((uint64_t)((unsigned)y+1)*sh/(unsigned)box.height);
    if(xb<=xa)xb=xa+1;if(yb<=ya)yb=ya+1;uint32_t red=0,green=0,blue=0,count=0;
    for(uint32_t yy=ya;yy<yb;yy++){uint32_t row=b->top_down?yy:b->height-yy-1;for(uint32_t xx=xa;xx<xb;xx++){
     const uint8_t*px=d+b->offset+(size_t)row*b->stride+(size_t)xx*(b->bpp/8);blue+=px[0];green+=px[1];red+=px[2];count++;
    }}
    color=(((red+count/2)/count)<<16)|(((green+count/2)/count)<<8)|((blue+count/2)/count);
   }else{const uint8_t*px=d+b->offset+(size_t)sy*b->stride+(size_t)sx*(b->bpp/8);color=(uint32_t)px[0]|((uint32_t)px[1]<<8)|((uint32_t)px[2]<<16);}
   p->canvas[(size_t)dy*p->width+(size_t)dx]=color;
  }
 }
}
int db_menu_draw(DbPlatform*p,const DbConfig*c,const DbMenuAssets*a,unsigned selected,uint32_t remaining,int timer){
 DbMenuLayout l;DbBmp icons[2],background;
 if(!p||!p->canvas||!c||!a||!db_menu_layout(p->width,p->height,c->first,selected,&l))return 0;
 for(int i=0;i<2;i++)if(!db_bmp_info(&icons[i],a->icons[i],a->icon_len[i]))return 0;
 for(size_t i=0;i<(size_t)p->width*p->height;i++)p->canvas[i]=c->background;
 if(db_bmp_info(&background,a->background,a->background_len))image(p,a->background,&background,(DbBox){0,0,(int)p->width,(int)p->height},1);
 uint32_t u=unit_for(p->width,p->height);DbBox chosen=l.cards[selected];
 for(int layer=12;layer>=1;layer--){int spread=scaled(layer*3,u);DbBox glow={chosen.x-spread,chosen.y-spread+scaled(12,u),chosen.width+2*spread,chosen.height+2*spread};rounded(p,glow,l.radius+spread,0x366aa4,3);}
 for(int i=0;i<2;i++){
  DbBox card=l.cards[i];int border=i==(int)selected?l.border:scaled(1,u);if(border<1)border=1;
  rounded(p,card,l.radius,i==(int)selected?c->accent:0x2a303b,255);
  DbBox inside={card.x+border,card.y+border,card.width-2*border,card.height-2*border};
  rounded(p,inside,l.radius-border,0x171c25,255);image(p,a->icons[i],&icons[i],l.icons[i],0);
  if(!p->targets[i].available)rounded(p,inside,l.radius-border,0x080b12,115);
 }
 if(timer&&c->timeout){
  int width=scaled(96,u),height=scaled(2,u);if(height<1)height=1;uint32_t rest=remaining>c->timeout?c->timeout:remaining;
  DbBox bar={(int)p->width/2-width/2,chosen.y+chosen.height+scaled(42,u),width,height};rounded(p,bar,height/2,0x2a303b,255);bar.width=(int)((uint64_t)(unsigned)width*rest/c->timeout);rounded(p,bar,height/2,c->accent,255);
 }
 return 1;
}

#include "menu.h"
static int scaled(int value,uint32_t unit){return (int)(((uint32_t)value*unit+32768u)/65536u);}
static uint32_t unit_for(uint32_t w,uint32_t h){uint32_t a=w*65536u/1920u,b=h*65536u/1080u;uint32_t u=a<b?a:b;return u<26214u?26214u:u;}
static DbBox cache_region(uint32_t w,uint32_t h){uint32_t u=unit_for(w,h);int x=(int)w/2-scaled(320,u),y=(int)h/2-scaled(220,u),right=(int)w/2+scaled(320,u),bottom=(int)h/2+scaled(220,u);if(x<0)x=0;if(y<0)y=0;if(right>(int)w)right=(int)w;if(bottom>(int)h)bottom=(int)h;return (DbBox){x,y,right-x,bottom-y};}
size_t db_menu_cache_bytes(uint32_t w,uint32_t h){if(w<320||h<240||w>3840||h>2160)return 0;DbBox b=cache_region(w,h);return (size_t)b.width*(size_t)b.height*4;}
#define DB_SCALE_ONE 65536u
#define DB_SCALE_SELECTED 81265u
static uint32_t ease(uint64_t elapsed){uint64_t q=(DB_FOCUS_MS-(elapsed>=DB_FOCUS_MS?DB_FOCUS_MS:elapsed))*65536u/DB_FOCUS_MS;return 65536u-(uint32_t)(q*q/65536u*q/65536u);}
int db_menu_motion_update(DbMenuMotion*m,unsigned selected,uint64_t now){
 if(!m||selected>1)return 0;
 if(!m->initialized){m->initialized=1;m->selected=selected;m->started=now;for(unsigned i=0;i<2;i++)m->from[i]=m->scale[i]=i==selected?DB_SCALE_SELECTED:DB_SCALE_ONE;m->active=0;return 0;}
 uint64_t elapsed=now>=m->started?now-m->started:0;uint32_t t=ease(elapsed);
 for(unsigned i=0;i<2;i++){uint32_t target=i==m->selected?DB_SCALE_SELECTED:DB_SCALE_ONE;m->scale[i]=(uint32_t)((int64_t)m->from[i]+((int64_t)target-m->from[i])*t/65536);}
 if(selected!=m->selected){for(unsigned i=0;i<2;i++)m->from[i]=m->scale[i];m->selected=selected;m->started=now;elapsed=0;}
 m->active=elapsed<DB_FOCUS_MS&&(m->scale[0]!=(selected==0?DB_SCALE_SELECTED:DB_SCALE_ONE)||m->scale[1]!=(selected==1?DB_SCALE_SELECTED:DB_SCALE_ONE));return m->active;
}
static int layout_scaled(uint32_t w,uint32_t h,unsigned first,unsigned selected,const uint32_t scale[2],DbMenuLayout*l){
 if(!l||w<320||h<240||w>3840||h>2160||first>1||selected>1)return 0;
 uint32_t u=unit_for(w,h);l->radius=scaled(17,u);l->border=scaled(3,u);if(l->border<2)l->border=2;
 for(unsigned pos=0;pos<2;pos++){
  unsigned i=pos^first;if(scale[i]<32768u||scale[i]>131072u)return 0;
  int cx=(int)w/2+(pos?1:-1)*scaled(160,u),cy=(int)h/2,size=scaled(i==selected?256:236,u);
  l->icon_size_q16[i]=(uint32_t)((uint64_t)(i?128u:192u)*scale[i]*u/65536u);
  int icon=(int)((l->icon_size_q16[i]+131071u)/131072u)*2; /* even bounds keep the exact center fixed */
  l->cards[i]=(DbBox){cx-size/2,cy-size/2,size,size};l->icons[i]=(DbBox){cx-icon/2,cy-icon/2,icon,icon};
 }
 return 1;
}
int db_menu_layout_frame(uint32_t w,uint32_t h,unsigned first,unsigned selected,uint64_t elapsed,DbMenuLayout*l){
 uint32_t t=ease(elapsed),scale[2];for(unsigned i=0;i<2;i++)scale[i]=i==selected?DB_SCALE_ONE+(DB_SCALE_SELECTED-DB_SCALE_ONE)*t/65536u:DB_SCALE_SELECTED-(DB_SCALE_SELECTED-DB_SCALE_ONE)*t/65536u;
 return layout_scaled(w,h,first,selected,scale,l);
}
int db_menu_layout(uint32_t w,uint32_t h,unsigned first,unsigned selected,DbMenuLayout*l){return db_menu_layout_frame(w,h,first,selected,DB_FOCUS_MS,l);}
static uint32_t blend(uint32_t dst,uint32_t src,unsigned a){
 if(a==255)return src;if(!a)return dst;unsigned b=255-a;
 unsigned r=(((dst>>16)&255)*b+((src>>16)&255)*a+127)/255,g=(((dst>>8)&255)*b+((src>>8)&255)*a+127)/255,blue=((dst&255)*b+(src&255)*a+127)/255;
 return (r<<16)|(g<<8)|blue;
}
static void ellipse(DbPlatform*p,DbBox box,uint32_t color,unsigned alpha){
 int x0=box.x<0?0:box.x,y0=box.y<0?0:box.y,x1=box.x+box.width,y1=box.y+box.height;
 if(x1>(int)p->width)x1=(int)p->width;if(y1>(int)p->height)y1=(int)p->height;
 for(int y=y0;y<y1;y++)for(int x=x0;x<x1;x++){
  int64_t dx=2*(x-box.x)-box.width,dy=2*(y-box.y)-box.height;
  int64_t ww=(int64_t)box.width*box.width,hh=(int64_t)box.height*box.height;
  int64_t d=dx*dx*hh+dy*dy*ww,limit=ww*hh;
  if(d>=limit)continue;
  unsigned a=(unsigned)((limit-d)*alpha/limit);
  size_t index=(size_t)y*p->width+(size_t)x;p->canvas[index]=blend(p->canvas[index],color,a);
 }
}
static void bar_rect(DbPlatform*p,DbBox b,uint32_t color){for(int y=b.y;y<b.y+b.height;y++)for(int x=b.x;x<b.x+b.width;x++)if(x>=0&&y>=0&&x<(int)p->width&&y<(int)p->height)p->canvas[(size_t)y*p->width+(size_t)x]=color;}
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
   uint32_t color;unsigned alpha=255;
   if(!cover&&(sw>(unsigned)box.width||sh>(unsigned)box.height)){
    uint32_t xa=(uint32_t)((uint64_t)(unsigned)x*sw/(unsigned)box.width),xb=(uint32_t)((uint64_t)((unsigned)x+1)*sw/(unsigned)box.width);
    uint32_t ya=(uint32_t)((uint64_t)(unsigned)y*sh/(unsigned)box.height),yb=(uint32_t)((uint64_t)((unsigned)y+1)*sh/(unsigned)box.height);
    if(xb<=xa)xb=xa+1;if(yb<=ya)yb=ya+1;uint64_t red=0,green=0,blue=0,weight=0,count=0;
    for(uint32_t yy=ya;yy<yb;yy++){uint32_t row=b->top_down?yy:b->height-yy-1;for(uint32_t xx=xa;xx<xb;xx++){
     const uint8_t*px=d+b->offset+(size_t)row*b->stride+(size_t)xx*(b->bpp/8);unsigned a=b->bpp==32?px[3]:255;blue+=px[0]*a;green+=px[1]*a;red+=px[2]*a;weight+=a;count++;
    }}
    alpha=(unsigned)((weight+count/2)/count);color=weight?(uint32_t)((((red+weight/2)/weight)<<16)|(((green+weight/2)/weight)<<8)|((blue+weight/2)/weight)):0;
   }else{const uint8_t*px=d+b->offset+(size_t)sy*b->stride+(size_t)sx*(b->bpp/8);color=(uint32_t)px[0]|((uint32_t)px[1]<<8)|((uint32_t)px[2]<<16);if(!cover&&b->bpp==32)alpha=px[3];}
   size_t index=(size_t)dy*p->width+(size_t)dx;p->canvas[index]=blend(p->canvas[index],color,alpha);
  }
 }
}
/* Fractional geometry and premultiplied bilinear sampling avoid one-pixel wobble
 * and changing integer source bins. Sampling uses a bounded filter footprint. */
static void icon_image(DbPlatform*p,const uint8_t*d,const DbBmp*b,DbBox box,uint32_t size_q16){
 uint64_t wq=size_q16,hq=size_q16;
 if(b->width>b->height)hq=wq*b->height/b->width;else wq=hq*b->width/b->height;
 if(!wq||!hq)return;
 int64_t cx=(int64_t)(box.x+box.width/2)*65536,cy=(int64_t)(box.y+box.height/2)*65536,left=cx-(int64_t)wq/2,top=cy-(int64_t)hq/2,right=left+(int64_t)wq,bottom=top+(int64_t)hq;
 int x0=(int)(left/65536),y0=(int)(top/65536),x1=(int)((right+65535)/65536),y1=(int)((bottom+65535)/65536);
 if(x0<0)x0=0;if(y0<0)y0=0;if(x1>(int)p->width)x1=(int)p->width;if(y1>(int)p->height)y1=(int)p->height;
 int64_t sx_start=(((int64_t)x0*65536+32768-left)*b->width*65536)/(int64_t)wq-32768,sy=(((int64_t)y0*65536+32768-top)*b->height*65536)/(int64_t)hq-32768;
 int64_t step_x=((uint64_t)b->width<<32)/wq,step_y=((uint64_t)b->height<<32)/hq;
 unsigned samples=((uint64_t)b->width*65536u>=4*wq||(uint64_t)b->height*65536u>=4*hq)?4u:(((uint64_t)b->width*65536u>=2*wq||(uint64_t)b->height*65536u>=2*hq)?2u:1u);
 unsigned alpha_shift=16u+(samples==4?4u:(samples==2?2u:0u));int64_t offset_x[4],offset_y[4];
 for(unsigned i=0;i<samples;i++){offset_x[i]=((int64_t)(2*i+1)-(int64_t)samples)*step_x/(2*samples);offset_y[i]=((int64_t)(2*i+1)-(int64_t)samples)*step_y/(2*samples);}
 for(int y=y0;y<y1;y++,sy+=step_y){
  int64_t sx=sx_start;
  for(int x=x0;x<x1;x++,sx+=step_x){
   uint64_t red=0,green=0,blue=0,alpha=0;
   for(unsigned gy=0;gy<samples;gy++)for(unsigned gx=0;gx<samples;gx++){
    int64_t sample_y=sy+offset_y[gy],sample_x=sx+offset_x[gx];
    if(sample_y<0)sample_y=0;if(sample_y>(int64_t)(b->height-1)*65536)sample_y=(int64_t)(b->height-1)*65536;
    if(sample_x<0)sample_x=0;if(sample_x>(int64_t)(b->width-1)*65536)sample_x=(int64_t)(b->width-1)*65536;
    uint32_t ya=(uint32_t)(sample_y/65536),yb=ya+1<b->height?ya+1:ya,fy=(uint32_t)(sample_y%65536)>>8;
    uint32_t xa=(uint32_t)(sample_x/65536),xb=xa+1<b->width?xa+1:xa,fx=(uint32_t)(sample_x%65536)>>8;
    uint32_t rows[2]={b->top_down?ya:b->height-1-ya,b->top_down?yb:b->height-1-yb},xs[2]={xa,xb};
    unsigned weights[4]={(256-fx)*(256-fy),fx*(256-fy),(256-fx)*fy,fx*fy};
    for(unsigned k=0;k<4;k++){const uint8_t*px=d+b->offset+(size_t)rows[k/2]*b->stride+(size_t)xs[k%2]*(b->bpp/8);unsigned a=b->bpp==32?px[3]:255;uint64_t weight=(uint64_t)a*weights[k];alpha+=weight;red+=px[2]*weight;green+=px[1]*weight;blue+=px[0]*weight;}
   }
   if(!alpha)continue;
   uint32_t color=(uint32_t)(((red+alpha/2)/alpha)<<16)|((uint32_t)((green+alpha/2)/alpha)<<8)|(uint32_t)((blue+alpha/2)/alpha);unsigned a=(unsigned)((alpha+((uint64_t)1u<<(alpha_shift-1)))>>alpha_shift);
   int64_t px_left=(int64_t)x*65536,px_top=(int64_t)y*65536,ox=(right<px_left+65536?right:px_left+65536)-(left>px_left?left:px_left),oy=(bottom<px_top+65536?bottom:px_top+65536)-(top>px_top?top:px_top);
   if(ox<=0||oy<=0)continue;a=(unsigned)((uint64_t)a*(uint64_t)ox*(uint64_t)oy>>32);
   size_t index=(size_t)y*p->width+(size_t)x;p->canvas[index]=blend(p->canvas[index],color,a);
  }
 }
}
int db_menu_draw_frame(DbPlatform*p,const DbConfig*c,const DbMenuAssets*a,unsigned selected,uint32_t remaining,int timer,uint64_t elapsed){
 DbMenuLayout l;DbBmp icons[2],background;
 if(!p||!p->canvas||!c||!a)return 0;
 if(a->motion&&a->motion->initialized){if(!layout_scaled(p->width,p->height,c->first,selected,a->motion->scale,&l))return 0;}else if(!db_menu_layout_frame(p->width,p->height,c->first,selected,elapsed,&l))return 0;
 for(int i=0;i<2;i++)if(!db_bmp_info(&icons[i],a->icons[i],a->icon_len[i]))return 0;
 DbMenuCache *cache=a->cache;
 int usable=cache&&cache->pixels&&cache->capacity>=db_menu_cache_bytes(p->width,p->height);
 if(cache)cache->restored=0;
 if(usable&&cache->valid&&cache->width==p->width&&cache->height==p->height&&cache->color==c->background&&cache->source==a->background&&cache->source_len==a->background_len){
  DbBox b=cache->region;for(int y=0;y<b.height;y++)for(int x=0;x<b.width;x++)p->canvas[(size_t)(b.y+y)*p->width+(size_t)(b.x+x)]=cache->pixels[(size_t)y*(unsigned)b.width+(unsigned)x];cache->restored=1;
 }else{
  for(size_t i=0;i<(size_t)p->width*p->height;i++)p->canvas[i]=c->background;
  if(db_bmp_info(&background,a->background,a->background_len))image(p,a->background,&background,(DbBox){0,0,(int)p->width,(int)p->height},1);
  if(usable){DbBox b=cache_region(p->width,p->height);cache->region=b;for(int y=0;y<b.height;y++)for(int x=0;x<b.width;x++)cache->pixels[(size_t)y*(unsigned)b.width+(unsigned)x]=p->canvas[(size_t)(b.y+y)*p->width+(size_t)(b.x+x)];cache->width=p->width;cache->height=p->height;cache->color=c->background;cache->source=a->background;cache->source_len=a->background_len;cache->valid=1;}
 }
 uint32_t u=unit_for(p->width,p->height);DbBox chosen=l.cards[selected];
 for(int i=0;i<2;i++){
  DbBox icon=l.icons[i];
  icon_image(p,a->icons[i],&icons[i],icon,l.icon_size_q16[i]);
  if(!p->targets[i].available)ellipse(p,icon,0x080b12,170);
 }
 if(timer&&c->timeout){
  int width=scaled(96,u),height=scaled(2,u);if(height<1)height=1;uint32_t rest=remaining>c->timeout?c->timeout:remaining;
  DbBox bar={(int)p->width/2-width/2,chosen.y+chosen.height+scaled(42,u),width,height};bar_rect(p,bar,0x2a303b);bar.width=(int)((uint64_t)(unsigned)width*rest/c->timeout);bar_rect(p,bar,c->accent);
 }
 return 1;
}
int db_menu_draw(DbPlatform*p,const DbConfig*c,const DbMenuAssets*a,unsigned selected,uint32_t remaining,int timer){return db_menu_draw_frame(p,c,a,selected,remaining,timer,DB_FOCUS_MS);}

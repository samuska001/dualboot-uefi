/* DualBoot original implementation; public protocol definitions are referenced in docs. */
#include "core.h"
static int config_parse(DbConfig*c,const char*data,size_t len,int theme_only);
int db_theme_parse(DbConfig*c,const char*data,size_t len){return config_parse(c,data,len,1);}
int db_config_parse(DbConfig*c,const char*data,size_t len){return config_parse(c,data,len,0);}
static char lower(char c){return c>='A'&&c<='Z'?(char)(c+32):c;}
int db_equal(const char*a,const char*b){while(*a&&*b){if(lower(*a++)!=lower(*b++))return 0;}return *a==*b;}
int db_copy(char*d,size_t cap,const char*s){size_t i=0;if(!cap)return 0;while(s[i]){if(i+1>=cap){d[0]=0;return 0;}d[i]=s[i];++i;}d[i]=0;return 1;}
uint16_t db_u16(const uint8_t*p){return (uint16_t)(p[0]|((uint16_t)p[1]<<8));}
uint32_t db_u32(const uint8_t*p){return (uint32_t)db_u16(p)|((uint32_t)db_u16(p+2)<<16);}
static void zero(void*p,size_t n){uint8_t*d=p;for(size_t i=0;i<n;i++)d[i]=0;}
static int hex(char c){if(c>='0'&&c<='9')return c-'0';c=lower(c);return c>='a'&&c<='f'?c-'a'+10:-1;}
int db_guid_parse(const char*t,uint8_t*g){
 static const uint8_t order[16]={3,2,1,0,5,4,7,6,8,9,10,11,12,13,14,15};size_t pos=0;
 for(size_t i=0;i<16;i++){
  if(pos==8||pos==13||pos==18||pos==23){if(t[pos++]!='-')return 0;}
  int a=hex(t[pos]);if(a<0)return 0;++pos;int b=hex(t[pos]);if(b<0)return 0;++pos;
  g[order[i]]=(uint8_t)((a<<4)|b);
 }return t[pos]==0;
}
int db_safe_path(const char*p){
 if(!p||p[0]!='\\')return 0;size_t n=0,segment=0;
 for(;p[n];n++){
  if(n>=DB_PATH-1||p[n]<32||p[n]>126||p[n]==':'||p[n]=='/')return 0;
  if(p[n]=='\\'){if(n&&n==segment)return 0;segment=n+1;}
  else {
   if(n==segment&&p[n]==' ')return 0;
   if((p[n]=='.'||p[n]==' ')&&(p[n+1]=='\\'||p[n+1]==0))return 0;
   if(p[n]=='.'&&p[n+1]=='.'&&n==segment&&(p[n+2]=='\\'||p[n+2]==0))return 0;
  }
 }return n>4&&db_equal(p+n-4,".efi");
}
void db_defaults(DbConfig*c){
 zero(c,sizeof(*c));db_copy(c->title,sizeof c->title,"DUALBOOT");db_copy(c->theme,sizeof c->theme,"night");
 c->timeout=15;c->background=0x0b101a;c->foreground=0xe9eef5;c->accent=0x6be0b5;
 db_copy(c->entries[0].label,DB_LABEL,"Windows");db_copy(c->entries[1].label,DB_LABEL,"SteamOS");
 db_copy(c->entries[0].path,DB_PATH,"\\EFI\\Microsoft\\Boot\\bootmgfw.efi");db_copy(c->entries[1].path,DB_PATH,"\\EFI\\steamos\\steamcl.efi");
 for(int i=0;i<2;i++){c->entries[i].enabled=1;c->entries[i].firmware=1;}
}
static char*trim(char*s){while(*s==' '||*s=='\t')s++;size_t n=0;while(s[n])n++;while(n&&(s[n-1]==' '||s[n-1]=='\t'||s[n-1]=='\r'))s[--n]=0;return s;}
static int number(const char*s,uint32_t*value,uint32_t max){uint32_t n=0;if(!*s)return 0;while(*s){if(*s<'0'||*s>'9')return 0;uint32_t d=(uint32_t)(*s++-'0');if(d>max||n>(max-d)/10)return 0;n=n*10+d;}*value=n;return 1;}
static int color(const char*s,uint32_t*v){if(*s=='#')s++;uint32_t n=0;for(int i=0;i<6;i++){int h=hex(s[i]);if(h<0)return 0;n=(n<<4)|(uint32_t)h;}if(s[6])return 0;*v=n;return 1;}
static int boolean(const char*s,int*v){if(db_equal(s,"true")||db_equal(s,"1")){*v=1;return 1;}if(db_equal(s,"false")||db_equal(s,"0")){*v=0;return 1;}return 0;}
static int config_parse(DbConfig*c,const char*data,size_t len,int theme_only){
 if(!data||len>DB_CONFIG_MAX)return 0;DbConfig out=*c;char line[384],section[20]="";size_t pos=0;
 if(len>=3&&(uint8_t)data[0]==0xef&&(uint8_t)data[1]==0xbb&&(uint8_t)data[2]==0xbf)pos=3;
 while(pos<len){
  size_t n=0;while(pos<len&&data[pos]!='\n'){unsigned char ch=(unsigned char)data[pos++];if(ch==0||ch>126||(ch<32&&ch!='\r'&&ch!='\t')||n+1>=sizeof line)return 0;line[n++]=(char)ch;}if(pos<len)pos++;line[n]=0;char*s=trim(line);if(!*s||*s==';'||*s=='#')continue;
  if(*s=='['){size_t k=0;while(s[k])k++;if(k<3||s[k-1]!=']')return 0;s[k-1]=0;if(!db_copy(section,sizeof section,s+1))return 0;if(theme_only&&!db_equal(section,"theme"))return 0;if(!db_equal(section,"menu")&&!db_equal(section,"theme")&&!db_equal(section,"windows")&&!db_equal(section,"steamos"))return 0;continue;}
  char*eq=s;while(*eq&&*eq!='=')eq++;if(!*eq)return 0;*eq++=0;char*key=trim(s),*v=trim(eq);int valid=0;
  if(db_equal(section,"menu")){
   if(db_equal(key,"title"))valid=db_copy(out.title,sizeof out.title,v)&&*v;
   else if(db_equal(key,"timeout"))valid=number(v,&out.timeout,300);
   else if(db_equal(key,"default")){valid=db_equal(v,"windows")||db_equal(v,"steamos");out.selected=db_equal(v,"steamos");}
   else if(db_equal(key,"order")){valid=db_equal(v,"windows,steamos")||db_equal(v,"steamos,windows");out.first=db_equal(v,"steamos,windows");}
   else if(db_equal(key,"theme")){valid=*v!=0;for(size_t j=0;v[j];j++)if(!((lower(v[j])>='a'&&lower(v[j])<='z')||(v[j]>='0'&&v[j]<='9')||v[j]=='_'||v[j]=='-'))valid=0;valid=valid&&db_copy(out.theme,sizeof out.theme,v);}
  }else if(db_equal(section,"theme")){
   if(db_equal(key,"background"))valid=color(v,&out.background);else if(db_equal(key,"foreground"))valid=color(v,&out.foreground);else if(db_equal(key,"accent"))valid=color(v,&out.accent);
  }else if(db_equal(section,"windows")||db_equal(section,"steamos")){
   DbEntry*e=&out.entries[db_equal(section,"steamos")?1:0];
   if(db_equal(key,"label"))valid=db_copy(e->label,sizeof e->label,v)&&*v;
   else if(db_equal(key,"partition_guid")){uint8_t g[16];valid=db_guid_parse(v,g)&&db_copy(e->guid,sizeof e->guid,v);}
   else if(db_equal(key,"path"))valid=db_safe_path(v)&&db_copy(e->path,sizeof e->path,v);
   else if(db_equal(key,"enabled"))valid=boolean(v,&e->enabled);
   else if(db_equal(key,"source")){valid=db_equal(v,"firmware")||db_equal(v,"file");e->firmware=db_equal(v,"firmware");}
  }if(!valid)return 0;
 }*c=out;return 1;
}
int db_pad_decode(DbPad*p,const uint8_t*d,size_t len){if(!d||len<20||d[0]!=0||d[1]!=20)return 0;DbPad a={0};a.buttons=db_u16(d+2);a.lt=d[4];a.rt=d[5];a.lx=(int16_t)db_u16(d+6);a.ly=(int16_t)db_u16(d+8);a.rx=(int16_t)db_u16(d+10);a.ry=(int16_t)db_u16(d+12);*p=a;return 1;}
unsigned db_nav_update(DbNav*n,const DbPad*p,uint64_t now){
 uint16_t b=p->buttons;if(p->lx < -12000)b|=4;if(p->lx>12000)b|=8;if(p->ly>12000)b|=1;if(p->ly < -12000)b|=2;
 if(!n->initialized){n->initialized=1;n->previous=b;n->repeat_at=now+450;return 0;}
 uint16_t pressed=(uint16_t)(b&~n->previous);unsigned a=pressed?DB_ACTIVITY:0;unsigned direction=(b&5)?DB_LEFT:((b&10)?DB_RIGHT:0);
 if(direction&&((pressed&15)||now>=n->repeat_at)){a|=direction|DB_ACTIVITY;n->repeat_at=now+((pressed&15)?450:120);}
 if(!(b&15))n->repeat_at=now+450;
 if(pressed&0x2000)a|=DB_BACK;else if(pressed&0x1000)a|=DB_CONFIRM;
 n->previous=b;return a;
}
int db_bmp_info(DbBmp*b,const uint8_t*d,size_t len){
 if(!d||len<54||len>DB_FILE_MAX||d[0]!='B'||d[1]!='M')return 0;
 uint32_t size=db_u32(d+2),off=db_u32(d+10),dib=db_u32(d+14),w=db_u32(d+18);int32_t sh=(int32_t)db_u32(d+22);uint32_t bits=db_u16(d+28);
 if(dib<40||dib>len-14||off<14+dib||off>len||db_u16(d+26)!=1||(bits!=24&&bits!=32)||db_u32(d+30)||!w||w>3840||!sh||sh==INT32_MIN)return 0;
 uint32_t h=(uint32_t)(sh<0?-sh:sh);if(h>2160)return 0;uint64_t stride=((uint64_t)w*bits+31)/32*4,needed=(uint64_t)off+stride*h;
 if(needed>len||size>len||size<needed)return 0;
 b->width=w;b->height=h;b->stride=(uint32_t)stride;b->offset=off;b->bpp=bits;b->top_down=sh<0;return 1;
}
int db_device_path(DbBootOption*o,const uint8_t*d,size_t len){
 if(!d||len<4||len>65535)return 0;size_t pos=0,path=0;o->has_guid=0;o->path[0]=0;
 while(pos+4<=len){size_t n=db_u16(d+pos+2);if(n<4||n>len-pos)return 0;uint8_t type=d[pos],sub=d[pos+1];
  if(type==0x7f){if(sub!=0xff||n!=4||pos+n!=len)return 0;return path>0&&db_safe_path(o->path);}
  if(type==4&&sub==1&&n==42&&d[pos+41]==2){for(int j=0;j<16;j++)o->guid[j]=d[pos+24+(size_t)j];o->has_guid=1;}
  if(type==4&&sub==4){if(n<6||n%2)return 0;int terminated=0;for(size_t k=4;k+1<n;k+=2){uint16_t ch=db_u16(d+pos+k);if(!ch){terminated=1;if(k+2!=n)return 0;break;}if(ch<32||ch>126||path+1>=DB_PATH)return 0;o->path[path++]=(char)ch;}if(!terminated)return 0;o->path[path]=0;}
  pos+=n;
 }return 0;
}
int db_boot_option_parse(DbBootOption*o,const uint8_t*d,size_t len){
 if(!d||len<10||len>65535||!(db_u32(d)&1))return 0;zero(o,sizeof(*o));size_t pos=6,name=0;int ended=0;size_t pathlen=db_u16(d+4);
 while(pos+2<=len){uint16_t ch=db_u16(d+pos);pos+=2;if(!ch){ended=1;break;}if(name+1<DB_LABEL)o->label[name++]=(ch>=32&&ch<127)?(char)ch:'?';}
 o->label[name]=0;if(!ended||pathlen>len-pos||!db_device_path(o,d+pos,pathlen))return 0;
 o->device_path=d+pos;o->path_bytes=pathlen;o->options=d+pos+pathlen;o->options_bytes=len-pos-pathlen;return 1;
}

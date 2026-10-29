#ifndef DB_MENU_H
#define DB_MENU_H
#include "platform.h"
typedef struct { uint8_t *background,*icons[2]; size_t background_len,icon_len[2]; } DbMenuAssets;
typedef struct { int x,y,width,height; } DbBox;
typedef struct { DbBox cards[2],icons[2]; int radius,border; } DbMenuLayout;
int db_menu_layout(uint32_t width,uint32_t height,unsigned first,unsigned selected,DbMenuLayout *layout);
int db_menu_draw(DbPlatform *p,const DbConfig *c,const DbMenuAssets *assets,unsigned selected,uint32_t remaining,int timer);
#endif

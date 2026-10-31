#ifndef DB_MENU_H
#define DB_MENU_H
#include "platform.h"
typedef struct DbMenuCache DbMenuCache;
typedef struct { uint32_t from[2],scale[2];uint64_t started;unsigned selected;int initialized,active; } DbMenuMotion;
typedef struct { uint8_t *background,*icons[2]; size_t background_len,icon_len[2];DbMenuCache *cache;DbMenuMotion *motion; } DbMenuAssets;
typedef struct { int x,y,width,height; } DbBox;
struct DbMenuCache { uint32_t *pixels;size_t capacity;DbBox region;uint32_t width,height,color;const uint8_t *source;size_t source_len;int valid,restored; };
typedef struct { DbBox cards[2],icons[2];uint32_t icon_size_q16[2]; int radius,border; } DbMenuLayout;
int db_menu_motion_update(DbMenuMotion *motion,unsigned selected,uint64_t now);
int db_menu_layout(uint32_t width,uint32_t height,unsigned first,unsigned selected,DbMenuLayout *layout);
#define DB_FOCUS_MS 180u
#define DB_FRAME_100NS 166667u
size_t db_menu_cache_bytes(uint32_t width,uint32_t height);
/* Milliseconds since focus changed. Selected expands; the other contracts. */
int db_menu_layout_frame(uint32_t width,uint32_t height,unsigned first,unsigned selected,uint64_t elapsed,DbMenuLayout *layout);
int db_menu_draw(DbPlatform *p,const DbConfig *c,const DbMenuAssets *assets,unsigned selected,uint32_t remaining,int timer);
int db_menu_draw_frame(DbPlatform *p,const DbConfig *c,const DbMenuAssets *assets,unsigned selected,uint32_t remaining,int timer,uint64_t elapsed);
#endif

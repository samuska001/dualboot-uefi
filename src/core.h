#ifndef DB_CORE_H
#define DB_CORE_H
#include <stdint.h>
#include <stddef.h>
#define DB_PATH 192
#define DB_LABEL 64
#define DB_CONFIG_MAX 16384
#define DB_FILE_MAX (32u*1024u*1024u)
enum { DB_LEFT=1, DB_RIGHT=2, DB_CONFIRM=4, DB_BACK=8, DB_ACTIVITY=16 };
typedef struct { char label[DB_LABEL], guid[37], path[DB_PATH]; int enabled, firmware; } DbEntry;
typedef struct { char title[DB_LABEL], theme[48]; uint32_t timeout, selected, first, background, foreground, accent; DbEntry entries[2]; } DbConfig;
typedef struct { uint16_t buttons; uint8_t lt,rt; int16_t lx,ly,rx,ry; } DbPad;
typedef struct { uint16_t previous; uint64_t repeat_at; int initialized; } DbNav;
typedef struct { uint32_t width,height,stride,offset,bpp; int top_down; } DbBmp;
typedef struct { char label[DB_LABEL], path[DB_PATH]; uint8_t guid[16]; int has_guid; const uint8_t *device_path,*options; size_t path_bytes,options_bytes; } DbBootOption;
void db_defaults(DbConfig *c);
int db_config_parse(DbConfig *c, const char *data, size_t len);
int db_theme_parse(DbConfig *c, const char *data, size_t len);
int db_pad_decode(DbPad *p,const uint8_t *data,size_t len);
unsigned db_nav_update(DbNav *nav,const DbPad *pad,uint64_t milliseconds);
int db_bmp_info(DbBmp *bmp,const uint8_t *data,size_t len);
int db_boot_option_parse(DbBootOption *option,const uint8_t *data,size_t len);
int db_device_path(DbBootOption *option,const uint8_t *data,size_t len);
int db_guid_parse(const char *text,uint8_t guid[16]);
int db_equal(const char *a,const char *b);
int db_copy(char *dst,size_t cap,const char *src);
int db_safe_path(const char *path);
uint16_t db_u16(const uint8_t *p);
uint32_t db_u32(const uint8_t *p);
#endif

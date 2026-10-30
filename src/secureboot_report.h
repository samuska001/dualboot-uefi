#ifndef DB_SECUREBOOT_REPORT_H
#define DB_SECUREBOOT_REPORT_H
#include "platform.h"
/* Fixed little-endian on-disk ABI. Patch this region BEFORE signing the PE.
 * No terminators in ASCII fields. Unused DER bytes must remain zero. */
#define DB_SB_BINDING_MAGIC "DUALBOOT_SB_BINDING_V2_20261030!"
#define DB_SB_CERT_MAX 4096
#define DB_SB_BINDING_SIZE 4228
typedef struct {
 uint8_t magic[32]; uint32_t certificate_length;
 char session_id[32],certificate_sha256[64]; uint8_t certificate[DB_SB_CERT_MAX];
} DbSecureBootBinding;
_Static_assert(sizeof(DB_SB_BINDING_MAGIC)-1==32,"binding magic ABI");
_Static_assert(sizeof(DbSecureBootBinding)==DB_SB_BINDING_SIZE,"binding size ABI");
_Static_assert(offsetof(DbSecureBootBinding,certificate)==132,"binding DER ABI");
int db_sb_binding_valid(const DbSecureBootBinding*b);
/* 1=exact X509 entry present, 0=absent, -1=malformed list. */
int db_sb_mok_contains(const uint8_t*data,size_t length,const uint8_t*cert,size_t cert_length);
size_t db_sb_format(char*out,size_t capacity,const DbSecureBootBinding*b,char secure,char setup,char mok,char shim,char disabled);
void db_secureboot_report(DbPlatform*p);
#endif

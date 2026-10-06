// Retro Sk8 - iCloud backup of the save file (Phase 22), using iCloud key-value storage (one key, under 1 MB).
// Only active when Info.plist has RetroSk8CloudSave = YES (set by the post-build step together with the
// ubiquity-kvstore entitlement); without the entitlement NSUbiquitousKeyValueStore silently stores nothing.
// Called from CloudBackup.cs via DllImport("__Internal").
#import <Foundation/Foundation.h>
#include <stdlib.h>
#include <string.h>

static BOOL RetroSk8CloudSaveOn(void)
{
    id flag = [[NSBundle mainBundle] objectForInfoDictionaryKey:@"RetroSk8CloudSave"];
    return [flag respondsToSelector:@selector(boolValue)] && [flag boolValue];
}

// Starts the first download from iCloud (call at launch; reads right after install can come back empty until it lands).
extern "C" void RetroSk8_CloudSaveSync(void)
{
    if (!RetroSk8CloudSaveOn()) return;
    [[NSUbiquitousKeyValueStore defaultStore] synchronize];
}

// 1 when backups can work in this build and the player is signed in to iCloud.
extern "C" int RetroSk8_CloudSaveAvailable(void)
{
    if (!RetroSk8CloudSaveOn()) return 0;
    return [[NSFileManager defaultManager] ubiquityIdentityToken] != nil ? 1 : 0;
}

extern "C" int RetroSk8_CloudSaveSet(const char* key, const char* value)
{
    if (!RetroSk8CloudSaveOn() || key == NULL || value == NULL) return 0;
    if (strlen(value) > 900 * 1024) return 0; // the per-key limit is 1 MB
    NSString* k = [NSString stringWithUTF8String:key];
    NSString* v = [NSString stringWithUTF8String:value];
    if (k == nil || v == nil) return 0;
    NSUbiquitousKeyValueStore* store = [NSUbiquitousKeyValueStore defaultStore];
    [store setString:v forKey:k];
    return [store synchronize] ? 1 : 0;
}

// Returns a malloc'd copy (IL2CPP frees it), or NULL when there's nothing stored.
extern "C" char* RetroSk8_CloudSaveGet(const char* key)
{
    if (!RetroSk8CloudSaveOn() || key == NULL) return NULL;
    NSString* k = [NSString stringWithUTF8String:key];
    if (k == nil) return NULL;
    NSUbiquitousKeyValueStore* store = [NSUbiquitousKeyValueStore defaultStore];
    [store synchronize];
    id value = [store objectForKey:k];
    if (![value isKindOfClass:[NSString class]]) return NULL;
    const char* utf8 = [(NSString*)value UTF8String];
    if (utf8 == NULL) return NULL;
    size_t n = strlen(utf8);
    if (n > 900 * 1024) return NULL;
    char* copy = (char*)malloc(n + 1);
    if (copy == NULL) return NULL;
    memcpy(copy, utf8, n + 1);
    return copy;
}

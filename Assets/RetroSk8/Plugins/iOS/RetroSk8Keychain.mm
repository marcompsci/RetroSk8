// Retro Sk8 - the save seal's key in the iOS Keychain (Phase 19). The key never goes into the save file, so editing
// the save can't also fix its seal. Called from SealKeys.cs via DllImport("__Internal"). Values are base64 text.
#import <Foundation/Foundation.h>
#import <Security/Security.h>
#include <string.h>

static NSString *const RetroSk8KeychainService = @"retrosk8.saveseal";

static NSMutableDictionary *RetroSk8KeychainQuery(const char *account)
{
    NSMutableDictionary *q = [NSMutableDictionary dictionary];
    q[(__bridge id)kSecClass] = (__bridge id)kSecClassGenericPassword;
    q[(__bridge id)kSecAttrService] = RetroSk8KeychainService;
    q[(__bridge id)kSecAttrAccount] = [NSString stringWithUTF8String:(account != NULL ? account : "default")];
    return q;
}

// Returns the stored base64 value, or "" when there is none. The caller frees the copy.
extern "C" char *RetroSk8_KeychainGet(const char *account)
{
    NSMutableDictionary *q = RetroSk8KeychainQuery(account);
    q[(__bridge id)kSecReturnData] = @YES;
    q[(__bridge id)kSecMatchLimit] = (__bridge id)kSecMatchLimitOne;
    CFTypeRef result = NULL;
    OSStatus status = SecItemCopyMatching((__bridge CFDictionaryRef)q, &result);
    if (status != errSecSuccess || result == NULL) return strdup("");
    NSData *data = (__bridge NSData *)result;
    NSString *text = [[NSString alloc] initWithData:data encoding:NSUTF8StringEncoding];
    char *copy = strdup(text != nil ? [text UTF8String] : "");
#if !__has_feature(objc_arc)
    [text release];
#endif
    CFRelease(result);
    return copy;
}

// Stores (or replaces) the value. Returns 1 on success. Available after the first unlock, and it moves with
// encrypted backups so a restored phone keeps its seal.
extern "C" int RetroSk8_KeychainSet(const char *account, const char *value)
{
    if (value == NULL) return 0;
    NSMutableDictionary *q = RetroSk8KeychainQuery(account);
    SecItemDelete((__bridge CFDictionaryRef)q);
    q[(__bridge id)kSecValueData] = [[NSString stringWithUTF8String:value] dataUsingEncoding:NSUTF8StringEncoding];
    q[(__bridge id)kSecAttrAccessible] = (__bridge id)kSecAttrAccessibleAfterFirstUnlock;
    return SecItemAdd((__bridge CFDictionaryRef)q, NULL) == errSecSuccess ? 1 : 0;
}

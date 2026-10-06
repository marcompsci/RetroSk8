// Retro Sk8 - online gallery (Phase 16) on CloudKit's public database. Called from GalleryService.cs via
// DllImport("__Internal"). Only active when the build turns the gallery on (RetroSk8Gallery = YES in Info.plist and
// the iCloud/CloudKit entitlement); otherwise every call reports "off" without touching CloudKit, which would crash
// an app that lacks the entitlement.
//
// Record types (create them in the CloudKit console, see README):
//   RetroSk8Share  kind (Int64), name (String), author (String), detail (Int64), location (String), code (String)
//                  Indexes: kind QUERYABLE, createdTimestamp SORTABLE, recordName QUERYABLE
//   RetroSk8Report target (String), reason (String)
//
// One request at a time: poll RetroSk8_GalleryState (0 idle, 1 busy, 2 done, 3 failed) and read the text with
// RetroSk8_GalleryResult. Lists come back as "id\tkind\tname\tauthor\tdetail\tlocation\tcreated\tcreator" lines
// (creator, Phase 19: the poster's iCloud user record name, set by CloudKit and not editable by players).
#import <Foundation/Foundation.h>
#import <CloudKit/CloudKit.h>
#include <string.h>

static int s_galleryState = 0;
static NSString *s_galleryResult = nil;

static void RetroSk8GallerySet(int state, NSString *text)
{
    @synchronized ([NSNull class])
    {
#if !__has_feature(objc_arc)
        [s_galleryResult release];
        s_galleryResult = [text retain];
#else
        s_galleryResult = text;
#endif
        s_galleryState = state;
    }
}

static BOOL RetroSk8GalleryOn(void)
{
    id flag = [[NSBundle mainBundle] objectForInfoDictionaryKey:@"RetroSk8Gallery"];
    return flag != nil && [flag boolValue];
}

static CKDatabase *RetroSk8GalleryDB(void)
{
    return [[CKContainer defaultContainer] publicCloudDatabase];
}

// Phase 19: fields come from other players, so anything that isn't text becomes "", text is capped, and the
// separators the list format uses can't be smuggled in.
static NSString *RetroSk8GalleryClean(id value)
{
    if (![value isKindOfClass:[NSString class]]) return @"";
    NSString *s = (NSString *)value;
    if (s.length > 64) s = [s substringToIndex:64];
    s = [s stringByReplacingOccurrencesOfString:@"\t" withString:@" "];
    s = [s stringByReplacingOccurrencesOfString:@"\r" withString:@" "];
    return [s stringByReplacingOccurrencesOfString:@"\n" withString:@" "];
}

static NSString *RetroSk8GalleryError(NSError *error)
{
    if (error == nil) return @"UNKNOWN ERROR";
    if (error.code == CKErrorNotAuthenticated) return @"SIGN IN TO ICLOUD IN SETTINGS TO POST";
    if (error.code == CKErrorNetworkUnavailable || error.code == CKErrorNetworkFailure) return @"NO CONNECTION";
    if (error.code == CKErrorQuotaExceeded) return @"ICLOUD STORAGE IS FULL";
    return [error.localizedDescription uppercaseString] ?: @"GALLERY ERROR";
}

extern "C" int RetroSk8_GalleryAvailable(void) { return RetroSk8GalleryOn() ? 1 : 0; }

extern "C" int RetroSk8_GalleryState(void)
{
    @synchronized ([NSNull class]) { return s_galleryState; }
}

// The caller (IL2CPP marshalling) frees the returned copy.
extern "C" char *RetroSk8_GalleryResult(void)
{
    @synchronized ([NSNull class])
    {
        const char *utf8 = s_galleryResult != nil ? [s_galleryResult UTF8String] : "";
        return strdup(utf8 != NULL ? utf8 : "");
    }
}

// Newest posts of one kind (0 parks, 1 ghosts), without their (large) codes.
extern "C" void RetroSk8_GalleryQuery(int kind, int limit)
{
    if (!RetroSk8GalleryOn()) { RetroSk8GallerySet(3, @"GALLERY IS OFF IN THIS BUILD"); return; }
    RetroSk8GallerySet(1, @"");
    NSPredicate *pred = [NSPredicate predicateWithFormat:@"kind == %d", kind];
    CKQuery *query = [[CKQuery alloc] initWithRecordType:@"RetroSk8Share" predicate:pred];
    query.sortDescriptors = @[[NSSortDescriptor sortDescriptorWithKey:@"creationDate" ascending:NO]];
    CKQueryOperation *op = [[CKQueryOperation alloc] initWithQuery:query];
    op.desiredKeys = @[@"kind", @"name", @"author", @"detail", @"location"];
    op.resultsLimit = limit > 0 ? (NSUInteger)limit : 30;
    NSMutableString *text = [NSMutableString string];
    op.recordMatchedBlock = ^(CKRecordID *recordID, CKRecord *record, NSError *error) {
        if (record == nil || error != nil) return;
        @synchronized (text)
        {
            id kv = record[@"kind"], dv = record[@"detail"];
            NSNumber *k = [kv isKindOfClass:[NSNumber class]] ? (NSNumber *)kv : nil;
            NSNumber *d = [dv isKindOfClass:[NSNumber class]] ? (NSNumber *)dv : nil;
            long long created = (long long)[record.creationDate timeIntervalSince1970];
            NSString *creator = record.creatorUserRecordID != nil ? record.creatorUserRecordID.recordName : @"";
            [text appendFormat:@"%@\t%d\t%@\t%@\t%lld\t%@\t%lld\t%@\n", recordID.recordName, k != nil ? k.intValue : 0,
                RetroSk8GalleryClean(record[@"name"]), RetroSk8GalleryClean(record[@"author"]),
                d != nil ? d.longLongValue : 0LL, RetroSk8GalleryClean(record[@"location"]), created, RetroSk8GalleryClean(creator)];
        }
    };
    // queryResultBlock is Swift-only in the iOS SDK; Objective-C uses queryCompletionBlock (found by native-check, Phase 19).
#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Wdeprecated-declarations"
    op.queryCompletionBlock = ^(CKQueryCursor *cursor, NSError *error) {
        if (error != nil) RetroSk8GallerySet(3, RetroSk8GalleryError(error));
        else RetroSk8GallerySet(2, text);
    };
#pragma clang diagnostic pop
    [RetroSk8GalleryDB() addOperation:op];
#if !__has_feature(objc_arc)
    [query release];
    [op release];
#endif
}

// Posts a park or ghost; the result is the new record's id.
extern "C" void RetroSk8_GalleryUpload(int kind, const char *name, const char *author, long long detail, const char *location, const char *code)
{
    if (!RetroSk8GalleryOn()) { RetroSk8GallerySet(3, @"GALLERY IS OFF IN THIS BUILD"); return; }
    if (code == NULL) { RetroSk8GallerySet(3, @"NOTHING TO POST"); return; }
    if (strlen(code) > 100000) { RetroSk8GallerySet(3, @"THAT'S TOO BIG TO POST"); return; } // Phase 19: CodeLimits.MaxGhostCodeChars
    RetroSk8GallerySet(1, @"");
    CKRecord *record = [[CKRecord alloc] initWithRecordType:@"RetroSk8Share"];
    record[@"kind"] = @(kind);
    record[@"name"] = name != NULL ? [NSString stringWithUTF8String:name] : @"";
    record[@"author"] = author != NULL ? [NSString stringWithUTF8String:author] : @"";
    record[@"detail"] = @(detail);
    record[@"location"] = location != NULL ? [NSString stringWithUTF8String:location] : @"";
    record[@"code"] = [NSString stringWithUTF8String:code];
    [RetroSk8GalleryDB() saveRecord:record completionHandler:^(CKRecord *saved, NSError *error) {
        if (error != nil || saved == nil) RetroSk8GallerySet(3, RetroSk8GalleryError(error));
        else RetroSk8GallerySet(2, saved.recordID.recordName);
    }];
#if !__has_feature(objc_arc)
    [record release];
#endif
}

// Downloads one post's code (the park or ghost itself).
extern "C" void RetroSk8_GalleryFetchCode(const char *recordName)
{
    if (!RetroSk8GalleryOn()) { RetroSk8GallerySet(3, @"GALLERY IS OFF IN THIS BUILD"); return; }
    if (recordName == NULL) { RetroSk8GallerySet(3, @"NOTHING TO GET"); return; }
    RetroSk8GallerySet(1, @"");
    CKRecordID *rid = [[CKRecordID alloc] initWithRecordName:[NSString stringWithUTF8String:recordName]];
    [RetroSk8GalleryDB() fetchRecordWithID:rid completionHandler:^(CKRecord *record, NSError *error) {
        id value = record != nil ? record[@"code"] : nil;
        // Phase 19: the field must really be text and of a sane size (another player wrote it).
        NSString *code = [value isKindOfClass:[NSString class]] ? (NSString *)value : nil;
        if (code != nil && code.length > 100000) code = nil;
        if (error != nil || code == nil) RetroSk8GallerySet(3, error != nil ? RetroSk8GalleryError(error) : @"THAT POST IS GONE OR BROKEN");
        else RetroSk8GallerySet(2, code);
    }];
#if !__has_feature(objc_arc)
    [rid release];
#endif
}

// Reports a post for review (moderation happens in the CloudKit console).
extern "C" void RetroSk8_GalleryReport(const char *recordName, const char *reason)
{
    if (!RetroSk8GalleryOn()) { RetroSk8GallerySet(3, @"GALLERY IS OFF IN THIS BUILD"); return; }
    RetroSk8GallerySet(1, @"");
    CKRecord *record = [[CKRecord alloc] initWithRecordType:@"RetroSk8Report"];
    record[@"target"] = recordName != NULL ? [NSString stringWithUTF8String:recordName] : @"";
    record[@"reason"] = reason != NULL ? [NSString stringWithUTF8String:reason] : @"";
    [RetroSk8GalleryDB() saveRecord:record completionHandler:^(CKRecord *saved, NSError *error) {
        if (error != nil) RetroSk8GallerySet(3, RetroSk8GalleryError(error));
        else RetroSk8GallerySet(2, @"REPORTED");
    }];
#if !__has_feature(objc_arc)
    [record release];
#endif
}

// Removes one of your own posts.
extern "C" void RetroSk8_GalleryDelete(const char *recordName)
{
    if (!RetroSk8GalleryOn()) { RetroSk8GallerySet(3, @"GALLERY IS OFF IN THIS BUILD"); return; }
    if (recordName == NULL) { RetroSk8GallerySet(3, @"NOTHING TO DELETE"); return; }
    RetroSk8GallerySet(1, @"");
    CKRecordID *rid = [[CKRecordID alloc] initWithRecordName:[NSString stringWithUTF8String:recordName]];
    [RetroSk8GalleryDB() deleteRecordWithID:rid completionHandler:^(CKRecordID *deleted, NSError *error) {
        if (error != nil) RetroSk8GallerySet(3, RetroSk8GalleryError(error));
        else RetroSk8GallerySet(2, @"DELETED");
    }];
#if !__has_feature(objc_arc)
    [rid release];
#endif
}

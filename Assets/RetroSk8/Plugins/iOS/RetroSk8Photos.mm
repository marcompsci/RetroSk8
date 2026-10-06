// Retro Sk8 - Photo mode 2.0 (Phase 25): saves a finished photo (a PNG the game wrote) to the Photos library.
// iOS asks the player once (NSPhotoLibraryAddUsageDescription, set by the post-build step); add-only access,
// so the game can never read the player's photos. Called from PhotoSaver.cs via DllImport("__Internal").
#import <UIKit/UIKit.h>

static volatile int s_retroSk8PhotoState = 0; // 0 idle, 1 saving, 2 saved, 3 failed

@interface RetroSk8PhotoSaver : NSObject
- (void)image:(UIImage*)image didFinishSavingWithError:(NSError*)error contextInfo:(void*)context;
@end

@implementation RetroSk8PhotoSaver
- (void)image:(UIImage*)image didFinishSavingWithError:(NSError*)error contextInfo:(void*)context
{
    s_retroSk8PhotoState = error == nil ? 2 : 3;
}
@end

static RetroSk8PhotoSaver* s_retroSk8Saver = nil;

extern "C" int RetroSk8_SavePhoto(const char* path)
{
    if (path == NULL) return 0;
    NSString* p = [NSString stringWithUTF8String:path];
    UIImage* image = p != nil ? [UIImage imageWithContentsOfFile:p] : nil;
    if (image == nil) return 0;
    if (s_retroSk8Saver == nil) s_retroSk8Saver = [[RetroSk8PhotoSaver alloc] init];
    s_retroSk8PhotoState = 1;
    UIImageWriteToSavedPhotosAlbum(image, s_retroSk8Saver, @selector(image:didFinishSavingWithError:contextInfo:), NULL);
    return 1;
}

extern "C" int RetroSk8_PhotoState(void) { return s_retroSk8PhotoState; }

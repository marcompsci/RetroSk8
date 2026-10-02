// Retro Sk8 - ReplayKit bridge for shareable run clips. Called from ClipRecorder.cs via DllImport("__Internal").
// The ReplayKit framework is linked by RetroSk8IOSPostBuild.cs.
#import <UIKit/UIKit.h>
#import <ReplayKit/ReplayKit.h>

// Works whether or not the Xcode project compiles with ARC.
#if __has_feature(objc_arc)
#define RSK_RETAIN(x) (x)
#define RSK_RELEASE(x)
#else
#define RSK_RETAIN(x) [(x) retain]
#define RSK_RELEASE(x) [(x) release]
#endif

static void RetroSk8SetPreview(RPPreviewViewController *preview);

// States mirror RetroSk8.Replay.ClipState.
static const int kIdle = 0, kRecording = 1, kReady = 2, kFailed = 3, kSaving = 4;
static int s_state = kIdle;
static RPPreviewViewController *s_preview = nil;

@interface RetroSk8ClipDelegate : NSObject <RPPreviewViewControllerDelegate>
@end

@implementation RetroSk8ClipDelegate
- (void)previewControllerDidFinish:(RPPreviewViewController *)previewController
{
    [previewController dismissViewControllerAnimated:YES completion:nil];
}
@end

static RetroSk8ClipDelegate *s_delegate = nil;

static void RetroSk8SetPreview(RPPreviewViewController *preview)
{
    if (preview == s_preview) return;
    RSK_RELEASE(s_preview);
    s_preview = preview != nil ? RSK_RETAIN(preview) : nil;
}

static UIViewController *RetroSk8TopController(void)
{
    UIWindow *window = nil;
    for (UIScene *scene in [UIApplication sharedApplication].connectedScenes)
    {
        if (![scene isKindOfClass:[UIWindowScene class]]) continue;
        for (UIWindow *w in ((UIWindowScene *)scene).windows)
            if (w.isKeyWindow) { window = w; break; }
        if (window == nil && ((UIWindowScene *)scene).windows.count > 0) window = ((UIWindowScene *)scene).windows.firstObject;
        if (window != nil) break;
    }
    UIViewController *vc = window.rootViewController;
    while (vc.presentedViewController != nil) vc = vc.presentedViewController;
    return vc;
}

extern "C" int RetroSk8_ClipAvailable(void)
{
    return [RPScreenRecorder sharedRecorder].isAvailable ? 1 : 0;
}

extern "C" int RetroSk8_ClipState(void)
{
    return s_state;
}

extern "C" void RetroSk8_ClipStart(void)
{
    RPScreenRecorder *recorder = [RPScreenRecorder sharedRecorder];
    if (!recorder.isAvailable || recorder.isRecording) return;
    RetroSk8SetPreview(nil);
    s_state = kRecording;
    recorder.microphoneEnabled = NO;
    [recorder startRecordingWithHandler:^(NSError *error) {
        if (error != nil) dispatch_async(dispatch_get_main_queue(), ^{ s_state = kFailed; });
    }];
}

extern "C" void RetroSk8_ClipStop(void)
{
    RPScreenRecorder *recorder = [RPScreenRecorder sharedRecorder];
    if (!recorder.isRecording) { if (s_state == kRecording) s_state = kFailed; return; }
    s_state = kSaving;
    [recorder stopRecordingWithHandler:^(RPPreviewViewController *preview, NSError *error) {
        dispatch_async(dispatch_get_main_queue(), ^{
            if (preview != nil && error == nil) { RetroSk8SetPreview(preview); s_state = kReady; }
            else { RetroSk8SetPreview(nil); s_state = kFailed; }
        });
    }];
}

extern "C" void RetroSk8_ClipCancel(void)
{
    RPScreenRecorder *recorder = [RPScreenRecorder sharedRecorder];
    RetroSk8SetPreview(nil);
    s_state = kIdle;
    if (!recorder.isRecording) return;
    [recorder stopRecordingWithHandler:^(RPPreviewViewController *preview, NSError *error) {
        [[RPScreenRecorder sharedRecorder] discardRecordingWithHandler:^{}];
    }];
}

extern "C" void RetroSk8_ClipShare(void)
{
    if (s_preview == nil) return;
    dispatch_async(dispatch_get_main_queue(), ^{
        UIViewController *top = RetroSk8TopController();
        if (top == nil || s_preview == nil) return;
        if (s_delegate == nil) s_delegate = [[RetroSk8ClipDelegate alloc] init];
        s_preview.previewControllerDelegate = s_delegate;
        s_preview.modalPresentationStyle = UIModalPresentationFullScreen;
        [top presentViewController:s_preview animated:YES completion:nil];
    });
}

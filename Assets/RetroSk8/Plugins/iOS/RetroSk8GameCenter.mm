// Retro Sk8 - Game Center bridge (sign-in, leaderboards, achievements). Called from GameCenter.cs via DllImport("__Internal").
// Does nothing unless the build was made with Game Center enabled: RetroSk8IOSPostBuild then sets
// RetroSk8GameCenter = YES in Info.plist and adds the entitlement. GameKit is weak-linked.
#import <UIKit/UIKit.h>
#import <GameKit/GameKit.h>

#if __has_feature(objc_arc)
#define RSKGC_AUTORELEASE(x) (x)
#else
#define RSKGC_AUTORELEASE(x) [(x) autorelease]
#endif

static BOOL s_gcAuthStarted = NO;

static UIViewController *RetroSk8GCTopController(void)
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

@interface RetroSk8GCDelegate : NSObject <GKGameCenterControllerDelegate>
@end

@implementation RetroSk8GCDelegate
- (void)gameCenterViewControllerDidFinish:(GKGameCenterViewController *)gameCenterViewController
{
    [gameCenterViewController dismissViewControllerAnimated:YES completion:nil];
}
@end

static RetroSk8GCDelegate *s_gcDelegate = nil;

extern "C" int RetroSk8_GCAvailable(void)
{
    id flag = [[NSBundle mainBundle] objectForInfoDictionaryKey:@"RetroSk8GameCenter"];
    if (![flag respondsToSelector:@selector(boolValue)] || ![flag boolValue]) return 0;
    return NSClassFromString(@"GKLocalPlayer") != nil ? 1 : 0;
}

extern "C" int RetroSk8_GCIsAuthenticated(void)
{
    if (!RetroSk8_GCAvailable()) return 0;
    return [GKLocalPlayer localPlayer].isAuthenticated ? 1 : 0;
}

extern "C" void RetroSk8_GCAuthenticate(void)
{
    if (!RetroSk8_GCAvailable() || s_gcAuthStarted) return;
    s_gcAuthStarted = YES;
    dispatch_async(dispatch_get_main_queue(), ^{
        [GKLocalPlayer localPlayer].authenticateHandler = ^(UIViewController *viewController, NSError *error) {
            if (viewController != nil)
            {
                UIViewController *top = RetroSk8GCTopController();
                if (top != nil) [top presentViewController:viewController animated:YES completion:nil];
            }
            // Errors (declined, offline, no entitlement) just leave Game Center signed out; local records still work.
        };
    });
}

extern "C" void RetroSk8_GCSubmitScore(const char *leaderboardId, long long score)
{
    if (!RetroSk8_GCIsAuthenticated() || leaderboardId == NULL) return;
    NSString *board = [NSString stringWithUTF8String:leaderboardId];
    [GKLeaderboard submitScore:(NSInteger)score
                       context:0
                        player:[GKLocalPlayer localPlayer]
                leaderboardIDs:@[board]
             completionHandler:^(NSError *error) {}];
}

extern "C" void RetroSk8_GCReportAchievement(const char *achievementId, double percent)
{
    if (!RetroSk8_GCIsAuthenticated() || achievementId == NULL) return;
    GKAchievement *a = RSKGC_AUTORELEASE([[GKAchievement alloc] initWithIdentifier:[NSString stringWithUTF8String:achievementId]]);
    a.percentComplete = percent;
    a.showsCompletionBanner = YES;
    [GKAchievement reportAchievements:@[a] withCompletionHandler:^(NSError *error) {}];
}

extern "C" void RetroSk8_GCShowDashboard(void)
{
    if (!RetroSk8_GCIsAuthenticated()) return;
    dispatch_async(dispatch_get_main_queue(), ^{
        UIViewController *top = RetroSk8GCTopController();
        if (top == nil) return;
        if (s_gcDelegate == nil) s_gcDelegate = [[RetroSk8GCDelegate alloc] init];
        GKGameCenterViewController *vc = RSKGC_AUTORELEASE([[GKGameCenterViewController alloc] initWithState:GKGameCenterViewControllerStateLeaderboards]);
        vc.gameCenterDelegate = s_gcDelegate;
        [top presentViewController:vc animated:YES completion:nil];
    });
}

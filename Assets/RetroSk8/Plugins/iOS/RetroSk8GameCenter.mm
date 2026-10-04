// Retro Sk8 - Game Center bridge (sign-in, leaderboards, achievements). Called from GameCenter.cs via DllImport("__Internal").
// Does nothing unless the build was made with Game Center enabled: RetroSk8IOSPostBuild then sets
// RetroSk8GameCenter = YES in Info.plist and adds the entitlement. GameKit is weak-linked.
#import <UIKit/UIKit.h>
#import <GameKit/GameKit.h>
#include <string.h>
#include <stdlib.h>

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

// ---------------------------------------------------------------- friends' scores (Phase 12)
// Loads the friends-only scope of a leaderboard. Polled from C# (FriendsBoard): state 0 idle, 1 loading,
// 2 ready, 3 failed. Results come back as "rank\tname\tscore\tisLocal" lines.

static int s_friendsState = 0;
static NSString *s_friendsResult = nil;

static void RetroSk8GCSetFriends(int state, NSString *text)
{
    @synchronized ([GKLocalPlayer class])
    {
#if !__has_feature(objc_arc)
        [s_friendsResult release];
        s_friendsResult = [text retain];
#else
        s_friendsResult = text;
#endif
        s_friendsState = state;
    }
}

extern "C" void RetroSk8_GCLoadFriendScores(const char *leaderboardId)
{
    if (!RetroSk8_GCIsAuthenticated() || leaderboardId == NULL) { RetroSk8GCSetFriends(3, @""); return; }
    NSString *board = [NSString stringWithUTF8String:leaderboardId];
    RetroSk8GCSetFriends(1, @"");
    [GKLeaderboard loadLeaderboardsWithIDs:@[board] completionHandler:^(NSArray<GKLeaderboard *> *boards, NSError *error) {
        GKLeaderboard *lb = boards.firstObject;
        if (error != nil || lb == nil) { RetroSk8GCSetFriends(3, @""); return; }
        [lb loadEntriesForPlayerScope:GKLeaderboardPlayerScopeFriendsOnly
                            timeScope:GKLeaderboardTimeScopeAllTime
                                range:NSMakeRange(1, 25)
                    completionHandler:^(GKLeaderboardEntry *local, NSArray<GKLeaderboardEntry *> *entries, NSInteger total, NSError *err) {
            if (err != nil) { RetroSk8GCSetFriends(3, @""); return; }
            NSMutableString *text = [NSMutableString string];
            NSString *me = [GKLocalPlayer localPlayer].gamePlayerID;
            BOOL sawMe = NO;
            for (GKLeaderboardEntry *e in entries)
            {
                BOOL isMe = [e.player.gamePlayerID isEqualToString:me];
                sawMe = sawMe || isMe;
                NSString *name = [[e.player.displayName stringByReplacingOccurrencesOfString:@"\t" withString:@" "] stringByReplacingOccurrencesOfString:@"\n" withString:@" "];
                [text appendFormat:@"%ld\t%@\t%lld\t%d\n", (long)e.rank, name, (long long)e.score, isMe ? 1 : 0];
            }
            if (!sawMe && local != nil)
                [text appendFormat:@"%ld\t%@\t%lld\t1\n", (long)local.rank, local.player.displayName, (long long)local.score];
            RetroSk8GCSetFriends(2, text);
        }];
    }];
}

extern "C" int RetroSk8_GCFriendScoresState(void)
{
    @synchronized ([GKLocalPlayer class]) { return s_friendsState; }
}

// The caller (IL2CPP marshalling) frees the returned copy.
extern "C" char *RetroSk8_GCFriendScores(void)
{
    @synchronized ([GKLocalPlayer class])
    {
        const char *utf8 = s_friendsResult != nil ? [s_friendsResult UTF8String] : "";
        return strdup(utf8 != NULL ? utf8 : "");
    }
}

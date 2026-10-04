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

static void RetroSk8GCAppendEntry(NSMutableString *text, GKLeaderboardEntry *e, NSString *me)
{
    BOOL isMe = me != nil && [e.player.gamePlayerID isEqualToString:me];
    NSString *name = e.player.displayName != nil ? e.player.displayName : @"?";
    name = [[name stringByReplacingOccurrencesOfString:@"\t" withString:@" "] stringByReplacingOccurrencesOfString:@"\n" withString:@" "];
    [text appendFormat:@"%ld\t%@\t%lld\t%d\n", (long)e.rank, name, (long long)e.score, isMe ? 1 : 0];
}

// Loads a leaderboard page into the shared result (poll RetroSk8_GCFriendScoresState / RetroSk8_GCFriendScores).
// Rows are "rank\tname\tscore\tisLocal"; a "#total\tN" line gives the board's player count. Global scope loads
// the top <pageSize>, then (when you're ranked below that) a few rows around you so the hub can show your neighbours.
extern "C" void RetroSk8_GCLoadScores(const char *leaderboardId, int friendsOnly, int pageSize)
{
    if (!RetroSk8_GCIsAuthenticated() || leaderboardId == NULL) { RetroSk8GCSetFriends(3, @""); return; }
    NSString *board = [NSString stringWithUTF8String:leaderboardId];
    NSInteger size = pageSize > 0 ? (NSInteger)pageSize : 25;
    GKLeaderboardPlayerScope scope = friendsOnly != 0 ? GKLeaderboardPlayerScopeFriendsOnly : GKLeaderboardPlayerScopeGlobal;
    RetroSk8GCSetFriends(1, @"");
    [GKLeaderboard loadLeaderboardsWithIDs:@[board] completionHandler:^(NSArray<GKLeaderboard *> *boards, NSError *error) {
        GKLeaderboard *lb = boards.firstObject;
        if (error != nil || lb == nil) { RetroSk8GCSetFriends(3, @""); return; }
        [lb loadEntriesForPlayerScope:scope
                            timeScope:GKLeaderboardTimeScopeAllTime
                                range:NSMakeRange(1, size)
                    completionHandler:^(GKLeaderboardEntry *local, NSArray<GKLeaderboardEntry *> *entries, NSInteger total, NSError *err) {
            if (err != nil) { RetroSk8GCSetFriends(3, @""); return; }
            NSMutableString *text = [NSMutableString string];
            [text appendFormat:@"#total\t%ld\n", (long)total];
            NSString *me = [GKLocalPlayer localPlayer].gamePlayerID;
            for (GKLeaderboardEntry *e in entries) RetroSk8GCAppendEntry(text, e, me);
            if (local == nil || local.rank <= size) { RetroSk8GCSetFriends(2, text); return; }
            // You're below the page: fetch the two players above you and the one below, plus you.
            NSInteger from = MAX(size + 1, local.rank - 2);
            [lb loadEntriesForPlayerScope:scope
                                timeScope:GKLeaderboardTimeScopeAllTime
                                    range:NSMakeRange(from, local.rank + 2 - from)
                        completionHandler:^(GKLeaderboardEntry *local2, NSArray<GKLeaderboardEntry *> *near, NSInteger total2, NSError *err2) {
                if (err2 == nil)
                    for (GKLeaderboardEntry *e in near) RetroSk8GCAppendEntry(text, e, me);
                RetroSk8GCAppendEntry(text, local, me); // duplicates are dropped by the C# parser
                RetroSk8GCSetFriends(2, text);
            }];
        }];
    }];
}

extern "C" void RetroSk8_GCLoadFriendScores(const char *leaderboardId)
{
    RetroSk8_GCLoadScores(leaderboardId, 1, 25);
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

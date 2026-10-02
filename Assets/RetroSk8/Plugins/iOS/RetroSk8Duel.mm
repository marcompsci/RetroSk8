// Retro Sk8 - live S.K.A.T.E. over Game Center real-time matches (GKMatch). Called from GameCenterDuelTransport.cs.
// Apple runs the matchmaking and relays the messages, so there's no game server. Needs a build with Game Center
// enabled (Info.plist flag RetroSk8GameCenter = YES) and a signed-in player; otherwise every call is a no-op.
// Messages are queued here and polled from C# once per frame (no UnitySendMessage threading surprises).
#import <UIKit/UIKit.h>
#import <GameKit/GameKit.h>
#include <string.h>

extern "C" int RetroSk8_GCAvailable(void);
extern "C" int RetroSk8_GCIsAuthenticated(void);

#if __has_feature(objc_arc)
#define RSKD_RELEASE(x)
#define RSKD_RETAIN(x) (x)
#else
#define RSKD_RELEASE(x) [(x) release]
#define RSKD_RETAIN(x) [(x) retain]
#endif

enum { DuelIdle = 0, DuelSearching = 1, DuelConnected = 2, DuelFailed = 3, DuelDisconnected = 4 };

static int s_duelState = DuelIdle;
static GKMatch *s_match = nil;
static NSMutableArray *s_inbox = nil;
static NSString *s_opponentName = nil;

static UIViewController *RetroSk8DuelTopController(void)
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

@interface RetroSk8DuelDelegate : NSObject <GKMatchmakerViewControllerDelegate, GKMatchDelegate>
@end

@implementation RetroSk8DuelDelegate

- (void)matchmakerViewControllerWasCancelled:(GKMatchmakerViewController *)viewController
{
    [viewController dismissViewControllerAnimated:YES completion:nil];
    s_duelState = DuelFailed;
}

- (void)matchmakerViewController:(GKMatchmakerViewController *)viewController didFailWithError:(NSError *)error
{
    [viewController dismissViewControllerAnimated:YES completion:nil];
    s_duelState = DuelFailed;
}

- (void)matchmakerViewController:(GKMatchmakerViewController *)viewController didFindMatch:(GKMatch *)match
{
    [viewController dismissViewControllerAnimated:YES completion:nil];
    if (s_match != nil) { [s_match disconnect]; RSKD_RELEASE(s_match); }
    s_match = RSKD_RETAIN(match);
    s_match.delegate = self;
    if (s_match.players.count > 0)
    {
        RSKD_RELEASE(s_opponentName);
        s_opponentName = RSKD_RETAIN([s_match.players.firstObject displayName]);
    }
    if (match.expectedPlayerCount == 0) s_duelState = DuelConnected;
}

- (void)match:(GKMatch *)match didReceiveData:(NSData *)data fromRemotePlayer:(GKPlayer *)player
{
    @synchronized (s_inbox) { [s_inbox addObject:data]; }
}

- (void)match:(GKMatch *)match player:(GKPlayer *)player didChangeConnectionState:(GKPlayerConnectionState)state
{
    if (state == GKPlayerStateConnected)
    {
        RSKD_RELEASE(s_opponentName);
        s_opponentName = RSKD_RETAIN(player.displayName);
        if (match.expectedPlayerCount == 0) s_duelState = DuelConnected;
    }
    else if (state == GKPlayerStateDisconnected) s_duelState = DuelDisconnected;
}

- (void)match:(GKMatch *)match didFailWithError:(NSError *)error
{
    s_duelState = DuelDisconnected;
}

@end

static RetroSk8DuelDelegate *s_duelDelegate = nil;

extern "C" void RetroSk8_DuelFindMatch(void)
{
    if (!RetroSk8_GCIsAuthenticated()) { s_duelState = DuelFailed; return; }
    if (s_inbox == nil) s_inbox = [[NSMutableArray alloc] init];
    @synchronized (s_inbox) { [s_inbox removeAllObjects]; }
    s_duelState = DuelSearching;
    dispatch_async(dispatch_get_main_queue(), ^{
        if (s_duelDelegate == nil) s_duelDelegate = [[RetroSk8DuelDelegate alloc] init];
        GKMatchRequest *request = [[GKMatchRequest alloc] init];
        request.minPlayers = 2;
        request.maxPlayers = 2;
        GKMatchmakerViewController *vc = [[GKMatchmakerViewController alloc] initWithMatchRequest:request];
        vc.matchmakerDelegate = s_duelDelegate;
        UIViewController *top = RetroSk8DuelTopController();
        if (top == nil || vc == nil) s_duelState = DuelFailed;
        else [top presentViewController:vc animated:YES completion:nil];
        RSKD_RELEASE(vc);
        RSKD_RELEASE(request);
    });
}

extern "C" int RetroSk8_DuelState(void)
{
    return s_duelState;
}

extern "C" int RetroSk8_DuelOpponentName(char *buffer, int capacity)
{
    if (buffer == NULL || capacity <= 0) return 0;
    const char *utf8 = s_opponentName != nil ? [s_opponentName UTF8String] : "";
    int n = (int)strlen(utf8);
    if (n >= capacity) n = capacity - 1;
    memcpy(buffer, utf8, n);
    buffer[n] = 0;
    return n;
}

extern "C" void RetroSk8_DuelSend(const unsigned char *data, int length, int reliable)
{
    if (s_match == nil || data == NULL || length <= 0 || s_duelState != DuelConnected) return;
    NSData *payload = [NSData dataWithBytes:data length:(NSUInteger)length];
    [s_match sendDataToAllPlayers:payload
                     withDataMode:(reliable ? GKMatchSendDataReliable : GKMatchSendDataUnreliable)
                            error:NULL];
}

/// Copies the oldest queued message into buffer and returns its length (0 when the queue is empty, -1 if too big).
extern "C" int RetroSk8_DuelReceive(unsigned char *buffer, int capacity)
{
    if (s_inbox == nil || buffer == NULL) return 0;
    NSData *next = nil;
    @synchronized (s_inbox)
    {
        if (s_inbox.count == 0) return 0;
        next = RSKD_RETAIN([s_inbox objectAtIndex:0]);
        [s_inbox removeObjectAtIndex:0];
    }
    int n = (int)next.length;
    if (n > capacity) { RSKD_RELEASE(next); return -1; }
    memcpy(buffer, next.bytes, (size_t)n);
    RSKD_RELEASE(next);
    return n;
}

extern "C" void RetroSk8_DuelLeave(void)
{
    if (s_match != nil)
    {
        [s_match disconnect];
        s_match.delegate = nil;
        RSKD_RELEASE(s_match);
        s_match = nil;
    }
    if (s_inbox != nil) { @synchronized (s_inbox) { [s_inbox removeAllObjects]; } }
    s_duelState = DuelIdle;
}

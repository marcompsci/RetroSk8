// Retro Sk8 - opt-in local reminders (Phase 15). Called from NotificationService.cs via DllImport("__Internal").
// Only local notifications: nothing is sent to a server, and nothing is scheduled until the player turns
// REMINDERS on in Settings and allows them in the iOS prompt.
#import <Foundation/Foundation.h>
#import <UserNotifications/UserNotifications.h>

// 0 = not asked yet, 1 = asking, 2 = allowed, 3 = denied
static int s_notifyAuth = 0;

static void RetroSk8NotifySetAuth(int state)
{
    @synchronized ([NSNull class]) { s_notifyAuth = state; }
}

extern "C" void RetroSk8_NotifyRefreshAuth(void)
{
    [[UNUserNotificationCenter currentNotificationCenter] getNotificationSettingsWithCompletionHandler:^(UNNotificationSettings *settings) {
        UNAuthorizationStatus st = settings.authorizationStatus;
        if (st == UNAuthorizationStatusNotDetermined) RetroSk8NotifySetAuth(0);
        else if (st == UNAuthorizationStatusDenied) RetroSk8NotifySetAuth(3);
        else RetroSk8NotifySetAuth(2); // authorized, provisional or ephemeral
    }];
}

extern "C" void RetroSk8_NotifyRequest(void)
{
    RetroSk8NotifySetAuth(1);
    UNAuthorizationOptions opts = UNAuthorizationOptionAlert | UNAuthorizationOptionSound | UNAuthorizationOptionBadge;
    [[UNUserNotificationCenter currentNotificationCenter] requestAuthorizationWithOptions:opts completionHandler:^(BOOL granted, NSError *error) {
        RetroSk8NotifySetAuth(granted ? 2 : 3);
    }];
}

extern "C" int RetroSk8_NotifyAuthState(void)
{
    @synchronized ([NSNull class]) { return s_notifyAuth; }
}

extern "C" void RetroSk8_NotifyCancelAll(void)
{
    UNUserNotificationCenter *center = [UNUserNotificationCenter currentNotificationCenter];
    [center removeAllPendingNotificationRequests];
    [center removeAllDeliveredNotifications];
}

extern "C" void RetroSk8_NotifySchedule(const char *identifier, const char *title, const char *body, double secondsFromNow)
{
    if (identifier == NULL || title == NULL || body == NULL || secondsFromNow < 1.0) return;
    UNMutableNotificationContent *content = [[UNMutableNotificationContent alloc] init];
    content.title = [NSString stringWithUTF8String:title];
    content.body = [NSString stringWithUTF8String:body];
    content.sound = [UNNotificationSound defaultSound];
    UNTimeIntervalNotificationTrigger *trigger = [UNTimeIntervalNotificationTrigger triggerWithTimeInterval:secondsFromNow repeats:NO];
    UNNotificationRequest *request = [UNNotificationRequest requestWithIdentifier:[NSString stringWithUTF8String:identifier] content:content trigger:trigger];
    [[UNUserNotificationCenter currentNotificationCenter] addNotificationRequest:request withCompletionHandler:nil];
#if !__has_feature(objc_arc)
    [content release];
#endif
}

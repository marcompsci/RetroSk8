// Retro Sk8 - minimal Taptic Engine bridge. Called from HapticsManager.cs via DllImport("__Internal").
#import <UIKit/UIKit.h>

extern "C" void RetroSk8_Haptic(int kind)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        switch (kind)
        {
            case 0: { UISelectionFeedbackGenerator *g = [[UISelectionFeedbackGenerator alloc] init]; [g selectionChanged]; break; }
            case 1: { UIImpactFeedbackGenerator *g = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleLight]; [g impactOccurred]; break; }
            case 2: { UIImpactFeedbackGenerator *g = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleMedium]; [g impactOccurred]; break; }
            case 3: { UIImpactFeedbackGenerator *g = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleHeavy]; [g impactOccurred]; break; }
            case 4: { UINotificationFeedbackGenerator *g = [[UINotificationFeedbackGenerator alloc] init]; [g notificationOccurred:UINotificationFeedbackTypeSuccess]; break; }
            default: break;
        }
    });
}

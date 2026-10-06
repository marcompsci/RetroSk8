// Retro Sk8 - the phone's own accessibility settings (Phase 20). Read once on first launch so the game starts with
// Reduce Motion and Larger Text matching iOS; the player can still change them in Settings > ACCESSIBILITY.
// Called from AccessibilityDefaults.cs via DllImport("__Internal").
#import <UIKit/UIKit.h>

extern "C" int RetroSk8_A11yReduceMotion(void)
{
    return UIAccessibilityIsReduceMotionEnabled() ? 1 : 0;
}

// 1 when the iOS text size is one of the larger settings (XL and up, including the accessibility sizes).
extern "C" int RetroSk8_A11yLargeText(void)
{
    UIContentSizeCategory size = [UIApplication sharedApplication].preferredContentSizeCategory;
    return UIContentSizeCategoryCompareToCategory(size, UIContentSizeCategoryExtraLarge) != NSOrderedAscending ? 1 : 0;
}

extern "C" int RetroSk8_A11yBoldText(void)
{
    return UIAccessibilityIsBoldTextEnabled() ? 1 : 0;
}

extern "C" int RetroSk8_A11yVoiceOver(void)
{
    return UIAccessibilityIsVoiceOverRunning() ? 1 : 0;
}

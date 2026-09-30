using System.Runtime.InteropServices;
using RetroSk8.Save;
using UnityEngine;

namespace RetroSk8.Feedback
{
    public enum HapticKind
    {
        Selection = 0,
        Light = 1,
        Medium = 2,
        Heavy = 3,
        Success = 4,
    }

    /// <summary>
    /// HapticsManager. iOS uses the native Taptic Engine bridge in Plugins/iOS/RetroSk8Haptics.mm;
    /// Android falls back to a short vibration for strong events only; editor/desktop is a no-op.
    /// Respects the haptics toggle in settings.
    /// </summary>
    public static class HapticsManager
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void RetroSk8_Haptic(int kind);
#endif

        public static bool Enabled => SaveManager.Data.settings.hapticsEnabled;

        public static void Play(HapticKind kind)
        {
            if (!Enabled) return;
#if UNITY_IOS && !UNITY_EDITOR
            RetroSk8_Haptic((int)kind);
#elif UNITY_ANDROID && !UNITY_EDITOR
            if (kind == HapticKind.Heavy) Handheld.Vibrate();
#endif
        }
    }
}

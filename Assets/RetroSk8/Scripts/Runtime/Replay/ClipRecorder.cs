using System.Runtime.InteropServices;
using RetroSk8.Save;
using UnityEngine;

namespace RetroSk8.Replay
{
    public enum ClipState
    {
        Idle = 0,
        Recording = 1,
        /// <summary>Recording stopped; iOS is still finalizing the video.</summary>
        Saving = 4,
        Ready = 2,
        Failed = 3,
    }

    /// <summary>
    /// Records each run with Apple's ReplayKit so the player can trim and share it from the Results screen
    /// (the system preview sheet handles editing, saving to Photos and sharing).
    /// iOS devices only, opt-in from Settings because iOS asks permission to record the screen.
    /// Everywhere else every call is a no-op and <see cref="IsSupported"/> is false.
    /// </summary>
    public static class ClipRecorder
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern int RetroSk8_ClipAvailable();
        [DllImport("__Internal")] private static extern void RetroSk8_ClipStart();
        [DllImport("__Internal")] private static extern void RetroSk8_ClipStop();
        [DllImport("__Internal")] private static extern void RetroSk8_ClipCancel();
        [DllImport("__Internal")] private static extern int RetroSk8_ClipState();
        [DllImport("__Internal")] private static extern void RetroSk8_ClipShare();

        public static bool IsSupported => RetroSk8_ClipAvailable() != 0;
        public static ClipState State => (ClipState)RetroSk8_ClipState();
        private static void NativeStart() => RetroSk8_ClipStart();
        private static void NativeStop() => RetroSk8_ClipStop();
        private static void NativeCancel() => RetroSk8_ClipCancel();
        private static void NativeShare() => RetroSk8_ClipShare();
#else
        public static bool IsSupported => false;
        public static ClipState State => ClipState.Idle;
        private static void NativeStart() { }
        private static void NativeStop() { }
        private static void NativeCancel() { }
        private static void NativeShare() { }
#endif

        public static bool Enabled => IsSupported && SaveManager.Data.settings.recordClips;

        /// <summary>Start recording a new run (replaces any previous clip).</summary>
        public static void BeginRun()
        {
            if (Enabled) NativeStart();
        }

        /// <summary>Records a replay-editor export (asked for explicitly, so it ignores the per-run setting).</summary>
        public static void StartExport()
        {
            if (IsSupported) NativeStart();
        }

        /// <summary>Stop at the end of a run; the clip becomes shareable once iOS finishes writing it.</summary>
        public static void EndRun()
        {
            if (State == ClipState.Recording) NativeStop();
        }

        /// <summary>Leaving a run early (quit to menu, restart): throw the recording away.</summary>
        public static void CancelRun()
        {
            if (State == ClipState.Recording) NativeCancel();
        }

        /// <summary>Opens the system preview sheet (trim, save, share).</summary>
        public static void Share()
        {
            if (State == ClipState.Ready) NativeShare();
        }
    }

    /// <summary>Ties ReplayKit recording to one run in the skate scene.</summary>
    public sealed class ClipRunHook : MonoBehaviour
    {
        private Game.RunController _run;
        private bool _ended;

        public void Init(Game.RunController run)
        {
            _run = run;
            run.Finished += OnFinished;
            ClipRecorder.BeginRun();
        }

        private void OnFinished(Game.RunResult _)
        {
            _ended = true;
            ClipRecorder.EndRun();
        }

        private void OnDestroy()
        {
            if (_run != null) _run.Finished -= OnFinished;
            if (!_ended) ClipRecorder.CancelRun();
        }
    }
}

using RetroSk8.Core;
using UnityEngine;

namespace RetroSk8.Input
{
    /// <summary>
    /// Programmatic input used by PlayMode smoke tests (and usable later for demo/attract mode).
    /// Hold-style values persist until changed; one-shot presses fire on the next frame only.
    /// </summary>
    public sealed class ScriptedInputSource : IInputSource
    {
        private SwipeDirection _pendingSwipe;
        private bool _pendingAction;

        public bool IsActive { get; set; } = true;
        public Vector2 Steer { get; set; }
        public bool JumpHeld { get; set; }

        public void Swipe(SwipeDirection dir) => _pendingSwipe = dir;
        public void PressAction() => _pendingAction = true;

        public void Contribute(ref InputFrame frame)
        {
            Vector2 steer = Vector2.ClampMagnitude(Steer, 1f);
            if (steer.sqrMagnitude > frame.Steer.sqrMagnitude) frame.Steer = steer;
            frame.JumpHeld |= JumpHeld;
            frame.ActionPressed |= _pendingAction;
            if (frame.Swipe == SwipeDirection.None) frame.Swipe = _pendingSwipe;
            _pendingAction = false;
            _pendingSwipe = SwipeDirection.None;
        }
    }
}

using RetroSk8.Core;
using UnityEngine;

namespace RetroSk8.Input
{
    /// <summary>
    /// Written to by the on-screen controls (TouchControlsView). Edge events are latched until the router
    /// consumes them so a tap shorter than a frame is never lost.
    /// </summary>
    public sealed class TouchInputSource : IInputSource
    {
        private Vector2 _steer;
        private bool _jumpHeld;
        private bool _jumpTapLatched; // press+release within one frame
        private bool _action;
        private bool _pause;
        private SwipeDirection _swipe;

        public bool IsActive { get; set; } = true;

        public void SetSteer(Vector2 v) => _steer = Vector2.ClampMagnitude(v, 1f);

        public void SetJumpHeld(bool held)
        {
            if (!held && _jumpHeld) _jumpTapLatched = true;
            _jumpHeld = held;
        }

        public void PressAction() => _action = true;
        public void PressPause() => _pause = true;
        public void PushSwipe(SwipeDirection dir) { if (dir != SwipeDirection.None) _swipe = dir; }

        public void Contribute(ref InputFrame frame)
        {
            if (_steer.sqrMagnitude > frame.Steer.sqrMagnitude) frame.Steer = _steer;
            // A release that happened since last frame still reads as "held" this frame so the router sees the release edge next frame.
            frame.JumpHeld |= _jumpHeld || _jumpTapLatched;
            frame.ActionPressed |= _action;
            frame.PausePressed |= _pause;
            if (frame.Swipe == SwipeDirection.None) frame.Swipe = _swipe;

            _jumpTapLatched = false;
            _action = false;
            _pause = false;
            _swipe = SwipeDirection.None;
        }

        public void ReleaseAll()
        {
            _steer = Vector2.zero;
            _jumpHeld = false;
            _jumpTapLatched = false;
        }
    }
}

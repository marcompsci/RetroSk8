using RetroSk8.Core;
using UnityEngine;

namespace RetroSk8.Input
{
    /// <summary>One frame of merged player intent from every active input source.</summary>
    public struct InputFrame
    {
        public Vector2 Steer;
        public bool JumpHeld;
        public bool JumpPressed;
        public bool JumpReleased;
        public SwipeDirection Swipe;
        public bool ActionPressed;
        public bool PausePressed;
        public bool DebugPressed;

        public StickZone SteerZone => GestureRules.ClassifyStick(Steer.x, Steer.y, 0.35f);
    }

    /// <summary>
    /// A device family that contributes to the InputFrame (keyboard/gamepad, touch UI, future MFi/Bluetooth profiles).
    /// Sources report raw state; the router handles merging and edge detection for Jump.
    /// </summary>
    public interface IInputSource
    {
        bool IsActive { get; }
        /// <summary>Adds this source's state to the frame. Steer is merged by largest magnitude; buttons are OR-ed.</summary>
        void Contribute(ref InputFrame frame);
    }
}

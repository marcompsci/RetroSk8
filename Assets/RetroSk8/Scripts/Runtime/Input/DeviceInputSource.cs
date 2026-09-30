using System;
using RetroSk8.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RetroSk8.Input
{
    /// <summary>
    /// Keyboard (editor testing) and any Input System gamepad, including Bluetooth/MFi controllers.
    /// Actions are built in code so no generated asset is needed; rebinding UI can be layered on later.
    /// </summary>
    public sealed class DeviceInputSource : IInputSource, IDisposable
    {
        private const float RightStickFlickThreshold = 0.7f;
        private const float RightStickResetThreshold = 0.3f;

        private readonly InputAction _steer;
        private readonly InputAction _jump;
        private readonly InputAction _action;
        private readonly InputAction _pause;
        private readonly InputAction _debug;
        private readonly InputAction _swipeUp;
        private readonly InputAction _swipeDown;
        private readonly InputAction _swipeLeft;
        private readonly InputAction _swipeRight;
        private readonly InputAction _flickStick;
        private bool _flickArmed = true;

        public DeviceInputSource()
        {
            _steer = new InputAction("Steer", InputActionType.Value, expectedControlType: "Vector2");
            _steer.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            _steer.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            _steer.AddBinding("<Gamepad>/leftStick");

            _jump = Button("Jump", "<Keyboard>/space", "<Gamepad>/buttonSouth");
            _action = Button("Action", "<Keyboard>/e", "<Gamepad>/buttonWest", "<Gamepad>/rightShoulder");
            _pause = Button("Pause", "<Keyboard>/escape", "<Keyboard>/p", "<Gamepad>/start");
            _debug = Button("Debug", "<Keyboard>/f1", "<Keyboard>/backquote", "<Gamepad>/select");

            _swipeUp = Button("SwipeUp", "<Keyboard>/i", "<Gamepad>/dpad/up", "<Gamepad>/buttonNorth");
            _swipeDown = Button("SwipeDown", "<Keyboard>/k", "<Gamepad>/dpad/down", "<Gamepad>/buttonEast");
            _swipeLeft = Button("SwipeLeft", "<Keyboard>/j", "<Gamepad>/dpad/left");
            _swipeRight = Button("SwipeRight", "<Keyboard>/l", "<Gamepad>/dpad/right");

            _flickStick = new InputAction("Flick", InputActionType.Value, "<Gamepad>/rightStick", expectedControlType: "Vector2");

            foreach (var a in All()) a.Enable();
        }

        public bool IsActive => true;

        public void Contribute(ref InputFrame frame)
        {
            Vector2 steer = Vector2.ClampMagnitude(_steer.ReadValue<Vector2>(), 1f);
            if (steer.sqrMagnitude > frame.Steer.sqrMagnitude) frame.Steer = steer;

            frame.JumpHeld |= _jump.IsPressed();
            frame.ActionPressed |= _action.WasPressedThisFrame();
            frame.PausePressed |= _pause.WasPressedThisFrame();
            frame.DebugPressed |= _debug.WasPressedThisFrame();

            if (frame.Swipe == SwipeDirection.None)
            {
                if (_swipeUp.WasPressedThisFrame()) frame.Swipe = SwipeDirection.Up;
                else if (_swipeDown.WasPressedThisFrame()) frame.Swipe = SwipeDirection.Down;
                else if (_swipeLeft.WasPressedThisFrame()) frame.Swipe = SwipeDirection.Left;
                else if (_swipeRight.WasPressedThisFrame()) frame.Swipe = SwipeDirection.Right;
                else frame.Swipe = ReadFlick();
            }
        }

        // Right stick acts as a swipe: a flick past the threshold fires once, then must return near centre.
        private SwipeDirection ReadFlick()
        {
            Vector2 v = _flickStick.ReadValue<Vector2>();
            float m = v.magnitude;
            if (!_flickArmed)
            {
                if (m < RightStickResetThreshold) _flickArmed = true;
                return SwipeDirection.None;
            }
            if (m < RightStickFlickThreshold) return SwipeDirection.None;
            _flickArmed = false;
            return GestureRules.ClassifySwipe(v.x, v.y, RightStickFlickThreshold * 0.9f);
        }

        private static InputAction Button(string name, params string[] paths)
        {
            var a = new InputAction(name, InputActionType.Button);
            foreach (var p in paths) a.AddBinding(p);
            return a;
        }

        private InputAction[] All() => new[]
        {
            _steer, _jump, _action, _pause, _debug, _swipeUp, _swipeDown, _swipeLeft, _swipeRight, _flickStick,
        };

        public void Dispose()
        {
            foreach (var a in All())
            {
                a.Disable();
                a.Dispose();
            }
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace RetroSk8.Input
{
    /// <summary>Merges every IInputSource into one InputFrame per rendered frame, before gameplay scripts run.</summary>
    [DefaultExecutionOrder(-200)]
    public sealed class PlayerInputRouter : MonoBehaviour
    {
        private readonly List<IInputSource> _sources = new List<IInputSource>();
        private DeviceInputSource _device;
        private bool _prevJumpHeld;

        public InputFrame Frame { get; private set; }
        public TouchInputSource Touch { get; private set; }

        private void Awake()
        {
            _device = new DeviceInputSource();
            Touch = new TouchInputSource();
            _sources.Add(_device);
            _sources.Add(Touch);
        }

        public void AddSource(IInputSource source)
        {
            if (source != null && !_sources.Contains(source)) _sources.Add(source);
        }

        private void Update()
        {
            var f = new InputFrame();
            foreach (var s in _sources)
            {
                if (s.IsActive) s.Contribute(ref f);
            }
            f.JumpPressed = f.JumpHeld && !_prevJumpHeld;
            f.JumpReleased = !f.JumpHeld && _prevJumpHeld;
            _prevJumpHeld = f.JumpHeld;
            Frame = f;
        }

        private void OnDestroy() => _device?.Dispose();
    }
}

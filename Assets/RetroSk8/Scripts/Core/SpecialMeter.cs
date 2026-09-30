using System;

namespace RetroSk8.Core
{
    /// <summary>Fills from banked combo points; when full, the next air trick becomes that family's special.</summary>
    public sealed class SpecialMeter
    {
        private readonly float _pointsToFill;

        public SpecialMeter(float pointsToFill)
        {
            _pointsToFill = Math.Max(1f, pointsToFill);
        }

        public float Value { get; private set; }
        public bool IsReady => Value >= 1f;

        public void Add(long bankedPoints)
        {
            if (bankedPoints <= 0) return;
            Value = Math.Min(1f, Value + bankedPoints / _pointsToFill);
        }

        public bool TryConsume()
        {
            if (!IsReady) return false;
            Value = 0f;
            return true;
        }

        public void Reset() => Value = 0f;
    }
}

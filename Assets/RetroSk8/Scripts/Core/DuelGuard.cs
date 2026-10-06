using System;

namespace RetroSk8.Core
{
    /// <summary>
    /// Phase 19: online S.K.A.T.E. treats everything the other phone sends as untrusted. Messages over the size cap are
    /// dropped unread, every field is checked or clamped after parsing (names through the word filter, points within
    /// what a line can pay, frame numbers finite and in the world), and a rate limit stops a flood. Engine-free and
    /// unit-tested; DuelSession applies it to each received message.
    /// </summary>
    public static class DuelGuard
    {
        /// <summary>The biggest message the game sends is a ~120-byte frame; anything past this is refused.</summary>
        public const int MaxMessageBytes = 512;
        public const int MaxNameChars = 16;
        public const int MaxLabelChars = 60;
        /// <summary>Turns in one game can't get anywhere near this.</summary>
        public const int MaxTurn = 1000;
        /// <summary>Frame positions farther than this (m) from the world origin are nonsense.</summary>
        public const float MaxCoordinate = 5000f;
        /// <summary>Messages per second allowed, with a burst on top (frames are sent 12 times a second).</summary>
        public const float MessagesPerSecond = 40f;
        public const float Burst = 80f;

        /// <summary>
        /// Checks and cleans a parsed message in place. Returns false when it should be dropped (a broken frame or
        /// an out-of-range turn); names, labels and points are fixed up rather than dropped.
        /// </summary>
        public static bool Sanitize(DuelMessage m)
        {
            if (m == null) return false;
            switch (m.Type)
            {
                case DuelMessageType.Hello:
                    m.Name = Gallery.SafeName(CodeLimits.Cap(m.Name, 64), MaxNameChars, "SKATER");
                    if (m.Style < 0 || m.Style > 3) m.Style = 0;
                    return true;
                case DuelMessageType.Start:
                    if (m.ParkIndex < 0 || m.ParkIndex > 255) return false;
                    m.FirstSetter &= 1;
                    return true;
                case DuelMessageType.AttemptBegin:
                    return m.Turn >= 0 && m.Turn <= MaxTurn;
                case DuelMessageType.AttemptResult:
                    if (m.Turn < 0 || m.Turn > MaxTurn) return false;
                    m.Points = ScoreLimits.ClampCombo(m.Points);
                    m.Label = CleanLabel(m.Label);
                    return true;
                case DuelMessageType.Frame:
                    return FrameOk(m.Frame);
                case DuelMessageType.Rematch:
                case DuelMessageType.Leave:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>The trick list shown for the other player's line: printable ASCII only, capped.</summary>
        public static string CleanLabel(string label)
        {
            if (string.IsNullOrEmpty(label)) return "";
            var sb = new System.Text.StringBuilder(Math.Min(label.Length, MaxLabelChars));
            foreach (char c in label)
            {
                if (sb.Length >= MaxLabelChars) break;
                if (c >= ' ' && c <= '~') sb.Append(c);
            }
            string s = sb.ToString().Trim();
            return Gallery.IsBlocked(s) ? "" : s;
        }

        public static bool FrameOk(ReplayFrame f) =>
            Finite(f.Time) && f.Time >= 0f && f.Time < 3600f
            && VecOk(f.Position) && VecOk(f.BoardPosition)
            && QuatOk(f.Rotation) && QuatOk(f.Pose) && QuatOk(f.Body) && QuatOk(f.Board);

        private static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);

        private static bool VecOk(RVec3 v) =>
            Finite(v.X) && Finite(v.Y) && Finite(v.Z)
            && Math.Abs(v.X) <= MaxCoordinate && Math.Abs(v.Y) <= MaxCoordinate && Math.Abs(v.Z) <= MaxCoordinate;

        private static bool QuatOk(RQuat q)
        {
            if (!Finite(q.X) || !Finite(q.Y) || !Finite(q.Z) || !Finite(q.W)) return false;
            float len = q.X * q.X + q.Y * q.Y + q.Z * q.Z + q.W * q.W;
            return len > 0.5f && len < 1.5f; // a rotation is (nearly) unit length
        }
    }

    /// <summary>A token bucket: <see cref="Allow"/> spends one token, tokens refill at a steady rate up to a cap.</summary>
    public sealed class RateLimiter
    {
        private readonly float _rate, _burst;
        private float _tokens;
        private double _last = double.NaN;

        public RateLimiter(float perSecond, float burst)
        {
            _rate = Math.Max(0.01f, perSecond);
            _burst = Math.Max(1f, burst);
            _tokens = _burst;
        }

        /// <summary>Messages dropped so far.</summary>
        public int Dropped { get; private set; }

        /// <param name="now">A steadily increasing clock in seconds.</param>
        public bool Allow(double now)
        {
            if (!double.IsNaN(_last) && now > _last) _tokens = (float)Math.Min(_burst, _tokens + (now - _last) * _rate);
            _last = double.IsNaN(_last) || now > _last ? now : _last;
            if (_tokens >= 1f) { _tokens -= 1f; return true; }
            Dropped++;
            return false;
        }
    }
}

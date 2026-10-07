using System;
using System.Collections.Generic;

namespace RetroSk8.Core
{
    /// <summary>The flip part of a custom trick (board rotations). Saved by number: append only.</summary>
    public enum TrickFlip { None = 0, Kick = 1, Heel = 2, DoubleKick = 3, DoubleHeel = 4, TripleKick = 5, Front = 6, Back = 7 }

    /// <summary>The board's flat spin (degrees). Saved by number: append only.</summary>
    public enum TrickShove { None = 0, Half = 1, Full = 2, OneAndHalf = 3 }

    /// <summary>The grab held during the trick. Saved by number: append only.</summary>
    public enum TrickGrab { None = 0, Nose = 1, Tail = 2, Mute = 3, Stale = 4, Reach = 5 }

    /// <summary>The skater's own body spin (whole turns, so you land facing the way you rode in). Saved by number: append only.</summary>
    public enum TrickBody { None = 0, Full = 1, Double = 2 }

    /// <summary>One custom trick as saved (JsonUtility-friendly).</summary>
    [Serializable]
    public sealed class CustomTrick
    {
        /// <summary>Slot number 1..<see cref="CustomTricks.MaxTricks"/>; the trick's id is "custom_" + slot.</summary>
        public int slot;
        public string name = "MY TRICK";
        public int flip;
        public int shove;
        public int grab;
        public int body;
        /// <summary>The air gesture it takes over (see <see cref="CustomTricks.GestureName"/>), or -1 when switched off.</summary>
        public int gesture = -1;

        public CustomTrick Clone() => (CustomTrick)MemberwiseClone();
    }

    /// <summary>How a custom trick moves and scores, worked out from its parts.</summary>
    public struct CustomTrickShape
    {
        public float RollTurns, PitchTurns, YawDegrees, BodySpin;
        public float GrabX, GrabY, GrabZ;
        public float Duration;
        public int Points;
        public TrickCategory Category;
    }

    /// <summary>
    /// Create-a-Trick (Phase 27): build your own named air trick from a flip, a board spin, a grab and a body spin,
    /// put it on one of the twelve air gestures (swipe up / down / sideways with the stick neutral, left, right or
    /// up-down), and share it as a code. Each part is worth what the built-in trick that does it is worth; one part
    /// pays 90% of that and a combination 75% of the sum, capped well under a special. More parts also take longer in
    /// the air, so they're riskier to land. Engine-free and unit-tested.
    /// </summary>
    public static class CustomTricks
    {
        public const int MaxTricks = 4;
        public const int GestureCount = 12;
        public const int MaxNameLength = 16;
        public const string IdPrefix = "custom_";
        public const string CodePrefix = "RT:";
        private const int CodeVersion = 1;

        public static readonly string[] FlipNames = { "NO FLIP", "KICK ROLL", "HEEL ROLL", "DOUBLE KICK", "DOUBLE HEEL", "TRIPLE KICK", "FRONT TUMBLE", "BACK TUMBLE" };
        public static readonly string[] ShoveNames = { "NO SPIN", "180 SPIN", "360 SPIN", "540 SPIN" };
        public static readonly string[] GrabNames = { "NO GRAB", "NOSE GRAB", "TAIL GRAB", "MIDDLE GRAB", "BACK GRAB", "REACH GRAB" };
        public static readonly string[] BodyNames = { "NO BODY SPIN", "BODY 360", "BODY 720" };

        // What each part is worth on its own, matched to the built-in trick that does the same thing (Tide Flip 400,
        // Heel Tumble 450, Keel Twist 750, Pivot Scoop 300, Gyre Scoop 500, Wharf Wheel 800, the grabs 350-550).
        private static readonly int[] FlipPoints = { 0, 400, 450, 750, 800, 1000, 650, 650 };
        private static readonly int[] ShovePoints = { 0, 300, 500, 800 };
        private static readonly int[] GrabPoints = { 0, 400, 450, 350, 400, 550 };
        private static readonly int[] BodyPoints = { 0, 500, 900 };
        /// <summary>A one-part custom trick pays a bit less than its built-in twin; combining parts pays less than their sum.</summary>
        public const float SinglePartFactor = 0.9f, ComboFactor = 0.75f;
        /// <summary>Never worth more than this (a built-in special is 2,000-3,000).</summary>
        public const int MaxPoints = 1500;

        public static string IdFor(int slot) => IdPrefix + slot;
        public static bool IsCustomId(string id) => id != null && id.StartsWith(IdPrefix, StringComparison.Ordinal);

        /// <summary>Gesture index = family * 4 + variation: family 0 flip (swipe up), 1 grab (down), 2 shove (sideways).</summary>
        public static int Gesture(TrickFamily family, int variation) => FamilyIndex(family) * 4 + Math.Max(0, Math.Min(3, variation));

        public static TrickFamily FamilyOf(int gesture) => gesture / 4 == 1 ? TrickFamily.Grab : gesture / 4 == 2 ? TrickFamily.Shove : TrickFamily.Flip;

        private static int FamilyIndex(TrickFamily f) => f == TrickFamily.Grab ? 1 : f == TrickFamily.Shove ? 2 : 0;

        private static readonly string[] Swipes = { "SWIPE UP", "SWIPE DOWN", "SWIPE SIDEWAYS" };
        private static readonly string[] Sticks = { "", " + STICK LEFT", " + STICK RIGHT", " + STICK UP/DOWN" };

        public static string GestureName(int gesture) =>
            gesture < 0 || gesture >= GestureCount ? "OFF" : Swipes[gesture / 4] + Sticks[gesture % 4];

        /// <summary>Movement, airtime and points for a trick's parts (out-of-range parts count as none).</summary>
        public static CustomTrickShape Shape(CustomTrick t)
        {
            var s = new CustomTrickShape();
            if (t == null) return s;
            switch ((TrickFlip)Clamp(t.flip, FlipNames.Length))
            {
                case TrickFlip.Kick: s.RollTurns = 1f; break;
                case TrickFlip.Heel: s.RollTurns = -1f; break;
                case TrickFlip.DoubleKick: s.RollTurns = 2f; break;
                case TrickFlip.DoubleHeel: s.RollTurns = -2f; break;
                case TrickFlip.TripleKick: s.RollTurns = 3f; break;
                case TrickFlip.Front: s.PitchTurns = 1f; break;
                case TrickFlip.Back: s.PitchTurns = -1f; break;
            }
            s.YawDegrees = 180f * Clamp(t.shove, ShoveNames.Length);
            switch ((TrickGrab)Clamp(t.grab, GrabNames.Length))
            {
                case TrickGrab.Nose: s.GrabX = -30f; break;
                case TrickGrab.Tail: s.GrabX = 35f; break;
                case TrickGrab.Mute: s.GrabZ = 25f; break;
                case TrickGrab.Stale: s.GrabZ = -25f; s.GrabY = 15f; break;
                case TrickGrab.Reach: s.GrabY = 40f; s.GrabZ = -30f; break;
            }
            s.BodySpin = 360f * Clamp(t.body, BodyNames.Length);

            // Airtime: a quick base plus time for each rotation and for holding a grab.
            float d = 0.3f + 0.12f * Math.Abs(s.RollTurns) + 0.16f * Math.Abs(s.PitchTurns) + 0.09f * (s.YawDegrees / 180f)
                      + (t.grab > 0 && t.grab < GrabNames.Length ? 0.14f : 0f) + 0.1f * (s.BodySpin / 180f);
            s.Duration = Math.Min(MaxDuration, d);
            // Points: each part at its built-in value, then 90% for a single part or 75% of the sum for a combination
            // (doing the parts as separate tricks takes longer but also feeds the combo multiplier), capped.
            int parts = (s.RollTurns != 0f || s.PitchTurns != 0f ? 1 : 0) + (s.YawDegrees > 0f ? 1 : 0) + (t.grab > 0 && t.grab < GrabNames.Length ? 1 : 0) + (s.BodySpin > 0f ? 1 : 0);
            int sum = FlipPoints[Clamp(t.flip, FlipNames.Length)] + ShovePoints[Clamp(t.shove, ShoveNames.Length)]
                      + GrabPoints[Clamp(t.grab, GrabNames.Length)] + BodyPoints[Clamp(t.body, BodyNames.Length)];
            double raw = sum * (parts <= 1 ? SinglePartFactor : ComboFactor);
            s.Points = Math.Min(MaxPoints, (int)Math.Round(raw / 10.0 + 1e-6, MidpointRounding.AwayFromZero) * 10); // tens, halves up
            s.Category = parts == 1 && t.grab > 0 && t.grab < GrabNames.Length ? TrickCategory.Grab : TrickCategory.BoardFlip;
            return s;
        }

        /// <summary>The longest a custom trick can take (a built-in special is 0.8-1 s).</summary>
        public const float MaxDuration = 1.0f;

        /// <summary>A trick needs at least one part to be worth anything.</summary>
        public static bool IsEmpty(CustomTrick t) =>
            t == null || (Clamp(t.flip, FlipNames.Length) == 0 && Clamp(t.shove, ShoveNames.Length) == 0 && Clamp(t.grab, GrabNames.Length) == 0 && Clamp(t.body, BodyNames.Length) == 0);

        /// <summary>A name built from the parts, e.g. "360 KICK ROLL MUTE".</summary>
        public static string SuggestName(CustomTrick t)
        {
            if (IsEmpty(t)) return "MY TRICK";
            var parts = new List<string>();
            if (t.body > 0) parts.Add(t.body == 2 ? "BODY 720" : "BODY 360");
            if (t.shove > 0) parts.Add(new[] { "", "180", "360", "540" }[Clamp(t.shove, 4)]);
            if (t.flip > 0) parts.Add(new[] { "", "KICK", "HEEL", "DBL KICK", "DBL HEEL", "TRIPLE", "FRONT", "BACK" }[Clamp(t.flip, 8)]);
            if (t.grab > 0) parts.Add(new[] { "", "NOSE", "TAIL", "MUTE", "STALE", "REACH" }[Clamp(t.grab, 6)]);
            string n = string.Join(" ", parts);
            return n.Length > MaxNameLength ? n.Substring(0, MaxNameLength).TrimEnd() : n;
        }

        /// <summary>
        /// Repairs a saved list: drops empty or out-of-range slots and duplicates, clamps parts, and keeps one trick per
        /// gesture (the lower slot wins).
        /// </summary>
        public static void Sanitize(List<CustomTrick> list)
        {
            if (list == null) return;
            list.RemoveAll(t => t == null || t.slot < 1 || t.slot > MaxTricks);
            var seenSlots = new HashSet<int>();
            list.Sort((a, b) => a.slot.CompareTo(b.slot));
            list.RemoveAll(t => !seenSlots.Add(t.slot));
            var usedGestures = new HashSet<int>();
            foreach (var t in list)
            {
                t.flip = Clamp(t.flip, FlipNames.Length);
                t.shove = Clamp(t.shove, ShoveNames.Length);
                t.grab = Clamp(t.grab, GrabNames.Length);
                t.body = Clamp(t.body, BodyNames.Length);
                t.name = Gallery.SafeName(t.name, MaxNameLength, SuggestName(t));
                if (t.gesture < 0 || t.gesture >= GestureCount || !usedGestures.Add(t.gesture)) t.gesture = -1;
            }
        }

        /// <summary>The trick that owns a gesture, or null.</summary>
        public static CustomTrick ForGesture(IList<CustomTrick> list, int gesture)
        {
            if (list == null || gesture < 0) return null;
            foreach (var t in list) if (t != null && t.gesture == gesture && !IsEmpty(t)) return t;
            return null;
        }

        /// <summary>First free slot number, or 0 when all are used.</summary>
        public static int FreeSlot(IList<CustomTrick> list)
        {
            for (int s = 1; s <= MaxTricks; s++)
            {
                bool used = false;
                if (list != null) foreach (var t in list) if (t != null && t.slot == s) { used = true; break; }
                if (!used) return s;
            }
            return 0;
        }

        // ---------------------------------------------------------------- codes

        public static bool IsCode(string text) => text != null && text.TrimStart().StartsWith(CodePrefix, StringComparison.Ordinal);

        /// <summary>A short code for a trick's name and parts (not its slot or gesture: the friend picks those).</summary>
        public static string Encode(CustomTrick t, string from)
        {
            if (t == null || IsEmpty(t)) throw new ArgumentException("Nothing to share");
            var w = new ShareCodes.BitWriter();
            w.Write(CodeVersion, 4);
            w.Write(Clamp(t.flip, FlipNames.Length), 4);
            w.Write(Clamp(t.shove, ShoveNames.Length), 3);
            w.Write(Clamp(t.grab, GrabNames.Length), 4);
            w.Write(Clamp(t.body, BodyNames.Length), 3);
            ShareCodes.WriteName(w, t.name ?? "", MaxNameLength);
            ShareCodes.WriteName(w, from ?? "", ShareCodes.MaxFromLength);
            return GhostCodes.Seal(CodePrefix, w.ToBytes());
        }

        public static bool TryDecode(string code, out CustomTrick trick, out string from, out string error)
        {
            trick = null;
            from = null;
            if (string.IsNullOrWhiteSpace(code)) { error = "NO CODE FOUND"; return false; }
            if (code.Length > CodeLimits.MaxParkCodeChars) { error = "THAT TRICK CODE IS TOO LONG"; return false; }
            if (!IsCode(code)) { error = "THAT ISN'T A TRICK CODE"; return false; }
            var bytes = GhostCodes.Unseal(CodePrefix, code);
            if (bytes == null) { error = "THAT TRICK CODE IS INCOMPLETE (COPY ALL OF IT)"; return false; }
            try
            {
                var r = new ShareCodes.BitReader(bytes);
                if (r.Read(4) != CodeVersion) { error = "THAT TRICK NEEDS A NEWER VERSION"; return false; }
                var t = new CustomTrick { flip = r.Read(4), shove = r.Read(3), grab = r.Read(4), body = r.Read(3) };
                if (t.flip >= FlipNames.Length || t.shove >= ShoveNames.Length || t.grab >= GrabNames.Length || t.body >= BodyNames.Length || IsEmpty(t))
                {
                    error = "THAT TRICK NEEDS A NEWER VERSION";
                    return false;
                }
                t.name = Gallery.SafeName(ShareCodes.ReadName(r, MaxNameLength), MaxNameLength, SuggestName(t));
                from = ShareCodes.ReadName(r, ShareCodes.MaxFromLength);
                trick = t;
                error = null;
                return true;
            }
            catch (Exception)
            {
                error = "THAT TRICK CODE IS DAMAGED";
                return false;
            }
        }

        private static int Clamp(int v, int count) => v < 0 || v >= count ? 0 : v;
    }
}

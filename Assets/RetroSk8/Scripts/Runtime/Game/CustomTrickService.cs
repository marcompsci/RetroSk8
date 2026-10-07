using System.Collections.Generic;
using RetroSk8.Core;
using RetroSk8.Data;
using RetroSk8.Save;
using UnityEngine;

namespace RetroSk8.Game
{
    /// <summary>
    /// Phase 27: Create-a-Trick at runtime. Turns saved custom tricks into TrickDefinitions and answers "does a custom
    /// trick own this air gesture?" for the trick controller. Custom tricks sit out games where both sides must play
    /// the same trick list (S.K.A.T.E.) and the lessons that teach the built-in tricks.
    /// </summary>
    public static class CustomTrickService
    {
        private static readonly Dictionary<int, TrickDefinition> s_defs = new Dictionary<int, TrickDefinition>();
        private static string s_builtFrom;

        public static List<CustomTrick> Tricks => SaveManager.Data.customTricks;

        /// <summary>Whether custom tricks are used in this kind of session.</summary>
        public static bool ActiveIn(RunMode mode) =>
            mode != RunMode.Duel && mode != RunMode.Tutorial && mode != RunMode.Replay && string.IsNullOrEmpty(GameSession.LessonId);

        /// <summary>The custom trick on this gesture as a TrickDefinition, or null (use the built-in trick).</summary>
        public static TrickDefinition For(TrickFamily family, int variation)
        {
            if (!ActiveIn(GameSession.Mode)) return null;
            var t = CustomTricks.ForGesture(Tricks, CustomTricks.Gesture(family, variation));
            return t == null ? null : Definition(t);
        }

        /// <summary>
        /// True when the built-in trick a custom trick would replace is today's unfinished Daily Trick: the custom
        /// trick steps aside so the daily can still be done.
        /// </summary>
        public static bool StepsAsideFor(TrickDefinition builtIn)
        {
            if (builtIn == null || DailyTrickService.DoneToday) return false;
            var today = DailyTrickService.Today;
            return today != null && today.Kind == DailyTrickKind.LandTrick && today.TrickId == builtIn.id;
        }

        /// <summary>The saved custom trick behind an id like "custom_2", as a TrickDefinition, or null.</summary>
        public static TrickDefinition ById(string id)
        {
            if (!CustomTricks.IsCustomId(id) || !int.TryParse(id.Substring(CustomTricks.IdPrefix.Length), out int slot)) return null;
            var t = Find(slot);
            return t == null || CustomTricks.IsEmpty(t) ? null : Definition(t);
        }

        /// <summary>A TrickDefinition for a custom trick (rebuilt when the saved tricks change).</summary>
        public static TrickDefinition Definition(CustomTrick t)
        {
            string key = Fingerprint();
            if (key != s_builtFrom) { s_defs.Clear(); s_builtFrom = key; }
            if (s_defs.TryGetValue(t.slot, out var d) && d != null) return d;
            var shape = CustomTricks.Shape(t);
            d = TrickDefinition.CreateRuntime(CustomTricks.IdFor(t.slot), t.name, shape.Category, CustomTricks.FamilyOf(Mathf.Max(0, t.gesture)), 0, shape.Points, shape.Duration);
            d.rollTurns = shape.RollTurns;
            d.pitchTurns = shape.PitchTurns;
            d.yawDegrees = shape.YawDegrees;
            d.bodySpinDegrees = shape.BodySpin;
            d.grabTilt = new Vector3(shape.GrabX, shape.GrabY, shape.GrabZ);
            s_defs[t.slot] = d;
            return d;
        }

        private static string Fingerprint()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var t in Tricks) sb.Append(t.slot).Append(t.name).Append(t.flip).Append(t.shove).Append(t.grab).Append(t.body).Append(t.gesture).Append('|');
            return sb.ToString();
        }

        /// <summary>Saves a trick into its slot (taking its gesture from any other trick that had it).</summary>
        public static void Save(CustomTrick t)
        {
            if (t == null || t.slot < 1 || t.slot > CustomTricks.MaxTricks) return;
            var list = Tricks;
            list.RemoveAll(x => x.slot == t.slot);
            if (t.gesture >= 0) foreach (var other in list) if (other.gesture == t.gesture) other.gesture = -1;
            list.Add(t.Clone());
            CustomTricks.Sanitize(list);
            SaveManager.Save();
        }

        public static void Delete(int slot)
        {
            Tricks.RemoveAll(x => x.slot == slot);
            SaveManager.Save();
        }

        public static CustomTrick Find(int slot)
        {
            foreach (var t in Tricks) if (t.slot == slot) return t;
            return null;
        }
    }
}

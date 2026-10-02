using System.Collections.Generic;
using RetroSk8.Core;
using UnityEngine;

namespace RetroSk8.Data
{
    /// <summary>Runtime TrickDefinitions for the Phase 10 trick and style pack (<see cref="StyleTricks"/>), built once.</summary>
    public static class StylePack
    {
        private static readonly Dictionary<string, TrickDefinition> s_defs = new Dictionary<string, TrickDefinition>();

        public static TrickDefinition Get(StyleTrick t)
        {
            if (t == null) return null;
            if (s_defs.TryGetValue(t.Id, out var d) && d != null) return d;
            var family = t.Category == TrickCategory.Special ? TrickFamily.Flip : TrickFamily.Shove;
            d = TrickDefinition.CreateRuntime(t.Id, t.Name, t.Category, family, 0, t.Points, t.Duration);
            d.isSpecial = t.Category == TrickCategory.Special;
            d.rollTurns = t.RollTurns;
            d.pitchTurns = t.PitchTurns;
            d.yawDegrees = t.YawDegrees;
            d.bodySpinDegrees = t.BodySpin;
            d.grabTilt = new Vector3(t.GrabX, t.GrabY, t.GrabZ);
            s_defs[t.Id] = d;
            return d;
        }

        /// <summary>The signature special for the saved skater style.</summary>
        public static TrickDefinition Signature() => Get(StyleTricks.Signature(RetroSk8.Save.SaveManager.Data.look.style));
    }
}

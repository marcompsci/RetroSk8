namespace RetroSk8.Core
{
    /// <summary>Your skater's style (Create-a-Skater). It picks the signature special. Saved by number: append only.</summary>
    public enum SkaterStyle
    {
        Street = 0,
        Vert = 1,
        Tech = 2,
        Flow = 3,
    }

    /// <summary>A trick from the style pack, before it becomes a TrickDefinition.</summary>
    public sealed class StyleTrick
    {
        public string Id;
        public string Name;
        public TrickCategory Category;
        public int Points;
        public float Duration;
        // Procedural pose (see TrickDefinition).
        public float RollTurns, PitchTurns, YawDegrees, BodySpin;
        public float GrabX, GrabY, GrabZ;
    }

    /// <summary>
    /// Phase 10 trick and style pack. Each is a variation on an input you already know, so nothing new to learn:
    /// - Pop with the stick held DOWN: a quick tap is a <b>No-Comply</b>, a charged pop a <b>Boneless</b> (flat ground or a manual).
    /// - Start a grind on a ledge or coping with the stick DOWN for a <b>Bluntslide</b>.
    /// - Start a manual with the stick LEFT for a <b>One-Foot Manual</b>, RIGHT for a <b>Casper</b> (each pays more than a plain manual).
    /// - Hit a wall head-on with the stick DOWN for a <b>Footplant</b> instead of a wallplant.
    /// - With the special meter full, a flip swipe performs your style's <b>signature special</b>.
    /// All names other than the common skate trick names are original.
    /// </summary>
    public static class StyleTricks
    {
        public static readonly StyleTrick NoComply = new StyleTrick
        {
            Id = "no_comply", Name = "No-Comply", Category = TrickCategory.Pop, Points = 180, Duration = 0.32f, YawDegrees = 180f,
        };

        public static readonly StyleTrick Boneless = new StyleTrick
        {
            Id = "boneless", Name = "Boneless", Category = TrickCategory.Pop, Points = 220, Duration = 0.38f, GrabX = 18f,
        };

        public static readonly StyleTrick Bluntslide = new StyleTrick
        {
            Id = "bluntslide", Name = "Bluntslide", Category = TrickCategory.Grind, Points = 320, Duration = 0f,
        };

        public static readonly StyleTrick OneFootManual = new StyleTrick
        {
            Id = "one_foot_manual", Name = "One-Foot Manual", Category = TrickCategory.Manual, Points = 260, Duration = 0f,
        };

        public static readonly StyleTrick Casper = new StyleTrick
        {
            Id = "casper", Name = "Casper", Category = TrickCategory.Manual, Points = 320, Duration = 0f,
        };

        public const string FootplantId = "footplant";
        public const string FootplantName = "Footplant";
        /// <summary>Footplants pay a quarter more than a wallplant.</summary>
        public const float FootplantBonus = 1.25f;

        public static readonly StyleTrick[] Signatures =
        {
            new StyleTrick { Id = "sig_street", Name = "Gridline Lightning", Category = TrickCategory.Special, Points = 2600, Duration = 0.85f, RollTurns = 2f, YawDegrees = 360f },
            new StyleTrick { Id = "sig_vert", Name = "Skylight Orbit", Category = TrickCategory.Special, Points = 3000, Duration = 1.0f, BodySpin = 540f, GrabX = 30f, GrabZ = 20f },
            new StyleTrick { Id = "sig_tech", Name = "Tidewater Triple", Category = TrickCategory.Special, Points = 2800, Duration = 0.9f, RollTurns = 3f, PitchTurns = 1f },
            new StyleTrick { Id = "sig_flow", Name = "Sundown Carousel", Category = TrickCategory.Special, Points = 2700, Duration = 0.95f, BodySpin = 360f, YawDegrees = 180f, GrabY = 25f },
        };

        public static readonly string[] StyleNames = { "STREET", "VERT", "TECH", "FLOW" };

        public static StyleTrick Signature(int style) => Signatures[SkaterLook.Wrap(style, Signatures.Length)];

        /// <summary>A pop at least this charged (with the stick down) is a Boneless; a quicker one is a No-Comply.</summary>
        public const float BonelessCharge = 0.6f;

        /// <summary>
        /// Pop variant, or null for a plain ollie. Hold the stick DOWN while popping from flat ground or a manual:
        /// a quick tap is a No-Comply, a charged pop a Boneless. (Up stays free for pushing.)
        /// </summary>
        public static StyleTrick PopVariant(StickZone zone, float charge, bool fromFlatOrManual)
        {
            if (!fromFlatOrManual || zone != StickZone.Down) return null;
            return charge >= BonelessCharge ? Boneless : NoComply;
        }

        /// <summary>True when a grind should become a bluntslide (stick down on a ledge or coping, not a round rail).</summary>
        public static bool IsBluntslide(StickZone zone, bool onRoundRail) => zone == StickZone.Down && !onRoundRail;

        /// <summary>Manual variation for the stick at entry, or null for the regular tail/nose manual.</summary>
        public static StyleTrick ManualVariant(StickZone zone) =>
            zone == StickZone.Left ? OneFootManual : zone == StickZone.Right ? Casper : null;

        public static bool IsFootplant(StickZone zone) => zone == StickZone.Down;
    }
}

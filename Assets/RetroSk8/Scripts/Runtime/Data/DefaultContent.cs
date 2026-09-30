using RetroSk8.Core;
using UnityEngine;

namespace RetroSk8.Data
{
    /// <summary>
    /// The original starter content. The editor setup saves these as assets; at runtime they are a fallback
    /// so any scene can be played directly even before setup has run.
    /// All names here are invented for Retro Sk8.
    /// </summary>
    public static class DefaultContent
    {
        public static TrickLibrary CreateTrickLibrary()
        {
            var lib = ScriptableObject.CreateInstance<TrickLibrary>();
            lib.name = "TrickLibrary";

            // Flip family (swipe up). Variation: 0 neutral, 1 left, 2 right, 3 up/down.
            lib.airTricks.Add(Flip("flip_tide", "Tide Flip", 0, 400, 0.42f, roll: 1f));
            lib.airTricks.Add(Flip("flip_heel_tumble", "Heel Tumble", 1, 450, 0.42f, roll: -1f));
            lib.airTricks.Add(Flip("flip_riffle", "Riffle Flip", 2, 600, 0.5f, roll: 1f, yaw: 180f));
            lib.airTricks.Add(Flip("flip_keel_twist", "Keel Twist", 3, 750, 0.6f, roll: 2f));

            // Grab family (swipe down).
            lib.airTricks.Add(Grab("grab_anchor", "Anchor Grab", 0, 350, 0.5f, new Vector3(0f, 0f, 25f)));
            lib.airTricks.Add(Grab("grab_porthole", "Porthole Grab", 1, 400, 0.55f, new Vector3(-30f, 0f, 0f)));
            lib.airTricks.Add(Grab("grab_tailhook", "Tailhook", 2, 450, 0.55f, new Vector3(35f, 0f, 0f)));
            lib.airTricks.Add(Grab("grab_buoy_snatch", "Buoy Snatch", 3, 550, 0.62f, new Vector3(0f, 40f, -30f)));

            // Shove family (swipe left/right): flat board rotations.
            lib.airTricks.Add(Shove("shove_pivot_scoop", "Pivot Scoop", 0, 300, 0.36f, 180f));
            lib.airTricks.Add(Shove("shove_gyre_scoop", "Gyre Scoop", 1, 500, 0.48f, 360f));
            lib.airTricks.Add(Shove("shove_scoop_roll", "Scoop Roll", 2, 650, 0.52f, 180f, roll: 1f));
            lib.airTricks.Add(Shove("shove_wharf_wheel", "Wharf Wheel", 3, 800, 0.62f, 540f));

            // Specials: one per family, available when the special meter is full.
            var s1 = Flip("special_lighthouse", "Lighthouse Twirl", 0, 2200, 0.8f, roll: 3f);
            s1.category = TrickCategory.Special; s1.isSpecial = true; s1.bodySpinDegrees = 360f;
            var s2 = Grab("special_gull_dive", "Gull Dive", 0, 2000, 0.85f, new Vector3(-60f, 0f, 45f));
            s2.category = TrickCategory.Special; s2.isSpecial = true;
            var s3 = Shove("special_tidal_twister", "Tidal Twister", 0, 2400, 0.85f, 720f, roll: 2f);
            s3.category = TrickCategory.Special; s3.isSpecial = true;
            lib.specials.Add(s1);
            lib.specials.Add(s2);
            lib.specials.Add(s3);

            // Grinds by stick direction at ACTION tap.
            lib.grinds.Add(Balance("grind_center_glide", "Center Glide", TrickCategory.Grind, 0, 120));
            lib.grinds.Add(Balance("grind_plank_slide", "Plank Slide", TrickCategory.Grind, 1, 150));
            lib.grinds.Add(Balance("grind_crossbar", "Crossbar Slide", TrickCategory.Grind, 2, 150));
            lib.grinds.Add(Balance("grind_nose_needle", "Nose Needle", TrickCategory.Grind, 3, 200));

            lib.tailManual = Balance("manual_tail_cruise", "Tail Cruise", TrickCategory.Manual, 0, 60);
            lib.noseManual = Balance("manual_nose_cruise", "Nose Cruise", TrickCategory.Manual, 1, 90);

            lib.spinNames.AddRange(new[]
            {
                "Half Cycle", "Full Cycle", "Cycle and a Half", "Double Cycle", "Double and a Half", "Triple Cycle",
            });
            return lib;
        }

        public static ScoringProfile CreateScoringProfile()
        {
            var p = ScriptableObject.CreateInstance<ScoringProfile>();
            p.name = "ScoringProfile_Default";
            return p;
        }

        public static LocationDefinition CreateHarborPlaza()
        {
            var l = ScriptableObject.CreateInstance<LocationDefinition>();
            l.name = "Location_HarborPlaza";
            return l;
        }

        public static LocationDefinition CreateNeonWarehouse()
        {
            var l = ScriptableObject.CreateInstance<LocationDefinition>();
            l.name = "Location_NeonWarehouse";
            l.id = "neon_warehouse";
            l.displayName = "Neon Warehouse";
            l.sceneName = "SkateScene_NeonWarehouse";
            l.isPlayable = false;
            l.ambience = AmbienceKind.Warehouse;
            l.skyColor = new Color(0.08f, 0.06f, 0.14f);
            l.ambientColor = new Color(0.35f, 0.25f, 0.5f);
            l.fogColor = new Color(0.12f, 0.08f, 0.2f);
            return l;
        }

        public static LocationDefinition CreateRooftopRun()
        {
            var l = ScriptableObject.CreateInstance<LocationDefinition>();
            l.name = "Location_RooftopRun";
            l.id = "rooftop_run";
            l.displayName = "Rooftop Run";
            l.sceneName = "SkateScene_RooftopRun";
            l.isPlayable = false;
            l.ambience = AmbienceKind.Rooftop;
            l.skyColor = new Color(0.45f, 0.62f, 0.9f);
            l.ambientColor = new Color(0.55f, 0.6f, 0.7f);
            l.fogColor = new Color(0.7f, 0.78f, 0.9f);
            return l;
        }

        public static ContentRegistry CreateRegistry()
        {
            var r = ScriptableObject.CreateInstance<ContentRegistry>();
            r.name = "ContentRegistry_Runtime";
            r.trickLibrary = CreateTrickLibrary();
            r.scoringProfile = CreateScoringProfile();
            r.locations.Add(CreateHarborPlaza());
            r.locations.Add(CreateNeonWarehouse());
            r.locations.Add(CreateRooftopRun());
            r.contracts.Add(CreateHarborContract());
            r.cosmetics.AddRange(CreateCosmetics());
            return r;
        }

        public static ContractDefinition CreateHarborContract()
        {
            var c = ScriptableObject.CreateInstance<ContractDefinition>();
            c.name = "Contract_HarborPlaza";
            c.locationId = "harbor_plaza";
            c.displayName = "Harbor Plaza Contract";
            c.goals.Add(new GoalDefinition("harbor_line", "Land a 10,000-point line", GoalType.ComboScore, 10000));
            c.goals.Add(new GoalDefinition("harbor_rails", "Grind three separate rails", GoalType.DistinctRails, 3));
            c.goals.Add(new GoalDefinition("harbor_fountain", "Clear the fountain gap", GoalType.ClearGap, 1, "fountain_gap"));
            return c;
        }

        /// <summary>Original cosmetic set. Price 0 items are owned from the start (one per slot).</summary>
        public static System.Collections.Generic.List<CosmeticDefinition> CreateCosmetics()
        {
            var tape = new Color(0.95f, 0.76f, 0.19f);
            var coral = new Color(1f, 0.35f, 0.3f);
            var teal = new Color(0.12f, 0.78f, 0.71f);
            var ink = new Color(0.07f, 0.075f, 0.09f);
            var cream = new Color(0.95f, 0.91f, 0.82f);
            var sand = new Color(0.78f, 0.7f, 0.56f);
            var denim = new Color(0.16f, 0.24f, 0.42f);
            var olive = new Color(0.36f, 0.4f, 0.24f);
            var grey = new Color(0.45f, 0.45f, 0.47f);

            return new System.Collections.Generic.List<CosmeticDefinition>
            {
                CosmeticDefinition.CreateRuntime("deck_harbor_tape", "Harbor Tape", CosmeticSlot.Deck, 0, tape, ink),
                CosmeticDefinition.CreateRuntime("deck_buoy_stripes", "Buoy Stripes", CosmeticSlot.Deck, 40, coral, cream, DeckPattern.Stripes),
                CosmeticDefinition.CreateRuntime("deck_split_tide", "Split Tide", CosmeticSlot.Deck, 50, teal, ink, DeckPattern.Split),
                CosmeticDefinition.CreateRuntime("deck_pier_checker", "Pier Checker", CosmeticSlot.Deck, 60, ink, cream, DeckPattern.Checker),
                CosmeticDefinition.CreateRuntime("deck_night_dots", "Night Dots", CosmeticSlot.Deck, 70, ink, teal, DeckPattern.Dots),
                CosmeticDefinition.CreateRuntime("deck_gull_chevron", "Gull Chevron", CosmeticSlot.Deck, 80, cream, coral, DeckPattern.Chevron),
                CosmeticDefinition.CreateRuntime("deck_sunset_bands", "Sunset Bands", CosmeticSlot.Deck, 120, coral, tape, DeckPattern.Bands),

                CosmeticDefinition.CreateRuntime("wheels_cream", "Cream Classics", CosmeticSlot.Wheels, 0, cream, cream),
                CosmeticDefinition.CreateRuntime("wheels_coral", "Coral Rollers", CosmeticSlot.Wheels, 20, coral, coral),
                CosmeticDefinition.CreateRuntime("wheels_ink", "Ink Blacks", CosmeticSlot.Wheels, 20, ink, ink),
                CosmeticDefinition.CreateRuntime("wheels_teal", "Teal Glow", CosmeticSlot.Wheels, 25, teal, teal),
                CosmeticDefinition.CreateRuntime("wheels_tape", "Tape Gold", CosmeticSlot.Wheels, 35, tape, tape),

                CosmeticDefinition.CreateRuntime("grip_ink", "Plain Ink", CosmeticSlot.Grip, 0, ink, ink),
                CosmeticDefinition.CreateRuntime("grip_sand", "Sand Grey", CosmeticSlot.Grip, 15, grey, grey),
                CosmeticDefinition.CreateRuntime("grip_teal", "Teal Grit", CosmeticSlot.Grip, 20, teal * 0.7f + ink * 0.3f, ink),
                CosmeticDefinition.CreateRuntime("grip_coral", "Coral Grit", CosmeticSlot.Grip, 20, coral * 0.7f + ink * 0.3f, ink),

                CosmeticDefinition.CreateRuntime("shirt_coral", "Coral Tee", CosmeticSlot.Shirt, 0, coral, cream),
                CosmeticDefinition.CreateRuntime("shirt_teal", "Teal Tee", CosmeticSlot.Shirt, 25, teal, ink),
                CosmeticDefinition.CreateRuntime("shirt_cream", "Cream Tee", CosmeticSlot.Shirt, 25, cream, coral),
                CosmeticDefinition.CreateRuntime("shirt_ink", "Ink Tee", CosmeticSlot.Shirt, 25, ink, tape),
                CosmeticDefinition.CreateRuntime("shirt_tape", "Tape Tee", CosmeticSlot.Shirt, 40, tape, ink),

                CosmeticDefinition.CreateRuntime("hat_teal", "Teal Cap", CosmeticSlot.Hat, 0, teal, ink),
                CosmeticDefinition.CreateRuntime("hat_none", "No Cap", CosmeticSlot.Hat, 0, ink, ink, DeckPattern.Solid, true),
                CosmeticDefinition.CreateRuntime("hat_coral", "Coral Cap", CosmeticSlot.Hat, 20, coral, cream),
                CosmeticDefinition.CreateRuntime("hat_ink", "Ink Cap", CosmeticSlot.Hat, 20, ink, tape),
                CosmeticDefinition.CreateRuntime("hat_cream", "Cream Cap", CosmeticSlot.Hat, 20, cream, teal),

                CosmeticDefinition.CreateRuntime("palette_dock_denim", "Dock Denim", CosmeticSlot.Palette, 0, denim, cream),
                CosmeticDefinition.CreateRuntime("palette_ink_cargo", "Ink Cargo", CosmeticSlot.Palette, 30, ink, coral),
                CosmeticDefinition.CreateRuntime("palette_sand_chino", "Sand Chinos", CosmeticSlot.Palette, 30, sand, teal),
                CosmeticDefinition.CreateRuntime("palette_harbor_olive", "Harbor Olive", CosmeticSlot.Palette, 40, olive, tape),
            };
        }

        private static TrickDefinition Flip(string id, string name, int v, int value, float dur, float roll = 0f, float yaw = 0f)
        {
            var t = TrickDefinition.CreateRuntime(id, name, TrickCategory.BoardFlip, TrickFamily.Flip, v, value, dur);
            t.rollTurns = roll;
            t.yawDegrees = yaw;
            return t;
        }

        private static TrickDefinition Grab(string id, string name, int v, int value, float dur, Vector3 tilt)
        {
            var t = TrickDefinition.CreateRuntime(id, name, TrickCategory.Grab, TrickFamily.Grab, v, value, dur);
            t.grabTilt = tilt;
            return t;
        }

        private static TrickDefinition Shove(string id, string name, int v, int value, float dur, float yaw, float roll = 0f)
        {
            var t = TrickDefinition.CreateRuntime(id, name, TrickCategory.BoardFlip, TrickFamily.Shove, v, value, dur);
            t.yawDegrees = yaw;
            t.rollTurns = roll;
            return t;
        }

        private static TrickDefinition Balance(string id, string name, TrickCategory cat, int v, int value)
        {
            return TrickDefinition.CreateRuntime(id, name, cat, TrickFamily.Flip, v, value, 0f);
        }
    }
}

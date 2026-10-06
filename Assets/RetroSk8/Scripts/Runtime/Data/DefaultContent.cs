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
            l.isPlayable = true;
            l.ambience = AmbienceKind.Warehouse;
            l.skyColor = new Color(0.08f, 0.06f, 0.14f);
            l.ambientColor = new Color(0.5f, 0.42f, 0.66f); // bright enough to read ramps indoors
            l.fogColor = new Color(0.12f, 0.08f, 0.2f);
            l.fogDensity = 0.004f;
            l.sunColor = new Color(0.62f, 0.7f, 1f); // cool skylight through the roof trusses
            l.sunEuler = new Vector3(62f, 20f, 0f);
            return l;
        }

        public static LocationDefinition CreateRooftopRun()
        {
            var l = ScriptableObject.CreateInstance<LocationDefinition>();
            l.name = "Location_RooftopRun";
            l.id = "rooftop_run";
            l.displayName = "Rooftop Run";
            l.sceneName = "SkateScene_RooftopRun";
            l.isPlayable = true;
            l.ambience = AmbienceKind.Rooftop;
            l.skyColor = new Color(0.45f, 0.62f, 0.9f);
            l.ambientColor = new Color(0.55f, 0.6f, 0.7f);
            l.fogColor = new Color(0.7f, 0.78f, 0.9f);
            l.fogDensity = 0.0035f; // keep the skyline visible
            l.sunColor = new Color(1f, 0.96f, 0.9f);
            l.sunEuler = new Vector3(48f, 140f, 0f);
            return l;
        }

        public static LocationDefinition CreateSunsetBowls()
        {
            var l = ScriptableObject.CreateInstance<LocationDefinition>();
            l.name = "Location_SunsetBowls";
            l.id = "sunset_bowls";
            l.displayName = "Sunset Bowls";
            l.sceneName = "SkateScene_SunsetBowls";
            l.isPlayable = true;
            l.ambience = AmbienceKind.Bowls;
            l.skyColor = new Color(1f, 0.55f, 0.42f);
            l.ambientColor = new Color(0.62f, 0.5f, 0.5f);
            l.fogColor = new Color(1f, 0.62f, 0.5f);
            l.fogDensity = 0.005f;
            l.sunColor = new Color(1f, 0.78f, 0.55f);
            l.sunEuler = new Vector3(18f, -60f, 0f); // low golden-hour sun, long shadows
            return l;
        }

        /// <summary>Floodgate Ditch (Phase 13): a concrete drainage channel at dusk, dropped into from the dam.</summary>
        public static LocationDefinition CreateFloodgateDitch()
        {
            var l = ScriptableObject.CreateInstance<LocationDefinition>();
            l.name = "Location_FloodgateDitch";
            l.id = "floodgate_ditch";
            l.displayName = "Floodgate Ditch";
            l.sceneName = "SkateScene_FloodgateDitch";
            l.isPlayable = true;
            l.ambience = AmbienceKind.Ditch;
            l.skyColor = new Color(0.36f, 0.42f, 0.6f);
            l.ambientColor = new Color(0.55f, 0.56f, 0.64f);
            l.fogColor = new Color(0.45f, 0.48f, 0.62f);
            l.fogDensity = 0.006f;
            l.sunColor = new Color(0.95f, 0.74f, 0.6f);
            l.sunEuler = new Vector3(14f, 120f, 0f); // dusk: low and warm across the channel
            return l;
        }

        public static ContractDefinition CreateDitchContract()
        {
            var c = ScriptableObject.CreateInstance<ContractDefinition>();
            c.name = "Contract_FloodgateDitch";
            c.locationId = "floodgate_ditch";
            c.displayName = "Floodgate Ditch Contract";
            c.goals.Add(new GoalDefinition("ditch_transfer", "Transfer bank to bank across the ditch", GoalType.ClearGap, 1, "ditch_transfer"));
            c.goals.Add(new GoalDefinition("ditch_outlet", "Clear the outlet gap", GoalType.ClearGap, 1, "outlet_gap"));
            c.goals.Add(new GoalDefinition("ditch_line", "Bank a 14,000-point combo", GoalType.ComboScore, 14000));
            return c;
        }

        /// <summary>Moonlight Pier (Phase 15): a seaside boardwalk and wooden pier at night.</summary>
        public static LocationDefinition CreateMoonlightPier()
        {
            var l = ScriptableObject.CreateInstance<LocationDefinition>();
            l.name = "Location_MoonlightPier";
            l.id = "moonlight_pier";
            l.displayName = "Moonlight Pier";
            l.sceneName = "SkateScene_MoonlightPier";
            l.isPlayable = true;
            l.ambience = AmbienceKind.Pier;
            l.skyColor = new Color(0.12f, 0.14f, 0.3f);
            l.ambientColor = new Color(0.42f, 0.44f, 0.6f);
            l.fogColor = new Color(0.16f, 0.2f, 0.36f);
            l.fogDensity = 0.007f;
            l.sunColor = new Color(0.7f, 0.78f, 1f);  // moonlight: cool and soft
            l.sunEuler = new Vector3(32f, 200f, 0f);
            return l;
        }

        public static ContractDefinition CreatePierContract()
        {
            var c = ScriptableObject.CreateInstance<ContractDefinition>();
            c.name = "Contract_MoonlightPier";
            c.locationId = "moonlight_pier";
            c.displayName = "Moonlight Pier Contract";
            c.goals.Add(new GoalDefinition("pier_plank", "Clear the Plank Gap", GoalType.ClearGap, 1, "plank_gap"));
            c.goals.Add(new GoalDefinition("pier_end", "Get air off the pier-end quarter", GoalType.ClearGap, 1, "pier_end_air"));
            c.goals.Add(new GoalDefinition("pier_line", "Bank a 15,000-point combo", GoalType.ComboScore, 15000));
            return c;
        }

        /// <summary>Twin Screen Drive-In (Phase 18): an old drive-in movie lot at night, full of things to bonk.</summary>
        public static LocationDefinition CreateDriveIn()
        {
            var l = ScriptableObject.CreateInstance<LocationDefinition>();
            l.name = "Location_DriveIn";
            l.id = "drive_in";
            l.displayName = "Twin Screen Drive-In";
            l.sceneName = "SkateScene_DriveIn";
            l.isPlayable = true;
            l.ambience = AmbienceKind.DriveIn;
            l.skyColor = new Color(0.1f, 0.08f, 0.2f);
            l.ambientColor = new Color(0.46f, 0.4f, 0.58f);
            l.fogColor = new Color(0.14f, 0.11f, 0.26f);
            l.fogDensity = 0.006f;
            l.sunColor = new Color(0.86f, 0.8f, 1f);  // the screens' glow
            l.sunEuler = new Vector3(38f, 160f, 0f);
            return l;
        }

        public static ContractDefinition CreateDriveInContract()
        {
            var c = ScriptableObject.CreateInstance<ContractDefinition>();
            c.name = "Contract_DriveIn";
            c.locationId = "drive_in";
            c.displayName = "Twin Screen Drive-In Contract";
            c.goals.Add(new GoalDefinition("drivein_car", "Clear the Car Hop", GoalType.ClearGap, 1, "car_hop"));
            c.goals.Add(new GoalDefinition("drivein_screen", "Get air off the Intermission quarter", GoalType.ClearGap, 1, "intermission_air"));
            c.goals.Add(new GoalDefinition("drivein_line", "Bank a 6-trick combo", GoalType.ComboTrickCount, 6));
            return c;
        }

        /// <summary>Off-Season Rink (Phase 21): a town ice arena in summer, the ice gone and the boards left to skate.</summary>
        public static LocationDefinition CreateOffseasonRink()
        {
            var l = ScriptableObject.CreateInstance<LocationDefinition>();
            l.name = "Location_OffseasonRink";
            l.id = "offseason_rink";
            l.displayName = "Off-Season Rink";
            l.sceneName = "SkateScene_OffseasonRink";
            l.isPlayable = true;
            l.ambience = AmbienceKind.Rink;
            l.skyColor = new Color(0.12f, 0.14f, 0.2f);
            l.ambientColor = new Color(0.62f, 0.66f, 0.74f);
            l.fogColor = new Color(0.2f, 0.23f, 0.3f);
            l.fogDensity = 0.005f;
            l.sunColor = new Color(0.92f, 0.96f, 1f);  // the arena lights
            l.sunEuler = new Vector3(62f, 20f, 0f);
            return l;
        }

        public static ContractDefinition CreateRinkContract()
        {
            var c = ScriptableObject.CreateInstance<ContractDefinition>();
            c.name = "Contract_OffseasonRink";
            c.locationId = "offseason_rink";
            c.displayName = "Off-Season Rink Contract";
            c.goals.Add(new GoalDefinition("rink_boards", "Clear the Boards Hop", GoalType.ClearGap, 1, "boards_hop"));
            c.goals.Add(new GoalDefinition("rink_bleachers", "Clear the Bleacher Set", GoalType.ClearGap, 1, "bleacher_set"));
            c.goals.Add(new GoalDefinition("rink_line", "Bank a 20,000-point combo", GoalType.ComboScore, 20000));
            return c;
        }

        /// <summary>Shipyard (Phase 24): a dockyard at dawn — containers, a quay and a gantry crane.</summary>
        public static LocationDefinition CreateShipyard()
        {
            var l = ScriptableObject.CreateInstance<LocationDefinition>();
            l.name = "Location_Shipyard";
            l.id = "shipyard";
            l.displayName = "Shipyard";
            l.sceneName = "SkateScene_Shipyard";
            l.isPlayable = true;
            l.ambience = AmbienceKind.Shipyard;
            l.skyColor = new Color(0.86f, 0.62f, 0.52f);
            l.ambientColor = new Color(0.6f, 0.58f, 0.62f);
            l.fogColor = new Color(0.78f, 0.66f, 0.62f);
            l.fogDensity = 0.007f;
            l.sunColor = new Color(1f, 0.82f, 0.68f);  // low dawn sun
            l.sunEuler = new Vector3(18f, 70f, 0f);
            return l;
        }

        public static ContractDefinition CreateShipyardContract()
        {
            var c = ScriptableObject.CreateInstance<ContractDefinition>();
            c.name = "Contract_Shipyard";
            c.locationId = "shipyard";
            c.displayName = "Shipyard Contract";
            c.goals.Add(new GoalDefinition("ship_canyon", "Clear Container Canyon", GoalType.ClearGap, 1, "container_canyon"));
            c.goals.Add(new GoalDefinition("ship_gangway", "Clear the Gangway Set", GoalType.ClearGap, 1, "gangway_set"));
            c.goals.Add(new GoalDefinition("ship_line", "Bank a 25,000-point combo", GoalType.ComboScore, 25000));
            return c;
        }

        public static LocationDefinition CreateRetroCity()
        {
            var l = ScriptableObject.CreateInstance<LocationDefinition>();
            l.name = "Location_RetroCity";
            l.id = "retro_city";
            l.displayName = "Retro City";
            l.sceneName = "SkateScene_RetroCity";
            l.isPlayable = true;
            l.runDurationSeconds = 180f; // a bigger map gets a longer timed run
            l.ambience = AmbienceKind.City;
            l.skyColor = new Color(0.32f, 0.36f, 0.62f);
            l.ambientColor = new Color(0.56f, 0.52f, 0.66f);
            l.fogColor = new Color(0.42f, 0.4f, 0.62f);
            l.fogDensity = 0.004f;
            l.sunColor = new Color(1f, 0.82f, 0.72f);
            l.sunEuler = new Vector3(32f, 35f, 0f);
            return l;
        }

        public static ContractDefinition CreateSunsetContract()
        {
            var c = ScriptableObject.CreateInstance<ContractDefinition>();
            c.name = "Contract_SunsetBowls";
            c.locationId = "sunset_bowls";
            c.displayName = "Sunset Bowls Contract";
            c.goals.Add(new GoalDefinition("bowls_air", "Clear the deep end air", GoalType.ClearGap, 1, "deep_end_air"));
            c.goals.Add(new GoalDefinition("bowls_coping", "Grind three separate copings or rails", GoalType.DistinctRails, 3));
            c.goals.Add(new GoalDefinition("bowls_line", "Bank a 12,000-point combo", GoalType.ComboScore, 12000));
            return c;
        }

        public static ContractDefinition CreateCityContract()
        {
            var c = ScriptableObject.CreateInstance<ContractDefinition>();
            c.name = "Contract_RetroCity";
            c.locationId = "retro_city";
            c.displayName = "Retro City Contract";
            c.goals.Add(new GoalDefinition("city_civic", "Clear the Civic 8", GoalType.ClearGap, 1, "civic_eight"));
            c.goals.Add(new GoalDefinition("city_canal", "Jump the drained canal", GoalType.ClearGap, 1, "canal_jump"));
            c.goals.Add(new GoalDefinition("city_line", "Bank a 15,000-point combo", GoalType.ComboScore, 15000));
            return c;
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
            r.locations.Add(CreateSunsetBowls());
            r.locations.Add(CreateFloodgateDitch());
            r.locations.Add(CreateMoonlightPier());
            r.locations.Add(CreateDriveIn());
            r.locations.Add(CreateOffseasonRink());
            r.locations.Add(CreateShipyard());
            r.locations.Add(CreateRetroCity());
            r.contracts.Add(CreateHarborContract());
            r.contracts.Add(CreateNeonContract());
            r.contracts.Add(CreateRooftopContract());
            r.contracts.Add(CreateSunsetContract());
            r.contracts.Add(CreateCityContract());
            r.contracts.Add(CreateDitchContract());
            r.contracts.Add(CreatePierContract());
            r.contracts.Add(CreateDriveInContract());
            r.contracts.Add(CreateRinkContract());
            r.contracts.Add(CreateShipyardContract());
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

        public static ContractDefinition CreateNeonContract()
        {
            var c = ScriptableObject.CreateInstance<ContractDefinition>();
            c.name = "Contract_NeonWarehouse";
            c.locationId = "neon_warehouse";
            c.displayName = "Neon Warehouse Contract";
            c.goals.Add(new GoalDefinition("neon_conveyor", "Clear the conveyor gap", GoalType.ClearGap, 1, "conveyor_gap"));
            c.goals.Add(new GoalDefinition("neon_line", "Bank a 15,000-point combo", GoalType.ComboScore, 15000));
            c.goals.Add(new GoalDefinition("neon_manual", "Hold a manual for 4 seconds", GoalType.ManualSeconds, 4));
            return c;
        }

        public static ContractDefinition CreateRooftopContract()
        {
            var c = ScriptableObject.CreateInstance<ContractDefinition>();
            c.name = "Contract_RooftopRun";
            c.locationId = "rooftop_run";
            c.displayName = "Rooftop Run Contract";
            c.goals.Add(new GoalDefinition("rooftop_gap", "Clear the rooftop gap", GoalType.ClearGap, 1, "rooftop_gap"));
            c.goals.Add(new GoalDefinition("rooftop_rails", "Grind three separate rails", GoalType.DistinctRails, 3));
            c.goals.Add(new GoalDefinition("rooftop_540", "Land a 540 spin", GoalType.SpinHalfTurns, 3));
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
            var violet = new Color(0.55f, 0.3f, 0.95f);
            var lime = new Color(0.62f, 0.95f, 0.25f);
            var rust = new Color(0.66f, 0.3f, 0.16f);
            var clay = new Color(0.8f, 0.5f, 0.36f);
            var pink = new Color(1f, 0.25f, 0.62f);
            var cyan = new Color(0.15f, 0.85f, 0.95f);

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

                // Phase 23 drop: rink and drive-in colours for tokens, and two shirts you can only win in the story.
                CosmeticDefinition.CreateRuntime("deck_rink_lines", "Rink Lines", CosmeticSlot.Deck, 70, cyan, cream, DeckPattern.Stripes),
                CosmeticDefinition.CreateRuntime("deck_frost_split", "Frost Split", CosmeticSlot.Deck, 60, cream, cyan, DeckPattern.Split),
                CosmeticDefinition.CreateRuntime("deck_marquee", "Marquee Bands", CosmeticSlot.Deck, 90, pink, tape, DeckPattern.Bands),
                CosmeticDefinition.CreateRuntime("wheels_ice", "Ice Blue", CosmeticSlot.Wheels, 25, cyan, cyan),
                CosmeticDefinition.CreateRuntime("wheels_marquee", "Marquee Pink", CosmeticSlot.Wheels, 30, pink, pink),
                CosmeticDefinition.CreateRuntime("grip_rink", "Rink Grip", CosmeticSlot.Grip, 20, cyan * 0.4f + ink * 0.6f, ink),
                CosmeticDefinition.CreateRuntime("hat_frost", "Frost Cap", CosmeticSlot.Hat, 25, cream, cyan),
                CosmeticDefinition.CreateRuntime("palette_rink_sweats", "Rink Sweats", CosmeticSlot.Palette, 35, grey, cyan),
                CosmeticDefinition.CreateRuntime("shirt_rink_rats", "Rink Rats Jersey", CosmeticSlot.Shirt, 0, teal, cream, DeckPattern.Solid, false, null, "s8_frost"),
                CosmeticDefinition.CreateRuntime("shirt_deckhand", "Deckhand Jacket", CosmeticSlot.Shirt, 0, tape, ink, DeckPattern.Solid, false, null, "s9_anchor"),
                CosmeticDefinition.CreateRuntime("shirt_projectionist", "Projectionist Tee", CosmeticSlot.Shirt, 0, ink, tape, DeckPattern.Solid, false, null, "s7_reel"),
                // Phase 26: the story finale's reward, signed by every crew.
                CosmeticDefinition.CreateRuntime("shirt_all_city", "All-City Hoodie", CosmeticSlot.Shirt, 0, violet, tape, DeckPattern.Solid, false, null, Story.FinalStepId),

                // App Store cosmetic packs (RetroSk8.Core.Shop.Packs): looks only.
                CosmeticDefinition.CreateRuntime("deck_graveyard_shift", "Graveyard Shift", CosmeticSlot.Deck, 0, violet, ink, DeckPattern.Chevron, false, "night_shift"),
                CosmeticDefinition.CreateRuntime("wheels_uv_glow", "UV Glow", CosmeticSlot.Wheels, 0, violet, violet, DeckPattern.Solid, false, "night_shift"),
                CosmeticDefinition.CreateRuntime("grip_static", "Static Grip", CosmeticSlot.Grip, 0, violet * 0.5f + ink * 0.5f, ink, DeckPattern.Solid, false, "night_shift"),
                CosmeticDefinition.CreateRuntime("shirt_night_shift", "Night Shift Tee", CosmeticSlot.Shirt, 0, violet, lime, DeckPattern.Solid, false, "night_shift"),
                CosmeticDefinition.CreateRuntime("hat_lamp", "Lamp Cap", CosmeticSlot.Hat, 0, lime, ink, DeckPattern.Solid, false, "night_shift"),
                CosmeticDefinition.CreateRuntime("palette_midnight_cargo", "Midnight Cargo", CosmeticSlot.Palette, 0, ink, violet, DeckPattern.Solid, false, "night_shift"),

                CosmeticDefinition.CreateRuntime("deck_mesa_bands", "Mesa Bands", CosmeticSlot.Deck, 0, rust, sand, DeckPattern.Bands, false, "desert_heat"),
                CosmeticDefinition.CreateRuntime("wheels_sun_baked", "Sun-Baked", CosmeticSlot.Wheels, 0, sand, sand, DeckPattern.Solid, false, "desert_heat"),
                CosmeticDefinition.CreateRuntime("grip_dune", "Dune Grip", CosmeticSlot.Grip, 0, sand * 0.6f + ink * 0.4f, ink, DeckPattern.Solid, false, "desert_heat"),
                CosmeticDefinition.CreateRuntime("shirt_dust", "Dust Tee", CosmeticSlot.Shirt, 0, clay, cream, DeckPattern.Solid, false, "desert_heat"),
                CosmeticDefinition.CreateRuntime("hat_canyon", "Canyon Cap", CosmeticSlot.Hat, 0, rust, sand, DeckPattern.Solid, false, "desert_heat"),
                CosmeticDefinition.CreateRuntime("palette_clay_chino", "Clay Chinos", CosmeticSlot.Palette, 0, clay, rust, DeckPattern.Solid, false, "desert_heat"),

                CosmeticDefinition.CreateRuntime("deck_pixel_checker", "Pixel Checker", CosmeticSlot.Deck, 0, pink, cyan, DeckPattern.Checker, false, "arcade"),
                CosmeticDefinition.CreateRuntime("wheels_coin_op", "Coin-Op Pink", CosmeticSlot.Wheels, 0, pink, pink, DeckPattern.Solid, false, "arcade"),
                CosmeticDefinition.CreateRuntime("grip_scanline", "Scanline Grip", CosmeticSlot.Grip, 0, cyan * 0.4f + ink * 0.6f, ink, DeckPattern.Solid, false, "arcade"),
                CosmeticDefinition.CreateRuntime("shirt_high_score", "High Score Tee", CosmeticSlot.Shirt, 0, cyan, pink, DeckPattern.Solid, false, "arcade"),
                CosmeticDefinition.CreateRuntime("hat_joystick", "Joystick Cap", CosmeticSlot.Hat, 0, pink, cyan, DeckPattern.Solid, false, "arcade"),
                CosmeticDefinition.CreateRuntime("palette_neon_denim", "Neon Denim", CosmeticSlot.Palette, 0, denim, pink, DeckPattern.Solid, false, "arcade"),
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

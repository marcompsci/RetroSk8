using System.Collections.Generic;
using UnityEngine;

namespace RetroSk8.Level
{
    /// <summary>
    /// Off-Season Rink (Phase 21): an original town ice arena in summer, with the ice melted off and the concrete floor
    /// left bare. The rink boards are long ledges with doors in them, and each end has a quarter pipe. Bleachers are
    /// stair sets with handrails and a hubba. There are goal frames, cones and a parked ice-resurfacing cart to bonk,
    /// and a kicker that launches over the boards into the bench area. Everything here is invented.
    /// </summary>
    public sealed class OffseasonRinkBuilder : ParkBuilder
    {
        public const string GeneratedRootName = "OffseasonRink_Generated";
        protected override string RootName => GeneratedRootName;
        public override string LocationId => ParkCatalog.OffseasonRink;

        /// <summary>The rink: boards at x = ±HalfWidth and z = ±HalfLength.</summary>
        public const float HalfWidth = 15f, HalfLength = 30f;
        /// <summary>Doors in the side boards (|z| &lt; DoorHalf) to roll in and out of the rink.</summary>
        public const float DoorHalf = 4f;
        public const float BoardHeight = 1.1f;
        public const int BleacherSteps = 10;
        /// <summary>The bottom of each bleacher set (x, both sides).</summary>
        public const float BleacherBottomX = 22f;
        public const float KickerZ = 12f;

        private static readonly Color Floor = new Color(0.62f, 0.66f, 0.7f);
        private static readonly Color Boards = new Color(0.92f, 0.93f, 0.95f);
        private static readonly Color Seats = new Color(0.16f, 0.32f, 0.62f);
        private static readonly Color Arena = new Color(0.2f, 0.22f, 0.28f);
        private static readonly Color LineRed = new Color(0.86f, 0.18f, 0.2f);
        private static readonly Color LineBlue = new Color(0.16f, 0.36f, 0.86f);

        protected override void BuildPark(LevelInfo level)
        {
            // The arena: a concrete floor, walls all round and a dark roof line.
            Box("Floor", new Vector3(0f, -0.5f, 0f), new Vector3(96f, 1f, 100f), Floor);
            Box("Wall_W", new Vector3(-48.5f, 5f, 0f), new Vector3(1f, 10f, 100f), Arena);
            Box("Wall_E", new Vector3(48.5f, 5f, 0f), new Vector3(1f, 10f, 100f), Arena);
            Box("Wall_S", new Vector3(0f, 5f, -50.5f), new Vector3(98f, 10f, 1f), Arena);
            Box("Wall_N", new Vector3(0f, 5f, 50.5f), new Vector3(98f, 10f, 1f), Arena);

            // Rink paint (glowing, no colliders): centre line, two blue lines, the centre circle and faceoff spots.
            Neon("Line_Centre", new Vector3(0f, 0.02f, 0f), new Vector3(HalfWidth * 2f - 0.6f, 0.03f, 0.35f), LineRed);
            foreach (float z in new[] { -9f, 9f }) Neon("Line_Blue", new Vector3(0f, 0.02f, z), new Vector3(HalfWidth * 2f - 0.6f, 0.03f, 0.35f), LineBlue);
            for (int i = 0; i < 16; i++)
            {
                float a = i * Mathf.PI * 2f / 16f;
                Neon("Circle", new Vector3(Mathf.Cos(a) * 4.5f, 0.02f, Mathf.Sin(a) * 4.5f), new Vector3(1.8f, 0.03f, 0.25f), LineBlue, -a * Mathf.Rad2Deg + 90f);
            }
            foreach (float x in new[] { -7f, 7f })
                foreach (float z in new[] { -19f, 19f })
                    Neon("Faceoff_Spot", new Vector3(x, 0.02f, z), new Vector3(0.7f, 0.03f, 0.7f), LineRed);

            // The boards: long ledges (grind either edge), with a door in the middle of each side.
            Ledge("Boards_W_S", new Vector3(-HalfWidth, 0f, -HalfLength), new Vector3(-HalfWidth, 0f, -DoorHalf), BoardHeight, 0.3f, Boards);
            Ledge("Boards_W_N", new Vector3(-HalfWidth, 0f, DoorHalf), new Vector3(-HalfWidth, 0f, HalfLength), BoardHeight, 0.3f, Boards);
            Ledge("Boards_E_S", new Vector3(HalfWidth, 0f, -HalfLength), new Vector3(HalfWidth, 0f, -DoorHalf), BoardHeight, 0.3f, Boards);
            Ledge("Boards_E_N", new Vector3(HalfWidth, 0f, DoorHalf), new Vector3(HalfWidth, 0f, HalfLength), BoardHeight, 0.3f, Boards);
            Ledge("Boards_S", new Vector3(-HalfWidth, 0f, -HalfLength), new Vector3(HalfWidth, 0f, -HalfLength), BoardHeight, 0.3f, Boards);
            Ledge("Boards_N", new Vector3(-HalfWidth, 0f, HalfLength), new Vector3(HalfWidth, 0f, HalfLength), BoardHeight, 0.3f, Boards);
            foreach (float x in new[] { -HalfWidth, HalfWidth })
                Neon("Kickplate", new Vector3(x, 0.12f, 0f), new Vector3(0.32f, 0.2f, HalfLength * 2f), Palette.TapeYellow);

            // A quarter pipe across each end of the rink.
            float toe = HalfLength - 4.4f;
            Quarter("End_N_QP", new Vector3(0f, 0f, toe), 0f, 22f, 2.4f, 1.6f);
            Quarter("End_S_QP", new Vector3(0f, 0f, -toe), 180f, 22f, 2.4f, 1.6f);

            // Goal frames in front of each quarter (bonkable) and a manual pad on centre ice.
            foreach (float z in new[] { -21f, 21f }) GoalFrame("Goal", new Vector3(0f, 0f, z), z > 0f ? 0f : 180f);
            ManualPad("CentreIce_Pad", new Vector3(0f, 0f, 0f), new Vector2(3f, 6f), 0f, Palette.Concrete);

            // The Boards Hop: a kicker inside the rink that launches west over the boards into the bench area.
            Kicker("BoardsHop_Kicker", new Vector3(-HalfWidth + 7.5f, 0f, KickerZ), -90f, 3f, 2.5f, 0.9f);

            // Team benches and penalty boxes just outside the doors.
            foreach (float side in new[] { -1f, 1f })
            {
                float x = side * (HalfWidth + 2.5f);
                // Benches sit north and south of the doors, clear of the Boards Hop landing (z around KickerZ).
                Bench("TeamBench", new Vector3(x, 0f, -17f), 0f, 5f);
                Bench("TeamBench", new Vector3(x, 0f, 19f), 0f, 5f);
                foreach (float z in new[] { -6.5f, 6.5f })
                {
                    var box = Box("PenaltyBox", new Vector3(x + side * 0.6f, 0.6f, z), new Vector3(1.4f, 1.2f, 2.6f), Seats);
                    Bonkable(box);
                }
            }

            // Bleachers each side: a stair set down toward the rink from a deck, with handrails and a hubba.
            foreach (float side in new[] { -1f, 1f })
            {
                string s = side < 0f ? "W" : "E";
                var down = new Vector3(-side, 0f, 0f);
                float h = BleacherSteps * StepRise, run = BleacherSteps * StepRun;
                Vector3 top = Stairs("Bleachers_" + s, new Vector3(side * BleacherBottomX, 0f, 0f), down, BleacherSteps, 18f, Seats);
                float deckFrom = BleacherBottomX + run, deckTo = 46f;
                Box("Bleacher_Deck_" + s, new Vector3(side * (deckFrom + deckTo) * 0.5f, h * 0.5f, 0f), new Vector3(deckTo - deckFrom, h, 18f), Palette.ConcreteDark);
                Handrail("Bleacher_Rail_" + s, top, down, BleacherSteps, 3.5f);
                Handrail("Bleacher_Rail2_" + s, top, down, BleacherSteps, -3.5f);
                Hubba("Bleacher_Hubba_" + s, top, down, BleacherSteps, 9.4f, Palette.Concrete);
                Neon("Bleacher_Trim_" + s, new Vector3(side * deckFrom, h + 0.05f, 0f), new Vector3(0.15f, 0.1f, 18f), Palette.TapeYellow);
            }

            // The ice-resurfacing cart, parked in the south-west corner (bonk it or ollie it), and its garage door.
            var cart = Box("Resurfacer", new Vector3(-30f, 0.8f, -38f), new Vector3(2.2f, 1.2f, 4f), Palette.Teal);
            var seat = Box("Resurfacer_Cab", new Vector3(-30f, 1.8f, -39.2f), new Vector3(1.6f, 0.8f, 1.4f), Palette.Cream);
            RemoveCollider(seat);
            foreach (float fx in new[] { -1.05f, 1.05f })
                foreach (float fz in new[] { -39.4f, -36.6f })
                {
                    var w = Cylinder("Resurfacer_Wheel", new Vector3(-30f + fx, 0.3f, fz), 0.3f, 0.2f, Palette.Rubber, false);
                    w.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
                }
            Bonkable(cart);
            Box("Garage_Door", new Vector3(-30f, 3f, -49.9f), new Vector3(8f, 6f, 0.2f), Palette.Metal);
            Kicker("Garage_Bank", new Vector3(-22f, 0f, -44f), 180f, 6f, 3f, 1.4f);

            // North-east: a flat rail and a funbox by the snack window; cones to bonk round the floor.
            var rail = new List<Vector3> { new Vector3(34f, 0.5f, 30f), new Vector3(34f, 0.5f, 44f) };
            Rail("Flat_Rail", rail, GrindSurface.Rail, false, _root);
            Bar(rail[0], rail[1], 0.05f, Palette.Metal, true);
            Funbox("Funbox", new Vector3(-32f, 0f, 36f), 3f, 2.5f, 0.9f, Palette.Concrete, true);
            Building("Snack_Window", new Vector3(30f, 0f, -42f), new Vector3(10f, 3.5f, 6f), Palette.Brick, false);
            Neon("Snack_Stripe", new Vector3(30f, 2.8f, -38.95f), new Vector3(8f, 0.25f, 0.1f), Palette.NeonCyan);
            foreach (var p in new[] { new Vector3(-6f, 0f, -12f), new Vector3(6f, 0f, -12f), new Vector3(-3f, 0f, 18f), new Vector3(6f, 0f, 12f), new Vector3(36f, 0f, 22f), new Vector3(-36f, 0f, -22f) })
                Cone("Cone", p);

            // The scoreboard hanging over centre ice: plain glowing panels, no text.
            RemoveCollider(Box("Scoreboard", new Vector3(0f, 13f, 0f), new Vector3(6f, 3f, 6f), Palette.Ink));
            foreach (float yaw in new[] { 0f, 90f, 180f, 270f })
            {
                var rot = Quaternion.Euler(0f, yaw, 0f);
                Neon("Scoreboard_Panel", new Vector3(0f, 13.4f, 0f) + rot * new Vector3(0f, 0f, 3.05f), new Vector3(4.6f, 1.4f, 0.08f), yaw % 180f == 0f ? LineRed : Palette.TapeYellow, yaw);
            }
            // Arena lights in rows under the roof.
            for (int i = -2; i <= 2; i++)
                foreach (float x in new[] { -24f, 0f, 24f })
                    Neon("Light", new Vector3(x, 18f, i * 18f), new Vector3(6f, 0.3f, 1.2f), Palette.Cream);

            level.gaps.Add(Gap(ParkCatalog.BoardsHop, "Boards Hop", new Vector3(-HalfWidth, BoardHeight + 1.2f, KickerZ), new Vector3(1.4f, 1.6f, 4f), 400));
            level.gaps.Add(Gap(ParkCatalog.BleacherSet, "Bleacher Set", new Vector3(BleacherBottomX + BleacherSteps * StepRun * 0.5f, BleacherSteps * StepRise + 3.3f, 0f), new Vector3(BleacherSteps * StepRun * 0.6f, 1.2f, 16f), 450)); // Phase 25: above an ollie on the steps, so you have to jump the set
            level.gaps.Add(Gap(ParkCatalog.RinkEndAir, "Rink End Air", new Vector3(0f, 4.6f, toe + 2.4f), new Vector3(20f, 2f, 2f), 400));

            KillPlane(-6f);
            level.playableBounds = new Bounds(new Vector3(0f, 10f, 0f), new Vector3(100f, 40f, 104f));
            Spawn(level, new Vector3(4f, 0.05f, -14f), Vector3.forward); // in the rink, facing centre ice (clear of the pad)
        }

        /// <summary>A quarter pipe rising away from <paramref name="toe"/> (yaw 0 rises toward +z), with a grindable coping.</summary>
        private void Quarter(string name, Vector3 toe, float yaw, float width, float radius, float deck)
        {
            var qp = MeshObject(name, ProcMesh.QuarterPipe(width, radius, deck), toe, yaw, Palette.Plywood);
            var rot = Quaternion.Euler(0f, yaw, 0f);
            float lipAlong = radius * Mathf.Sin(88f * Mathf.Deg2Rad), lipY = radius - radius * Mathf.Cos(88f * Mathf.Deg2Rad);
            var a = toe + rot * new Vector3(-width * 0.5f + 0.3f, lipY + 0.03f, lipAlong);
            var b = toe + rot * new Vector3(width * 0.5f - 0.3f, lipY + 0.03f, lipAlong);
            Rail(name + "_Coping", new List<Vector3> { a, b }, GrindSurface.Coping, false, qp.transform);
            Bar(a, b, 0.07f, Palette.Coping, false);
        }

        /// <summary>A goal frame: two posts, a crossbar and a slanted back bar, all one bonkable piece.</summary>
        private void GoalFrame(string name, Vector3 ground, float yaw)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f);
            var frame = Box(name, ground + Vector3.up * 0.6f, new Vector3(1.8f, 1.2f, 1.1f), LineRed);
            frame.transform.rotation = rot;
            frame.GetComponent<MeshRenderer>().enabled = false; // the collider; the visible frame is the bars below
            foreach (float x in new[] { -0.9f, 0.9f })
            {
                var post = Box(name + "_Post", ground + rot * new Vector3(x, 0.6f, -0.5f), new Vector3(0.1f, 1.2f, 0.1f), LineRed);
                post.transform.rotation = rot;
                RemoveCollider(post);
            }
            var bar = Box(name + "_Bar", ground + rot * new Vector3(0f, 1.2f, -0.5f), new Vector3(1.9f, 0.1f, 0.1f), LineRed);
            bar.transform.rotation = rot;
            RemoveCollider(bar);
            var net = Box(name + "_Net", ground + rot * new Vector3(0f, 0.55f, 0.05f), new Vector3(1.7f, 1.05f, 1f), Palette.Cream);
            net.transform.rotation = rot;
            RemoveCollider(net);
            Bonkable(frame);
        }
    }
}

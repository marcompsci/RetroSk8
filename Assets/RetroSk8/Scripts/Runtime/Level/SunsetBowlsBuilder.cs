using System.Collections.Generic;
using UnityEngine;

namespace RetroSk8.Level
{
    /// <summary>
    /// Sunset Bowls: an original all-transition park at golden hour. A deep end bowl and a shallow bowl on raised
    /// decks (grindable coping all round, perfect for lip tricks), a mini ramp, a volcano, a funbox with hubba edges,
    /// a flat rail and a quarter pipe wall. Sized from public skatepark guidelines: bowls 1.6-2.8 m deep,
    /// mini ramp ~1.4 m, funbox ~1 m.
    /// </summary>
    public sealed class SunsetBowlsBuilder : ParkBuilder
    {
        public const string GeneratedRootName = "SunsetBowls_Generated";
        protected override string RootName => GeneratedRootName;
        public override string LocationId => ParkCatalog.SunsetBowls;

        private static readonly Color Pool = new Color(0.86f, 0.74f, 0.6f);
        private static readonly Color Deck = new Color(0.66f, 0.55f, 0.47f);

        protected override void BuildPark(LevelInfo level)
        {
            Box("Ground", new Vector3(0f, -0.5f, 0f), new Vector3(84f, 1f, 74f), Palette.Paving);
            foreach (var (pos, size) in new[]
            {
                (new Vector3(0f, 1.5f, 37.5f), new Vector3(84f, 3f, 1f)), (new Vector3(0f, 1.5f, -37.5f), new Vector3(84f, 3f, 1f)),
                (new Vector3(42.5f, 1.5f, 0f), new Vector3(1f, 3f, 74f)), (new Vector3(-42.5f, 1.5f, 0f), new Vector3(1f, 3f, 74f)),
            })
                Box("Wall", pos, size, Palette.Coral);

            // Deep end: 2.8 m bowl on a deck, bank up from the south.
            var deep = new Vector3(-16f, 0f, 10f);
            BowlWithDeck("DeepEnd", deep, 3f, 3f, 2.8f, 9f, Pool);
            MeshObject("DeepEnd_Bank", ProcMesh.Wedge(8f, 7f, 2.8f), deep + new Vector3(0f, 0f, -16f), 0f, Palette.Concrete);

            // Shallow bowl for carving.
            var shallow = new Vector3(16f, 0f, 14f);
            BowlWithDeck("Shallow", shallow, 2f, 2.4f, 1.6f, 6f, Pool);
            MeshObject("Shallow_Bank", ProcMesh.Wedge(6f, 4.5f, 1.6f), shallow + new Vector3(0f, 0f, -10.5f), 0f, Palette.Concrete);

            MiniRamp("Mini", new Vector3(0f, 0f, -18f), 90f, 10f, 1.6f, 6f, 1.4f);

            // Volcano: a cone with a flat cap.
            var volcano = new Vector3(-28f, 0f, -18f);
            MeshObject("Volcano", ProcMesh.RingBank(1f, 4.2f, 1.4f, 0f, 24), volcano, 0f, Palette.Concrete);
            var cap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Place(cap, "Volcano_Cap", volcano + Vector3.up * 0.7f, new Vector3(2f, 0.7f, 2f), Palette.ConcreteDark);

            Funbox("Funbox", new Vector3(26f, 0f, -16f), 3f, 3f, 1f, Palette.Concrete, true);

            var a = new Vector3(4f, 0.5f, -31f);
            var b = new Vector3(18f, 0.5f, -31f);
            Rail("FlatRail", new List<Vector3> { a, b }, GrindSurface.Rail, false, _root);
            Bar(a, b, 0.05f, Palette.Metal, true);

            var qp = MeshObject("Wall_QP", ProcMesh.QuarterPipe(16f, 2.6f, 1.2f), new Vector3(0f, 0f, 31f), 0f, Palette.Concrete);
            float lipAlong = 2.6f * Mathf.Sin(88f * Mathf.Deg2Rad), lipY = 2.6f - 2.6f * Mathf.Cos(88f * Mathf.Deg2Rad);
            var ca = new Vector3(-7.7f, lipY + 0.03f, 31f + lipAlong);
            var cb = new Vector3(7.7f, lipY + 0.03f, 31f + lipAlong);
            Rail("Wall_QPCoping", new List<Vector3> { ca, cb }, GrindSurface.Coping, false, qp.transform);
            Bar(ca, cb, 0.07f, Palette.Coping, false);

            // Palm-ish trees and sunset neon for the look (visual only).
            foreach (var p in new[] { new Vector3(-38f, 0f, 30f), new Vector3(38f, 0f, 30f), new Vector3(-38f, 0f, -30f), new Vector3(38f, 0f, -30f) })
            {
                PrimitiveMeshes.CreateVisual("Trunk", PrimitiveType.Cylinder, _root, p + Vector3.up * 3f, new Vector3(0.3f, 3f, 0.3f), Palette.Wood).isStatic = true;
                PrimitiveMeshes.CreateVisual("Fronds", PrimitiveType.Sphere, _root, p + Vector3.up * 6.4f, new Vector3(3.2f, 1.2f, 3.2f), new Color(0.2f, 0.5f, 0.3f)).isStatic = true;
            }
            Neon("SunsetStripe", new Vector3(0f, 2.6f, 36.9f), new Vector3(70f, 0.15f, 0.1f), Palette.NeonPink);
            Neon("SunsetStripe", new Vector3(0f, 2.2f, 36.9f), new Vector3(60f, 0.15f, 0.1f), Palette.TapeYellow);

            level.gaps.Add(Gap(ParkCatalog.DeepEndAir, "Deep End Air", deep + new Vector3(0f, 4.4f, 0f), new Vector3(16f, 2.4f, 16f), 700));
            level.gaps.Add(Gap(ParkCatalog.VolcanoHop, "Volcano Hop", volcano + new Vector3(0f, 2.6f, 0f), new Vector3(3f, 1.6f, 3f), 600));
            level.gaps.Add(Gap(ParkCatalog.FunboxHop, "Funbox Hop", new Vector3(26f, 2.3f, -16f), new Vector3(4f, 1.6f, 4f), 450));

            KillPlane(-6f);
            level.killHeight = -3.5f;
            level.playableBounds = new Bounds(new Vector3(0f, 10f, 0f), new Vector3(90f, 40f, 80f));
            Spawn(level, new Vector3(0f, 0.05f, 2f), Vector3.back);
        }
    }
}

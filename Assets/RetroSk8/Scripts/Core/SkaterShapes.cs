using System;

namespace RetroSk8.Core
{
    /// <summary>
    /// Body-part outlines for the Phase 17 skater model. Each is a lathe profile (height, radius) in the same unit box as
    /// Unity's capsule (height -1..1, radius up to 0.5), so a part can swap its capsule for a shaped mesh without any
    /// change to the scales that outfits and Create-a-Skater already apply. Engine-free so the outlines are tested.
    /// </summary>
    public static class SkaterShapes
    {
        public sealed class Profile
        {
            public string Name;
            public float[] Y;
            public float[] R;
        }

        /// <summary>Upper arm, forearm, thigh: full at the top, tapering to the joint below.</summary>
        public static readonly Profile Limb = new Profile
        {
            Name = "Limb",
            Y = new[] { 1f, 0.95f, 0.8f, 0.4f, -0.2f, -0.8f, -0.95f, -1f },
            R = new[] { 0f, 0.3f, 0.48f, 0.47f, 0.42f, 0.36f, 0.25f, 0f },
        };

        /// <summary>Shin with a calf bulge in its upper half.</summary>
        public static readonly Profile Calf = new Profile
        {
            Name = "Calf",
            Y = new[] { 1f, 0.92f, 0.6f, 0.2f, -0.4f, -0.85f, -1f },
            R = new[] { 0f, 0.33f, 0.46f, 0.5f, 0.4f, 0.3f, 0f },
        };

        /// <summary>Chest: broad shoulders easing into the waist.</summary>
        public static readonly Profile Chest = new Profile
        {
            Name = "Chest",
            Y = new[] { 1f, 0.85f, 0.5f, 0f, -0.5f, -0.9f, -1f },
            R = new[] { 0f, 0.35f, 0.5f, 0.49f, 0.45f, 0.35f, 0f },
        };

        /// <summary>Hips and waist: rounded, widest a little below the middle.</summary>
        public static readonly Profile Hips = new Profile
        {
            Name = "Hips",
            Y = new[] { 1f, 0.8f, 0.2f, -0.4f, -0.85f, -1f },
            R = new[] { 0f, 0.42f, 0.5f, 0.48f, 0.3f, 0f },
        };

        /// <summary>Head: an egg, fuller at the cranium than the jaw.</summary>
        public static readonly Profile Head = new Profile
        {
            Name = "Head",
            Y = new[] { 1f, 0.9f, 0.6f, 0.2f, -0.3f, -0.7f, -0.92f, -1f },
            R = new[] { 0f, 0.25f, 0.43f, 0.5f, 0.47f, 0.36f, 0.2f, 0f },
        };

        public static readonly Profile[] All = { Limb, Calf, Chest, Hips, Head };

        /// <summary>Vertices a lathe makes: one ring per profile point (the poles are single points).</summary>
        public static int VertexCount(Profile p, int segments)
        {
            int n = 0;
            for (int i = 0; i < p.Y.Length; i++) n += p.R[i] <= 0f ? 1 : segments;
            return n;
        }

        /// <summary>True when an outline stays inside the capsule box and closes at both ends.</summary>
        public static bool Valid(Profile p)
        {
            if (p == null || p.Y == null || p.R == null || p.Y.Length != p.R.Length || p.Y.Length < 3) return false;
            if (p.R[0] != 0f || p.R[p.R.Length - 1] != 0f) return false;
            for (int i = 0; i < p.Y.Length; i++)
            {
                if (p.Y[i] > 1f || p.Y[i] < -1f || p.R[i] < 0f || p.R[i] > 0.5f) return false;
                if (i > 0 && p.Y[i] >= p.Y[i - 1]) return false; // top to bottom
            }
            return true;
        }
    }
}

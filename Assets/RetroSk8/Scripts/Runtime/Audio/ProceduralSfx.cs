using UnityEngine;

namespace RetroSk8.Audio
{
    public enum SfxId
    {
        RollLoop,
        GrindLoop,
        Pop,
        Land,
        LandSketchy,
        Bail,
        Bank,
        TrickWhoosh,
        UiClick,
        SpecialReady,
        GoalComplete,
        AmbienceHarbor,
        AmbienceWarehouse,
        AmbienceRooftop,
        MusicLoop,
    }

    /// <summary>
    /// Synthesises every placeholder sound at startup, so the prototype ships with zero external audio.
    /// All sounds and the music loop are generated from code written for Retro Sk8 (original by construction).
    /// </summary>
    public static class ProceduralSfx
    {
        private const int Rate = 22050;

        public static AudioClip Create(SfxId id)
        {
            switch (id)
            {
                case SfxId.RollLoop: return Loop("sfx_roll_loop", 1.2f, RollSample, 0.15f);
                case SfxId.GrindLoop: return Loop("sfx_grind_loop", 1.0f, GrindSample, 0.12f);
                case SfxId.Pop: return OneShot("sfx_pop", 0.16f, PopSample);
                case SfxId.Land: return OneShot("sfx_land", 0.28f, LandSample);
                case SfxId.LandSketchy: return OneShot("sfx_land_sketchy", 0.35f, SketchySample);
                case SfxId.Bail: return OneShot("sfx_bail", 0.6f, BailSample);
                case SfxId.Bank: return OneShot("sfx_bank", 0.42f, BankSample);
                case SfxId.TrickWhoosh: return OneShot("sfx_whoosh", 0.3f, WhooshSample);
                case SfxId.UiClick: return OneShot("sfx_ui_click", 0.05f, ClickSample);
                case SfxId.SpecialReady: return OneShot("sfx_special_ready", 0.6f, SpecialSample);
                case SfxId.GoalComplete: return OneShot("sfx_goal_complete", 0.55f, GoalSample);
                case SfxId.AmbienceHarbor: return Loop("amb_harbor", 6f, HarborSample, 0.5f);
                case SfxId.AmbienceWarehouse: return Loop("amb_warehouse", 5f, WarehouseSample, 0.5f);
                case SfxId.AmbienceRooftop: return Loop("amb_rooftop", 6f, RooftopSample, 0.5f);
                case SfxId.MusicLoop: return Music();
                default: return OneShot("sfx_silence", 0.05f, (t, s) => 0f);
            }
        }

        // ------------------------------------------------------------------ builders

        private delegate float Sampler(float t, NoiseState s);

        private sealed class NoiseState
        {
            private uint _seed = 0x9E3779B9u;
            public float Low;      // one-pole low-passed noise
            public float Brown;
            public float Band1, Band2;

            public float White()
            {
                _seed ^= _seed << 13; _seed ^= _seed >> 17; _seed ^= _seed << 5;
                return (_seed / (float)uint.MaxValue) * 2f - 1f;
            }
        }

        private static AudioClip OneShot(string name, float seconds, Sampler f)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            var s = new NoiseState();
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(f(i / (float)Rate, s), -1f, 1f);
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Generates seconds + fade, then crossfades the tail into the head for a seamless loop.</summary>
        private static AudioClip Loop(string name, float seconds, Sampler f, float fade)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            int nf = Mathf.CeilToInt(fade * Rate);
            var raw = new float[n + nf];
            var s = new NoiseState();
            for (int i = 0; i < raw.Length; i++) raw[i] = f(i / (float)Rate, s);
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = raw[i];
            for (int i = 0; i < nf; i++)
            {
                float k = i / (float)nf;
                data[i] = raw[i] * k + raw[n + i] * (1f - k);
            }
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(data[i], -1f, 1f);
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // ------------------------------------------------------------------ voices

        private static float Env(float t, float attack, float decay) => t < attack ? t / attack : Mathf.Exp(-(t - attack) / decay);
        private static float Sine(float t, float hz) => Mathf.Sin(2f * Mathf.PI * hz * t);
        private static float Square(float t, float hz) => Sine(t, hz) >= 0f ? 1f : -1f;
        private static float Tri(float t, float hz) { float p = t * hz % 1f; return 4f * Mathf.Abs(p - 0.5f) - 1f; }

        private static float RollSample(float t, NoiseState s)
        {
            s.Brown = Mathf.Clamp(s.Brown * 0.995f + s.White() * 0.05f, -1f, 1f);
            float rumble = Sine(t, 38f) * 0.25f + Sine(t, 57f) * 0.12f;
            float seams = Mathf.Pow(Mathf.Abs(Sine(t, 2.5f)), 40f) * s.White() * 0.4f; // paving joints
            return (s.Brown * 0.8f + rumble + seams) * 0.55f;
        }

        private static float GrindSample(float t, NoiseState s)
        {
            float w = s.White();
            // Two resonant band-ish filters for a metallic scrape.
            s.Band1 += (w - s.Band1) * 0.35f;
            s.Band2 += (s.Band1 - s.Band2) * 0.2f;
            float scrape = (s.Band1 - s.Band2) * 1.6f;
            float chatter = 0.6f + 0.4f * Sine(t, 17f);
            float ring = Sine(t, 2230f) * 0.08f + Sine(t, 3170f) * 0.05f;
            return (scrape * chatter + ring) * 0.6f;
        }

        private static float PopSample(float t, NoiseState s)
        {
            float click = t < 0.006f ? s.White() * 0.9f : 0f;
            float body = Sine(t, 170f - 80f * t) * Env(t, 0.002f, 0.04f);
            return click + body * 0.8f;
        }

        private static float LandSample(float t, NoiseState s)
        {
            s.Low += (s.White() - s.Low) * 0.08f;
            float thump = Sine(t, 75f - 40f * t) * Env(t, 0.003f, 0.07f);
            float slap = s.Low * Env(t, 0.001f, 0.03f) * 2f;
            return (thump + slap) * 0.85f;
        }

        private static float SketchySample(float t, NoiseState s)
        {
            float a = LandSample(t, s);
            float scuff = s.White() * Env(t, 0.02f, 0.12f) * 0.25f;
            return a * 0.8f + scuff;
        }

        private static float BailSample(float t, NoiseState s)
        {
            s.Low += (s.White() - s.Low) * 0.15f;
            float grains = Mathf.Pow(Mathf.Abs(Sine(t, 11f)), 6f);
            float thud = Sine(t, 60f) * Env(t, 0.004f, 0.12f);
            return (s.Low * grains * 1.5f + thud) * Env(t, 0.01f, 0.25f) * 0.9f;
        }

        private static float BankSample(float t, NoiseState s)
        {
            float first = Tri(t, 660f) * Env(t, 0.004f, 0.08f);
            float second = t > 0.09f ? Tri(t, 990f) * Env(t - 0.09f, 0.004f, 0.14f) : 0f;
            return (first + second) * 0.35f;
        }

        private static float WhooshSample(float t, NoiseState s)
        {
            float k = t / 0.3f;
            float cutoff = Mathf.Lerp(0.02f, 0.25f, Mathf.Sin(k * Mathf.PI));
            s.Low += (s.White() - s.Low) * cutoff;
            return s.Low * Mathf.Sin(k * Mathf.PI) * 0.7f;
        }

        private static float ClickSample(float t, NoiseState s) => Square(t, 1250f) * Env(t, 0.001f, 0.012f) * 0.25f;

        private static float SpecialSample(float t, NoiseState s)
        {
            float step = Mathf.Floor(t / 0.1f);
            float hz = 440f * Mathf.Pow(2f, (step * 4f) / 12f);
            return Square(t, hz) * Env(t % 0.1f, 0.003f, 0.06f) * 0.18f;
        }

        private static float GoalSample(float t, NoiseState s)
        {
            // Three rising triangle notes (original motif).
            float[] notes = { 523.25f, 659.25f, 987.77f };
            int i = Mathf.Min(2, (int)(t / 0.12f));
            float local = t - i * 0.12f;
            float decay = i == 2 ? 0.2f : 0.08f;
            return Tri(t, notes[i]) * Env(local, 0.003f, decay) * 0.3f;
        }

        private static float HarborSample(float t, NoiseState s)
        {
            s.Low += (s.White() - s.Low) * 0.02f;
            float swell = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * t / 6f);   // waves, one per loop
            s.Brown = Mathf.Clamp(s.Brown * 0.998f + s.White() * 0.02f, -1f, 1f);
            return (s.Low * 2.2f * swell + s.Brown * 0.3f) * 0.5f;
        }

        private static float WarehouseSample(float t, NoiseState s)
        {
            s.Low += (s.White() - s.Low) * 0.01f;
            float hum = Sine(t, 60f) * 0.12f + Sine(t, 120f) * 0.05f;       // lighting hum
            return (s.Low * 1.5f + hum) * 0.5f;
        }

        private static float RooftopSample(float t, NoiseState s)
        {
            float gust = 0.4f + 0.6f * Mathf.Pow(0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * t / 3f), 2f);
            s.Low += (s.White() - s.Low) * (0.01f + 0.04f * gust);
            return s.Low * 2f * gust * 0.5f;
        }

        /// <summary>Original 8-bar loop: square bass, triangle arpeggio, noise hats. 112 BPM, in A minor.</summary>
        private static AudioClip Music()
        {
            const float bpm = 112f;
            float beat = 60f / bpm;
            float step = beat / 4f; // 16th notes
            int steps = 16 * 8;
            int n = Mathf.CeilToInt(steps * step * Rate);
            var data = new float[n];
            var s = new NoiseState();

            // Chord roots per bar (semitones from A2): Am, F, C, G, Am, F, G, E
            int[] roots = { 0, -4, 3, -2, 0, -4, -2, -5 };
            int[] bassPattern = { 0, -1, 0, 12, -1, 0, 7, -1, 0, -1, 12, 0, -1, 7, 0, -1 }; // -1 = rest
            int[] arp = { 0, 3, 7, 12, 7, 3, 0, 3 };

            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                int st = Mathf.Min(steps - 1, (int)(t / step));
                float tin = t - st * step;
                int bar = st / 16;
                int sInBar = st % 16;
                bool minorChord = bar != 1 && bar != 2 && bar != 3 && bar != 6 && bar != 7;
                int root = roots[bar];

                float v = 0f;
                int b = bassPattern[sInBar];
                if (b >= 0) v += Square(t, Hz(45 + root + b - 12)) * Env(tin, 0.004f, 0.09f) * 0.16f;

                int a = arp[sInBar % arp.Length];
                if (!minorChord && a == 3) a = 4;
                v += Tri(t, Hz(57 + root + a + 12)) * Env(tin, 0.003f, 0.07f) * 0.12f;

                if (sInBar % 2 == 1) v += s.White() * Env(tin, 0.001f, 0.012f) * 0.08f;
                if (sInBar == 0 || sInBar == 8) v += Sine(t, 55f - 30f * tin) * Env(tin, 0.002f, 0.08f) * 0.35f;
                if (sInBar == 4 || sInBar == 12) v += s.White() * Env(tin, 0.002f, 0.05f) * 0.18f;
                data[i] = Mathf.Clamp(v, -1f, 1f);
            }
            var clip = AudioClip.Create("music_placeholder_loop", n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static float Hz(int midi) => 440f * Mathf.Pow(2f, (midi - 69) / 12f);
    }
}

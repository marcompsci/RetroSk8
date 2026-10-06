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
        RollWood,
        RollMetal,
        RollRubber,
        GrindLedge,
        AmbienceCity,
        RainLoop,
        CarHorn,
        // Phase 12
        CrowdCheer,
        CrowdGroan,
        WindLoop,
        Coin,
        Countdown,
        CountdownGo,
        // Phase 13
        AmbienceDitch,
        Whoosh,
        Fanfare,
        Firework,
        // Phase 15
        AmbiencePier,
        // Phase 18
        AmbienceDriveIn,
        Bonk,
    }

    /// <summary>
    /// Synthesises every placeholder sound at startup, so the prototype ships with zero external audio.
    /// All sounds and the music loop are generated from code written for Retro Sk8 (original by construction).
    /// </summary>
    public static class ProceduralSfx
    {
        private const int Rate = 22050;
        /// <summary>Sample rate of every generated clip (the radio renders songs at this rate off the main thread).</summary>
        public const int SampleRate = Rate;

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
                case SfxId.MusicLoop: return Music(RetroSk8.Core.MusicComposer.Menu);
                case SfxId.RollWood: return Loop("sfx_roll_wood", 1.2f, RollWoodSample, 0.15f);
                case SfxId.RollMetal: return Loop("sfx_roll_metal", 1.0f, RollMetalSample, 0.12f);
                case SfxId.RollRubber: return Loop("sfx_roll_rubber", 1.0f, RollRubberSample, 0.12f);
                case SfxId.GrindLedge: return Loop("sfx_grind_ledge", 1.0f, GrindLedgeSample, 0.12f);
                case SfxId.AmbienceCity: return Loop("amb_city", 8f, CitySample, 0.5f);
                case SfxId.RainLoop: return Loop("amb_rain", 4f, RainSample, 0.4f);
                case SfxId.CarHorn: return OneShot("sfx_horn", 0.45f, HornSample);
                case SfxId.CrowdCheer: return OneShot("sfx_crowd_cheer", 1.8f, CheerSample);
                case SfxId.CrowdGroan: return OneShot("sfx_crowd_groan", 1.3f, GroanSample);
                case SfxId.WindLoop: return Loop("sfx_wind_loop", 2.5f, WindSample, 0.3f);
                case SfxId.Coin: return OneShot("sfx_coin", 0.32f, CoinSample);
                case SfxId.Countdown: return OneShot("sfx_countdown", 0.18f, (t, s) => Square(t, 660f) * Env(t, 0.002f, 0.06f) * 0.2f);
                case SfxId.AmbienceDitch: return Loop("amb_ditch", 6f, DitchSample, 0.5f);
                case SfxId.AmbiencePier: return Loop("amb_pier", 8f, PierSample, 0.5f);
                case SfxId.AmbienceDriveIn: return Loop("amb_drivein", 8f, DriveInSample, 0.5f);
                case SfxId.Bonk: return OneShot("sfx_bonk", 0.35f, BonkSample);
                case SfxId.Whoosh: return OneShot("sfx_slowmo", 0.7f, SlowMoSample);
                case SfxId.Fanfare: return OneShot("sfx_fanfare", 1.1f, FanfareSample);
                case SfxId.Firework: return OneShot("sfx_firework", 0.9f, FireworkSample);
                case SfxId.CountdownGo: return OneShot("sfx_countdown_go", 0.4f, (t, s) => Square(t, 990f) * Env(t, 0.002f, 0.16f) * 0.22f);
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

        /// <summary>Renders an original song (see RetroSk8.Core.MusicComposer) into a looping clip.</summary>
        public static AudioClip Music(RetroSk8.Core.SongSpec spec)
        {
            var data = RetroSk8.Core.MusicComposer.Render(spec, Rate);
            var clip = AudioClip.Create(spec.Name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // Surface-specific rolling and grinding (Phase 6 sound pass).

        private static float RollWoodSample(float t, NoiseState s)
        {
            // Plywood: hollow, boomy, with a soft knock at each panel seam.
            s.Brown = Mathf.Clamp(s.Brown * 0.993f + s.White() * 0.04f, -1f, 1f);
            float body = Sine(t, 92f) * 0.22f + Sine(t, 141f) * 0.1f;
            float knock = Mathf.Pow(Mathf.Abs(Sine(t, 1.6f)), 60f) * Sine(t, 180f) * 0.6f;
            return (s.Brown * 0.6f + body * (0.7f + 0.3f * s.Brown) + knock) * 0.5f;
        }

        private static float RollMetalSample(float t, NoiseState s)
        {
            // Sheet metal / trusses: bright rattle with a ringing overtone.
            s.Band1 += (s.White() - s.Band1) * 0.5f;
            float rattle = (s.White() - s.Band1) * (0.5f + 0.5f * Mathf.Abs(Sine(t, 9f)));
            float ring = Sine(t, 1180f) * 0.05f + Sine(t, 1730f) * 0.03f;
            return (rattle * 0.5f + ring + Sine(t, 48f) * 0.15f) * 0.55f;
        }

        private static float RollRubberSample(float t, NoiseState s)
        {
            // Conveyor belt: soft, dull rumble plus the motor's hum.
            s.Low += (s.White() - s.Low) * 0.04f;
            float hum = Sine(t, 100f) * 0.12f + Sine(t, 200f) * 0.04f;
            return (s.Low * 1.4f + hum + Sine(t, 31f) * 0.18f) * 0.5f;
        }

        private static float CitySample(float t, NoiseState s)
        {
            // Distant traffic: a low rumble with slow pass-bys, plus a faint electrical hum.
            s.Low += (s.White() - s.Low) * 0.015f;
            s.Brown = Mathf.Clamp(s.Brown * 0.997f + s.White() * 0.03f, -1f, 1f);
            float passBy = 0.35f + 0.65f * Mathf.Pow(0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * t / 4f), 3f);
            return (s.Low * 2f * passBy + s.Brown * 0.25f + Sine(t, 50f) * 0.03f) * 0.5f;
        }

        private static float RainSample(float t, NoiseState s)
        {
            // Steady hiss (high-passed noise) with random droplet ticks.
            float w = s.White();
            s.Low += (w - s.Low) * 0.2f;
            float hiss = (w - s.Low) * 0.35f;
            float tick = s.White() > 0.995f ? s.White() * 0.6f : 0f;
            return hiss + tick;
        }

        private static float HornSample(float t, NoiseState s)
        {
            // Two-tone toy-car horn with a soft attack and release.
            float env = Mathf.Clamp01(t * 30f) * Mathf.Clamp01((0.45f - t) * 12f);
            float tone = Mathf.Sign(Sine(t, 370f)) * 0.5f + Mathf.Sign(Sine(t, 466f)) * 0.5f;
            return tone * env * 0.22f;
        }

        private static float GrindLedgeSample(float t, NoiseState s)
        {
            // Concrete / waxed ledge: gritty, lower and less tonal than a metal rail.
            float w = s.White();
            s.Low += (w - s.Low) * 0.18f;
            float grit = (w - s.Low) * (0.55f + 0.45f * Mathf.Abs(Sine(t, 23f)));
            return (grit * 0.9f + s.Low * 0.6f) * 0.5f;
        }

        private static float Hz(int midi) => 440f * Mathf.Pow(2f, (midi - 69) / 12f);

        // Phase 12 sound pass: a distant crowd, air, and shop sounds.

        private static float CheerSample(float t, NoiseState s)
        {
            // A far-off crowd: band-limited noise shaped by a few slowly wobbling "voice" formants, plus scattered claps.
            float w = s.White();
            s.Band1 += (w - s.Band1) * 0.3f;
            s.Band2 += (s.Band1 - s.Band2) * 0.12f;
            float voice = (s.Band1 - s.Band2);
            float vowel = 0.6f + 0.25f * Sine(t, 5.3f) + 0.15f * Sine(t, 7.9f);
            float body = Sine(t, 310f + 40f * Sine(t, 3.1f)) * 0.12f + Sine(t, 470f + 60f * Sine(t, 2.3f)) * 0.08f;
            float clap = s.White() > 0.985f ? s.White() * 0.5f : 0f;
            float env = Mathf.Clamp01(t / 0.25f) * Mathf.Clamp01((1.8f - t) / 0.9f);
            return (voice * vowel * 1.4f + body * vowel + clap * Mathf.Clamp01(t * 2f)) * env * 0.5f;
        }

        private static float GroanSample(float t, NoiseState s)
        {
            // "Ooh": low voices sliding down, softer than a cheer.
            float w = s.White();
            s.Low += (w - s.Low) * 0.08f;
            float slide = 1f - 0.25f * Mathf.Clamp01(t / 1.1f);
            float voices = Sine(t, 220f * slide) * 0.18f + Sine(t, 262f * slide) * 0.12f + Sine(t, 196f * slide) * 0.1f;
            float env = Mathf.Clamp01(t / 0.15f) * Mathf.Clamp01((1.3f - t) / 0.7f);
            return (voices + s.Low * 0.8f) * env * 0.45f;
        }

        private static float WindSample(float t, NoiseState s)
        {
            // Rushing air: low-passed noise that breathes a little.
            float gust = 0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * t / 2.5f);
            s.Low += (s.White() - s.Low) * (0.03f + 0.03f * gust);
            s.Brown = Mathf.Clamp(s.Brown * 0.996f + s.White() * 0.02f, -1f, 1f);
            return (s.Low * 2.4f + s.Brown * 0.3f) * gust * 0.5f;
        }

        private static float DitchSample(float t, NoiseState s)
        {
            // Water trickling down the channel plus a hollow concrete drone.
            float w = s.White();
            s.Low += (w - s.Low) * 0.25f;
            float trickle = (w - s.Low) * (0.25f + 0.2f * Mathf.Abs(Sine(t, 0.7f))) * (s.White() > 0.6f ? 1.4f : 0.6f);
            s.Brown = Mathf.Clamp(s.Brown * 0.997f + s.White() * 0.02f, -1f, 1f);
            float drone = Sine(t, 55f) * 0.05f + Sine(t, 82.5f) * 0.03f;
            return (trickle * 0.5f + s.Brown * 0.35f + drone) * 0.5f;
        }

        private static float PierSample(float t, NoiseState s)
        {
            // Waves rolling in under the boards (slow swells of filtered noise), a creak now and then and a far bell buoy.
            float w = s.White();
            s.Low += (w - s.Low) * 0.06f;
            float swell = 0.5f + 0.5f * Sine(t, 0.125f);  // one wave every 8 s: loops cleanly
            float wash = s.Low * (0.35f + 0.65f * swell * swell);
            s.Brown = Mathf.Clamp(s.Brown * 0.995f + w * 0.015f, -1f, 1f);
            float bellEnv = Mathf.Exp(-Mathf.Repeat(t, 4f) * 1.6f);
            float bell = (Sine(t, 392f) * 0.6f + Sine(t, 588f) * 0.3f) * bellEnv * 0.025f;
            return (wash * 0.9f + s.Brown * 0.25f + bell) * 0.5f;
        }

        private static float DriveInSample(float t, NoiseState s)
        {
            // Crickets in the grass, a projector rattling away and a far-off film soundtrack murmuring from the speakers.
            float chirpGate = Mathf.Repeat(t, 0.5f) < 0.12f && Mathf.Repeat(t, 2f) < 1.2f ? 1f : 0f;
            float cricket = Sine(t, 4300f) * (0.5f + 0.5f * Sine(t, 60f)) * chirpGate * 0.04f;
            float rattle = (Mathf.Repeat(t * 24f, 1f) < 0.25f ? 1f : 0f) * s.White() * 0.03f;
            s.Low += (s.White() - s.Low) * 0.04f;
            float murmur = s.Low * (0.5f + 0.5f * Sine(t, 0.5f)) * 0.5f + Sine(t, 196f) * 0.012f * (0.5f + 0.5f * Sine(t, 0.25f));
            return (cricket + rattle + murmur) * 0.6f;
        }

        private static float BonkSample(float t, NoiseState s)
        {
            // A hollow metal clank: two inharmonic partials over a click of noise.
            float body = (Sine(t, 523f) * 0.6f + Sine(t, 1307f) * 0.35f + Sine(t, 2215f) * 0.15f) * Mathf.Exp(-t * 14f);
            float click = s.White() * Mathf.Exp(-t * 90f) * 0.6f;
            return (body + click) * 0.45f;
        }

        private static float SlowMoSample(float t, NoiseState s)
        {
            // A deep falling whoosh for slow motion.
            float k = t / 0.7f;
            s.Low += (s.White() - s.Low) * Mathf.Lerp(0.2f, 0.02f, k);
            float sub = Sine(t, Mathf.Lerp(140f, 50f, k)) * 0.3f;
            return (s.Low * 1.4f + sub) * Mathf.Sin(Mathf.PI * Mathf.Min(1f, k)) * 0.6f;
        }

        private static float FanfareSample(float t, NoiseState s)
        {
            // Four rising notes then a held chord (original motif).
            float[] notes = { 392f, 523.25f, 659.25f, 783.99f };
            if (t < 0.48f)
            {
                int i = Mathf.Min(3, (int)(t / 0.12f));
                return Square(t, notes[i]) * Env(t - i * 0.12f, 0.004f, 0.09f) * 0.16f;
            }
            float u = t - 0.48f;
            return (Tri(t, 523.25f) + Tri(t, 659.25f) + Tri(t, 783.99f)) * Env(u, 0.01f, 0.35f) * 0.12f;
        }

        private static float FireworkSample(float t, NoiseState s)
        {
            // A soft pop then crackle.
            float pop = t < 0.05f ? s.White() * (1f - t / 0.05f) * 0.6f : 0f;
            float crackle = t > 0.12f && s.White() > 0.93f ? s.White() * Env(t - 0.12f, 0.001f, 0.35f) * 0.7f : 0f;
            return pop + crackle;
        }

        private static float CoinSample(float t, NoiseState s)
        {
            // Two quick bright blips (original).
            if (t < 0.07f) return Square(t, 988f) * Env(t, 0.002f, 0.05f) * 0.18f;
            return Square(t, 1319f) * Env(t - 0.07f, 0.002f, 0.12f) * 0.18f;
        }
    }
}

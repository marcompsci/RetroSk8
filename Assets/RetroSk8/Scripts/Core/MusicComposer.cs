using System;

namespace RetroSk8.Core
{
    public enum MusicScale
    {
        Minor = 0,
        Dorian = 1,
        Major = 2,
        Lydian = 3,
    }

    /// <summary>Everything that makes one original loop sound like itself. All values are plain data.</summary>
    public sealed class SongSpec
    {
        public string Name = "song";
        public float Bpm = 110f;
        /// <summary>MIDI note of the key's root in the bass octave (e.g. 38 = D2).</summary>
        public int RootMidi = 45;
        public MusicScale Scale = MusicScale.Minor;
        /// <summary>Chord per bar as a 0-based scale degree (0 = I, 3 = IV...). Length sets the loop length in bars.</summary>
        public int[] Progression = { 0, 5, 3, 4 };
        /// <summary>0 = straight 16ths; ~0.2 = laid-back swing.</summary>
        public float Swing;
        /// <summary>0 = half-time beat, 1 = four-on-the-floor.</summary>
        public float Drive = 0.5f;
        /// <summary>Melody level (0 mutes it).</summary>
        public float Lead = 0.5f;
        /// <summary>Soft held chords (1) versus short stabs (0).</summary>
        public float Pad = 0.5f;
        public int Seed = 1;
    }

    /// <summary>
    /// Writes Retro Sk8's music from code: drums, bass, chords and a seeded melody over a chord progression,
    /// rendered to a seamless mono loop. Every song is generated from its <see cref="SongSpec"/>, so the music is
    /// original by construction and costs nothing in the app download. Engine-free so it is unit-tested.
    /// </summary>
    public static class MusicComposer
    {
        private static readonly int[][] Scales =
        {
            new[] { 0, 2, 3, 5, 7, 8, 10 }, // minor
            new[] { 0, 2, 3, 5, 7, 9, 10 }, // dorian
            new[] { 0, 2, 4, 5, 7, 9, 11 }, // major
            new[] { 0, 2, 4, 6, 7, 9, 11 }, // lydian
        };

        // ------------------------------------------------------------------ the park songs (all original)

        public static SongSpec Menu => new SongSpec
        {
            Name = "music_menu", Bpm = 112f, RootMidi = 45, Scale = MusicScale.Minor,
            Progression = new[] { 0, 5, 2, 6, 0, 5, 6, 4 }, Swing = 0.05f, Drive = 0.4f, Lead = 0.45f, Pad = 0.3f, Seed = 11,
        };

        /// <summary>Harbor Plaza: sunny, laid-back, swung.</summary>
        public static SongSpec Harbor => new SongSpec
        {
            Name = "music_harbor", Bpm = 96f, RootMidi = 38, Scale = MusicScale.Dorian,
            Progression = new[] { 0, 3, 0, 4, 0, 3, 5, 4 }, Swing = 0.2f, Drive = 0.2f, Lead = 0.55f, Pad = 0.8f, Seed = 23,
        };

        /// <summary>Neon Warehouse: driving and dark.</summary>
        public static SongSpec Warehouse => new SongSpec
        {
            Name = "music_warehouse", Bpm = 124f, RootMidi = 40, Scale = MusicScale.Minor,
            Progression = new[] { 0, 0, 5, 6, 0, 0, 3, 4 }, Swing = 0f, Drive = 1f, Lead = 0.4f, Pad = 0.15f, Seed = 37,
        };

        /// <summary>Rooftop Run: bright and open.</summary>
        public static SongSpec Rooftop => new SongSpec
        {
            Name = "music_rooftop", Bpm = 108f, RootMidi = 41, Scale = MusicScale.Lydian,
            Progression = new[] { 0, 4, 5, 3, 0, 4, 1, 4 }, Swing = 0.1f, Drive = 0.6f, Lead = 0.6f, Pad = 0.6f, Seed = 41,
        };

        /// <summary>Retro City: head-nodding and swung, for cruising the streets.</summary>
        public static SongSpec City => new SongSpec
        {
            Name = "music_city", Bpm = 92f, RootMidi = 43, Scale = MusicScale.Dorian,
            Progression = new[] { 0, 0, 3, 3, 5, 4, 3, 4 }, Swing = 0.22f, Drive = 0.35f, Lead = 0.5f, Pad = 0.7f, Seed = 53,
        };

        /// <summary>Sunset Bowls: bright and surfy.</summary>
        public static SongSpec Bowls => new SongSpec
        {
            Name = "music_bowls", Bpm = 128f, RootMidi = 40, Scale = MusicScale.Major,
            Progression = new[] { 0, 3, 4, 3, 0, 3, 4, 4 }, Swing = 0f, Drive = 0.8f, Lead = 0.65f, Pad = 0.25f, Seed = 67,
        };

        public static float LoopSeconds(SongSpec spec) => spec.Progression.Length * 4f * 60f / spec.Bpm;
        public static int LoopSamples(SongSpec spec, int sampleRate) => (int)Math.Round(LoopSeconds(spec) * sampleRate);

        /// <summary>Renders one seamless loop (decays that spill past the end wrap round to the start).</summary>
        public static float[] Render(SongSpec spec, int sampleRate)
        {
            if (spec == null) throw new ArgumentNullException(nameof(spec));
            if (spec.Progression == null || spec.Progression.Length == 0) throw new ArgumentException("Progression is empty");

            int n = LoopSamples(spec, sampleRate);
            int tail = sampleRate; // one second of spill-over, folded back in
            var buf = new float[n + tail];
            var rng = new Rng((uint)(spec.Seed * 2654435761u) | 1u);
            int[] scale = Scales[(int)spec.Scale];

            float beat = 60f / spec.Bpm;
            float step = beat / 4f;
            int bars = spec.Progression.Length;
            var melody = ComposeMelody(spec, rng);
            var bassPattern = ComposeBass(rng);

            for (int bar = 0; bar < bars; bar++)
            {
                int degree = spec.Progression[bar];
                int[] chord = Triad(scale, degree);
                for (int s = 0; s < 16; s++)
                {
                    float swing = (s % 2 == 1) ? spec.Swing * step : 0f;
                    float t0 = (bar * 16 + s) * step + swing;
                    int start = (int)(t0 * sampleRate);

                    // Drums.
                    bool kick = spec.Drive >= 0.75f ? s % 4 == 0 : (s == 0 || s == 8 || (s == 14 && bar % 2 == 1) || (spec.Drive >= 0.4f && s == 10));
                    if (kick) AddKick(buf, start, sampleRate, 0.55f);
                    if (s == 4 || s == 12) AddSnare(buf, start, sampleRate, 0.28f, rng);
                    if (s % 2 == 0) AddHat(buf, start, sampleRate, s % 4 == 2 ? 0.07f : 0.045f, 0.012f, rng);
                    else if (spec.Drive >= 0.75f && s % 4 == 3) AddHat(buf, start, sampleRate, 0.04f, 0.05f, rng);

                    // Bass.
                    int b = bassPattern[s];
                    if (b != Rest)
                        AddTone(buf, start, sampleRate, Hz(spec.RootMidi + chord[0] + b), step * 1.6f, 0.002f, 0.12f, 0.17f, Wave.Square, 0.25f);

                    // Chords: stabs on the beat, or a pad that swells across the bar.
                    if (spec.Pad >= 0.5f && s == 0)
                    {
                        foreach (int c in chord)
                            AddTone(buf, start, sampleRate, Hz(spec.RootMidi + 24 + c), beat * 4f, beat * 0.6f, beat * 2.5f, 0.05f * spec.Pad, Wave.Triangle, 1f);
                    }
                    else if (spec.Pad < 0.5f && (s == 0 || s == 6 || s == 10))
                    {
                        foreach (int c in chord)
                            AddTone(buf, start, sampleRate, Hz(spec.RootMidi + 24 + c), step * 2f, 0.002f, 0.08f, 0.05f, Wave.Square, 0.35f);
                    }

                    // Melody.
                    int note = melody[(bar % 8) * 16 + s];
                    if (note != Rest && spec.Lead > 0f)
                        AddTone(buf, start, sampleRate, Hz(spec.RootMidi + 36 + note), step * 2.5f, 0.004f, 0.16f, 0.09f * spec.Lead, Wave.Triangle, 1f);
                }
            }

            // Fold the spill-over into the start so the loop is seamless, then soft-clip.
            var output = new float[n];
            for (int i = 0; i < n; i++) output[i] = buf[i];
            for (int i = 0; i < tail && i < n; i++) output[i] += buf[n + i];
            for (int i = 0; i < n; i++) output[i] = SoftClip(output[i] * 1.8f);
            return output;
        }

        // ------------------------------------------------------------------ composition

        private const int Rest = int.MinValue;

        private static int[] Triad(int[] scale, int degree)
        {
            int Note(int d) => scale[((d % 7) + 7) % 7] + 12 * (int)Math.Floor(d / 7f);
            return new[] { Note(degree), Note(degree + 2), Note(degree + 4) };
        }

        /// <summary>A one-bar bass rhythm in semitone offsets from the chord root (octaves and fifths).</summary>
        private static int[] ComposeBass(Rng rng)
        {
            var p = new int[16];
            for (int i = 0; i < 16; i++) p[i] = Rest;
            p[0] = 0;
            p[8] = 0;
            int[] options = { 0, 12, 7, -5 };
            for (int i = 2; i < 16; i += 2)
                if (i != 8 && rng.Next(0, 100) < 45) p[i] = options[rng.Next(0, options.Length)];
            if (rng.Next(0, 2) == 0) p[15] = 12; // pickup into the next bar
            return p;
        }

        /// <summary>
        /// Eight bars of melody as an AABA'-style form: a two-bar phrase, repeated, a contrasting phrase,
        /// and the first phrase with a new ending. Notes are scale steps that favour chord tones.
        /// </summary>
        private static int[] ComposeMelody(SongSpec spec, Rng rng)
        {
            int[] scale = Scales[(int)spec.Scale];
            var a = Phrase(spec, scale, rng, 0);
            var b = Phrase(spec, scale, rng, 4);
            var aEnd = Phrase(spec, scale, rng, 6);
            var m = new int[8 * 16];
            Copy(a, m, 0);
            Copy(a, m, 2);
            Copy(b, m, 4);
            Copy(a, m, 6);
            // New ending: replace the last bar of the final phrase.
            for (int i = 0; i < 16; i++) m[7 * 16 + i] = aEnd[16 + i];
            return m;
        }

        private static void Copy(int[] phrase, int[] into, int bar)
        {
            for (int i = 0; i < 32; i++) into[bar * 16 + i] = phrase[i];
        }

        private static int[] Phrase(SongSpec spec, int[] scale, Rng rng, int barOffset)
        {
            var p = new int[32];
            int pos = rng.Next(0, 5); // scale step around the middle of the range
            for (int i = 0; i < 32; i++)
            {
                int bar = barOffset + i / 16;
                int[] chord = Triad(scale, spec.Progression[bar % spec.Progression.Length]);
                bool strong = i % 4 == 0;
                int density = strong ? 70 : 28;
                if (rng.Next(0, 100) >= density) { p[i] = Rest; continue; }

                pos += rng.Next(-2, 3);
                if (pos < -2) pos = -2;
                if (pos > 9) pos = 9;
                int semis = scale[((pos % 7) + 7) % 7] + 12 * (int)Math.Floor(pos / 7f);
                if (strong) semis = Nearest(semis, chord); // land on chord tones on the beat
                p[i] = semis;
            }
            p[30] = Rest; // breathe at the end of the phrase
            p[31] = Rest;
            return p;
        }

        private static int Nearest(int semis, int[] chord)
        {
            int best = semis, bestDist = int.MaxValue;
            for (int oct = -12; oct <= 24; oct += 12)
                foreach (int c in chord)
                {
                    int cand = c + oct;
                    int d = Math.Abs(cand - semis);
                    if (d < bestDist) { bestDist = d; best = cand; }
                }
            return best;
        }

        // ------------------------------------------------------------------ voices

        private enum Wave { Square, Triangle }

        private static float Hz(int midi) => 440f * (float)Math.Pow(2.0, (midi - 69) / 12.0);

        private static void AddTone(float[] buf, int start, int rate, float hz, float length, float attack, float decay, float gain, Wave wave, float brightness)
        {
            int len = (int)((length + decay * 3f) * rate);
            float lp = 0f;
            float k = 0.05f + 0.9f * brightness; // one-pole low-pass amount (1 = open)
            double phase = 0.0, inc = hz / rate;
            for (int i = 0; i < len; i++)
            {
                int idx = start + i;
                if (idx < 0) continue;
                if (idx >= buf.Length) break;
                float t = i / (float)rate;
                float env = t < attack ? t / attack : (t < length ? (float)Math.Exp(-(t - attack) / decay) : (float)Math.Exp(-(length - attack) / decay) * (float)Math.Exp(-(t - length) / (decay * 0.3f)));
                phase += inc;
                if (phase >= 1.0) phase -= 1.0;
                float raw = wave == Wave.Square ? (phase < 0.5 ? 1f : -1f) : (float)(4.0 * Math.Abs(phase - 0.5) - 1.0);
                lp += (raw - lp) * k;
                buf[idx] += lp * env * gain;
            }
        }

        private static void AddKick(float[] buf, int start, int rate, float gain)
        {
            int len = (int)(0.25f * rate);
            double phase = 0.0;
            for (int i = 0; i < len && start + i < buf.Length; i++)
            {
                float t = i / (float)rate;
                float hz = 50f + 90f * (float)Math.Exp(-t / 0.03f);
                phase += hz / rate;
                buf[start + i] += (float)Math.Sin(2.0 * Math.PI * phase) * (float)Math.Exp(-t / 0.09f) * gain;
            }
        }

        private static void AddSnare(float[] buf, int start, int rate, float gain, Rng rng)
        {
            int len = (int)(0.2f * rate);
            for (int i = 0; i < len && start + i < buf.Length; i++)
            {
                float t = i / (float)rate;
                float tone = (float)Math.Sin(2.0 * Math.PI * 185.0 * t) * (float)Math.Exp(-t / 0.03f) * 0.5f;
                float noise = rng.White() * (float)Math.Exp(-t / 0.06f);
                buf[start + i] += (tone + noise) * gain;
            }
        }

        private static void AddHat(float[] buf, int start, int rate, float gain, float decay, Rng rng)
        {
            int len = (int)(decay * 5f * rate);
            float prev = 0f;
            for (int i = 0; i < len && start + i < buf.Length; i++)
            {
                float t = i / (float)rate;
                float w = rng.White();
                float hp = w - prev; // crude high-pass for a bright tick
                prev = w;
                buf[start + i] += hp * (float)Math.Exp(-t / decay) * gain;
            }
        }

        private static float SoftClip(float x) => x / (1f + Math.Abs(x));

        /// <summary>xorshift32, identical on every platform.</summary>
        private sealed class Rng
        {
            private uint _s;
            public Rng(uint seed) { _s = seed == 0 ? 1u : seed; }

            private uint NextU()
            {
                _s ^= _s << 13;
                _s ^= _s >> 17;
                _s ^= _s << 5;
                return _s;
            }

            /// <summary>Uniform integer in [min, max).</summary>
            public int Next(int min, int max) => min + (int)(NextU() % (uint)Math.Max(1, max - min));
            public float White() => NextU() / (float)uint.MaxValue * 2f - 1f;
        }
    }
}

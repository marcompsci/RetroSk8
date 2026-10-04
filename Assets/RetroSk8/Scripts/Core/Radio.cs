using System;
using System.Collections.Generic;

namespace RetroSk8.Core
{
    /// <summary>What plays while you skate: each park's own theme, the in-game radio, or nothing.</summary>
    public enum MusicMode
    {
        ParkThemes = 0,
        Radio = 1,
        Off = 2,
    }

    /// <summary>One in-game radio station: a name, a sound and a handful of original songs.</summary>
    public sealed class RadioStation
    {
        public string Id;
        public string Name;
        public string Tagline;
        public SongSpec[] Songs;
        public string[] Titles;
    }

    /// <summary>
    /// The in-game radio: three stations of songs written by <see cref="MusicComposer"/> (original by construction,
    /// nothing licensed). Each song loops long enough to feel like a track, then the station moves on in a
    /// shuffled order. Engine-free so it is unit-tested.
    /// </summary>
    public static class Radio
    {
        /// <summary>Roughly how long each song plays before the station moves on.</summary>
        public const float TargetSeconds = 80f;

        public static readonly RadioStation[] Stations =
        {
            new RadioStation
            {
                Id = "low_tide", Name = "LOW TIDE FM", Tagline = "LAID-BACK SWING FOR LONG LINES",
                Titles = new[] { "TIDE LINE", "SALT WAX", "SLOW CARVE", "GULL SEASON" },
                Songs = new[]
                {
                    Song("radio_tide_line", 90f, 38, MusicScale.Dorian, new[] { 0, 3, 0, 4, 0, 3, 5, 4 }, 0.22f, 0.25f, 0.55f, 0.8f, 101),
                    Song("radio_salt_wax", 86f, 41, MusicScale.Dorian, new[] { 0, 0, 3, 3, 5, 5, 4, 4 }, 0.25f, 0.2f, 0.5f, 0.9f, 107),
                    Song("radio_slow_carve", 94f, 36, MusicScale.Minor, new[] { 0, 5, 3, 4, 0, 5, 3, 6 }, 0.18f, 0.35f, 0.45f, 0.7f, 113),
                    Song("radio_gull_season", 98f, 43, MusicScale.Major, new[] { 0, 4, 5, 3, 0, 4, 3, 4 }, 0.2f, 0.3f, 0.6f, 0.75f, 127),
                },
            },
            new RadioStation
            {
                Id = "concrete", Name = "CONCRETE 101", Tagline = "FAST AND LOUD FOR BIG AIRS",
                Titles = new[] { "KERB STATIC", "NIGHT BUS", "FLUORESCENT", "LOADING BAY" },
                Songs = new[]
                {
                    Song("radio_kerb_static", 132f, 40, MusicScale.Minor, new[] { 0, 0, 5, 6, 0, 0, 3, 4 }, 0f, 1f, 0.4f, 0.1f, 131),
                    Song("radio_night_bus", 124f, 38, MusicScale.Minor, new[] { 0, 6, 5, 6, 0, 6, 3, 4 }, 0f, 0.9f, 0.45f, 0.2f, 137),
                    Song("radio_fluorescent", 138f, 42, MusicScale.Dorian, new[] { 0, 3, 0, 3, 5, 4, 5, 4 }, 0f, 1f, 0.5f, 0.15f, 149),
                    Song("radio_loading_bay", 128f, 37, MusicScale.Minor, new[] { 0, 0, 3, 3, 5, 5, 6, 4 }, 0.05f, 0.85f, 0.35f, 0.25f, 151),
                },
            },
            new RadioStation
            {
                Id = "sunset", Name = "SUNSET CASSETTE", Tagline = "BRIGHT TAPES FOR GOLDEN HOUR",
                Titles = new[] { "GOLDEN HOUR", "COPING SONG", "BOARDWALK", "LAST LIGHT" },
                Songs = new[]
                {
                    Song("radio_golden_hour", 112f, 41, MusicScale.Lydian, new[] { 0, 4, 5, 3, 0, 4, 1, 4 }, 0.1f, 0.6f, 0.65f, 0.6f, 163),
                    Song("radio_coping_song", 118f, 40, MusicScale.Major, new[] { 0, 3, 4, 3, 0, 3, 4, 4 }, 0.05f, 0.7f, 0.6f, 0.4f, 167),
                    Song("radio_boardwalk", 104f, 43, MusicScale.Major, new[] { 0, 5, 3, 4, 0, 5, 3, 4 }, 0.15f, 0.5f, 0.7f, 0.55f, 173),
                    Song("radio_last_light", 100f, 39, MusicScale.Lydian, new[] { 0, 1, 0, 4, 0, 1, 5, 4 }, 0.12f, 0.45f, 0.55f, 0.85f, 179),
                },
            },
        };

        private static SongSpec Song(string name, float bpm, int root, MusicScale scale, int[] progression,
            float swing, float drive, float lead, float pad, int seed) => new SongSpec
            {
                Name = name, Bpm = bpm, RootMidi = root, Scale = scale, Progression = progression,
                Swing = swing, Drive = drive, Lead = lead, Pad = pad, Seed = seed,
            };

        /// <summary>How many times a song's loop plays before the next song (at least twice).</summary>
        public static int RepeatsFor(SongSpec spec)
        {
            float loop = MusicComposer.LoopSeconds(spec);
            if (loop <= 0f) return 2;
            return Math.Max(2, (int)Math.Round(TargetSeconds / loop));
        }

        public static RadioStation Station(int index)
        {
            int n = Stations.Length;
            return Stations[((index % n) + n) % n];
        }

        /// <summary>A shuffled play order (Fisher-Yates over a seeded xorshift; the same seed gives the same order).</summary>
        public static int[] ShuffleOrder(int count, int seed)
        {
            var order = new int[Math.Max(0, count)];
            for (int i = 0; i < order.Length; i++) order[i] = i;
            uint s = (uint)seed * 2654435761u | 1u;
            for (int i = order.Length - 1; i > 0; i--)
            {
                s ^= s << 13; s ^= s >> 17; s ^= s << 5;
                int j = (int)(s % (uint)(i + 1));
                int t = order[i]; order[i] = order[j]; order[j] = t;
            }
            return order;
        }

        /// <summary>"LOW TIDE FM · TIDE LINE" for the now-playing card.</summary>
        public static string NowPlaying(RadioStation station, int song) =>
            station == null || station.Titles == null || song < 0 || song >= station.Titles.Length
                ? ""
                : station.Name + "  ·  " + station.Titles[song];

        public static string ModeName(MusicMode mode, int station) =>
            mode == MusicMode.Radio ? "RADIO: " + Station(station).Name
            : mode == MusicMode.Off ? "MUSIC: OFF"
            : "MUSIC: PARK THEMES";

        /// <summary>The music button's cycle: park themes → each radio station → off → park themes.</summary>
        public static void Cycle(ref MusicMode mode, ref int station)
        {
            if (mode == MusicMode.ParkThemes) { mode = MusicMode.Radio; station = 0; return; }
            if (mode == MusicMode.Radio && station < Stations.Length - 1) { station++; return; }
            if (mode == MusicMode.Radio) { mode = MusicMode.Off; return; }
            mode = MusicMode.ParkThemes;
            station = 0;
        }
    }

    /// <summary>Which song a station is on, in shuffled order; reshuffles (never repeating a song back to back) each lap.</summary>
    public sealed class RadioPlayer
    {
        private int[] _order = new int[0];
        private int _pos;
        private int _seed;

        public int StationIndex { get; private set; }
        public RadioStation Station => Radio.Station(StationIndex);
        public int SongIndex => _order.Length == 0 ? 0 : _order[_pos];
        public SongSpec Song => Station.Songs[SongIndex];
        public string Title => Station.Titles[SongIndex];
        public string NowPlaying => Radio.NowPlaying(Station, SongIndex);

        public RadioPlayer(int station, int seed)
        {
            _seed = seed;
            Tune(station);
        }

        /// <summary>Changes station and starts at the top of a fresh shuffle.</summary>
        public void Tune(int station)
        {
            int n = Radio.Stations.Length;
            StationIndex = ((station % n) + n) % n;
            _order = Radio.ShuffleOrder(Station.Songs.Length, _seed + StationIndex * 7919);
            _pos = 0;
        }

        /// <summary>Moves to the next song. Returns its index.</summary>
        public int Next()
        {
            if (_order.Length <= 1) return SongIndex;
            _pos++;
            if (_pos >= _order.Length)
            {
                int last = _order[_order.Length - 1];
                _seed++;
                _order = Radio.ShuffleOrder(_order.Length, _seed + StationIndex * 7919);
                if (_order[0] == last) { int t = _order[0]; _order[0] = _order[1]; _order[1] = t; }
                _pos = 0;
            }
            return SongIndex;
        }

        /// <summary>The songs in the order they'll play from now (current first), for tests and the station card.</summary>
        public List<int> Upcoming()
        {
            var list = new List<int>();
            for (int i = _pos; i < _order.Length; i++) list.Add(_order[i]);
            return list;
        }
    }
}

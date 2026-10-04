using System;
using System.Collections.Generic;
using System.Text;

namespace RetroSk8.Core
{
    /// <summary>A line the ghost's skater banked: when, and how many points.</summary>
    [Serializable]
    public struct GhostBank
    {
        public float Time;
        public long Points;
        public GhostBank(float time, long points) { Time = time; Points = points; }
    }

    /// <summary>A friend's ghost you raced (kept for the CODES screen).</summary>
    [Serializable]
    public sealed class RivalRecord
    {
        public string from;
        public string parkName;
        public long theirScore;
        public long myScore;
        public bool won;
        public int dateKey;
    }

    /// <summary>The friend's score at any moment of their run, from their banked lines.</summary>
    public static class RivalTimeline
    {
        public static long ScoreAt(IList<GhostBank> banks, float time)
        {
            if (banks == null) return 0;
            long total = 0;
            foreach (var b in banks) if (b.Time <= time) total += b.Points;
            return total;
        }

        /// <summary>Adds a race to the rival list (newest first, at most <paramref name="max"/>).</summary>
        public static void Record(List<RivalRecord> rivals, RivalRecord r, int max = 12)
        {
            if (rivals == null || r == null) return;
            rivals.Insert(0, r);
            while (rivals.Count > max) rivals.RemoveAt(rivals.Count - 1);
        }
    }

    /// <summary>
    /// Ghost codes: a whole Two-Minute Run packed into text, so a friend can race your actual line, not just your
    /// score. No server: copy the code, send it any way you like, paste it into CODES. The run is resampled to
    /// 10 Hz, positions are stored as centimetre deltas and rotations as 29-bit "smallest three" quaternions,
    /// which keeps a full run to roughly 35-40k characters (fine for Messages, notes or email, too long to type).
    /// Format: "RG:" + URL-safe base 64 of a versioned, checksummed bit stream. Engine-free so it is unit-tested.
    /// </summary>
    public static class GhostCodes
    {
        public const string Prefix = "RG:";
        private const int Version = 1;
        public const float SampleRate = 10f;
        /// <summary>Longest run a code carries (a Two-Minute Run plus landing time, with room to spare).</summary>
        public const float MaxSeconds = 180f;
        public const int MaxBanks = 255;
        private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";

        // Quantisation.
        private const float PosUnit = 0.01f;       // 1 cm
        private const int DeltaBits = 12;          // ±20 m per sample (a respawn uses an absolute position instead)
        private const int AbsBits = 24;            // ±83 km
        private const float BoardUnit = 0.002f;    // 2 mm
        private const int BoardBits = 11;          // ±2 m from the skater
        private const int QuatBits = 9;
        private const float QuatRange = 0.70710678f;

        public static bool IsGhostCode(string text) => text != null && text.TrimStart().StartsWith(Prefix, StringComparison.Ordinal);

        // ---------------------------------------------------------------- encode

        public static string Encode(ScoreChallenge c)
        {
            if (c == null) throw new ArgumentNullException(nameof(c));
            if (c.Ghost == null || c.Ghost.Count < 2) throw new ArgumentException("No ghost to send");

            var w = new ShareCodes.BitWriter();
            w.Write(Version, 4);
            int index = c.Park != null ? -1 : Array.IndexOf(ShareCodes.BuiltInParks, c.LocationId);
            if (c.Park == null && index < 0) throw new ArgumentException("Unknown park " + c.LocationId);
            w.Write(c.Park != null ? 1 : 0, 1);
            if (c.Park != null) ShareCodes.WritePark(w, c.Park); else w.Write(index, 4);
            w.Write((int)Math.Min(Math.Max(0, c.Target), (1L << 30) - 1), 30);
            ShareCodes.WriteName(w, c.From ?? "", ShareCodes.MaxFromLength);

            // Frames, resampled at a fixed rate from the start of the run.
            float duration = Math.Min(c.Ghost.Duration, MaxSeconds);
            int count = Math.Max(2, (int)Math.Floor(duration * SampleRate) + 1);
            w.Write(count, 12);
            int px = 0, py = 0, pz = 0;
            for (int i = 0; i < count; i++)
            {
                c.Ghost.Sample(i / SampleRate, out var f);
                int x = Q(f.Position.X, PosUnit), y = Q(f.Position.Y, PosUnit), z = Q(f.Position.Z, PosUnit);
                int dx = x - px, dy = y - py, dz = z - pz;
                int lim = (1 << (DeltaBits - 1)) - 1;
                bool small = i > 0 && Math.Abs(dx) <= lim && Math.Abs(dy) <= lim && Math.Abs(dz) <= lim;
                w.Write(small ? 0 : 1, 1);
                if (small)
                {
                    WriteSigned(w, dx, DeltaBits); WriteSigned(w, dy, DeltaBits); WriteSigned(w, dz, DeltaBits);
                }
                else
                {
                    WriteSigned(w, x, AbsBits); WriteSigned(w, y, AbsBits); WriteSigned(w, z, AbsBits);
                }
                // Continue from what the reader will reconstruct, so rounding never drifts.
                px = small ? px + dx : Clamp(x, AbsBits);
                py = small ? py + dy : Clamp(y, AbsBits);
                pz = small ? pz + dz : Clamp(z, AbsBits);

                WriteQuat(w, f.Rotation);
                WriteQuat(w, f.Pose);
                WriteQuat(w, f.Body);
                WriteQuat(w, f.Board);
                WriteSigned(w, Q(f.BoardPosition.X, BoardUnit), BoardBits);
                WriteSigned(w, Q(f.BoardPosition.Y, BoardUnit), BoardBits);
                WriteSigned(w, Q(f.BoardPosition.Z, BoardUnit), BoardBits);
            }

            var banks = c.GhostBanks ?? new List<GhostBank>();
            int n = Math.Min(banks.Count, MaxBanks);
            w.Write(n, 8);
            for (int i = 0; i < n; i++)
            {
                w.Write(Math.Max(0, Math.Min(4095, (int)Math.Round(banks[i].Time * 10f))), 12);
                w.Write((int)Math.Max(0, Math.Min((1L << 26) - 1, banks[i].Points)), 26);
            }
            w.Write((int)Math.Min(Math.Max(0, c.Ghost.Score > 0 ? c.Ghost.Score : c.Target), (1L << 30) - 1), 30);

            var bytes = w.ToBytes();
            int sum = ShareCodes.Checksum(Prefix, bytes);
            var all = new byte[bytes.Length + 2];
            Array.Copy(bytes, all, bytes.Length);
            all[bytes.Length] = (byte)(sum >> 8);
            all[bytes.Length + 1] = (byte)sum;
            return Prefix + ToBase64(all);
        }

        // ---------------------------------------------------------------- decode

        public static bool TryDecode(string code, out ScoreChallenge challenge, out string error)
        {
            challenge = null;
            if (string.IsNullOrWhiteSpace(code)) { error = "NO CODE FOUND"; return false; }
            string text = Strip(code);
            if (!text.StartsWith(Prefix, StringComparison.Ordinal)) { error = "THAT ISN'T A GHOST CODE"; return false; }
            if (!TryFromBase64(text.Substring(Prefix.Length), out var all) || all.Length < 3)
            {
                error = "THAT GHOST CODE IS INCOMPLETE (COPY ALL OF IT)";
                return false;
            }
            var bytes = new byte[all.Length - 2];
            Array.Copy(all, bytes, bytes.Length);
            int sum = (all[all.Length - 2] << 8) | all[all.Length - 1];
            if (sum != ShareCodes.Checksum(Prefix, bytes)) { error = "THAT GHOST CODE IS INCOMPLETE (COPY ALL OF IT)"; return false; }

            try
            {
                var r = new ShareCodes.BitReader(bytes);
                if (r.Read(4) != Version) { error = "THAT GHOST NEEDS A NEWER VERSION"; return false; }
                var c = new ScoreChallenge();
                if (r.Read(1) == 1) c.Park = ShareCodes.ReadPark(r, CustomParkIds.Prefix + "shared");
                else
                {
                    int i = r.Read(4);
                    if (i >= ShareCodes.BuiltInParks.Length) { error = "THAT PARK NEEDS A NEWER VERSION"; return false; }
                    c.LocationId = ShareCodes.BuiltInParks[i];
                }
                c.Target = r.Read(30);
                c.From = ShareCodes.ReadName(r, ShareCodes.MaxFromLength);

                int count = r.Read(12);
                if (count < 2) { error = "THAT GHOST IS EMPTY"; return false; }
                var track = new ReplayTrack(SampleRate) { LocationId = c.Park != null ? c.Park.id : c.LocationId };
                int px = 0, py = 0, pz = 0;
                for (int i = 0; i < count; i++)
                {
                    bool abs = r.Read(1) == 1;
                    if (abs)
                    {
                        px = ReadSigned(r, AbsBits); py = ReadSigned(r, AbsBits); pz = ReadSigned(r, AbsBits);
                    }
                    else
                    {
                        px += ReadSigned(r, DeltaBits); py += ReadSigned(r, DeltaBits); pz += ReadSigned(r, DeltaBits);
                    }
                    var f = new ReplayFrame
                    {
                        Time = i / SampleRate,
                        Position = new RVec3(px * PosUnit, py * PosUnit, pz * PosUnit),
                        Rotation = ReadQuat(r),
                        Pose = ReadQuat(r),
                        Body = ReadQuat(r),
                        Board = ReadQuat(r),
                    };
                    f.BoardPosition = new RVec3(ReadSigned(r, BoardBits) * BoardUnit, ReadSigned(r, BoardBits) * BoardUnit, ReadSigned(r, BoardBits) * BoardUnit);
                    track.Add(f);
                }
                int n = r.Read(8);
                var banks = new List<GhostBank>(n);
                for (int i = 0; i < n; i++) banks.Add(new GhostBank(r.Read(12) / 10f, r.Read(26)));
                track.Score = r.Read(30);
                c.Ghost = track;
                c.GhostBanks = banks;
                challenge = c;
                error = null;
                return true;
            }
            catch (Exception)
            {
                error = "THAT GHOST CODE IS DAMAGED";
                return false;
            }
        }

        // ---------------------------------------------------------------- helpers

        private static int Q(float v, float unit) => (int)Math.Round(v / unit);

        private static int Clamp(int v, int bits)
        {
            int lim = (1 << (bits - 1)) - 1;
            return Math.Max(-lim, Math.Min(lim, v));
        }

        private static void WriteSigned(ShareCodes.BitWriter w, int v, int bits)
        {
            v = Clamp(v, bits);
            w.Write(v + (1 << (bits - 1)), bits);
        }

        private static int ReadSigned(ShareCodes.BitReader r, int bits) => r.Read(bits) - (1 << (bits - 1));

        /// <summary>"Smallest three": drop the largest component (made positive), store the other three in 9 bits each.</summary>
        private static void WriteQuat(ShareCodes.BitWriter w, RQuat q)
        {
            float len = (float)Math.Sqrt(q.X * q.X + q.Y * q.Y + q.Z * q.Z + q.W * q.W);
            if (len < 1e-6f) q = RQuat.Identity;
            else q = new RQuat(q.X / len, q.Y / len, q.Z / len, q.W / len);
            float[] c = { q.X, q.Y, q.Z, q.W };
            int big = 0;
            for (int i = 1; i < 4; i++) if (Math.Abs(c[i]) > Math.Abs(c[big])) big = i;
            float sign = c[big] < 0f ? -1f : 1f;
            w.Write(big, 2);
            int max = (1 << QuatBits) - 1;
            for (int i = 0; i < 4; i++)
            {
                if (i == big) continue;
                float v = c[i] * sign;
                int qv = (int)Math.Round((v / QuatRange * 0.5f + 0.5f) * max);
                w.Write(Math.Max(0, Math.Min(max, qv)), QuatBits);
            }
        }

        private static RQuat ReadQuat(ShareCodes.BitReader r)
        {
            int big = r.Read(2);
            int max = (1 << QuatBits) - 1;
            var c = new float[4];
            float sumSq = 0f;
            for (int i = 0; i < 4; i++)
            {
                if (i == big) continue;
                float v = (r.Read(QuatBits) / (float)max - 0.5f) * 2f * QuatRange;
                c[i] = v;
                sumSq += v * v;
            }
            c[big] = (float)Math.Sqrt(Math.Max(0f, 1f - sumSq));
            return new RQuat(c[0], c[1], c[2], c[3]);
        }

        /// <summary>Drops whitespace and line breaks that mail or chat apps may add when wrapping a long code.</summary>
        private static string Strip(string code)
        {
            var sb = new StringBuilder(code.Length);
            foreach (char ch in code) if (!char.IsWhiteSpace(ch)) sb.Append(ch);
            return sb.ToString();
        }

        private static string ToBase64(byte[] data)
        {
            var sb = new StringBuilder((data.Length * 4 + 2) / 3);
            int buffer = 0, bits = 0;
            foreach (byte x in data)
            {
                buffer = (buffer << 8) | x;
                bits += 8;
                while (bits >= 6)
                {
                    sb.Append(Alphabet[(buffer >> (bits - 6)) & 63]);
                    bits -= 6;
                }
                buffer &= (1 << bits) - 1;
            }
            if (bits > 0) sb.Append(Alphabet[(buffer << (6 - bits)) & 63]);
            return sb.ToString();
        }

        private static bool TryFromBase64(string text, out byte[] data)
        {
            var list = new List<byte>(text.Length * 3 / 4 + 1);
            int buffer = 0, bits = 0;
            foreach (char ch in text)
            {
                int v = Alphabet.IndexOf(ch);
                if (v < 0) { data = null; return false; }
                buffer = (buffer << 6) | v;
                bits += 6;
                if (bits >= 8)
                {
                    list.Add((byte)((buffer >> (bits - 8)) & 255));
                    bits -= 8;
                }
                buffer &= (1 << bits) - 1;
            }
            data = list.ToArray();
            return true;
        }
    }
}

namespace RetroSk8.Core
{
    /// <summary>One row of a friends-only Game Center leaderboard.</summary>
    public sealed class FriendScore
    {
        public int Rank;
        public string Name;
        public long Score;
        public bool IsYou;

        /// <summary>Parses the native bridge's "rank\tname\tscore\tisLocal" lines (bad lines are skipped).</summary>
        public static System.Collections.Generic.List<FriendScore> Parse(string text)
        {
            var list = new System.Collections.Generic.List<FriendScore>();
            if (string.IsNullOrEmpty(text)) return list;
            foreach (var line in text.Split('\n'))
            {
                var parts = line.Split('\t');
                if (parts.Length < 4) continue;
                if (!int.TryParse(parts[0], out int rank) || !long.TryParse(parts[2], out long score)) continue;
                list.Add(new FriendScore { Rank = rank, Name = parts[1].Trim(), Score = score, IsYou = parts[3].Trim() == "1" });
            }
            list.Sort((a, b) => a.Rank.CompareTo(b.Rank));
            return list;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Text;

namespace RetroSk8.Core
{
    /// <summary>A score to beat on a park, sent to a friend as a code.</summary>
    public sealed class ScoreChallenge
    {
        /// <summary>Built-in park id, or null when <see cref="Park"/> carries a custom park.</summary>
        public string LocationId;
        public CustomPark Park;
        public long Target;
        public string From;
        /// <summary>The friend's run to race against (ghost codes only; null for plain challenge codes).</summary>
        public ReplayTrack Ghost;
        /// <summary>When the friend banked each line, so the HUD can show their score as their ghost skates.</summary>
        public List<GhostBank> GhostBanks;
    }

    /// <summary>
    /// Text codes for sharing Create-a-Park layouts and score challenges without a server: a compact bit-packed
    /// payload with a version and checksum, written in Crockford base-32 (no I, L, O or U, so it is easy to read
    /// out or type) and grouped with dashes. Park codes start "RP", challenge codes "RC". Decoding is forgiving
    /// about case, spaces, dashes and the look-alike letters.
    /// </summary>
    public static class ShareCodes
    {
        public const string ParkPrefix = "RP";
        public const string ChallengePrefix = "RC";
        private const int Version = 1;
        /// <summary>Phase 16: parks with stretched or bent pieces need two extra fields per piece. Codes only use
        /// version 2 when a park needs it, so everything else stays readable by older builds.</summary>
        public const int ExtendedVersion = 2;

        public static int VersionFor(CustomPark park) => park != null && NeedsExtended(park) ? ExtendedVersion : Version;

        public static bool NeedsExtended(CustomPark park)
        {
            foreach (var p in park.pieces) if (p != null && (p.length != 0 || p.bend != 0)) return true;
            return false;
        }
        private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
        private const string NameChars = " ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-!'.&#";
        public const int MaxFromLength = 12;

        /// <summary>Built-in parks a challenge can name. Saved by index: append only.</summary>
        public static readonly string[] BuiltInParks = { "harbor_plaza", "neon_warehouse", "rooftop_run", "sunset_bowls", "retro_city", "floodgate_ditch", "moonlight_pier" };

        // ---------------------------------------------------------------- parks

        public static string EncodePark(CustomPark park)
        {
            var w = new BitWriter();
            int v = VersionFor(park);
            w.Write(v, 4);
            WritePark(w, park, v >= ExtendedVersion);
            return Finish(ParkPrefix, w);
        }

        public static bool TryDecodePark(string code, out CustomPark park, out string error)
        {
            park = null;
            if (!TryOpen(code, ParkPrefix, out var r, out error, out int version)) return false;
            try
            {
                park = ReadPark(r, CustomParkIds.ForSlot(1), version >= ExtendedVersion);
                return true;
            }
            catch (Exception)
            {
                error = "THAT CODE IS DAMAGED";
                park = null;
                return false;
            }
        }

        // ---------------------------------------------------------------- challenges

        public static string EncodeChallenge(ScoreChallenge c)
        {
            var w = new BitWriter();
            int v = VersionFor(c.Park);
            w.Write(v, 4);
            int index = c.Park != null ? -1 : Array.IndexOf(BuiltInParks, c.LocationId);
            if (c.Park == null && index < 0) throw new ArgumentException("Unknown park " + c.LocationId);
            w.Write(c.Park != null ? 1 : 0, 1);
            if (c.Park != null) WritePark(w, c.Park, v >= ExtendedVersion); else w.Write(index, 4);
            w.Write((int)Math.Min(Math.Max(0, c.Target), (1L << 30) - 1), 30);
            WriteName(w, c.From ?? "", MaxFromLength);
            return Finish(ChallengePrefix, w);
        }

        public static bool TryDecodeChallenge(string code, out ScoreChallenge challenge, out string error)
        {
            challenge = null;
            if (!TryOpen(code, ChallengePrefix, out var r, out error, out int version)) return false;
            try
            {
                var c = new ScoreChallenge();
                if (r.Read(1) == 1) c.Park = ReadPark(r, CustomParkIds.Prefix + "shared", version >= ExtendedVersion);
                else
                {
                    int i = r.Read(4);
                    if (i >= BuiltInParks.Length) { error = "THAT PARK NEEDS A NEWER VERSION"; return false; }
                    c.LocationId = BuiltInParks[i];
                }
                c.Target = r.Read(30);
                c.From = ReadName(r, MaxFromLength);
                challenge = c;
                return true;
            }
            catch (Exception)
            {
                error = "THAT CODE IS DAMAGED";
                return false;
            }
        }

        /// <summary>"RP" or "RC" for a code, or null when it isn't one of ours.</summary>
        public static string KindOf(string code)
        {
            string clean = Clean(code);
            if (clean.StartsWith(ParkPrefix, StringComparison.Ordinal)) return ParkPrefix;
            if (clean.StartsWith(ChallengePrefix, StringComparison.Ordinal)) return ChallengePrefix;
            return null;
        }

        // ---------------------------------------------------------------- payloads

        internal static void WritePark(BitWriter w, CustomPark park, bool extended)
        {
            WriteName(w, park.name ?? "", CustomPark.MaxNameLength);
            w.Write(park.theme & 3, 2);
            int n = Math.Min(park.pieces.Count, CustomPark.MaxPieces);
            w.Write(n, 6);
            for (int i = 0; i < n; i++)
            {
                var p = park.pieces[i];
                w.Write(p.kind & 15, 4);
                w.Write(p.x & 63, 6);
                w.Write(p.z & 63, 6);
                w.Write(p.rot & 3, 2);
                w.Write(p.size & 3, 2);
                if (!extended) continue;
                w.Write(Math.Max(0, Math.Min(CustomPark.MaxLength, p.length)), 2);
                w.Write(Math.Max(-CustomPark.MaxBend, Math.Min(CustomPark.MaxBend, p.bend)) + CustomPark.MaxBend, 3);
            }
        }

        internal static CustomPark ReadPark(BitReader r, string id, bool extended)
        {
            var park = CustomPark.Create(id, ReadName(r, CustomPark.MaxNameLength));
            park.theme = r.Read(2);
            int n = r.Read(6);
            for (int i = 0; i < n; i++)
            {
                var p = new ParkPiece { kind = r.Read(4), x = r.Read(6), z = r.Read(6), rot = r.Read(2), size = r.Read(2) };
                if (extended)
                {
                    p.length = r.Read(2);
                    p.bend = r.Read(3) - CustomPark.MaxBend;
                }
                park.pieces.Add(p);
            }
            park.Sanitize(); // drops anything overlapping or out of range (a hand-edited code can't break the builder)
            return park;
        }

        internal static void WriteName(BitWriter w, string name, int max)
        {
            var chars = new List<int>();
            foreach (char ch in name.ToUpperInvariant())
            {
                int i = NameChars.IndexOf(ch);
                if (i >= 0) chars.Add(i);
                if (chars.Count >= max) break;
            }
            w.Write(chars.Count, 5);
            foreach (int c in chars) w.Write(c, 6);
        }

        internal static string ReadName(BitReader r, int max)
        {
            int n = Math.Min(r.Read(5), max);
            var sb = new StringBuilder();
            for (int i = 0; i < n; i++)
            {
                int c = r.Read(6);
                sb.Append(c < NameChars.Length ? NameChars[c] : ' ');
            }
            return sb.ToString().Trim();
        }

        // ---------------------------------------------------------------- framing

        private static string Finish(string prefix, BitWriter w)
        {
            var bytes = w.ToBytes();
            int sum = Checksum(prefix, bytes);
            var all = new byte[bytes.Length + 2];
            Array.Copy(bytes, all, bytes.Length);
            all[bytes.Length] = (byte)(sum >> 8);
            all[bytes.Length + 1] = (byte)sum;
            string body = ToBase32(all);
            var sb = new StringBuilder(prefix);
            for (int i = 0; i < body.Length; i++)
            {
                if (i % 5 == 0) sb.Append('-');
                sb.Append(body[i]);
            }
            return sb.ToString();
        }

        private static bool TryOpen(string code, string prefix, out BitReader reader, out string error, out int version)
        {
            reader = null;
            version = 0;
            string clean = Clean(code);
            if (string.IsNullOrEmpty(clean)) { error = "NO CODE FOUND"; return false; }
            if (!clean.StartsWith(prefix, StringComparison.Ordinal))
            {
                error = KindOf(clean) == null ? "THAT ISN'T A RETRO SK8 CODE" : prefix == ParkPrefix ? "THAT'S A CHALLENGE CODE, NOT A PARK" : "THAT'S A PARK CODE, NOT A CHALLENGE";
                return false;
            }
            if (!TryFromBase32(clean.Substring(prefix.Length), out var all) || all.Length < 3)
            {
                error = "THAT CODE HAS A TYPO";
                return false;
            }
            var bytes = new byte[all.Length - 2];
            Array.Copy(all, bytes, bytes.Length);
            int sum = (all[all.Length - 2] << 8) | all[all.Length - 1];
            if (sum != Checksum(prefix, bytes)) { error = "THAT CODE HAS A TYPO"; return false; }
            reader = new BitReader(bytes);
            version = reader.Read(4);
            if (version < Version || version > ExtendedVersion) { error = "THAT CODE NEEDS A NEWER VERSION"; reader = null; return false; }
            error = null;
            return true;
        }

        /// <summary>Uppercase, no spaces or dashes, look-alikes mapped (I/L→1, O→0).</summary>
        private static string Clean(string code)
        {
            if (string.IsNullOrEmpty(code)) return "";
            var sb = new StringBuilder();
            foreach (char raw in code.ToUpperInvariant())
            {
                if (raw == '-' || char.IsWhiteSpace(raw)) continue;
                sb.Append(raw);
            }
            // Prefix letters stay as typed; only the body gets look-alike mapping.
            if (sb.Length <= 2) return sb.ToString();
            var body = new StringBuilder(sb.ToString(0, 2));
            for (int i = 2; i < sb.Length; i++)
            {
                char c = sb[i];
                body.Append(c == 'I' || c == 'L' ? '1' : c == 'O' ? '0' : c);
            }
            return body.ToString();
        }

        /// <summary>Fletcher-16 over the prefix and payload.</summary>
        internal static int Checksum(string prefix, byte[] bytes)
        {
            int a = 0, b = 0;
            foreach (char c in prefix) { a = (a + c) % 255; b = (b + a) % 255; }
            foreach (byte x in bytes) { a = (a + x) % 255; b = (b + a) % 255; }
            return (b << 8) | a;
        }

        private static string ToBase32(byte[] data)
        {
            var sb = new StringBuilder();
            int buffer = 0, bits = 0;
            foreach (byte x in data)
            {
                buffer = (buffer << 8) | x;
                bits += 8;
                while (bits >= 5)
                {
                    sb.Append(Alphabet[(buffer >> (bits - 5)) & 31]);
                    bits -= 5;
                }
            }
            if (bits > 0) sb.Append(Alphabet[(buffer << (5 - bits)) & 31]);
            return sb.ToString();
        }

        private static bool TryFromBase32(string text, out byte[] data)
        {
            var list = new List<byte>();
            int buffer = 0, bits = 0;
            foreach (char c in text)
            {
                int v = Alphabet.IndexOf(c);
                if (v < 0) { data = null; return false; }
                buffer = (buffer << 5) | v;
                bits += 5;
                if (bits >= 8)
                {
                    list.Add((byte)((buffer >> (bits - 8)) & 255));
                    bits -= 8;
                }
            }
            data = list.ToArray();
            return true;
        }

        internal sealed class BitWriter
        {
            private readonly List<byte> _bytes = new List<byte>();
            private int _bit;

            public void Write(int value, int count)
            {
                for (int i = count - 1; i >= 0; i--)
                {
                    if (_bit % 8 == 0) _bytes.Add(0);
                    if (((value >> i) & 1) != 0) _bytes[_bytes.Count - 1] |= (byte)(1 << (7 - _bit % 8));
                    _bit++;
                }
            }

            public byte[] ToBytes() => _bytes.ToArray();
        }

        internal sealed class BitReader
        {
            private readonly byte[] _bytes;
            private int _bit;
            public BitReader(byte[] bytes) { _bytes = bytes; }
            public int BitsLeft => _bytes.Length * 8 - _bit;

            public int Read(int count)
            {
                int v = 0;
                for (int i = 0; i < count; i++)
                {
                    int byteIndex = _bit / 8;
                    if (byteIndex >= _bytes.Length) throw new FormatException("code too short");
                    v = (v << 1) | ((_bytes[byteIndex] >> (7 - _bit % 8)) & 1);
                    _bit++;
                }
                return v;
            }
        }
    }
}

using System;
using System.IO;
using System.Text;

namespace RetroSk8.Core
{
    public enum DuelPhase
    {
        /// <summary>The setter's attempt: bank any combo to set a target.</summary>
        Setting = 0,
        /// <summary>The other skater must bank at least the target or take a letter.</summary>
        Matching = 1,
        Finished = 2,
    }

    /// <summary>
    /// A 1-on-1 game of S.K.A.T.E. (classic rules): the setter banks any line; the opponent must bank at least
    /// <see cref="MatchFactor"/> of it or take a letter. The setter keeps setting until they miss a set, then the
    /// set passes over. Spell S-K-A-T-E and you lose. Pure and deterministic: both phones run their own copy and
    /// apply the same results in the same order, so they always agree.
    /// </summary>
    public sealed class SkateDuel
    {
        public const string Word = "SKATE";
        public const int MatchPercent = 80;
        public const float MatchFactor = MatchPercent / 100f;
        public const float AttemptSeconds = 25f;

        private readonly int[] _letters = new int[2];

        public DuelPhase Phase { get; private set; } = DuelPhase.Setting;
        public int Setter { get; private set; }
        /// <summary>Who is skating now (0 or 1).</summary>
        public int Actor => Phase == DuelPhase.Matching ? 1 - Setter : Setter;
        public long Target { get; private set; }
        public long SetPoints { get; private set; }
        /// <summary>Counts attempts; results carry it so a duplicated or late message is ignored.</summary>
        public int Turn { get; private set; }
        public string LastMessage { get; private set; } = "";
        public int Winner => Phase != DuelPhase.Finished ? -1 : _letters[0] >= Word.Length ? 1 : 0;

        public SkateDuel(int firstSetter) { Setter = firstSetter & 1; }

        public int Letters(int player) => _letters[player & 1];
        public static string LetterText(int letters) => Word.Substring(0, Math.Min(letters, Word.Length));

        /// <summary>
        /// Applies the result of attempt <paramref name="turn"/> by the current actor. Returns false (and changes
        /// nothing) for a stale turn or once the game is over.
        /// </summary>
        public bool Apply(int turn, long bankedPoints, string[] names = null)
        {
            if (Phase == DuelPhase.Finished || turn != Turn) return false;
            Turn++;
            bankedPoints = Math.Max(0, bankedPoints);
            string N(int i) => names != null && i < names.Length ? names[i] : (i == 0 ? "P1" : "P2");

            if (Phase == DuelPhase.Setting)
            {
                if (bankedPoints > 0)
                {
                    SetPoints = bankedPoints;
                    Target = ScoreMath.CeilPercent(bankedPoints, MatchPercent);
                    Phase = DuelPhase.Matching;
                    LastMessage = $"{N(Setter)} SET {bankedPoints:N0}. {N(1 - Setter)} NEEDS {Target:N0}";
                }
                else
                {
                    LastMessage = $"{N(Setter)} MISSED THE SET. {N(1 - Setter)} SETS";
                    Setter = 1 - Setter;
                }
                return true;
            }

            int matcher = 1 - Setter;
            if (bankedPoints >= Target) LastMessage = $"{N(matcher)} MATCHED IT";
            else
            {
                _letters[matcher]++;
                LastMessage = _letters[matcher] >= Word.Length
                    ? $"{N(matcher)} SPELLED {Word}. {N(Setter)} WINS!"
                    : $"{N(matcher)} TAKES {Word[_letters[matcher] - 1]}";
            }
            Target = 0;
            SetPoints = 0;
            Phase = _letters[matcher] >= Word.Length ? DuelPhase.Finished : DuelPhase.Setting;
            return true;
        }

        /// <summary>The other skater left: the one still here wins.</summary>
        public void Forfeit(int leaver)
        {
            if (Phase == DuelPhase.Finished) return;
            _letters[leaver & 1] = Word.Length;
            Phase = DuelPhase.Finished;
            LastMessage = "YOUR OPPONENT LEFT";
        }
    }

    public enum DuelMessageType : byte
    {
        Hello = 1,
        Start = 2,
        AttemptBegin = 3,
        Frame = 4,
        AttemptResult = 5,
        Rematch = 6,
        Leave = 7,
    }

    /// <summary>One network message for online S.K.A.T.E. Small, versioned, little-endian.</summary>
    public sealed class DuelMessage
    {
        public const byte ProtocolVersion = 1;

        public DuelMessageType Type;
        // Hello
        public string Name = "";
        public int Nonce;
        public int Style;
        // Start
        public int ParkIndex;
        public int FirstSetter;
        // AttemptBegin / AttemptResult
        public int Turn;
        public long Points;
        public string Label = "";
        // Frame
        public ReplayFrame Frame;

        public byte[] ToBytes()
        {
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms, Encoding.UTF8))
            {
                w.Write(ProtocolVersion);
                w.Write((byte)Type);
                switch (Type)
                {
                    case DuelMessageType.Hello: WriteString(w, Name, 16); w.Write(Nonce); w.Write((byte)Style); break;
                    case DuelMessageType.Start: w.Write((byte)ParkIndex); w.Write((byte)FirstSetter); break;
                    case DuelMessageType.AttemptBegin: w.Write(Turn); break;
                    case DuelMessageType.AttemptResult: w.Write(Turn); w.Write(Points); WriteString(w, Label, 60); break;
                    case DuelMessageType.Frame: WriteFrame(w, Frame); break;
                }
                return ms.ToArray();
            }
        }

        public static bool TryParse(byte[] data, out DuelMessage m)
        {
            m = null;
            if (data == null || data.Length < 2 || data.Length > DuelGuard.MaxMessageBytes || data[0] != ProtocolVersion) return false; // Phase 19 size cap
            try
            {
                using (var ms = new MemoryStream(data))
                using (var r = new BinaryReader(ms, Encoding.UTF8))
                {
                    r.ReadByte();
                    var msg = new DuelMessage { Type = (DuelMessageType)r.ReadByte() };
                    switch (msg.Type)
                    {
                        case DuelMessageType.Hello: msg.Name = ReadString(r); msg.Nonce = r.ReadInt32(); msg.Style = r.ReadByte(); break;
                        case DuelMessageType.Start: msg.ParkIndex = r.ReadByte(); msg.FirstSetter = r.ReadByte() & 1; break;
                        case DuelMessageType.AttemptBegin: msg.Turn = r.ReadInt32(); break;
                        case DuelMessageType.AttemptResult: msg.Turn = r.ReadInt32(); msg.Points = r.ReadInt64(); msg.Label = ReadString(r); break;
                        case DuelMessageType.Frame: msg.Frame = ReadFrame(r); break;
                        case DuelMessageType.Rematch:
                        case DuelMessageType.Leave: break;
                        default: return false;
                    }
                    m = msg;
                    return true;
                }
            }
            catch (EndOfStreamException) { return false; }
            catch (IOException) { return false; }
        }

        private static void WriteString(BinaryWriter w, string s, int max)
        {
            s = s ?? "";
            if (s.Length > max) s = s.Substring(0, max);
            var bytes = Encoding.UTF8.GetBytes(s);
            w.Write((byte)Math.Min(255, bytes.Length));
            w.Write(bytes, 0, Math.Min(255, bytes.Length));
        }

        private static string ReadString(BinaryReader r)
        {
            int n = r.ReadByte();
            return Encoding.UTF8.GetString(r.ReadBytes(n));
        }

        private static void WriteFrame(BinaryWriter w, ReplayFrame f)
        {
            w.Write(f.Time);
            V(w, f.Position); Q(w, f.Rotation); Q(w, f.Pose); Q(w, f.Body); V(w, f.BoardPosition); Q(w, f.Board);
        }

        private static ReplayFrame ReadFrame(BinaryReader r) => new ReplayFrame
        {
            Time = r.ReadSingle(),
            Position = V(r), Rotation = Q(r), Pose = Q(r), Body = Q(r), BoardPosition = V(r), Board = Q(r),
        };

        private static void V(BinaryWriter w, RVec3 v) { w.Write(v.X); w.Write(v.Y); w.Write(v.Z); }
        private static void Q(BinaryWriter w, RQuat q) { w.Write(q.X); w.Write(q.Y); w.Write(q.Z); w.Write(q.W); }
        private static RVec3 V(BinaryReader r) => new RVec3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
        private static RQuat Q(BinaryReader r) => new RQuat(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
    }

    /// <summary>The CPU opponent's skill: how often it lands a set or a match, and how big its lines are.</summary>
    public sealed class DuelBot
    {
        public enum Level { Easy = 0, Medium = 1, Hard = 2 }

        private readonly Random _rng;
        public Level Skill { get; }

        public DuelBot(Level skill, int seed) { Skill = skill; _rng = new Random(seed); }

        public static string Name(Level l) => l == Level.Easy ? "CPU ROOKIE" : l == Level.Medium ? "CPU LOCAL" : "CPU PRO";

        /// <summary>Points the bot banks when setting (0 = it bailed).</summary>
        public long Set()
        {
            double landChance = Skill == Level.Easy ? 0.55 : Skill == Level.Medium ? 0.72 : 0.86;
            if (_rng.NextDouble() > landChance) return 0;
            int lo = Skill == Level.Easy ? 400 : Skill == Level.Medium ? 1200 : 2600;
            int hi = Skill == Level.Easy ? 2200 : Skill == Level.Medium ? 5000 : 9000;
            return lo + (long)(_rng.NextDouble() * (hi - lo));
        }

        /// <summary>Points the bot banks when matching a target (harder targets are missed more often).</summary>
        public long Match(long target)
        {
            double reach = Skill == Level.Easy ? 2500 : Skill == Level.Medium ? 5500 : 10000;
            double chance = Math.Max(0.1, Math.Min(0.92, 1.05 - target / reach * 0.6));
            if (Skill == Level.Easy) chance *= 0.8;
            if (_rng.NextDouble() > chance) return (long)(target * (0.3 + _rng.NextDouble() * 0.5));
            return target + (long)(_rng.NextDouble() * target * 0.4);
        }

        /// <summary>Seconds the bot "skates" before its result arrives (feels like a real attempt).</summary>
        public float AttemptTime() => 4f + (float)_rng.NextDouble() * 5f;
    }
}

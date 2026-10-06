using System;
using System.Collections.Generic;

namespace RetroSk8.Core
{
    public enum PartyGame
    {
        /// <summary>Set a line, everyone else must beat it or take a letter. Spell RETRO and you're out.</summary>
        Letters = 0,
        /// <summary>Everyone gets one timed run; best score wins.</summary>
        ScoreTurns = 1,
        /// <summary>Phase 23: the setter calls a trick by landing it in a line; everyone else must land that trick or take a letter.</summary>
        TrickBattle = 2,
    }

    public enum PartyPhase
    {
        /// <summary>Waiting for the next player to take the phone and press READY.</summary>
        Handoff = 0,
        Playing = 1,
        Finished = 2,
    }

    public sealed class PartyPlayer
    {
        public string Name;
        public int Letters;
        public long Score;
        public bool Out => Letters >= PartyRules.Word.Length;
        public string LetterText => PartyRules.Word.Substring(0, Math.Min(Letters, PartyRules.Word.Length));
    }

    /// <summary>
    /// Pass-and-play on one phone. Pure turn logic (who's up, what they must beat, letters, winner);
    /// the skate scene feeds it each attempt's banked points and shows its state.
    ///
    /// Letters: the setter has one attempt to bank a combo. If they do, its points become the target and each
    /// other player still in gets one attempt to bank at least <see cref="MatchFactor"/> of it; missing earns a
    /// letter. After everyone has tried, the next player in order sets. A failed set just passes the set on.
    /// The last player who hasn't spelled RETRO wins.
    ///
    /// Score turns: each player gets one timed run in order; the best total wins (ties share the win).
    /// </summary>
    public sealed class PartyRules
    {
        public const string Word = "RETRO";
        public const int MinPlayers = 2;
        public const int MaxPlayers = 4;
        /// <summary>Matchers need this share of the setter's points (a perfect copy of a line is hard on a phone).</summary>
        public const int MatchPercent = 80;
        public const float MatchFactor = MatchPercent / 100f;
        public const float AttemptSeconds = 25f;
        public const float ScoreTurnSeconds = 60f;

        private readonly List<PartyPlayer> _players = new List<PartyPlayer>();
        private int _setter;
        private int _current;
        private readonly List<int> _toMatch = new List<int>();

        public PartyGame Game { get; }
        public PartyPhase Phase { get; private set; } = PartyPhase.Handoff;
        public IReadOnlyList<PartyPlayer> Players => _players;
        public PartyPlayer Current => _players[_current];
        public int CurrentIndex => _current;
        /// <summary>Points the current player must bank (0 while setting).</summary>
        public long Target { get; private set; }
        public long SetPoints { get; private set; }
        public bool IsSetting => (Game == PartyGame.Letters && Target == 0) || (Game == PartyGame.TrickBattle && TargetTrickId == null);
        public float AttemptLength => Game == PartyGame.ScoreTurns ? ScoreTurnSeconds : AttemptSeconds;
        /// <summary>Trick Battle: the trick everyone has to land (null while setting).</summary>
        public string TargetTrickId { get; private set; }
        public string TargetTrickName { get; private set; } = "";
        /// <summary>Longest player name (Phase 23 custom names).</summary>
        public const int MaxNameLength = 12;
        /// <summary>Last attempt's outcome, for the handoff screen.</summary>
        public string LastMessage { get; private set; } = "";

        public PartyRules(PartyGame game, IList<string> names)
        {
            if (names == null || names.Count < MinPlayers || names.Count > MaxPlayers)
                throw new ArgumentException($"Party needs {MinPlayers}-{MaxPlayers} players");
            Game = game;
            foreach (var n in CleanNames(names)) _players.Add(new PartyPlayer { Name = n });
            _setter = 0;
            _current = 0;
        }

        /// <summary>The player has the phone and pressed READY.</summary>
        public void BeginAttempt()
        {
            if (Phase == PartyPhase.Handoff) Phase = PartyPhase.Playing;
        }

        /// <summary>
        /// Ends the current attempt. For Letters, <paramref name="bankedPoints"/> is the best combo banked during the
        /// attempt (0 if they bailed or banked nothing). For Score turns it is the attempt's total.
        /// </summary>
        public void EndAttempt(long bankedPoints)
        {
            if (Phase != PartyPhase.Playing) return;
            bankedPoints = Math.Max(0, bankedPoints);
            if (Game == PartyGame.ScoreTurns) EndScoreTurn(bankedPoints);
            else if (Game == PartyGame.Letters) EndLettersAttempt(bankedPoints);
            else EndTrickAttempt(null, null, null); // Trick Battle uses EndTrickAttempt; points alone can't match a trick
        }

        public List<PartyPlayer> Winners()
        {
            var list = new List<PartyPlayer>();
            if (Game != PartyGame.ScoreTurns)
            {
                foreach (var p in _players) if (!p.Out) list.Add(p);
                return Phase == PartyPhase.Finished ? list : new List<PartyPlayer>();
            }
            long best = -1;
            foreach (var p in _players) best = Math.Max(best, p.Score);
            foreach (var p in _players) if (p.Score == best) list.Add(p);
            return list;
        }

        /// <summary>
        /// Phase 23 player names: cleaned like every other typed name (uppercase, safe characters, the word filter),
        /// empty or blocked names become PLAYER n, and repeats get a number so everyone can tell whose turn it is.
        /// </summary>
        public static List<string> CleanNames(IList<string> names)
        {
            var list = new List<string>();
            for (int i = 0; i < names.Count; i++)
            {
                string n = Gallery.SafeName(names[i], MaxNameLength, "PLAYER " + (i + 1));
                string unique = n;
                for (int k = 2; list.Contains(unique); k++)
                {
                    string suffix = " " + k;
                    unique = (n.Length + suffix.Length > MaxNameLength ? n.Substring(0, MaxNameLength - suffix.Length).TrimEnd() : n) + suffix;
                }
                list.Add(unique);
            }
            return list;
        }

        /// <summary>
        /// Trick Battle: the trick a banked line "calls" — its highest-scoring trick (the last one on a tie), so the
        /// setter decides by what they land. Null for an empty line. <paramref name="points"/> gives each id's value.
        /// </summary>
        public static string KeyTrick(IList<string> lineIds, Func<string, int> points)
        {
            string best = null;
            int bestPoints = int.MinValue;
            if (lineIds == null) return null;
            foreach (var id in lineIds)
            {
                if (string.IsNullOrEmpty(id) || id.StartsWith("gap_", StringComparison.Ordinal)) continue;
                int p = points != null ? points(id) : 0;
                if (p >= bestPoints) { bestPoints = p; best = id; }
            }
            return best;
        }

        /// <summary>
        /// Trick Battle: ends the current attempt. <paramref name="keyTrickId"/> is what the setter's best line called
        /// (null if nothing banked); <paramref name="landedIds"/> is every trick in every line the player banked.
        /// </summary>
        public void EndTrickAttempt(string keyTrickId, string keyTrickName, ICollection<string> landedIds)
        {
            if (Phase != PartyPhase.Playing || Game != PartyGame.TrickBattle) return;
            if (IsSetting)
            {
                if (!string.IsNullOrEmpty(keyTrickId))
                {
                    TargetTrickId = keyTrickId;
                    TargetTrickName = string.IsNullOrEmpty(keyTrickName) ? keyTrickId.ToUpperInvariant() : keyTrickName.ToUpperInvariant();
                    LastMessage = $"{Current.Name} CALLED {TargetTrickName}";
                    _toMatch.Clear();
                    for (int i = 1; i < _players.Count; i++)
                    {
                        int idx = (_setter + i) % _players.Count;
                        if (!_players[idx].Out) _toMatch.Add(idx);
                    }
                    NextMatcherOrNewSetter();
                }
                else
                {
                    LastMessage = $"{Current.Name} MISSED THE CALL";
                    NewSetter();
                }
                return;
            }
            bool landed = landedIds != null && landedIds.Contains(TargetTrickId);
            if (landed) LastMessage = $"{Current.Name} LANDED THE {TargetTrickName}";
            else
            {
                Current.Letters++;
                LastMessage = Current.Out ? $"{Current.Name} SPELLED {Word} AND IS OUT" : $"{Current.Name} TAKES {Word[Current.Letters - 1]}";
            }
            if (StillIn() <= 1) { Phase = PartyPhase.Finished; return; }
            NextMatcherOrNewSetter();
        }

        // ------------------------------------------------------------------ score turns

        private void EndScoreTurn(long points)
        {
            Current.Score = points;
            LastMessage = $"{Current.Name} SCORED {points:N0}";
            if (_current == _players.Count - 1) { Phase = PartyPhase.Finished; return; }
            _current++;
            Phase = PartyPhase.Handoff;
        }

        // ------------------------------------------------------------------ letters

        private void EndLettersAttempt(long points)
        {
            if (IsSetting)
            {
                if (points > 0)
                {
                    SetPoints = points;
                    Target = ScoreMath.CeilPercent(points, MatchPercent);
                    LastMessage = $"{Current.Name} SET {points:N0}. BANK {Target:N0} TO MATCH";
                    _toMatch.Clear();
                    for (int i = 1; i < _players.Count; i++)
                    {
                        int idx = (_setter + i) % _players.Count;
                        if (!_players[idx].Out) _toMatch.Add(idx);
                    }
                    NextMatcherOrNewSetter();
                }
                else
                {
                    LastMessage = $"{Current.Name} MISSED THE SET";
                    NewSetter();
                }
                return;
            }

            if (points >= Target) LastMessage = $"{Current.Name} MATCHED IT";
            else
            {
                Current.Letters++;
                LastMessage = Current.Out ? $"{Current.Name} SPELLED {Word} AND IS OUT" : $"{Current.Name} TAKES {Word[Current.Letters - 1]}";
            }
            if (StillIn() <= 1) { Phase = PartyPhase.Finished; return; }
            NextMatcherOrNewSetter();
        }

        private void NextMatcherOrNewSetter()
        {
            while (_toMatch.Count > 0)
            {
                int idx = _toMatch[0];
                _toMatch.RemoveAt(0);
                if (_players[idx].Out) continue;
                _current = idx;
                Phase = PartyPhase.Handoff;
                return;
            }
            NewSetter();
        }

        private void NewSetter()
        {
            Target = 0;
            SetPoints = 0;
            TargetTrickId = null;
            TargetTrickName = "";
            _toMatch.Clear();
            for (int i = 1; i <= _players.Count; i++)
            {
                int idx = (_setter + i) % _players.Count;
                if (_players[idx].Out) continue;
                _setter = idx;
                break;
            }
            _current = _setter;
            Phase = PartyPhase.Handoff;
        }

        private int StillIn()
        {
            int n = 0;
            foreach (var p in _players) if (!p.Out) n++;
            return n;
        }
    }
}

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
        public bool IsSetting => Game == PartyGame.Letters && Target == 0;
        public float AttemptLength => Game == PartyGame.Letters ? AttemptSeconds : ScoreTurnSeconds;
        /// <summary>Last attempt's outcome, for the handoff screen.</summary>
        public string LastMessage { get; private set; } = "";

        public PartyRules(PartyGame game, IList<string> names)
        {
            if (names == null || names.Count < MinPlayers || names.Count > MaxPlayers)
                throw new ArgumentException($"Party needs {MinPlayers}-{MaxPlayers} players");
            Game = game;
            foreach (var n in names) _players.Add(new PartyPlayer { Name = n });
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
            else EndLettersAttempt(bankedPoints);
        }

        public List<PartyPlayer> Winners()
        {
            var list = new List<PartyPlayer>();
            if (Game == PartyGame.Letters)
            {
                foreach (var p in _players) if (!p.Out) list.Add(p);
                return Phase == PartyPhase.Finished ? list : new List<PartyPlayer>();
            }
            long best = -1;
            foreach (var p in _players) best = Math.Max(best, p.Score);
            foreach (var p in _players) if (p.Score == best) list.Add(p);
            return list;
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

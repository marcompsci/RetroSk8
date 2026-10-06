using System;
using RetroSk8.Core;
using RetroSk8.Game;
using RetroSk8.Level;
using RetroSk8.Save;
using UnityEngine;

namespace RetroSk8.Duel
{
    /// <summary>
    /// One S.K.A.T.E. match that lives across scene loads: the connection (online or CPU), both names, the park,
    /// and the shared <see cref="SkateDuel"/> rules. Online, both phones say Hello; the one with the higher random
    /// number hosts (it is player 0), picks the park and who sets first, and sends Start. Then whoever is up skates,
    /// streams their skater's pose so the other phone can watch live, and reports the banked points.
    /// </summary>
    public sealed class DuelSession : MonoBehaviour
    {
        public enum Stage { Connecting = 0, Handshake = 1, Playing = 2, Ended = 3 }

        public static DuelSession Current { get; private set; }

        public IDuelTransport Transport { get; private set; }
        public SkateDuel Duel { get; private set; }
        public Stage State { get; private set; } = Stage.Connecting;
        /// <summary>This phone's player index (the host is 0).</summary>
        public int Me { get; private set; }
        public int Opponent => 1 - Me;
        public readonly string[] Names = { "P1", "P2" };
        public int ParkIndex { get; private set; }
        public bool IsOnline => Transport != null && Transport.IsOnline;
        public bool OpponentWantsRematch { get; private set; }
        public bool IWantRematch { get; private set; }
        public string Status { get; private set; } = "";

        /// <summary>The opponent started an attempt (turn).</summary>
        public event Action<int> OpponentAttemptStarted;
        /// <summary>A live pose of the opponent's skater.</summary>
        public event Action<ReplayFrame> OpponentFrame;
        /// <summary>A result was applied (either side) or the match ended.</summary>
        public event Action Changed;

        private int _myNonce;
        private int _theirNonce = -1;
        private bool _helloSent;
        private int _seed;
        private string _cpuName;
        private bool _endRecorded;

        public static readonly string[] Parks = ShareCodes.BuiltInParks;
        public string ParkId => Parks[Mathf.Clamp(ParkIndex, 0, Parks.Length - 1)];
        public string MyName => Names[Me];
        public string TheirName => Names[Opponent];

        // ---------------------------------------------------------------- start

        /// <summary>Starts matchmaking through Game Center. Check <see cref="GameCenterDuelTransport.Supported"/> first.</summary>
        public static DuelSession StartOnline()
        {
            var s = Create();
            s.Transport = new GameCenterDuelTransport();
            s._myNonce = UnityEngine.Random.Range(1, int.MaxValue);
            s.Status = "FINDING A SKATER...";
            return s;
        }

        /// <summary>A match against the CPU on the chosen park; you set first.</summary>
        public static DuelSession StartCpu(DuelBot.Level level, int parkIndex, int seed, string opponentName = null)
        {
            var s = Create();
            s._cpuName = opponentName;
            s.Transport = new CpuDuelTransport(level, 0, seed, opponentName);
            s.Me = 0;
            s.ParkIndex = parkIndex;
            s.Names[0] = SaveManager.Data.settings.playerName;
            s.Names[1] = s.Transport.OpponentName;
            s.BeginMatch(0);
            return s;
        }

        private static DuelSession Create()
        {
            End();
            var go = new GameObject("DuelSession");
            DontDestroyOnLoad(go);
            Current = go.AddComponent<DuelSession>();
            Current._seed = Environment.TickCount;
            return Current;
        }

        /// <summary>Leaves the match (tells the other phone) and removes the session.</summary>
        public static void End()
        {
            if (Current == null) return;
            var s = Current;
            Current = null;
            if (s.Transport != null)
            {
                if (s.State != Stage.Ended) s.Transport.Send(new DuelMessage { Type = DuelMessageType.Leave }, true);
                s.Transport.Leave();
            }
            Destroy(s.gameObject);
        }

        private void BeginMatch(int firstSetter)
        {
            Duel = new SkateDuel(firstSetter);
            _endRecorded = false;
            State = Stage.Playing;
            IWantRematch = OpponentWantsRematch = false;
            Status = "";
            RecordEnd();
            Changed?.Invoke();
        }

        // ---------------------------------------------------------------- loop

        private void Update()
        {
            if (Transport == null) return;
            Transport.Tick(Time.unscaledDeltaTime);

            var link = Transport.State;
            if (State == Stage.Connecting)
            {
                if (link == LinkState.Failed) { Status = "COULDN'T FIND A MATCH"; State = Stage.Ended; RecordEnd();
            Changed?.Invoke(); return; }
                if (link == LinkState.Connected)
                {
                    State = Stage.Handshake;
                    Status = "CONNECTED. SAYING HI...";
                    RecordEnd();
            Changed?.Invoke();
                }
            }
            if (State == Stage.Handshake && !_helloSent)
            {
                _helloSent = true;
                Transport.Send(new DuelMessage { Type = DuelMessageType.Hello, Name = SaveManager.Data.settings.playerName, Nonce = _myNonce, Style = SaveManager.Data.look.style }, true);
                TryStartAsHost();
            }
            if ((State == Stage.Playing || State == Stage.Handshake) && link == LinkState.Disconnected) OpponentGone();

            // Phase 19: everything from the other phone is untrusted: rate-limited, checked and cleaned before use.
            while (Transport.TryReceive(out var m))
            {
                if (!_incoming.Allow(Time.realtimeSinceStartupAsDouble)) continue;
                if (!IsOnline || DuelGuard.Sanitize(m)) Handle(m);
            }
        }

        private readonly RateLimiter _incoming = new RateLimiter(DuelGuard.MessagesPerSecond, DuelGuard.Burst);

        private void Handle(DuelMessage m)
        {
            switch (m.Type)
            {
                case DuelMessageType.Hello:
                {
                    if (Duel != null || _theirNonce >= 0) break; // Phase 19: one hello per match; a late one can't swap roles
                    _theirNonce = m.Nonce;
                    string mine = SaveManager.Data.settings.playerName;
                    string theirs = string.IsNullOrWhiteSpace(m.Name) ? Transport.OpponentName : m.Name.ToUpperInvariant();
                    // Higher random number hosts; on the (vanishingly rare) tie, the names decide.
                    Me = _myNonce != _theirNonce ? (_myNonce > _theirNonce ? 0 : 1) : (string.CompareOrdinal(mine, theirs) >= 0 ? 0 : 1);
                    Names[Me] = mine;
                    Names[Opponent] = theirs;
                    TryStartAsHost();
                    break;
                }

                case DuelMessageType.Start:
                    if (Me == 1 && Duel == null)
                    {
                        ParkIndex = Mathf.Clamp(m.ParkIndex, 0, Parks.Length - 1);
                        BeginMatch(m.FirstSetter);
                        LoadPark();
                    }
                    break;

                case DuelMessageType.AttemptBegin:
                    if (Duel != null && Duel.Actor == Opponent) OpponentAttemptStarted?.Invoke(m.Turn);
                    break;

                case DuelMessageType.Frame:
                    if (Duel != null && Duel.Actor == Opponent) OpponentFrame?.Invoke(m.Frame); // only while it's their go
                    break;

                case DuelMessageType.AttemptResult:
                    if (Duel != null && Duel.Actor == Opponent && Duel.Apply(m.Turn, m.Points, Names))
                    {
                        if (Duel.Phase == DuelPhase.Finished) State = Stage.Ended;
                        RecordEnd();
            Changed?.Invoke();
                    }
                    break;

                case DuelMessageType.Rematch:
                    OpponentWantsRematch = true;
                    TryRematch();
                    RecordEnd();
            Changed?.Invoke();
                    break;

                case DuelMessageType.Leave:
                    OpponentGone();
                    break;
            }
        }

        private void TryStartAsHost()
        {
            if (Me != 0 || _theirNonce < 0 || !_helloSent || Duel != null) return;
            // Host picks the park (any of the five) and a random first setter.
            ParkIndex = UnityEngine.Random.Range(0, Parks.Length);
            int first = UnityEngine.Random.Range(0, 2);
            Transport.Send(new DuelMessage { Type = DuelMessageType.Start, ParkIndex = ParkIndex, FirstSetter = first }, true);
            BeginMatch(first);
            LoadPark();
        }

        private void OpponentGone()
        {
            if (Duel == null) { Status = "THE OTHER SKATER LEFT"; State = Stage.Ended; RecordEnd();
            Changed?.Invoke(); return; }
            if (Duel.Phase != DuelPhase.Finished) Duel.Forfeit(Opponent);
            State = Stage.Ended;
            Status = "THE OTHER SKATER LEFT";
            RecordEnd();
            Changed?.Invoke();
        }

        // ---------------------------------------------------------------- my turn

        public bool MyTurn => State == Stage.Playing && Duel != null && Duel.Phase != DuelPhase.Finished && Duel.Actor == Me;

        public void SendAttemptBegin() => Transport.Send(new DuelMessage { Type = DuelMessageType.AttemptBegin, Turn = Duel.Turn }, true);

        public void SendFrame(ReplayFrame f)
        {
            if (IsOnline) Transport.Send(new DuelMessage { Type = DuelMessageType.Frame, Frame = f }, false);
        }

        /// <summary>Reports my attempt (0 = bailed or banked nothing) and applies it here.</summary>
        public void ReportMyResult(long points, string label)
        {
            if (!MyTurn) return;
            int turn = Duel.Turn;
            Transport.Send(new DuelMessage { Type = DuelMessageType.AttemptResult, Turn = turn, Points = points, Label = label ?? "" }, true);
            Duel.Apply(turn, points, Names);
            if (Duel.Phase == DuelPhase.Finished) State = Stage.Ended;
            RecordEnd();
            Changed?.Invoke();
        }

        // ---------------------------------------------------------------- rematch

        public void RequestRematch()
        {
            if (Duel == null || Duel.Phase != DuelPhase.Finished || Transport.State == LinkState.Disconnected) return;
            if (!IsOnline)
            {
                // The CPU always says yes; the loser sets first.
                var cpu = (CpuDuelTransport)Transport;
                int loser = Duel.Winner == 0 ? 1 : 0;
                Transport = new CpuDuelTransport(cpu.Level, loser, _seed++, _cpuName);
                BeginMatch(loser);
                return;
            }
            IWantRematch = true;
            Transport.Send(new DuelMessage { Type = DuelMessageType.Rematch }, true);
            TryRematch();
            RecordEnd();
            Changed?.Invoke();
        }

        private void TryRematch()
        {
            if (!IWantRematch || !OpponentWantsRematch || Duel == null) return;
            BeginMatch(Duel.Winner == 0 ? 1 : 0); // both phones compute the same loser
        }

        /// <summary>Once per match: wins count for the leaderboard, the weekly event and crew recruiting.</summary>
        private void RecordEnd()
        {
            if (_endRecorded || Duel == null || Duel.Phase != DuelPhase.Finished) return;
            _endRecorded = true;
            if (Duel.Winner != Me) return;
            SaveManager.Data.skateWins++;
            GameCenter.SubmitScore(Leaderboards.SkateWins, SaveManager.Data.skateWins);
            WeeklyService.Count(WeeklyCounters.SkateWins, 1);
            if (!IsOnline && !string.IsNullOrEmpty(GameSession.CrewRecruitId)) CrewService.Recruit(GameSession.CrewRecruitId);
            if (!IsOnline) StoryService.OnSkateWon();
        }

        private void LoadPark()
        {
            GameSession.Mode = RunMode.Duel;
            GameSession.EditPark = false;
            GameSession.Challenge = null;
            SceneRouter.LoadPark(ParkId, ParkCatalog.SceneFor(ParkId));
        }

        /// <summary>Loads the match's park (the menu calls this for CPU matches; online matches load themselves).</summary>
        public void Go() => LoadPark();
    }
}

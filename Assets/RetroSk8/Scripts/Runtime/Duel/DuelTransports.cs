using System.Collections.Generic;
using System.Runtime.InteropServices;
using RetroSk8.Core;
using UnityEngine;

namespace RetroSk8.Duel
{
    public enum LinkState { Idle = 0, Searching = 1, Connected = 2, Failed = 3, Disconnected = 4 }

    /// <summary>How the two phones talk during S.K.A.T.E. (or how the CPU opponent answers).</summary>
    public interface IDuelTransport
    {
        bool IsOnline { get; }
        LinkState State { get; }
        string OpponentName { get; }
        void Send(DuelMessage message, bool reliable);
        bool TryReceive(out DuelMessage message);
        void Tick(float dt);
        void Leave();
    }

    /// <summary>
    /// Live online play through Game Center real-time matches (Apple's matchmaking and relay; no game server).
    /// iOS builds with Game Center enabled only; elsewhere it reports Failed straight away.
    /// </summary>
    public sealed class GameCenterDuelTransport : IDuelTransport
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void RetroSk8_DuelFindMatch();
        [DllImport("__Internal")] private static extern int RetroSk8_DuelState();
        [DllImport("__Internal")] private static extern int RetroSk8_DuelOpponentName(byte[] buffer, int capacity);
        [DllImport("__Internal")] private static extern void RetroSk8_DuelSend(byte[] data, int length, int reliable);
        [DllImport("__Internal")] private static extern int RetroSk8_DuelReceive(byte[] buffer, int capacity);
        [DllImport("__Internal")] private static extern void RetroSk8_DuelLeave();
        private static bool Native => true;
#else
        private static void RetroSk8_DuelFindMatch() { }
        private static int RetroSk8_DuelState() => (int)LinkState.Failed;
        private static int RetroSk8_DuelOpponentName(byte[] buffer, int capacity) => 0;
        private static void RetroSk8_DuelSend(byte[] data, int length, int reliable) { }
        private static int RetroSk8_DuelReceive(byte[] buffer, int capacity) => 0;
        private static void RetroSk8_DuelLeave() { }
        private static bool Native => false;
#endif
        private readonly byte[] _buffer = new byte[2048];
        private readonly byte[] _name = new byte[128];

        /// <summary>True when this build can play online at all (Game Center on and signed in).</summary>
        public static bool Supported => Native && RetroSk8.Game.GameCenter.IsAuthenticated;

        public GameCenterDuelTransport() { RetroSk8_DuelFindMatch(); }

        public bool IsOnline => true;
        public LinkState State => (LinkState)RetroSk8_DuelState();

        public string OpponentName
        {
            get
            {
                int n = RetroSk8_DuelOpponentName(_name, _name.Length);
                return n > 0 ? System.Text.Encoding.UTF8.GetString(_name, 0, n) : "OPPONENT";
            }
        }

        public void Send(DuelMessage message, bool reliable)
        {
            var bytes = message.ToBytes();
            RetroSk8_DuelSend(bytes, bytes.Length, reliable ? 1 : 0);
        }

        public bool TryReceive(out DuelMessage message)
        {
            message = null;
            while (true)
            {
                int n = RetroSk8_DuelReceive(_buffer, _buffer.Length);
                if (n == 0) return false;
                if (n < 0) continue; // oversized: drop it
                var data = new byte[n];
                System.Array.Copy(_buffer, data, n);
                if (DuelMessage.TryParse(data, out message)) return true;
            }
        }

        public void Tick(float dt) { }
        public void Leave() => RetroSk8_DuelLeave();
    }

    /// <summary>
    /// The CPU opponent, speaking the same protocol as a real phone. It keeps its own copy of the duel from the
    /// results it hears, and on its turn "skates" for a few seconds before reporting a result from <see cref="DuelBot"/>.
    /// </summary>
    public sealed class CpuDuelTransport : IDuelTransport
    {
        private readonly DuelBot _bot;
        private readonly SkateDuel _duel;
        private readonly int _me; // the bot's player index
        private readonly Queue<DuelMessage> _outbox = new Queue<DuelMessage>();
        private float _attemptTimer = -1f;
        private int _attemptTurn = -1;

        public CpuDuelTransport(DuelBot.Level level, int firstSetter, int seed)
        {
            _bot = new DuelBot(level, seed);
            _duel = new SkateDuel(firstSetter);
            _me = 1;
        }

        public bool IsOnline => false;
        public LinkState State => LinkState.Connected;
        public string OpponentName => DuelBot.Name(_bot.Skill);

        public void Send(DuelMessage message, bool reliable)
        {
            if (message.Type == DuelMessageType.AttemptResult) _duel.Apply(message.Turn, message.Points);
        }

        public bool TryReceive(out DuelMessage message)
        {
            if (_outbox.Count > 0) { message = _outbox.Dequeue(); return true; }
            message = null;
            return false;
        }

        public void Tick(float dt)
        {
            if (_duel.Phase == DuelPhase.Finished || _duel.Actor != _me) return;
            if (_attemptTurn != _duel.Turn)
            {
                _attemptTurn = _duel.Turn;
                _attemptTimer = _bot.AttemptTime();
                _outbox.Enqueue(new DuelMessage { Type = DuelMessageType.AttemptBegin, Turn = _duel.Turn });
                return;
            }
            if (_attemptTimer < 0f) return;
            _attemptTimer -= dt;
            if (_attemptTimer > 0f) return;
            _attemptTimer = -1f;
            long points = _duel.Phase == DuelPhase.Setting ? _bot.Set() : _bot.Match(_duel.Target);
            int turn = _duel.Turn;
            _duel.Apply(turn, points);
            _outbox.Enqueue(new DuelMessage
            {
                Type = DuelMessageType.AttemptResult, Turn = turn, Points = points,
                Label = points > 0 ? "CPU LINE" : "CPU BAILED",
            });
        }

        public void Leave() { }
    }
}

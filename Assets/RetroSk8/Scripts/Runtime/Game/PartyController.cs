using System;
using System.Collections.Generic;
using RetroSk8.Core;
using RetroSk8.Level;
using RetroSk8.Player;
using RetroSk8.Scoring;
using UnityEngine;

namespace RetroSk8.Game
{
    /// <summary>
    /// Runs pass-and-play turns in the skate scene (RunMode.Party) on top of <see cref="PartyRules"/>.
    /// Between turns the skater waits at the spawn with input off until the next player presses READY.
    /// Letters: an attempt ends when a combo banks or you bail (or time runs out). Score turns: a timed run.
    /// </summary>
    public sealed class PartyController : MonoBehaviour
    {
        private const float SettleDelay = 1.2f;

        private PlayerController _player;
        private ComboManager _combo;
        private BailHandler _bail;
        private LevelInfo _level;

        private float _attemptTimer;
        private float _endAt = -1f;
        private long _best;
        private long _total;
        private readonly List<string> _bestLine = new List<string>();
        private readonly HashSet<string> _landed = new HashSet<string>();

        public PartyRules Rules { get; private set; }
        public float TimeLeft => Mathf.Max(0f, _attemptTimer);
        public long AttemptBest => _best;
        public long AttemptTotal => _total;

        /// <summary>Raised whenever the phase, player or letters change (the view redraws).</summary>
        public event Action Changed;

        public static List<string> DefaultNames(int count)
        {
            var names = new List<string>();
            for (int i = 0; i < Mathf.Clamp(count, PartyRules.MinPlayers, PartyRules.MaxPlayers); i++) names.Add("PLAYER " + (i + 1));
            return names;
        }

        public void Init(PlayerController player, ComboManager combo, LevelInfo level, PartyGame game, int players)
        {
            _player = player;
            _combo = combo;
            _level = level;
            _bail = player.GetComponent<BailHandler>();
            var names = DefaultNames(players);
            // Phase 23: names typed on the setup screen (cleaned and filtered by PartyRules).
            if (GameSession.PartyNames != null)
                for (int i = 0; i < names.Count && i < GameSession.PartyNames.Count; i++)
                    if (!string.IsNullOrWhiteSpace(GameSession.PartyNames[i])) names[i] = GameSession.PartyNames[i];
            Rules = new PartyRules(game, names);
            combo.BankedDetail += (ids, points) =>
            {
                if (Rules.Phase != PartyPhase.Playing || _endAt >= 0f || ids == null || points <= 0) return;
                foreach (var id in ids) if (!string.IsNullOrEmpty(id)) _landed.Add(id);
                if (points >= _best) { _bestLine.Clear(); _bestLine.AddRange(ids); }
            };

            combo.Banked += (result, _, __) =>
            {
                if (Rules.Phase != PartyPhase.Playing || _endAt >= 0f) return;
                _best = Math.Max(_best, result.Points);
                _total += result.Points;
                if (Rules.Game != PartyGame.ScoreTurns && result.Points > 0) _endAt = Time.time + SettleDelay;
            };
            combo.Bailed += (_, __) =>
            {
                if (Rules.Phase == PartyPhase.Playing && Rules.Game != PartyGame.ScoreTurns && _endAt < 0f) _endAt = Time.time + SettleDelay;
            };
            EnterHandoff();
        }

        /// <summary>The player holding the phone is ready.</summary>
        public void Ready()
        {
            if (Rules.Phase != PartyPhase.Handoff) return;
            ResetSkater();
            _best = 0;
            _total = 0;
            _bestLine.Clear();
            _landed.Clear();
            _endAt = -1f;
            _attemptTimer = Rules.AttemptLength;
            Rules.BeginAttempt();
            _player.SetInputEnabled(true);
            Changed?.Invoke();
        }

        private void Update()
        {
            if (Rules == null || Rules.Phase != PartyPhase.Playing) return;
            if (_endAt >= 0f)
            {
                if (Time.time >= _endAt) FinishAttempt();
                return;
            }
            _attemptTimer -= Time.deltaTime;
            if (_attemptTimer <= 0f)
            {
                if (_combo.HasPendingBank) _combo.BankNow(); // a landed line counts even on the buzzer
                else _combo.Discard();
                FinishAttempt();
            }
        }

        private void FinishAttempt()
        {
            _endAt = -1f;
            if (Rules.Game == PartyGame.TrickBattle)
            {
                string key = PartyRules.KeyTrick(_bestLine, id => TrickBookService.Find(id)?.Points ?? 0);
                Rules.EndTrickAttempt(key, key != null ? TrickBookService.Find(key)?.Name : null, _landed);
            }
            else Rules.EndAttempt(Rules.Game == PartyGame.Letters ? _best : _total);
            if (Rules.Phase == PartyPhase.Handoff) EnterHandoff();
            else
            {
                if (Rules.Phase == PartyPhase.Finished && Rules.Game == PartyGame.TrickBattle)
                {
                    RetroSk8.Save.SaveManager.Data.stats.trickBattles++; // Phase 25: Called It
                    RetroSk8.Save.SaveManager.Save();
                }
                _player.SetInputEnabled(false);
                Changed?.Invoke();
            }
        }

        private void EnterHandoff()
        {
            _player.SetInputEnabled(false);
            ResetSkater();
            Changed?.Invoke();
        }

        private void ResetSkater()
        {
            _combo.Discard();
            if (_bail != null && _bail.IsBailing) _bail.RespawnNow();
            if (_level != null && _level.spawnPoint != null)
                _player.Teleport(_level.spawnPoint.position, _level.spawnPoint.forward);
        }
    }
}

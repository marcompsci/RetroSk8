using System;
using RetroSk8.Core;
using RetroSk8.Player;
using RetroSk8.Save;
using RetroSk8.Scoring;
using UnityEngine;

namespace RetroSk8.Game
{
    /// <summary>Crew mode on top of the saved <see cref="CrewState"/>: perks, XP and recruiting.</summary>
    public static class CrewService
    {
        public static CrewState State => SaveManager.Data.crew;

        public static float ScoreFactor { get { State.Perks(out var s, out _, out _); return s; } }
        public static float TokenFactor { get { State.Perks(out _, out var t, out _); return t; } }
        public static float SpecialFactor { get { State.Perks(out _, out _, out var p); return p; } }

        /// <summary>Banked points → crew XP (doubled in Crew Week). Saves only on level-ups; the run end saves the rest.</summary>
        public static void AddPoints(long points)
        {
            long xp = points / CrewLevels.PointsPerXp;
            if (WeeklyService.Modifier == WeeklyModifier.CrewXp) xp *= 2;
            if (xp <= 0) return;
            int gained = State.AddXp(xp, out int tokens);
            WeeklyService.Count(WeeklyCounters.CrewXp, xp, save: false);
            if (gained <= 0) return;
            CareerService.Pending.Add($"CREW LEVEL {State.Level}: {CrewLevels.Title(State.Level)}  +{tokens}");
            SaveManager.AddTokens(tokens);
        }

        /// <summary>Recruits after a won challenge. Returns true the first time.</summary>
        public static bool Recruit(string id)
        {
            var m = CrewRoster.Find(id);
            if (m == null || !State.Recruit(id)) return false;
            CareerService.Pending.Add($"{m.Name.ToUpperInvariant()} JOINED YOUR CREW!");
            SaveManager.Save();
            return true;
        }

        /// <summary>Starts a recruit attempt: a S.K.A.T.E. game against them or a score run at their park.</summary>
        public static void StartRecruit(CrewMember m, RetroSk8.Data.ContentRegistry content)
        {
            GameSession.CrewRecruitId = m.Id;
            if (m.Recruit == RecruitKind.Duel)
            {
                var s = RetroSk8.Duel.DuelSession.StartCpu(m.DuelLevel, Math.Max(0, Array.IndexOf(ShareCodes.BuiltInParks, m.HomePark)), Environment.TickCount, m.Name.ToUpperInvariant());
                s.Go();
                return;
            }
            ShareService.StartChallenge(new ScoreChallenge { LocationId = m.HomePark, Target = m.ScoreTarget, From = ShortName(m) }, content);
            GameSession.CrewRecruitId = m.Id; // StartChallenge leaves it alone, but be explicit
        }

        public static string ShortName(CrewMember m)
        {
            string n = m.Name.ToUpperInvariant();
            int q = n.IndexOf('"');
            if (q >= 0) n = n.Substring(0, q).Trim();
            int space = n.IndexOf(' ');
            return space > 0 ? n.Substring(0, space) : n;
        }
    }

    /// <summary>This week's event: counters, goal payouts and the weekly leaderboard.</summary>
    public static class WeeklyService
    {
        public static int WeekKey => WeeklyEvents.WeekKey(DateTime.Now);
        public static WeeklyEvent Current => WeeklyEvents.For(WeekKey);
        public static WeeklyModifier Modifier => Current.Modifier;

        public static WeeklyState State
        {
            get
            {
                var s = SaveManager.Data.weekly;
                s.EnsureWeek(WeekKey);
                return s;
            }
        }

        /// <summary>Counts toward this week's goals and pays any that complete.</summary>
        public static void Count(string counter, long amount, bool save = true)
        {
            var ev = Current;
            var u = State.Count(ev, counter, amount);
            if (counter == WeeklyCounters.BestScore && amount > 0) GameCenter.SubmitScore(WeeklyEvents.LeaderboardId, amount);
            if (u.Any)
            {
                foreach (var g in u.NewGoals) CareerService.Pending.Add($"{ev.Name}: {g.Text.ToUpperInvariant()}  +{WeeklyEvents.GoalTokens}");
                if (u.AllDone) CareerService.Pending.Add($"{ev.Name} COMPLETE!  +{WeeklyEvents.AllGoalsBonus} BONUS");
                SaveManager.AddTokens(u.Tokens);
                return;
            }
            if (save) SaveManager.Save();
        }

        /// <summary>Gap points multiplier for the week.</summary>
        public static float GapFactor => Modifier == WeeklyModifier.GapPoints ? 2f : 1f;
        public static int RaceTokenFactor => Modifier == WeeklyModifier.RaceTokens ? 2 : 1;
    }

    /// <summary>
    /// Per-run bookkeeping for crew and weekly events: applies the riding crew's perks, turns banked combos into
    /// crew XP and weekly counts, and records the run's results (best score, own-park runs, Daily Line clears).
    /// </summary>
    public sealed class MetaHook : MonoBehaviour
    {
        private ComboManager _combo;
        private RunController _run;
        private GapTracker _gaps;
        private string _locationId;

        public void Init(ComboManager combo, RunController run, PlayerController player, string locationId)
        {
            _combo = combo;
            _run = run;
            _locationId = locationId;
            combo.CrewFactor = CrewService.ScoreFactor;
            combo.SpecialFactor = CrewService.SpecialFactor;
            combo.Banked += OnBanked;
            run.Finished += OnFinished;
            _gaps = player.GetComponent<GapTracker>();
            if (_gaps != null) _gaps.GapCleared += OnGap;
        }

        private void OnDestroy()
        {
            if (_combo != null) _combo.Banked -= OnBanked;
            if (_run != null) _run.Finished -= OnFinished;
            if (_gaps != null) _gaps.GapCleared -= OnGap;
            SaveManager.Save(); // crew XP and weekly counts gathered since the last save
        }

        private void OnBanked(ComboResult result, string label, LandingQuality quality)
        {
            if (result.Points <= 0) return;
            CrewService.AddPoints(result.Points);
            WeeklyService.Count(WeeklyCounters.Combos, 1, save: false);
        }

        private void OnGap(RetroSk8.Level.GapZone zone) => WeeklyService.Count(WeeklyCounters.Gaps, 1, save: false);

        private void OnFinished(RunResult r)
        {
            if (r == null) return;
            bool scored = r.mode == RunMode.TwoMinuteRun || r.mode == RunMode.SpotContract || r.mode == RunMode.DailyLine;
            if (!scored) return;
            WeeklyService.Count(WeeklyCounters.BestScore, r.score, save: false);
            if (r.mode == RunMode.TwoMinuteRun && CustomParkIds.IsCustom(_locationId)) WeeklyService.Count(WeeklyCounters.CustomRuns, 1, save: false);
            if (r.tokensFromDaily > 0) WeeklyService.Count(WeeklyCounters.Daily, 1, save: false);

            // Crew recruit by score: beat their number at their park.
            var c = GameSession.ActiveChallengeFor(_locationId);
            if (c != null && !string.IsNullOrEmpty(GameSession.CrewRecruitId) && r.score > c.Target) CrewService.Recruit(GameSession.CrewRecruitId);
            SaveManager.Save();
        }
    }
}

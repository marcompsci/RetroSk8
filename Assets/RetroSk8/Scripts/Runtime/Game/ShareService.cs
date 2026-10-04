using RetroSk8.Core;
using RetroSk8.Data;
using RetroSk8.Save;
using UnityEngine;

namespace RetroSk8.Game
{
    /// <summary>Clipboard and import/start helpers around <see cref="ShareCodes"/>.</summary>
    public static class ShareService
    {
        /// <summary>Where a challenge's custom park is kept (it never takes one of your six slots).</summary>
        public static readonly string SharedParkId = CustomParkIds.Prefix + "shared";

        public static void Copy(string text)
        {
            GUIUtility.systemCopyBuffer = text ?? "";
            if (!string.IsNullOrEmpty(text)) WeeklyService.Count(WeeklyCounters.CodesShared, 1);
        }
        public static string Paste() => GUIUtility.systemCopyBuffer ?? "";

        public static string PlayerName => SaveManager.Data.settings.playerName;

        /// <summary>Copies a park's code. Returns the code.</summary>
        public static string SharePark(CustomPark park)
        {
            string code = ShareCodes.EncodePark(park);
            Copy(code);
            return code;
        }

        /// <summary>Saves an imported park into the first empty slot. Returns its id, or null when all six are used.</summary>
        public static string ImportPark(CustomPark park)
        {
            for (int slot = 1; slot <= CustomParkIds.MaxSlots; slot++)
            {
                string id = CustomParkIds.ForSlot(slot);
                if (SaveManager.FindCustomPark(id) != null) continue;
                var copy = park.Clone();
                copy.id = id;
                SaveManager.SaveCustomPark(copy);
                return id;
            }
            return null;
        }

        /// <summary>A code challenging a friend to beat <paramref name="score"/> at a park (built-in or your own).</summary>
        public static string ChallengeCode(string locationId, long score)
        {
            var c = new ScoreChallenge { Target = score, From = PlayerName };
            if (CustomParkIds.IsCustom(locationId))
            {
                c.Park = SaveManager.FindCustomPark(locationId);
                if (c.Park == null) return null;
            }
            else if (System.Array.IndexOf(ShareCodes.BuiltInParks, locationId) >= 0) c.LocationId = locationId;
            else return null;
            return ShareCodes.EncodeChallenge(c);
        }

        /// <summary>
        /// A ghost code for the run that just finished (the whole line, so a friend races your ghost, not just your
        /// score). Null when there's no recording of it.
        /// </summary>
        public static string GhostCode(string locationId, long score)
        {
            var track = GameSession.LastRunTrack;
            if (track == null || track.Count < 2 || track.LocationId != locationId) return null;
            var c = new ScoreChallenge { Target = score, From = PlayerName, Ghost = track, GhostBanks = GameSession.LastRunBanks };
            if (CustomParkIds.IsCustom(locationId))
            {
                c.Park = SaveManager.FindCustomPark(locationId);
                if (c.Park == null) return null;
            }
            else if (System.Array.IndexOf(ShareCodes.BuiltInParks, locationId) >= 0) c.LocationId = locationId;
            else return null;
            try { return GhostCodes.Encode(c); }
            catch (System.Exception e)
            {
                Debug.LogWarning("[RetroSk8] Could not make a ghost code: " + e.Message);
                return null;
            }
        }

        /// <summary>Keeps how a ghost race went (shown under RECENT RIVALS in CODES).</summary>
        public static void RecordRival(ScoreChallenge c, string parkName, long myScore)
        {
            if (c == null || c.Ghost == null) return;
            RivalTimeline.Record(SaveManager.Data.rivals, new RivalRecord
            {
                from = string.IsNullOrEmpty(c.From) ? "A FRIEND" : c.From,
                parkName = parkName,
                theirScore = c.Target,
                myScore = myScore,
                won = myScore > c.Target,
                dateKey = GameSession.TodayKey,
            });
            SaveManager.Save();
        }

        /// <summary>Loads the challenge's park in a Two-Minute Run with the target on screen.</summary>
        public static void StartChallenge(ScoreChallenge c, ContentRegistry content)
        {
            GameSession.Mode = RunMode.TwoMinuteRun;
            GameSession.EditPark = false;
            if (c.Park != null)
            {
                c.Park.id = SharedParkId;
                SaveManager.SaveCustomPark(c.Park);
                GameSession.Challenge = c;
                SceneRouter.LoadPark(SharedParkId, null);
                return;
            }
            GameSession.Challenge = c;
            var loc = content != null ? content.FindLocationExact(c.LocationId) : null;
            SceneRouter.LoadPark(c.LocationId, loc != null ? loc.sceneName : RetroSk8.Level.ParkCatalog.SceneFor(c.LocationId));
        }
    }
}

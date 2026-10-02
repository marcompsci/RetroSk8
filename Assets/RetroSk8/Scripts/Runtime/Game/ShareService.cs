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

        public static void Copy(string text) => GUIUtility.systemCopyBuffer = text ?? "";
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

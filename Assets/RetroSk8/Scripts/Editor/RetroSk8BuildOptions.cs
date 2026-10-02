using System.IO;
using UnityEditor;
using UnityEngine;

namespace RetroSk8.EditorTools
{
    /// <summary>
    /// Project-wide build switches, stored in ProjectSettings/RetroSk8BuildOptions.json so they travel with the repo.
    /// Game Center is opt-in because free (personal team) Apple accounts can't sign apps that use it.
    /// </summary>
    public static class RetroSk8BuildOptions
    {
        private const string FilePath = "ProjectSettings/RetroSk8BuildOptions.json";
        private const string GameCenterMenu = "Retro Sk8/Build iOS/Enable Game Center (paid Apple account)";

        [System.Serializable]
        private sealed class Data
        {
            public bool gameCenter;
        }

        private static Data Load()
        {
            try
            {
                if (File.Exists(FilePath)) return JsonUtility.FromJson<Data>(File.ReadAllText(FilePath)) ?? new Data();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[RetroSk8] Could not read build options: " + e.Message);
            }
            return new Data();
        }

        private static void Save(Data d) => File.WriteAllText(FilePath, JsonUtility.ToJson(d, true));

        public static bool GameCenterEnabled => Load().gameCenter;

        [MenuItem(GameCenterMenu, priority = 80)]
        private static void ToggleGameCenter()
        {
            var d = Load();
            d.gameCenter = !d.gameCenter;
            Save(d);
            Debug.Log(d.gameCenter
                ? "[RetroSk8] Game Center ON: the next iOS build adds the Game Center capability. Create the leaderboards in App Store Connect (see README)."
                : "[RetroSk8] Game Center OFF: builds sign with a free Apple account. Scores and achievements stay local.");
        }

        [MenuItem(GameCenterMenu, true)]
        private static bool ToggleGameCenterValidate()
        {
            Menu.SetChecked(GameCenterMenu, GameCenterEnabled);
            return true;
        }
    }
}

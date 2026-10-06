using System.Collections.Generic;
using System.IO;
using RetroSk8.Core;
using RetroSk8.Data;
using UnityEditor;
using UnityEngine;

namespace RetroSk8.EditorTools
{
    /// <summary>
    /// Retro Sk8 → Build iOS → Write Game Center Setup List (Phase 18): writes GameCenterSetup.md next to Assets with
    /// every leaderboard and achievement App Store Connect needs, generated from the same lists the game submits to
    /// (GameCenterSetup). Bridge: <c>gamecenter</c>.
    /// </summary>
    public static class RetroSk8GameCenterList
    {
        public const string OutputPath = "GameCenterSetup.md";
        private const string RegistryPath = "Assets/RetroSk8/ScriptableObjects/ContentRegistry.asset";

        [MenuItem("Retro Sk8/Build iOS/Write Game Center Setup List", priority = 83)]
        private static void Menu()
        {
            string result = Write();
            Debug.Log("[RetroSk8] " + result);
            EditorUtility.RevealInFinder(Path.GetFullPath(OutputPath));
        }

        /// <summary>Writes the list; returns a one-line summary.</summary>
        public static string Write()
        {
            var registry = AssetDatabase.LoadAssetAtPath<ContentRegistry>(RegistryPath);
            bool temporary = registry == null;
            if (temporary) registry = DefaultContent.CreateRegistry();
            var parks = new List<(string, string)>();
            foreach (var loc in registry.PlayableLocations()) parks.Add((loc.id, loc.displayName));
            var boards = LeaderboardHub.Boards(parks);
            string md = GameCenterSetup.Markdown(boards, Achievements.All);
            if (temporary) Object.DestroyImmediate(registry);
            try { File.WriteAllText(OutputPath, md); }
            catch (IOException e) { return "could not write " + OutputPath + ": " + e.Message; }
            return $"{OutputPath} written: {boards.Count} leaderboards, {Achievements.All.Count} achievements ({GameCenterSetup.TotalPoints(Achievements.All)} points)";
        }
    }
}

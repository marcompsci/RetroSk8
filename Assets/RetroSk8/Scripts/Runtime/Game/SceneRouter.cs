using RetroSk8.Level;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RetroSk8.Game
{
    /// <summary>
    /// Loads parks. Every park scene is just a camera, a sun and an installer (the park itself is generated
    /// from code), so a park whose scene isn't in Build Settings yet can borrow any other park scene: the
    /// installer swaps in the right builder. That keeps new parks playable before "Setup Project" is re-run.
    /// </summary>
    public static class SceneRouter
    {
        public static void LoadPark(string locationId, string sceneName)
        {
            GameSession.LocationId = locationId;
            if (!string.IsNullOrEmpty(sceneName) && Application.CanStreamedLevelBeLoaded(sceneName))
            {
                GameSession.ParkOverride = false;
                SceneManager.LoadScene(sceneName);
                return;
            }
            foreach (var fallback in new[] { SceneNames.HarborPlaza, SceneNames.NeonWarehouse, SceneNames.RooftopRun })
            {
                if (!Application.CanStreamedLevelBeLoaded(fallback)) continue;
                GameSession.ParkOverride = true; // the installer builds locationId instead of the scene's own park
                SceneManager.LoadScene(fallback);
                return;
            }
            Debug.LogWarning("[RetroSk8] No park scene is in Build Settings. Run 'Retro Sk8 > Setup Project'.");
        }

        public static void Load(string scene)
        {
            if (Application.CanStreamedLevelBeLoaded(scene)) SceneManager.LoadScene(scene);
            else Debug.LogWarning($"[RetroSk8] {scene} is not in Build Settings. Run 'Retro Sk8 > Setup Project'.");
        }
    }
}

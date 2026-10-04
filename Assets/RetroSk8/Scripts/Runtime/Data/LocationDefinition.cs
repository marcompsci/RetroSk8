using UnityEngine;

namespace RetroSk8.Data
{
    public enum AmbienceKind
    {
        Harbor = 0,
        Warehouse = 1,
        Rooftop = 2,
        City = 3,
        Bowls = 4,
        Ditch = 5,
        Pier = 6,
    }

    [CreateAssetMenu(menuName = "Retro Sk8/Location", fileName = "Location_")]
    public sealed class LocationDefinition : ScriptableObject
    {
        public string id = "harbor_plaza";
        public string displayName = "Harbor Plaza";
        public string sceneName = "SkateScene_HarborPlaza";
        [Tooltip("False until the park is built; locked parks are hidden from the debug park switcher.")]
        public bool isPlayable = true;
        public float runDurationSeconds = 120f;
        public AmbienceKind ambience = AmbienceKind.Harbor;

        [Header("Look")]
        public Color skyColor = new Color(0.98f, 0.62f, 0.45f);
        public Color ambientColor = new Color(0.55f, 0.52f, 0.6f);
        public Color fogColor = new Color(0.93f, 0.66f, 0.55f);
        public float fogDensity = 0.006f;
        public Color sunColor = new Color(1f, 0.9f, 0.78f);
        public Vector3 sunEuler = new Vector3(38f, -35f, 0f);
    }
}

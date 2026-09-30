using RetroSk8.Core;
using RetroSk8.Player;
using UnityEngine;

namespace RetroSk8.Level
{
    /// <summary>Per-park runtime data the gameplay layer needs: spawn, playable bounds and the fall-out height.</summary>
    public sealed class LevelInfo : MonoBehaviour
    {
        public Transform spawnPoint;
        public Bounds playableBounds = new Bounds(Vector3.zero, new Vector3(100f, 60f, 100f));
        public float killHeight = -4f;
        public System.Collections.Generic.List<GapZone> gaps = new System.Collections.Generic.List<GapZone>();

        public bool IsOutOfBounds(Vector3 position) => position.y < killHeight || !playableBounds.Contains(position);
    }
}

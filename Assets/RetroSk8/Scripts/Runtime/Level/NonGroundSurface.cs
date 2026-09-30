using UnityEngine;

namespace RetroSk8.Level
{
    /// <summary>
    /// Marks colliders that block the skater but must never count as ground (thin rails, posts).
    /// Without this the ground probe could "stand" the skater on top of a bar.
    /// </summary>
    public sealed class NonGroundSurface : MonoBehaviour { }
}

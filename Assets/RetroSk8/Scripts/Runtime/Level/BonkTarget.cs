using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RetroSk8.Level
{
    /// <summary>
    /// Marks an object you can bonk (Phase 18): cones, hydrants, barrels, car bumpers. <see cref="pole"/> marks a
    /// slanted post you can also pole jam by rolling into it. The skater checks for this on any collider it hits
    /// (see BonkRules). After a hit the skater passes through the object briefly so it clears it.
    /// </summary>
    public sealed class BonkTarget : MonoBehaviour
    {
        public bool pole;

        /// <summary>Time.time of the last bonk (for BonkRules.Cooldown).</summary>
        public float LastHit { get; private set; } = -99f;

        private Collider[] _colliders;

        public static int Count => s_all.Count;
        private static readonly HashSet<BonkTarget> s_all = new HashSet<BonkTarget>();

        private void OnEnable() => s_all.Add(this);
        private void OnDisable() => s_all.Remove(this);

        public void MarkHit(Collider skater, float passThroughSeconds)
        {
            LastHit = Time.time;
            if (skater == null || !isActiveAndEnabled) return;
            if (_colliders == null) _colliders = GetComponentsInChildren<Collider>();
            StartCoroutine(PassThrough(skater, passThroughSeconds));
        }

        private IEnumerator PassThrough(Collider skater, float seconds)
        {
            foreach (var c in _colliders) if (c != null) Physics.IgnoreCollision(skater, c, true);
            yield return new WaitForSeconds(seconds);
            foreach (var c in _colliders) if (c != null && skater != null) Physics.IgnoreCollision(skater, c, false);
        }
    }
}

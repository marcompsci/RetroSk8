using RetroSk8.Core;
using RetroSk8.Player;
using UnityEngine;

namespace RetroSk8.Level
{
    /// <summary>Trigger that sends the skater back to safety (water, pits).</summary>
    [RequireComponent(typeof(Collider))]
    public sealed class KillVolume : MonoBehaviour
    {
        private void Reset() => GetComponent<Collider>().isTrigger = true;

        private void OnTriggerEnter(Collider other)
        {
            var bail = other.attachedRigidbody != null ? other.attachedRigidbody.GetComponent<BailHandler>() : null;
            if (bail != null) bail.Trigger(BailReason.OutOfBounds);
        }
    }
}

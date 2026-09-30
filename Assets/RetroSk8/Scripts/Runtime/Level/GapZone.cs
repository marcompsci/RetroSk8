using RetroSk8.Player;
using UnityEngine;

namespace RetroSk8.Level
{
    /// <summary>
    /// A named gap. Passing through the trigger while airborne and then landing (or locking into a grind) clears it:
    /// the gap is added to the combo and reported to goals.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class GapZone : MonoBehaviour
    {
        public string gapId = "gap";
        public string displayName = "Gap";
        public int points = 750;

        private void Reset() => GetComponent<BoxCollider>().isTrigger = true;

        // Stay as well as Enter: a skater can roll into the volume on the ground and only become airborne inside it.
        private void OnTriggerEnter(Collider other) => Notify(other);
        private void OnTriggerStay(Collider other) => Notify(other);

        private void Notify(Collider other)
        {
            var tracker = other.attachedRigidbody != null ? other.attachedRigidbody.GetComponent<GapTracker>() : null;
            if (tracker != null) tracker.OnEnterGap(this);
        }

        private void OnDrawGizmos()
        {
            var box = GetComponent<BoxCollider>();
            if (box == null) return;
            Gizmos.color = new Color(1f, 0.76f, 0.19f, 0.35f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
        }
    }
}

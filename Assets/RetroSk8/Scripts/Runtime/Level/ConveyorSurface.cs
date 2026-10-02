using UnityEngine;

namespace RetroSk8.Level
{
    /// <summary>
    /// A moving belt. While the skater is grounded on this collider the belt carries them along
    /// <see cref="direction"/> (PlayerController applies it). The visual stripe texture scrolls to match.
    /// </summary>
    public sealed class ConveyorSurface : MonoBehaviour
    {
        public Vector3 direction = Vector3.forward;
        public float speed = 3f;
        [Tooltip("Optional renderer whose texture scrolls with the belt.")]
        public Renderer beltRenderer;

        private Material _material;
        private float _offset;

        public Vector3 Velocity => direction.normalized * speed;

        private void Start()
        {
            if (beltRenderer != null) _material = beltRenderer.material; // instance, so each belt scrolls on its own
        }

        private void Update()
        {
            if (_material == null) return;
            _offset = (_offset + speed * Time.deltaTime * 0.25f) % 1f;
            _material.mainTextureOffset = new Vector2(0f, -_offset);
        }
    }
}

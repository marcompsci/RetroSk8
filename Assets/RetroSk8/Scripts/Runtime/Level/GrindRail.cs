using System.Collections.Generic;
using UnityEngine;

namespace RetroSk8.Level
{
    public enum GrindSurface
    {
        Rail = 0,
        Ledge = 1,
        Coping = 2,
    }

    /// <summary>
    /// A grindable polyline in local space. Static geometry: world points are cached on enable.
    /// Rails register themselves so the GrindController can query without physics casts.
    /// </summary>
    public sealed class GrindRail : MonoBehaviour
    {
        private static readonly List<GrindRail> s_active = new List<GrindRail>();

        public List<Vector3> localPoints = new List<Vector3> { Vector3.zero, Vector3.forward * 4f };
        public bool loop;
        public GrindSurface surface = GrindSurface.Rail;

        private Vector3[] _world;
        private float[] _cumulative;

        public static IReadOnlyList<GrindRail> Active => s_active;
        public float Length { get; private set; }
        public int SegmentCount => _world == null ? 0 : (loop ? _world.Length : _world.Length - 1);

        private void OnEnable()
        {
            Rebuild();
            if (!s_active.Contains(this)) s_active.Add(this);
        }

        private void OnDisable() => s_active.Remove(this);

        public void Rebuild()
        {
            int n = localPoints.Count;
            _world = new Vector3[n];
            for (int i = 0; i < n; i++) _world[i] = transform.TransformPoint(localPoints[i]);

            int segs = loop ? n : n - 1;
            _cumulative = new float[segs + 1];
            for (int i = 0; i < segs; i++)
            {
                _cumulative[i + 1] = _cumulative[i] + Vector3.Distance(_world[i], _world[(i + 1) % n]);
            }
            Length = _cumulative[segs];
        }

        /// <summary>Wraps (loops) or clamps a distance along the rail.</summary>
        public float Normalize(float distance)
        {
            if (loop && Length > 0f)
            {
                distance %= Length;
                if (distance < 0f) distance += Length;
                return distance;
            }
            return Mathf.Clamp(distance, 0f, Length);
        }

        public Vector3 PointAt(float distance)
        {
            Locate(Normalize(distance), out int seg, out float t);
            return Vector3.Lerp(_world[seg], _world[(seg + 1) % _world.Length], t);
        }

        public Vector3 TangentAt(float distance)
        {
            Locate(Normalize(distance), out int seg, out _);
            Vector3 d = _world[(seg + 1) % _world.Length] - _world[seg];
            return d.sqrMagnitude > 1e-6f ? d.normalized : Vector3.forward;
        }

        public float ClosestDistance(Vector3 worldPos, out Vector3 closest)
        {
            closest = _world[0];
            float bestSqr = float.MaxValue;
            float bestAlong = 0f;
            for (int i = 0; i < SegmentCount; i++)
            {
                Vector3 a = _world[i];
                Vector3 b = _world[(i + 1) % _world.Length];
                Vector3 ab = b - a;
                float len2 = ab.sqrMagnitude;
                float t = len2 > 1e-6f ? Mathf.Clamp01(Vector3.Dot(worldPos - a, ab) / len2) : 0f;
                Vector3 p = a + ab * t;
                float d2 = (worldPos - p).sqrMagnitude;
                if (d2 < bestSqr)
                {
                    bestSqr = d2;
                    closest = p;
                    bestAlong = _cumulative[i] + Mathf.Sqrt(len2) * t;
                }
            }
            return bestAlong;
        }

        public bool IsEnd(float distance, float margin) => !loop && (distance <= margin || distance >= Length - margin);

        public static bool FindNearest(Vector3 worldPos, float maxDistance, out GrindRail rail, out float along, out Vector3 point)
        {
            rail = null;
            along = 0f;
            point = Vector3.zero;
            float best = maxDistance * maxDistance;
            foreach (var r in s_active)
            {
                float a = r.ClosestDistance(worldPos, out Vector3 p);
                float d2 = (p - worldPos).sqrMagnitude;
                if (d2 < best)
                {
                    best = d2;
                    rail = r;
                    along = a;
                    point = p;
                }
            }
            return rail != null;
        }

        private void Locate(float distance, out int segment, out float t)
        {
            int segs = SegmentCount;
            for (int i = 0; i < segs; i++)
            {
                if (distance <= _cumulative[i + 1] || i == segs - 1)
                {
                    float len = _cumulative[i + 1] - _cumulative[i];
                    segment = i;
                    t = len > 1e-6f ? Mathf.Clamp01((distance - _cumulative[i]) / len) : 0f;
                    return;
                }
            }
            segment = 0;
            t = 0f;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = surface == GrindSurface.Coping ? Color.cyan : (surface == GrindSurface.Ledge ? Color.yellow : Color.magenta);
            for (int i = 0; i + 1 < localPoints.Count; i++)
                Gizmos.DrawLine(transform.TransformPoint(localPoints[i]), transform.TransformPoint(localPoints[i + 1]));
            if (loop && localPoints.Count > 2)
                Gizmos.DrawLine(transform.TransformPoint(localPoints[localPoints.Count - 1]), transform.TransformPoint(localPoints[0]));
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_2023_2_OR_NEWER
using UnityEngine.Accessibility;
#endif

namespace RetroSk8.UI
{
    /// <summary>
    /// VoiceOver for the menus (Phase 20). When a screen reader is running, this mirrors every visible, tappable
    /// button into Unity's accessibility hierarchy with its label, so VoiceOver can read it and swipe between buttons
    /// (double-tap activates it at its centre). Rebuilt a few times a second while the reader is on; does nothing
    /// otherwise. Skating itself isn't readable (it's a real-time action game), but every menu, result and setting is.
    /// </summary>
    public sealed class ScreenReaderBridge : MonoBehaviour
    {
        private const float RefreshSeconds = 0.5f;
        private static ScreenReaderBridge s_instance;
        private float _next;
        private readonly Vector3[] _corners = new Vector3[4];
        private readonly List<string> _lastLabels = new List<string>();

        public static void Ensure()
        {
            if (s_instance != null) return;
            var go = new GameObject("ScreenReaderBridge");
            DontDestroyOnLoad(go);
            s_instance = go.AddComponent<ScreenReaderBridge>();
        }

        /// <summary>The words VoiceOver should say for a button: its visible text, cleaned up (e.g. "II" becomes "Pause").</summary>
        public static string SpokenLabel(string buttonName, string text)
        {
            string t = (text ?? "").Replace('\n', ' ').Trim();
            if (t == "II") return "Pause";
            if (t == "<" || t == "◀") return "Previous";
            if (t == ">" || t == "▶") return "Next";
            // Phase 21: symbol-only buttons ("+", "-", "x") read their name instead ("ZoomIn" becomes "Zoom in").
            bool hasWord = false;
            foreach (char c in t) if (char.IsLetterOrDigit(c)) { hasWord = true; break; }
            if (!hasWord) t = Words(buttonName ?? "");
            // Upper-case UI text reads better as sentence case ("TRICK BOOK" → "Trick book").
            if (t.Length > 1 && t.ToUpperInvariant() == t) t = t.Substring(0, 1) + t.Substring(1).ToLowerInvariant();
            return t;
        }

        /// <summary>"ZoomIn" or "Add_Ledge" → "Zoom in" / "Add ledge".</summary>
        private static string Words(string name)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (c == '_' || c == '-') { if (sb.Length > 0 && sb[sb.Length - 1] != ' ') sb.Append(' '); continue; }
                if (char.IsUpper(c) && i > 0 && sb.Length > 0 && sb[sb.Length - 1] != ' ' && !char.IsUpper(name[i - 1])) sb.Append(' ');
                sb.Append(sb.Length == 0 ? char.ToUpperInvariant(c) : char.ToLowerInvariant(c));
            }
            return sb.ToString().Trim();
        }

#if UNITY_2023_2_OR_NEWER
        private AccessibilityHierarchy _hierarchy;
        private readonly List<Rect> _lastFrames = new List<Rect>();

        private void Update()
        {
            if (!AssistiveSupport.isScreenReaderEnabled)
            {
                if (_hierarchy != null) { AssistiveSupport.activeHierarchy = null; _hierarchy = null; _lastLabels.Clear(); _lastFrames.Clear(); }
                return;
            }
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + RefreshSeconds;
            Rebuild();
        }

        private void Rebuild()
        {
            var labels = new List<(string label, Rect frame)>();
            foreach (var s in Selectable.allSelectablesArray)
            {
                if (s == null || !s.isActiveAndEnabled || !s.IsInteractable() || !(s is Button)) continue;
                var canvas = s.GetComponentInParent<Canvas>();
                if (canvas == null || !canvas.isActiveAndEnabled) continue;
                var text = s.GetComponentInChildren<Text>();
                var rt = (RectTransform)s.transform;
                rt.GetWorldCorners(_corners);
                var cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, _corners[0]);
                Vector2 b = RectTransformUtility.WorldToScreenPoint(cam, _corners[2]);
                var frame = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
                if (frame.width < 4f || frame.xMax < 0f || frame.yMax < 0f || frame.xMin > Screen.width || frame.yMin > Screen.height) continue;
                // Unity screen space has y up; the accessibility frame is measured from the top of the screen.
                frame.y = Screen.height - frame.yMax;
                labels.Add((SpokenLabel(s.name, text != null ? text.text : ""), frame));
            }
            // Reading order: top to bottom, then left to right.
            labels.Sort((x, y) => Mathf.Abs(x.frame.y - y.frame.y) > 20f ? x.frame.y.CompareTo(y.frame.y) : x.frame.x.CompareTo(y.frame.x));

            // Phase 21: also rebuild when a button moves (a scrolled list), not only when the labels change.
            bool same = labels.Count == _lastLabels.Count;
            for (int i = 0; same && i < labels.Count; i++)
                same = labels[i].label == _lastLabels[i]
                       && Mathf.Abs(labels[i].frame.x - _lastFrames[i].x) < 2f && Mathf.Abs(labels[i].frame.y - _lastFrames[i].y) < 2f;
            if (same && _hierarchy != null) return; // nothing new to announce

            _lastLabels.Clear();
            _lastFrames.Clear();
            var h = new AccessibilityHierarchy();
            foreach (var (label, frame) in labels)
            {
                var node = h.AddNode(label);
                node.role = AccessibilityRole.Button;
                node.frame = frame;
                _lastLabels.Add(label);
                _lastFrames.Add(frame);
            }
            _hierarchy = h;
            AssistiveSupport.activeHierarchy = h;
        }
#endif
    }
}

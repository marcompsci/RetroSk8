using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace RetroSk8.UI
{
    /// <summary>Retro Sk8's original UI identity: bold block type, tape-sticker panels, sparing CRT scanlines.</summary>
    public static class Theme
    {
        public static readonly Color Ink = new Color32(0x12, 0x13, 0x17, 0xFF);
        public static readonly Color InkSoft = new Color32(0x12, 0x13, 0x17, 0xB8);
        public static readonly Color Cream = new Color32(0xF3, 0xE9, 0xD2, 0xFF);
        public static readonly Color Tape = new Color32(0xF2, 0xC2, 0x30, 0xFF);

        // Coral/teal mark fail/success; colour-safe mode swaps them for orange/blue, which stay distinct
        // for the common red-green colour-vision types.
        private static readonly Color CoralStd = new Color32(0xFF, 0x5A, 0x4E, 0xFF);
        private static readonly Color TealStd = new Color32(0x1F, 0xC7, 0xB6, 0xFF);
        private static readonly Color CoralSafe = new Color32(0xF5, 0x8A, 0x07, 0xFF);
        private static readonly Color TealSafe = new Color32(0x3A, 0x8D, 0xF0, 0xFF);
        public static bool ColorSafe;
        public static Color Coral => ColorSafe ? CoralSafe : CoralStd;
        public static Color Teal => ColorSafe ? TealSafe : TealStd;

        /// <summary>Multiplier for small text (larger-text option).</summary>
        public static float TextScale = 1f;

        /// <summary>Reads the accessibility settings; call before building UI.</summary>
        public static void ApplySettings(RetroSk8.Save.SettingsData s)
        {
            if (s == null) return;
            ColorSafe = s.colorSafe;
            TextScale = s.largeText ? 1.15f : 1f;
        }
        public static readonly Color White = Color.white;

        // Reference resolution for landscape phones; CanvasScaler matches height so type stays readable on small iPhones.
        public static readonly Vector2 Reference = new Vector2(2340f, 1080f);
    }

    public static class UIFactory
    {
        private static Font s_font;
        private static Sprite s_circle;
        private static Texture2D s_scanlines;

        public static Font Font
        {
            get
            {
                if (s_font == null) s_font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return s_font;
            }
        }

        public static Canvas CreateCanvas(string name, int sortOrder, Transform parent = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            if (parent != null) go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = Theme.Reference;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            go.AddComponent<AdaptiveCanvasScaler>(); // iPads and 16:9 phones lean toward width so wide panels still fit
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public static void EnsureEventSystem()
        {
            RetroSk8.Input.PadNavigator.Ensure(); // game controllers can drive every menu
            if (EventSystem.current != null || UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            UnityEngine.Object.DontDestroyOnLoad(go);
        }

        /// <summary>Full-stretch container that respects the device safe area (notch, home indicator).</summary>
        public static RectTransform SafeArea(Transform canvas)
        {
            var rt = Rect("SafeArea", canvas);
            Stretch(rt);
            rt.gameObject.AddComponent<SafeAreaFitter>();
            return rt;
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static void Stretch(RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }

        public static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        public static Image Panel(string name, Transform parent, Color color, bool raycast = false)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        public static Text Label(string name, Transform parent, string text, int size, Color color,
            TextAnchor align = TextAnchor.MiddleCenter, bool outline = true)
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size < 40 ? Mathf.RoundToInt(size * Theme.TextScale) : size; // headings are big enough already
            t.fontStyle = FontStyle.Bold;
            t.color = color;
            t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            if (outline)
            {
                var o = rt.gameObject.AddComponent<Outline>();
                o.effectColor = Theme.Ink;
                o.effectDistance = new Vector2(3f, -3f);
            }
            return t;
        }

        /// <summary>A strip of "tape" behind a label: slightly rotated cream sticker with ink text.</summary>
        public static Text TapeLabel(string name, Transform parent, string text, int size, Color tape, float tilt)
        {
            var bg = Panel(name, parent, tape);
            bg.rectTransform.localRotation = Quaternion.Euler(0f, 0f, tilt);
            var shadow = bg.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.35f);
            shadow.effectDistance = new Vector2(6f, -6f);
            var label = Label("Text", bg.transform, text, size, Theme.Ink, TextAnchor.MiddleCenter, false);
            Stretch(label.rectTransform, 8f);
            return label;
        }

        public static Button MakeButton(string name, Transform parent, string text, Vector2 size, Color bg, Action onClick, int fontSize = 44)
        {
            var img = Panel(name, parent, bg, true);
            img.rectTransform.sizeDelta = size;
            var shadow = img.gameObject.AddComponent<Shadow>();
            shadow.effectColor = Theme.Ink;
            shadow.effectDistance = new Vector2(8f, -8f);
            var btn = img.gameObject.AddComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = new Color(1f, 1f, 1f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            btn.colors = colors;
            var label = Label("Label", img.transform, text, fontSize, Theme.Ink, TextAnchor.MiddleCenter, false);
            Stretch(label.rectTransform);
            if (onClick != null) btn.onClick.AddListener(() =>
            {
                Audio.AudioManager.Instance?.PlaySfx(Audio.SfxId.UiClick);
                onClick();
            });
            return btn;
        }

        public static Sprite Circle
        {
            get
            {
                if (s_circle != null) return s_circle;
                const int n = 128;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[n * n];
                float r = n * 0.5f - 1f;
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n * 0.5f, n * 0.5f));
                    byte a = (byte)(Mathf.Clamp01(r - d) * 255f);
                    px[y * n + x] = new Color32(255, 255, 255, a);
                }
                tex.SetPixels32(px);
                tex.Apply();
                s_circle = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
                return s_circle;
            }
        }

        /// <summary>CRT scanline overlay. Used sparingly (timer and results header only).</summary>
        public static RawImage Scanlines(Transform parent, float alpha = 0.18f)
        {
            if (s_scanlines == null)
            {
                s_scanlines = new Texture2D(1, 4, TextureFormat.RGBA32, false)
                {
                    wrapMode = TextureWrapMode.Repeat,
                    filterMode = FilterMode.Point,
                };
                s_scanlines.SetPixels32(new[]
                {
                    new Color32(0, 0, 0, 255), new Color32(0, 0, 0, 0), new Color32(0, 0, 0, 0), new Color32(0, 0, 0, 0),
                });
                s_scanlines.Apply();
            }
            var rt = Rect("Scanlines", parent);
            Stretch(rt);
            var img = rt.gameObject.AddComponent<RawImage>();
            img.texture = s_scanlines;
            img.color = new Color(1f, 1f, 1f, alpha);
            img.raycastTarget = false;
            rt.gameObject.AddComponent<ScanlineTiler>();
            return img;
        }
    }

    /// <summary>Keeps scanline density constant regardless of element size.</summary>
    [RequireComponent(typeof(RawImage))]
    public sealed class ScanlineTiler : MonoBehaviour
    {
        public float pixelsPerLine = 6f;

        private void LateUpdate()
        {
            var rt = (RectTransform)transform;
            var img = GetComponent<RawImage>();
            // The texture is 4 rows with one dark row, so one repeat per pixelsPerLine gives one line each pixelsPerLine.
            img.uvRect = new Rect(0f, 0f, 1f, Mathf.Max(1f, rt.rect.height / pixelsPerLine));
        }
    }

    /// <summary>Keeps CanvasScaler's width/height match right for the current screen shape (see RetroSk8.Core.UiScale).</summary>
    [RequireComponent(typeof(CanvasScaler))]
    public sealed class AdaptiveCanvasScaler : MonoBehaviour
    {
        private CanvasScaler _scaler;
        private int _w, _h;

        private void OnEnable()
        {
            _scaler = GetComponent<CanvasScaler>();
            Apply();
        }

        private void Update()
        {
            if (Screen.width != _w || Screen.height != _h) Apply();
        }

        private void Apply()
        {
            _w = Screen.width;
            _h = Screen.height;
            if (_scaler == null) return;
            _scaler.matchWidthOrHeight = RetroSk8.Core.UiScale.MatchFor(_w, _h, RetroSk8.Core.UiScale.MinLogicalWidth, Theme.Reference.x, Theme.Reference.y);
        }
    }

    /// <summary>Resizes a RectTransform to Screen.safeArea.</summary>
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        private Rect _applied;

        private void OnEnable() => Apply();
        private void Update() { if (Screen.safeArea != _applied) Apply(); }

        private void Apply()
        {
            var rt = (RectTransform)transform;
            Rect safe = Screen.safeArea;
            _applied = safe;
            if (Screen.width <= 0 || Screen.height <= 0) return;
            Vector2 min = safe.position;
            Vector2 max = safe.position + safe.size;
            min.x /= Screen.width; min.y /= Screen.height;
            max.x /= Screen.width; max.y /= Screen.height;
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}

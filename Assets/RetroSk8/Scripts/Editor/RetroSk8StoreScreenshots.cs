using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RetroSk8.EditorTools
{
    /// <summary>
    /// Retro Sk8 → Store Screenshot (Phase 15): grabs the Game view in Play mode (UI included) and saves it at the
    /// exact pixel size App Store Connect asks for, as an opaque PNG in StoreScreenshots/ (next to Assets, not in the
    /// build). Set the Game view to a matching aspect first so nothing gets cropped:
    ///   iPhone 6.9" landscape 2868 x 1320 (about 19.5:9)  and  iPad 13" landscape 2752 x 2064 (4:3).
    /// The aspect of the Game view picks which set the shot goes in; anything else is centre-cropped to fit.
    /// </summary>
    public static class RetroSk8StoreScreenshots
    {
        public const string Folder = "StoreScreenshots";

        public static readonly (string set, int w, int h)[] Sizes =
        {
            ("iPhone69", 2868, 1320),
            ("iPad13", 2752, 2064),
        };

        [MenuItem("Retro Sk8/Store Screenshot (Play mode)", priority = 55)]
        public static void Capture()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Retro Sk8", "Enter Play mode, set up the moment you want, then choose this again.\n\nGame view aspect: about 19.5:9 for the iPhone set, 4:3 for the iPad set.", "OK");
                return;
            }
            var go = new GameObject("StoreScreenshotGrabber") { hideFlags = HideFlags.HideAndDontSave };
            go.AddComponent<Grabber>();
        }

        /// <summary>Which set a Game view aspect belongs to (wider than 16:9 → iPhone, else iPad).</summary>
        public static int SetFor(float aspect) => aspect >= 1.75f ? 0 : 1;

        /// <summary>UV scale/offset that centre-crops a source of <paramref name="srcAspect"/> to <paramref name="dstAspect"/>.</summary>
        public static (Vector2 scale, Vector2 offset) Crop(float srcAspect, float dstAspect)
        {
            if (srcAspect > dstAspect)
            {
                float s = dstAspect / srcAspect;
                return (new Vector2(s, 1f), new Vector2((1f - s) * 0.5f, 0f));
            }
            float t = srcAspect / dstAspect;
            return (new Vector2(1f, t), new Vector2(0f, (1f - t) * 0.5f));
        }

        private sealed class Grabber : MonoBehaviour
        {
            private IEnumerator Start()
            {
                yield return new WaitForEndOfFrame();
                Texture2D shot = null;
                RenderTexture rt = null;
                Texture2D output = null;
                try
                {
                    shot = ScreenCapture.CaptureScreenshotAsTexture();
                    float srcAspect = shot.width / (float)shot.height;
                    var (set, w, h) = Sizes[SetFor(srcAspect)];
                    var (scale, offset) = Crop(srcAspect, w / (float)h);
                    rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
                    shot.filterMode = FilterMode.Bilinear;
                    Graphics.Blit(shot, rt, scale, offset);
                    var prev = RenderTexture.active;
                    RenderTexture.active = rt;
                    output = new Texture2D(w, h, TextureFormat.RGB24, false); // no alpha channel: App Store Connect rejects it
                    output.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                    output.Apply();
                    RenderTexture.active = prev;
                    Directory.CreateDirectory(Path.Combine(Folder, set));
                    string path = Path.Combine(Folder, set, $"RetroSk8_{set}_{DateTime.Now:yyyyMMdd_HHmmss}.png");
                    File.WriteAllBytes(path, output.EncodeToPNG());
                    bool cropped = Mathf.Abs(srcAspect - w / (float)h) > 0.02f;
                    Debug.Log($"[RetroSk8] Store screenshot saved: {path} ({w}x{h}){(cropped ? " - the Game view aspect didn't match, so the edges were cropped" : "")}");
                    EditorUtility.RevealInFinder(path);
                }
                finally
                {
                    if (rt != null) RenderTexture.ReleaseTemporary(rt);
                    if (shot != null) Destroy(shot);
                    if (output != null) Destroy(output);
                    Destroy(gameObject);
                }
            }
        }
    }
}

using System;
using System.Collections;
using System.IO;
using System.Runtime.InteropServices;
using RetroSk8.Core;
using UnityEngine;

namespace RetroSk8.Game
{
    /// <summary>
    /// Photo mode 2.0 (Phase 25): grabs the screen at the end of a frame, runs <see cref="PhotoFx"/> on it, writes a PNG
    /// into the app's Photos folder, and on iPhone hands it to the Photos library (add-only permission).
    /// </summary>
    public static class PhotoSaver
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern int RetroSk8_SavePhoto(string path);
        [DllImport("__Internal")] private static extern int RetroSk8_PhotoState();
#endif
        public static string Folder => Path.Combine(Application.persistentDataPath, "Photos");
        public static string LastPath { get; private set; }

        /// <summary>Captures, styles and saves. <paramref name="done"/> gets a short message for the player.</summary>
        public static IEnumerator Capture(PhotoFilter filter, PhotoFrame frame, PhotoStickers stickers, Action<string, Texture2D> done)
        {
            yield return new WaitForEndOfFrame();
            Texture2D tex = null;
            string message;
            try
            {
                tex = ScreenCapture.CaptureScreenshotAsTexture();
                // Convert to plain RGBA so PhotoFx's byte layout is guaranteed whatever the capture format is.
                if (tex.format != TextureFormat.RGBA32)
                {
                    var conv = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
                    conv.SetPixels32(tex.GetPixels32());
                    UnityEngine.Object.Destroy(tex);
                    tex = conv;
                }
                byte[] raw = tex.GetRawTextureData();
                PhotoFx.Apply(raw, tex.width, tex.height, filter, frame, stickers, Environment.TickCount);
                tex.LoadRawTextureData(raw);
                tex.Apply(false);
                Directory.CreateDirectory(Folder);
                LastPath = Path.Combine(Folder, "retrosk8_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png");
                File.WriteAllBytes(LastPath, tex.EncodeToPNG());
                message = SendToPhotos(LastPath) ? "SAVING TO PHOTOS…" : "PHOTO SAVED IN THE APP";
            }
            catch (Exception e)
            {
                Debug.LogWarning("[RetroSk8] Photo capture failed: " + e.Message);
                message = "COULDN'T SAVE THE PHOTO";
            }
            done?.Invoke(message, tex);
        }

        private static bool SendToPhotos(string path)
        {
#if UNITY_IOS && !UNITY_EDITOR
            try { return RetroSk8_SavePhoto(path) == 1; } catch (Exception) { return false; }
#else
            return false;
#endif
        }

        /// <summary>0 idle, 1 saving, 2 saved to Photos, 3 failed (for example, permission denied).</summary>
        public static int PhotosState
        {
            get
            {
#if UNITY_IOS && !UNITY_EDITOR
                try { return RetroSk8_PhotoState(); } catch (Exception) { return 0; }
#else
                return 0;
#endif
            }
        }
    }
}

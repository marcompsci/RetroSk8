using System;
using System.Runtime.InteropServices;
using RetroSk8.Core;
using RetroSk8.Save;
using UnityEngine;

namespace RetroSk8.Game
{
    /// <summary>
    /// The optional App Store cosmetic packs (see <see cref="Shop.Packs"/>): local prices, buying, restoring, and
    /// unlocking what was bought. On iPhone it talks to StoreKit through RetroSk8Store.mm. In the Unity editor a
    /// clearly-labelled TEST STORE unlocks packs without charging anything, so the flow can be tried and tested.
    /// Other platforms show the packs as unavailable. Purchases are checked by the App Store; there is no server.
    /// </summary>
    public static class StoreService
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void RetroSk8_StoreInit(string csvIds);
        [DllImport("__Internal")] private static extern int RetroSk8_StoreCanPay();
        [DllImport("__Internal")] private static extern string RetroSk8_StorePrice(string productId);
        [DllImport("__Internal")] private static extern void RetroSk8_StoreBuy(string productId);
        [DllImport("__Internal")] private static extern void RetroSk8_StoreRestore();
        [DllImport("__Internal")] private static extern string RetroSk8_StorePoll();
        [DllImport("__Internal")] private static extern void RetroSk8_StoreFinish(string productId);
        public static bool IsSupported => true;
        public static bool IsTestStore => false;
#else
        private static void RetroSk8_StoreInit(string csvIds) { }
        private static int RetroSk8_StoreCanPay() => Application.isEditor ? 1 : 0;
        private static string RetroSk8_StorePrice(string productId) => "";
        private static void RetroSk8_StoreBuy(string productId) { if (Application.isEditor) QueueTest("purchased|" + productId + "|"); }
        private static void RetroSk8_StoreRestore() { if (Application.isEditor) QueueTest("restoreDone||"); }
        private static void RetroSk8_StoreFinish(string productId) { }

        // Editor TEST STORE: answers after a short pause, like a real store round trip, and never charges anything.
        private static string s_testLine;
        private static float s_testReadyAt;

        private static void QueueTest(string line)
        {
            s_testLine = line;
            s_testReadyAt = Time.unscaledTime + 0.6f;
        }

        private static string RetroSk8_StorePoll()
        {
            if (s_testLine == null || Time.unscaledTime < s_testReadyAt) return "";
            string line = s_testLine;
            s_testLine = null;
            return line;
        }
        public static bool IsSupported => Application.isEditor;
        public static bool IsTestStore => Application.isEditor;
#endif

        private static bool s_started;
        private static bool s_busy;

        /// <summary>A line for the shop's status bar (purchase results, restore results).</summary>
        public static event Action<string> Message;
        /// <summary>Ownership or prices changed: redraw.</summary>
        public static event Action Changed;

        public static bool Busy => s_busy;
        public static bool CanPay => IsSupported && RetroSk8_StoreCanPay() != 0;

        /// <summary>Starts listening to the App Store (call early: it also delivers purchases left unfinished last time).</summary>
        public static void Init()
        {
            if (s_started) return;
            s_started = true;
            StoreRunner.Ensure();
            if (IsSupported) RetroSk8_StoreInit(string.Join(",", Shop.ProductIds()));
        }

        public static bool Owns(CosmeticPack pack) => pack != null && SaveManager.Data.ownedPacks.Contains(pack.Id);

        /// <summary>The local App Store price, or the fallback until it arrives.</summary>
        public static string Price(CosmeticPack pack)
        {
            if (pack == null) return "";
            if (IsTestStore) return pack.FallbackPrice + " (TEST)";
            string p = IsSupported ? RetroSk8_StorePrice(pack.ProductId) : "";
            return string.IsNullOrEmpty(p) ? pack.FallbackPrice : p;
        }

        public static void Buy(CosmeticPack pack)
        {
            Init();
            if (pack == null || Owns(pack)) return;
            if (!IsSupported) { Message?.Invoke("PACKS ARE AVAILABLE IN THE IPHONE APP."); return; }
            if (!CanPay) { Message?.Invoke("PURCHASES ARE TURNED OFF ON THIS DEVICE (SCREEN TIME)."); return; }
            s_busy = true;
            Message?.Invoke(IsTestStore ? "TEST STORE: UNLOCKING (NO CHARGE)..." : "TALKING TO THE APP STORE...");
            RetroSk8_StoreBuy(pack.ProductId);
            Changed?.Invoke();
        }

        /// <summary>Brings back packs bought on this Apple ID (new phone, reinstall). Required by App Review.</summary>
        public static void Restore()
        {
            Init();
            if (!IsSupported) { Message?.Invoke("RESTORE WORKS IN THE IPHONE APP."); return; }
            s_busy = true;
            Message?.Invoke("RESTORING PURCHASES...");
            RetroSk8_StoreRestore();
            Changed?.Invoke();
        }

        /// <summary>Drains the bridge's event queue (called every frame by <see cref="StoreRunner"/>).</summary>
        internal static void Poll()
        {
            for (int guard = 0; guard < 16; guard++)
            {
                string line = RetroSk8_StorePoll();
                if (string.IsNullOrEmpty(line)) return;
                if (StoreEvent.TryParse(line, out var e)) Handle(e);
            }
        }

        /// <summary>Applies one store event. Public so PlayMode tests can drive the flow.</summary>
        public static void Handle(StoreEvent e)
        {
            var pack = Shop.FindByProduct(e.ProductId);
            switch (e.Kind)
            {
                case StoreEventKind.Purchased:
                case StoreEventKind.Restored:
                    s_busy = false;
                    if (pack != null)
                    {
                        bool isNew = Grant(pack);
                        if (isNew || e.Kind == StoreEventKind.Purchased)
                            Message?.Invoke(e.Kind == StoreEventKind.Restored ? $"RESTORED THE {pack.Name}." : $"UNLOCKED THE {pack.Name}! FIND IT IN SKATER.");
                    }
                    RetroSk8_StoreFinish(e.ProductId); // only after unlocking
                    break;
                case StoreEventKind.Cancelled:
                    s_busy = false;
                    Message?.Invoke("PURCHASE CANCELLED.");
                    break;
                case StoreEventKind.Failed:
                    s_busy = false;
                    Message?.Invoke(string.IsNullOrEmpty(e.Message) ? "THE PURCHASE DIDN'T GO THROUGH." : e.Message.ToUpperInvariant());
                    break;
                case StoreEventKind.Deferred:
                    s_busy = false;
                    Message?.Invoke("WAITING FOR APPROVAL (ASK TO BUY). IT UNLOCKS WHEN APPROVED.");
                    break;
                case StoreEventKind.RestoreFinished:
                    s_busy = false;
                    Message?.Invoke(SaveManager.Data.ownedPacks.Count > 0 ? "RESTORE DONE." : "NOTHING TO RESTORE ON THIS APPLE ID.");
                    break;
                case StoreEventKind.RestoreFailed:
                    s_busy = false;
                    Message?.Invoke("RESTORE FAILED: " + (e.Message ?? "").ToUpperInvariant());
                    break;
            }
            Changed?.Invoke();
        }

        /// <summary>Unlocks a pack. Returns true the first time.</summary>
        public static bool Grant(CosmeticPack pack)
        {
            if (pack == null || SaveManager.Data.ownedPacks.Contains(pack.Id)) return false;
            SaveManager.Data.ownedPacks.Add(pack.Id);
            SaveManager.Save();
            RetroSk8.Audio.AudioManager.Instance?.PlaySfx(RetroSk8.Audio.SfxId.Coin);
            return true;
        }
    }

    /// <summary>Keeps polling the App Store bridge across scenes.</summary>
    public sealed class StoreRunner : MonoBehaviour
    {
        private static StoreRunner s_instance;

        public static void Ensure()
        {
            if (s_instance != null) return;
            var go = new GameObject("StoreRunner");
            DontDestroyOnLoad(go);
            s_instance = go.AddComponent<StoreRunner>();
        }

        private void Update() => StoreService.Poll();
    }
}

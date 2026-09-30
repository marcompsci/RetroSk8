using System;
using System.Collections.Generic;
using System.IO;
using RetroSk8.Data;
using RetroSk8.Player;
using UnityEngine;

namespace RetroSk8.Game
{
    /// <summary>
    /// One live-tunable number: label, range and how to read/write it on the running game.
    /// The tuning panel is generated from this list, so adding a knob is a one-line change.
    /// </summary>
    public sealed class Tunable
    {
        public string Key;
        public string Label;
        public string Group;
        public float Min;
        public float Max;
        public Func<float> Get;
        public Action<float> Set;
        public float Default;
    }

    /// <summary>
    /// Phase 2 feel-tuning: exposes motor, trick, balance and camera values for live editing on device or in the Simulator,
    /// and saves/loads them as JSON so good settings can be copied back into the project defaults.
    /// </summary>
    public sealed class TuningSession
    {
        [Serializable]
        private sealed class Entry
        {
            public string key;
            public float value;
        }

        [Serializable]
        private sealed class Preset
        {
            public int version = 1;
            public List<Entry> values = new List<Entry>();
        }

        private readonly List<Tunable> _tunables = new List<Tunable>();

        public IReadOnlyList<Tunable> Tunables => _tunables;
        public static string PresetPath => Path.Combine(Application.persistentDataPath, "retrosk8_tuning.json");

        public TuningSession(PlayerController player, ScoringProfile profile, CameraRig camera)
        {
            var m = player.motor;
            var grind = player.GetComponent<GrindController>().settings;

            Add("Motor", "cruise", "Cruise speed", 4f, 16f, () => m.cruiseSpeed, v => m.cruiseSpeed = v);
            Add("Motor", "push", "Push speed", 8f, 22f, () => m.pushSpeed, v => m.pushSpeed = v);
            Add("Motor", "accel", "Acceleration", 3f, 25f, () => m.acceleration, v => m.acceleration = v);
            Add("Motor", "turnSlow", "Turn rate (slow)", 60f, 300f, () => m.turnRateSlow, v => m.turnRateSlow = v);
            Add("Motor", "turnFast", "Turn rate (fast)", 40f, 220f, () => m.turnRateFast, v => m.turnRateFast = v);
            Add("Motor", "steerExp", "Steer curve", 1f, 3f, () => m.steerExponent, v => m.steerExponent = v);
            Add("Air", "gravity", "Gravity", 10f, 40f, () => m.gravity, v => m.gravity = v);
            Add("Air", "minPop", "Pop (tap)", 2f, 10f, () => m.minPop, v => m.minPop = v);
            Add("Air", "maxPop", "Pop (full charge)", 5f, 16f, () => m.maxPop, v => m.maxPop = v);
            Add("Air", "charge", "Charge time", 0.1f, 1f, () => m.chargeTime, v => m.chargeTime = v);
            Add("Air", "spin", "Air spin rate", 200f, 900f, () => m.airSpinRate, v => m.airSpinRate = v);
            Add("Air", "jumpBuffer", "Jump buffer (s)", 0f, 0.3f, () => m.jumpBuffer, v => m.jumpBuffer = v);
            Add("Landing", "cleanYaw", "Clean yaw window", 5f, 45f, () => profile.landing.cleanYawError, v => profile.landing.cleanYawError = v);
            Add("Landing", "sketchyYaw", "Sketchy yaw window", 20f, 80f, () => profile.landing.sketchyYawError, v => profile.landing.sketchyYawError = v);
            Add("Landing", "alignAssist", "Landing align assist", 0f, 1f, () => m.landingAlignAssist, v => m.landingAlignAssist = v);
            Add("Landing", "bailImpact", "Wall bail speed", 6f, 25f, () => m.bailImpactSpeed, v => m.bailImpactSpeed = v);
            Add("Balance", "grindTip", "Grind instability", 0.3f, 3f, () => profile.grindBalance.baseTip, v => profile.grindBalance.baseTip = v);
            Add("Balance", "grindControl", "Grind control", 1f, 6f, () => profile.grindBalance.control, v => profile.grindBalance.control = v);
            Add("Balance", "manualTip", "Manual instability", 0.3f, 3f, () => profile.manualBalance.baseTip, v => profile.manualBalance.baseTip = v);
            Add("Balance", "manualControl", "Manual control", 1f, 6f, () => profile.manualBalance.control, v => profile.manualBalance.control = v);
            Add("Balance", "manualWindow", "Manual window (s)", 0.1f, 0.8f, () => profile.manualWindow, v => profile.manualWindow = v);
            Add("Balance", "grindSnap", "Grind snap radius", 0.6f, 3f, () => grind.snapRadius, v => grind.snapRadius = v);
            Add("Camera", "camDist", "Camera distance", 3f, 12f, () => camera.distance, v => camera.distance = v);
            Add("Camera", "camHeight", "Camera height", 0.8f, 6f, () => camera.height, v => camera.height = v);
            Add("Camera", "camFov", "Camera FOV", 45f, 90f, () => camera.fieldOfView, v => camera.fieldOfView = v);
            Add("Camera", "camAir", "Air pull-back", 1f, 1.6f, () => camera.airPullBack, v => camera.airPullBack = v);
            Add("Camera", "camShake", "Landing shake", 0f, 0.04f, () => camera.landingShakePerSpeed, v => camera.landingShakePerSpeed = v);
        }

        private void Add(string group, string key, string label, float min, float max, Func<float> get, Action<float> set)
        {
            _tunables.Add(new Tunable { Group = group, Key = key, Label = label, Min = min, Max = max, Get = get, Set = set, Default = get() });
        }

        public void ResetToDefaults()
        {
            foreach (var t in _tunables) t.Set(t.Default);
        }

        public string ToJson()
        {
            var p = new Preset();
            foreach (var t in _tunables) p.values.Add(new Entry { key = t.Key, value = t.Get() });
            return JsonUtility.ToJson(p, true);
        }

        /// <returns>Number of values applied.</returns>
        public int ApplyJson(string json)
        {
            Preset p;
            try { p = JsonUtility.FromJson<Preset>(json); }
            catch (Exception) { return 0; }
            if (p?.values == null) return 0;
            int applied = 0;
            foreach (var e in p.values)
            {
                var t = _tunables.Find(x => x.Key == e.key);
                if (t == null) continue;
                t.Set(Mathf.Clamp(e.value, t.Min, t.Max));
                applied++;
            }
            return applied;
        }

        public bool Save()
        {
            try
            {
                File.WriteAllText(PresetPath, ToJson());
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[RetroSk8] Could not save tuning preset: " + e.Message);
                return false;
            }
        }

        public int LoadSaved()
        {
            try { return File.Exists(PresetPath) ? ApplyJson(File.ReadAllText(PresetPath)) : 0; }
            catch (Exception) { return 0; }
        }

        public static void DeleteSaved()
        {
            try { if (File.Exists(PresetPath)) File.Delete(PresetPath); }
            catch (Exception) { /* nothing to clean up */ }
        }
    }
}

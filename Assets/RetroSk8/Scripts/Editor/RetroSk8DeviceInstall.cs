using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using RetroSk8.Core;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace RetroSk8.EditorTools
{
    /// <summary>
    /// Retro Sk8 → Build iOS → Install on Connected iPhone (Phase 29): takes the last TestFlight archive
    /// (Builds/RetroSk8.xcarchive), exports a development-signed .ipa, finds the iPhone plugged into (or paired with)
    /// this Mac with <c>xcrun devicectl</c>, installs the app and launches it. Runs in the background like the
    /// TestFlight job; output goes to Temp/RetroSk8Xcode.log and a summary to Temp/RetroSk8DeviceReport.txt.
    /// Remote bridge: <c>device</c>. Needs Developer Mode on the phone and the phone trusted by this Mac.
    /// </summary>
    [InitializeOnLoad]
    public static class RetroSk8DeviceInstall
    {
        public const string ReportPath = "Temp/RetroSk8DeviceReport.txt";
        private const string DevicesJson = "Temp/RetroSk8Devices.json";
        private const string ExportOptionsPath = "Builds/DevExportOptions.plist";
        private const string ExportPath = "Builds/Device";
        private const string BundleId = "com.omariibell.retrosk8";

        private static Process s_process;
        private static string s_stage;
        private static string s_device;
        private static string s_deviceName;
        private static DateTime s_started;
        private static readonly ConcurrentQueue<string> s_lines = new ConcurrentQueue<string>();
        private static readonly List<string> s_output = new List<string>();

        public static bool Busy => s_process != null;

        static RetroSk8DeviceInstall() { EditorApplication.update += Pump; }

        [MenuItem("Retro Sk8/Build iOS/Install on Connected iPhone (last archive)", priority = 72)]
        private static void Menu() => Start();

        public static string Start()
        {
            if (Busy) return "device: already running (" + s_stage + ")";
            if (RetroSk8TestFlight.Busy) return "device: a TestFlight job is running; try again when it finishes";
            string team = RetroSk8BuildOptions.TeamId;
            if (!TestFlightRules.IsValidTeamId(team)) return Finish(false, "No valid Team ID.");
#if !UNITY_EDITOR_OSX
            return Finish(false, "Installing on an iPhone needs a Mac (xcrun devicectl).");
#else
            if (!Directory.Exists(TestFlightRules.ArchivePath))
                return Finish(false, "No archive at " + TestFlightRules.ArchivePath + ": run testflight first.");
            s_output.Clear();
            s_started = DateTime.Now;
            s_device = null;
            s_deviceName = null;
            try
            {
                Directory.CreateDirectory("Temp");
                File.WriteAllText(RetroSk8TestFlight.XcodeLogPath, "");
                if (File.Exists(DevicesJson)) File.Delete(DevicesJson);
                File.WriteAllText(ExportOptionsPath, DevExportOptions(team));
                if (Directory.Exists(ExportPath)) Directory.Delete(ExportPath, true);
            }
            catch (IOException e) { return Finish(false, "Could not write build files: " + e.Message); }
            return Run("find iPhone", "devicectl list devices --json-output " + DevicesJson);
#endif
        }

        private static string DevExportOptions(string team) =>
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
            "<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">\n" +
            "<plist version=\"1.0\">\n<dict>\n" +
            "\t<key>method</key>\n\t<string>development</string>\n" +
            "\t<key>teamID</key>\n\t<string>" + team + "</string>\n" +
            "\t<key>signingStyle</key>\n\t<string>automatic</string>\n" +
            "\t<key>compileBitcode</key>\n\t<false/>\n" +
            "</dict>\n</plist>\n";

        private static string Run(string stage, string arguments)
        {
            s_stage = stage;
            var info = new ProcessStartInfo
            {
                FileName = "/usr/bin/xcrun",
                Arguments = arguments,
                WorkingDirectory = Directory.GetCurrentDirectory(),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            try
            {
                var p = new Process { StartInfo = info, EnableRaisingEvents = true };
                p.OutputDataReceived += (_, e) => { if (e.Data != null) s_lines.Enqueue(e.Data); };
                p.ErrorDataReceived += (_, e) => { if (e.Data != null) s_lines.Enqueue(e.Data); };
                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                s_process = p;
            }
            catch (Exception e) { return Finish(false, "Could not start xcrun: " + e.Message); }
            string msg = "device: " + stage + " started";
            RetroSk8RemoteBridge.Log(msg);
            return msg;
        }

        private static void Pump()
        {
            if (s_lines.IsEmpty && s_process == null) return;
            var sb = new StringBuilder();
            while (s_lines.TryDequeue(out var line)) { s_output.Add(line); sb.AppendLine(line); }
            if (sb.Length > 0) { try { File.AppendAllText(RetroSk8TestFlight.XcodeLogPath, sb.ToString()); } catch (IOException) { } }
            if (s_process == null || !s_process.HasExited || !s_lines.IsEmpty) return;

            int code = s_process.ExitCode;
            string stage = s_stage;
            s_process.Dispose();
            s_process = null;

            switch (stage)
            {
                case "find iPhone":
                    if (code != 0) { Finish(false, "xcrun devicectl failed (exit " + code + "). Is Xcode 15 or later installed?"); return; }
                    if (!PickDevice()) return;
                    Run("export (development signing)",
                        "xcodebuild -exportArchive -archivePath " + TestFlightRules.ArchivePath + " -exportOptionsPlist " + ExportOptionsPath +
                        " -exportPath " + ExportPath + " -allowProvisioningUpdates");
                    return;
                case "export (development signing)":
                    if (code != 0) { Finish(false, "Development export failed (exit " + code + "). Usually the iPhone isn't registered to the team yet: open Xcode → Window → Devices and Simulators with the phone plugged in, then try again."); return; }
                    string ipa = FindIpa();
                    if (ipa == null) { Finish(false, "Export finished but no .ipa was found in " + ExportPath + "."); return; }
                    Run("install on " + s_deviceName, "devicectl device install app --device " + s_device + " \"" + ipa + "\"");
                    return;
                default:
                    if (stage.StartsWith("install"))
                    {
                        if (code != 0) { Finish(false, "Install failed (exit " + code + "). Unlock the phone, check Settings → Privacy & Security → Developer Mode is on, and that it trusts this Mac."); return; }
                        Run("launch", "devicectl device process launch --device " + s_device + " " + BundleId);
                        return;
                    }
                    if (stage == "launch")
                    {
                        Finish(true, code == 0
                            ? "Installed and launched Retro Sk8 on " + s_deviceName + "."
                            : "Installed on " + s_deviceName + ". It didn't open by itself (phone locked?): tap the Retro Sk8 icon.");
                        return;
                    }
                    Finish(code == 0, stage + " finished (exit " + code + ")");
                    return;
            }
        }

        [Serializable] private class DeviceList { public DeviceResult result; }
        [Serializable] private class DeviceResult { public DeviceEntry[] devices; }
        [Serializable] private class DeviceEntry
        {
            public string identifier;
            public DeviceProps deviceProperties;
            public HardwareProps hardwareProperties;
            public ConnectionProps connectionProperties;
        }
        [Serializable] private class DeviceProps { public string name; }
        [Serializable] private class HardwareProps { public string platform; public string reality; public string deviceType; }
        [Serializable] private class ConnectionProps { public string pairingState; public string tunnelState; public string transportType; }

        private static bool PickDevice()
        {
            DeviceList list;
            try { list = JsonUtility.FromJson<DeviceList>(File.ReadAllText(DevicesJson)); }
            catch (Exception e) { Finish(false, "Couldn't read the device list: " + e.Message); return false; }
            var seen = new List<string>();
            DeviceEntry best = null;
            int bestScore = -1;
            if (list?.result?.devices != null)
            {
                foreach (var d in list.result.devices)
                {
                    if (d?.hardwareProperties == null || d.hardwareProperties.platform != "iOS") continue;
                    if (d.hardwareProperties.reality == "virtual") continue;
                    var c = d.connectionProperties ?? new ConnectionProps();
                    string name = d.deviceProperties?.name ?? d.identifier;
                    seen.Add(name + " (" + (c.pairingState ?? "?") + ", " + (c.tunnelState ?? "?") + ", " + (c.transportType ?? "?") + ")");
                    if (c.pairingState != "paired") continue;
                    // Prefer a cable, then a live tunnel, then anything paired.
                    int score = c.transportType == "wired" ? 3 : c.tunnelState == "connected" ? 2 : c.tunnelState == "disconnected" ? 0 : 1;
                    if (score > bestScore) { best = d; bestScore = score; }
                }
            }
            if (best == null || bestScore == 0)
            {
                Finish(false, seen.Count == 0
                    ? "No iPhone found. Plug it into the Mac with a cable, unlock it and tap Trust."
                    : "No reachable iPhone. Seen: " + string.Join("; ", seen) + ". Plug it in with a cable and unlock it.");
                return false;
            }
            s_device = best.identifier;
            s_deviceName = best.deviceProperties?.name ?? "the iPhone";
            RetroSk8RemoteBridge.Log("device: found " + s_deviceName);
            return true;
        }

        private static string FindIpa()
        {
            if (!Directory.Exists(ExportPath)) return null;
            var files = Directory.GetFiles(ExportPath, "*.ipa", SearchOption.AllDirectories);
            return files.Length > 0 ? files[0] : null;
        }

        private static string Finish(bool ok, string message)
        {
            s_stage = null;
            var sb = new StringBuilder();
            sb.AppendLine((ok ? "DEVICE INSTALL SUCCEEDED  " : "DEVICE INSTALL FAILED  ") + DateTime.Now.ToString("yyyy-MM-dd HH:mm")
                + "  version " + PlayerSettings.bundleVersion + " (" + PlayerSettings.iOS.buildNumber + ")");
            if (s_started != default) sb.AppendLine("time " + (DateTime.Now - s_started).TotalMinutes.ToString("0.0") + " min");
            sb.AppendLine(message);
            foreach (var line in TestFlightRules.ImportantLines(s_output)) sb.AppendLine("  " + line);
            try { Directory.CreateDirectory("Temp"); File.WriteAllText(ReportPath, sb.ToString()); } catch (IOException) { }
            RetroSk8RemoteBridge.Log("device: " + (ok ? "done" : "failed") + " - " + message + " (report: " + ReportPath + ")");
            if (ok) Debug.Log("[RetroSk8] " + sb); else Debug.LogWarning("[RetroSk8] " + sb);
            return message;
        }
    }
}

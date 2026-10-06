using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;

namespace RetroSk8.EditorTools
{
    /// <summary>
    /// Retro Sk8 → Build iOS → Check Native Plugins (Phase 19; bridge: <c>native-check</c>). Compiles every iOS plugin
    /// in Assets/RetroSk8/Plugins/iOS against the real iPhone SDK without building the whole app: Objective-C++ files
    /// with clang (-fsyntax-only) and Swift files with swiftc (-typecheck). Results go to Temp/RetroSk8NativeCheck.txt.
    /// Unity's own export never compiles these, so this is the quick way to know a plugin change will build in Xcode.
    /// </summary>
    public static class RetroSk8NativeCheck
    {
        public const string PluginDir = "Assets/RetroSk8/Plugins/iOS";
        public const string ReportPath = "Temp/RetroSk8NativeCheck.txt";
        public const string MinIOS = "15.0";

        [MenuItem("Retro Sk8/Build iOS/Check Native Plugins", priority = 86)]
        private static void Menu() => UnityEngine.Debug.Log("[RetroSk8] " + Run());

        /// <summary>Checks every plugin; returns a one-line summary.</summary>
        public static string Run()
        {
#if !UNITY_EDITOR_OSX
            return Write("NATIVE CHECK NOT RUN: needs a Mac with Xcode.");
#else
            if (!Directory.Exists(PluginDir)) return Write("NATIVE CHECK NOT RUN: no " + PluginDir);
            var (sdkOk, sdk) = Exec("xcrun --sdk iphoneos --show-sdk-path");
            if (!sdkOk || string.IsNullOrWhiteSpace(sdk)) return Write("NATIVE CHECK NOT RUN: iPhone SDK not found (is Xcode installed?)\n" + sdk);
            sdk = sdk.Trim();
            var sb = new StringBuilder();
            int failed = 0, total = 0;
            foreach (var file in Directory.GetFiles(PluginDir))
            {
                string ext = Path.GetExtension(file).ToLowerInvariant();
                string args;
                if (ext == ".mm" || ext == ".m")
                    args = $"--sdk iphoneos clang++ -fsyntax-only -x objective-c++ -std=c++17 -target arm64-apple-ios{MinIOS} -isysroot \"{sdk}\" \"{Path.GetFullPath(file)}\"";
                else if (ext == ".swift")
                    args = $"--sdk iphoneos swiftc -typecheck -swift-version 5 -target arm64-apple-ios{MinIOS} -sdk \"{sdk}\" \"{Path.GetFullPath(file)}\"";
                else continue;
                total++;
                var (ok, output) = Exec("xcrun " + args);
                if (!ok) failed++;
                sb.AppendLine((ok ? "[ OK ] " : "[FAIL] ") + Path.GetFileName(file));
                if (!ok || output.IndexOf("warning", StringComparison.OrdinalIgnoreCase) >= 0)
                    foreach (var line in output.Split('\n'))
                        if (line.Contains("error") || line.Contains("warning")) sb.AppendLine("       " + line.Trim());
            }
            string head = failed == 0 ? $"NATIVE CHECK PASSED: {total} plugins compile for iOS {MinIOS}" : $"NATIVE CHECK FAILED: {failed} of {total} plugins";
            return Write(head + "\n" + sb);
#endif
        }

        private static (bool ok, string output) Exec(string commandLine)
        {
            int space = commandLine.IndexOf(' ');
            var info = new ProcessStartInfo("/usr/bin/" + commandLine.Substring(0, space), commandLine.Substring(space + 1))
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            try
            {
                using (var p = Process.Start(info))
                {
                    var o = p.StandardOutput.ReadToEndAsync();
                    var e = p.StandardError.ReadToEndAsync();
                    if (!p.WaitForExit(120000)) { try { p.Kill(); } catch (InvalidOperationException) { } return (false, "timed out"); }
                    return (p.ExitCode == 0, (o.Result + "\n" + e.Result).Trim());
                }
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        private static string Write(string text)
        {
            try
            {
                Directory.CreateDirectory("Temp");
                File.WriteAllText(ReportPath, text + "\n");
            }
            catch (IOException) { }
            int nl = text.IndexOf('\n');
            return nl > 0 ? text.Substring(0, nl) : text;
        }
    }
}

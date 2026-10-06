using System;
using System.Collections.Generic;
using System.Text;

namespace RetroSk8.Core
{
    /// <summary>
    /// Engine-free pieces of the TestFlight pipeline (Phase 18): Team ID checks, the export options Xcode needs to
    /// upload an archive to App Store Connect, the xcodebuild argument lists, and reading xcodebuild's output for the
    /// lines a person needs to see. The editor side (RetroSk8TestFlight) runs the commands.
    /// </summary>
    public static class TestFlightRules
    {
        public const string ArchivePath = "Builds/RetroSk8.xcarchive";
        public const string ExportPath = "Builds/TestFlight";
        public const string ExportOptionsPath = "Builds/ExportOptions.plist";
        public const string XcodeProject = "Builds/iOS-Device/Unity-iPhone.xcodeproj";

        /// <summary>A Team ID is exactly 10 capital letters or digits.</summary>
        public static bool IsValidTeamId(string id)
        {
            if (string.IsNullOrEmpty(id) || id.Length != 10) return false;
            foreach (char c in id)
                if (!((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9'))) return false;
            return true;
        }

        /// <summary>
        /// ExportOptions.plist for an App Store Connect upload: automatic signing with the team, symbols included,
        /// and Xcode leaves the version and build number alone (Unity already bumps the build number).
        /// </summary>
        public static string ExportOptions(string teamId, bool upload)
        {
            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n");
            sb.Append("<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">\n");
            sb.Append("<plist version=\"1.0\">\n<dict>\n");
            Key(sb, "method", "app-store-connect");
            Key(sb, "destination", upload ? "upload" : "export");
            Key(sb, "teamID", teamId);
            Key(sb, "signingStyle", "automatic");
            sb.Append("\t<key>uploadSymbols</key>\n\t<true/>\n");
            sb.Append("\t<key>manageAppVersionAndBuildNumber</key>\n\t<false/>\n");
            sb.Append("</dict>\n</plist>\n");
            return sb.ToString();
        }

        private static void Key(StringBuilder sb, string key, string value) =>
            sb.Append("\t<key>").Append(key).Append("</key>\n\t<string>").Append(Escape(value)).Append("</string>\n");

        private static string Escape(string s) =>
            (s ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

        /// <summary>xcodebuild arguments that archive the exported Unity project (Release, any iPhone, automatic signing).</summary>
        public static string[] ArchiveArgs(string teamId) => new[]
        {
            "-project", XcodeProject,
            "-scheme", "Unity-iPhone",
            "-configuration", "Release",
            "-destination", "generic/platform=iOS",
            "-archivePath", ArchivePath,
            "-allowProvisioningUpdates",
            "DEVELOPMENT_TEAM=" + teamId,
            "CODE_SIGN_STYLE=Automatic",
            "archive",
        };

        /// <summary>xcodebuild arguments that export (and, with destination upload, send) the archive to App Store Connect.</summary>
        public static string[] ExportArgs() => new[]
        {
            "-exportArchive",
            "-archivePath", ArchivePath,
            "-exportOptionsPlist", ExportOptionsPath,
            "-exportPath", ExportPath,
            "-allowProvisioningUpdates",
        };

        /// <summary>Joins arguments for a shell, quoting the ones with spaces.</summary>
        public static string Join(IList<string> args)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < args.Count; i++)
            {
                if (i > 0) sb.Append(' ');
                string a = args[i] ?? "";
                if (a.IndexOf(' ') >= 0 || a.Length == 0) sb.Append('"').Append(a.Replace("\"", "\\\"")).Append('"');
                else sb.Append(a);
            }
            return sb.ToString();
        }

        /// <summary>
        /// The lines of xcodebuild output a person needs: errors, signing and provisioning problems, upload results.
        /// Duplicates are dropped and the list is capped.
        /// </summary>
        public static List<string> ImportantLines(IEnumerable<string> output, int max = 40)
        {
            var found = new List<string>();
            var seen = new HashSet<string>();
            if (output == null) return found;
            foreach (var raw in output)
            {
                if (string.IsNullOrEmpty(raw)) continue;
                string line = raw.Trim();
                if (!IsImportant(line) || !seen.Add(line)) continue;
                found.Add(line.Length > 400 ? line.Substring(0, 400) + "…" : line);
                if (found.Count >= max) break;
            }
            return found;
        }

        private static bool IsImportant(string line)
        {
            string l = line.ToLowerInvariant();
            if (l.StartsWith("error:") || l.Contains(" error:")) return true;
            if (l.Contains("** archive") || l.Contains("** export")) return true; // ** ARCHIVE SUCCEEDED ** etc.
            if (l.Contains("provisioning profile") || l.Contains("no account")) return true;
            if (l.Contains("signing") && (l.Contains("requires") || l.Contains("failed") || l.Contains("error"))) return true;
            if (l.Contains("upload succeeded") || l.Contains("uploaded") || l.Contains("upload failed")) return true;
            if (l.Contains("exportarchive")) return true;
            return l.Contains("no suitable application records") || l.Contains("bundle version must be higher");
        }

        /// <summary>A plain-English next step for the commonest failures, or null.</summary>
        public static string Advice(IEnumerable<string> output)
        {
            if (output == null) return null;
            foreach (var raw in output)
            {
                string l = (raw ?? "").ToLowerInvariant();
                if (l.Contains("no accounts") || l.Contains("no account for team"))
                    return "Xcode isn't signed in to your Apple developer account: Xcode → Settings → Accounts → + → Apple ID.";
                if (l.Contains("no suitable application records") || l.Contains("cannot determine the apple id")
                    || l.Contains("error downloading app information")) // Phase 20: what xcodebuild says before the app record exists
                    return "App Store Connect has no app with this bundle id yet: create it (Apps → + → New App), then upload again.";
                if (l.Contains("bundle version must be higher") || l.Contains("cfbundleversion"))
                    return "That build number was already uploaded: run the archive again (every build bumps it).";
                if (l.Contains("failed to register bundle identifier") || l.Contains("is not available"))
                    return "Another developer owns this bundle id: change it in Player Settings (e.g. add .game) and in App Store Connect.";
                if (l.Contains("requires a development team"))
                    return "No Team ID: set it with Retro Sk8 → Build iOS → TestFlight → Use Team ID.";
                if (l.Contains("agreement") && (l.Contains("expired") || l.Contains("not been accepted") || l.Contains("must accept")))
                    return "Apple needs you to accept an updated agreement: developer.apple.com → Account, then App Store Connect → Business.";
            }
            return null;
        }
    }
}

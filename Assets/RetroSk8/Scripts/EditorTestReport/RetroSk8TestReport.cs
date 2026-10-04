using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace RetroSk8.EditorTools
{
    /// <summary>
    /// Writes every Test Runner result (EditMode and PlayMode) to Temp/RetroSk8TestReport.txt: totals on the first
    /// line, then each failure with its message. Retro Sk8 → Run All Tests runs both suites in one go.
    /// The callbacks are re-registered after every domain reload, so PlayMode runs are captured too.
    /// </summary>
    [InitializeOnLoad]
    public static class RetroSk8TestReport
    {
        static RetroSk8TestReport()
        {
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new Writer());
            RetroSk8RemoteBridge.RunTests = RunAll;
        }

        // Run All Tests runs EditMode, then PlayMode (one Execute call with both modes only ran EditMode in Unity 6),
        // and the report merges both. The stage survives the PlayMode domain reload in SessionState.
        private const string StageKey = "RetroSk8.Tests.Stage";       // 0 none, 1 EditMode running, 2 PlayMode running
        private const string EditResultKey = "RetroSk8.Tests.EditResult";

        [MenuItem("Retro Sk8/Run All Tests (writes report)", priority = 51)]
        public static void RunAll()
        {
            SessionState.SetInt(StageKey, 1);
            SessionState.EraseString(EditResultKey);
            Execute(TestMode.EditMode);
            Debug.Log("[RetroSk8] Running EditMode, then PlayMode tests. The report is written to " + RetroSk8TestReportPaths.Report);
        }

        private static void Execute(TestMode mode)
        {
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.Execute(new ExecutionSettings(new Filter { testMode = mode }));
        }

        private sealed class Writer : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }

            public void RunFinished(ITestResultAdaptor result)
            {
                int passed = 0, failed = 0, skipped = 0;
                var failures = new StringBuilder();
                Walk(result, ref passed, ref failed, ref skipped, failures);
                int stage = SessionState.GetInt(StageKey, 0);

                if (stage == 1)
                {
                    // EditMode done: keep its numbers and start PlayMode on the next editor tick.
                    SessionState.SetString(EditResultKey, $"{passed}|{failed}|{skipped}|{failures}");
                    SessionState.SetInt(StageKey, 2);
                    RetroSk8RemoteBridge.Log($"EditMode finished: {passed} passed, {failed} failed; starting PlayMode");
                    EditorApplication.delayCall += () => Execute(TestMode.PlayMode);
                    return;
                }

                string modes = "Test Runner";
                if (stage == 2)
                {
                    modes = "EditMode+PlayMode";
                    var parts = SessionState.GetString(EditResultKey, "0|0|0|").Split(new[] { '|' }, 4);
                    if (parts.Length == 4)
                    {
                        int.TryParse(parts[0], out int ep); int.TryParse(parts[1], out int ef); int.TryParse(parts[2], out int es);
                        RetroSk8RemoteBridge.Log($"PlayMode finished: {passed} passed, {failed} failed");
                        passed += ep; failed += ef; skipped += es;
                        failures.Insert(0, parts[3]);
                    }
                    SessionState.SetInt(StageKey, 0);
                    SessionState.EraseString(EditResultKey);
                }

                var sb = new StringBuilder();
                sb.AppendLine($"TESTS {System.DateTime.Now:yyyy-MM-dd HH:mm}  PASSED {passed}  FAILED {failed}  SKIPPED {skipped}  ({modes})");
                sb.Append(failures);
                try
                {
                    Directory.CreateDirectory("Temp");
                    File.WriteAllText(RetroSk8TestReportPaths.Report, sb.ToString());
                }
                catch (IOException e)
                {
                    Debug.LogWarning("[RetroSk8] Could not write the test report: " + e.Message);
                }
                Debug.Log("[RetroSk8] " + sb);
                RetroSk8RemoteBridge.OnTestsFinished();
            }

            private static void Walk(ITestResultAdaptor r, ref int passed, ref int failed, ref int skipped, StringBuilder failures)
            {
                if (r.HasChildren)
                {
                    foreach (var c in r.Children) Walk(c, ref passed, ref failed, ref skipped, failures);
                    return;
                }
                switch (r.TestStatus)
                {
                    case TestStatus.Passed: passed++; break;
                    case TestStatus.Failed:
                        failed++;
                        failures.AppendLine("FAIL " + r.FullName);
                        if (!string.IsNullOrEmpty(r.Message)) failures.AppendLine("     " + r.Message.Trim().Replace("\n", "\n     "));
                        break;
                    default: skipped++; break;
                }
            }
        }
    }
}

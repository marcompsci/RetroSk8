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
        }

        [MenuItem("Retro Sk8/Run All Tests (writes report)", priority = 51)]
        public static void RunAll()
        {
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode }, new Filter { testMode = TestMode.PlayMode }));
            Debug.Log("[RetroSk8] Running EditMode + PlayMode tests. The report is written to " + RetroSk8TestReportPaths.Report);
        }

        private sealed class Writer : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }

            public void RunFinished(ITestResultAdaptor result)
            {
                int passed = 0, failed = 0, skipped = 0;
                var modes = new HashSet<string>();
                var failures = new StringBuilder();
                Walk(result, ref passed, ref failed, ref skipped, modes, failures);
                var sb = new StringBuilder();
                sb.AppendLine($"TESTS {System.DateTime.Now:yyyy-MM-dd HH:mm}  PASSED {passed}  FAILED {failed}  SKIPPED {skipped}  ({string.Join("+", new List<string>(modes).ToArray())})");
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
            }

            private static void Walk(ITestResultAdaptor r, ref int passed, ref int failed, ref int skipped, HashSet<string> modes, StringBuilder failures)
            {
                if (r.HasChildren)
                {
                    foreach (var c in r.Children) Walk(c, ref passed, ref failed, ref skipped, modes, failures);
                    return;
                }
                modes.Add(r.Test.TestMode.ToString());
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

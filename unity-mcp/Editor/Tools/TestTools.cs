using System.Collections.Generic;
using System.Linq;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityMcp.Shared.Attributes;
using UnityMcp.Shared.Models;
using UnityMcp.Shared.Utils;

namespace UnityMcp.Editor.Tools
{
    [McpToolGroup("Test")]
    public static class TestTools
    {
        [McpTool("test_run", "Run Unity tests (EditMode or PlayMode)",
            Group = "test")]
        public static ToolResult Run(
            [Desc("Test mode: 'EditMode' or 'PlayMode'")] string testMode = "EditMode",
            [Desc("Specific test name filter (optional)")] string testFilter = null)
        {
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            var filter = new Filter
            {
                testMode = testMode.ToLower().Contains("play")
                    ? TestMode.PlayMode
                    : TestMode.EditMode
            };

            if (!string.IsNullOrEmpty(testFilter))
                filter.testNames = new[] { testFilter };

            var callbacks = new TestCallbacks();
            api.RegisterCallbacks(callbacks);
            api.Execute(new ExecutionSettings(filter));

            return ToolResult.Json(new
            {
                success = true,
                message = $"Test run started ({testMode})",
                filter = testFilter ?? "(all)"
            });
        }

        [McpTool("test_get_results", "Get the latest test run results",
            Group = "test", ReadOnly = true)]
        public static ToolResult GetResults(
            [Desc("Test mode: 'EditMode' or 'PlayMode'")] string testMode = "EditMode")
        {
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            var mode = testMode.ToLower().Contains("play")
                ? TestMode.PlayMode
                : TestMode.EditMode;

            var results = new List<object>();
            api.RetrieveTestList(mode, (testRoot) =>
            {
                CollectResults(testRoot, results);
            });

            return ToolResult.Json(new
            {
                testMode,
                count = results.Count,
                results
            });
        }

        private static void CollectResults(ITestAdaptor test, List<object> results)
        {
            if (!test.HasChildren && test.RunState != RunState.NotRunnable)
            {
                results.Add(new
                {
                    name = test.Name,
                    fullName = test.FullName,
                    runState = test.RunState.ToString(),
                });
            }

            if (test.Children != null)
            {
                foreach (var child in test.Children)
                    CollectResults(child, results);
            }
        }

        private class TestCallbacks : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void RunFinished(ITestResultAdaptor result)
            {
                McpLogger.Info($"Test run finished: {result.TestStatus} " +
                              $"(Passed: {result.PassCount}, Failed: {result.FailCount}, " +
                              $"Skipped: {result.SkipCount})");
            }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }
        }
    }
}

using System;
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
        private static readonly object s_stateLock = new();
        private static TestRunnerApi s_activeApi;
        private static TestCallbacks s_activeCallbacks;
        private static TestRunState s_latestRun = TestRunState.CreateIdle();

        [McpTool("test_run", "Run Unity tests (EditMode or PlayMode)",
            Group = "test")]
        public static ToolResult Run(
            [Desc("Test mode: 'EditMode' or 'PlayMode'")] string testMode = "EditMode",
            [Desc("Specific test name filter (optional)")] string testFilter = null)
        {
            var mode = ParseMode(testMode);

            lock (s_stateLock)
            {
                if (s_latestRun.IsRunning)
                {
                    return ToolResult.Error(
                        $"A {s_latestRun.TestMode} test run is already in progress.",
                        "test_run_in_progress");
                }

                s_latestRun = TestRunState.CreateRunning(mode, testFilter);
            }

            s_activeApi = ScriptableObject.CreateInstance<TestRunnerApi>();
            s_activeApi.hideFlags = HideFlags.HideAndDontSave;

            s_activeCallbacks = new TestCallbacks();
            s_activeApi.RegisterCallbacks(s_activeCallbacks);

            var filter = new Filter
            {
                testMode = mode
            };

            if (!string.IsNullOrEmpty(testFilter))
                filter.testNames = new[] { testFilter };

            try
            {
                s_activeApi.Execute(new ExecutionSettings(filter));
            }
            catch (Exception ex)
            {
                CleanupActiveRun();

                lock (s_stateLock)
                {
                    s_latestRun = TestRunState.CreateFailedToStart(mode, testFilter, ex.Message);
                }

                return ToolResult.Error($"Failed to start test run: {ex.Message}", "test_run_failed");
            }

            return ToolResult.Json(new
            {
                status = "running",
                testMode = mode.ToString(),
                filter = testFilter ?? "(all)",
                startedAtUtc = s_latestRun.StartedAtUtc
            });
        }

        [McpTool("test_get_results", "Get the latest test run results",
            Group = "test", ReadOnly = true)]
        public static ToolResult GetResults(
            [Desc("Test mode: 'EditMode' or 'PlayMode'")] string testMode = "EditMode")
        {
            var mode = ParseMode(testMode);
            TestRunState snapshot;

            lock (s_stateLock)
            {
                snapshot = s_latestRun.Clone();
            }

            if (!string.Equals(snapshot.TestMode, mode.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return ToolResult.Json(new
                {
                    status = "idle",
                    requestedMode = mode.ToString(),
                    hasResults = false,
                    message = $"No cached results for {mode}."
                });
            }

            return ToolResult.Json(new
            {
                status = snapshot.Status,
                testMode = snapshot.TestMode,
                filter = snapshot.Filter,
                hasResults = snapshot.Results.Count > 0,
                startedAtUtc = snapshot.StartedAtUtc,
                finishedAtUtc = snapshot.FinishedAtUtc,
                durationSeconds = snapshot.DurationSeconds,
                summary = snapshot.Summary,
                count = snapshot.Results.Count,
                results = snapshot.Results
            });
        }

        private static TestMode ParseMode(string testMode)
        {
            return testMode != null && testMode.ToLower().Contains("play")
                ? TestMode.PlayMode
                : TestMode.EditMode;
        }

        private static void CollectResults(ITestResultAdaptor test, List<TestCaseResult> results)
        {
            if (!test.HasChildren)
            {
                results.Add(new TestCaseResult
                {
                    Name = test.Name,
                    FullName = test.FullName,
                    Status = test.TestStatus.ToString(),
                    DurationSeconds = test.Duration,
                    Message = string.IsNullOrWhiteSpace(test.Message) ? null : test.Message,
                    StackTrace = string.IsNullOrWhiteSpace(test.StackTrace) ? null : test.StackTrace,
                    Output = string.IsNullOrWhiteSpace(test.Output) ? null : test.Output
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
                var results = new List<TestCaseResult>();
                CollectResults(result, results);

                lock (s_stateLock)
                {
                    var finishedAtUtc = DateTime.UtcNow;
                    s_latestRun.IsRunning = false;
                    s_latestRun.Status = result.FailCount > 0 ? "failed" : "completed";
                    s_latestRun.FinishedAtUtc = finishedAtUtc.ToString("O");
                    s_latestRun.DurationSeconds = (finishedAtUtc - s_latestRun.StartedAtUtcValue).TotalSeconds;
                    s_latestRun.Summary = new TestRunSummary
                    {
                        Status = result.TestStatus.ToString(),
                        Passed = result.PassCount,
                        Failed = result.FailCount,
                        Skipped = result.SkipCount,
                        Inconclusive = result.InconclusiveCount,
                    };
                    s_latestRun.Results = results;
                }

                McpLogger.Info($"Test run finished: {result.TestStatus} " +
                              $"(Passed: {result.PassCount}, Failed: {result.FailCount}, " +
                              $"Skipped: {result.SkipCount})");

                CleanupActiveRun();
            }

            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }
        }

        [Serializable]
        private class TestRunState
        {
            public string Status;
            public bool IsRunning;
            public string TestMode;
            public string Filter;
            public string StartedAtUtc;
            public string FinishedAtUtc;
            public double? DurationSeconds;
            public TestRunSummary Summary;
            public List<TestCaseResult> Results;
            [NonSerialized] public DateTime StartedAtUtcValue;

            public static TestRunState CreateIdle()
            {
                return new TestRunState
                {
                    Status = "idle",
                    IsRunning = false,
                    TestMode = UnityEditor.TestTools.TestRunner.Api.TestMode.EditMode.ToString(),
                    Filter = "(all)",
                    StartedAtUtcValue = default,
                    Results = new List<TestCaseResult>(),
                    Summary = new TestRunSummary()
                };
            }

            public static TestRunState CreateRunning(TestMode mode, string filter)
            {
                var startedAtUtc = DateTime.UtcNow;
                return new TestRunState
                {
                    Status = "running",
                    IsRunning = true,
                    TestMode = mode.ToString(),
                    Filter = string.IsNullOrEmpty(filter) ? "(all)" : filter,
                    StartedAtUtc = startedAtUtc.ToString("O"),
                    StartedAtUtcValue = startedAtUtc,
                    Results = new List<TestCaseResult>(),
                    Summary = new TestRunSummary()
                };
            }

            public static TestRunState CreateFailedToStart(TestMode mode, string filter, string error)
            {
                var state = CreateRunning(mode, filter);
                state.Status = "failed_to_start";
                state.IsRunning = false;
                state.FinishedAtUtc = DateTime.UtcNow.ToString("O");
                state.DurationSeconds = 0;
                state.Summary = new TestRunSummary
                {
                    Status = "FailedToStart",
                    Error = error
                };
                return state;
            }

            public TestRunState Clone()
            {
                return new TestRunState
                {
                    Status = Status,
                    IsRunning = IsRunning,
                    TestMode = TestMode,
                    Filter = Filter,
                    StartedAtUtc = StartedAtUtc,
                    FinishedAtUtc = FinishedAtUtc,
                    DurationSeconds = DurationSeconds,
                    StartedAtUtcValue = StartedAtUtcValue,
                    Summary = Summary == null
                        ? null
                        : new TestRunSummary
                        {
                            Status = Summary.Status,
                            Passed = Summary.Passed,
                            Failed = Summary.Failed,
                            Skipped = Summary.Skipped,
                            Inconclusive = Summary.Inconclusive,
                            Error = Summary.Error
                        },
                    Results = Results?.Select(result => new TestCaseResult
                    {
                        Name = result.Name,
                        FullName = result.FullName,
                        Status = result.Status,
                        DurationSeconds = result.DurationSeconds,
                        Message = result.Message,
                        StackTrace = result.StackTrace,
                        Output = result.Output
                    }).ToList() ?? new List<TestCaseResult>()
                };
            }
        }

        [Serializable]
        private class TestRunSummary
        {
            public string Status;
            public int Passed;
            public int Failed;
            public int Skipped;
            public int Inconclusive;
            public string Error;
        }

        [Serializable]
        private class TestCaseResult
        {
            public string Name;
            public string FullName;
            public string Status;
            public double DurationSeconds;
            public string Message;
            public string StackTrace;
            public string Output;
        }

        private static void CleanupActiveRun()
        {
            if (s_activeApi != null && s_activeCallbacks != null)
                s_activeApi.UnregisterCallbacks(s_activeCallbacks);

            if (s_activeApi != null)
                ScriptableObject.DestroyImmediate(s_activeApi);

            s_activeApi = null;
            s_activeCallbacks = null;
        }
    }
}

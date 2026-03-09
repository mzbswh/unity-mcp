using System.Collections.Generic;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityMcp.Shared.Attributes;
using UnityMcp.Shared.Models;

namespace UnityMcp.Editor.Resources
{
    [McpToolGroup("TestResources")]
    public static class TestResources
    {
        [McpResource("unity://tests/{mode}", "Test List",
            "List of tests for the specified mode (EditMode or PlayMode)")]
        public static ToolResult GetTests(
            [Desc("Test mode: 'EditMode' or 'PlayMode'")] string mode)
        {
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            var testMode = mode.ToLower().Contains("play")
                ? TestMode.PlayMode
                : TestMode.EditMode;

            var tests = new List<object>();
            api.RetrieveTestList(testMode, (testRoot) =>
            {
                CollectTests(testRoot, tests);
            });

            return ToolResult.Json(new
            {
                mode = testMode.ToString(),
                count = tests.Count,
                tests
            });
        }

        private static void CollectTests(ITestAdaptor test, List<object> results)
        {
            if (!test.HasChildren && test.RunState != RunState.NotRunnable)
            {
                results.Add(new
                {
                    name = test.Name,
                    fullName = test.FullName,
                    runState = test.RunState.ToString(),
                    testCaseCount = test.TestCaseCount
                });
            }

            if (test.Children != null)
            {
                foreach (var child in test.Children)
                    CollectTests(child, results);
            }
        }
    }
}

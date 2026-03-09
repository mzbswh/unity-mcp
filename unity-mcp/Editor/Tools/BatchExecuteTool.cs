using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityMcp.Editor.Core;
using UnityMcp.Shared.Attributes;
using UnityMcp.Shared.Models;
using UnityMcp.Shared.Utils;

namespace UnityMcp.Editor.Tools
{
    [McpToolGroup("Batch")]
    public static class BatchExecuteTool
    {
        [McpTool("batch_execute",
            "Execute multiple tool operations in a single request. " +
            "Supports stopOnError (halt on first failure) and atomic (undo all on failure).",
            Group = "core")]
        public static ToolResult Execute(
            [Desc("Array of operations: [{id, tool, arguments}]")] JArray operations,
            [Desc("Stop executing on first error")] bool stopOnError = true,
            [Desc("Atomic: undo all operations if any fails")] bool atomic = false)
        {
            if (operations == null || operations.Count == 0)
                return ToolResult.Error("No operations provided");

            var registry = McpServer.Registry;
            var results = new List<object>();
            int succeeded = 0, failed = 0;

            int undoGroup = -1;
            if (atomic)
            {
                Undo.IncrementCurrentGroup();
                Undo.SetCurrentGroupName("BatchExecute");
                undoGroup = Undo.GetCurrentGroup();
            }

            for (int i = 0; i < operations.Count; i++)
            {
                var op = operations[i] as JObject;
                var opId = op?["id"]?.ToString() ?? i.ToString();
                var toolName = op?["tool"]?.ToString();
                var arguments = op?["arguments"] as JObject;

                if (string.IsNullOrEmpty(toolName))
                {
                    results.Add(new { id = opId, index = i, success = false, error = "Missing 'tool' field" });
                    failed++;
                    if (stopOnError) break;
                    continue;
                }

                var entry = registry.GetTool(toolName);
                if (entry == null)
                {
                    results.Add(new { id = opId, index = i, success = false, error = $"Unknown tool: {toolName}" });
                    failed++;
                    if (stopOnError) break;
                    continue;
                }

                try
                {
                    var args = ParameterBinder.Bind(entry.Method, arguments);
                    var ret = entry.Method.Invoke(entry.Instance, args);
                    var toolResult = ret as ToolResult ?? ToolResult.Json(ret);

                    if (toolResult.IsSuccess)
                    {
                        results.Add(new { id = opId, index = i, success = true, result = toolResult.Content });
                        succeeded++;
                    }
                    else
                    {
                        results.Add(new { id = opId, index = i, success = false, error = toolResult.ErrorMessage });
                        failed++;
                        if (stopOnError) break;
                    }
                }
                catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException != null)
                {
                    results.Add(new { id = opId, index = i, success = false, error = ex.InnerException.Message });
                    failed++;
                    if (stopOnError) break;
                }
                catch (System.Exception ex)
                {
                    results.Add(new { id = opId, index = i, success = false, error = ex.Message });
                    failed++;
                    if (stopOnError) break;
                }
            }

            // Atomic rollback on failure
            if (atomic && failed > 0 && undoGroup >= 0)
            {
                Undo.RevertAllDownToGroup(undoGroup);
            }

            return ToolResult.Json(new
            {
                results,
                summary = new { total = operations.Count, succeeded, failed }
            });
        }
    }
}

#if UNITY_MCP_RUNTIME
using System;
using System.Reflection;
using UnityEngine;
using UnityMcp.Shared.Attributes;
using UnityMcp.Shared.Models;

namespace UnityMcp.Runtime.Tools
{
    [McpToolGroup("Runtime.Invoke")]
    public static class RuntimeInvokeTools
    {
        [McpTool("runtime_invoke",
            "Invoke a method on a MonoBehaviour. Only methods marked with [McpInvokable] are allowed.",
            Group = "runtime")]
        public static ToolResult Invoke(
            [Desc("GameObject path in hierarchy (e.g. 'Player' or 'UI/Canvas/Panel')")] string gameObjectPath,
            [Desc("Component type name (e.g. 'PlayerController')")] string componentType,
            [Desc("Method name to invoke")] string methodName,
            [Desc("Method arguments as JSON array (optional)")] object[] args = null)
        {
            var go = GameObject.Find(gameObjectPath);
            if (go == null)
                return ToolResult.Error($"GameObject '{gameObjectPath}' not found");

            // Find component by type name
            Component target = null;
            foreach (var comp in go.GetComponents<Component>())
            {
                if (comp != null && comp.GetType().Name == componentType)
                {
                    target = comp;
                    break;
                }
            }
            if (target == null)
                return ToolResult.Error($"Component '{componentType}' not found on '{gameObjectPath}'");

            // Find method - only public instance methods are allowed
            var method = target.GetType().GetMethod(methodName,
                BindingFlags.Public | BindingFlags.Instance);
            if (method == null)
                return ToolResult.Error($"Method '{methodName}' not found on '{componentType}'");

            // Whitelist check: must have [McpInvokable]
            if (method.GetCustomAttribute<McpInvokableAttribute>() == null)
                return ToolResult.Error(
                    $"Method '{methodName}' is not marked with [McpInvokable]. " +
                    "Only methods explicitly marked as invokable can be called for safety.");

            try
            {
                var result = method.Invoke(target, args);
                return ToolResult.Json(new
                {
                    success = true,
                    gameObject = gameObjectPath,
                    component = componentType,
                    method = methodName,
                    returnValue = result
                });
            }
            catch (TargetInvocationException ex)
            {
                return ToolResult.Error($"Invocation error: {ex.InnerException?.Message ?? ex.Message}");
            }
        }
    }
}
#endif

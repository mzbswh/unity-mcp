#if UNITY_MCP_RUNTIME
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityMcp.Shared.Attributes;
using UnityMcp.Shared.Interfaces;
using UnityMcp.Shared.Models;
using UnityMcp.Shared.Utils;

namespace UnityMcp.Runtime.Core
{
    internal class RuntimeEntry
    {
        public MethodInfo Method;
        public object Instance;
    }

    public class RuntimeToolRegistry : IToolRegistry
    {
        private readonly ConcurrentDictionary<string, RuntimeEntry> _tools = new();
        private readonly ConcurrentDictionary<string, McpToolAttribute> _toolAttrs = new();
        private readonly ConcurrentDictionary<string, RuntimeEntry> _resources = new();
        private readonly ConcurrentDictionary<string, McpResourceAttribute> _resourceAttrs = new();
        private readonly ConcurrentDictionary<string, RuntimeEntry> _prompts = new();
        private readonly ConcurrentDictionary<string, McpPromptAttribute> _promptAttrs = new();

        public int ToolCount => _tools.Count;
        public int ResourceCount => _resources.Count;
        public int PromptCount => _prompts.Count;

        public void ScanAll()
        {
            var toolGroupTypes = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !IsSystemAssembly(a))
                .SelectMany(a =>
                {
                    try { return a.GetTypes(); }
                    catch { return Array.Empty<Type>(); }
                })
                .Where(t => t.GetCustomAttribute<McpToolGroupAttribute>() != null);

            foreach (var type in toolGroupTypes)
            {
                object instance = null; // lazily created for instance methods
                var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance);
                foreach (var method in methods)
                {
                    object GetOrCreateInstance()
                    {
                        if (method.IsStatic) return null;
                        return instance ??= Activator.CreateInstance(type);
                    }

                    var toolAttr = method.GetCustomAttribute<McpToolAttribute>();
                    if (toolAttr != null)
                    {
                        _tools[toolAttr.Name] = new RuntimeEntry { Method = method, Instance = GetOrCreateInstance() };
                        _toolAttrs[toolAttr.Name] = toolAttr;
                    }

                    var resourceAttr = method.GetCustomAttribute<McpResourceAttribute>();
                    if (resourceAttr != null)
                    {
                        _resources[resourceAttr.UriTemplate] = new RuntimeEntry { Method = method, Instance = GetOrCreateInstance() };
                        _resourceAttrs[resourceAttr.UriTemplate] = resourceAttr;
                    }

                    var promptAttr = method.GetCustomAttribute<McpPromptAttribute>();
                    if (promptAttr != null)
                    {
                        _prompts[promptAttr.Name] = new RuntimeEntry { Method = method, Instance = GetOrCreateInstance() };
                        _promptAttrs[promptAttr.Name] = promptAttr;
                    }
                }
            }
        }

        public bool HasTool(string name) => _tools.ContainsKey(name);
        public bool HasResource(string uri)
        {
            if (_resources.ContainsKey(uri)) return true;
            // Try template match
            foreach (var template in _resources.Keys)
            {
                if (MatchUriTemplate(template, uri) != null) return true;
            }
            return false;
        }

        public JArray GetToolList()
        {
            var arr = new JArray();
            foreach (var kvp in _toolAttrs)
            {
                var attr = kvp.Value;
                var method = _tools[kvp.Key].Method;
                var schema = JsonSchemaGenerator.GenerateForMethod(method);
                var tool = new JObject
                {
                    ["name"] = attr.Name,
                    ["description"] = attr.Description,
                    ["inputSchema"] = schema
                };

                if (!string.IsNullOrEmpty(attr.Title) || attr.ReadOnly || attr.Idempotent)
                {
                    var annotations = new JObject();
                    if (!string.IsNullOrEmpty(attr.Title)) annotations["title"] = attr.Title;
                    if (attr.ReadOnly) annotations["readOnlyHint"] = true;
                    if (attr.Idempotent) annotations["idempotentHint"] = true;
                    tool["annotations"] = annotations;
                }

                arr.Add(tool);
            }
            return arr;
        }

        public string GetResourceMimeType(string uri)
        {
            if (_resourceAttrs.TryGetValue(uri, out var attr))
                return attr.MimeType;
            foreach (var kvp in _resourceAttrs)
            {
                if (MatchUriTemplate(kvp.Key, uri) != null)
                    return kvp.Value.MimeType;
            }
            return "application/json";
        }

        public JArray GetResourceList()
        {
            var arr = new JArray();
            foreach (var kvp in _resourceAttrs)
            {
                arr.Add(new JObject
                {
                    ["uri"] = kvp.Value.UriTemplate,
                    ["name"] = kvp.Value.Name,
                    ["description"] = kvp.Value.Description,
                    ["mimeType"] = kvp.Value.MimeType
                });
            }
            return arr;
        }

        public JArray GetPromptList()
        {
            var arr = new JArray();
            foreach (var kvp in _promptAttrs)
            {
                var prompt = new JObject
                {
                    ["name"] = kvp.Value.Name,
                    ["description"] = kvp.Value.Description
                };

                if (_prompts.TryGetValue(kvp.Key, out var entry))
                {
                    var arguments = new JArray();
                    foreach (var param in entry.Method.GetParameters())
                    {
                        var arg = new JObject
                        {
                            ["name"] = param.Name,
                            ["required"] = !param.HasDefaultValue &&
                                           Nullable.GetUnderlyingType(param.ParameterType) == null
                        };
                        var desc = param.GetCustomAttribute<DescAttribute>();
                        if (desc != null)
                            arg["description"] = desc.Text;
                        arguments.Add(arg);
                    }
                    if (arguments.Count > 0)
                        prompt["arguments"] = arguments;
                }

                arr.Add(prompt);
            }
            return arr;
        }

        public JObject GetPrompt(string name, JObject arguments)
        {
            if (!_prompts.TryGetValue(name, out var entry))
                return null;
            var args = ParameterBinder.Bind(entry.Method, arguments ?? new JObject());
            var ret = entry.Method.Invoke(entry.Instance, args);
            var description = _promptAttrs[name].Description;

            if (ret is ToolResult tr)
            {
                if (!tr.IsSuccess)
                    throw new System.Exception(tr.ErrorMessage ?? "Prompt execution failed");
                return tr.ToPromptResponse(description);
            }

            string text = ret?.ToString() ?? "";
            return new JObject
            {
                ["description"] = description,
                ["messages"] = new JArray
                {
                    new JObject
                    {
                        ["role"] = "user",
                        ["content"] = new JObject
                        {
                            ["type"] = "text",
                            ["text"] = text
                        }
                    }
                }
            };
        }

        public ToolResult ExecuteTool(string name, JObject arguments)
        {
            if (!_tools.TryGetValue(name, out var entry))
                return ToolResult.Error($"Tool not found: {name}");

            try
            {
                var args = ParameterBinder.Bind(entry.Method, arguments);
                var ret = entry.Method.Invoke(entry.Instance, args);
                return ret as ToolResult ?? ToolResult.Json(ret);
            }
            catch (Exception ex)
            {
                var inner = ex is TargetInvocationException tie ? tie.InnerException ?? ex : ex;
                UnityEngine.Debug.LogError($"[MCP Runtime] Tool execution error [{name}]: {inner}");
                return ToolResult.Error(inner.Message);
            }
        }

        public ToolResult ReadResource(string uri)
        {
            try
            {
                // Try exact match first
                if (_resources.TryGetValue(uri, out var entry))
                {
                    var args = ParameterBinder.Bind(entry.Method, new JObject());
                    var ret = entry.Method.Invoke(entry.Instance, args);
                    return ret as ToolResult ?? ToolResult.Json(ret);
                }

                // Try template match
                foreach (var kvp in _resources)
                {
                    var templateArgs = MatchUriTemplate(kvp.Key, uri);
                    if (templateArgs != null)
                    {
                        var e = kvp.Value;
                        var args = ParameterBinder.Bind(e.Method, JObject.FromObject(templateArgs));
                        var ret = e.Method.Invoke(e.Instance, args);
                        return ret as ToolResult ?? ToolResult.Json(ret);
                    }
                }

                return ToolResult.Error($"Resource not found: {uri}");
            }
            catch (Exception ex)
            {
                var inner = ex is TargetInvocationException tie ? tie.InnerException ?? ex : ex;
                UnityEngine.Debug.LogError($"[MCP Runtime] Resource read error [{uri}]: {inner}");
                return ToolResult.Error(inner.Message);
            }
        }

        private static Dictionary<string, string> MatchUriTemplate(string template, string uri)
        {
            var pattern = System.Text.RegularExpressions.Regex.Replace(
                template, @"\{(\w+)\}", @"(?<$1>[^/]+)");
            var match = System.Text.RegularExpressions.Regex.Match(uri, $"^{pattern}$");
            if (!match.Success) return null;

            var result = new Dictionary<string, string>();
            // Extract param names from the template for .NET Standard 2.0 compatibility
            // (GroupCollection.Keys requires .NET Standard 2.1)
            foreach (System.Text.RegularExpressions.Match m in
                System.Text.RegularExpressions.Regex.Matches(template, @"\{(\w+)\}"))
            {
                var paramName = m.Groups[1].Value;
                var group = match.Groups[paramName];
                if (group.Success)
                    result[paramName] = group.Value;
            }
            return result;
        }

        private static bool IsSystemAssembly(Assembly assembly)
        {
            var name = assembly.GetName().Name;
            return name.StartsWith("System") || name.StartsWith("Unity.")
                || name.StartsWith("UnityEngine.") || name.StartsWith("UnityEditor")
                || name.StartsWith("UnityMcp.Editor")
                || name == "mscorlib" || name == "netstandard"
                || name == "UnityEngine" || name == "Newtonsoft.Json";
        }
    }
}
#endif

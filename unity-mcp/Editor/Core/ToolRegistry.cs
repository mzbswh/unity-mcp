using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityMcp.Shared.Attributes;
using UnityMcp.Shared.Interfaces;
using UnityMcp.Shared.Models;
using UnityMcp.Shared.Utils;

namespace UnityMcp.Editor.Core
{
    public class ToolEntry
    {
        public McpToolAttribute Attribute;
        public MethodInfo Method;
        public object Instance;
        public JObject InputSchema;
    }

    public class ResourceEntry
    {
        public McpResourceAttribute Attribute;
        public MethodInfo Method;
        public object Instance;
        public string UriTemplate;
        public Regex UriRegex;
    }

    public class PromptEntry
    {
        public McpPromptAttribute Attribute;
        public MethodInfo Method;
        public object Instance;
    }

    public class ToolRegistry : IToolRegistry
    {
        private readonly ConcurrentDictionary<string, ToolEntry> _tools = new();
        private readonly ConcurrentDictionary<string, ResourceEntry> _resources = new();
        private readonly ConcurrentDictionary<string, PromptEntry> _prompts = new();
        // Thread-safe cache of tool enabled state — avoids EditorPrefs access from TCP threads
        private readonly ConcurrentDictionary<string, bool> _enabledCache = new();

        public int ToolCount => _tools.Count;
        public int ResourceCount => _resources.Count;
        public int PromptCount => _prompts.Count;

        public void ScanAll()
        {
            _tools.Clear();
            _resources.Clear();
            _prompts.Clear();

            var toolGroupTypes = TypeCache.GetTypesWithAttribute<McpToolGroupAttribute>();
            foreach (var type in toolGroupTypes)
            {
                object instance = null;
                bool isStatic = type.IsAbstract && type.IsSealed; // C# static class
                if (!isStatic && type.GetConstructor(Type.EmptyTypes) != null)
                    instance = Activator.CreateInstance(type);

                ScanType(type, instance);
            }

            // Cache enabled state on main thread (EditorPrefs is not thread-safe)
            _enabledCache.Clear();
            foreach (var name in _tools.Keys)
                _enabledCache[name] = ReadToolEnabledFromPrefs(name);

            McpLogger.Info($"Discovered {_tools.Count} tools, {_resources.Count} resources, " +
                           $"{_prompts.Count} prompts from {toolGroupTypes.Count} tool groups");
        }

        private void ScanType(Type type, object instance)
        {
            var flags = BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance;
            foreach (var method in type.GetMethods(flags))
            {
                var toolAttr = method.GetCustomAttribute<McpToolAttribute>();
                if (toolAttr != null)
                {
                    _tools[toolAttr.Name] = new ToolEntry
                    {
                        Attribute = toolAttr,
                        Method = method,
                        Instance = method.IsStatic ? null : instance,
                        InputSchema = JsonSchemaGenerator.GenerateForMethod(method)
                    };
                    continue;
                }

                var resAttr = method.GetCustomAttribute<McpResourceAttribute>();
                if (resAttr != null)
                {
                    _resources[resAttr.UriTemplate] = new ResourceEntry
                    {
                        Attribute = resAttr,
                        Method = method,
                        Instance = method.IsStatic ? null : instance,
                        UriTemplate = resAttr.UriTemplate,
                        UriRegex = BuildUriRegex(resAttr.UriTemplate)
                    };
                    continue;
                }

                var promptAttr = method.GetCustomAttribute<McpPromptAttribute>();
                if (promptAttr != null)
                {
                    _prompts[promptAttr.Name] = new PromptEntry
                    {
                        Attribute = promptAttr,
                        Method = method,
                        Instance = method.IsStatic ? null : instance
                    };
                }
            }
        }

        // --- Query methods ---

        public bool HasTool(string name) => _tools.ContainsKey(name) && IsToolEnabled(name);

        public bool HasResource(string uri) => MatchResource(uri) != null;

        public ToolEntry GetTool(string name)
        {
            return _tools.TryGetValue(name, out var entry) && IsToolEnabled(name) ? entry : null;
        }

        public ResourceEntry MatchResource(string uri)
        {
            return MatchResource(uri, out _);
        }

        public ResourceEntry MatchResource(string uri, out Dictionary<string, string> extractedParams)
        {
            extractedParams = null;
            foreach (var entry in _resources.Values)
            {
                var match = entry.UriRegex.Match(uri);
                if (match.Success)
                {
                    extractedParams = ExtractParams(entry.UriTemplate, match);
                    return entry;
                }
            }
            return null;
        }

        public PromptEntry GetPrompt(string name)
        {
            return _prompts.TryGetValue(name, out var entry) ? entry : null;
        }

        // --- List methods (MCP protocol responses) ---

        public JArray GetToolList()
        {
            var arr = new JArray();
            foreach (var kv in _tools)
            {
                if (!IsToolEnabled(kv.Key)) continue;
                var attr = kv.Value.Attribute;
                var tool = new JObject
                {
                    ["name"] = attr.Name,
                    ["description"] = attr.Description,
                    ["inputSchema"] = kv.Value.InputSchema
                };

                // MCP annotations (readOnlyHint, idempotentHint, title)
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

        public JArray GetResourceList()
        {
            var arr = new JArray();
            foreach (var kv in _resources)
            {
                var attr = kv.Value.Attribute;
                var res = new JObject
                {
                    ["uri"] = attr.UriTemplate,
                    ["name"] = attr.Name,
                    ["description"] = attr.Description,
                    ["mimeType"] = attr.MimeType
                };
                arr.Add(res);
            }
            return arr;
        }

        public JArray GetPromptList()
        {
            var arr = new JArray();
            foreach (var kv in _prompts)
            {
                var attr = kv.Value.Attribute;
                var prompt = new JObject
                {
                    ["name"] = attr.Name,
                    ["description"] = attr.Description,
                };

                // MCP spec: each prompt must declare its arguments
                var arguments = BuildPromptArguments(kv.Value.Method);
                if (arguments.Count > 0)
                    prompt["arguments"] = arguments;

                arr.Add(prompt);
            }
            return arr;
        }

        private static JArray BuildPromptArguments(MethodInfo method)
        {
            var args = new JArray();
            foreach (var param in method.GetParameters())
            {
                var arg = new JObject
                {
                    ["name"] = param.Name,
                    ["required"] = !param.HasDefaultValue && !IsNullableParam(param.ParameterType)
                };
                var desc = param.GetCustomAttribute<DescAttribute>();
                if (desc != null)
                    arg["description"] = desc.Text;
                args.Add(arg);
            }
            return args;
        }

        private static bool IsNullableParam(Type type) =>
            Nullable.GetUnderlyingType(type) != null;

        public JObject GetPrompt(string name, JObject arguments)
        {
            var entry = GetPrompt(name);
            if (entry == null) return null;
            var args = ParameterBinder.Bind(entry.Method, arguments ?? new JObject());
            var result = entry.Method.Invoke(entry.Instance, args);

            // MCP prompts/get format: {"description":"...","messages":[...]}
            var description = entry.Attribute.Description ?? name;
            if (result is ToolResult tr)
            {
                if (!tr.IsSuccess)
                    throw new System.Exception(tr.ErrorMessage ?? "Prompt execution failed");
                return tr.ToPromptResponse(description);
            }

            string text = result?.ToString() ?? "";
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

        // --- Per-tool enable/disable ---

        public bool IsToolEnabled(string toolName)
        {
            return _enabledCache.TryGetValue(toolName, out var enabled) ? enabled : true;
        }

        public void SetToolEnabled(string toolName, bool enabled)
        {
            EditorPrefs.SetBool($"UnityMcp_Tool_{toolName}", enabled);
            _enabledCache[toolName] = enabled;
        }

        private bool ReadToolEnabledFromPrefs(string toolName)
        {
            string key = $"UnityMcp_Tool_{toolName}";
            if (EditorPrefs.HasKey(key))
                return EditorPrefs.GetBool(key);
            return _tools.TryGetValue(toolName, out var entry) && entry.Attribute.AutoRegister;
        }

        // --- Editor window queries (include disabled items) ---

        /// <summary>Returns all registered tool names and descriptions (regardless of enabled state).</summary>
        public IEnumerable<(string name, string description, string group)> GetAllToolEntries()
        {
            foreach (var kv in _tools)
            {
                var attr = kv.Value.Attribute;
                yield return (attr.Name, attr.Description, attr.Group ?? "");
            }
        }

        /// <summary>Returns all registered resource names and descriptions.</summary>
        public IEnumerable<(string name, string description)> GetAllResourceEntries()
        {
            foreach (var kv in _resources)
            {
                var attr = kv.Value.Attribute;
                yield return (attr.Name, attr.Description);
            }
        }

        /// <summary>Returns all registered prompt names and descriptions.</summary>
        public IEnumerable<(string name, string description)> GetAllPromptEntries()
        {
            foreach (var kv in _prompts)
            {
                var attr = kv.Value.Attribute;
                yield return (attr.Name, attr.Description);
            }
        }

        // --- URI template helpers ---

        private static Regex BuildUriRegex(string template)
        {
            // "unity://gameobject/{id}" -> ^unity://gameobject/(?<id>[^/]+)$
            var pattern = Regex.Replace(template, @"\{(\w+)\}", @"(?<$1>[^/]+)");
            return new Regex($"^{pattern}$", RegexOptions.Compiled);
        }

        private static Dictionary<string, string> ExtractParams(string template, Match match)
        {
            var result = new Dictionary<string, string>();
            foreach (Match m in Regex.Matches(template, @"\{(\w+)\}"))
            {
                var paramName = m.Groups[1].Value;
                var group = match.Groups[paramName];
                if (group.Success)
                    result[paramName] = group.Value;
            }
            return result;
        }
    }
}

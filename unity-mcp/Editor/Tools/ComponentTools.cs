using System;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityMcp.Editor.Utils;
using UnityMcp.Shared.Attributes;
using UnityMcp.Shared.Models;

namespace UnityMcp.Editor.Tools
{
    [McpToolGroup("Component")]
    public static class ComponentTools
    {
        [McpTool("component_add", "Add a component to a GameObject",
            Group = "component")]
        public static ToolResult Add(
            [Desc("Name or path of the target GameObject")] string target,
            [Desc("Component type name (e.g. Rigidbody, BoxCollider, AudioSource)")] string type)
        {
            var go = GameObjectTools.FindGameObject(target, null);
            if (go == null)
                return ToolResult.Error($"GameObject not found: {target}");

            var componentType = ResolveComponentType(type);
            if (componentType == null)
                return ToolResult.Error($"Unknown component type: {type}");

            var component = Undo.AddComponent(go, componentType);
            return ToolResult.Json(new
            {
                success = true,
                gameObject = go.name,
                component = component.GetType().Name,
                instanceId = component.GetInstanceID()
            });
        }

        [McpTool("component_remove", "Remove a component from a GameObject",
            Group = "component")]
        public static ToolResult Remove(
            [Desc("Name or path of the target GameObject")] string target,
            [Desc("Component type name to remove")] string type)
        {
            var go = GameObjectTools.FindGameObject(target, null);
            if (go == null)
                return ToolResult.Error($"GameObject not found: {target}");

            var componentType = ResolveComponentType(type);
            if (componentType == null)
                return ToolResult.Error($"Unknown component type: {type}");

            var component = go.GetComponent(componentType);
            if (component == null)
                return ToolResult.Error($"Component '{type}' not found on '{target}'");

            UndoHelper.DestroyObject(component);
            return ToolResult.Text($"Removed {type} from '{go.name}'");
        }

        [McpTool("component_get", "Get serialized field values of a component",
            Group = "component", ReadOnly = true)]
        public static ToolResult Get(
            [Desc("Name or path of the target GameObject")] string target,
            [Desc("Component type name")] string type)
        {
            var go = GameObjectTools.FindGameObject(target, null);
            if (go == null)
                return ToolResult.Error($"GameObject not found: {target}");

            var componentType = ResolveComponentType(type);
            if (componentType == null)
                return ToolResult.Error($"Unknown component type: {type}");

            var component = go.GetComponent(componentType);
            if (component == null)
                return ToolResult.Error($"Component '{type}' not found on '{target}'");

            var so = new SerializedObject(component);
            var fields = new JObject();
            var prop = so.GetIterator();
            prop.Next(true); // skip root
            while (prop.NextVisible(false))
            {
                fields[prop.name] = SerializedPropertyToToken(prop);
            }

            return ToolResult.Json(new { gameObject = go.name, component = type, fields });
        }

        [McpTool("component_modify", "Modify serialized field values of a component",
            Group = "component")]
        public static ToolResult Modify(
            [Desc("Name or path of the target GameObject")] string target,
            [Desc("Component type name")] string type,
            [Desc("Fields to set as {fieldName: value} object")] JObject fields)
        {
            var go = GameObjectTools.FindGameObject(target, null);
            if (go == null)
                return ToolResult.Error($"GameObject not found: {target}");

            var componentType = ResolveComponentType(type);
            if (componentType == null)
                return ToolResult.Error($"Unknown component type: {type}");

            var component = go.GetComponent(componentType);
            if (component == null)
                return ToolResult.Error($"Component '{type}' not found on '{target}'");

            var so = new SerializedObject(component);
            int modified = 0;

            foreach (var kv in fields)
            {
                var prop = so.FindProperty(kv.Key);
                if (prop == null) continue;
                SetSerializedProperty(prop, kv.Value);
                modified++;
            }

            so.ApplyModifiedProperties();
            return ToolResult.Text($"Modified {modified} fields on {type} of '{go.name}'");
        }

        // --- Helpers ---

        private static Type ResolveComponentType(string typeName)
        {
            // Search all loaded assemblies for a Component with the given name
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException ex) { types = ex.Types; }

                foreach (var t in types)
                {
                    if (t != null && t.Name == typeName && typeof(Component).IsAssignableFrom(t))
                        return t;
                }
            }

            return null;
        }

        private static JToken SerializedPropertyToToken(SerializedProperty prop)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer: return prop.intValue;
                case SerializedPropertyType.Boolean: return prop.boolValue;
                case SerializedPropertyType.Float: return prop.floatValue;
                case SerializedPropertyType.String: return prop.stringValue;
                case SerializedPropertyType.Enum:
                    var names = prop.enumNames;
                    var idx = prop.enumValueIndex;
                    return idx >= 0 && idx < names.Length ? names[idx] : idx.ToString();
                case SerializedPropertyType.Vector2:
                    var v2 = prop.vector2Value;
                    return new JObject { ["x"] = v2.x, ["y"] = v2.y };
                case SerializedPropertyType.Vector3:
                    var v3 = prop.vector3Value;
                    return new JObject { ["x"] = v3.x, ["y"] = v3.y, ["z"] = v3.z };
                case SerializedPropertyType.Color:
                    var c = prop.colorValue;
                    return new JObject { ["r"] = c.r, ["g"] = c.g, ["b"] = c.b, ["a"] = c.a };
                case SerializedPropertyType.ObjectReference:
                    return prop.objectReferenceValue != null
                        ? prop.objectReferenceValue.name : null;
                default:
                    return prop.propertyType.ToString();
            }
        }

        private static void SetSerializedProperty(SerializedProperty prop, JToken value)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer:
                    prop.intValue = value.Value<int>();
                    break;
                case SerializedPropertyType.Boolean:
                    prop.boolValue = value.Value<bool>();
                    break;
                case SerializedPropertyType.Float:
                    prop.floatValue = value.Value<float>();
                    break;
                case SerializedPropertyType.String:
                    prop.stringValue = value.Value<string>();
                    break;
                case SerializedPropertyType.Enum:
                    var enumName = value.Value<string>();
                    var idx = System.Array.IndexOf(prop.enumNames, enumName);
                    if (idx >= 0) prop.enumValueIndex = idx;
                    break;
                case SerializedPropertyType.Vector2:
                    prop.vector2Value = new Vector2(
                        value["x"]?.Value<float>() ?? 0f,
                        value["y"]?.Value<float>() ?? 0f);
                    break;
                case SerializedPropertyType.Vector3:
                    prop.vector3Value = new Vector3(
                        value["x"]?.Value<float>() ?? 0f,
                        value["y"]?.Value<float>() ?? 0f,
                        value["z"]?.Value<float>() ?? 0f);
                    break;
                case SerializedPropertyType.Color:
                    prop.colorValue = new Color(
                        value["r"]?.Value<float>() ?? 0f,
                        value["g"]?.Value<float>() ?? 0f,
                        value["b"]?.Value<float>() ?? 0f,
                        value["a"]?.Value<float>() ?? 1f);
                    break;
            }
        }
    }
}

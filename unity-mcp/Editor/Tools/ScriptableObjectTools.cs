using System;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityMcp.Shared.Attributes;
using UnityMcp.Shared.Models;
using UnityMcp.Shared.Utils;

namespace UnityMcp.Editor.Tools
{
    [McpToolGroup("ScriptableObject")]
    public static class ScriptableObjectTools
    {
        [McpTool("so_create", "Create a ScriptableObject asset",
            Group = "scriptableobject")]
        public static ToolResult Create(
            [Desc("Full type name of the ScriptableObject (e.g. 'GameConfig', 'MyNamespace.PlayerData')")] string type,
            [Desc("Save path (e.g. Assets/Data/Config.asset)")] string path,
            [Desc("Initial field values as {fieldName: value}")] JObject fields = null)
        {
            var pv = PathValidator.QuickValidate(path);
            if (!pv.IsValid) return ToolResult.Error(pv.Error);

            var soType = ResolveSOType(type);
            if (soType == null)
                return ToolResult.Error($"ScriptableObject type not found: {type}");

            var so = ScriptableObject.CreateInstance(soType);
            if (so == null)
                return ToolResult.Error($"Failed to create instance of: {type}");

            if (fields != null && fields.Count > 0)
            {
                var serialized = new SerializedObject(so);
                foreach (var kv in fields)
                {
                    var prop = ComponentTools.FindPropertyFuzzy(serialized, kv.Key);
                    if (prop != null)
                        ComponentTools.SetSerializedProperty(prop, kv.Value);
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            var dir = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !AssetDatabase.IsValidFolder(dir))
            {
                System.IO.Directory.CreateDirectory(dir);
                AssetDatabase.Refresh();
            }

            AssetDatabase.CreateAsset(so, path);
            AssetDatabase.SaveAssets();

            return ToolResult.Json(new
            {
                success = true,
                path,
                type = soType.Name,
                message = $"Created ScriptableObject: {path}"
            });
        }

        [McpTool("so_get", "Read serialized field values of a ScriptableObject asset",
            Group = "scriptableobject", ReadOnly = true)]
        public static ToolResult Get(
            [Desc("Asset path (e.g. Assets/Data/Config.asset)")] string path)
        {
            var pv = PathValidator.QuickValidate(path);
            if (!pv.IsValid) return ToolResult.Error(pv.Error);

            var obj = AssetDatabase.LoadMainAssetAtPath(path) as ScriptableObject;
            if (obj == null)
                return ToolResult.Error($"ScriptableObject not found: {path}");

            var so = new SerializedObject(obj);
            var fieldsObj = new JObject();
            var prop = so.GetIterator();
            prop.Next(true);
            while (prop.NextVisible(false))
            {
                if (prop.name == "m_Script") continue;
                fieldsObj[prop.name] = SerializedPropertyToToken(prop);
            }

            return ToolResult.Json(new
            {
                path,
                type = obj.GetType().FullName,
                name = obj.name,
                fields = fieldsObj
            });
        }

        [McpTool("so_modify", "Modify serialized field values of a ScriptableObject asset. Field names support fuzzy matching.",
            Group = "scriptableobject")]
        public static ToolResult Modify(
            [Desc("Asset path")] string path,
            [Desc("Fields to set as {fieldName: value}")] JObject fields)
        {
            var pv = PathValidator.QuickValidate(path);
            if (!pv.IsValid) return ToolResult.Error(pv.Error);

            var obj = AssetDatabase.LoadMainAssetAtPath(path) as ScriptableObject;
            if (obj == null)
                return ToolResult.Error($"ScriptableObject not found: {path}");

            var so = new SerializedObject(obj);
            int modified = 0;
            var errors = new JArray();

            foreach (var kv in fields)
            {
                var prop = ComponentTools.FindPropertyFuzzy(so, kv.Key);
                if (prop == null)
                {
                    errors.Add($"Field '{kv.Key}' not found");
                    continue;
                }
                ComponentTools.SetSerializedProperty(prop, kv.Value);
                modified++;
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(obj);
            AssetDatabase.SaveAssets();

            if (errors.Count > 0)
                return ToolResult.Json(new { message = $"Modified {modified} fields on '{path}'", errors });
            return ToolResult.Text($"Modified {modified} fields on '{path}'");
        }

        [McpTool("so_list_types", "List available ScriptableObject types in the project",
            Group = "scriptableobject", ReadOnly = true)]
        public static ToolResult ListTypes(
            [Desc("Filter by name substring")] string filter = null,
            [Desc("Max results")] int maxCount = 50)
        {
            var types = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(asm =>
                {
                    try { return asm.GetTypes(); }
                    catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t != null); }
                })
                .Where(t => t != null
                    && !t.IsAbstract
                    && typeof(ScriptableObject).IsAssignableFrom(t)
                    && !typeof(Editor).IsAssignableFrom(t)
                    && !typeof(EditorWindow).IsAssignableFrom(t)
                    && !t.FullName.StartsWith("UnityEditor.")
                    && !t.FullName.StartsWith("UnityEngine.UIElements."))
                .Where(t => string.IsNullOrEmpty(filter) ||
                    t.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    t.FullName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                .Take(maxCount)
                .Select(t => new { name = t.Name, fullName = t.FullName, assembly = t.Assembly.GetName().Name })
                .ToArray();

            return ToolResult.Json(new { count = types.Length, types });
        }

        private static Type ResolveSOType(string typeName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException ex) { types = ex.Types; }

                foreach (var t in types)
                {
                    if (t == null || t.IsAbstract || !typeof(ScriptableObject).IsAssignableFrom(t))
                        continue;
                    if (t.Name == typeName || t.FullName == typeName)
                        return t;
                }
            }
            return null;
        }

        private static JToken SerializedPropertyToToken(SerializedProperty prop)
        {
            if (prop.isArray && prop.propertyType != SerializedPropertyType.String)
            {
                var arr = new JArray();
                for (int i = 0; i < prop.arraySize; i++)
                    arr.Add(SerializedPropertyToToken(prop.GetArrayElementAtIndex(i)));
                return arr;
            }

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
                case SerializedPropertyType.ObjectReference:
                    if (prop.objectReferenceValue == null) return null;
                    var assetPath = AssetDatabase.GetAssetPath(prop.objectReferenceValue);
                    return !string.IsNullOrEmpty(assetPath) ? assetPath : prop.objectReferenceValue.name;
                case SerializedPropertyType.Vector2:
                    var v2 = prop.vector2Value;
                    return new JObject { ["x"] = v2.x, ["y"] = v2.y };
                case SerializedPropertyType.Vector3:
                    var v3 = prop.vector3Value;
                    return new JObject { ["x"] = v3.x, ["y"] = v3.y, ["z"] = v3.z };
                case SerializedPropertyType.Color:
                    var c = prop.colorValue;
                    return new JObject { ["r"] = c.r, ["g"] = c.g, ["b"] = c.b, ["a"] = c.a };
                default:
                    return prop.propertyType.ToString();
            }
        }
    }
}

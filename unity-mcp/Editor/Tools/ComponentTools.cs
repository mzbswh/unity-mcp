using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityMcp.Editor.Utils;
using UnityMcp.Shared.Attributes;
using UnityMcp.Shared.Models;

namespace UnityMcp.Editor.Tools
{
    [McpToolGroup("GameObject")]
    public static class ComponentTools
    {
        [McpTool("gameobject_add_component", "Add a component to a GameObject",
            Group = "gameobject")]
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

            var addabilityError = ValidateAddableComponentType(go, componentType, type);
            if (addabilityError != null)
                return ToolResult.Error(addabilityError);

            var component = Undo.AddComponent(go, componentType);
            if (component == null)
                return ToolResult.Error($"Failed to add component '{type}' to '{go.name}'");

            return ToolResult.Json(new
            {
                gameObject = go.name,
                component = component.GetType().Name,
                instanceId = component.GetInstanceID()
            });
        }

        [McpTool("gameobject_remove_component", "Remove a component from a GameObject",
            Group = "gameobject")]
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

        [McpTool("gameobject_get_component", "Get serialized field values of a component. ObjectReference fields return asset paths, array fields return JSON arrays.",
            Group = "gameobject", ReadOnly = true)]
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

        [McpTool("gameobject_modify_component", "Modify serialized field values of a component. Supports int, float, bool, string, enum, Vector2/3/4, Quaternion, Rect, Bounds, Color, ObjectReference (pass asset path like 'Assets/Materials/X.mat'), and arrays (pass JSON array). Field names are matched flexibly: you can use the C# property name (e.g. 'sprite'), the serialized field name (e.g. 'm_Sprite'), or camelCase (e.g. 'color'). Supports nested fields via dot-path (e.g. 'UVModule.tilesX') or nested JSON objects (e.g. {\"UVModule\": {\"enabled\": true, \"tilesX\": 8}}). This is especially useful for ParticleSystem submodules. Use gameobject_get_component to see available field names.",
            Group = "gameobject")]
        public static ToolResult Modify(
            [Desc("Name or path of the target GameObject")] string target,
            [Desc("Component type name")] string type,
            [Desc("Fields to set as {fieldName: value}. Supports nested objects for submodules (e.g. {\"UVModule\": {\"enabled\": true}}) or dot-paths (e.g. {\"UVModule.tilesX\": 8})")] JObject fields)
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
            var changes = new List<string>();
            var errors = new JArray();

            // Flatten nested JObjects into dot-path entries
            // e.g. {"UVModule": {"enabled": true, "tilesX": 8}} → {"UVModule.enabled": true, "UVModule.tilesX": 8}
            var flatFields = new List<KeyValuePair<string, JToken>>();
            FlattenFields(fields, "", flatFields);

            foreach (var kv in flatFields)
            {
                var prop = FindPropertyFuzzy(so, kv.Key);
                if (prop == null)
                {
                    errors.Add($"Field '{kv.Key}' not found on {type}");
                    continue;
                }
                SetSerializedProperty(prop, kv.Value);
                changes.Add(kv.Key);
            }

            so.ApplyModifiedProperties();

            if (errors.Count > 0)
                return ToolResult.Json(new { message = $"{type} on '{go.name}' updated: {string.Join(", ", changes)}", errors });
            if (changes.Count == 0) return ToolResult.Text($"No fields changed on {type} of '{go.name}'");
            return ToolResult.Text($"{type} on '{go.name}' updated: {string.Join(", ", changes)}");
        }

        [McpTool("gameobject_copy_component", "Copy all serialized property values from one component to another component of the same type (on a different GameObject)",
            Group = "gameobject")]
        public static ToolResult CopyValues(
            [Desc("Name or path of the source GameObject")] string source,
            [Desc("Component type name on the source")] string type,
            [Desc("Name or path of the destination GameObject")] string destination)
        {
            var srcGo = GameObjectTools.FindGameObject(source, null);
            if (srcGo == null)
                return ToolResult.Error($"Source GameObject not found: {source}");

            var dstGo = GameObjectTools.FindGameObject(destination, null);
            if (dstGo == null)
                return ToolResult.Error($"Destination GameObject not found: {destination}");

            var compType = ResolveComponentType(type);
            if (compType == null)
                return ToolResult.Error($"Unknown component type: {type}");

            var srcComp = srcGo.GetComponent(compType);
            if (srcComp == null)
                return ToolResult.Error($"Component '{type}' not found on source '{source}'");

            var dstComp = dstGo.GetComponent(compType);
            if (dstComp == null)
            {
                var addabilityError = ValidateAddableComponentType(dstGo, compType, type);
                if (addabilityError != null)
                    return ToolResult.Error(addabilityError);

                // Add component if it doesn't exist on destination
                dstComp = Undo.AddComponent(dstGo, compType);
                if (dstComp == null)
                    return ToolResult.Error($"Failed to add component '{type}' to '{dstGo.name}'");
            }
            else
            {
                Undo.RecordObject(dstComp, "Copy Component Values");
            }

            EditorUtility.CopySerialized(srcComp, dstComp);
            return ToolResult.Text($"Copied {type} values from '{srcGo.name}' to '{dstGo.name}'");
        }

        // --- Helpers ---

        // Known SerializedProperty types that use JObject as their value format (not nested containers)
        private static readonly HashSet<string> VectorLikeKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "x", "y", "z", "w", "r", "g", "b", "a",
            "center", "size", "extent", "extents",
            "width", "height"
        };

        /// <summary>
        /// Recursively flattens nested JObject fields into dot-path keys.
        /// JObjects that look like Vector/Color/Rect/Bounds values are kept as-is.
        /// </summary>
        private static void FlattenFields(JObject obj, string prefix, List<KeyValuePair<string, JToken>> result)
        {
            foreach (var kv in obj)
            {
                string key = string.IsNullOrEmpty(prefix) ? kv.Key : prefix + "." + kv.Key;
                if (kv.Value is JObject nested && !IsVectorLikeObject(nested))
                    FlattenFields(nested, key, result);
                else
                    result.Add(new KeyValuePair<string, JToken>(key, kv.Value));
            }
        }

        /// <summary>
        /// Heuristic: a JObject is "vector-like" if all its keys are known vector/color/rect components.
        /// These should be passed as-is to SetSerializedProperty rather than flattened.
        /// </summary>
        private static bool IsVectorLikeObject(JObject obj)
        {
            if (obj.Count == 0) return false;
            foreach (var kv in obj)
            {
                if (!VectorLikeKeys.Contains(kv.Key))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Finds a SerializedProperty by name with fuzzy matching.
        /// Tries: exact name → m_ + PascalCase → case-insensitive scan of all visible properties.
        /// </summary>
        internal static SerializedProperty FindPropertyFuzzy(SerializedObject so, string name)
        {
            // 0. Dot-path: e.g. "UVModule.tilesX" — try direct path first
            if (name.Contains("."))
            {
                var prop = so.FindProperty(name);
                if (prop != null) return prop;

                // Try fuzzy-resolving each segment
                var segments = name.Split('.');
                prop = FindPropertyFuzzySegment(so, segments[0]);
                for (int i = 1; i < segments.Length && prop != null; i++)
                    prop = FindNestedPropertyFuzzy(prop, segments[i]);
                if (prop != null) return prop;
            }

            // 1. Exact match
            {
                var prop = so.FindProperty(name);
                if (prop != null) return prop;
            }

            // 2. Try m_ + variations (covers both Unity style "m_Sprite" and TMP style "m_text")
            if (!name.StartsWith("m_"))
            {
                // Try m_ + original name (e.g. "text" → "m_text", "fontSize" → "m_fontSize")
                var prop = so.FindProperty("m_" + name);
                if (prop != null) return prop;

                // Try m_ + PascalCase (e.g. "sprite" → "m_Sprite", "color" → "m_Color")
                string pascal = char.ToUpperInvariant(name[0]) + name.Substring(1);
                prop = so.FindProperty("m_" + pascal);
                if (prop != null) return prop;
            }

            // 3. Case-insensitive scan of all visible properties
            string lower = name.Replace("_", "").ToLowerInvariant();
            var iter = so.GetIterator();
            iter.Next(true);
            while (iter.NextVisible(false))
            {
                string iterLower = iter.name.Replace("_", "").Replace("m_", "").ToLowerInvariant();
                if (iterLower == lower || iter.name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    return so.FindProperty(iter.name);
            }

            return null;
        }

        /// <summary>
        /// Fuzzy-find a top-level property by single segment name.
        /// </summary>
        private static SerializedProperty FindPropertyFuzzySegment(SerializedObject so, string name)
        {
            var prop = so.FindProperty(name);
            if (prop != null) return prop;

            if (!name.StartsWith("m_"))
            {
                prop = so.FindProperty("m_" + name);
                if (prop != null) return prop;
                string pascal = char.ToUpperInvariant(name[0]) + name.Substring(1);
                prop = so.FindProperty("m_" + pascal);
                if (prop != null) return prop;
            }

            string lower = name.Replace("_", "").ToLowerInvariant();
            var iter = so.GetIterator();
            iter.Next(true);
            while (iter.NextVisible(false))
            {
                string iterLower = iter.name.Replace("_", "").Replace("m_", "").ToLowerInvariant();
                if (iterLower == lower || iter.name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    return so.FindProperty(iter.name);
            }
            return null;
        }

        /// <summary>
        /// Fuzzy-find a child property within a parent SerializedProperty.
        /// </summary>
        private static SerializedProperty FindNestedPropertyFuzzy(SerializedProperty parent, string childName)
        {
            // Direct child lookup
            var child = parent.FindPropertyRelative(childName);
            if (child != null) return child;

            if (!childName.StartsWith("m_"))
            {
                child = parent.FindPropertyRelative("m_" + childName);
                if (child != null) return child;
                string pascal = char.ToUpperInvariant(childName[0]) + childName.Substring(1);
                child = parent.FindPropertyRelative("m_" + pascal);
                if (child != null) return child;
            }

            // Case-insensitive scan of child properties
            string lower = childName.Replace("_", "").ToLowerInvariant();
            var iter = parent.Copy();
            int depth = iter.depth;
            if (iter.Next(true))
            {
                do
                {
                    if (iter.depth != depth + 1) continue;
                    string iterLower = iter.name.Replace("_", "").Replace("m_", "").ToLowerInvariant();
                    if (iterLower == lower || iter.name.Equals(childName, StringComparison.OrdinalIgnoreCase))
                        return parent.FindPropertyRelative(iter.name);
                } while (iter.Next(false) && iter.depth > depth);
            }
            return null;
        }

        internal static Type ResolveComponentType(string typeName)
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

        internal static string ValidateAddableComponentType(GameObject target, Type componentType, string requestedTypeName)
        {
            if (componentType == null)
                return $"Unknown component type: {requestedTypeName}";

            if (!typeof(Component).IsAssignableFrom(componentType))
                return $"Type '{requestedTypeName}' is not a Unity component";

            if (componentType.IsAbstract)
                return $"Component type '{requestedTypeName}' is abstract and cannot be added";

            if (componentType.IsGenericTypeDefinition || componentType.ContainsGenericParameters)
                return $"Component type '{requestedTypeName}' is an open generic type and cannot be added";

            if (typeof(Transform).IsAssignableFrom(componentType))
                return "Transform already exists on every GameObject and cannot be added";

            if (target != null &&
                Attribute.IsDefined(componentType, typeof(DisallowMultipleComponent)) &&
                target.GetComponent(componentType) != null)
            {
                return $"Component '{requestedTypeName}' does not allow multiple instances on '{target.name}'";
            }

            return null;
        }

        private static JToken SerializedPropertyToToken(SerializedProperty prop)
        {
            // Handle arrays
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
                case SerializedPropertyType.Vector2:
                    var v2 = prop.vector2Value;
                    return new JObject { ["x"] = v2.x, ["y"] = v2.y };
                case SerializedPropertyType.Vector3:
                    var v3 = prop.vector3Value;
                    return new JObject { ["x"] = v3.x, ["y"] = v3.y, ["z"] = v3.z };
                case SerializedPropertyType.Vector4:
                    var v4 = prop.vector4Value;
                    return new JObject { ["x"] = v4.x, ["y"] = v4.y, ["z"] = v4.z, ["w"] = v4.w };
                case SerializedPropertyType.Quaternion:
                    var q = prop.quaternionValue;
                    return new JObject { ["x"] = q.x, ["y"] = q.y, ["z"] = q.z, ["w"] = q.w };
                case SerializedPropertyType.Rect:
                    var r = prop.rectValue;
                    return new JObject { ["x"] = r.x, ["y"] = r.y, ["width"] = r.width, ["height"] = r.height };
                case SerializedPropertyType.Bounds:
                    var b = prop.boundsValue;
                    return new JObject
                    {
                        ["center"] = new JObject { ["x"] = b.center.x, ["y"] = b.center.y, ["z"] = b.center.z },
                        ["size"] = new JObject { ["x"] = b.size.x, ["y"] = b.size.y, ["z"] = b.size.z }
                    };
                case SerializedPropertyType.Color:
                    var c = prop.colorValue;
                    return new JObject { ["r"] = c.r, ["g"] = c.g, ["b"] = c.b, ["a"] = c.a };
                case SerializedPropertyType.ObjectReference:
                    if (prop.objectReferenceValue == null) return null;
                    var assetPath = AssetDatabase.GetAssetPath(prop.objectReferenceValue);
                    return !string.IsNullOrEmpty(assetPath) ? assetPath : prop.objectReferenceValue.name;
                default:
                    return SerializedPropertyToObjectToken(prop);
            }
        }

        /// <summary>
        /// Expands a Generic/complex SerializedProperty into a JObject of its child properties.
        /// Limited to one level of depth to avoid excessive output.
        /// </summary>
        private static JToken SerializedPropertyToObjectToken(SerializedProperty prop)
        {
            var obj = new JObject();
            var iter = prop.Copy();
            int parentDepth = iter.depth;
            if (!iter.Next(true)) return prop.propertyType.ToString();

            int count = 0;
            const int maxChildren = 50;
            do
            {
                if (iter.depth <= parentDepth) break;
                // Only include direct children (depth == parentDepth + 1)
                if (iter.depth == parentDepth + 1)
                {
                    obj[iter.name] = SerializedPropertyToToken(iter);
                    if (++count >= maxChildren) break;
                }
            } while (iter.Next(false));

            return obj.Count > 0 ? obj : prop.propertyType.ToString();
        }

        internal static void SetSerializedProperty(SerializedProperty prop, JToken value)
        {
            // Handle arrays: value should be a JArray, or a single value wrapped into one
            if (prop.isArray && prop.propertyType != SerializedPropertyType.String)
            {
                var arr = value is JArray ja ? ja : new JArray { value };
                // Only resize if the new array is larger; preserve existing slots
                // (e.g. Renderer.m_Materials should keep its submesh-count-based size)
                if (arr.Count > prop.arraySize)
                    prop.arraySize = arr.Count;
                for (int i = 0; i < arr.Count; i++)
                    SetSerializedProperty(prop.GetArrayElementAtIndex(i), arr[i]);
                return;
            }

            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer:
                    prop.intValue = value.ToObject<int>();
                    break;
                case SerializedPropertyType.Boolean:
                    prop.boolValue = value.ToObject<bool>();
                    break;
                case SerializedPropertyType.Float:
                    prop.floatValue = value.ToObject<float>();
                    break;
                case SerializedPropertyType.String:
                    prop.stringValue = value.ToObject<string>();
                    break;
                case SerializedPropertyType.Enum:
                    var enumName = value.ToObject<string>();
                    var idx = System.Array.IndexOf(prop.enumNames, enumName);
                    if (idx >= 0) prop.enumValueIndex = idx;
                    break;
                case SerializedPropertyType.Vector2:
                    prop.vector2Value = new Vector2(
                        value["x"]?.ToObject<float>() ?? 0f,
                        value["y"]?.ToObject<float>() ?? 0f);
                    break;
                case SerializedPropertyType.Vector3:
                    prop.vector3Value = new Vector3(
                        value["x"]?.ToObject<float>() ?? 0f,
                        value["y"]?.ToObject<float>() ?? 0f,
                        value["z"]?.ToObject<float>() ?? 0f);
                    break;
                case SerializedPropertyType.Vector4:
                    prop.vector4Value = new Vector4(
                        value["x"]?.ToObject<float>() ?? 0f,
                        value["y"]?.ToObject<float>() ?? 0f,
                        value["z"]?.ToObject<float>() ?? 0f,
                        value["w"]?.ToObject<float>() ?? 0f);
                    break;
                case SerializedPropertyType.Quaternion:
                    prop.quaternionValue = new Quaternion(
                        value["x"]?.ToObject<float>() ?? 0f,
                        value["y"]?.ToObject<float>() ?? 0f,
                        value["z"]?.ToObject<float>() ?? 0f,
                        value["w"]?.ToObject<float>() ?? 1f);
                    break;
                case SerializedPropertyType.Rect:
                    prop.rectValue = new Rect(
                        value["x"]?.ToObject<float>() ?? 0f,
                        value["y"]?.ToObject<float>() ?? 0f,
                        value["width"]?.ToObject<float>() ?? 0f,
                        value["height"]?.ToObject<float>() ?? 0f);
                    break;
                case SerializedPropertyType.Bounds:
                    prop.boundsValue = new Bounds(
                        new Vector3(
                            value["center"]?["x"]?.ToObject<float>() ?? 0f,
                            value["center"]?["y"]?.ToObject<float>() ?? 0f,
                            value["center"]?["z"]?.ToObject<float>() ?? 0f),
                        new Vector3(
                            value["size"]?["x"]?.ToObject<float>() ?? 0f,
                            value["size"]?["y"]?.ToObject<float>() ?? 0f,
                            value["size"]?["z"]?.ToObject<float>() ?? 0f));
                    break;
                case SerializedPropertyType.Color:
                    prop.colorValue = new Color(
                        value["r"]?.ToObject<float>() ?? 0f,
                        value["g"]?.ToObject<float>() ?? 0f,
                        value["b"]?.ToObject<float>() ?? 0f,
                        value["a"]?.ToObject<float>() ?? 1f);
                    break;
                case SerializedPropertyType.ObjectReference:
                    var path = value.ToObject<string>();
                    if (string.IsNullOrEmpty(path))
                    {
                        prop.objectReferenceValue = null;
                    }
                    else
                    {
                        var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                        if (asset != null)
                        {
                            // Try direct assignment first
                            prop.objectReferenceValue = asset;
                            // If the property rejected it (e.g. expects Sprite but got Texture2D),
                            // search sub-assets for a compatible type
                            if (prop.objectReferenceValue == null)
                            {
                                var allAssets = AssetDatabase.LoadAllAssetsAtPath(path);
                                foreach (var sub in allAssets)
                                {
                                    if (sub == asset) continue;
                                    prop.objectReferenceValue = sub;
                                    if (prop.objectReferenceValue != null) break;
                                }
                            }
                        }
                    }
                    break;
            }
        }
    }
}

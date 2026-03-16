using System;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityMcp.Editor.Utils;
using UnityMcp.Shared.Attributes;
using UnityMcp.Shared.Models;
using UnityMcp.Shared.Utils;

namespace UnityMcp.Editor.Tools
{
    [McpToolGroup("Prefab")]
    public static class PrefabTools
    {
        [McpTool("prefab_create", "Create a Prefab from an existing GameObject in the scene",
            Group = "prefab")]
        public static ToolResult Create(
            [Desc("Name or path of the source GameObject")] string target,
            [Desc("Save path (e.g. Assets/Prefabs/Player.prefab)")] string path,
            [Desc("Instance ID of the source GameObject")] int? instanceId = null)
        {
            var pv = PathValidator.QuickValidate(path);
            if (!pv.IsValid) return ToolResult.Error(pv.Error);

            var go = GameObjectTools.FindGameObject(target, instanceId);
            if (go == null)
                return ToolResult.Error($"GameObject not found: {target ?? instanceId?.ToString()}");

            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !AssetDatabase.IsValidFolder(dir))
            {
                Directory.CreateDirectory(dir);
                AssetDatabase.Refresh();
            }

            var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(
                go, path, InteractionMode.UserAction);

            return ToolResult.Json(new
            {
                path,
                name = prefab.name,
                instanceId = prefab.GetInstanceID(),
                message = $"Created prefab: {path}"
            });
        }

        [McpTool("prefab_instantiate", "Instantiate a Prefab into the scene",
            Group = "prefab")]
        public static ToolResult Instantiate(
            [Desc("Prefab asset path (e.g. Assets/Prefabs/Player.prefab)")] string path,
            [Desc("World position")] Vector3? position = null,
            [Desc("Rotation euler angles")] Vector3? rotation = null,
            [Desc("Parent GameObject name or path")] string parent = null)
        {
            var pv = PathValidator.QuickValidate(path);
            if (!pv.IsValid) return ToolResult.Error(pv.Error);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
                return ToolResult.Error($"Prefab not found: {path}");

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (instance == null)
                return ToolResult.Error($"Failed to instantiate prefab: {path}");

            if (position.HasValue) instance.transform.position = position.Value;
            if (rotation.HasValue) instance.transform.eulerAngles = rotation.Value;

            if (!string.IsNullOrEmpty(parent))
            {
                var parentGo = GameObjectTools.FindGameObject(parent, null);
                if (parentGo != null)
                    instance.transform.SetParent(parentGo.transform, true);
            }

            UndoHelper.RegisterCreatedObject(instance, $"Instantiate {prefab.name}");

            return ToolResult.Json(new
            {
                instanceId = instance.GetInstanceID(),
                name = instance.name,
                prefabPath = path,
                message = $"Instantiated '{prefab.name}'"
            });
        }

        [McpTool("prefab_open", "Open a Prefab in Prefab edit mode",
            Group = "prefab")]
        public static ToolResult Open(
            [Desc("Prefab asset path")] string path)
        {
            var pv = PathValidator.QuickValidate(path);
            if (!pv.IsValid) return ToolResult.Error(pv.Error);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
                return ToolResult.Error($"Prefab not found: {path}");

            AssetDatabase.OpenAsset(prefab);
            return ToolResult.Text($"Opened prefab: {path}");
        }

        [McpTool("prefab_close", "Close the currently open Prefab edit mode",
            Group = "prefab")]
        public static ToolResult SaveClose(
            [Desc("Save changes before closing (default true)")] bool save = true)
        {
            var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            if (stage == null)
                return ToolResult.Error("No prefab is currently open for editing");

            var prefabPath = stage.assetPath;

            if (!save)
            {
                // Discard changes by clearing dirty flag before exiting
                var prefabRoot = stage.prefabContentsRoot;
                if (prefabRoot != null)
                    EditorUtility.ClearDirty(prefabRoot);
            }

            UnityEditor.SceneManagement.StageUtility.GoToMainStage();

            return save
                ? ToolResult.Text($"Saved and closed prefab: {prefabPath}")
                : ToolResult.Text($"Closed prefab without saving: {prefabPath}");
        }

        [McpTool("prefab_get_hierarchy", "Get the hierarchy of objects inside a Prefab asset (without opening it in the editor). Returns names, paths, active state, and components for each object.",
            Group = "prefab", ReadOnly = true)]
        public static ToolResult GetHierarchy(
            [Desc("Prefab asset path (e.g. Assets/Prefabs/Player.prefab)")] string path)
        {
            var pv = PathValidator.QuickValidate(path);
            if (!pv.IsValid) return ToolResult.Error(pv.Error);

            var contents = PrefabUtility.LoadPrefabContents(path);
            if (contents == null)
                return ToolResult.Error($"Failed to load prefab: {path}");

            try
            {
                var items = new System.Collections.Generic.List<object>();
                CollectHierarchy(contents.transform, "", items);

                return ToolResult.Json(new
                {
                    prefabPath = path,
                    total = items.Count,
                    items
                });
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        [McpTool("prefab_get_stage_objects", "Get the hierarchy of objects in the currently open Prefab Stage (Prefab edit mode). Use after prefab_open.",
            Group = "prefab", ReadOnly = true)]
        public static ToolResult GetStageObjects()
        {
            var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            if (stage == null)
                return ToolResult.Error("No prefab is currently open for editing. Use prefab_open first.");

            var root = stage.prefabContentsRoot;
            var items = new System.Collections.Generic.List<object>();
            CollectHierarchy(root.transform, "", items);

            return ToolResult.Json(new
            {
                prefabPath = stage.assetPath,
                total = items.Count,
                items
            });
        }

        private static void CollectHierarchy(Transform transform, string parentPath, System.Collections.Generic.List<object> items)
        {
            string currentPath = string.IsNullOrEmpty(parentPath) ? transform.name : parentPath + "/" + transform.name;

            var components = new System.Collections.Generic.List<string>();
            foreach (var c in transform.GetComponents<Component>())
            {
                if (c != null && !(c is Transform))
                    components.Add(c.GetType().Name);
            }

            items.Add(new
            {
                name = transform.name,
                path = currentPath,
                active = transform.gameObject.activeSelf,
                components,
                childCount = transform.childCount
            });

            for (int i = 0; i < transform.childCount; i++)
                CollectHierarchy(transform.GetChild(i), currentPath, items);
        }

        [McpTool("prefab_modify_contents",
            "Modify a Prefab asset's contents without opening Prefab Stage (headless). " +
            "Supports: rename, set active, transform (position/rotation/scale), " +
            "add/remove components, and modify component properties via componentProperties. " +
            "Target defaults to root object; use name or path (e.g. 'Child' or 'Parent/Child') to target a child.",
            Group = "prefab")]
        public static ToolResult ModifyContents(
            [Desc("Prefab asset path (e.g. Assets/Prefabs/Player.prefab)")] string path,
            [Desc("Target object name or path within the prefab (defaults to root)")] string target = null,
            [Desc("New name for the target object")] string name = null,
            [Desc("Set active state")] bool? setActive = null,
            [Desc("Local position")] Vector3? position = null,
            [Desc("Local rotation euler angles")] Vector3? rotation = null,
            [Desc("Local scale")] Vector3? scale = null,
            [Desc("Component type names to add (e.g. [\"BoxCollider\", \"AudioSource\"])")] string[] componentsToAdd = null,
            [Desc("Component type names to remove")] string[] componentsToRemove = null,
            [Desc("Component properties to set: {\"ComponentType\": {\"fieldName\": value}}. Field names support fuzzy matching (e.g. 'sprite' → 'm_Sprite').")] JObject componentProperties = null)
        {
            var pv = PathValidator.QuickValidate(path);
            if (!pv.IsValid) return ToolResult.Error(pv.Error);

            var prefabContents = PrefabUtility.LoadPrefabContents(path);
            if (prefabContents == null)
                return ToolResult.Error($"Failed to load prefab: {path}");

            try
            {
                var targetGo = FindInPrefabContents(prefabContents, target);
                if (targetGo == null)
                {
                    string searched = string.IsNullOrEmpty(target) ? "root" : $"'{target}'";
                    return ToolResult.Error($"Target {searched} not found in prefab '{path}'");
                }

                bool modified = false;
                var errors = new JArray();

                // Name
                if (!string.IsNullOrEmpty(name) && targetGo.name != name)
                {
                    targetGo.name = name;
                    modified = true;
                }

                // Active state
                if (setActive.HasValue && targetGo.activeSelf != setActive.Value)
                {
                    targetGo.SetActive(setActive.Value);
                    modified = true;
                }

                // Transform
                if (position.HasValue)
                { targetGo.transform.localPosition = position.Value; modified = true; }
                if (rotation.HasValue)
                { targetGo.transform.localEulerAngles = rotation.Value; modified = true; }
                if (scale.HasValue)
                { targetGo.transform.localScale = scale.Value; modified = true; }

                // Add components
                if (componentsToAdd != null)
                {
                    foreach (var typeName in componentsToAdd)
                    {
                        var compType = ComponentTools.ResolveComponentType(typeName);
                        if (compType == null)
                        { errors.Add($"Unknown component type: {typeName}"); continue; }
                        targetGo.AddComponent(compType);
                        modified = true;
                    }
                }

                // Remove components
                if (componentsToRemove != null)
                {
                    foreach (var typeName in componentsToRemove)
                    {
                        var compType = ComponentTools.ResolveComponentType(typeName);
                        if (compType == null)
                        { errors.Add($"Unknown component type: {typeName}"); continue; }
                        var comp = targetGo.GetComponent(compType);
                        if (comp != null)
                        { Undo.DestroyObjectImmediate(comp); modified = true; }
                        else
                        { errors.Add($"Component '{typeName}' not found on '{targetGo.name}'"); }
                    }
                }

                // Modify component properties
                if (componentProperties != null)
                {
                    foreach (var entry in componentProperties)
                    {
                        var compType = ComponentTools.ResolveComponentType(entry.Key);
                        if (compType == null)
                        { errors.Add($"Unknown component type: {entry.Key}"); continue; }

                        var comp = targetGo.GetComponent(compType);
                        if (comp == null)
                        { errors.Add($"Component '{entry.Key}' not found on '{targetGo.name}'"); continue; }

                        if (entry.Value is not JObject props || !props.HasValues) continue;

                        var so = new SerializedObject(comp);
                        foreach (var prop in props)
                        {
                            var sp = ComponentTools.FindPropertyFuzzy(so, prop.Key);
                            if (sp == null)
                            { errors.Add($"{entry.Key}.{prop.Key}: field not found"); continue; }
                            ComponentTools.SetSerializedProperty(sp, prop.Value);
                            modified = true;
                        }
                        so.ApplyModifiedProperties();
                    }
                }

                if (!modified)
                {
                    return ToolResult.Json(new
                    {
                        prefabPath = path,
                        target = targetGo.name,
                        modified = false,
                        message = "No changes needed"
                    });
                }

                // Save
                bool success;
                PrefabUtility.SaveAsPrefabAsset(prefabContents, path, out success);
                if (!success)
                    return ToolResult.Error($"Failed to save prefab: {path}");

                var result = new JObject
                {
                    ["success"] = true,
                    ["prefabPath"] = path,
                    ["target"] = targetGo.name,
                    ["modified"] = true,
                    ["message"] = $"Modified prefab '{path}' (target: {targetGo.name})"
                };
                if (errors.Count > 0)
                    result["errors"] = errors;

                return ToolResult.Json(result);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabContents);
            }
        }

        private static GameObject FindInPrefabContents(GameObject prefabContents, string target)
        {
            if (string.IsNullOrEmpty(target))
                return prefabContents;

            // Try path first (e.g. "Parent/Child")
            if (target.Contains("/"))
            {
                var found = prefabContents.transform.Find(target);
                if (found != null) return found.gameObject;

                // If path starts with root name, try without it
                if (target.StartsWith(prefabContents.name + "/"))
                {
                    found = prefabContents.transform.Find(target.Substring(prefabContents.name.Length + 1));
                    if (found != null) return found.gameObject;
                }
            }

            // Check root name
            if (prefabContents.name == target)
                return prefabContents;

            // Search by name in hierarchy
            foreach (Transform t in prefabContents.GetComponentsInChildren<Transform>(true))
            {
                if (t.gameObject.name == target)
                    return t.gameObject;
            }

            return null;
        }

        [McpTool("prefab_apply_overrides", "Apply all property overrides on a Prefab instance back to the source Prefab asset",
            Group = "prefab")]
        public static ToolResult ApplyOverrides(
            [Desc("Name or path of the Prefab instance in scene")] string target,
            [Desc("Instance ID")] int? instanceId = null)
        {
            var go = GameObjectTools.FindGameObject(target, instanceId);
            if (go == null)
                return ToolResult.Error($"GameObject not found: {target ?? instanceId?.ToString()}");

            if (!PrefabUtility.IsPartOfAnyPrefab(go))
                return ToolResult.Error($"'{go.name}' is not a Prefab instance");

            var root = PrefabUtility.GetOutermostPrefabInstanceRoot(go);
            if (root == null) root = go;

            PrefabUtility.ApplyPrefabInstance(root, InteractionMode.UserAction);
            var assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root);
            return ToolResult.Text($"Applied overrides from '{root.name}' to prefab '{assetPath}'");
        }

        [McpTool("prefab_revert_overrides", "Revert all property overrides on a Prefab instance back to match the source Prefab asset",
            Group = "prefab")]
        public static ToolResult RevertOverrides(
            [Desc("Name or path of the Prefab instance in scene")] string target,
            [Desc("Instance ID")] int? instanceId = null)
        {
            var go = GameObjectTools.FindGameObject(target, instanceId);
            if (go == null)
                return ToolResult.Error($"GameObject not found: {target ?? instanceId?.ToString()}");

            if (!PrefabUtility.IsPartOfAnyPrefab(go))
                return ToolResult.Error($"'{go.name}' is not a Prefab instance");

            var root = PrefabUtility.GetOutermostPrefabInstanceRoot(go);
            if (root == null) root = go;

            PrefabUtility.RevertPrefabInstance(root, InteractionMode.UserAction);
            return ToolResult.Text($"Reverted all overrides on '{root.name}'");
        }

        [McpTool("prefab_unpack", "Unpack a Prefab instance in the scene",
            Group = "prefab")]
        public static ToolResult Unpack(
            [Desc("Name or path of the Prefab instance in scene")] string target,
            [Desc("Instance ID")] int? instanceId = null,
            [Desc("Unpack completely (true) or just the outermost root (false)")] bool completely = false)
        {
            var go = GameObjectTools.FindGameObject(target, instanceId);
            if (go == null)
                return ToolResult.Error($"GameObject not found: {target ?? instanceId?.ToString()}");

            if (!PrefabUtility.IsPartOfAnyPrefab(go))
                return ToolResult.Error($"'{go.name}' is not a Prefab instance");

            Undo.RegisterFullObjectHierarchyUndo(go, "Unpack Prefab");

            if (completely)
                PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.UserAction);
            else
                PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.OutermostRoot, InteractionMode.UserAction);

            return ToolResult.Text($"Unpacked prefab '{go.name}' ({(completely ? "completely" : "outermost root")})");
        }
    }
}

using System.IO;
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
                success = true,
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
                success = true,
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

        [McpTool("prefab_save_close", "Save and close the currently open Prefab edit mode",
            Group = "prefab")]
        public static ToolResult SaveClose()
        {
            var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            if (stage == null)
                return ToolResult.Error("No prefab is currently open for editing");

            var prefabPath = stage.assetPath;
            // Save is automatic when exiting prefab mode
            UnityEditor.SceneManagement.StageUtility.GoToMainStage();

            return ToolResult.Text($"Saved and closed prefab: {prefabPath}");
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

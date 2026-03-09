using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityMcp.Shared.Attributes;
using UnityMcp.Shared.Models;

namespace UnityMcp.Editor.Resources
{
    [McpToolGroup("SceneResources")]
    public static class SceneResources
    {
        [McpResource("unity://scene/hierarchy", "Scene Hierarchy",
            "Full hierarchy tree of the active scene")]
        public static ToolResult GetHierarchy()
        {
            var scene = SceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();
            var nodes = roots.Select(r => BuildNode(r.transform, 5, 0)).ToArray();

            return ToolResult.Json(new
            {
                sceneName = scene.name,
                scenePath = scene.path,
                rootCount = roots.Length,
                hierarchy = nodes
            });
        }

        [McpResource("unity://scene/list", "Scene List",
            "All scenes in Build Settings plus currently loaded scenes")]
        public static ToolResult GetSceneList()
        {
            var buildScenes = EditorBuildSettings.scenes.Select((s, i) => new
            {
                index = i,
                path = s.path,
                name = Path.GetFileNameWithoutExtension(s.path),
                enabled = s.enabled,
            }).ToArray();

            var loaded = new List<object>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                loaded.Add(new { name = s.name, path = s.path, isLoaded = s.isLoaded });
            }

            return ToolResult.Json(new
            {
                activeScene = SceneManager.GetActiveScene().path,
                buildScenes,
                loadedScenes = loaded
            });
        }

        private static object BuildNode(Transform t, int maxDepth, int depth)
        {
            var node = new Dictionary<string, object>
            {
                ["name"] = t.name,
                ["instanceId"] = t.gameObject.GetInstanceID(),
                ["active"] = t.gameObject.activeSelf,
                ["childCount"] = t.childCount,
            };

            if (depth < maxDepth)
            {
                var children = new List<object>();
                for (int i = 0; i < t.childCount; i++)
                    children.Add(BuildNode(t.GetChild(i), maxDepth, depth + 1));
                node["children"] = children;
            }

            return node;
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityMcp.Shared.Attributes;
using UnityMcp.Shared.Models;

namespace UnityMcp.Editor.Resources
{
    [McpToolGroup("EditorMetaResources")]
    public static class EditorMetaResources
    {
        [McpResource("unity://tags", "Tag List",
            "All tags defined in the project")]
        public static ToolResult GetTags()
        {
            return ToolResult.Json(new { tags = InternalEditorUtility.tags });
        }

        [McpResource("unity://layers", "Layer List",
            "All layers defined in the project")]
        public static ToolResult GetLayers()
        {
            var layers = new List<object>();
            for (int i = 0; i < 32; i++)
            {
                string name = LayerMask.LayerToName(i);
                if (!string.IsNullOrEmpty(name))
                    layers.Add(new { index = i, name });
            }
            return ToolResult.Json(new { layers });
        }

        [McpResource("unity://menu/items", "Menu Items",
            "Common Unity Editor menu item paths")]
        public static ToolResult GetMenuItems()
        {
            var items = new[]
            {
                "File/New Scene", "File/Open Scene", "File/Save", "File/Save As...",
                "File/Build Settings...", "File/Build And Run",
                "Edit/Undo", "Edit/Redo", "Edit/Select All", "Edit/Deselect All",
                "Edit/Play", "Edit/Pause", "Edit/Step",
                "Edit/Project Settings...", "Edit/Preferences...",
                "Assets/Create/Folder", "Assets/Create/C# Script",
                "Assets/Create/Material", "Assets/Create/Shader/Standard Surface Shader",
                "Assets/Refresh",
                "GameObject/Create Empty", "GameObject/Create Empty Child",
                "GameObject/3D Object/Cube", "GameObject/3D Object/Sphere",
                "GameObject/3D Object/Plane", "GameObject/Light/Directional Light",
                "GameObject/Camera", "GameObject/UI/Canvas", "GameObject/UI/Text - TextMeshPro",
                "Component/Physics/Rigidbody", "Component/Physics/Box Collider",
                "Window/General/Console", "Window/General/Inspector",
                "Window/General/Scene", "Window/General/Game",
                "Window/General/Hierarchy", "Window/General/Project",
            };

            return ToolResult.Json(new { count = items.Length, menuItems = items });
        }
    }
}
